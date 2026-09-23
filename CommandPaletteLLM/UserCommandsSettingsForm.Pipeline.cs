using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json.Nodes;

namespace CommandPaletteLLM;

internal sealed partial class UserCommandsSettingsForm
{
    private readonly Dictionary<string, UserCommandDefinition> _pipelineDrafts = new(StringComparer.Ordinal);
    private readonly HashSet<string> _expandedStepIds = new(StringComparer.Ordinal);

    private UserCommandDefinition GetPipelineDraft(UserCommandDefinition command) =>
        _pipelineDrafts.TryGetValue(command.Id, out var draft) ? draft : command;

    private CommandResult EditPipeline(string actionId)
    {
        var parts = actionId.Split(':');
        if (parts.Length < 3)
        {
            return ShowError("The step action could not be read.");
        }

        var command = _store.GetCommands().FirstOrDefault(command => command.Id == parts[1]);
        if (command is null)
        {
            return ShowError("The command no longer exists.");
        }

        command = GetPipelineDraft(command).Clone();
        ReadStepInputs(command, _draftInputs);
        var operation = parts[2];
        if (operation == "mode")
        {
            command.Mode = ReadText(_draftInputs, $"mode_{command.Id}", command.Mode.ToString()) == "Pipeline"
                ? CommandMode.Pipeline : CommandMode.Default;
            if (command.Mode == CommandMode.Pipeline && command.Steps.Count == 0)
            {
                var providerId = ReadText(_draftInputs, $"provider_{command.Id}", command.ProviderId);
                command.Steps.Add(new CommandStepDefinition
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = "Step 1",
                    Prompt = ReadText(_draftInputs, $"format_{command.Id}", command.OutputFormat),
                    ProviderId = providerId,
                    RequestTemplateId = ReadText(_draftInputs, $"requestTemplate_{command.Id}", command.RequestTemplateId),
                    CustomRequestArguments = ReadText(_draftInputs, $"requestArguments_{command.Id}", command.CustomRequestArguments),
                });
                _expandedStepIds.Add(StepCardId(command.Id, command.Steps[0].Id));
            }
        }
        else if (operation is "add" or "add-system")
        {
            var selectedKind = operation == "add-system" || string.Equals(
                ReadText(_draftInputs, $"newStepType_{command.Id}", nameof(CommandStepKind.User)),
                nameof(CommandStepKind.AdvancedOutput),
                StringComparison.Ordinal)
                    ? CommandStepKind.AdvancedOutput
                    : CommandStepKind.User;
            var step = NewStep(command, selectedKind);
            command.Steps.Add(step);
            _expandedStepIds.Add(StepCardId(command.Id, step.Id));
            _draftInputs[$"newStepType_{command.Id}"] = nameof(CommandStepKind.User);
        }
        else if (parts.Length == 4)
        {
            var index = command.Steps.FindIndex(step => step.Id == parts[3]);
            if (index < 0)
            {
                return ShowError("The step no longer exists.");
            }

            if (operation == "remove")
            {
                _expandedStepIds.Remove(StepCardId(command.Id, command.Steps[index].Id));
                command.Steps.RemoveAt(index);
            }
            else if (operation is "expand" or "collapse" or "edit-system")
            {
                var cardId = StepCardId(command.Id, command.Steps[index].Id);
                if (operation == "collapse")
                {
                    _expandedStepIds.Remove(cardId);
                }
                else
                {
                    _expandedStepIds.Add(cardId);
                    if (operation == "edit-system")
                    {
                        _globalSettingsExpanded = true;
                    }
                }
            }
            else if (operation is "up" or "down")
            {
                var destination = index + (operation == "up" ? -1 : 1);
                if (destination >= 0 && destination < command.Steps.Count)
                {
                    (command.Steps[index], command.Steps[destination]) =
                        (command.Steps[destination], command.Steps[index]);
                }
            }
            else if (operation == "provider")
            {
                var step = command.Steps[index];
                var template = _requestTemplateStore.Get(step.RequestTemplateId);
                if (template is null || template.ProviderId != step.ProviderId)
                {
                    step.RequestTemplateId = string.Empty;
                    _draftInputs[$"step_{command.Id}_{step.Id}_template"] = string.Empty;
                }
            }
        }

        _pipelineDrafts[command.Id] = command;
        _expandedCommandIds.Add(command.Id);
        Refresh();
        return CommandResult.KeepOpen();
    }

    private static CommandStepDefinition NewStep(UserCommandDefinition command, CommandStepKind kind) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Name = kind == CommandStepKind.AdvancedOutput ? "Advanced output format" : $"Step {command.Steps.Count + 1}",
        Kind = kind,
        ProviderId = command.Steps.LastOrDefault()?.ProviderId ?? command.ProviderId,
    };

    private static void ReadStepInputs(UserCommandDefinition command, JsonObject inputs)
    {
        foreach (var step in command.Steps)
        {
            var key = $"step_{command.Id}_{step.Id}_";
            step.Name = ReadText(inputs, key + "name", step.Name).Trim();
            step.Prompt = ReadText(inputs, key + "prompt", step.Prompt);
            step.ProviderId = ReadText(inputs, key + "provider", step.ProviderId);
            step.RequestTemplateId = ReadText(inputs, key + "template", step.RequestTemplateId);
            step.CustomRequestArguments = ReadText(inputs, key + "arguments", step.CustomRequestArguments);
        }
    }

    private string? ValidatePipeline(UserCommandDefinition command)
    {
        if (command.Steps.Count == 0)
        {
            return "A pipeline must contain at least one step.";
        }

        foreach (var step in command.Steps)
        {
            if (!UserCommandStore.IsValidStep(step))
            {
                return $"Step '{step.Name}' needs a name and a valid prompt using {{}} for its input.";
            }

            if (_providerSettingsStore.Get(step.ProviderId) is null)
            {
                return $"Step '{step.Name}' must use an existing LLM provider.";
            }

            var template = _requestTemplateStore.Get(step.RequestTemplateId);
            if (template is null || template.ProviderId != step.ProviderId)
            {
                step.RequestTemplateId = string.Empty;
            }

            if (!CustomRequestArguments.TryParse(step.CustomRequestArguments, out var arguments, out var error))
            {
                return $"Step '{step.Name}': {error}";
            }

            arguments?.Dispose();
        }

        return null;
    }

    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Uses explicit JsonNode instances and primitive values.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Uses explicit JsonNode instances and primitive values.")]
    private void ConfigureCommandModeCard(
        JsonObject card,
        UserCommandDefinition command,
        IReadOnlyList<LlmProviderSettings> providers,
        IReadOnlyList<JsonRequestTemplateDefinition> templates,
        JsonObject drafts)
    {
        var editor = card["items"]![1]!["items"]!.AsArray();
        if (command.Mode == CommandMode.Pipeline)
        {
            // Replace the default request fields, leaving name, delay, fallback and icon on the command.
            var start = editor.Select((node, index) => (node, index))
                .First(item => item.node?["id"]?.ToString() == $"format_{command.Id}").index;
            var end = editor.Select((node, index) => (node, index))
                .First(item => item.node?["id"]?.ToString() == $"delay_{command.Id}").index;
            for (var index = end - 1; index >= start; index--)
            {
                editor.RemoveAt(index);
            }

            editor.Insert(start, BuildPipelineEditor(command, providers, templates, drafts));
            editor.Insert(start + 1, BuildToggleInput($"advancedOutput_{command.Id}", "Enable advanced output format",
                ReadBoolean(drafts, $"advancedOutput_{command.Id}", command.EnableAdvancedOutput)));
            var summary = card["items"]![0]!["items"]![0]!["columns"]![0]!["items"]!.AsArray();
            summary[1]!["text"] = string.Join(" → ", command.Steps.Select(step => step.Name).Append("Output"));
            summary[2]!["text"] = $"Pipeline · {command.Steps.Count} steps · {GetExposureTitle(command.EffectiveExposure)} · {command.SendDelayMilliseconds} ms";
            summary[2]!["wrap"] = true;
        }

        editor.Insert(1, BuildChoiceInput($"mode_{command.Id}", "Command mode", command.Mode.ToString(),
            [("Default (single step)", "Default"), ("Pipeline of steps", "Pipeline")]));
        editor.Insert(2, new JsonObject
        {
            ["type"] = "ActionSet",
            ["actions"] = new JsonArray(BuildSubmitAction("Apply mode", $"pipeline:{command.Id}:mode")),
        });
    }

    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Uses explicit JsonNode instances and primitive values.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Uses explicit JsonNode instances and primitive values.")]
    private JsonObject BuildPipelineEditor(
        UserCommandDefinition command,
        IReadOnlyList<LlmProviderSettings> providers,
        IReadOnlyList<JsonRequestTemplateDefinition> templates,
        JsonObject drafts)
    {
        var items = new JsonArray(new JsonObject
        {
            ["type"] = "TextBlock",
            ["text"] = "Steps run from top to bottom. {} is the search text in the first step and the previous step's response thereafter. Multiple responses are joined with blank lines. The last step feeds Output automatically. Enable advanced output on the command to display rich results.",
            ["wrap"] = true,
        });
        for (var index = 0; index < command.Steps.Count; index++)
        {
            var step = command.Steps[index];
            var key = $"step_{command.Id}_{step.Id}_";
            var action = $"pipeline:{command.Id}:";
            var providerId = ReadText(drafts, key + "provider", step.ProviderId);
            var matchingTemplates = templates.Where(template => template.ProviderId == providerId).ToArray();
            var templateId = ReadText(drafts, key + "template", step.RequestTemplateId);
            if (!matchingTemplates.Any(template => template.Id == templateId))
            {
                templateId = string.Empty;
            }

            var cardId = StepCardId(command.Id, step.Id);
            var isExpanded = _expandedStepIds.Contains(cardId);
            var fields = new JsonArray(BuildTextInput(key + "name", "Step name",
                ReadText(drafts, key + "name", step.Name), string.Empty));
            if (step.Kind == CommandStepKind.User)
            {
                fields.Add(BuildTextInput(key + "prompt", "Prompt format", ReadText(drafts, key + "prompt", step.Prompt),
                    "Use {} for input, {{ for {, and }} for }.", isMultiline: true));
            }
            else
            {
                fields.Add(new JsonObject
                {
                    ["type"] = "TextBlock",
                    ["text"] = "Formats its input using the shared prompt in the Advanced output format system-step card. The built-in step cannot be deleted and can appear anywhere in the chain. Removing it here only removes its reference from this pipeline.",
                    ["wrap"] = true,
                    ["isSubtle"] = true,
                });
                fields.Add(new JsonObject
                {
                    ["type"] = "ActionSet",
                    ["actions"] = new JsonArray(BuildSubmitAction("Edit shared system prompt", action + $"edit-system:{step.Id}")),
                });
            }

            fields.Add(BuildChoiceInput(key + "provider", "LLM provider", providerId,
                providers.Select(provider => (provider.Name, provider.Id))));
            fields.Add(new JsonObject
            {
                ["type"] = "ActionSet",
                ["actions"] = new JsonArray(BuildSubmitAction("Apply provider", action + $"provider:{step.Id}")),
            });
            fields.Add(BuildChoiceInput(key + "template", "JSON request template", templateId,
                new[] { ("None", string.Empty) }.Concat(matchingTemplates.Select(template => (template.Name, template.Id)))));
            fields.Add(BuildTextInput(key + "arguments", "Custom request arguments (optional)",
                ReadText(drafts, key + "arguments", step.CustomRequestArguments), "{\"temperature\":0.7}",
                isRequired: false, isMultiline: true));
            var inputSource = index == 0 ? "Input" : command.Steps[index - 1].Name;
            var header = new JsonObject
            {
                ["type"] = "ColumnSet",
                ["columns"] = new JsonArray(
                    new JsonObject
                    {
                        ["type"] = "Column",
                        ["width"] = "stretch",
                        ["items"] = new JsonArray(
                            new JsonObject
                            {
                                ["type"] = "TextBlock",
                                ["text"] = $"{index + 1}. {ReadText(drafts, key + "name", step.Name)}",
                                ["weight"] = "Bolder",
                                ["wrap"] = true,
                            },
                            new JsonObject
                            {
                                ["type"] = "TextBlock",
                                ["text"] = $"{(step.Kind == CommandStepKind.AdvancedOutput ? "Advanced output format · System step" : "User step")} · Input: {inputSource} · {GetProviderName(providerId, providers)}",
                                ["isSubtle"] = true,
                                ["spacing"] = "Small",
                                ["wrap"] = true,
                            }),
                    },
                    new JsonObject
                    {
                        ["type"] = "Column",
                        ["width"] = "auto",
                        ["items"] = new JsonArray(new JsonObject
                        {
                            ["type"] = "ActionSet",
                            ["actions"] = new JsonArray(BuildSubmitAction(
                                isExpanded ? "Collapse step" : "Edit step",
                                action + $"{(isExpanded ? "collapse" : "expand")}:{step.Id}")),
                        }),
                    }),
            };
            items.Add(new JsonObject
            {
                ["type"] = "Container",
                ["id"] = cardId,
                ["style"] = "default",
                ["spacing"] = "Medium",
                ["items"] = new JsonArray(
                    header,
                    new JsonObject
                    {
                        ["type"] = "Container",
                        ["id"] = $"step_editor_{command.Id}_{step.Id}",
                        ["isVisible"] = isExpanded,
                        ["spacing"] = "Medium",
                        ["items"] = fields,
                    },
                    new JsonObject
                    {
                        ["type"] = "ActionSet",
                        ["spacing"] = "Medium",
                        ["actions"] = new JsonArray(
                            BuildSubmitAction("Move up", action + $"up:{step.Id}", isEnabled: index > 0),
                            BuildSubmitAction("Move down", action + $"down:{step.Id}", isEnabled: index < command.Steps.Count - 1),
                            BuildSubmitAction("Remove step", action + $"remove:{step.Id}")),
                    }),
            });
        }

        var newStepTypeId = $"newStepType_{command.Id}";
        items.Add(new JsonObject
        {
            ["type"] = "ColumnSet",
            ["spacing"] = "Medium",
            ["columns"] = new JsonArray(
                new JsonObject
                {
                    ["type"] = "Column",
                    ["width"] = "stretch",
                    ["items"] = new JsonArray(BuildChoiceInput(
                        newStepTypeId,
                        "Step type",
                        ReadText(drafts, newStepTypeId, nameof(CommandStepKind.User)),
                        [
                            ("User step", nameof(CommandStepKind.User)),
                            ("Advanced output format (system step)", nameof(CommandStepKind.AdvancedOutput)),
                        ])),
                },
                new JsonObject
                {
                    ["type"] = "Column",
                    ["width"] = "auto",
                    ["verticalContentAlignment"] = "Bottom",
                    ["items"] = new JsonArray(new JsonObject
                    {
                        ["type"] = "ActionSet",
                        ["actions"] = new JsonArray(BuildSubmitAction("Add step", $"pipeline:{command.Id}:add")),
                    }),
                }),
        });
        items.Add(new JsonObject { ["type"] = "TextBlock", ["text"] = "↓ Output", ["weight"] = "Bolder" });
        return new JsonObject { ["type"] = "Container", ["id"] = $"pipeline_steps_{command.Id}", ["items"] = items };
    }

    private static string StepCardId(string commandId, string stepId) => $"step_card_{commandId}_{stepId}";
}
