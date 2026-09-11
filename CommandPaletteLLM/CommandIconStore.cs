using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CommandPaletteLLM;

internal sealed class CommandIconStore
{
    private static readonly string[] SupportedExtensions = [".ico", ".png", ".jpg", ".jpeg", ".svg"];
    private readonly string? _directory;

    public CommandIconStore(string? settingsDirectory)
    {
        _directory = settingsDirectory is null ? null : Path.Combine(settingsDirectory, "Icons");
    }

    public bool TryImport(
        string commandId,
        string sourcePath,
        string existingPath,
        out string storedPath,
        out string error)
    {
        sourcePath = sourcePath.Trim().Trim('"');
        if (string.IsNullOrEmpty(sourcePath))
        {
            storedPath = string.Empty;
            error = string.Empty;
            return true;
        }

        if (string.Equals(sourcePath, existingPath, StringComparison.OrdinalIgnoreCase))
        {
            storedPath = existingPath;
            error = string.Empty;
            return true;
        }

        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (Array.IndexOf(SupportedExtensions, extension) < 0)
        {
            storedPath = existingPath;
            error = "Command icons must be ICO, PNG, JPG, JPEG, or SVG files.";
            return false;
        }

        if (!File.Exists(sourcePath))
        {
            storedPath = existingPath;
            error = "The selected command icon file does not exist.";
            return false;
        }

        if (_directory is null)
        {
            storedPath = existingPath;
            error = "Command icons cannot be imported without persistent settings storage.";
            return false;
        }

        Directory.CreateDirectory(_directory);
        var safeName = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(commandId)));
        storedPath = Path.Combine(_directory, $"{safeName}{extension}");
        if (!string.Equals(
            Path.GetFullPath(sourcePath),
            Path.GetFullPath(storedPath),
            StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(sourcePath, storedPath, overwrite: true);
        }

        error = string.Empty;
        return true;
    }

    public static IconInfo GetIcon(string iconPath) =>
        string.IsNullOrWhiteSpace(iconPath) || !File.Exists(iconPath)
            ? IconHelpers.FromRelativePath("Assets\\StoreLogo.png")
            : new IconInfo(iconPath);
}
