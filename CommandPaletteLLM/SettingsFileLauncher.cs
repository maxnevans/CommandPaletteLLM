using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace CommandPaletteLLM;

internal interface ISettingsFileLauncher
{
    void Reveal(string filePath);

    void Open(string filePath);

    bool PickAndImportFile(string destinationPath);

    string? PickExportFile();
}

internal sealed class SettingsFileLauncher : ISettingsFileLauncher
{
    internal static string GetShellVisiblePath(string filePath)
    {
        try
        {
            return GetShellVisiblePath(
                filePath,
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                ApplicationData.Current.LocalCacheFolder.Path);
        }
        catch (InvalidOperationException)
        {
            return filePath;
        }
        catch (COMException)
        {
            return filePath;
        }
    }

    internal static string GetShellVisiblePath(
        string filePath,
        string localApplicationDataPath,
        string packageLocalCachePath)
    {
        var fullFilePath = Path.GetFullPath(filePath);
        var localApplicationDataRoot = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(localApplicationDataPath));
        var localApplicationDataPrefix = $"{localApplicationDataRoot}{Path.DirectorySeparatorChar}";
        if (!fullFilePath.StartsWith(localApplicationDataPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return fullFilePath;
        }

        var relativePath = Path.GetRelativePath(localApplicationDataRoot, fullFilePath);
        return Path.Combine(packageLocalCachePath, "Local", relativePath);
    }

    internal static ProcessStartInfo CreateRevealStartInfo(string filePath) => new()
    {
        FileName = Path.GetDirectoryName(filePath) ?? filePath,
        UseShellExecute = true,
    };

    public void Reveal(string filePath) => Process.Start(CreateRevealStartInfo(filePath));

    public void Open(string filePath) => Process.Start(new ProcessStartInfo
    {
        FileName = filePath,
        UseShellExecute = true,
    });

    public bool PickAndImportFile(string destinationPath)
    {
        var owner = GetForegroundWindow();
        return Task.Run(async () =>
        {
            var picker = new FileOpenPicker
            {
                CommitButtonText = "Import",
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                ViewMode = PickerViewMode.List,
            };
            picker.FileTypeFilter.Add(".json");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, owner);
            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                return false;
            }

            var directoryPath = Path.GetDirectoryName(destinationPath);
            if (string.IsNullOrEmpty(directoryPath))
            {
                throw new InvalidOperationException("The settings storage folder could not be found.");
            }

            Directory.CreateDirectory(directoryPath);
            BackupExistingFile(destinationPath, DateTime.Now);
            var destinationFolder = await StorageFolder.GetFolderFromPathAsync(directoryPath);
            await file.CopyAsync(
                destinationFolder,
                Path.GetFileName(destinationPath),
                NameCollisionOption.ReplaceExisting);
            return true;
        }).GetAwaiter().GetResult();
    }

    internal static string GetBackupPath(string settingsFilePath, DateTime date) =>
        Path.Combine(
            Path.GetDirectoryName(settingsFilePath) ?? string.Empty,
            $"settings_backup_{date:yyyy-MM-dd}.json");

    internal static void BackupExistingFile(string settingsFilePath, DateTime date)
    {
        if (File.Exists(settingsFilePath))
        {
            File.Move(
                settingsFilePath,
                GetBackupPath(settingsFilePath, date),
                overwrite: true);
        }
    }

    public string? PickExportFile()
    {
        var owner = GetForegroundWindow();
        return Task.Run(async () =>
        {
            var picker = new FileSavePicker
            {
                CommitButtonText = "Export",
                SuggestedFileName = "settings",
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            };
            picker.FileTypeChoices.Add(
                "JSON settings",
                new List<string> { ".json" });
            WinRT.Interop.InitializeWithWindow.Initialize(picker, owner);
            var file = await picker.PickSaveFileAsync();
            return file?.Path;
        }).GetAwaiter().GetResult();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
