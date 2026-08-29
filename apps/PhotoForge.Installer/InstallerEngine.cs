using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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
        progressCallback?.Invoke("Preparing installation directory...", 0.10);
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        // 1. Extract application payload
        progressCallback?.Invoke("Extracting application binaries...", 0.30);
        ExtractPayload(targetDir);

        // 2. Register Shell Context Menu if requested
        if (registerShell)
        {
            progressCallback?.Invoke("Registering Windows Explorer integration...", 0.60);
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
            progressCallback?.Invoke("Creating application shortcuts...", 0.80);
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
        progressCallback?.Invoke("Registering uninstaller in Windows...", 0.90);
        CreateUninstaller(targetDir);

        progressCallback?.Invoke("Installation completed successfully!", 1.0);
    }

    private static void ExtractPayload(string targetDir)
    {
        var asm = Assembly.GetExecutingAssembly();
        var resourceName = "PhotoForge.Installer.Payload.zip";

        using var stream = asm.GetManifestResourceStream(resourceName);
        if (stream != null)
        {
            using var archive = new ZipArchive(stream);
            archive.ExtractToDirectory(targetDir, overwriteFiles: true);
        }
        else
        {
            // Fallback: If running in dev/local build, copy sibling output files
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var files = Directory.GetFiles(baseDir, "*.*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                if (file.EndsWith("PhotoForge-Setup-v1.0.0-x64.exe", StringComparison.OrdinalIgnoreCase))
                    continue;

                var rel = Path.GetRelativePath(baseDir, file);
                var dest = Path.Combine(targetDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(file, dest, true);
            }
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

reg delete ""HKCU\Software\Classes\*\shell\PhotoForge"" /f >nul 2>&1
reg delete ""HKCU\Software\Classes\Directory\shell\PhotoForge"" /f >nul 2>&1
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
            uninstallKey.SetValue("DisplayName", "PhotoForge (Offline Metadata Continuity Platform)");
            uninstallKey.SetValue("DisplayVersion", "1.0.0");
            uninstallKey.SetValue("Publisher", "PhotoForge Team");
            uninstallKey.SetValue("InstallLocation", targetDir);
            uninstallKey.SetValue("UninstallString", $"cmd.exe /c \"{uninstallerBat}\"");
            uninstallKey.SetValue("DisplayIcon", Path.Combine(targetDir, "PhotoForge.Desktop.exe"));
            uninstallKey.SetValue("NoModify", 1, RegistryValueKind.DWord);
            uninstallKey.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }
    }
}
