using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System;

namespace CommandPaletteLLM;

internal sealed partial class UserCommandsSettingsPage : ContentPage
{
    private readonly UserCommandsSettingsForm _form;

    public UserCommandsSettingsPage(UserCommandStore store, Action commandsChanged)
    {
        Title = "Command Palette LLM settings";
        Name = "Settings";
        Icon = new IconInfo("\uE713");
        _form = new UserCommandsSettingsForm(store, commandsChanged);
    }

    public override IContent[] GetContent() => [_form];
}
