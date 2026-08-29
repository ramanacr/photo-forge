using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace PhotoForge.Shell;

/// <summary>
/// Manages Windows Explorer context menu shell verbs and registry registration.
/// </summary>
[SupportedOSPlatform("windows")]
public static class ShellRegistration
{
    private const string AppKeyName = "PhotoForge";
    private const string ShellKeyPath = @"Software\Classes\SystemFileAssociations\image\shell\PhotoForge";

    public static bool IsRegistered()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(ShellKeyPath);
            return key != null;
        }
        catch
        {
            return false;
        }
    }

    public static void Register(string? executablePath = null)
    {
        executablePath ??= Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(executablePath) || !File.Exists(executablePath))
            return;

        try
        {
            using var rootKey = Registry.CurrentUser.CreateSubKey(ShellKeyPath);
            if (rootKey == null) return;

            rootKey.SetValue("MUIVerb", "PhotoForge");
            rootKey.SetValue("SubCommands", "");
            rootKey.SetValue("Icon", $"\"{executablePath}\",0");

            var shellPath = $@"{ShellKeyPath}\shell";
            using var subShell = Registry.CurrentUser.CreateSubKey(shellPath);
            if (subShell == null) return;

            // 1. Restore Metadata
            using var cmd1 = subShell.CreateSubKey("cmd1_restore");
            cmd1.SetValue("MUIVerb", "Restore Metadata from Original");
            using var cmd1Exec = cmd1.CreateSubKey("command");
            cmd1Exec.SetValue("", $"\"{executablePath}\" restore --edited \"%1\"");

            // 2. Restore + HEIC
            using var cmd2 = subShell.CreateSubKey("cmd2_heic");
            cmd2.SetValue("MUIVerb", "Restore + Convert to HEIC");
            using var cmd2Exec = cmd2.CreateSubKey("command");
            cmd2Exec.SetValue("", $"\"{executablePath}\" convert --input \"%1\" --format heic");

            // 3. Inspect Metadata
            using var cmd3 = subShell.CreateSubKey("cmd3_inspect");
            cmd3.SetValue("MUIVerb", "Inspect Metadata");
            using var cmd3Exec = cmd3.CreateSubKey("command");
            cmd3Exec.SetValue("", $"\"{executablePath}\" inspect --input \"%1\"");

            // 4. Verify
            using var cmd4 = subShell.CreateSubKey("cmd4_verify");
            cmd4.SetValue("MUIVerb", "Verify Migration Status");
            using var cmd4Exec = cmd4.CreateSubKey("command");
            cmd4Exec.SetValue("", $"\"{executablePath}\" verify --input \"%1\"");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to register shell context menu: {ex.Message}");
        }
    }

    public static void Unregister()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(ShellKeyPath, throwOnMissingSubKey: false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to unregister shell context menu: {ex.Message}");
        }
    }
}
