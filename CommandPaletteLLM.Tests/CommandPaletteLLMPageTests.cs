using Microsoft.CommandPalette.Extensions;
using Xunit;

namespace CommandPaletteLLM.Tests;

public sealed class CommandPaletteLLMPageTests
{
    [Fact]
    public void TopLevelCommands_ExposesEchoCommandWithStableIdentity()
    {
        var command = CreateCommand();

        Assert.Equal("Echo message", command.Title);
        Assert.Equal("CommandPaletteLLM.Echo", command.Command.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void GetItems_ReturnsNoItemsForEmptyInput(string input)
    {
        var page = CreatePage();

        page.SearchText = input;

        Assert.Empty(page.GetItems());
    }

    [Theory]
    [InlineData("Hello world!")]
    [InlineData("Hello,  world! #42")]
    public void GetItems_ReturnsOneItemContainingTheCompleteInput(string input)
    {
        var page = CreatePage();

        page.SearchText = input;

        var item = Assert.Single(page.GetItems());
        Assert.Equal(input, item.Title);
    }

    [Fact]
    public void GetItems_ReplacesThePreviousResultWhenInputChanges()
    {
        var page = CreatePage();
        page.SearchText = "First";

        page.SearchText = "Second";

        var item = Assert.Single(page.GetItems());
        Assert.Equal("Second", item.Title);
    }

    [Fact]
    public void EchoResult_KeepsCommandPaletteOpenWhenInvoked()
    {
        var page = CreatePage();
        page.SearchText = "Hello world!";
        var item = Assert.Single(page.GetItems());
        var command = Assert.IsAssignableFrom<IInvokableCommand>(item.Command);

        var result = command.Invoke(item);

        Assert.Equal(CommandResultKind.KeepOpen, result.Kind);
    }

    [Fact]
    public void FallbackCommands_ExposesGlobalEchoWithStableIdentity()
    {
        var provider = new CommandPaletteLLMCommandsProvider();

        var fallback = Assert.IsType<GlobalEchoFallbackItem>(Assert.Single(provider.FallbackCommands()));
        Assert.Equal("Echo message in global results", fallback.DisplayTitle);
        Assert.Equal("CommandPaletteLLM.GlobalEcho", fallback.Id);
    }

    [Fact]
    public void GlobalEcho_ReturnsTheCompleteQuery()
    {
        var fallback = new GlobalEchoFallbackItem();

        fallback.FallbackHandler.UpdateQuery("Hello,  world! #42");

        Assert.Equal("Hello,  world! #42", fallback.Title);
    }

    [Fact]
    public void GlobalEcho_HidesWhitespaceOnlyQueries()
    {
        var fallback = new GlobalEchoFallbackItem();

        fallback.FallbackHandler.UpdateQuery("  \t");

        Assert.Empty(fallback.Title);
    }

    [Fact]
    public void GlobalEchoResult_KeepsCommandPaletteOpenWhenInvoked()
    {
        var fallback = new GlobalEchoFallbackItem();
        fallback.FallbackHandler.UpdateQuery("Hello world!");
        var command = Assert.IsAssignableFrom<IInvokableCommand>(fallback.Command);

        var result = command.Invoke(fallback);

        Assert.Equal(CommandResultKind.KeepOpen, result.Kind);
    }

    private static ICommandItem CreateCommand()
    {
        var provider = new CommandPaletteLLMCommandsProvider();
        return Assert.Single(provider.TopLevelCommands());
    }

    private static IDynamicListPage CreatePage() =>
        Assert.IsAssignableFrom<IDynamicListPage>(CreateCommand().Command);
}
