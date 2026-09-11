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
    private readonly Action _commandsChanged;

    public UserCommandsSettingsForm(UserCommandStore store, Action commandsChanged)
    {
        _store = store;
        _commandsChanged = commandsChanged;
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
        });

        return ValidateAndSave(commands);
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
        TemplateJson = BuildCard(_store.GetCommands(), errorMessage).ToJsonString();
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
        IReadOnlyList<UserCommandDefinition> commands,
        string? errorMessage)
    {
        var body = new JsonArray
        {
            new JsonObject
            {
                ["type"] = "TextBlock",
                ["text"] = "Commands",
                ["size"] = "Large",
                ["weight"] = "Bolder",
            },
            new JsonObject
            {
                ["type"] = "TextBlock",
                ["text"] = "Create commands here, then use Command Palette's native command settings to configure aliases and global results. Edit a command to change its output format.",
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
                            "Output format",
                            command.OutputFormat,
                            "Use {} for the search term, {{ for {, and }} for }."),
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
        }

        return null;
    }

    private static string ReadText(JsonObject input, string key, string fallback = "") =>
        input[key]?.GetValue<string>() ?? fallback;

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
