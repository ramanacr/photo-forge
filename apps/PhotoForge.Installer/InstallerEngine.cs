using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using Microsoft.Win32;
using PhotoForge.Shell;

namespace PhotoForge.Installer;

public static class InstallerEngine
{
    public static void PerformInstall(
        string targetDir,
        bool createShortcuts,
        bool registerShell,
        bool addToPath,
        Action<string, double>? progressCallback = null)
    {
        progressCallback?.Invoke("Preparing installation directory...", 0.05);
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        // 1. Extract application payload
        progressCallback?.Invoke("Extracting application binaries...", 0.15);
        ExtractPayload(targetDir, progressCallback);

        // 2. Register Shell Context Menu if requested
        if (registerShell)
        {
            progressCallback?.Invoke("Registering Windows Explorer integration...", 0.75);
            try
            {
                var desktopExe = Path.Combine(targetDir, "PhotoForge.Desktop.exe");
                ShellRegistration.Register(desktopExe);
            }
            catch { }
        }

        // 3. Create Start Menu and Desktop Shortcuts
        if (createShortcuts)
        {
            progressCallback?.Invoke("Creating application shortcuts...", 0.85);
            try
            {
                CreateShortcuts(targetDir);
            }
            catch { }
        }

        // 4. Add to User PATH if requested
        if (addToPath)
        {
            try
            {
                AddDirectoryToUserPath(targetDir);
            }
            catch { }
        }

        // 5. Create Windows Add/Remove Programs Uninstall Entry
        progressCallback?.Invoke("Registering uninstaller in Windows...", 0.95);
        try
        {
            CreateUninstaller(targetDir);
        }
        catch { }

        progressCallback?.Invoke("Installation completed successfully!", 1.0);
    }

    private static void ExtractPayload(string targetDir, Action<string, double>? progressCallback)
    {
        var asm = Assembly.GetExecutingAssembly();
        var resourceNames = asm.GetManifestResourceNames();
        var payloadName = resourceNames.FirstOrDefault(n => n.EndsWith("Payload.zip", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(payloadName))
        {
            using var stream = asm.GetManifestResourceStream(payloadName);
            if (stream != null)
            {
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                var totalEntries = archive.Entries.Count;
                int currentEntry = 0;

                foreach (var entry in archive.Entries)
                {
                    currentEntry++;
                    var entryProgress = 0.15 + (0.55 * ((double)currentEntry / Math.Max(1, totalEntries)));
                    progressCallback?.Invoke($"Extracting {entry.Name}...", entryProgress);

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        var dirPath = Path.Combine(targetDir, entry.FullName);
                        Directory.CreateDirectory(dirPath);
                        continue;
                    }

                    var destPath = Path.Combine(targetDir, entry.FullName);
                    var destDir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }

                    entry.ExtractToFile(destPath, overwrite: true);
                }
                return;
            }
        }

        // Development / Sibling fallback: look for PhotoForge.Desktop.exe in current directory
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var directFiles = Directory.GetFiles(baseDir, "*.*", SearchOption.TopDirectoryOnly);
        var filteredFiles = directFiles.Where(f =>
            !Path.GetFileName(f).StartsWith("PhotoForge-Setup-", StringComparison.OrdinalIgnoreCase) &&
            !f.EndsWith("PhotoForge.Installer.exe", StringComparison.OrdinalIgnoreCase)).ToList();

        if (filteredFiles.Count == 0)
        {
            throw new InvalidOperationException("Embedded installer payload was not found inside the setup executable.");
        }

        for (int i = 0; i < filteredFiles.Count; i++)
        {
            var file = filteredFiles[i];
            var progress = 0.15 + (0.55 * ((double)(i + 1) / filteredFiles.Count));
            progressCallback?.Invoke($"Copying {Path.GetFileName(file)}...", progress);

            var dest = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, dest, true);
        }
    }

    private static void CreateShortcuts(string targetDir)
    {
        var desktopExe = Path.Combine(targetDir, "PhotoForge.Desktop.exe");
        if (!File.Exists(desktopExe)) return;

        var startMenuPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            "PhotoForge.lnk");

        var desktopPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "PhotoForge.lnk");

        CreateWindowsShortcut(startMenuPath, desktopExe, "PhotoForge - Metadata Continuity Platform");
        CreateWindowsShortcut(desktopPath, desktopExe, "PhotoForge - Metadata Continuity Platform");
    }

    private static void CreateWindowsShortcut(string shortcutPath, string targetPath, string description)
    {
        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType != null)
            {
                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = targetPath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
                shortcut.Description = description;
                shortcut.Save();
            }
        }
        catch { }
    }

    private static void AddDirectoryToUserPath(string dir)
    {
        using var envKey = Registry.CurrentUser.OpenSubKey("Environment", true);
        if (envKey == null) return;

        var currentPath = envKey.GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? "";
        var paths = currentPath.Split(';', StringSplitOptions.RemoveEmptyEntries);

        if (!Array.Exists(paths, p => string.Equals(p.Trim(), dir.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            var newPath = string.IsNullOrEmpty(currentPath) ? dir : $"{currentPath};{dir}";
            envKey.SetValue("Path", newPath, RegistryValueKind.ExpandString);
        }
    }

    private static void CreateUninstaller(string targetDir)
    {
        var uninstallerBat = Path.Combine(targetDir, "uninstall.cmd");
        var script = $@"@echo off
echo Uninstalling PhotoForge...
taskkill /F /IM PhotoForge.Desktop.exe >nul 2>&1
taskkill /F /IM photoforge.exe >nul 2>&1

reg delete ""HKCU\Software\Classes\SystemFileAssociations\image\shell\PhotoForge"" /f >nul 2>&1
reg delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\PhotoForge"" /f >nul 2>&1

del /Q ""{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "PhotoForge.lnk")}"" >nul 2>&1
del /Q ""{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "PhotoForge.lnk")}"" >nul 2>&1

timeout /t 2 /nobreak >nul
cd ..
rmdir /S /Q ""{targetDir}"" >nul 2>&1
echo PhotoForge has been successfully uninstalled.
";
        File.WriteAllText(uninstallerBat, script);

        using var uninstallKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\PhotoForge");
        if (uninstallKey != null)
        {
            var installerVer = typeof(InstallerEngine).Assembly.GetName().Version;
            var verString = installerVer != null ? $"{installerVer.Major}.{installerVer.Minor}.{installerVer.Build}" : "1.3.0";

            uninstallKey.SetValue("DisplayName", "PhotoForge (Offline Metadata Continuity Platform)");
            uninstallKey.SetValue("DisplayVersion", verString);
            uninstallKey.SetValue("Publisher", "PhotoForge Team");
            uninstallKey.SetValue("InstallLocation", targetDir);
            uninstallKey.SetValue("UninstallString", $"cmd.exe /c \"{uninstallerBat}\"");
            uninstallKey.SetValue("DisplayIcon", Path.Combine(targetDir, "PhotoForge.Desktop.exe"));
            uninstallKey.SetValue("NoModify", 1, RegistryValueKind.DWord);
            uninstallKey.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }
    }
}
