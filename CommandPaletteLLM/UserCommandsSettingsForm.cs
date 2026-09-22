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
    private readonly GlobalSettingsStore _globalSettingsStore;
    private readonly Action _commandsChanged;
    private readonly Action _globalSettingsChanged;
    private readonly Action _providerSettingsChanged;
    private readonly CommandIconStore _iconStore;
    private readonly HashSet<string> _expandedCommandIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _expandedProviderIds = new(StringComparer.Ordinal);
    private readonly JsonObject _draftInputs = [];
    private bool _globalSettingsExpanded;

    public UserCommandsSettingsForm(
        UserCommandStore store,
        LlmProviderSettingsStore providerSettingsStore,
        GlobalSettingsStore globalSettingsStore,
        Action commandsChanged,
        Action globalSettingsChanged,
        Action providerSettingsChanged)
    {
        _store = store;
        _providerSettingsStore = providerSettingsStore;
        _globalSettingsStore = globalSettingsStore;
        _commandsChanged = commandsChanged;
        _globalSettingsChanged = globalSettingsChanged;
        _providerSettingsChanged = providerSettingsChanged;
        _iconStore = new CommandIconStore(store.StorageDirectory);
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

            CaptureDraftInputs(input);

            if (string.Equals(actionId, "edit-global", StringComparison.Ordinal))
            {
                _globalSettingsExpanded = true;
                Refresh();
                return CommandResult.KeepOpen();
            }

            if (string.Equals(actionId, "cancel-global", StringComparison.Ordinal))
            {
                _globalSettingsExpanded = false;
                RemoveDraftInput("advancedOutputSystemPrompt");
                Refresh();
                return CommandResult.KeepOpen();
            }

            if (string.Equals(actionId, "save-global", StringComparison.Ordinal))
            {
                var settings = _globalSettingsStore.Get();
                settings.AdvancedOutputSystemPrompt = ReadText(
                    input,
                    "advancedOutputSystemPrompt",
                    settings.AdvancedOutputSystemPrompt);
                _globalSettingsStore.Replace(settings);
                _globalSettingsExpanded = false;
                RemoveDraftInput("advancedOutputSystemPrompt");
                _globalSettingsChanged();
                Refresh();
                return CommandResult.KeepOpen();
            }

            if (string.Equals(actionId, "reset-global", StringComparison.Ordinal))
            {
                _globalSettingsStore.ResetAdvancedOutputSystemPrompt();
                _globalSettingsExpanded = false;
                RemoveDraftInput("advancedOutputSystemPrompt");
                _globalSettingsChanged();
                Refresh();
                return CommandResult.KeepOpen();
            }

            const string editPrefix = "edit:";
            if (actionId?.StartsWith(editPrefix, StringComparison.Ordinal) == true)
            {
                return SetCommandEditorVisibility(actionId[editPrefix.Length..], isExpanded: true);
            }

            const string cancelPrefix = "cancel:";
            if (actionId?.StartsWith(cancelPrefix, StringComparison.Ordinal) == true)
            {
                return SetCommandEditorVisibility(actionId[cancelPrefix.Length..], isExpanded: false);
            }

            const string editProviderPrefix = "edit-provider:";
            if (actionId?.StartsWith(editProviderPrefix, StringComparison.Ordinal) == true)
            {
                return SetProviderEditorVisibility(actionId[editProviderPrefix.Length..], isExpanded: true);
            }

            const string cancelProviderPrefix = "cancel-provider:";
            if (actionId?.StartsWith(cancelProviderPrefix, StringComparison.Ordinal) == true)
            {
                return SetProviderEditorVisibility(actionId[cancelProviderPrefix.Length..], isExpanded: false);
            }

            if (string.Equals(actionId, "add", StringComparison.Ordinal))
            {
                return Add(input);
            }

            if (string.Equals(actionId, "add-provider", StringComparison.Ordinal))
            {
                return AddProvider(input);
            }

            const string saveProviderPrefix = "save-provider:";
            if (actionId?.StartsWith(saveProviderPrefix, StringComparison.Ordinal) == true)
            {
                return SaveProvider(input, actionId[saveProviderPrefix.Length..]);
            }

            const string clearProviderKeyPrefix = "clear-provider-key:";
            if (actionId?.StartsWith(clearProviderKeyPrefix, StringComparison.Ordinal) == true)
            {
                return ClearProviderApiKey(actionId[clearProviderKeyPrefix.Length..]);
            }

            const string deleteProviderPrefix = "delete-provider:";
            if (actionId?.StartsWith(deleteProviderPrefix, StringComparison.Ordinal) == true)
            {
                return DeleteProvider(actionId[deleteProviderPrefix.Length..]);
            }

            if (string.Equals(actionId, "save-provider", StringComparison.Ordinal))
            {
                return SaveLegacyProvider(input);
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
            EnableAdvancedOutput = false,
            ProviderId = _providerSettingsStore.Get().Id,
            Exposure = CommandExposure.FallbackCommand,
            EnableGlobalFallback = true,
        });

        _draftInputs.Remove("newName");
        return ValidateAndSave(commands);
    }

    private CommandResult AddProvider(JsonObject input)
    {
        var providers = _providerSettingsStore.GetProviders()
            .Select(provider => provider.Clone())
            .ToList();
        providers.Add(new LlmProviderSettings
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = ReadText(input, "newProviderName"),
        });

        _draftInputs.Remove("newProviderName");
        return ValidateAndSaveProviders(providers);
    }

    private CommandResult SaveProvider(JsonObject input, string providerId)
    {
        var providers = _providerSettingsStore.GetProviders()
            .Select(provider => provider.Clone())
            .ToList();
        var provider = providers.FirstOrDefault(item =>
            string.Equals(item.Id, providerId, StringComparison.Ordinal));
        if (provider is null)
        {
            return ShowError("The provider no longer exists.");
        }

        var apiKey = ReadText(input, $"providerApiKey_{provider.Id}");
        provider.Name = ReadText(input, $"providerName_{provider.Id}", provider.Name);
        provider.BaseUrl = ReadText(input, $"providerBaseUrl_{provider.Id}", provider.BaseUrl).Trim();
        provider.Model = ReadText(input, $"providerModel_{provider.Id}", provider.Model).Trim();
        if (!string.IsNullOrEmpty(apiKey))
        {
            provider.ApiKey = apiKey;
        }

        return ValidateAndSaveProviders(providers, provider.Id);
    }

    private CommandResult SaveLegacyProvider(JsonObject input)
    {
        var existing = _providerSettingsStore.Get();
        var apiKey = ReadText(input, "providerApiKey");
        var settings = new LlmProviderSettings
        {
            BaseUrl = ReadText(input, "providerBaseUrl", existing.BaseUrl).Trim(),
            Model = ReadText(input, "providerModel", existing.Model).Trim(),
            Id = existing.Id,
            Name = existing.Name,
            ApiKey = string.IsNullOrEmpty(apiKey) ? existing.ApiKey : apiKey,
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

    private CommandResult ClearProviderApiKey(string providerId)
    {
        var providers = _providerSettingsStore.GetProviders()
            .Select(provider => provider.Clone())
            .ToList();
        var provider = providers.FirstOrDefault(item =>
            string.Equals(item.Id, providerId, StringComparison.Ordinal));
        if (provider is null)
        {
            return ShowError("The provider no longer exists.");
        }

        provider.ApiKey = string.Empty;
        _draftInputs.Remove($"providerApiKey_{provider.Id}");
        _providerSettingsStore.ReplaceAll(providers);
        _providerSettingsChanged();
        Refresh();
        return CommandResult.KeepOpen();
    }

    private CommandResult DeleteProvider(string providerId)
    {
        var providers = _providerSettingsStore.GetProviders();
        if (providers.Count == 1)
        {
            return ShowError("At least one LLM provider is required.");
        }

        if (_store.GetCommands().Any(command =>
            string.Equals(command.ProviderId, providerId, StringComparison.Ordinal)))
        {
            return ShowError("Assign commands to another provider before deleting this provider.");
        }

        _providerSettingsStore.ReplaceAll(providers.Where(provider =>
            !string.Equals(provider.Id, providerId, StringComparison.Ordinal)));
        _expandedProviderIds.Remove(providerId);
        RemoveDraftInputsForProvider(providerId);
        _providerSettingsChanged();
        Refresh();
        return CommandResult.KeepOpen();
    }

    private CommandResult ValidateAndSaveProviders(
        List<LlmProviderSettings> providers,
        string? savedProviderId = null)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in providers)
        {
            provider.Name = provider.Name.Trim();
            if (!LlmProviderSettingsStore.IsValid(provider))
            {
                return ShowError("Every provider needs a unique name, an absolute HTTP or HTTPS URL, and a model.");
            }

            if (!names.Add(provider.Name))
            {
                return ShowError($"Provider names must be unique. '{provider.Name}' is used more than once.");
            }
        }

        _providerSettingsStore.ReplaceAll(providers);
        if (savedProviderId is not null)
        {
            _expandedProviderIds.Remove(savedProviderId);
            RemoveDraftInputsForProvider(savedProviderId);
        }

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
        command.CustomRequestArguments = ReadText(
            input,
            $"requestArguments_{command.Id}",
            command.CustomRequestArguments);
        var advancedOutputKey = $"advancedOutput_{command.Id}";
        if (input[advancedOutputKey] is not null)
        {
            command.EnableAdvancedOutput = ReadBoolean(input, advancedOutputKey);
        }

        var delayKey = $"delay_{command.Id}";
        if (input[delayKey] is not null)
        {
            if (!TryReadInteger(input, delayKey, out var delay))
            {
                return ShowError($"Command '{command.Name}' must have a whole-number send delay.");
            }

            command.SendDelayMilliseconds = delay;
        }

        var providerKey = $"provider_{command.Id}";
        command.ProviderId = ReadText(input, providerKey, command.ProviderId);
        if (_providerSettingsStore.Get(command.ProviderId) is null)
        {
            return ShowError($"Command '{command.Name}' must use an existing LLM provider.");
        }

        var fallbackKey = $"fallback_{command.Id}";
        if (input[fallbackKey] is not null)
        {
            command.EnableGlobalFallback = ReadBoolean(input, fallbackKey);
            command.Exposure = command.EnableGlobalFallback
                ? CommandExposure.FallbackCommand
                : CommandExposure.None;
        }

        var iconKey = $"icon_{command.Id}";
        var storedIconPath = command.IconPath;
        if (input[iconKey] is not null &&
            !_iconStore.TryImport(
                command.Id,
                ReadText(input, iconKey),
                command.IconPath,
                out storedIconPath,
                out var iconError))
        {
            return ShowError($"Command '{command.Name}': {iconError}");
        }
        else if (input[iconKey] is not null)
        {
            command.IconPath = storedIconPath;
        }

        var validationError = Validate(commands);
        if (validationError is not null)
        {
            return ShowError(validationError);
        }

        _store.ReplaceAll(commands);
        _expandedCommandIds.Remove(commandId);
        RemoveDraftInputsForCommand(commandId);
        _commandsChanged();
        Refresh();
        return CommandResult.KeepOpen();
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
        _expandedCommandIds.Remove(commandId);
        RemoveDraftInputsForCommand(commandId);
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
            _globalSettingsStore.Get(),
            _providerSettingsStore.GetProviders(),
            _store.GetCommands(),
            _globalSettingsExpanded,
            _expandedProviderIds,
            _expandedCommandIds,
            _draftInputs,
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
        GlobalSettings globalSettings,
        IReadOnlyList<LlmProviderSettings> providers,
        IReadOnlyList<UserCommandDefinition> commands,
        bool expandedGlobalSettings,
        HashSet<string> expandedProviderIds,
        HashSet<string> expandedCommandIds,
        JsonObject draftInputs,
        string? errorMessage)
    {
        var body = new JsonArray
        {
            new JsonObject
            {
                ["type"] = "TextBlock",
                ["text"] = "Global",
                ["size"] = "Large",
                ["weight"] = "Bolder",
            },
            BuildGlobalSettingsEditor(globalSettings, expandedGlobalSettings, draftInputs),
            new JsonObject
            {
                ["type"] = "TextBlock",
                ["text"] = "LLM providers",
                ["size"] = "Large",
                ["weight"] = "Bolder",
                ["separator"] = true,
                ["spacing"] = "ExtraLarge",
            },
            new JsonObject
            {
                ["type"] = "TextBlock",
                ["text"] = "Connect one or more OpenAI-compatible chat-completions APIs, including llama.cpp servers.",
                ["wrap"] = true,
            },
        };

        foreach (var provider in providers)
        {
            body.Add(BuildProviderEditor(
                provider,
                providers.Count > 1,
                expandedProviderIds.Contains(provider.Id),
                draftInputs));
        }

        body.Add(new JsonObject
        {
            ["type"] = "TextBlock",
            ["text"] = "Add an LLM provider",
            ["weight"] = "Bolder",
            ["separator"] = true,
        });
        body.Add(BuildTextInput(
            "newProviderName",
            "Provider name",
            string.Empty,
            "For example: Local llama.cpp",
            isRequired: false));
        body.Add(new JsonObject
        {
            ["type"] = "ActionSet",
            ["actions"] = new JsonArray
            {
                BuildSubmitAction("Add provider", "add-provider"),
            },
        });

        body.Add(new JsonObject
        {
            ["type"] = "TextBlock",
            ["text"] = "Commands",
            ["size"] = "Large",
            ["weight"] = "Bolder",
            ["separator"] = true,
            ["spacing"] = "ExtraLarge",
        });
        body.Add(new JsonObject
        {
            ["type"] = "TextBlock",
            ["text"] = "Every command remains available in Command Palette's Commands list for aliases and alias activation. Enable fallback separately for commands that should process fallback queries.",
            ["wrap"] = true,
        });

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
            body.Add(BuildCommandEditor(
                command,
                providers,
                expandedCommandIds.Contains(command.Id),
                draftInputs));
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
    private static JsonObject BuildGlobalSettingsEditor(
        GlobalSettings settings,
        bool isExpanded,
        JsonObject draftInputs)
    {
        var prompt = settings.AdvancedOutputSystemPrompt;
        var status = string.IsNullOrWhiteSpace(prompt)
            ? "Disabled"
            : string.Equals(
                prompt,
                GlobalSettings.DefaultAdvancedOutputSystemPrompt,
                StringComparison.Ordinal)
                ? "Default"
                : "Customized";

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
                    ["id"] = "global_display",
                    ["isVisible"] = !isExpanded,
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
                                            ["text"] = "Advanced output system prompt",
                                            ["weight"] = "Bolder",
                                            ["wrap"] = true,
                                        },
                                        new JsonObject
                                        {
                                            ["type"] = "TextBlock",
                                            ["text"] = status,
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
                                                BuildSubmitAction(
                                                    "✎",
                                                    "edit-global",
                                                    "Edit global advanced output prompt",
                                                    associatedInputs: "none"),
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
                    ["id"] = "global_editor",
                    ["isVisible"] = isExpanded,
                    ["items"] = new JsonArray
                    {
                        BuildTextInput(
                            "advancedOutputSystemPrompt",
                            "Advanced output system prompt",
                            ReadText(draftInputs, "advancedOutputSystemPrompt", prompt),
                            "Leave empty to disable automatic prompt injection.",
                            isRequired: false,
                            isMultiline: true),
                        new JsonObject
                        {
                            ["type"] = "TextBlock",
                            ["text"] = "Sent as a system message before the command prompt whenever advanced output is enabled. An empty value disables injection but keeps advanced response parsing enabled.",
                            ["isSubtle"] = true,
                            ["wrap"] = true,
                            ["spacing"] = "Small",
                        },
                        new JsonObject
                        {
                            ["type"] = "ActionSet",
                            ["horizontalAlignment"] = "Right",
                            ["actions"] = new JsonArray
                            {
                                BuildSubmitAction(
                                    "Reset to default",
                                    "reset-global",
                                    associatedInputs: "none"),
                                BuildSubmitAction("✓", "save-global", "Save global prompt"),
                                BuildSubmitAction(
                                    "✕",
                                    "cancel-global",
                                    "Cancel editing",
                                    associatedInputs: "none"),
                            },
                        },
                    },
                },
            },
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
    private static JsonObject BuildProviderEditor(
        LlmProviderSettings provider,
        bool canDelete,
        bool isExpanded,
        JsonObject draftInputs)
    {
        var displayId = $"provider_display_{provider.Id}";
        var editorId = $"provider_editor_{provider.Id}";
        var actions = new JsonArray
        {
            BuildSubmitAction(
                "✎",
                $"edit-provider:{provider.Id}",
                "Edit provider"),
        };
        if (canDelete)
        {
            actions.Add(BuildSubmitAction(
                "🗑",
                $"delete-provider:{provider.Id}",
                "Delete provider",
                associatedInputs: "none",
                style: "destructive"));
        }

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
                    ["isVisible"] = !isExpanded,
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
                                            ["text"] = provider.Name,
                                            ["weight"] = "Bolder",
                                            ["wrap"] = true,
                                        },
                                        new JsonObject
                                        {
                                            ["type"] = "TextBlock",
                                            ["text"] = $"{provider.Model} · {provider.BaseUrl}",
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
                                            ["actions"] = actions,
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
                    ["isVisible"] = isExpanded,
                    ["items"] = new JsonArray
                    {
                        BuildTextInput($"providerName_{provider.Id}", "Provider name", ReadText(draftInputs, $"providerName_{provider.Id}", provider.Name), string.Empty),
                        BuildTextInput($"providerBaseUrl_{provider.Id}", "API base URL", ReadText(draftInputs, $"providerBaseUrl_{provider.Id}", provider.BaseUrl), "http://127.0.0.1:8080/v1"),
                        BuildTextInput($"providerModel_{provider.Id}", "Model", ReadText(draftInputs, $"providerModel_{provider.Id}", provider.Model), "local-model"),
                        BuildPasswordInput(
                            $"providerApiKey_{provider.Id}",
                            "API key (optional)",
                            string.IsNullOrEmpty(provider.ApiKey)
                                ? "Optional bearer token"
                                : "Saved; leave blank to keep it",
                            ReadText(draftInputs, $"providerApiKey_{provider.Id}")),
                        new JsonObject
                        {
                            ["type"] = "TextBlock",
                            ["text"] = "Prompts are sent directly to this provider. Its operator may process or retain them under its own terms and privacy policy. API keys are protected for your Windows account.",
                            ["isSubtle"] = true,
                            ["wrap"] = true,
                            ["spacing"] = "Small",
                        },
                        new JsonObject
                        {
                            ["type"] = "ActionSet",
                            ["horizontalAlignment"] = "Right",
                            ["actions"] = new JsonArray
                            {
                                BuildSubmitAction("Clear API key", $"clear-provider-key:{provider.Id}"),
                                BuildSubmitAction("✓", $"save-provider:{provider.Id}", "Save provider"),
                                BuildSubmitAction(
                                    "✕",
                                    $"cancel-provider:{provider.Id}",
                                    "Cancel editing"),
                            },
                        },
                    },
                },
            },
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
    private static JsonObject BuildCommandEditor(
        UserCommandDefinition command,
        IReadOnlyList<LlmProviderSettings> providers,
        bool isExpanded,
        JsonObject draftInputs)
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
                    ["isVisible"] = !isExpanded,
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
                                            ["text"] = $"{GetExposureTitle(command.EffectiveExposure)} · {GetProviderName(command.ProviderId, providers)} · {command.SendDelayMilliseconds} ms{(string.IsNullOrWhiteSpace(command.CustomRequestArguments) ? string.Empty : " · Custom request arguments")}{(command.EnableAdvancedOutput ? " · Advanced output" : string.Empty)}",
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
                                                BuildSubmitAction(
                                                    "✎",
                                                    $"edit:{command.Id}",
                                                    "Edit command"),
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
                    ["isVisible"] = isExpanded,
                    ["items"] = new JsonArray
                    {
                        BuildTextInput(
                            $"name_{command.Id}",
                            "Command name",
                            ReadText(draftInputs, $"name_{command.Id}", command.Name),
                            string.Empty),
                        BuildTextInput(
                            $"format_{command.Id}",
                            "Prompt format",
                            ReadText(draftInputs, $"format_{command.Id}", command.OutputFormat),
                            "Use {} for the search term, {{ for {, and }} for }.",
                            isMultiline: true),
                        BuildTextInput(
                            $"requestArguments_{command.Id}",
                            "Custom request arguments (optional)",
                            ReadText(
                                draftInputs,
                                $"requestArguments_{command.Id}",
                                command.CustomRequestArguments),
                            "{\"chat_template_kwargs\":{\"enable_thinking\":true}}",
                            isRequired: false,
                            isMultiline: true),
                        new JsonObject
                        {
                            ["type"] = "TextBlock",
                            ["text"] = "Enter a JSON object with provider-specific request fields, such as reasoning options or n. The protected model and messages fields are not allowed. Values are sent in the JSON body only.",
                            ["isSubtle"] = true,
                            ["wrap"] = true,
                            ["spacing"] = "Small",
                        },
                        BuildAdvancedOutputInput(
                            $"advancedOutput_{command.Id}",
                            ReadBoolean(
                                draftInputs,
                                $"advancedOutput_{command.Id}",
                                command.EnableAdvancedOutput)),
                        BuildNumberInput(
                            $"delay_{command.Id}",
                            "Send delay (milliseconds)",
                            ReadInteger(draftInputs, $"delay_{command.Id}", command.SendDelayMilliseconds),
                            0,
                            10_000),
                        BuildChoiceInput(
                            $"provider_{command.Id}",
                            "LLM provider",
                            ReadText(draftInputs, $"provider_{command.Id}", command.ProviderId),
                            providers.Select(provider => (provider.Name, provider.Id))),
                        BuildToggleInput(
                            $"fallback_{command.Id}",
                            "Enable as fallback command",
                            ReadBoolean(
                                draftInputs,
                                $"fallback_{command.Id}",
                                command.EffectiveExposure != CommandExposure.None)),
                        BuildTextInput(
                            $"icon_{command.Id}",
                            "Custom icon file (optional)",
                            ReadText(draftInputs, $"icon_{command.Id}", command.IconPath),
                            "ICO, PNG, JPG, or SVG path; copied into extension storage",
                            isRequired: false),
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
                                BuildSubmitAction(
                                    "✕",
                                    $"cancel:{command.Id}",
                                    "Cancel editing"),
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
        bool isRequired = true,
        bool isMultiline = false) =>
        new()
        {
            ["type"] = "Input.Text",
            ["id"] = id,
            ["label"] = label,
            ["value"] = value,
            ["placeholder"] = placeholder,
            ["isRequired"] = isRequired,
            ["isMultiline"] = isMultiline,
            ["errorMessage"] = $"{label} is required.",
        };

    private static JsonObject BuildPasswordInput(
        string id,
        string label,
        string placeholder,
        string value) =>
        new()
        {
            ["type"] = "Input.Text",
            ["id"] = id,
            ["label"] = label,
            ["placeholder"] = placeholder,
            ["value"] = value,
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

    private static JsonObject BuildChoiceInput(
        string id,
        string label,
        string value,
        IEnumerable<(string Title, string Value)> choices) =>
        new()
        {
            ["type"] = "Input.ChoiceSet",
            ["id"] = id,
            ["label"] = label,
            ["style"] = "compact",
            ["value"] = value,
            ["choices"] = new JsonArray(choices
                .Select(choice => (JsonNode)new JsonObject
                {
                    ["title"] = choice.Title,
                    ["value"] = choice.Value,
                })
                .ToArray()),
        };

    private static JsonObject BuildToggleInput(string id, string title, bool value) =>
        new()
        {
            ["type"] = "Input.Toggle",
            ["id"] = id,
            ["title"] = title,
            ["value"] = value ? "true" : "false",
            ["valueOn"] = "true",
            ["valueOff"] = "false",
        };

    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "The help card adds only primitive values and explicit JsonNode instances.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "The help card adds only primitive values and explicit JsonNode instances.")]
    private static JsonObject BuildAdvancedOutputInput(string id, bool value)
    {
        var helpId = $"{id}_help";
        return new JsonObject
        {
            ["type"] = "Container",
            ["items"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "ColumnSet",
                    ["spacing"] = "None",
                    ["columns"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "Column",
                            ["width"] = "auto",
                            ["verticalContentAlignment"] = "Center",
                            ["items"] = new JsonArray
                            {
                                BuildToggleInput(id, "Enable advanced output format", value),
                            },
                        },
                        new JsonObject
                        {
                            ["type"] = "Column",
                            ["width"] = "auto",
                            ["spacing"] = "Small",
                            ["verticalContentAlignment"] = "Center",
                            ["items"] = new JsonArray
                            {
                                new JsonObject
                                {
                                    ["type"] = "RichTextBlock",
                                    ["inlines"] = new JsonArray
                                    {
                                        new JsonObject
                                        {
                                            ["type"] = "TextRun",
                                            ["text"] = "ⓘ",
                                            ["isSubtle"] = true,
                                            ["selectAction"] = new JsonObject
                                            {
                                                ["type"] = "Action.ToggleVisibility",
                                                ["tooltip"] = "JSON result object or ordered array with optional title, subtitle, details, section, and tags fields. Click for details.",
                                                ["targetElements"] = new JsonArray(helpId),
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
                BuildAdvancedOutputHelp(helpId),
            },
        };
    }

    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "The help content adds only primitive values and explicit JsonNode instances.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "The help content adds only primitive values and explicit JsonNode instances.")]
    private static JsonObject BuildAdvancedOutputHelp(string id) =>
        new()
        {
            ["type"] = "Container",
            ["id"] = id,
            ["isVisible"] = false,
            ["spacing"] = "Small",
            ["style"] = "emphasis",
            ["items"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "TextBlock",
                    ["text"] = "Advanced output format",
                    ["weight"] = "Bolder",
                    ["wrap"] = true,
                },
                new JsonObject
                {
                    ["type"] = "TextBlock",
                    ["text"] = "The response must be one JSON result object, or an ordered array of result objects when the prompt asks for variations. All five fields are optional. If present, title, subtitle, details, and section must be strings; tags must be an array of strings. Property names are lowercase and case-sensitive. Extra properties are ignored.",
                    ["wrap"] = true,
                },
                new JsonObject
                {
                    ["type"] = "TextBlock",
                    ["text"] = "{\n  \"title\": \"Result title\",\n  \"subtitle\": \"Supporting text\",\n  \"details\": \"Full content copied when selected\",\n  \"section\": \"Section name\",\n  \"tags\": [\"tag one\", \"tag two\"]\n}",
                    ["fontType"] = "Monospace",
                    ["wrap"] = true,
                },
                new JsonObject
                {
                    ["type"] = "TextBlock",
                    ["text"] = "Arrays preserve element order and may be empty. Invalid array elements appear as error results with their original JSON in details; root fallback results skip them and use the first valid object. A single unlabelled or json Markdown code fence is accepted. Other invalid formatted output uses the normal output rules. The global system prompt controls this format and can be edited in Global settings.",
                    ["isSubtle"] = true,
                    ["wrap"] = true,
                },
            },
        };

    private static string GetProviderName(
        string providerId,
        IReadOnlyList<LlmProviderSettings> providers) =>
        providers.FirstOrDefault(provider =>
            string.Equals(provider.Id, providerId, StringComparison.Ordinal))?.Name ??
        providers[0].Name;

    private static string GetExposureTitle(CommandExposure exposure) => exposure switch
    {
        CommandExposure.None => "Command only",
        _ => "Fallback command",
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

            if (!CustomRequestArguments.TryParse(
                command.CustomRequestArguments,
                out var customArguments,
                out var customArgumentsError))
            {
                return $"Command '{command.Name}': {customArgumentsError}";
            }

            customArguments?.Dispose();
        }

        return null;
    }

    private CommandResult SetCommandEditorVisibility(string commandId, bool isExpanded)
    {
        if (!_store.GetCommands().Any(command =>
            string.Equals(command.Id, commandId, StringComparison.Ordinal)))
        {
            return ShowError("The command no longer exists.");
        }

        if (isExpanded)
        {
            _expandedCommandIds.Add(commandId);
        }
        else
        {
            _expandedCommandIds.Remove(commandId);
            RemoveDraftInputsForCommand(commandId);
        }

        Refresh();
        return CommandResult.KeepOpen();
    }

    private CommandResult SetProviderEditorVisibility(string providerId, bool isExpanded)
    {
        if (_providerSettingsStore.Get(providerId) is null)
        {
            return ShowError("The provider no longer exists.");
        }

        if (isExpanded)
        {
            _expandedProviderIds.Add(providerId);
        }
        else
        {
            _expandedProviderIds.Remove(providerId);
            RemoveDraftInputsForProvider(providerId);
        }

        Refresh();
        return CommandResult.KeepOpen();
    }

    private void CaptureDraftInputs(JsonObject input)
    {
        foreach (var property in input)
        {
            if (!string.Equals(property.Key, "actionId", StringComparison.Ordinal))
            {
                _draftInputs[property.Key] = property.Value?.DeepClone();
            }
        }
    }

    private void RemoveDraftInputsForCommand(string commandId)
    {
        RemoveDraftInput($"name_{commandId}");
        RemoveDraftInput($"format_{commandId}");
        RemoveDraftInput($"requestArguments_{commandId}");
        RemoveDraftInput($"advancedOutput_{commandId}");
        RemoveDraftInput($"delay_{commandId}");
        RemoveDraftInput($"provider_{commandId}");
        RemoveDraftInput($"fallback_{commandId}");
        RemoveDraftInput($"icon_{commandId}");
    }

    private void RemoveDraftInputsForProvider(string providerId)
    {
        RemoveDraftInput($"providerName_{providerId}");
        RemoveDraftInput($"providerBaseUrl_{providerId}");
        RemoveDraftInput($"providerModel_{providerId}");
        RemoveDraftInput($"providerApiKey_{providerId}");
    }

    private void RemoveDraftInput(string key) => _draftInputs.Remove(key);

    private static int ReadInteger(JsonObject input, string key, int fallback) =>
        TryReadInteger(input, key, out var value) ? value : fallback;

    private static string ReadText(JsonObject input, string key, string fallback = "") =>
        input[key]?.GetValue<string>() ?? fallback;

    private static bool ReadBoolean(JsonObject input, string key, bool fallback = false) =>
        input[key] is null
            ? fallback
            : string.Equals(input[key]?.ToString(), "true", StringComparison.OrdinalIgnoreCase);

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
