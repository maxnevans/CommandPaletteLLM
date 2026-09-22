using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CommandPaletteLLM;

internal sealed class SettingsDocumentStore
{
    private static readonly CommandPaletteJsonContext BeautifulJsonContext = new(
        new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true,
            IndentSize = 4,
        });

    private readonly object _sync = new();
    private readonly string? _filePath;
    private readonly IDataProtector _dataProtector;
    private GlobalSettings _globalSettings;
    private List<LlmProviderSettings> _providers;
    private List<UserCommandDefinition> _commands;
    private bool _beautifulFormatting;

    public SettingsDocumentStore()
        : this(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CommandPaletteLLM",
                "settings.json"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CommandPaletteLLM",
                "commands.json"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CommandPaletteLLM",
                "providers.json"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CommandPaletteLLM",
                "provider.json"),
            new DpapiDataProtector())
    {
    }

    internal SettingsDocumentStore(
        string? filePath,
        string? legacyCommandsPath,
        string? legacyProvidersPath,
        string? legacyProviderPath,
        IDataProtector dataProtector)
    {
        _filePath = filePath;
        _dataProtector = dataProtector;

        var existingSettingsPath = GetExistingFilePath();
        if (TryLoadDocument(existingSettingsPath, out var document, out _beautifulFormatting))
        {
            _globalSettings = NormalizeGlobalSettings(document.Global);
            _providers = NormalizeProviders(
                document.Providers.Select(provider =>
                    LlmProviderSettingsStore.FromStored(provider, dataProtector)));
            _commands = NormalizeCommands(document.Commands);
            return;
        }

        _globalSettings = GlobalSettingsStore.Load(existingSettingsPath ?? filePath);
        _providers = LlmProviderSettingsStore.Load(
            legacyProvidersPath,
            legacyProviderPath,
            dataProtector,
            out _);
        _commands = UserCommandStore.LoadCommands(legacyCommandsPath);

        var hasLegacySettings =
            existingSettingsPath is not null ||
            legacyCommandsPath is not null && File.Exists(legacyCommandsPath) ||
            legacyProvidersPath is not null && File.Exists(legacyProvidersPath) ||
            legacyProviderPath is not null && File.Exists(legacyProviderPath);
        if (hasLegacySettings && _filePath is not null)
        {
            Save();
            DeleteLegacyFile(legacyCommandsPath);
            DeleteLegacyFile(legacyProvidersPath);
            DeleteLegacyFile(legacyProviderPath);
        }
    }

    internal string? StorageDirectory =>
        _filePath is null ? null : Path.GetDirectoryName(_filePath);

    internal string? ImportFilePath =>
        _filePath is null ? null : SettingsFileLauncher.GetShellVisiblePath(_filePath);

    internal GlobalSettings GetGlobalSettings()
    {
        lock (_sync)
        {
            return _globalSettings.Clone();
        }
    }

    internal IReadOnlyList<LlmProviderSettings> GetProviders()
    {
        lock (_sync)
        {
            return _providers.Select(provider => provider.Clone()).ToArray();
        }
    }

    internal IReadOnlyList<UserCommandDefinition> GetCommands()
    {
        lock (_sync)
        {
            return _commands.Select(command => command.Clone()).ToArray();
        }
    }

    internal void ReplaceGlobalSettings(GlobalSettings settings)
    {
        lock (_sync)
        {
            _globalSettings = NormalizeGlobalSettings(settings);
            Save();
        }
    }

    internal void ReplaceProviders(IEnumerable<LlmProviderSettings> providers)
    {
        lock (_sync)
        {
            _providers = NormalizeProviders(providers);
            Save();
        }
    }

    internal void ReplaceCommands(IEnumerable<UserCommandDefinition> commands)
    {
        lock (_sync)
        {
            _commands = commands.Select(command => command.Clone()).ToList();
            Save();
        }
    }

    internal SettingsFileStatus GetFileStatus()
    {
        lock (_sync)
        {
            var existingFilePath = GetExistingFilePath();
            if (existingFilePath is null)
            {
                return new SettingsFileStatus(false, null, null, false);
            }

            return new SettingsFileStatus(
                true,
                string.Equals(existingFilePath, _filePath, StringComparison.OrdinalIgnoreCase)
                    ? SettingsFileLauncher.GetShellVisiblePath(existingFilePath)
                    : existingFilePath,
                File.GetLastWriteTime(existingFilePath),
                _beautifulFormatting);
        }
    }

    internal void ToggleFormatting()
    {
        lock (_sync)
        {
            if (GetExistingFilePath() is null)
            {
                throw new FileNotFoundException("The settings file has not been created yet.");
            }

            _beautifulFormatting = !_beautifulFormatting;
            try
            {
                Save();
            }
            catch
            {
                _beautifulFormatting = !_beautifulFormatting;
                throw;
            }
        }
    }

    internal void Reload()
    {
        lock (_sync)
        {
            var existingFilePath = GetExistingFilePath();
            if (existingFilePath is null)
            {
                _globalSettings = new GlobalSettings();
                _providers = [new LlmProviderSettings()];
                _commands = [];
                _beautifulFormatting = false;
                return;
            }

            if (!TryLoadDocument(existingFilePath, out var document, out var beautifulFormatting))
            {
                throw new InvalidDataException("The current settings.json file is not valid.");
            }

            ApplyDocument(document);
            _beautifulFormatting = beautifulFormatting;
        }
    }

    internal void Export(string destinationPath)
    {
        lock (_sync)
        {
            var existingFilePath = GetExistingFilePath();
            if (existingFilePath is null)
            {
                throw new FileNotFoundException("The settings file has not been created yet.");
            }

            if (string.Equals(
                Path.GetFullPath(existingFilePath),
                Path.GetFullPath(destinationPath),
                StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            File.Copy(existingFilePath, destinationPath, overwrite: true);
        }
    }

    private void Save()
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

        var document = new SettingsDocument
        {
            BeautifulFormatting = _beautifulFormatting,
            Global = _globalSettings.Clone(),
            Providers = _providers
                .Select(provider => LlmProviderSettingsStore.ToStored(provider, _dataProtector))
                .ToList(),
            Commands = _commands.Select(command => command.Clone()).ToList(),
        };
        var temporaryPath = $"{_filePath}.tmp";
        var json = JsonSerializer.Serialize(
            document,
            _beautifulFormatting
                ? BeautifulJsonContext.SettingsDocument
                : CommandPaletteJsonContext.Default.SettingsDocument);
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, _filePath, true);
    }

    private static bool TryLoadDocument(
        string? filePath,
        out SettingsDocument document,
        out bool beautifulFormatting)
    {
        document = new SettingsDocument();
        beautifulFormatting = false;
        if (filePath is null || !File.Exists(filePath))
        {
            return false;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            return TryParseDocument(json, out document, out beautifulFormatting);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryParseDocument(
        string json,
        out SettingsDocument document,
        out bool beautifulFormatting)
    {
        document = new SettingsDocument();
        beautifulFormatting = false;
        try
        {
            using var parsed = JsonDocument.Parse(json);
            if (parsed.RootElement.ValueKind != JsonValueKind.Object ||
                !parsed.RootElement.TryGetProperty(nameof(SettingsDocument.Global), out _))
            {
                return false;
            }

            document = JsonSerializer.Deserialize(
                json,
                CommandPaletteJsonContext.Default.SettingsDocument) ?? new SettingsDocument();
            document.Global ??= new GlobalSettings();
            document.Providers ??= [];
            document.Commands ??= [];
            beautifulFormatting =
                document.BeautifulFormatting ??
                json.Contains('\n', StringComparison.Ordinal);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private string? GetExistingFilePath()
    {
        if (_filePath is null)
        {
            return null;
        }

        if (File.Exists(_filePath))
        {
            return _filePath;
        }

        var shellVisiblePath = SettingsFileLauncher.GetShellVisiblePath(_filePath);
        return !string.Equals(shellVisiblePath, _filePath, StringComparison.OrdinalIgnoreCase) &&
            File.Exists(shellVisiblePath)
                ? shellVisiblePath
                : null;
    }

    private static GlobalSettings NormalizeGlobalSettings(GlobalSettings settings)
    {
        var normalized = settings.Clone();
        normalized.AdvancedOutputSystemPrompt ??= string.Empty;
        if (string.Equals(
            normalized.AdvancedOutputSystemPrompt,
            GlobalSettings.LegacyAdvancedOutputSystemPrompt,
            StringComparison.Ordinal))
        {
            normalized.AdvancedOutputSystemPrompt =
                GlobalSettings.DefaultAdvancedOutputSystemPrompt;
        }

        return normalized;
    }

    private static List<LlmProviderSettings> NormalizeProviders(
        IEnumerable<LlmProviderSettings> providers)
    {
        var normalized = providers
            .Select(LlmProviderSettingsStore.Normalize)
            .Where(LlmProviderSettingsStore.IsValid)
            .GroupBy(provider => provider.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
        return normalized.Count == 0 ? [new LlmProviderSettings()] : normalized;
    }

    private static List<UserCommandDefinition> NormalizeCommands(
        IEnumerable<UserCommandDefinition> commands) => commands
            .Where(UserCommandStore.IsValidStoredCommand)
            .GroupBy(command => command.Id, StringComparer.Ordinal)
            .Select(group => group.First().Clone())
            .ToList();

    private void ApplyDocument(SettingsDocument document)
    {
        _globalSettings = NormalizeGlobalSettings(document.Global);
        _providers = NormalizeProviders(
            document.Providers.Select(provider =>
                LlmProviderSettingsStore.FromStored(provider, _dataProtector)));
        _commands = NormalizeCommands(document.Commands);
    }

    private static void DeleteLegacyFile(string? filePath)
    {
        if (filePath is null)
        {
            return;
        }

        try
        {
            File.Delete(filePath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
