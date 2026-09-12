using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CommandPaletteLLM;

internal sealed class LlmProviderSettingsStore
{
    private readonly object _sync = new();
    private readonly string? _filePath;
    private readonly IDataProtector _dataProtector;
    private List<LlmProviderSettings> _providers;

    public LlmProviderSettingsStore()
        : this(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CommandPaletteLLM",
                "providers.json"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CommandPaletteLLM",
                "provider.json"))
    {
    }

    internal LlmProviderSettingsStore(string? filePath)
        : this(filePath, legacyFilePath: null)
    {
    }

    internal LlmProviderSettingsStore(string? filePath, string? legacyFilePath)
        : this(filePath, legacyFilePath, new DpapiDataProtector())
    {
    }

    internal LlmProviderSettingsStore(
        string? filePath,
        string? legacyFilePath,
        IDataProtector dataProtector)
    {
        _filePath = filePath;
        _dataProtector = dataProtector;
        _providers = Load(filePath, legacyFilePath, dataProtector, out var needsMigration);
        if (needsMigration && _filePath is not null)
        {
            Save(_providers);
        }
    }

    public LlmProviderSettings Get()
    {
        lock (_sync)
        {
            return _providers[0].Clone();
        }
    }

    public LlmProviderSettings? Get(string providerId)
    {
        lock (_sync)
        {
            return _providers.FirstOrDefault(provider =>
                string.Equals(provider.Id, providerId, StringComparison.Ordinal))?.Clone();
        }
    }

    public IReadOnlyList<LlmProviderSettings> GetProviders()
    {
        lock (_sync)
        {
            return _providers.Select(provider => provider.Clone()).ToArray();
        }
    }

    public void Replace(LlmProviderSettings settings)
    {
        var providers = GetProviders().Select(provider => provider.Clone()).ToList();
        var replacement = settings.Clone();
        var index = providers.FindIndex(provider =>
            string.Equals(provider.Id, replacement.Id, StringComparison.Ordinal));
        if (index < 0 && providers.Count == 1 &&
            string.Equals(replacement.Id, "default", StringComparison.Ordinal))
        {
            index = 0;
        }

        if (index < 0)
        {
            providers.Add(replacement);
        }
        else
        {
            providers[index] = replacement;
        }

        ReplaceAll(providers);
    }

    public void ReplaceAll(IEnumerable<LlmProviderSettings> providers)
    {
        var replacement = providers.Select(Normalize).ToList();
        if (replacement.Count == 0)
        {
            replacement.Add(new LlmProviderSettings());
        }

        lock (_sync)
        {
            Save(replacement);
            _providers = replacement;
        }
    }

    private static List<LlmProviderSettings> Load(
        string? filePath,
        string? legacyFilePath,
        IDataProtector dataProtector,
        out bool needsMigration)
    {
        needsMigration = false;
        var sourcePath = filePath is not null && File.Exists(filePath)
            ? filePath
            : legacyFilePath is not null && File.Exists(legacyFilePath)
                ? legacyFilePath
                : null;
        if (sourcePath is null)
        {
            return [new LlmProviderSettings()];
        }

        needsMigration = !string.Equals(sourcePath, filePath, StringComparison.OrdinalIgnoreCase);

        try
        {
            var json = File.ReadAllText(sourcePath);
            using var document = JsonDocument.Parse(json);
            List<StoredLlmProviderSettings> storedProviders;
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                storedProviders = JsonSerializer.Deserialize(
                    json,
                    CommandPaletteJsonContext.Default.ListStoredLlmProviderSettings) ?? [];
            }
            else
            {
                var provider = JsonSerializer.Deserialize(
                    json,
                    CommandPaletteJsonContext.Default.StoredLlmProviderSettings);
                storedProviders = provider is null ? [] : [provider];
            }


            if (storedProviders.Any(provider => !string.IsNullOrEmpty(provider.ApiKey)))
            {
                needsMigration = true;
            }

            var providers = storedProviders.Select(provider => FromStored(provider, dataProtector));

            var valid = providers
                .Select(Normalize)
                .Where(IsValid)
                .GroupBy(provider => provider.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();
            return valid.Count == 0 ? [new LlmProviderSettings()] : valid;
        }
        catch (IOException)
        {
            return [new LlmProviderSettings()];
        }
        catch (JsonException)
        {
            return [new LlmProviderSettings()];
        }
        catch (UnauthorizedAccessException)
        {
            return [new LlmProviderSettings()];
        }
    }

    private void Save(List<LlmProviderSettings> providers)
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
        var storedProviders = providers.Select(provider => ToStored(provider, _dataProtector)).ToList();
        var json = JsonSerializer.Serialize(
            storedProviders,
            CommandPaletteJsonContext.Default.ListStoredLlmProviderSettings);
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, _filePath, true);
    }

    private static LlmProviderSettings FromStored(
        StoredLlmProviderSettings stored,
        IDataProtector dataProtector)
    {
        var apiKey = stored.ApiKey ?? string.Empty;
        if (!string.IsNullOrEmpty(stored.ProtectedApiKey))
        {
            try
            {
                apiKey = dataProtector.Unprotect(stored.ProtectedApiKey);
            }
            catch (Exception exception) when (
                exception is FormatException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                apiKey = string.Empty;
            }
        }

        return new LlmProviderSettings
        {
            Id = stored.Id,
            Name = stored.Name,
            BaseUrl = stored.BaseUrl,
            Model = stored.Model,
            ApiKey = apiKey,
            ConsentedRemoteOrigin = stored.ConsentedRemoteOrigin ?? string.Empty,
        };
    }

    private static StoredLlmProviderSettings ToStored(
        LlmProviderSettings provider,
        IDataProtector dataProtector) => new()
        {
            Id = provider.Id,
            Name = provider.Name,
            BaseUrl = provider.BaseUrl,
            Model = provider.Model,
            ProtectedApiKey = string.IsNullOrEmpty(provider.ApiKey)
                ? null
                : dataProtector.Protect(provider.ApiKey),
            ConsentedRemoteOrigin = string.IsNullOrEmpty(provider.ConsentedRemoteOrigin)
                ? null
                : provider.ConsentedRemoteOrigin,
        };

    private static LlmProviderSettings Normalize(LlmProviderSettings provider)
    {
        var normalized = provider.Clone();
        normalized.Id = string.IsNullOrWhiteSpace(normalized.Id) ? "default" : normalized.Id.Trim();
        normalized.Name = string.IsNullOrWhiteSpace(normalized.Name) ? "Local LLM" : normalized.Name.Trim();
        normalized.BaseUrl = normalized.BaseUrl.Trim();
        normalized.Model = normalized.Model.Trim();
        if (!ProviderDataConsent.HasConsent(normalized))
        {
            normalized.ConsentedRemoteOrigin = string.Empty;
        }

        return normalized;
    }

    internal static bool IsValid(LlmProviderSettings settings) =>
        !string.IsNullOrWhiteSpace(settings.Id) &&
        !string.IsNullOrWhiteSpace(settings.Name) &&
        Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out var baseUri) &&
        (baseUri.Scheme == Uri.UriSchemeHttp || baseUri.Scheme == Uri.UriSchemeHttps) &&
        !string.IsNullOrWhiteSpace(settings.Model);
}
