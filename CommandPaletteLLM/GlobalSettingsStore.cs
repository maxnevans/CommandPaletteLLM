using System;
using System.IO;
using System.Text.Json;

namespace CommandPaletteLLM;

internal sealed class GlobalSettingsStore
{
    private readonly object _sync = new();
    private readonly string? _filePath;
    private readonly SettingsDocumentStore? _settingsDocumentStore;
    private GlobalSettings _settings;

    public GlobalSettingsStore()
        : this(new SettingsDocumentStore())
    {
    }

    internal GlobalSettingsStore(SettingsDocumentStore settingsDocumentStore)
    {
        _settingsDocumentStore = settingsDocumentStore;
        _filePath = null;
        _settings = settingsDocumentStore.GetGlobalSettings();
    }

    internal GlobalSettingsStore(string? filePath)
    {
        _filePath = filePath;
        _settings = Load(filePath);
    }

    public GlobalSettings Get()
    {
        lock (_sync)
        {
            return _settings.Clone();
        }
    }

    public void Replace(GlobalSettings settings)
    {
        var replacement = settings.Clone();
        replacement.AdvancedOutputSystemPrompt ??= string.Empty;

        lock (_sync)
        {
            if (_settingsDocumentStore is not null)
            {
                _settingsDocumentStore.ReplaceGlobalSettings(replacement);
            }
            else
            {
                Save(replacement);
            }

            _settings = replacement;
        }
    }

    public void ResetAdvancedOutputSystemPrompt() => Replace(new GlobalSettings());

    internal SettingsDocumentStore? SettingsDocumentStore => _settingsDocumentStore;

    internal void ReloadFromDocument()
    {
        if (_settingsDocumentStore is null)
        {
            return;
        }

        lock (_sync)
        {
            _settings = _settingsDocumentStore.GetGlobalSettings();
        }
    }

    internal SettingsFileStatus GetFileStatus()
    {
        if (_settingsDocumentStore is not null)
        {
            return _settingsDocumentStore.GetFileStatus();
        }

        lock (_sync)
        {
            if (_filePath is null)
            {
                return new SettingsFileStatus(false, null, null, false);
            }

            if (!File.Exists(_filePath))
            {
                return new SettingsFileStatus(false, null, null, false);
            }

            return new SettingsFileStatus(
                true,
                SettingsFileLauncher.GetShellVisiblePath(_filePath),
                File.GetLastWriteTime(_filePath),
                false);
        }
    }

    internal static GlobalSettings Load(string? filePath)
    {
        if (filePath is null || !File.Exists(filePath))
        {
            return new GlobalSettings();
        }

        try
        {
            var settings = JsonSerializer.Deserialize(
                File.ReadAllText(filePath),
                CommandPaletteJsonContext.Default.GlobalSettings);
            if (settings?.AdvancedOutputSystemPrompt is null)
            {
                return new GlobalSettings();
            }

            if (string.Equals(
                settings.AdvancedOutputSystemPrompt,
                GlobalSettings.LegacyAdvancedOutputSystemPrompt,
                StringComparison.Ordinal))
            {
                settings.AdvancedOutputSystemPrompt =
                    GlobalSettings.DefaultAdvancedOutputSystemPrompt;
            }

            return settings;
        }
        catch (IOException)
        {
            return new GlobalSettings();
        }
        catch (JsonException)
        {
            return new GlobalSettings();
        }
        catch (UnauthorizedAccessException)
        {
            return new GlobalSettings();
        }
    }

    private void Save(GlobalSettings settings)
    {
        if (_filePath is null)
        {
            return;
        }

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = $"{_filePath}.tmp";
        var json = JsonSerializer.Serialize(
            settings,
            CommandPaletteJsonContext.Default.GlobalSettings);
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, _filePath, true);
    }
}
