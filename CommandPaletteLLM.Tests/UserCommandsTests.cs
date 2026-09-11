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
        Assert.Equal(1, itemsChanged);
        var command = Assert.Single(provider.TopLevelCommands());
        Assert.Equal("Greeting", command.Title);
        Assert.Equal($"CommandPaletteLLM.Command.{definition.Id}", command.Command.Id);
        var fallback = Assert.IsType<FormattedFallbackItem>(Assert.Single(provider.FallbackCommands()));
        Assert.Equal("Greeting", fallback.DisplayTitle);
        Assert.Equal($"CommandPaletteLLM.Global.{definition.Id}", fallback.Id);
    }

    [Fact]
    public void Settings_EditsCommandWithoutChangingItsIdentity()
    {
        var provider = CreateProvider(out var store);
        AddCommand(store, "Greeting", "Hello {}!", "stable-id");
        provider = new CommandPaletteLLMCommandsProvider(store);
        var originalCommandId = Assert.Single(provider.TopLevelCommands()).Command.Id;
        var originalFallbackId = Assert.IsType<FormattedFallbackItem>(
            Assert.Single(provider.FallbackCommands())).Id;
        var form = GetSettingsForm(provider);

        form.SubmitForm(
            """{"name_stable-id":"Welcome","format_stable-id":"Welcome, {}.","delay_stable-id":125}""",
            """{"actionId":"save:stable-id"}""");

        Assert.Equal(originalCommandId, Assert.Single(provider.TopLevelCommands()).Command.Id);
        Assert.Equal(
            originalFallbackId,
            Assert.IsType<FormattedFallbackItem>(Assert.Single(provider.FallbackCommands())).Id);
        Assert.Equal("Welcome", Assert.Single(provider.TopLevelCommands()).Title);
        Assert.Equal(125, Assert.Single(store.GetCommands()).SendDelayMilliseconds);
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
    public void Settings_ExistingCommandHasClearlyScopedActions()
    {
        var provider = CreateProvider(out var store);
        AddCommand(store, "Greeting", "Hello {}!", "stable-id");
        provider = new CommandPaletteLLMCommandsProvider(store);
        var form = GetSettingsForm(provider);

        Assert.Contains("Save changes", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("\"id\":\"save:stable-id\"", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("\"id\":\"delete:stable-id\"", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("Action.ToggleVisibility", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("\"isVisible\":false", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("Edit command", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("Delete command", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("Cancel editing", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("Send delay (milliseconds)", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"Input.Number\"", form.TemplateJson, StringComparison.Ordinal);
        Assert.Contains("\"id\":\"add\"", form.TemplateJson, StringComparison.Ordinal);
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
        }, client, TimeSpan.Zero);

        page.SearchText = "Maxim";

        var item = Assert.Single(page.GetItems());
        Assert.Equal("This is the answer.", item.Title);
        Assert.IsType<CopyTextCommand>(item.Command);
        Assert.Equal("Hello Maxim!", Assert.Single(client.Prompts));
        Assert.False(page.IsLoading);
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
        Assert.Equal("Hello Maxim!", Assert.Single(client.Prompts));
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
        await WaitUntilAsync(() => page.GetItems().Length == 1);
        Assert.Equal("new response", Assert.Single(page.GetItems()).Title);

        client.Requests[0].Completion.SetResult("stale response");
        await Task.Yield();
        Assert.Equal("new response", Assert.Single(page.GetItems()).Title);
    }

    [Fact]
    public void Provider_CancelsRequestsAcrossRootAndCommandPageNavigation()
    {
        var store = new UserCommandStore(filePath: null);
        AddCommand(store, "Summarize", "Summarize: {}", "summarize", sendDelayMilliseconds: 0);
        var client = new ControlledLlmClient();
        var provider = new CommandPaletteLLMCommandsProvider(
            store,
            new LlmProviderSettingsStore(filePath: null),
            client);
        var page = Assert.IsType<FormattedCommandPage>(
            Assert.Single(provider.TopLevelCommands()).Command);
        var fallback = Assert.IsType<FormattedFallbackItem>(
            Assert.Single(provider.FallbackCommands()));

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
        AddCommand(store, "Summarize", "Summarize: {}", "summarize", sendDelayMilliseconds: 0);
        var client = new RecordingLlmClient("unused");
        var provider = new CommandPaletteLLMCommandsProvider(
            store,
            new LlmProviderSettingsStore(filePath: null),
            client);
        var fallback = Assert.IsType<FormattedFallbackItem>(
            Assert.Single(provider.FallbackCommands()));

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
            """{"choices":[{"message":{"role":"assistant","content":"  The answer.  "}}]}""");
        var client = new OpenAiCompatibleLlmClient(store, new HttpClient(handler));

        var response = await client.CompleteAsync("Explain this", CancellationToken.None);

        Assert.Equal("The answer.", response);
        Assert.Equal("http://127.0.0.1:8080/v1/chat/completions", handler.RequestUri?.ToString());
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("test-key", handler.AuthorizationParameter);
        using var requestJson = JsonDocument.Parse(Assert.IsType<string>(handler.RequestBody));
        Assert.Equal("local-model", requestJson.RootElement.GetProperty("model").GetString());
        Assert.Equal(
            "Explain this",
            requestJson.RootElement.GetProperty("messages")[0].GetProperty("content").GetString());
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
            AddCommand(store, "Greeting", "Hello {}!", "persistent-id");

            var reloaded = new UserCommandStore(filePath);

            var command = Assert.Single(reloaded.GetCommands());
            Assert.Equal("persistent-id", command.Id);
            Assert.Equal("Greeting", command.Name);
            Assert.Equal("Hello {}!", command.OutputFormat);
            Assert.Equal(650, command.SendDelayMilliseconds);
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
        int sendDelayMilliseconds = 650)
    {
        var commands = store.GetCommands().Select(command => command.Clone()).ToList();
        commands.Add(new UserCommandDefinition
        {
            Id = id,
            Name = name,
            OutputFormat = outputFormat,
            SendDelayMilliseconds = sendDelayMilliseconds,
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

    private sealed class RecordingLlmClient(string response) : ILlmClient
    {
        public List<string> Prompts { get; } = [];

        public Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken)
        {
            Prompts.Add(prompt);
            return Task.FromResult(response);
        }
    }

    private sealed class ControlledLlmClient : ILlmClient
    {
        public List<PendingRequest> Requests { get; } = [];

        public Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken)
        {
            var request = new PendingRequest(prompt, cancellationToken);
            Requests.Add(request);
            return request.Completion.Task;
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
