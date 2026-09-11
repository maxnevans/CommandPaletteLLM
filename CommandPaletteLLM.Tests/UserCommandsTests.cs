using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.IO;
using System.Linq;
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
            """{"name_stable-id":"Welcome","format_stable-id":"Welcome, {}."}""",
            """{"actionId":"save:stable-id"}""");

        Assert.Equal(originalCommandId, Assert.Single(provider.TopLevelCommands()).Command.Id);
        Assert.Equal(
            originalFallbackId,
            Assert.IsType<FormattedFallbackItem>(Assert.Single(provider.FallbackCommands())).Id);
        Assert.Equal("Welcome", Assert.Single(provider.TopLevelCommands()).Title);
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
    public void FormattedPage_ReturnsOneFormattedResult()
    {
        var page = new FormattedCommandPage(new UserCommandDefinition
        {
            Id = "greeting",
            Name = "Greeting",
            OutputFormat = "Hello {}!",
        });

        page.SearchText = "Maxim";

        var item = Assert.Single(page.GetItems());
        Assert.Equal("Hello Maxim!", item.Title);
        var result = Assert.IsAssignableFrom<IInvokableCommand>(item.Command).Invoke(item);
        Assert.Equal(CommandResultKind.KeepOpen, result.Kind);
    }

    [Fact]
    public void FormattedFallback_ReturnsOneFormattedGlobalResult()
    {
        var fallback = new FormattedFallbackItem(new UserCommandDefinition
        {
            Id = "greeting",
            Name = "Greeting",
            OutputFormat = "Hello {}!",
        });

        fallback.FallbackHandler.UpdateQuery("Maxim");

        Assert.Equal("Hello Maxim!", fallback.Title);
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
        var page = new FormattedCommandPage(definition);
        var fallback = new FormattedFallbackItem(definition);

        page.SearchText = query;
        fallback.FallbackHandler.UpdateQuery(query);

        Assert.Empty(page.GetItems());
        Assert.Empty(fallback.Title);
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
        CommandPaletteLLMCommandsProvider provider) =>
        Assert.IsType<UserCommandsSettingsForm>(
            Assert.Single(provider.Settings.SettingsPage.GetContent()));

    private static void AddCommand(
        UserCommandStore store,
        string name,
        string outputFormat,
        string id)
    {
        var commands = store.GetCommands().Select(command => command.Clone()).ToList();
        commands.Add(new UserCommandDefinition
        {
            Id = id,
            Name = name,
            OutputFormat = outputFormat,
        });
        store.ReplaceAll(commands);
    }
}
