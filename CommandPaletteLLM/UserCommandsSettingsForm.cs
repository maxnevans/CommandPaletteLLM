using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CommandPaletteLLM;

internal sealed partial class UserCommandsSettingsForm : FormContent
{
    private readonly UserCommandStore _store;
    private readonly LlmProviderSettingsStore _providerSettingsStore;
    private readonly Action _commandsChanged;
    private readonly Action _providerSettingsChanged;

    public UserCommandsSettingsForm(
        UserCommandStore store,
        LlmProviderSettingsStore providerSettingsStore,
        Action commandsChanged,
        Action providerSettingsChanged)
    {
        _store = store;
        _providerSettingsStore = providerSettingsStore;
        _commandsChanged = commandsChanged;
        _providerSettingsChanged = providerSettingsChanged;
        Refresh();
    }

    public override CommandResult SubmitForm(string inputs, string data) =>
        Submit(inputs, ReadActionId(data) ?? ReadActionId(inputs));

    public override CommandResult SubmitForm(string payload) =>
        Submit(payload, ReadActionId(payload));

    private CommandResult Submit(string payload, string? actionId)
    {
        try
        {
            var input = JsonNode.Parse(payload)?.AsObject();
            if (input is null)
            {
                return ShowError("Command settings could not be read.");
            }

            if (string.Equals(actionId, "add", StringComparison.Ordinal))
            {
                return Add(input);
            }

            if (string.Equals(actionId, "save-provider", StringComparison.Ordinal))
            {
                return SaveProvider(input);
            }

            const string savePrefix = "save:";
            if (actionId?.StartsWith(savePrefix, StringComparison.Ordinal) == true)
            {
                return Save(input, actionId[savePrefix.Length..]);
            }

            const string deletePrefix = "delete:";
            if (actionId?.StartsWith(deletePrefix, StringComparison.Ordinal) == true)
            {
                return Delete(actionId[deletePrefix.Length..]);
            }

            return ShowError("Unknown settings action.");
        }
        catch (JsonException)
        {
            return ShowError("Command settings could not be read.");
        }
        catch (IOException exception)
        {
            return ShowError($"Commands could not be saved: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            return ShowError($"Commands could not be saved: {exception.Message}");
        }
    }

    private CommandResult Add(JsonObject input)
    {
        var commands = _store.GetCommands().Select(command => command.Clone()).ToList();
        commands.Add(new UserCommandDefinition
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = ReadText(input, "newName"),
            OutputFormat = "{}",
            SendDelayMilliseconds = 650,
        });

        return ValidateAndSave(commands);
    }

    private CommandResult SaveProvider(JsonObject input)
    {
        var existing = _providerSettingsStore.Get();
        var apiKey = ReadText(input, "providerApiKey");
        var settings = new LlmProviderSettings
        {
            BaseUrl = ReadText(input, "providerBaseUrl", existing.BaseUrl).Trim(),
            Model = ReadText(input, "providerModel", existing.Model).Trim(),
            ApiKey = ReadBoolean(input, "clearProviderApiKey")
                ? string.Empty
                : string.IsNullOrEmpty(apiKey) ? existing.ApiKey : apiKey,
        };

        if (!LlmProviderSettingsStore.IsValid(settings))
        {
            return ShowError("Provider base URL must be an absolute HTTP or HTTPS URL, and model is required.");
        }

        _providerSettingsStore.Replace(settings);
        _providerSettingsChanged();
        Refresh();
        return CommandResult.KeepOpen();
    }

    private CommandResult Save(JsonObject input, string commandId)
    {
        var commands = _store.GetCommands().Select(command => command.Clone()).ToList();
        var command = commands.FirstOrDefault(
            command => string.Equals(command.Id, commandId, StringComparison.Ordinal));
        if (command is null)
        {
            return ShowError("The command no longer exists.");
        }

        command.Name = ReadText(input, $"name_{command.Id}", command.Name);
        command.OutputFormat = ReadText(input, $"format_{command.Id}", command.OutputFormat);
        var delayKey = $"delay_{command.Id}";
        if (input[delayKey] is not null)
        {
            if (!TryReadInteger(input, delayKey, out var delay))
            {
                return ShowError($"Command '{command.Name}' must have a whole-number send delay.");
            }

            command.SendDelayMilliseconds = delay;
        }

        return ValidateAndSave(commands);
    }

    private CommandResult ValidateAndSave(List<UserCommandDefinition> commands)
    {

        var validationError = Validate(commands);
        if (validationError is not null)
        {
            return ShowError(validationError);
        }

        _store.ReplaceAll(commands);
        _commandsChanged();
        Refresh();
        return CommandResult.KeepOpen();
    }

    private CommandResult Delete(string commandId)
    {
        var existingCommands = _store.GetCommands();
        var commands = existingCommands
            .Where(command => !string.Equals(command.Id, commandId, StringComparison.Ordinal))
            .ToArray();

        if (commands.Length == existingCommands.Count)
        {
            return ShowError("The command no longer exists.");
        }

        _store.ReplaceAll(commands);
        _commandsChanged();
        Refresh();
        return CommandResult.KeepOpen();
    }

    private CommandResult ShowError(string message)
    {
        Refresh(message);
        return CommandResult.KeepOpen();
    }

    private void Refresh(string? errorMessage = null)
    {
        TemplateJson = BuildCard(
            _providerSettingsStore.Get(),
            _store.GetCommands(),
            errorMessage).ToJsonString();
        DataJson = "{}";
        StateJson = "{}";
    }

    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "The card builder adds only primitive values and explicit JsonNode instances.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "The card builder adds only primitive values and explicit JsonNode instances.")]
    private static JsonObject BuildCard(
        LlmProviderSettings providerSettings,
        IReadOnlyList<UserCommandDefinition> commands,
        string? errorMessage)
    {
        var body = new JsonArray
        {
            new JsonObject
            {
                ["type"] = "TextBlock",
                ["text"] = "LLM provider",
                ["size"] = "Large",
                ["weight"] = "Bolder",
            },
            new JsonObject
            {
                ["type"] = "TextBlock",
                ["text"] = "Connect to an OpenAI-compatible chat completions API, including llama.cpp server.",
                ["wrap"] = true,
            },
            BuildTextInput(
                "providerBaseUrl",
                "API base URL",
                providerSettings.BaseUrl,
                "http://127.0.0.1:8080/v1"),
            BuildTextInput(
                "providerModel",
                "Model",
                providerSettings.Model,
                "local-model"),
            BuildPasswordInput(
                "providerApiKey",
                "API key (optional)",
                string.IsNullOrEmpty(providerSettings.ApiKey)
                    ? "Optional bearer token"
                    : "Saved; leave blank to keep it"),
            new JsonObject
            {
                ["type"] = "Input.Toggle",
                ["id"] = "clearProviderApiKey",
                ["title"] = "Clear saved API key",
                ["value"] = "false",
                ["valueOn"] = "true",
                ["valueOff"] = "false",
            },
            new JsonObject
            {
                ["type"] = "ActionSet",
                ["actions"] = new JsonArray
                {
                    BuildSubmitAction("Save provider", "save-provider"),
                },
            },
            new JsonObject
            {
                ["type"] = "TextBlock",
                ["text"] = "Commands",
                ["size"] = "Large",
                ["weight"] = "Bolder",
                ["separator"] = true,
            },
            new JsonObject
            {
                ["type"] = "TextBlock",
                ["text"] = "Create commands here, then use Command Palette's native command settings to configure aliases and global results. Each format turns the message into the prompt sent to the provider.",
                ["wrap"] = true,
            },
        };

        if (commands.Count == 0)
        {
            body.Add(new JsonObject
            {
                ["type"] = "TextBlock",
                ["text"] = "No commands have been created yet.",
                ["isSubtle"] = true,
                ["wrap"] = true,
            });
        }

        foreach (var command in commands)
        {
            body.Add(BuildCommandEditor(command));
        }

        body.Add(new JsonObject
        {
            ["type"] = "TextBlock",
            ["text"] = "Add a command",
            ["weight"] = "Bolder",
            ["separator"] = true,
        });
        body.Add(BuildTextInput(
            "newName",
            "Command name",
            string.Empty,
            "For example: Greeting",
            isRequired: false));
        body.Add(new JsonObject
        {
            ["type"] = "ActionSet",
            ["actions"] = new JsonArray
            {
                BuildSubmitAction("Add command", "add"),
            },
        });

        if (!string.IsNullOrEmpty(errorMessage))
        {
            body.Add(new JsonObject
            {
                ["type"] = "TextBlock",
                ["text"] = errorMessage,
                ["color"] = "Attention",
                ["wrap"] = true,
            });
        }

        return new JsonObject
        {
            ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json",
            ["type"] = "AdaptiveCard",
            ["version"] = "1.5",
            ["body"] = body,
        };
    }

    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "The card builder adds only primitive values and explicit JsonNode instances.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "The card builder adds only primitive values and explicit JsonNode instances.")]
    private static JsonObject BuildCommandEditor(UserCommandDefinition command)
    {
        var displayId = $"display_{command.Id}";
        var editorId = $"editor_{command.Id}";

        return new JsonObject
        {
            ["type"] = "Container",
            ["style"] = "emphasis",
            ["spacing"] = "Medium",
            ["items"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "Container",
                    ["id"] = displayId,
                    ["items"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "ColumnSet",
                            ["columns"] = new JsonArray
                            {
                                new JsonObject
                                {
                                    ["type"] = "Column",
                                    ["width"] = "stretch",
                                    ["items"] = new JsonArray
                                    {
                                        new JsonObject
                                        {
                                            ["type"] = "TextBlock",
                                            ["text"] = command.Name,
                                            ["weight"] = "Bolder",
                                            ["wrap"] = true,
                                        },
                                        new JsonObject
                                        {
                                            ["type"] = "TextBlock",
                                            ["text"] = command.OutputFormat,
                                            ["fontType"] = "Monospace",
                                            ["isSubtle"] = true,
                                            ["spacing"] = "Small",
                                            ["wrap"] = true,
                                        },
                                        new JsonObject
                                        {
                                            ["type"] = "TextBlock",
                                            ["text"] = $"Send delay: {command.SendDelayMilliseconds} ms",
                                            ["isSubtle"] = true,
                                            ["spacing"] = "Small",
                                        },
                                    },
                                },
                                new JsonObject
                                {
                                    ["type"] = "Column",
                                    ["width"] = "auto",
                                    ["verticalContentAlignment"] = "Center",
                                    ["items"] = new JsonArray
                                    {
                                        new JsonObject
                                        {
                                            ["type"] = "ActionSet",
                                            ["horizontalAlignment"] = "Right",
                                            ["actions"] = new JsonArray
                                            {
                                                BuildToggleAction(
                                                    "✎",
                                                    "Edit command",
                                                    displayId,
                                                    editorId),
                                                BuildSubmitAction(
                                                    "🗑",
                                                    $"delete:{command.Id}",
                                                    "Delete command",
                                                    associatedInputs: "none",
                                                    style: "destructive"),
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
                new JsonObject
                {
                    ["type"] = "Container",
                    ["id"] = editorId,
                    ["isVisible"] = false,
                    ["items"] = new JsonArray
                    {
                        BuildTextInput(
                            $"name_{command.Id}",
                            "Command name",
                            command.Name,
                            string.Empty),
                        BuildTextInput(
                            $"format_{command.Id}",
                            "Prompt format",
                            command.OutputFormat,
                            "Use {} for the search term, {{ for {, and }} for }."),
                        BuildNumberInput(
                            $"delay_{command.Id}",
                            "Send delay (milliseconds)",
                            command.SendDelayMilliseconds,
                            0,
                            10_000),
                        new JsonObject
                        {
                            ["type"] = "ActionSet",
                            ["horizontalAlignment"] = "Right",
                            ["actions"] = new JsonArray
                            {
                                BuildSubmitAction(
                                    "✓",
                                    $"save:{command.Id}",
                                    "Save changes"),
                                BuildToggleAction(
                                    "✕",
                                    "Cancel editing",
                                    displayId,
                                    editorId),
                            },
                        },
                    },
                },
            },
        };
    }

    private static JsonObject BuildTextInput(
        string id,
        string label,
        string value,
        string placeholder,
        bool isRequired = true) =>
        new()
        {
            ["type"] = "Input.Text",
            ["id"] = id,
            ["label"] = label,
            ["value"] = value,
            ["placeholder"] = placeholder,
            ["isRequired"] = isRequired,
            ["errorMessage"] = $"{label} is required.",
        };

    private static JsonObject BuildPasswordInput(
        string id,
        string label,
        string placeholder) =>
        new()
        {
            ["type"] = "Input.Text",
            ["id"] = id,
            ["label"] = label,
            ["placeholder"] = placeholder,
            ["style"] = "password",
        };

    private static JsonObject BuildNumberInput(
        string id,
        string label,
        int value,
        int minimum,
        int maximum) =>
        new()
        {
            ["type"] = "Input.Number",
            ["id"] = id,
            ["label"] = label,
            ["value"] = value,
            ["min"] = minimum,
            ["max"] = maximum,
            ["isRequired"] = true,
            ["errorMessage"] = $"{label} must be between {minimum} and {maximum}.",
        };

    private static JsonObject BuildSubmitAction(
        string title,
        string actionId,
        string? tooltip = null,
        string? associatedInputs = null,
        string? style = null)
    {
        var action = new JsonObject
        {
            ["type"] = "Action.Submit",
            ["id"] = actionId,
            ["title"] = title,
            ["data"] = new JsonObject { ["actionId"] = actionId },
        };

        if (!string.IsNullOrEmpty(tooltip))
        {
            action["tooltip"] = tooltip;
        }

        if (!string.IsNullOrEmpty(associatedInputs))
        {
            action["associatedInputs"] = associatedInputs;
        }

        if (!string.IsNullOrEmpty(style))
        {
            action["style"] = style;
        }

        return action;
    }

    private static JsonObject BuildToggleAction(
        string title,
        string tooltip,
        params string[] targetElements) =>
        new()
        {
            ["type"] = "Action.ToggleVisibility",
            ["title"] = title,
            ["tooltip"] = tooltip,
            ["targetElements"] = new JsonArray(
                targetElements.Select(id => JsonValue.Create(id)).ToArray()),
        };

    private static string? Validate(IReadOnlyList<UserCommandDefinition> commands)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var command in commands)
        {
            if (string.IsNullOrWhiteSpace(command.Name))
            {
                return "Every command must have a name.";
            }

            command.Name = command.Name.Trim();
            if (!names.Add(command.Name))
            {
                return $"Command names must be unique. '{command.Name}' is used more than once.";
            }

            if (string.IsNullOrWhiteSpace(command.OutputFormat))
            {
                return $"Command '{command.Name}' must have an output format.";
            }

            if (!OutputFormatter.TryFormat(command.OutputFormat, string.Empty, out _, out var error))
            {
                return $"Command '{command.Name}' has an invalid output format: {error}";
            }

            if (command.SendDelayMilliseconds is < 0 or > 10_000)
            {
                return $"Command '{command.Name}' must have a send delay between 0 and 10000 milliseconds.";
            }
        }

        return null;
    }

    private static string ReadText(JsonObject input, string key, string fallback = "") =>
        input[key]?.GetValue<string>() ?? fallback;

    private static bool ReadBoolean(JsonObject input, string key) =>
        string.Equals(input[key]?.ToString(), "true", StringComparison.OrdinalIgnoreCase);

    private static bool TryReadInteger(JsonObject input, string key, out int value)
    {
        if (input[key] is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue(out value))
            {
                return true;
            }

            if (jsonValue.TryGetValue<string>(out var text) && int.TryParse(text, out value))
            {
                return true;
            }
        }

        value = 0;
        return false;
    }

    private static string? ReadActionId(string payload)
    {
        try
        {
            return JsonNode.Parse(payload)?["actionId"]?.GetValue<string>();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
