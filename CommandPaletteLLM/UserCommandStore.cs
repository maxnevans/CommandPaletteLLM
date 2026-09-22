using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CommandPaletteLLM;

internal sealed class UserCommandStore
{
    private readonly object _sync = new();
    private readonly string? _filePath;
    private List<UserCommandDefinition> _commands;

    public UserCommandStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CommandPaletteLLM",
            "commands.json"))
    {
    }

    internal UserCommandStore(string? filePath)
    {
        _filePath = filePath;
        _commands = LoadCommands();
    }

    internal string? StorageDirectory => _filePath is null ? null : Path.GetDirectoryName(_filePath);

    public IReadOnlyList<UserCommandDefinition> GetCommands()
    {
        lock (_sync)
        {
            return _commands.Select(command => command.Clone()).ToArray();
        }
    }

    public void ReplaceAll(IEnumerable<UserCommandDefinition> commands)
    {
        var replacement = commands.Select(command => command.Clone()).ToList();

        lock (_sync)
        {
            SaveCommands(replacement);
            _commands = replacement;
        }
    }

    private List<UserCommandDefinition> LoadCommands()
    {
        if (_filePath is null || !File.Exists(_filePath))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var commands = JsonSerializer.Deserialize(
                json,
                CommandPaletteJsonContext.Default.ListUserCommandDefinition) ?? [];

            return commands
                .OfType<UserCommandDefinition>()
                .Where(IsValidStoredCommand)
                .GroupBy(command => command.Id, StringComparer.Ordinal)
                .Select(group => group.First().Clone())
                .ToList();
        }
        catch (IOException)
        {
            return [];
        }
        catch (JsonException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private void SaveCommands(List<UserCommandDefinition> commands)
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
            commands,
            CommandPaletteJsonContext.Default.ListUserCommandDefinition);
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, _filePath, true);
    }

    private static bool IsValidStoredCommand(UserCommandDefinition command) =>
        !string.IsNullOrWhiteSpace(command.Id) &&
        !string.IsNullOrWhiteSpace(command.Name) &&
        !string.IsNullOrWhiteSpace(command.OutputFormat) &&
        !string.IsNullOrWhiteSpace(command.ProviderId) &&
        command.EffectiveExposure is (CommandExposure.None or
            CommandExposure.FallbackCommand or
            CommandExposure.GlobalResult) &&
        command.SendDelayMilliseconds is >= 0 and <= 10_000 &&
        OutputFormatter.TryFormat(command.OutputFormat, string.Empty, out _, out _);
}
