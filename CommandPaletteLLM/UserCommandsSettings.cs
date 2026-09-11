using Microsoft.CommandPalette.Extensions;
using System;

namespace CommandPaletteLLM;

internal sealed partial class UserCommandsSettings : ICommandSettings
{
    public UserCommandsSettings(
        UserCommandStore store,
        LlmProviderSettingsStore providerSettingsStore,
        Action commandsChanged)
    {
        SettingsPage = new UserCommandsSettingsPage(
            store,
            providerSettingsStore,
            commandsChanged);
    }

    public IContentPage SettingsPage { get; }
}
