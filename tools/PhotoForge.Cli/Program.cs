using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PhotoForge.Audit;
using PhotoForge.Core.Models;
using PhotoForge.Core.Pipeline;
using PhotoForge.Core.Services;
using PhotoForge.Imaging;
using PhotoForge.Matching;
using PhotoForge.Metadata;
using PhotoForge.Platform;
using PhotoForge.Storage;
using PhotoForge.Storage.Database;
using Spectre.Console;

namespace PhotoForge.Cli;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            PrintHelp();
            return 0;
        }

        bool jsonMode = args.Contains("--json");

        try
        {
            var command = args[0].ToLowerInvariant();

            // Shell integration commands
            if (command == "--register-shell" || (args.Length > 1 && args[1] == "--register-shell"))
            {
                if (OperatingSystem.IsWindows())
                {
                    PhotoForge.Shell.ShellRegistration.Register();
                    if (!jsonMode) AnsiConsole.MarkupLine("[green]Successfully registered PhotoForge context menu in Windows Explorer.[/]");
                    return 0;
                }
            }
            if (command == "--unregister-shell" || (args.Length > 1 && args[1] == "--unregister-shell"))
            {
                if (OperatingSystem.IsWindows())
                {
                    PhotoForge.Shell.ShellRegistration.Unregister();
                    if (!jsonMode) AnsiConsole.MarkupLine("[yellow]Successfully unregistered PhotoForge context menu from Windows Explorer.[/]");
                    return 0;
                }
            }

            // Instantiate services
            var metadataEngine = new MetadataEngine();
            var imageEngine = new ImageEngine();
            var matchingEngine = new MatchingEngine(imageEngine);
            var storageEngine = new StorageEngine();
            using var auditRepo = new AuditDatabase();
            await auditRepo.InitializeAsync();

            var pipeline = new PhotoForgePipeline(metadataEngine, matchingEngine, imageEngine, storageEngine, auditRepo);

            return command switch
            {
                "restore" => await HandleRestoreAsync(args, pipeline, metadataEngine, jsonMode),
                "convert" => await HandleConvertAsync(args, pipeline, imageEngine, jsonMode),
                "verify" => await HandleVerifyAsync(args, pipeline, jsonMode),
                "inspect" => await HandleInspectAsync(args, metadataEngine, imageEngine, jsonMode),
                "match" => await HandleMatchAsync(args, matchingEngine, imageEngine, metadataEngine, storageEngine, jsonMode),
                "batch" => await HandleBatchAsync(args, pipeline, jsonMode),
                "update" => await HandleUpdateAsync(args, jsonMode),
                _ => PrintUnknownCommand(command)
            };
        }
        catch (PhotoForgeException pex)
        {
            if (jsonMode)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { error = pex.Error.UserMessage, category = pex.Error.Category.ToString(), diagnostic = pex.Error.DiagnosticDetails }));
            }
            else
            {
                AnsiConsole.MarkupLine($"[red bold]Error ({pex.Error.Category}):[/] {pex.Error.UserMessage}");
                if (!string.IsNullOrEmpty(pex.Error.DiagnosticDetails))
                    AnsiConsole.MarkupLine($"[grey]{pex.Error.DiagnosticDetails}[/]");
            }
            return (int)pex.Error.Category;
        }
        catch (Exception ex)
        {
            if (jsonMode)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { error = ex.Message, stackTrace = ex.StackTrace }));
            }
            else
            {
                AnsiConsole.MarkupLine($"[red bold]Fatal error:[/] {ex.Message}");
            }
            return 99;
        }
    }

    private static void PrintHelp()
    {
        AnsiConsole.Write(new FigletText("PhotoForge").Color(Color.Cyan1));
        AnsiConsole.MarkupLine("[bold white]Offline-first Photo Metadata Continuity & Format Conversion[/]");
        AnsiConsole.MarkupLine("[grey]Edit freely. Keep everything.[/]");
        Console.WriteLine();
        AnsiConsole.MarkupLine("[bold yellow]USAGE:[/] photoforge <command> [[options]]");
        Console.WriteLine();
        AnsiConsole.MarkupLine("[bold]COMMANDS:[/]");
        AnsiConsole.MarkupLine("  [cyan]restore[/]    Restore original metadata into edited photo(s)");
        AnsiConsole.MarkupLine("  [cyan]convert[/]    Convert photo(s) to HEIC/WebP with metadata continuity");
        AnsiConsole.MarkupLine("  [cyan]verify[/]     Independently verify metadata continuity & file integrity");
        AnsiConsole.MarkupLine("  [cyan]inspect[/]    Inspect all extracted metadata categories");
        AnsiConsole.MarkupLine("  [cyan]match[/]      Find and rank original photo candidates for an edited photo");
        AnsiConsole.MarkupLine("  [cyan]batch[/]      Batch process directories of edited photos against originals");
        AnsiConsole.MarkupLine("  [cyan]update[/]     Check for and apply self-updates from GitHub Releases");
        Console.WriteLine();
        AnsiConsole.MarkupLine("[bold]GLOBAL OPTIONS:[/]");
        AnsiConsole.MarkupLine("  [cyan]--json[/]      Output machine-readable JSON");
        AnsiConsole.MarkupLine("  [cyan]--help, -h[/]  Show this help screen");
    }

    private static int PrintUnknownCommand(string command)
    {
        AnsiConsole.MarkupLine($"[red]Unknown command:[/] '{command}'. Run 'photoforge --help' for usage.");
        return 1;
    }

    private static string? GetOption(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }

    private static bool HasFlag(string[] args, string name) =>
        args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));

    private static async Task<int> HandleRestoreAsync(string[] args, IPhotoForgePipeline pipeline, IMetadataEngine metaEngine, bool jsonMode)
    {
        var original = GetOption(args, "--original") ?? GetOption(args, "-o");
        var edited = GetOption(args, "--edited") ?? GetOption(args, "-e") ?? GetOption(args, "--input") ?? GetOption(args, "-i");
        var output = GetOption(args, "--output");
        var profileName = GetOption(args, "--profile") ?? "standard-v1";
        var gpsMode = GetOption(args, "--gps")?.ToLowerInvariant();
        bool heic = HasFlag(args, "--heic");
        bool overwrite = HasFlag(args, "--overwrite");
        bool dryRun = HasFlag(args, "--dry-run");

        if (string.IsNullOrWhiteSpace(edited))
        {
            if (!jsonMode) AnsiConsole.MarkupLine("[red]Missing required argument: --edited <path>[/]");
            return 1;
        }

        var gpsPolicy = gpsMode switch
        {
            "remove" or "strip" => GpsPrivacyPolicy.Remove,
            "round" => GpsPrivacyPolicy.Round,
            "warn" => GpsPrivacyPolicy.CopyWithWarning,
            _ => GpsPrivacyPolicy.KeepExact
        };

        var profile = new MergeProfile
        {
            Name = profileName,
            GpsPolicy = gpsPolicy,
            OverwriteDestination = overwrite
        };

        if (dryRun)
        {
            if (!jsonMode) AnsiConsole.MarkupLine("[yellow bold][DRY-RUN][/] Simulating metadata merge preview...");
            var origMeta = original != null && File.Exists(original) ? await metaEngine.ExtractMetadataAsync(original) : new MetadataDocument();
            var targetMeta = await metaEngine.ExtractMetadataAsync(edited);
            var diff = metaEngine.ComputeDiff(origMeta, targetMeta, profile);

            if (jsonMode)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { dryRun = true, diff }));
            }
            else
            {
                DisplayDiffTable(diff);
            }
            return 0;
        }

        if (string.IsNullOrWhiteSpace(original))
        {
            if (!jsonMode) AnsiConsole.MarkupLine("[red]Missing required argument: --original <path>[/]");
            return 1;
        }

        var result = await pipeline.ProcessSinglePairAsync(original, edited, output, profile, convertToHeic: heic);

        if (jsonMode)
        {
            Console.WriteLine(AuditExporter.ToJson(result));
        }
        else
        {
            AnsiConsole.MarkupLine($"[green bold]✔ Restore complete![/] Output: [cyan]{result.OutputPath}[/]");
            AnsiConsole.MarkupLine($"Status: [bold]{result.Status}[/] in {result.Duration.TotalMilliseconds:F0}ms");
            DisplayDiffTable(result.Diff);
        }

        return result.Status == OperationStatus.Success || result.Status == OperationStatus.Skipped ? 0 : 1;
    }

    private static async Task<int> HandleConvertAsync(string[] args, IPhotoForgePipeline pipeline, IImageEngine imageEngine, bool jsonMode)
    {
        var input = GetOption(args, "--input") ?? GetOption(args, "-i");
        var output = GetOption(args, "--output") ?? GetOption(args, "-o");
        var format = GetOption(args, "--format") ?? "heic";
        var qualityStr = GetOption(args, "--quality") ?? "high";

        if (string.IsNullOrWhiteSpace(input) || !File.Exists(input))
        {
            if (!jsonMode) AnsiConsole.MarkupLine("[red]Missing or invalid input file: --input <path>[/]");
            return 1;
        }

        var quality = qualityStr.ToLowerInvariant() switch
        {
            "lossless" => ConversionQuality.LosslessWhereSupported,
            "veryhigh" or "max" => ConversionQuality.VeryHigh,
            "balanced" or "medium" => ConversionQuality.Balanced,
            "small" => ConversionQuality.Small,
            _ => ConversionQuality.High
        };

        output ??= Path.Combine(Path.GetDirectoryName(input)!, $"{Path.GetFileNameWithoutExtension(input)}.{format.ToLowerInvariant()}");

        await imageEngine.ConvertToHeicAsync(input, output, quality: quality);
        var verification = await pipeline.VerifyOutputAsync(output);

        if (jsonMode)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { input, output, format, quality = qualityStr, verification }));
        }
        else
        {
            AnsiConsole.MarkupLine($"[green bold]✔ Conversion complete![/] Output: [cyan]{output}[/]");
            AnsiConsole.MarkupLine($"Verified dimensions and container structure: [bold]{(verification.IsValid ? "[green]PASS[/]" : "[red]FAIL[/]")}[/]");
        }

        return verification.IsValid ? 0 : 1;
    }

    private static async Task<int> HandleVerifyAsync(string[] args, IPhotoForgePipeline pipeline, bool jsonMode)
    {
        var input = GetOption(args, "--input") ?? GetOption(args, "-i");
        if (string.IsNullOrWhiteSpace(input) || !File.Exists(input))
        {
            if (!jsonMode) AnsiConsole.MarkupLine("[red]Missing or invalid input file: --input <path>[/]");
            return 1;
        }

        var ver = await pipeline.VerifyOutputAsync(input);

        if (jsonMode)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { input, verification = ver }));
        }
        else
        {
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("[bold]Check[/]");
            table.AddColumn("[bold]Result[/]");

            table.AddRow("Valid Image Structure", ver.CanBeReopened ? "[green]✔ VALID[/]" : "[red]✘ INVALID[/]");
            table.AddRow("Valid Dimensions", ver.HasValidDimensions ? "[green]✔ VALID[/]" : "[red]✘ INVALID[/]");
            table.AddRow("Metadata Present", ver.HasRequiredMetadata ? "[green]✔ YES[/]" : "[yellow]- NO[/]");
            table.AddRow("PhotoForge Marker", ver.HasMigrationMarker ? "[green]✔ FOUND[/]" : "[grey]- NONE[/]");

            AnsiConsole.Write(table);

            if (ver.VerifiedFields.Count > 0)
            {
                AnsiConsole.MarkupLine("[bold green]Verified Attributes:[/]");
                foreach (var f in ver.VerifiedFields)
                    AnsiConsole.MarkupLine($"  • {f}");
            }

            if (ver.Errors.Count > 0)
            {
                AnsiConsole.MarkupLine("[bold red]Errors Found:[/]");
                foreach (var err in ver.Errors)
                    AnsiConsole.MarkupLine($"  • [red]{err}[/]");
            }
        }

        return ver.IsValid ? 0 : 1;
    }

    private static async Task<int> HandleInspectAsync(string[] args, IMetadataEngine metaEngine, IImageEngine imageEngine, bool jsonMode)
    {
        var input = GetOption(args, "--input") ?? GetOption(args, "-i");
        if (string.IsNullOrWhiteSpace(input) || !File.Exists(input))
        {
            if (!jsonMode) AnsiConsole.MarkupLine("[red]Missing or invalid input file: --input <path>[/]");
            return 1;
        }

        var format = imageEngine.SniffFormat(input);
        var dims = await imageEngine.InspectDimensionsAsync(input);
        var meta = await metaEngine.ExtractMetadataAsync(input);

        if (jsonMode)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { file = input, format = format.ToString(), dimensions = dims, metadata = meta }, new JsonSerializerOptions { WriteIndented = true }));
        }
        else
        {
            AnsiConsole.MarkupLine($"[cyan bold]File:[/] {input}");
            AnsiConsole.MarkupLine($"[cyan bold]Format:[/] {format} ({dims.Width}x{dims.Height})");

            var tree = new Tree("[bold white]Metadata Structure[/]");

            var exifNode = tree.AddNode("[yellow]EXIF[/]");
            if (meta.Exif.DateTimeOriginal.HasValue) exifNode.AddNode($"DateTimeOriginal: {meta.Exif.DateTimeOriginal:yyyy-MM-dd HH:mm:ss}");
            if (!string.IsNullOrEmpty(meta.Exif.Camera.Make)) exifNode.AddNode($"Make: {meta.Exif.Camera.Make}");
            if (!string.IsNullOrEmpty(meta.Exif.Camera.Model)) exifNode.AddNode($"Model: {meta.Exif.Camera.Model}");
            if (!string.IsNullOrEmpty(meta.Exif.Camera.LensModel)) exifNode.AddNode($"Lens: {meta.Exif.Camera.LensModel}");
            if (meta.Exif.Exposure.Iso.HasValue) exifNode.AddNode($"ISO: {meta.Exif.Exposure.Iso}");
            if (meta.Exif.Exposure.FNumber.HasValue) exifNode.AddNode($"F-Number: f/{meta.Exif.Exposure.FNumber:F1}");

            var gpsNode = tree.AddNode("[green]GPS Location[/]");
            if (meta.Gps != null)
            {
                gpsNode.AddNode($"Latitude: {meta.Gps.Latitude:F6}");
                gpsNode.AddNode($"Longitude: {meta.Gps.Longitude:F6}");
                if (meta.Gps.AltitudeMeters.HasValue) gpsNode.AddNode($"Altitude: {meta.Gps.AltitudeMeters:F1}m");
            }
            else
            {
                gpsNode.AddNode("[grey](No GPS data present)[/]");
            }

            var iptcNode = tree.AddNode("[blue]IPTC[/]");
            if (meta.Iptc.Keywords.Count > 0) iptcNode.AddNode($"Keywords: {string.Join(", ", meta.Iptc.Keywords)}");
            if (!string.IsNullOrEmpty(meta.Iptc.Caption)) iptcNode.AddNode($"Caption: {meta.Iptc.Caption}");

            var markerNode = tree.AddNode("[magenta]PhotoForge Migration Marker[/]");
            if (meta.Marker != null)
            {
                markerNode.AddNode($"Status: [green]PROCESSED[/]");
                markerNode.AddNode($"Source Fingerprint: {meta.Marker.SourceFingerprint}");
                markerNode.AddNode($"Profile: {meta.Marker.Profile}");
                markerNode.AddNode($"Processed At: {meta.Marker.ProcessedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
            }
            else
            {
                markerNode.AddNode("[grey](Not yet migrated by PhotoForge)[/]");
            }

            AnsiConsole.Write(tree);
        }

        return 0;
    }

    private static async Task<int> HandleMatchAsync(
        string[] args,
        IMatchingEngine matchingEngine,
        IImageEngine imageEngine,
        IMetadataEngine metaEngine,
        IStorageEngine storageEngine,
        bool jsonMode)
    {
        var edited = GetOption(args, "--edited") ?? GetOption(args, "-e");
        var originalsDir = GetOption(args, "--originals") ?? GetOption(args, "-o");

        if (string.IsNullOrWhiteSpace(edited) || !File.Exists(edited))
        {
            if (!jsonMode) AnsiConsole.MarkupLine("[red]Missing or invalid edited file: --edited <path>[/]");
            return 1;
        }
        if (string.IsNullOrWhiteSpace(originalsDir) || !Directory.Exists(originalsDir))
        {
            if (!jsonMode) AnsiConsole.MarkupLine("[red]Missing or invalid originals directory: --originals <path>[/]");
            return 1;
        }

        var tSha = await storageEngine.ComputeFileSha256Async(edited);
        var tFmt = imageEngine.SniffFormat(edited);
        var tDim = await imageEngine.InspectDimensionsAsync(edited);
        var tMeta = await metaEngine.ExtractMetadataAsync(edited);
        var tPhash = await imageEngine.ComputePerceptualHashAsync(edited);
        var targetRef = PhotoRef.Create(edited, tFmt, new FileInfo(edited).Length, tSha, tDim, metadata: tMeta, perceptualHash: tPhash);

        var candidateRefs = new List<PhotoRef>();
        foreach (var file in Directory.EnumerateFiles(originalsDir, "*.*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".tiff" or ".heic" or ".dng")
            {
                var sha = await storageEngine.ComputeFileSha256Async(file);
                var fmt = imageEngine.SniffFormat(file);
                var dim = await imageEngine.InspectDimensionsAsync(file);
                var meta = await metaEngine.ExtractMetadataAsync(file);
                var phash = await imageEngine.ComputePerceptualHashAsync(file);
                candidateRefs.Add(PhotoRef.Create(file, fmt, new FileInfo(file).Length, sha, dim, metadata: meta, perceptualHash: phash));
            }
        }

        var candidates = await matchingEngine.FindCandidatesAsync(targetRef, candidateRefs);

        if (jsonMode)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { target = targetRef.FileName, matches = candidates }, new JsonSerializerOptions { WriteIndented = true }));
        }
        else
        {
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("[bold]Candidate Original[/]");
            table.AddColumn("[bold]Score[/]");
            table.AddColumn("[bold]Decision Band[/]");
            table.AddColumn("[bold]Reasons[/]");

            foreach (var c in candidates.Take(10))
            {
                string bandColor = c.Band switch
                {
                    ConfidenceBand.AutoAccept => "[green]Auto-Accept[/]",
                    ConfidenceBand.Suggested => "[cyan]Suggested[/]",
                    ConfidenceBand.UserReviewRequired => "[yellow]Review Required[/]",
                    _ => "[grey]No Match[/]"
                };

                table.AddRow(
                    c.CandidateRef.FileName,
                    $"{(c.Score * 100):F1}%",
                    bandColor,
                    string.Join("; ", c.Reasons)
                );
            }

            AnsiConsole.Write(table);
        }

        return 0;
    }

    private static async Task<int> HandleBatchAsync(string[] args, IPhotoForgePipeline pipeline, bool jsonMode)
    {
        var inputDir = GetOption(args, "--input") ?? GetOption(args, "-i");
        var originalsDir = GetOption(args, "--originals") ?? GetOption(args, "-o");
        var outputDir = GetOption(args, "--output");
        bool heic = HasFlag(args, "--heic");
        bool autoAccept = HasFlag(args, "--auto-accept");
        var profile = GetOption(args, "--profile") ?? "standard-v1";

        if (string.IsNullOrWhiteSpace(inputDir) || !Directory.Exists(inputDir))
        {
            if (!jsonMode) AnsiConsole.MarkupLine("[red]Missing or invalid input directory: --input <path>[/]");
            return 1;
        }
        if (string.IsNullOrWhiteSpace(originalsDir) || !Directory.Exists(originalsDir))
        {
            if (!jsonMode) AnsiConsole.MarkupLine("[red]Missing or invalid originals directory: --originals <path>[/]");
            return 1;
        }

        outputDir ??= Path.Combine(inputDir, "PhotoForge_Restored");

        var editedFiles = Directory.EnumerateFiles(inputDir, "*.*", SearchOption.TopDirectoryOnly)
            .Where(f => !f.Contains("PhotoForge_Restored"))
            .ToList();
        var originalFiles = Directory.EnumerateFiles(originalsDir, "*.*", SearchOption.AllDirectories).ToList();

        if (!jsonMode) AnsiConsole.MarkupLine($"Processing [cyan]{editedFiles.Count}[/] edited photos against [cyan]{originalFiles.Count}[/] original candidates...");

        var summary = await pipeline.ProcessBatchAsync(
            editedFiles,
            originalFiles,
            outputDir,
            new MergeProfile { Name = profile },
            convertToHeic: heic,
            autoAcceptConfidentMatches: autoAccept);

        if (jsonMode)
        {
            Console.WriteLine(AuditExporter.ToJson(summary));
        }
        else
        {
            AnsiConsole.MarkupLine(AuditExporter.GenerateMarkdownSummary(summary));
        }

        return summary.FailedCount == 0 ? 0 : 1;
    }

    private static void DisplayDiffTable(MetadataDiff diff)
    {
        var table = new Table().Border(TableBorder.Simple);
        table.AddColumn("[bold]Category[/]");
        table.AddColumn("[bold]Count[/]");
        table.AddColumn("[bold]Fields / Details[/]");

        if (diff.CopiedFromOriginal.Count > 0)
            table.AddRow("[green]Copied from Original[/]", diff.CopiedFromOriginal.Count.ToString(), string.Join(", ", diff.CopiedFromOriginal));
        if (diff.PreservedFromTarget.Count > 0)
            table.AddRow("[blue]Preserved from Target[/]", diff.PreservedFromTarget.Count.ToString(), string.Join(", ", diff.PreservedFromTarget));
        if (diff.Skipped.Count > 0)
            table.AddRow("[grey]Skipped[/]", diff.Skipped.Count.ToString(), string.Join(", ", diff.Skipped));
        if (diff.Warnings.Count > 0)
            table.AddRow("[yellow]Warnings[/]", diff.Warnings.Count.ToString(), string.Join("; ", diff.Warnings));

        AnsiConsole.Write(table);
    }

    private static async Task<int> HandleUpdateAsync(string[] args, bool jsonMode)
    {
        var updateService = new GitHubUpdateService();
        bool apply = args.Contains("--apply") || args.Contains("-y");
        bool silent = args.Contains("--silent") || args.Contains("/s");

        if (!jsonMode)
        {
            AnsiConsole.MarkupLine("[bold cyan]Checking for PhotoForge updates on GitHub Releases...[/]");
        }

        var updateInfo = await updateService.CheckForUpdatesAsync("1.0.0");
        if (updateInfo == null)
        {
            if (jsonMode) Console.WriteLine(JsonSerializer.Serialize(new { status = "error", message = "Failed to query GitHub releases." }));
            else AnsiConsole.MarkupLine("[red]Unable to check for updates. Please verify your internet connection or check https://github.com/ramanacr/photo-forge/releases[/]");
            return 1;
        }

        if (!updateInfo.IsUpdateAvailable)
        {
            if (jsonMode)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { status = "up_to_date", currentVersion = updateInfo.CurrentVersion, latestVersion = updateInfo.LatestVersion }));
            }
            else
            {
                AnsiConsole.MarkupLine($"[green]PhotoForge is up to date! (Current version: v{updateInfo.CurrentVersion})[/]");
            }
            return 0;
        }

        if (jsonMode)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                status = "update_available",
                currentVersion = updateInfo.CurrentVersion,
                latestVersion = updateInfo.LatestVersion,
                title = updateInfo.ReleaseTitle,
                downloadUrl = updateInfo.DownloadUrl,
                releaseNotes = updateInfo.ReleaseNotes
            }));
            if (!apply) return 0;
        }
        else
        {
            AnsiConsole.MarkupLine($"[yellow bold]⚡ New version available:[/] [bold green]v{updateInfo.LatestVersion}[/] (current: v{updateInfo.CurrentVersion})");
            AnsiConsole.MarkupLine($"[white]{updateInfo.ReleaseTitle}[/]");
            if (!string.IsNullOrWhiteSpace(updateInfo.ReleaseNotes))
            {
                var panel = new Panel(updateInfo.ReleaseNotes.Trim()) { Header = new PanelHeader("Release Notes"), Border = BoxBorder.Rounded };
                AnsiConsole.Write(panel);
            }

            if (!apply)
            {
                if (!AnsiConsole.Confirm("Would you like to download and install this update now?"))
                {
                    return 0;
                }
            }
        }

        if (!jsonMode) AnsiConsole.MarkupLine("[cyan]Downloading update package...[/]");

        string downloadedFile;
        if (!jsonMode)
        {
            downloadedFile = await AnsiConsole.Progress()
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("[green]Downloading update[/]");
                    var progress = new Progress<double>(p => task.Value = p * 100);
                    return await updateService.DownloadUpdateAsync(updateInfo, progress);
                });
        }
        else
        {
            downloadedFile = await updateService.DownloadUpdateAsync(updateInfo);
        }

        if (!jsonMode) AnsiConsole.MarkupLine("[cyan]Verifying SHA-256 integrity checksum...[/]");
        bool verified = await updateService.VerifyDownloadedUpdateAsync(downloadedFile, updateInfo.ExpectedSha256 ?? "");
        if (!verified)
        {
            if (jsonMode) Console.WriteLine(JsonSerializer.Serialize(new { status = "error", message = "SHA-256 checksum verification failed." }));
            else AnsiConsole.MarkupLine("[red bold]Security Check Failed:[/] Downloaded update SHA-256 hash does not match release manifest!");
            return 1;
        }

        if (!jsonMode) AnsiConsole.MarkupLine("[green]Checksum verified! Launching installer and restarting...[/]");
        updateService.ApplyUpdateAndRestart(downloadedFile, silent);
        return 0;
    }
}
