using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace CommandPaletteLLM.Tests;

public sealed class PipelineTests
{
    [Fact]
    public async Task Pipeline_ChainsAllChoicesWithPerStepProviderAndArguments()
    {
        var steps = new[]
        {
            Step("translate", "Translate {}", "first"),
            Step("structure", "Structure {}", "second"),
            Step("format", "{}", "first", CommandStepKind.AdvancedOutput),
            Step("finish", "Finish {}", "second"),
        };
        steps[1].CustomRequestArguments = "{\"temperature\":0.1}";
        var first = new RecordingClient(["one", "two"], ["formatted"]);
        var second = new RecordingClient(["structured"], ["final"]);
        var pipeline = new PipelineLlmClient(steps,
            id => id == "first" ? first : second, step => "template-" + step.Id, "system instructions");

        var result = await pipeline.CompleteAsync("input", "ignored", "ignored", "ignored", CancellationToken.None);

        Assert.Equal("final", Assert.Single(result));
        Assert.Equal("Translate input", first.Calls[0].Prompt);
        Assert.Equal("Structure one\n\ntwo", second.Calls[0].Prompt);
        Assert.EndsWith("\n\nstructured", first.Calls[1].Prompt, StringComparison.Ordinal);
        Assert.Equal("Finish formatted", second.Calls[1].Prompt);
        Assert.Null(first.Calls[0].SystemPrompt);
        Assert.Equal("system instructions", first.Calls[1].SystemPrompt);
        Assert.Null(second.Calls[1].SystemPrompt);
        Assert.Equal("template-structure", second.Calls[0].Template);
        Assert.Equal("{\"temperature\":0.1}", second.Calls[0].Arguments);
    }

    [Fact]
    public async Task Pipeline_CancellationStopsBeforeNextStepEvenWhenProviderIgnoresToken()
    {
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        var client = new CallbackClient(_ =>
        {
            calls++;
            cancellation.Cancel();
            return Task.FromResult<IReadOnlyList<string>>(["stale"]);
        });
        var pipeline = new PipelineLlmClient([Step("one"), Step("two")], _ => client, _ => "", "");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            pipeline.CompleteAsync("input", null, "", "", cancellation.Token));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Pipeline_FailureNamesStepAndDoesNotRunLaterSteps()
    {
        var calls = 0;
        var client = new CallbackClient(_ =>
        {
            calls++;
            throw new LlmRequestException("Provider failed");
        });
        var pipeline = new PipelineLlmClient([Step("Translate"), Step("Finish")], _ => client, _ => "", "");

        var error = await Assert.ThrowsAsync<LlmRequestException>(() =>
            pipeline.CompleteAsync("input", null, "", "", CancellationToken.None));
        Assert.Contains("Translate", error.Message, StringComparison.Ordinal);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Pipeline_EmptyResponseStopsChain()
    {
        var client = new RecordingClient(Array.Empty<string>());
        var pipeline = new PipelineLlmClient([Step("Empty"), Step("Next")], _ => client, _ => "", "");
        await Assert.ThrowsAsync<LlmRequestException>(() =>
            pipeline.CompleteAsync("input", null, "", "", CancellationToken.None));
        Assert.Single(client.Calls);
    }

    [Theory]
    [InlineData("[{\"title\":\"One\"},{\"title\":\"Two\"}]", "One", 2)]
    [InlineData("[{\"title\":\"One\"},{\"tags\":42}]", "One", 2)]
    [InlineData("plain output", "plain output", 1)]
    [InlineData("{\"title\":42}", "{\"title\":42}", 1)]
    public void Pipeline_AdvancedOutputRetainsExistingPageAndFallbackBehavior(string output, string expectedTitle, int count)
    {
        var definition = Pipeline();
        definition.EnableAdvancedOutput = true;
        using var page = new FormattedCommandPage(definition, new RecordingClient([output]), TimeSpan.Zero);
        page.SearchText = "query";
        Assert.Equal(count, page.GetItems().Length);
        Assert.Equal(expectedTitle, page.GetItems()[0].Title);

        var fallback = new FormattedFallbackItem(definition, new RecordingClient([output]), TimeSpan.Zero);
        fallback.UpdateQuery("query");
        Assert.Equal(expectedTitle, fallback.Title);
    }

    [Fact]
    public void Pipeline_AdvancedOutputDisabledShowsJsonAsRegularText()
    {
        const string response = "{\"title\":\"A title\"}";
        var definition = Pipeline();
        using var page = new FormattedCommandPage(definition, new RecordingClient([response]), TimeSpan.Zero);
        page.SearchText = "query";
        Assert.Equal(response, Assert.Single(page.GetItems()).Title);
        var fallback = new FormattedFallbackItem(definition, new RecordingClient([response]), TimeSpan.Zero);
        fallback.UpdateQuery("query");
        Assert.Equal(response, fallback.Title);
    }

    [Fact]
    public void Provider_RunsPipelineForPageAndFallbackWithStableIdentities()
    {
        var store = new UserCommandStore(filePath: null);
        var definition = Pipeline();
        definition.Steps.Add(Step("two", "Second {}"));
        definition.EnableAdvancedOutput = true;
        store.ReplaceAll([definition]);
        var client = new RecordingClient(["intermediate"], ["{\"title\":\"Page result\"}"],
            ["intermediate"], ["{\"title\":\"Fallback result\"}"]);
        var provider = new CommandPaletteLLMCommandsProvider(store, new LlmProviderSettingsStore(filePath: null), client);
        var command = Assert.Single(provider.TopLevelCommands());
        Assert.Equal("CommandPaletteLLM.Command.pipeline", command.Command.Id);
        var page = Assert.IsType<FormattedCommandPage>(command.Command);
        page.SearchText = "source";
        Assert.Equal("Page result", Assert.Single(page.GetItems()).Title);
        Assert.Equal("source", client.Calls[0].Prompt);
        Assert.Equal("Second intermediate", client.Calls[1].Prompt);

        var fallback = Assert.IsType<FormattedFallbackItem>(Assert.Single(provider.FallbackCommands()));
        fallback.UpdateQuery("source");
        Assert.Equal("Fallback result", fallback.Title);
        Assert.Equal(4, client.Calls.Count);
        page.Dispose();
    }

    [Fact]
    public void Settings_SwitchesModesPreservesDefaultAndCancelsPipelineEdits()
    {
        var command = new UserCommandDefinition
        {
            Id = "pipeline", Name = "Translate", OutputFormat = "Translate {}",
            EnableAdvancedOutput = true, CustomRequestArguments = "{\"temperature\":0.3}",
        };
        var (store, form) = Form(command);
        Submit(form, "pipeline:pipeline:mode", new JsonObject { ["mode_pipeline"] = "Pipeline" });
        Assert.Equal(CommandMode.Default, Assert.Single(store.GetCommands()).Mode);
        Assert.Contains("\"id\":\"advancedOutput_pipeline\"", form.TemplateJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"id\":\"globalAdvancedOutputPrompt_pipeline\"", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("System step", form.TemplateJson, StringComparison.Ordinal);
        Submit(form, "save:pipeline");
        var saved = Assert.Single(store.GetCommands());
        Assert.Equal(CommandMode.Pipeline, saved.Mode);
        Assert.Single(saved.Steps);
        Assert.Equal("Translate {}", saved.Steps[0].Prompt);
        Assert.Equal(command.CustomRequestArguments, saved.Steps[0].CustomRequestArguments);
        Submit(form, "pipeline:pipeline:add");
        Submit(form, "cancel:pipeline");
        Assert.Single(Assert.Single(store.GetCommands()).Steps);
        Submit(form, "save:pipeline", new JsonObject { ["mode_pipeline"] = "Default" });
        saved = Assert.Single(store.GetCommands());
        Assert.Equal(CommandMode.Default, saved.Mode);
        Assert.True(saved.EnableAdvancedOutput);
        Assert.Contains("\"id\":\"globalAdvancedOutputPrompt_pipeline\"", form.TemplateJson, StringComparison.Ordinal);
        Assert.Equal("Translate {}", saved.OutputFormat);
        Assert.Single(saved.Steps);
        Assert.Contains("\"id\":\"advancedOutput_pipeline\"", form.TemplateJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Settings_ReordersStepsPreservesDraftAndAllowsRemovingSystemStep()
    {
        var command = Pipeline();
        command.Steps.Add(Step("system", kind: CommandStepKind.AdvancedOutput));
        var (store, form) = Form(command);
        Submit(form, "pipeline:pipeline:up:system", new JsonObject
        {
            ["step_pipeline_one_name"] = "Translate",
            ["step_pipeline_one_prompt"] = "New prompt {}",
            ["name_pipeline"] = "Renamed command",
        });
        Submit(form, "save:pipeline");
        var saved = Assert.Single(store.GetCommands());
        Assert.Equal("system", saved.Steps[0].Id);
        Assert.Equal("Translate", saved.Steps[1].Name);
        Assert.Equal("New prompt {}", saved.Steps[1].Prompt);
        Assert.Equal("Renamed command", saved.Name);
        Submit(form, "pipeline:pipeline:remove:system");
        Submit(form, "save:pipeline");
        Assert.Equal("one", Assert.Single(Assert.Single(store.GetCommands()).Steps).Id);
        Assert.Contains("Advanced output format (system step)", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("\"title\":\"Add step\"", form.TemplateJson, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("name", "")]
    [InlineData("prompt", "broken {")]
    [InlineData("arguments", "[]")]
    [InlineData("provider", "missing")]
    public void Settings_RejectsInvalidStepWithoutSaving(string field, string value)
    {
        var (store, form) = Form(Pipeline());
        Submit(form, "save:pipeline", new JsonObject { [$"step_pipeline_one_{field}"] = value });
        Assert.Contains("Step", form.TemplateJson, StringComparison.Ordinal);
        var step = Assert.Single(Assert.Single(store.GetCommands()).Steps);
        Assert.Equal("one", step.Name);
        Assert.Equal("{}", step.Prompt);
        Assert.Equal("", step.CustomRequestArguments);
        Assert.Equal("default", step.ProviderId);
    }

    [Fact]
    public void Settings_RejectsEmptyPipeline()
    {
        var (store, form) = Form(Pipeline());
        Submit(form, "pipeline:pipeline:remove:one");
        Submit(form, "save:pipeline");
        Assert.Contains("at least one step", form.TemplateJson, StringComparison.Ordinal);
        Assert.Single(Assert.Single(store.GetCommands()).Steps);
    }

    [Fact]
    public void Store_RoundTripsStepsAndReturnsIndependentCopies()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pipeline-{Guid.NewGuid():N}.json");
        try
        {
            var store = new UserCommandStore(path);
            var command = Pipeline();
            command.Steps.Add(Step("system", kind: CommandStepKind.AdvancedOutput));
            store.ReplaceAll([command]);
            command.Steps[0].Name = "mutated original";
            var saved = Assert.Single(new UserCommandStore(path).GetCommands());
            Assert.Equal(CommandMode.Pipeline, saved.Mode);
            Assert.Equal("one", saved.Steps[0].Name);
            Assert.Equal(CommandStepKind.AdvancedOutput, saved.Steps[1].Kind);
            saved.Steps.Clear();
            Assert.Equal(2, Assert.Single(store.GetCommands()).Steps.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LegacyJson_DefaultsToSingleStepMode()
    {
        var commands = JsonSerializer.Deserialize("""[{"Id":"old","Name":"Old","OutputFormat":"{}"}]""",
            CommandPaletteJsonContext.Default.ListUserCommandDefinition)!;
        var command = Assert.Single(commands);
        Assert.Equal(CommandMode.Default, command.Mode);
        Assert.Empty(command.Steps);
        Assert.True(UserCommandStore.IsValidStoredCommand(command));
    }

    [Theory]
    [InlineData("delete")]
    [InlineData("reassign")]
    [InlineData("edit")]
    public void Settings_UpdatesStepTemplateReferences(string operation)
    {
        var store = new UserCommandStore(filePath: null);
        var command = Pipeline();
        command.Steps[0].RequestTemplateId = "template";
        store.ReplaceAll([command]);
        var providers = new LlmProviderSettingsStore(filePath: null);
        var remote = providers.Get().Clone();
        remote.Id = "remote";
        remote.Name = "Remote";
        providers.ReplaceAll([providers.Get(), remote]);
        var templates = new JsonRequestTemplateStore();
        templates.ReplaceAll([new JsonRequestTemplateDefinition
        {
            Id = "template", Name = "Template", ProviderId = "default", RequestArguments = "{}",
        }]);
        var form = new UserCommandsSettingsForm(store, providers, new GlobalSettingsStore(filePath: null),
            templates, () => { }, () => { }, () => { }, () => { });

        Submit(form, operation == "delete" ? "delete-request-template:template" : "save-request-template:template",
            new JsonObject { ["requestTemplateProvider_template"] = operation == "reassign" ? "remote" : "default" });

        Assert.Equal(operation == "edit" ? "template" : "",
            Assert.Single(Assert.Single(store.GetCommands()).Steps).RequestTemplateId);
    }

    [Fact]
    public void Settings_PreventsDeletingProviderUsedOnlyByStep()
    {
        var store = new UserCommandStore(filePath: null);
        var command = Pipeline();
        command.Steps[0].ProviderId = "remote";
        store.ReplaceAll([command]);
        var providers = new LlmProviderSettingsStore(filePath: null);
        var remote = providers.Get().Clone();
        remote.Id = "remote";
        remote.Name = "Remote";
        providers.ReplaceAll([providers.Get(), remote]);
        var form = new UserCommandsSettingsForm(store, providers, new GlobalSettingsStore(filePath: null),
            () => { }, () => { }, () => { });
        Submit(form, "delete-provider:remote");
        Assert.NotNull(providers.Get("remote"));
        Assert.Contains("another provider", form.TemplateJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Document_RoundTripsPipelineAndNormalizesMismatchedStepTemplates()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pipeline-document-{Guid.NewGuid():N}.json");
        try
        {
            var document = new SettingsDocumentStore(path, null, null, null, new DpapiDataProtector());
            var command = Pipeline();
            command.Steps[0].RequestTemplateId = "missing";
            document.ReplaceCommands([command]);
            var reloaded = new SettingsDocumentStore(path, null, null, null, new DpapiDataProtector());
            var saved = Assert.Single(reloaded.GetCommands());
            Assert.Equal(CommandMode.Pipeline, saved.Mode);
            Assert.Equal("one", Assert.Single(saved.Steps).Name);
            Assert.Empty(saved.Steps[0].RequestTemplateId);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task PipelineMonitor_ChecksAllProvidersAndForwardsStatusChanges()
    {
        var first = new Monitor { Status = LlmEndpointStatus.Available };
        var second = new Monitor { Status = LlmEndpointStatus.Unavailable };
        var monitor = new PipelineEndpointMonitor([first, second]);
        var changes = 0;
        void Changed() => changes++;
        monitor.StatusChanged += Changed;
        Assert.Equal(LlmEndpointStatus.Unavailable, monitor.Status);
        Assert.False(await monitor.CheckAsync(true, CancellationToken.None));
        Assert.Equal(1, first.Checks);
        Assert.Equal(1, second.Checks);
        second.Status = LlmEndpointStatus.Available;
        second.Notify();
        Assert.Equal(1, changes);
        Assert.Equal(LlmEndpointStatus.Available, monitor.Status);
        monitor.StatusChanged -= Changed;
        second.Notify();
        Assert.Equal(1, changes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SystemStep_SharedPromptCanBeSavedAndResetIndependentlyOfOutputToggle(bool advancedOutput)
    {
        var first = Pipeline();
        first.EnableAdvancedOutput = advancedOutput;
        first.Steps = [Step("system", kind: CommandStepKind.AdvancedOutput)];
        var second = first.Clone();
        second.Id = "second";
        second.Name = "Second";
        var store = new UserCommandStore(filePath: null);
        store.ReplaceAll([first, second]);
        var global = new GlobalSettingsStore(filePath: null);
        var client = new RecordingClient(["one"], ["two"], ["three"], ["four"]);
        var provider = new CommandPaletteLLMCommandsProvider(store,
            new LlmProviderSettingsStore(filePath: null), global, client);
        var form = Assert.IsType<UserCommandsSettingsForm>(Assert.Single(provider.Settings!.SettingsPage.GetContent()));
        var card = JsonNode.Parse(form.TemplateJson)!["body"]!.AsArray()
            .Single(node => node?["id"]?.ToString() == "system_step_advanced_output")!;
        Assert.DoesNotContain("\"actionId\":\"delete", card.ToJsonString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reset-global", card.ToJsonString(), StringComparison.Ordinal);

        Submit(form, "save-global", new JsonObject { ["advancedOutputSystemPrompt"] = "Shared formatting instruction" });
        foreach (var item in provider.TopLevelCommands())
        {
            var page = Assert.IsType<FormattedCommandPage>(item.Command);
            page.SearchText = "Content to format";
            Assert.Single(page.GetItems());
        }

        Assert.Equal(2, client.Calls.Count);
        Assert.All(client.Calls, call => Assert.Equal("Shared formatting instruction", call.SystemPrompt));
        Submit(form, "reset-global");
        foreach (var item in provider.TopLevelCommands())
        {
            var page = Assert.IsType<FormattedCommandPage>(item.Command);
            page.SearchText = "Content to format";
            Assert.Single(page.GetItems());
            page.Dispose();
        }

        Assert.All(client.Calls.Skip(2), call => Assert.Equal(GlobalSettings.DefaultAdvancedOutputSystemPrompt, call.SystemPrompt));
        Assert.All(store.GetCommands(), command => Assert.Equal(advancedOutput, command.EnableAdvancedOutput));
    }

    private static UserCommandDefinition Pipeline() => new()
    {
        Id = "pipeline", Name = "Pipeline", Mode = CommandMode.Pipeline,
        OutputFormat = "Unused default {}", SendDelayMilliseconds = 0,
        Steps = [Step("one")],
    };

    [Fact]
    public void Settings_AddingFormattingStepsKeepsEditorsAndDraftsOpenWithUniqueCardIds()
    {
        var command = Pipeline();
        var other = Pipeline();
        other.Id = "other";
        other.Name = "Other";
        var (store, form) = Form(command);
        store.ReplaceAll([command, other]);
        Submit(form, "edit:pipeline");
        Submit(form, "edit:other");
        Submit(form, "edit-provider:default");
        Submit(form, "edit-global");
        Submit(form, "pipeline:pipeline:expand:one");
        Submit(form, "pipeline:other:expand:one");
        Submit(form, "pipeline:pipeline:add", new JsonObject
        {
            ["newStepType_pipeline"] = "AdvancedOutput",
            ["name_pipeline"] = "Unsaved command",
            ["step_pipeline_one_name"] = "Unsaved step",
            ["step_pipeline_one_prompt"] = "Draft input {}",
            ["step_other_one_prompt"] = "Other draft {}",
            ["advancedOutputSystemPrompt"] = "Unsaved shared instructions",
        });
        Submit(form, "pipeline:pipeline:add", new JsonObject { ["newStepType_pipeline"] = "AdvancedOutput" });
        Submit(form, "pipeline:other:add", new JsonObject { ["newStepType_other"] = "AdvancedOutput" });

        var root = JsonNode.Parse(form.TemplateJson)!;
        foreach (var id in new[] { "editor_pipeline", "editor_other", "provider_editor_default", "global_editor",
            "step_editor_pipeline_one", "step_editor_other_one" })
        {
            Assert.True(FindElement(root, id)["isVisible"]!.GetValue<bool>());
        }

        var commandEditor = FindElement(root, "editor_pipeline");
        var nestedCards = Elements(commandEditor).Where(node =>
            node["id"]?.ToString().StartsWith("step_card_pipeline_", StringComparison.Ordinal) == true).ToArray();
        Assert.Equal(3, nestedCards.Length);
        Assert.All(nestedCards, card =>
        {
            Assert.Equal("default", card["style"]!.ToString());
            var editor = Elements(card).Single(node =>
                node["id"]?.ToString().StartsWith("step_editor_", StringComparison.Ordinal) == true);
            Assert.True(editor["isVisible"]!.GetValue<bool>());
        });
        Assert.Equal("Unsaved command", FindElement(root, "name_pipeline")["value"]!.ToString());
        Assert.Equal("Draft input {}", FindElement(root, "step_pipeline_one_prompt")["value"]!.ToString());
        Assert.Equal("Other draft {}", FindElement(root, "step_other_one_prompt")["value"]!.ToString());
        Assert.Equal("Unsaved shared instructions", FindElement(root, "advancedOutputSystemPrompt")["value"]!.ToString());

        // The native renderer requires action IDs to be unique too, not just input IDs.
        var ids = Elements(root).Where(node => node["id"] is not null).Select(node => node["id"]!.ToString()).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(store.GetCommands(), saved => Assert.Single(saved.Steps));
    }

    [Fact]
    public void Settings_StepCardsCollapseIndependentlyAndPreserveDraftsAcrossReorderAndSave()
    {
        var command = Pipeline();
        command.Steps.Add(Step("two"));
        var (store, form) = Form(command);
        Submit(form, "edit:pipeline");
        Submit(form, "pipeline:pipeline:expand:one");
        Submit(form, "pipeline:pipeline:expand:two");
        var initialCard = JsonNode.Parse(form.TemplateJson)!;
        Assert.Contains(Elements(FindElement(initialCard, "step_card_pipeline_one")), node =>
            node["text"]?.ToString().Contains("User step · Input: Input", StringComparison.Ordinal) == true);
        Assert.Contains(Elements(FindElement(initialCard, "step_card_pipeline_two")), node =>
            node["text"]?.ToString().Contains("User step · Input: one", StringComparison.Ordinal) == true);
        Submit(form, "pipeline:pipeline:collapse:one", new JsonObject
        {
            ["step_pipeline_one_prompt"] = "Changed {}",
            ["step_pipeline_one_name"] = "Renamed",
        });
        Submit(form, "pipeline:pipeline:up:two");
        var root = JsonNode.Parse(form.TemplateJson)!;
        Assert.True(FindElement(root, "editor_pipeline")["isVisible"]!.GetValue<bool>());
        Assert.False(FindElement(root, "step_editor_pipeline_one")["isVisible"]!.GetValue<bool>());
        Assert.True(FindElement(root, "step_editor_pipeline_two")["isVisible"]!.GetValue<bool>());
        Assert.Contains("2. Renamed", FindElement(root, "step_card_pipeline_one").ToJsonString(), StringComparison.Ordinal);
        Assert.Contains(Elements(FindElement(root, "step_card_pipeline_one")), node =>
            node["text"]?.ToString().Contains("User step · Input: two", StringComparison.Ordinal) == true);

        Submit(form, "save:pipeline");
        var saved = Assert.Single(store.GetCommands());
        Assert.Equal(["two", "one"], saved.Steps.Select(step => step.Id));
        Assert.Equal("Changed {}", saved.Steps[1].Prompt);
        Assert.Equal("Renamed", saved.Steps[1].Name);
    }

    [Fact]
    public void Settings_EachFormattingCardOpensTheSharedPromptWithoutCollapsingItsCommand()
    {
        var command = Pipeline();
        command.Steps.Add(Step("format", kind: CommandStepKind.AdvancedOutput));
        var (_, form) = Form(command);
        Submit(form, "edit:pipeline");
        var stepCard = FindElement(JsonNode.Parse(form.TemplateJson)!, "step_card_pipeline_format");
        var action = Elements(stepCard).Single(node => node["title"]?.ToString() == "Edit shared system prompt");
        Assert.NotEqual("edit-global", action["id"]!.ToString());
        form.SubmitForm("{}", action["data"]!.ToJsonString());

        var root = JsonNode.Parse(form.TemplateJson)!;
        Assert.True(FindElement(root, "global_editor")["isVisible"]!.GetValue<bool>());
        Assert.True(FindElement(root, "editor_pipeline")["isVisible"]!.GetValue<bool>());
        Assert.True(FindElement(root, "step_editor_pipeline_format")["isVisible"]!.GetValue<bool>());
    }

    [Fact]
    public void Settings_AddStepPickerListsUserAndSystemStepsAndResetsAfterAdding()
    {
        var (_, form) = Form(Pipeline());
        Submit(form, "edit:pipeline");
        var root = JsonNode.Parse(form.TemplateJson)!;
        var picker = FindElement(root, "newStepType_pipeline");
        var choices = picker["choices"]!.AsArray();
        Assert.Collection(
            choices,
            choice =>
            {
                Assert.Equal("User step", choice!["title"]!.ToString());
                Assert.Equal("User", choice["value"]!.ToString());
            },
            choice =>
            {
                Assert.Equal("Advanced output format (system step)", choice!["title"]!.ToString());
                Assert.Equal("AdvancedOutput", choice["value"]!.ToString());
            });
        Assert.Single(Elements(root), node => node["title"]?.ToString() == "Add step");
        Assert.DoesNotContain(Elements(root), node => node["title"]?.ToString() is "Add prompt step" or "Add advanced output format");

        Submit(form, "pipeline:pipeline:add", new JsonObject { ["newStepType_pipeline"] = "AdvancedOutput" });
        root = JsonNode.Parse(form.TemplateJson)!;
        Assert.Equal("User", FindElement(root, "newStepType_pipeline")["value"]!.ToString());
        Assert.Contains(Elements(FindElement(root, "pipeline_steps_pipeline")), node =>
            node["text"]?.ToString().Contains(
                "Advanced output format · System step · Input: one",
                StringComparison.Ordinal) == true);
    }

    private static JsonObject FindElement(JsonNode root, string id) =>
        Elements(root).Single(node => node["id"]?.ToString() == id);

    private static IEnumerable<JsonObject> Elements(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            yield return obj;
            foreach (var child in obj.SelectMany(property => Elements(property.Value)))
            {
                yield return child;
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array.SelectMany(Elements))
            {
                yield return child;
            }
        }
    }

    private static CommandStepDefinition Step(string id, string prompt = "{}", string provider = "default",
        CommandStepKind kind = CommandStepKind.User) => new()
        {
            Id = id, Name = id, Prompt = prompt, ProviderId = provider, Kind = kind,
        };

    private static (UserCommandStore Store, UserCommandsSettingsForm Form) Form(UserCommandDefinition command)
    {
        var store = new UserCommandStore(filePath: null);
        store.ReplaceAll([command]);
        return (store, new UserCommandsSettingsForm(store,
            new LlmProviderSettingsStore(filePath: null), new GlobalSettingsStore(filePath: null),
            () => { }, () => { }, () => { }));
    }

    private static void Submit(UserCommandsSettingsForm form, string action, JsonObject? inputs = null) =>
        form.SubmitForm((inputs ?? []).ToJsonString(), new JsonObject { ["actionId"] = action }.ToJsonString());

    private sealed record Call(string Prompt, string? SystemPrompt, string Template, string Arguments);

    private sealed class RecordingClient(params string[][] responses) : ILlmClient
    {
        public List<Call> Calls { get; } = [];

        public Task<IReadOnlyList<string>> CompleteAsync(string prompt, string? systemPrompt,
            string templateRequestArguments, string customRequestArguments, CancellationToken cancellationToken)
        {
            Calls.Add(new Call(prompt, systemPrompt, templateRequestArguments, customRequestArguments));
            return Task.FromResult<IReadOnlyList<string>>(responses[Calls.Count - 1]);
        }
    }

    private sealed class CallbackClient(Func<CancellationToken, Task<IReadOnlyList<string>>> complete) : ILlmClient
    {
        public Task<IReadOnlyList<string>> CompleteAsync(string prompt, string? systemPrompt,
            string templateRequestArguments, string customRequestArguments, CancellationToken cancellationToken) =>
            complete(cancellationToken);
    }

    private sealed class Monitor : ILlmEndpointMonitor
    {
        public LlmEndpointStatus Status { get; set; }

        public int Checks { get; private set; }

        public event Action? StatusChanged;

        public void Notify() => StatusChanged?.Invoke();

        public Task<bool> CheckAsync(bool force, CancellationToken cancellationToken)
        {
            Checks++;
            return Task.FromResult(Status == LlmEndpointStatus.Available);
        }

        public void Invalidate() => Status = LlmEndpointStatus.Unknown;

        public void ReportReachable() => Status = LlmEndpointStatus.Available;

        public void ReportUnreachable() => Status = LlmEndpointStatus.Unavailable;
    }
}
