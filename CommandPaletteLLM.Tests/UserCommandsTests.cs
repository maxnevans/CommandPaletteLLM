using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace CommandPaletteLLM.Tests;

public sealed class UserCommandsTests
{
    [Fact]
    public void Provider_StartsWithNoCommands()
    {
        var provider = CreateProvider(out _);

        Assert.Empty(provider.TopLevelCommands());
        Assert.Empty(provider.FallbackCommands());
        Assert.False(provider.Frozen);
        Assert.NotNull(provider.Settings);
    }

    [Fact]
    public void Settings_AddsCommandAndReloadsProvider()
    {
        var provider = CreateProvider(out var store);
        var form = GetSettingsForm(provider);
        var itemsChanged = 0;
        provider.ItemsChanged += (_, _) => itemsChanged++;

        var result = form.SubmitForm(
            """{"newName":"Greeting"}""",
            """{"actionId":"add"}""");

        Assert.Equal(CommandResultKind.KeepOpen, result.Kind);
        var definition = Assert.Single(store.GetCommands());
        Assert.Equal("Greeting", definition.Name);
        Assert.Equal("{}", definition.OutputFormat);
        Assert.Equal(650, definition.SendDelayMilliseconds);
        Assert.Empty(definition.CustomRequestArguments);
        Assert.False(definition.EnableAdvancedOutput);
        Assert.Equal(CommandExposure.FallbackCommand, definition.Exposure);
        Assert.True(definition.EnableGlobalFallback);
        Assert.Equal("default", definition.ProviderId);
        Assert.Equal(1, itemsChanged);
        var command = Assert.Single(provider.TopLevelCommands());
        Assert.Equal("Greeting", command.Title);
        Assert.Equal($"CommandPaletteLLM.Command.{definition.Id}", command.Command.Id);
        Assert.Single(provider.FallbackCommands());
    }

    [Fact]
    public void Settings_EditsCommandWithoutChangingItsIdentity()
    {
        var provider = CreateProvider(out var store);
        AddCommand(
            store,
            "Greeting",
            "Hello {}!",
            "stable-id",
            exposure: CommandExposure.GlobalResult);
        provider = new CommandPaletteLLMCommandsProvider(store);
        var originalCommandId = Assert.Single(provider.TopLevelCommands()).Command.Id;
        var form = GetSettingsForm(provider);

        form.SubmitForm(
            """{"name_stable-id":"Welcome","format_stable-id":"Welcome, {}.","requestArguments_stable-id":"{\"temperature\":0.4}","advancedOutput_stable-id":"true","delay_stable-id":125}""",
            """{"actionId":"save:stable-id"}""");

        Assert.Equal(originalCommandId, Assert.Single(provider.TopLevelCommands()).Command.Id);
        Assert.Single(provider.FallbackCommands());
        Assert.Equal("Welcome", Assert.Single(provider.TopLevelCommands()).Title);
        Assert.Equal(125, Assert.Single(store.GetCommands()).SendDelayMilliseconds);
        Assert.Equal(
            """{"temperature":0.4}""",
            Assert.Single(store.GetCommands()).CustomRequestArguments);
        Assert.True(Assert.Single(store.GetCommands()).EnableAdvancedOutput);
    }

    [Fact]
    public void Settings_DeletesCommand()
    {
        var provider = CreateProvider(out var store);
        AddCommand(store, "Greeting", "Hello {}!", "delete-me");
        provider = new CommandPaletteLLMCommandsProvider(store);
        var form = GetSettingsForm(provider);

        form.SubmitForm("{}", """{"actionId":"delete:delete-me"}""");

        Assert.Empty(store.GetCommands());
        Assert.Empty(provider.TopLevelCommands());
        Assert.Empty(provider.FallbackCommands());
    }

    [Fact]
    public void Settings_RejectsDuplicateNames()
    {
        var provider = CreateProvider(out var store);
        AddCommand(store, "Greeting", "Hello {}!", "existing");
        provider = new CommandPaletteLLMCommandsProvider(store);
        var form = GetSettingsForm(provider);

        form.SubmitForm(
            """{"newName":"greeting"}""",
            """{"actionId":"add"}""");

        Assert.Single(store.GetCommands());
        Assert.Contains("must be unique", form.TemplateJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Settings_InitiallyShowsOnlyTheAddAction()
    {
        var provider = CreateProvider(out _);
        var form = GetSettingsForm(provider);

        Assert.Contains("Add command", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("\"id\":\"add\"", form.TemplateJson, StringComparison.Ordinal);
        Assert.DoesNotContain("newFormat", form.TemplateJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Save changes", form.TemplateJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Settings_GlobalPromptStartsCollapsedAndSupportsEditSaveCancelAndReset()
    {
        var commandStore = new UserCommandStore(filePath: null);
        var providerStore = new LlmProviderSettingsStore(filePath: null);
        var globalStore = new GlobalSettingsStore(filePath: null);
        var provider = new CommandPaletteLLMCommandsProvider(
            commandStore,
            providerStore,
            globalStore);
        var form = GetSettingsForm(provider);
        var itemsChanged = 0;
        provider.ItemsChanged += (_, _) => itemsChanged++;

        Assert.Contains("\"text\":\"Global\"", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("\"id\":\"global_display\",\"isVisible\":true", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("\"id\":\"global_editor\",\"isVisible\":false", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("\"text\":\"Default\"", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("\"id\":\"edit-global\"", form.TemplateJson, StringComparison.Ordinal);

        form.SubmitForm("{}", """{"actionId":"edit-global"}""");

        Assert.Contains("\"id\":\"global_editor\",\"isVisible\":true", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("\"id\":\"advancedOutputSystemPrompt\"", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("\"isMultiline\":true", form.TemplateJson, StringComparison.Ordinal);

        form.SubmitForm(
            """{"advancedOutputSystemPrompt":"Custom format instruction"}""",
            """{"actionId":"save-global"}""");

        Assert.Equal("Custom format instruction", globalStore.Get().AdvancedOutputSystemPrompt);
        Assert.Equal(1, itemsChanged);
        Assert.Contains("\"text\":\"Customized\"", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("\"id\":\"global_editor\",\"isVisible\":false", form.TemplateJson, StringComparison.Ordinal);

        form.SubmitForm("{}", """{"actionId":"edit-global"}""");
        form.SubmitForm(
            """{"advancedOutputSystemPrompt":"Unsaved draft"}""",
            """{"actionId":"cancel-global"}""");

        Assert.Equal("Custom format instruction", globalStore.Get().AdvancedOutputSystemPrompt);
        Assert.Equal(1, itemsChanged);
        Assert.DoesNotContain("Unsaved draft", form.TemplateJson, StringComparison.Ordinal);

        form.SubmitForm("{}", """{"actionId":"edit-global"}""");
        form.SubmitForm("{}", """{"actionId":"reset-global"}""");

        Assert.Equal(
            GlobalSettings.DefaultAdvancedOutputSystemPrompt,
            globalStore.Get().AdvancedOutputSystemPrompt);
        Assert.Equal(2, itemsChanged);
        Assert.Contains("\"text\":\"Default\"", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("\"id\":\"global_editor\",\"isVisible\":false", form.TemplateJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Settings_GlobalPromptAllowsEmptyValueAndShowsDisabledStatus()
    {
        var globalStore = new GlobalSettingsStore(filePath: null);
        var provider = new CommandPaletteLLMCommandsProvider(
            new UserCommandStore(filePath: null),
            new LlmProviderSettingsStore(filePath: null),
            globalStore);
        var form = GetSettingsForm(provider);

        form.SubmitForm("{}", """{"actionId":"edit-global"}""");
        form.SubmitForm(
            """{"advancedOutputSystemPrompt":""}""",
            """{"actionId":"save-global"}""");

        Assert.Empty(globalStore.Get().AdvancedOutputSystemPrompt);
        Assert.Contains("\"text\":\"Disabled\"", form.TemplateJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Settings_ExistingCommandHasClearlyScopedActions()
    {
        var provider = CreateProvider(out var store);
        AddCommand(store, "Greeting", "Hello {}!", "stable-id");
        provider = new CommandPaletteLLMCommandsProvider(store);
        var form = GetSettingsForm(provider);

        Assert.Contains("Save changes", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("\"id\":\"save:stable-id\"", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("\"id\":\"delete:stable-id\"", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("\"id\":\"edit:stable-id\"", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("\"id\":\"cancel:stable-id\"", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("\"isVisible\":false", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("Edit command", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("Delete command", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("Cancel editing", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("Send delay (milliseconds)", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("Custom request arguments (optional)", form.TemplateJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Response variations", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("Enable advanced output format", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"RichTextBlock\"", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"Action.ToggleVisibility\"", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("JSON result object or ordered array with optional title, subtitle, details, section, and tags fields", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("\"id\":\"advancedOutput_stable-id_help\",\"isVisible\":false", form.TemplateJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"type\":\"Action.ShowCard\"", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("All five fields are optional", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("Invalid array elements appear as error results", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("Full content copied when selected", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("\"id\":\"format_stable-id\",\"label\":\"Prompt format\"", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("\"id\":\"requestArguments_stable-id\"", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("\"isMultiline\":true", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("\"id\":\"name_stable-id\",\"label\":\"Command name\",\"value\":\"Greeting\",\"placeholder\":\"\",\"isRequired\":true,\"isMultiline\":false", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("Enable as fallback command", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"Input.Toggle\"", form.TemplateJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Command Palette placement", form.TemplateJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Global result", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("LLM provider", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("Custom icon file", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"Input.Number\"", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("\"id\":\"add\"", form.TemplateJson, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not json", "valid JSON")]
    [InlineData("[]", "JSON object")]
    [InlineData("{\"model\":\"other\"}", "protected")]
    [InlineData("{\"MESSAGES\":[]}", "protected")]
    public void Settings_RejectsInvalidCustomRequestArguments(string arguments, string expectedError)
    {
        var provider = CreateProvider(out var store);
        AddCommand(store, "Greeting", "Hello {}!", "stable-id");
        provider = new CommandPaletteLLMCommandsProvider(store);
        var form = GetSettingsForm(provider);
        var payload = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["requestArguments_stable-id"] = arguments,
        });

        form.SubmitForm(payload, """{"actionId":"save:stable-id"}""");

        Assert.Empty(Assert.Single(store.GetCommands()).CustomRequestArguments);
        Assert.Contains(expectedError, form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains(JsonEncodedText.Encode(arguments).ToString(), form.TemplateJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Settings_LegacySubmissionRoutesUsingActionData()
    {
        var provider = CreateProvider(out var store);
        var form = GetSettingsForm(provider);

        var result = form.SubmitForm(
            """{"newName":"Greeting"}""",
            """{"actionId":"add"}""");

        Assert.Equal(CommandResultKind.KeepOpen, result.Kind);
        Assert.Equal("Greeting", Assert.Single(store.GetCommands()).Name);
    }

    [Fact]
    public void Settings_TogglesFallbackWithoutRemovingTheCommand()
    {
        var provider = CreateProvider(out var store);
        AddCommand(store, "Summarize", "Summarize: {}", "summarize");
        provider = new CommandPaletteLLMCommandsProvider(store);
        var form = GetSettingsForm(provider);

        form.SubmitForm(
            """{"fallback_summarize":"false"}""",
            """{"actionId":"save:summarize"}""");

        Assert.Equal(CommandExposure.None, Assert.Single(store.GetCommands()).Exposure);
        Assert.False(Assert.Single(store.GetCommands()).EnableGlobalFallback);
        Assert.Empty(provider.FallbackCommands());
        Assert.Single(provider.TopLevelCommands());

        form.SubmitForm(
            """{"fallback_summarize":"true"}""",
            """{"actionId":"save:summarize"}""");

        Assert.Equal(CommandExposure.FallbackCommand, Assert.Single(store.GetCommands()).Exposure);
        Assert.True(Assert.Single(store.GetCommands()).EnableGlobalFallback);
        Assert.Single(provider.FallbackCommands());
        Assert.Single(provider.TopLevelCommands());

    }

    [Fact]
    public void Settings_SavingOneCommandCollapsesOnlyThatEditorAndPreservesOtherDrafts()
    {
        var provider = CreateProvider(out var store);
        AddCommand(store, "First", "First: {}", "first");
        AddCommand(store, "Second", "Second: {}", "second");
        provider = new CommandPaletteLLMCommandsProvider(store);
        var form = GetSettingsForm(provider);
        form.SubmitForm("{}", """{"actionId":"edit:first"}""");
        form.SubmitForm(
            """{"name_first":"Unsaved first"}""",
            """{"actionId":"edit:second"}""");

        Assert.Contains("\"id\":\"editor_first\",\"isVisible\":true", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("\"id\":\"editor_second\",\"isVisible\":true", form.TemplateJson, StringComparison.Ordinal);

        form.SubmitForm(
            """{"name_first":"Updated first","name_second":"Unsaved second"}""",
            """{"actionId":"save:first"}""");

        Assert.Equal("Updated first", store.GetCommands()[0].Name);
        Assert.Equal("Second", store.GetCommands()[1].Name);
        Assert.Contains("\"id\":\"editor_first\",\"isVisible\":false", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("\"id\":\"editor_second\",\"isVisible\":true", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("\"id\":\"name_second\",\"label\":\"Command name\",\"value\":\"Unsaved second\"", form.TemplateJson, StringComparison.Ordinal);

        form.SubmitForm(
            """{"name_second":"Unsaved second"}""",
            """{"actionId":"cancel:second"}""");

        Assert.Contains("\"id\":\"editor_second\",\"isVisible\":false", form.TemplateJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Unsaved second", form.TemplateJson, StringComparison.Ordinal);
        Assert.Equal("Second", store.GetCommands()[1].Name);
    }

    [Fact]
    public void Settings_SavingOneProviderCollapsesOnlyThatEditorAndPreservesOtherDrafts()
    {
        var commandStore = new UserCommandStore(filePath: null);
        var providerStore = new LlmProviderSettingsStore(filePath: null);
        providerStore.ReplaceAll(
        [
            new LlmProviderSettings { Id = "default", Name = "Local" },
            new LlmProviderSettings { Id = "remote", Name = "Remote" },
        ]);
        var provider = new CommandPaletteLLMCommandsProvider(commandStore, providerStore);
        var form = GetSettingsForm(provider);

        form.SubmitForm("{}", """{"actionId":"edit-provider:default"}""");
        form.SubmitForm(
            """{"providerName_default":"Local draft"}""",
            """{"actionId":"edit-provider:remote"}""");
        form.SubmitForm(
            """{"providerName_default":"Saved local","providerName_remote":"Remote draft"}""",
            """{"actionId":"save-provider:default"}""");

        Assert.Equal("Saved local", providerStore.Get("default")!.Name);
        Assert.Equal("Remote", providerStore.Get("remote")!.Name);
        Assert.Contains("\"id\":\"provider_editor_default\",\"isVisible\":false", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("\"id\":\"provider_editor_remote\",\"isVisible\":true", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("\"id\":\"providerName_remote\",\"label\":\"Provider name\",\"value\":\"Remote draft\"", form.TemplateJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Settings_AddsProvidersAndAssignsOneToACommand()
    {
        var store = new UserCommandStore(filePath: null);
        var providerStore = new LlmProviderSettingsStore(filePath: null);
        AddCommand(store, "Summarize", "Summarize: {}", "summarize");
        var provider = new CommandPaletteLLMCommandsProvider(store, providerStore);
        var form = GetSettingsForm(provider);

        form.SubmitForm(
            """{"newProviderName":"Remote LLM"}""",
            """{"actionId":"add-provider"}""");
        var remoteProvider = Assert.Single(
            providerStore.GetProviders(),
            item => item.Name == "Remote LLM");

        form.SubmitForm(
            $$"""{"provider_summarize":"{{remoteProvider.Id}}"}""",
            """{"actionId":"save:summarize"}""");

        Assert.Equal(remoteProvider.Id, Assert.Single(store.GetCommands()).ProviderId);
        Assert.Contains("Input.ChoiceSet", form.TemplateJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Settings_ClearsProviderApiKeyWithAButton()
    {
        var commandStore = new UserCommandStore(filePath: null);
        var providerStore = new LlmProviderSettingsStore(filePath: null);
        providerStore.Replace(new LlmProviderSettings { ApiKey = "secret" });
        var provider = new CommandPaletteLLMCommandsProvider(commandStore, providerStore);
        var form = GetSettingsForm(provider);

        Assert.Contains("clear-provider-key:default", form.TemplateJson, StringComparison.Ordinal);
        Assert.DoesNotContain("clearProviderApiKey", form.TemplateJson, StringComparison.Ordinal);

        form.SubmitForm("{}", """{"actionId":"clear-provider-key:default"}""");

        Assert.Empty(providerStore.Get().ApiKey);
    }

    [Fact]
    public void Settings_ImportsCommandIconIntoManagedStorage()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"CommandPaletteLLM.Tests-{Guid.NewGuid():N}");
        var sourceIcon = Path.Combine(directory, "source.png");
        var commandFile = Path.Combine(directory, "commands.json");

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(sourceIcon, [1, 2, 3, 4]);
            var store = new UserCommandStore(commandFile);
            AddCommand(store, "Summarize", "Summarize: {}", "summarize");
            var provider = new CommandPaletteLLMCommandsProvider(store);
            var form = GetSettingsForm(provider);
            var escapedPath = sourceIcon.Replace("\\", "\\\\", StringComparison.Ordinal);

            form.SubmitForm(
                $$"""{"icon_summarize":"{{escapedPath}}"}""",
                """{"actionId":"save:summarize"}""");

            var storedPath = Assert.Single(store.GetCommands()).IconPath;
            Assert.NotEqual(sourceIcon, storedPath);
            Assert.StartsWith(Path.Combine(directory, "Icons"), storedPath, StringComparison.OrdinalIgnoreCase);
            Assert.Equal([1, 2, 3, 4], File.ReadAllBytes(storedPath));
            File.Delete(sourceIcon);
            Assert.True(File.Exists(storedPath));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void Settings_SavesProviderWithoutRedisplayingApiKey()
    {
        var commandStore = new UserCommandStore(filePath: null);
        var providerStore = new LlmProviderSettingsStore(filePath: null);
        var provider = new CommandPaletteLLMCommandsProvider(commandStore, providerStore);
        var form = GetSettingsForm(provider);

        var result = form.SubmitForm(
            """{"providerBaseUrl":"http://localhost:8080/v1/","providerModel":"test-model","providerApiKey":"secret","clearProviderApiKey":"false"}""",
            """{"actionId":"save-provider"}""");

        Assert.Equal(CommandResultKind.KeepOpen, result.Kind);
        var settings = providerStore.Get();
        Assert.Equal("http://localhost:8080/v1/", settings.BaseUrl);
        Assert.Equal("test-model", settings.Model);
        Assert.Equal("secret", settings.ApiKey);
        Assert.DoesNotContain("secret", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("Saved; leave blank to keep it", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("Prompts are sent directly to this provider", form.TemplateJson, StringComparison.Ordinal);
        Assert.DoesNotContain("providerRemoteConsent", form.TemplateJson, StringComparison.Ordinal);
        Assert.DoesNotContain("[Privacy policy]", form.TemplateJson, StringComparison.Ordinal);
    }

    [Fact]
    public void FormattedPage_ReturnsOneFormattedResult()
    {
        var client = new RecordingLlmClient("This is the answer.");
        var page = new FormattedCommandPage(new UserCommandDefinition
        {
            Id = "greeting",
            Name = "Greeting",
            OutputFormat = "Hello {}!",
            CustomRequestArguments = """{"n":2}""",
        }, client, TimeSpan.Zero);

        page.SearchText = "Maxim";

        var item = Assert.Single(page.GetItems());
        Assert.Equal("This is the answer.", item.Title);
        Assert.Equal("This is the answer.", Assert.IsType<CopyTextCommand>(item.Command).Text);
        Assert.Equal("This is the answer.", item.TextToSuggest);
        Assert.False(page.ShowDetails);
        Assert.Null(item.Details);
        Assert.Equal("Hello Maxim!", Assert.Single(client.Prompts));
        Assert.Equal("""{"n":2}""", Assert.Single(client.RequestArguments));
        Assert.False(page.IsLoading);
    }

    [Fact]
    public void Provider_UsesOneGlobalSystemPromptOnlyForAdvancedCommands()
    {
        var commandStore = new UserCommandStore(filePath: null);
        AddCommand(
            commandStore,
            "Advanced",
            "Advanced: {}",
            "advanced",
            sendDelayMilliseconds: 0,
            enableAdvancedOutput: true);
        AddCommand(
            commandStore,
            "Plain",
            "Plain: {}",
            "plain",
            sendDelayMilliseconds: 0,
            exposure: CommandExposure.None);
        var globalStore = new GlobalSettingsStore(filePath: null);
        globalStore.Replace(new GlobalSettings
        {
            AdvancedOutputSystemPrompt = "Shared global instruction",
        });
        var client = new RecordingLlmClient("{\"title\":\"Answer\"}");
        var provider = new CommandPaletteLLMCommandsProvider(
            commandStore,
            new LlmProviderSettingsStore(filePath: null),
            globalStore,
            client);

        var advancedPage = Assert.IsType<FormattedCommandPage>(
            Assert.Single(provider.TopLevelCommands(), item => item.Title == "Advanced").Command);
        advancedPage.SearchText = "page query";
        var fallback = Assert.IsType<FormattedFallbackItem>(Assert.Single(provider.FallbackCommands()));
        fallback.FallbackHandler!.UpdateQuery("fallback query");
        var plainPage = Assert.IsType<FormattedCommandPage>(
            Assert.Single(provider.TopLevelCommands(), item => item.Title == "Plain").Command);
        plainPage.SearchText = "plain query";

        Assert.Equal(
            ["Shared global instruction", "Shared global instruction", null],
            client.SystemPrompts);
    }

    [Fact]
    public void AdvancedCommand_OmitsEmptyGlobalSystemPrompt()
    {
        var client = new RecordingLlmClient("{\"title\":\"Answer\"}");
        var page = new FormattedCommandPage(
            new UserCommandDefinition
            {
                Id = "advanced",
                Name = "Advanced",
                OutputFormat = "{}",
                EnableAdvancedOutput = true,
            },
            client,
            TimeSpan.Zero,
            advancedOutputSystemPrompt: string.Empty);

        page.SearchText = "query";

        Assert.Null(Assert.Single(client.SystemPrompts));
    }

    [Fact]
    public void FormattedPage_ShowsMultilineResponseInOneItemAndFullDetails()
    {
        const string response = "First line\nSecond line\nThird line";
        var page = new FormattedCommandPage(new UserCommandDefinition
        {
            Id = "summarize",
            Name = "Summarize",
            OutputFormat = "Summarize: {}",
        }, new RecordingLlmClient(response), TimeSpan.Zero);

        page.SearchText = "Text";

        var item = Assert.Single(page.GetItems());
        Assert.Equal("First line", item.Title);
        Assert.Equal("Second line Third line", item.Subtitle);
        Assert.Equal(response, item.Details?.Body);
        Assert.Equal(ContentSize.Large, Assert.IsType<Details>(item.Details).Size);
        Assert.True(page.ShowDetails);
    }

    [Fact]
    public void FormattedPage_MapsAdvancedOutputWithoutDefaultHeuristics()
    {
        const string response = """
            {
              "title": "A deliberately long title that remains the title even though it is longer than the compact-result threshold used by default formatting rules.",
              "subtitle": "Explicit subtitle",
              "details": "Detailed\nbody",
              "section": "Writing",
              "tags": ["concise", "reviewed"]
            }
            """;
        var page = new FormattedCommandPage(new UserCommandDefinition
        {
            Id = "structured",
            Name = "Structured",
            OutputFormat = "Respond to: {}",
            EnableAdvancedOutput = true,
        }, new RecordingLlmClient(response), TimeSpan.Zero);

        page.SearchText = "Text";

        var item = Assert.Single(page.GetItems());
        Assert.StartsWith("A deliberately long title", item.Title, StringComparison.Ordinal);
        Assert.Equal("Explicit subtitle", item.Subtitle);
        Assert.Equal("Writing", item.Section);
        Assert.Equal(["concise", "reviewed"], item.Tags.Select(tag => tag.Text));
        Assert.Equal("Detailed\nbody", item.Details?.Body);
        Assert.Equal("Detailed\nbody", Assert.IsType<CopyTextCommand>(item.Command).Text);
        Assert.Equal("Detailed\nbody", item.TextToSuggest);
        Assert.True(page.ShowDetails);
    }

    [Fact]
    public void FormattedPage_AcceptsFencedPartialAdvancedOutputAndCopiesTitle()
    {
        const string response = """
            ```json
            {"title":"Result","extra":42}
            ```
            """;
        var page = new FormattedCommandPage(new UserCommandDefinition
        {
            Id = "structured",
            Name = "Structured",
            OutputFormat = "{}",
            EnableAdvancedOutput = true,
        }, new RecordingLlmClient(response), TimeSpan.Zero);

        page.SearchText = "Text";

        var item = Assert.Single(page.GetItems());
        Assert.Equal("Result", item.Title);
        Assert.Empty(item.Subtitle);
        Assert.Empty(item.Section);
        Assert.Empty(item.Tags);
        Assert.Null(item.Details);
        Assert.Equal("Result", Assert.IsType<CopyTextCommand>(item.Command).Text);
        Assert.Equal("Result", item.TextToSuggest);
        Assert.False(page.ShowDetails);
    }

    [Fact]
    public void FormattedPage_PreservesAdvancedOutputArrayOrderAndShowsInvalidElements()
    {
        const string response = """
            [
              {"title":"First"},
              42,
              {"title":1},
              {"tags":"tag"},
              {"tags":["valid",1]},
              {"title":"Last","details":"Last details"}
            ]
            """;
        var page = new FormattedCommandPage(new UserCommandDefinition
        {
            Id = "structured",
            Name = "Structured",
            OutputFormat = "{}",
            EnableAdvancedOutput = true,
        }, new RecordingLlmClient(response), TimeSpan.Zero);

        page.SearchText = "Text";

        Assert.Collection(
            page.GetItems(),
            item => Assert.Equal("First", item.Title),
            item => AssertAdvancedOutputError(
                item,
                "Invalid variation: expected an object",
                "42"),
            item => AssertAdvancedOutputError(
                item,
                "Invalid variation: \"title\" must be a string",
                "{\"title\":1}"),
            item => AssertAdvancedOutputError(
                item,
                "Invalid variation: \"tags\" must be an array",
                "{\"tags\":\"tag\"}"),
            item => AssertAdvancedOutputError(
                item,
                "Invalid variation: \"tags\" must contain only strings",
                "{\"tags\":[\"valid\",1]}"),
            item =>
            {
                Assert.Equal("Last", item.Title);
                Assert.Equal("Last details", item.Details?.Body);
            });
        Assert.True(page.ShowDetails);
    }

    [Fact]
    public void FormattedPage_AcceptsFencedEmptyAdvancedOutputArray()
    {
        const string response = """
            ```json
            []
            ```
            """;
        var page = new FormattedCommandPage(new UserCommandDefinition
        {
            Id = "structured",
            Name = "Structured",
            OutputFormat = "{}",
            EnableAdvancedOutput = true,
        }, new RecordingLlmClient(response), TimeSpan.Zero);

        page.SearchText = "Text";

        Assert.Empty(page.GetItems());
        Assert.False(page.ShowDetails);
    }

    [Fact]
    public void FormattedPage_FlattensAdvancedOutputArraysAcrossProviderChoices()
    {
        var client = new SequenceLlmClient(
            """[{"title":"First"},{"title":"Second"}]""",
            """[{"title":"Third"}]""");
        var page = new FormattedCommandPage(new UserCommandDefinition
        {
            Id = "structured",
            Name = "Structured",
            OutputFormat = "{}",
            EnableAdvancedOutput = true,
            CustomRequestArguments = """{"n":2}""",
        }, client, TimeSpan.Zero);

        page.SearchText = "Text";

        Assert.Equal(
            ["First", "Second", "Third"],
            page.GetItems().Select(item => item.Title));
        Assert.Equal("""{"n":2}""", client.RequestedCustomRequestArguments);
    }

    [Fact]
    public void FormattedPage_AdvancedOutputDisabledTreatsArrayAsPlainText()
    {
        const string response = """[{"title":"First"},{"title":"Second"}]""";
        var page = new FormattedCommandPage(new UserCommandDefinition
        {
            Id = "plain",
            Name = "Plain",
            OutputFormat = "{}",
        }, new RecordingLlmClient(response), TimeSpan.Zero);

        page.SearchText = "Text";

        var item = Assert.Single(page.GetItems());
        Assert.Equal(response, item.Title);
        Assert.Equal(response, Assert.IsType<CopyTextCommand>(item.Command).Text);
    }

    [Fact]
    public void FormattedPage_ParsesEveryAdvancedOutputVariationIndependently()
    {
        var client = new SequenceLlmClient(
            """{"title":"First"}""",
            "```\n{\"title\":\"Second\",\"subtitle\":\"Alternative\"}\n```");
        var page = new FormattedCommandPage(new UserCommandDefinition
        {
            Id = "structured",
            Name = "Structured",
            OutputFormat = "{}",
            EnableAdvancedOutput = true,
            CustomRequestArguments = """{"n":2}""",
        }, client, TimeSpan.Zero);

        page.SearchText = "Text";

        Assert.Collection(
            page.GetItems(),
            item => Assert.Equal("First", item.Title),
            item =>
            {
                Assert.Equal("Second", item.Title);
                Assert.Equal("Alternative", item.Subtitle);
            });
        Assert.Equal("""{"n":2}""", client.RequestedCustomRequestArguments);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("42")]
    [InlineData("{\"title\":1}")]
    [InlineData("{\"subtitle\":null}")]
    [InlineData("{\"tags\":\"tag\"}")]
    [InlineData("{\"tags\":[\"valid\",1]}")]
    [InlineData("```javascript\n{\"title\":\"Result\"}\n```")]
    public void AdvancedOutputParser_RejectsInvalidFormattedOutput(string response)
    {
        Assert.False(AdvancedOutputParser.TryParse(response, out _));
    }

    [Fact]
    public void FormattedPage_InvalidAdvancedOutputUsesDefaultFormatting()
    {
        const string response = "{\"title\":1}\nSecond line";
        var page = new FormattedCommandPage(new UserCommandDefinition
        {
            Id = "structured",
            Name = "Structured",
            OutputFormat = "{}",
            EnableAdvancedOutput = true,
        }, new RecordingLlmClient(response), TimeSpan.Zero);

        page.SearchText = "Text";

        var item = Assert.Single(page.GetItems());
        Assert.Equal("{\"title\":1}", item.Title);
        Assert.Equal("Second line", item.Subtitle);
        Assert.Equal(response, item.Details?.Body);
        Assert.Equal(response, Assert.IsType<CopyTextCommand>(item.Command).Text);
    }

    [Fact]
    public void FormattedFallback_ReturnsOneFormattedGlobalResult()
    {
        var client = new RecordingLlmClient("This is the answer.");
        var fallback = new FormattedFallbackItem(new UserCommandDefinition
        {
            Id = "greeting",
            Name = "Greeting",
            OutputFormat = "Hello {}!",
        }, client, TimeSpan.Zero);

        fallback.FallbackHandler!.UpdateQuery("Maxim");

        Assert.Equal("This is the answer.", fallback.Title);
        Assert.Equal(
            "This is the answer.",
            Assert.IsType<StableCopyTextCommand>(fallback.Command).Text);
        Assert.Equal("Hello Maxim!", Assert.Single(client.Prompts));
    }

    [Fact]
    public void FormattedFallback_MapsAdvancedTitleSubtitleAndCopyText()
    {
        const string response = """{"title":"Result","subtitle":"Context","details":"Copy this","section":"Ignored","tags":["ignored"]}""";
        var fallback = new FormattedFallbackItem(new UserCommandDefinition
        {
            Id = "structured",
            Name = "Structured",
            OutputFormat = "{}",
            EnableAdvancedOutput = true,
        }, new RecordingLlmClient(response), TimeSpan.Zero);

        fallback.FallbackHandler!.UpdateQuery("Text");

        Assert.Equal("Result", fallback.Title);
        Assert.Equal("Context", fallback.Subtitle);
        Assert.Equal(
            "Copy this",
            Assert.IsType<StableCopyTextCommand>(fallback.Command).Text);
    }

    [Fact]
    public void FormattedFallback_UsesFirstValidAdvancedOutputArrayElement()
    {
        const string response = """[false,{"title":1},{"title":"Result"},{"title":"Ignored"}]""";
        var fallback = new FormattedFallbackItem(new UserCommandDefinition
        {
            Id = "structured",
            Name = "Structured",
            OutputFormat = "{}",
            EnableAdvancedOutput = true,
        }, new RecordingLlmClient(response), TimeSpan.Zero);

        fallback.FallbackHandler!.UpdateQuery("Text");

        Assert.Equal("Result", fallback.Title);
        Assert.Equal(
            "Result",
            Assert.IsType<StableCopyTextCommand>(fallback.Command).Text);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[false,{\"title\":1}]")]
    public void FormattedFallback_HidesAdvancedOutputArrayWithoutValidObjects(string response)
    {
        var fallback = new FormattedFallbackItem(new UserCommandDefinition
        {
            Id = "structured",
            Name = "Structured",
            OutputFormat = "{}",
            EnableAdvancedOutput = true,
        }, new RecordingLlmClient(response), TimeSpan.Zero);

        fallback.FallbackHandler!.UpdateQuery("Text");

        Assert.Empty(fallback.Title);
        Assert.Empty(fallback.Subtitle);
        Assert.Empty(Assert.IsType<StableCopyTextCommand>(fallback.Command).Text);
    }

    [Fact]
    public void FormattedFallback_InvalidAdvancedOutputUsesDefaultBehavior()
    {
        const string response = "{\"title\":false}";
        var fallback = new FormattedFallbackItem(new UserCommandDefinition
        {
            Id = "structured",
            Name = "Structured",
            OutputFormat = "{}",
            EnableAdvancedOutput = true,
        }, new RecordingLlmClient(response), TimeSpan.Zero);

        fallback.FallbackHandler!.UpdateQuery("Text");

        Assert.Equal(response, fallback.Title);
        Assert.Empty(fallback.Subtitle);
        Assert.Equal(response, Assert.IsType<StableCopyTextCommand>(fallback.Command).Text);
    }

    [Fact]
    public void FormattedFallback_KeepsStableIdentityAcrossEndpointChanges()
    {
        var monitor = new ControllableEndpointMonitor(LlmEndpointStatus.Available);
        var fallback = new FormattedFallbackItem(
            new UserCommandDefinition
            {
                Id = "summarize",
                Name = "Summarize",
                OutputFormat = "Summarize: {}",
            },
            new RecordingLlmClient("Summary"),
            TimeSpan.Zero,
            endpointMonitor: monitor);
        var command = Assert.IsType<StableCopyTextCommand>(fallback.Command);
        var fallbackId = fallback.Id;
        var commandId = command.Id;

        fallback.FallbackHandler!.UpdateQuery("Text");
        monitor.SetStatus(LlmEndpointStatus.Unavailable);
        fallback.EndpointStatusChanged(allowGlobalResults: true);
        monitor.SetStatus(LlmEndpointStatus.Available);
        fallback.EndpointStatusChanged(allowGlobalResults: true);

        var currentCommand = Assert.IsType<StableCopyTextCommand>(fallback.Command);
        Assert.Same(command, currentCommand);
        Assert.Equal(fallbackId, fallback.Id);
        Assert.Equal(commandId, currentCommand.Id);
        Assert.Equal($"{fallback.Id}.Copy", currentCommand.Id);
    }

    [Fact]
    public async Task FormattedPage_DoesNotPublishAStaleResponse()
    {
        var client = new ControlledLlmClient();
        var page = new FormattedCommandPage(new UserCommandDefinition
        {
            Id = "greeting",
            Name = "Greeting",
            OutputFormat = "Prompt: {}",
        }, client, TimeSpan.Zero);

        page.SearchText = "old";
        page.SearchText = "new";

        Assert.Equal(2, client.Requests.Count);
        client.Requests[1].Completion.SetResult("new response");
        await WaitUntilAsync(() =>
            page.GetItems() is [{ Title: "new response" }]);
        Assert.Equal("new response", Assert.Single(page.GetItems()).Title);

        client.Requests[0].Completion.SetResult("stale response");
        await Task.Yield();
        Assert.Equal("new response", Assert.Single(page.GetItems()).Title);
    }

    [Fact]
    public async Task FormattedPage_DoesNotSendClearedQueryAfterConnectionCheckCompletes()
    {
        var client = new ControlledLlmClient();
        var monitor = new DelayedEndpointMonitor();
        var page = new FormattedCommandPage(new UserCommandDefinition
        {
            Id = "greeting",
            Name = "Greeting",
            OutputFormat = "Prompt: {}",
        }, client, TimeSpan.Zero, endpointMonitor: monitor);

        page.GetItems();
        page.SearchText = "x";
        await WaitUntilAsync(() => monitor.PendingCheckCount == 1);

        page.SearchText = string.Empty;
        monitor.CompleteCheck();
        await Task.Yield();

        Assert.Empty(client.Requests);
        Assert.Empty(page.GetItems());
        Assert.False(page.IsLoading);
    }

    [Fact]
    public async Task FormattedPage_ShowsConnectionAndRequestProgressThenRecovers()
    {
        var client = new ControlledLlmClient();
        var monitor = new ControllableEndpointMonitor(LlmEndpointStatus.Unavailable);
        var page = new FormattedCommandPage(
            new UserCommandDefinition
            {
                Id = "translate",
                Name = "Translate",
                OutputFormat = "Translate: {}",
                SendDelayMilliseconds = 100,
            },
            client,
            endpointMonitor: monitor,
            retryDelay: TimeSpan.FromMilliseconds(500),
            statusUpdateInterval: TimeSpan.FromMilliseconds(10));

        Assert.Contains("LLM connection unavailable", Assert.Single(page.GetItems()).Title);
        monitor.SetStatus(LlmEndpointStatus.Available);
        Assert.Empty(page.GetItems());
        monitor.SetStatus(LlmEndpointStatus.Unavailable);
        Assert.Contains("LLM connection unavailable", Assert.Single(page.GetItems()).Title);

        page.SearchText = "hello";
        Assert.Empty(client.Requests);

        monitor.SetStatus(LlmEndpointStatus.Available);
        Assert.Contains("Sending to LLM in", Assert.Single(page.GetItems()).Title);
        await WaitUntilAsync(() => client.Requests.Count == 1);
        Assert.Equal("Waiting for LLM response…", Assert.Single(page.GetItems()).Title);

        client.Requests[0].Completion.SetResult("hola");
        await WaitUntilAsync(() => Assert.Single(page.GetItems()).Title == "hola");
        Assert.Equal("hola", Assert.Single(page.GetItems()).Title);
    }

    [Fact]
    public void FormattedPage_ForwardsCustomRequestArguments()
    {
        var client = new RecordingLlmClient("translation");
        var page = new FormattedCommandPage(new UserCommandDefinition
        {
            Id = "translate",
            Name = "Translate",
            OutputFormat = "Translate: {}",
            SendDelayMilliseconds = 0,
            CustomRequestArguments = """{"temperature":0.2}""",
        }, client, TimeSpan.Zero);

        page.SearchText = "hello";

        Assert.Single(page.GetItems());
        Assert.Equal("""{"temperature":0.2}""", Assert.Single(client.RequestArguments));
    }

    [Fact]
    public void Provider_CancelsRequestsAcrossRootAndCommandPageNavigation()
    {
        var store = new UserCommandStore(filePath: null);
        AddCommand(
            store,
            "Summarize",
            "Summarize: {}",
            "summarize",
            sendDelayMilliseconds: 0,
            exposure: CommandExposure.GlobalResult);
        AddCommand(store, "Ask", "Ask: {}", "ask", sendDelayMilliseconds: 0);
        var client = new ControlledLlmClient();
        var provider = new CommandPaletteLLMCommandsProvider(
            store,
            new LlmProviderSettingsStore(filePath: null),
            client);
        var page = Assert.IsType<FormattedCommandPage>(
            Assert.Single(provider.TopLevelCommands(), item => item.Title == "Summarize").Command);
        var fallback = Assert.IsType<FormattedFallbackItem>(
            Assert.Single(provider.FallbackCommands(), item => item.DisplayTitle == "Ask"));

        fallback.FallbackHandler!.UpdateQuery("text to summarize");
        var fallbackRequest = Assert.Single(client.Requests);
        page.GetItems();
        Assert.True(fallbackRequest.CancellationToken.IsCancellationRequested);

        page.SearchText = "page text";
        var pageRequest = client.Requests[1];
        fallback.FallbackHandler.UpdateQuery("Summarize");
        Assert.True(pageRequest.CancellationToken.IsCancellationRequested);
        Assert.Empty(page.GetItems());
        Assert.False(page.IsLoading);
        Assert.Empty(fallback.Title);
        Assert.Equal(2, client.Requests.Count);
    }

    [Fact]
    public void Fallback_DoesNotSendWhenQueryMatchesACommandName()
    {
        var store = new UserCommandStore(filePath: null);
        AddCommand(
            store,
            "Summarize",
            "Summarize: {}",
            "summarize",
            sendDelayMilliseconds: 0,
            exposure: CommandExposure.GlobalResult);
        AddCommand(store, "Ask", "Ask: {}", "ask", sendDelayMilliseconds: 0);
        var client = new RecordingLlmClient("unused");
        var provider = new CommandPaletteLLMCommandsProvider(
            store,
            new LlmProviderSettingsStore(filePath: null),
            client);
        var fallback = Assert.IsType<FormattedFallbackItem>(
            Assert.Single(provider.FallbackCommands(), item => item.DisplayTitle == "Ask"));

        fallback.FallbackHandler!.UpdateQuery("  summarize  ");

        Assert.Empty(client.Prompts);
        Assert.Empty(fallback.Title);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void FormattedCommands_HideEmptyQueries(string query)
    {
        var definition = new UserCommandDefinition
        {
            Id = "greeting",
            Name = "Greeting",
            OutputFormat = "Hello {}!",
        };
        var client = new RecordingLlmClient("unused");
        var page = new FormattedCommandPage(definition, client, TimeSpan.Zero);
        var fallback = new FormattedFallbackItem(definition, client, TimeSpan.Zero);

        page.SearchText = query;
        fallback.FallbackHandler!.UpdateQuery(query);

        Assert.Empty(page.GetItems());
        Assert.Empty(fallback.Title);
        Assert.Empty(client.Prompts);
    }

    [Fact]
    public async Task OpenAiClient_SendsChatCompletionRequestAndReadsResponse()
    {
        var store = new LlmProviderSettingsStore(filePath: null);
        store.Replace(new LlmProviderSettings
        {
            BaseUrl = "http://127.0.0.1:8080/v1/",
            Model = "local-model",
            ApiKey = "test-key",
        });
        var handler = new RecordingHttpMessageHandler(
            """{"choices":[{"message":{"role":"assistant","content":"  First answer.  "}},{"message":{"role":"assistant","content":"Second answer."}}]}""");
        var client = new OpenAiCompatibleLlmClient(store, new HttpClient(handler));

        var responses = await client.CompleteAsync(
            "Explain this",
            systemPrompt: null,
            string.Empty,
            CancellationToken.None);

        Assert.Equal(["First answer.", "Second answer."], responses);
        Assert.Equal("http://127.0.0.1:8080/v1/chat/completions", handler.RequestUri?.ToString());
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("test-key", handler.AuthorizationParameter);
        using var requestJson = JsonDocument.Parse(Assert.IsType<string>(handler.RequestBody));
        Assert.Equal("local-model", requestJson.RootElement.GetProperty("model").GetString());
        Assert.False(requestJson.RootElement.TryGetProperty("n", out _));
        Assert.Equal(
            "Explain this",
            requestJson.RootElement.GetProperty("messages")[0].GetProperty("content").GetString());
    }

    [Fact]
    public async Task OpenAiClient_PrependsSystemMessageBeforeUserMessage()
    {
        var store = new LlmProviderSettingsStore(filePath: null);
        store.Replace(new LlmProviderSettings
        {
            BaseUrl = "http://127.0.0.1:8080/v1",
            Model = "local-model",
        });
        var handler = new RecordingHttpMessageHandler(
            """{"choices":[{"message":{"content":"answer"}}]}""");
        var client = new OpenAiCompatibleLlmClient(store, new HttpClient(handler));

        await client.CompleteAsync(
            "User prompt",
            "System instruction",
            """{"temperature":0.2}""",
            CancellationToken.None);

        using var requestJson = JsonDocument.Parse(Assert.IsType<string>(handler.RequestBody));
        var messages = requestJson.RootElement.GetProperty("messages");
        Assert.Equal(2, messages.GetArrayLength());
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("System instruction", messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal("User prompt", messages[1].GetProperty("content").GetString());
        Assert.Equal(0.2, requestJson.RootElement.GetProperty("temperature").GetDouble());
    }

    [Fact]
    public async Task OpenAiClient_AddsCustomRequestArgumentsWithoutChangingCoreFields()
    {
        var store = new LlmProviderSettingsStore(filePath: null);
        store.Replace(new LlmProviderSettings
        {
            BaseUrl = "http://127.0.0.1:8080/v1",
            Model = "local-model",
        });
        var handler = new RecordingHttpMessageHandler(
            """{"choices":[{"message":{"content":"answer"}}]}""");
        var client = new OpenAiCompatibleLlmClient(store, new HttpClient(handler));
        const string arguments = """
            {
              "n": 2,
              "temperature": null,
              "stop": ["END"],
              "chat_template_kwargs": {
                "enable_thinking": true,
                "reasoning_effort": "medium",
                "preserve_thinking": false
              }
            }
            """;

        await client.CompleteAsync("Prompt", systemPrompt: null, arguments, CancellationToken.None);

        using var requestJson = JsonDocument.Parse(Assert.IsType<string>(handler.RequestBody));
        var root = requestJson.RootElement;
        Assert.Equal("local-model", root.GetProperty("model").GetString());
        Assert.Equal("Prompt", root.GetProperty("messages")[0].GetProperty("content").GetString());
        Assert.Equal(2, root.GetProperty("n").GetInt32());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("temperature").ValueKind);
        Assert.Equal("END", root.GetProperty("stop")[0].GetString());
        Assert.True(root.GetProperty("chat_template_kwargs").GetProperty("enable_thinking").GetBoolean());
        Assert.Equal(
            "medium",
            root.GetProperty("chat_template_kwargs").GetProperty("reasoning_effort").GetString());
        Assert.False(root.GetProperty("chat_template_kwargs").GetProperty("preserve_thinking").GetBoolean());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"model\":\"other\"}")]
    [InlineData("{\"Model\":\"other\"}")]
    [InlineData("{\"messages\":[]}")]
    [InlineData("{\"MESSAGES\":[]}")]
    public async Task OpenAiClient_RejectsInvalidCustomRequestArguments(string arguments)
    {
        var store = new LlmProviderSettingsStore(filePath: null);
        store.Replace(new LlmProviderSettings
        {
            BaseUrl = "http://127.0.0.1:8080/v1",
            Model = "local-model",
        });
        var handler = new RecordingHttpMessageHandler("{}");
        var client = new OpenAiCompatibleLlmClient(store, new HttpClient(handler));

        var exception = await Assert.ThrowsAsync<LlmRequestException>(() =>
            client.CompleteAsync("Prompt", systemPrompt: null, arguments, CancellationToken.None));

        Assert.Contains("Custom request arguments", exception.Message, StringComparison.Ordinal);
        Assert.Null(handler.RequestBody);
    }

    [Fact]
    public async Task OpenAiClient_UsesTheSelectedProvider()
    {
        var store = new LlmProviderSettingsStore(filePath: null);
        store.ReplaceAll(
        [
            new LlmProviderSettings
            {
                Id = "first",
                Name = "First",
                BaseUrl = "http://127.0.0.1:8080/v1",
                Model = "first-model",
            },
            new LlmProviderSettings
            {
                Id = "second",
                Name = "Second",
                BaseUrl = "https://second.example/v1",
                Model = "second-model",
            },
        ]);
        var handler = new RecordingHttpMessageHandler(
            """{"choices":[{"message":{"role":"assistant","content":"answer"}}]}""");
        var client = new OpenAiCompatibleLlmClient(
            store,
            "second",
            new HttpClient(handler));

        await client.CompleteAsync(
            "Prompt",
            systemPrompt: null,
            string.Empty,
            CancellationToken.None);

        Assert.Equal("https://second.example/v1/chat/completions", handler.RequestUri?.ToString());
        using var requestJson = JsonDocument.Parse(Assert.IsType<string>(handler.RequestBody));
        Assert.Equal("second-model", requestJson.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public async Task EndpointMonitor_ProbesModelsEndpointWithAuthentication()
    {
        var store = new LlmProviderSettingsStore(filePath: null);
        store.Replace(new LlmProviderSettings
        {
            BaseUrl = "http://127.0.0.1:8080/v1/chat/completions",
            Model = "local-model",
            ApiKey = "test-key",
        });
        var handler = new RecordingHttpMessageHandler("{}");
        var monitor = new LlmEndpointMonitor(store, new HttpClient(handler));

        var available = await monitor.CheckAsync(force: true, CancellationToken.None);

        Assert.True(available);
        Assert.Equal(LlmEndpointStatus.Available, monitor.Status);
        Assert.Equal("http://127.0.0.1:8080/v1/models", handler.RequestUri?.ToString());
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("test-key", handler.AuthorizationParameter);
    }

    [Fact]
    public void GlobalFallbackTracksEndpointAvailability()
    {
        var store = new UserCommandStore(filePath: null);
        AddCommand(store, "Summarize", "Summarize: {}", "summarize", sendDelayMilliseconds: 0);
        var client = new RecordingLlmClient("summary");
        var monitor = new ControllableEndpointMonitor(LlmEndpointStatus.Unavailable);
        var provider = new CommandPaletteLLMCommandsProvider(
            store,
            new LlmProviderSettingsStore(filePath: null),
            client,
            monitor);
        var fallback = Assert.IsType<FormattedFallbackItem>(
            Assert.Single(provider.FallbackCommands()));

        fallback.FallbackHandler!.UpdateQuery("some long text");
        Assert.Empty(fallback.Title);
        Assert.Empty(client.Prompts);

        monitor.SetStatus(LlmEndpointStatus.Available);
        Assert.Equal("summary", fallback.Title);
        Assert.Equal("Summarize: some long text", Assert.Single(client.Prompts));
    }

    [Fact]
    public void EndpointChanges_DoNotReregisterFallbackCommands()
    {
        var store = new UserCommandStore(filePath: null);
        AddCommand(store, "Summarize", "Summarize: {}", "summarize", sendDelayMilliseconds: 0);
        var monitor = new ControllableEndpointMonitor(LlmEndpointStatus.Available);
        var provider = new CommandPaletteLLMCommandsProvider(
            store,
            new LlmProviderSettingsStore(filePath: null),
            new RecordingLlmClient("summary"),
            monitor);
        var originalFallback = Assert.Single(provider.FallbackCommands());
        var itemsChanged = 0;
        provider.ItemsChanged += (_, _) => itemsChanged++;

        monitor.SetStatus(LlmEndpointStatus.Unavailable);
        monitor.SetStatus(LlmEndpointStatus.Available);

        Assert.Same(originalFallback, Assert.Single(provider.FallbackCommands()));
        Assert.Equal(0, itemsChanged);
    }

    [Theory]
    [InlineData("{} + {}", "Maxim", "Maxim + Maxim")]
    [InlineData("{{{}}}", "Maxim", "{Maxim}")]
    [InlineData("{{}}", "Maxim", "{}")]
    public void OutputFormatter_UsesStdFormatStyleBraces(
        string format,
        string argument,
        string expected)
    {
        var success = OutputFormatter.TryFormat(format, argument, out var result, out var error);

        Assert.True(success, error);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("}")]
    [InlineData("{0}")]
    public void OutputFormatter_RejectsUnsupportedBraces(string format)
    {
        Assert.False(OutputFormatter.TryFormat(format, "Maxim", out _, out _));
    }

    [Fact]
    public void Store_PersistsCommands()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"CommandPaletteLLM.Tests-{Guid.NewGuid():N}");
        var filePath = Path.Combine(directory, "commands.json");

        try
        {
            var store = new UserCommandStore(filePath);
            AddCommand(
                store,
                "Greeting",
                "Hello {}!",
                "persistent-id",
                enableAdvancedOutput: true,
                enableGlobalFallback: false,
                exposure: CommandExposure.Unspecified,
                customRequestArguments: """{"chat_template_kwargs":{"enable_thinking":true}}""");

            var reloaded = new UserCommandStore(filePath);

            var command = Assert.Single(reloaded.GetCommands());
            Assert.Equal("persistent-id", command.Id);
            Assert.Equal("Greeting", command.Name);
            Assert.Equal("Hello {}!", command.OutputFormat);
            Assert.Equal(650, command.SendDelayMilliseconds);
            Assert.Equal(
                """{"chat_template_kwargs":{"enable_thinking":true}}""",
                command.CustomRequestArguments);
            Assert.True(command.EnableAdvancedOutput);
            Assert.False(command.EnableGlobalFallback);
            Assert.Equal(CommandExposure.None, command.EffectiveExposure);
            Assert.Equal("default", command.ProviderId);
            Assert.Empty(command.IconPath);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void GlobalSettingsStore_DefaultsPersistsEmptyResetsAndRecoversFromCorruption()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"CommandPaletteLLM.Tests-{Guid.NewGuid():N}");
        var filePath = Path.Combine(directory, "settings.json");

        try
        {
            var store = new GlobalSettingsStore(filePath);
            Assert.Equal(
                GlobalSettings.DefaultAdvancedOutputSystemPrompt,
                store.Get().AdvancedOutputSystemPrompt);
            Assert.False(File.Exists(filePath));

            store.Replace(new GlobalSettings
            {
                AdvancedOutputSystemPrompt = GlobalSettings.LegacyAdvancedOutputSystemPrompt,
            });
            Assert.Equal(
                GlobalSettings.DefaultAdvancedOutputSystemPrompt,
                new GlobalSettingsStore(filePath).Get().AdvancedOutputSystemPrompt);

            store.Replace(new GlobalSettings
            {
                AdvancedOutputSystemPrompt = "Custom instruction",
            });
            Assert.Equal(
                "Custom instruction",
                new GlobalSettingsStore(filePath).Get().AdvancedOutputSystemPrompt);

            store.Replace(new GlobalSettings { AdvancedOutputSystemPrompt = string.Empty });
            Assert.Empty(new GlobalSettingsStore(filePath).Get().AdvancedOutputSystemPrompt);

            store.ResetAdvancedOutputSystemPrompt();
            Assert.Equal(
                GlobalSettings.DefaultAdvancedOutputSystemPrompt,
                new GlobalSettingsStore(filePath).Get().AdvancedOutputSystemPrompt);

            File.WriteAllText(filePath, "not json");
            Assert.Equal(
                GlobalSettings.DefaultAdvancedOutputSystemPrompt,
                new GlobalSettingsStore(filePath).Get().AdvancedOutputSystemPrompt);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void Store_LegacyCommandsDefaultToEnabledGlobalFallback()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"CommandPaletteLLM.Tests-{Guid.NewGuid():N}");
        var filePath = Path.Combine(directory, "commands.json");

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                filePath,
                """[{"Id":"legacy","Name":"Legacy","OutputFormat":"{}","SendDelayMilliseconds":650}]""");

            var store = new UserCommandStore(filePath);
            var command = Assert.Single(store.GetCommands());

            Assert.True(command.EnableGlobalFallback);
            Assert.False(command.EnableAdvancedOutput);
            Assert.Equal(CommandExposure.FallbackCommand, command.EffectiveExposure);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void ProviderStore_PersistsSettings()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"CommandPaletteLLM.Tests-{Guid.NewGuid():N}");
        var filePath = Path.Combine(directory, "provider.json");

        try
        {
            var store = new LlmProviderSettingsStore(filePath);
            store.Replace(new LlmProviderSettings
            {
                BaseUrl = "https://example.test/v1",
                Model = "test-model",
                ApiKey = "key",
            });

            var settings = new LlmProviderSettingsStore(filePath).Get();

            Assert.Equal("https://example.test/v1", settings.BaseUrl);
            Assert.Equal("test-model", settings.Model);
            Assert.Equal("key", settings.ApiKey);
            var persistedJson = File.ReadAllText(filePath);
            Assert.Contains("ProtectedApiKey", persistedJson, StringComparison.Ordinal);
            Assert.DoesNotContain("\"ApiKey\":\"key\"", persistedJson, StringComparison.Ordinal);
            Assert.DoesNotContain("\"key\"", persistedJson, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void ProviderStore_MigratesTheLegacySingleProviderFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"CommandPaletteLLM.Tests-{Guid.NewGuid():N}");
        var providersPath = Path.Combine(directory, "providers.json");
        var legacyPath = Path.Combine(directory, "provider.json");

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                legacyPath,
                """{"BaseUrl":"https://legacy.example/v1","Model":"legacy-model","ApiKey":"key"}""");

            var provider = Assert.Single(
                new LlmProviderSettingsStore(providersPath, legacyPath).GetProviders());

            Assert.Equal("default", provider.Id);
            Assert.Equal("Local LLM", provider.Name);
            Assert.Equal("https://legacy.example/v1", provider.BaseUrl);
            Assert.Equal("legacy-model", provider.Model);
            Assert.Equal("key", provider.ApiKey);
            var migratedJson = File.ReadAllText(providersPath);
            Assert.Contains("ProtectedApiKey", migratedJson, StringComparison.Ordinal);
            Assert.DoesNotContain("\"ApiKey\":\"key\"", migratedJson, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static CommandPaletteLLMCommandsProvider CreateProvider(out UserCommandStore store)
    {
        store = new UserCommandStore(filePath: null);
        return new CommandPaletteLLMCommandsProvider(store);
    }

    private static UserCommandsSettingsForm GetSettingsForm(
        CommandPaletteLLMCommandsProvider provider)
    {
        var settings = provider.Settings!;
        return Assert.IsType<UserCommandsSettingsForm>(
            Assert.Single(settings.SettingsPage.GetContent()));
    }

    private static void AddCommand(
        UserCommandStore store,
        string name,
        string outputFormat,
        string id,
        int sendDelayMilliseconds = 650,
        bool enableAdvancedOutput = false,
        bool enableGlobalFallback = true,
        CommandExposure exposure = CommandExposure.FallbackCommand,
        string customRequestArguments = "")
    {
        var commands = store.GetCommands().Select(command => command.Clone()).ToList();
        commands.Add(new UserCommandDefinition
        {
            Id = id,
            Name = name,
            OutputFormat = outputFormat,
            SendDelayMilliseconds = sendDelayMilliseconds,
            CustomRequestArguments = customRequestArguments,
            EnableAdvancedOutput = enableAdvancedOutput,
            Exposure = exposure,
            EnableGlobalFallback = exposure == CommandExposure.Unspecified
                ? enableGlobalFallback
                : exposure == CommandExposure.FallbackCommand,
        });
        store.ReplaceAll(commands);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    private static void AssertAdvancedOutputError(
        Microsoft.CommandPalette.Extensions.IListItem item,
        string title,
        string rawContent)
    {
        Assert.Equal(title, item.Title);
        Assert.Equal(rawContent, item.Details?.Body);
        Assert.Equal(title, item.Details?.Title);
        Assert.Equal(ContentSize.Large, Assert.IsType<Details>(item.Details).Size);
        Assert.Equal(rawContent, Assert.IsType<CopyTextCommand>(item.Command).Text);
        Assert.Equal(rawContent, item.TextToSuggest);
    }

    private sealed class RecordingLlmClient(string response) : ILlmClient
    {
        public List<string> Prompts { get; } = [];

        public List<string?> SystemPrompts { get; } = [];

        public List<string> RequestArguments { get; } = [];

        public Task<IReadOnlyList<string>> CompleteAsync(
            string prompt,
            string? systemPrompt,
            string customRequestArguments,
            CancellationToken cancellationToken)
        {
            Prompts.Add(prompt);
            SystemPrompts.Add(systemPrompt);
            RequestArguments.Add(customRequestArguments);
            return Task.FromResult<IReadOnlyList<string>>([response]);
        }
    }

    private sealed class SequenceLlmClient(params string[] responses) : ILlmClient
    {
        public string RequestedCustomRequestArguments { get; private set; } = string.Empty;

        public Task<IReadOnlyList<string>> CompleteAsync(
            string prompt,
            string? systemPrompt,
            string customRequestArguments,
            CancellationToken cancellationToken)
        {
            RequestedCustomRequestArguments = customRequestArguments;
            return Task.FromResult<IReadOnlyList<string>>(responses);
        }
    }

    private sealed class ControlledLlmClient : ILlmClient
    {
        public List<PendingRequest> Requests { get; } = [];

        public async Task<IReadOnlyList<string>> CompleteAsync(
            string prompt,
            string? systemPrompt,
            string customRequestArguments,
            CancellationToken cancellationToken)
        {
            var request = new PendingRequest(prompt, cancellationToken);
            Requests.Add(request);
            return [await request.Completion.Task];
        }
    }

    private sealed class ControllableEndpointMonitor(LlmEndpointStatus initialStatus)
        : ILlmEndpointMonitor
    {
        public LlmEndpointStatus Status { get; private set; } = initialStatus;

        public event Action? StatusChanged;

        public Task<bool> CheckAsync(bool force, CancellationToken cancellationToken) =>
            Task.FromResult(Status == LlmEndpointStatus.Available);

        public void Invalidate() => SetStatus(LlmEndpointStatus.Unknown);

        public void ReportReachable() => SetStatus(LlmEndpointStatus.Available);

        public void ReportUnreachable() => SetStatus(LlmEndpointStatus.Unavailable);

        public void SetStatus(LlmEndpointStatus status)
        {
            if (Status != status)
            {
                Status = status;
                StatusChanged?.Invoke();
            }
        }
    }

    private sealed class DelayedEndpointMonitor : ILlmEndpointMonitor
    {
        private readonly TaskCompletionSource<bool> _checkCompletion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public LlmEndpointStatus Status => LlmEndpointStatus.Available;

        public int PendingCheckCount { get; private set; }

        public event Action? StatusChanged
        {
            add { }
            remove { }
        }

        public Task<bool> CheckAsync(bool force, CancellationToken cancellationToken)
        {
            if (force)
            {
                return Task.FromResult(true);
            }

            PendingCheckCount++;
            return _checkCompletion.Task;
        }

        public void CompleteCheck() => _checkCompletion.TrySetResult(true);

        public void Invalidate()
        {
        }

        public void ReportReachable()
        {
        }

        public void ReportUnreachable()
        {
        }
    }

    private sealed class PendingRequest(string prompt, CancellationToken cancellationToken)
    {
        public string Prompt { get; } = prompt;

        public CancellationToken CancellationToken { get; } = cancellationToken;

        public TaskCompletionSource<string> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class RecordingHttpMessageHandler(string responseJson) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public string? RequestBody { get; private set; }

        public string? AuthorizationScheme { get; private set; }

        public string? AuthorizationParameter { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            };
        }
    }
}
