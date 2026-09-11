using Microsoft.CommandPalette.Extensions;
using System;

namespace CommandPaletteLLM;

internal sealed partial class UserCommandsSettings : ICommandSettings
{
    public UserCommandsSettings(UserCommandStore store, Action commandsChanged)
    {
        SettingsPage = new UserCommandsSettingsPage(store, commandsChanged);
    }

    public IContentPage SettingsPage { get; }
}
