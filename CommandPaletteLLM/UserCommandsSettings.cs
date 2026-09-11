using Microsoft.CommandPalette.Extensions;
using System;

namespace CommandPaletteLLM;

internal sealed partial class UserCommandsSettings : ICommandSettings
{
    public UserCommandsSettings(
        UserCommandStore store,
        LlmProviderSettingsStore providerSettingsStore,
        Action commandsChanged,
        Action providerSettingsChanged)
    {
        SettingsPage = new UserCommandsSettingsPage(
            store,
            providerSettingsStore,
            commandsChanged,
            providerSettingsChanged);
    }

    public IContentPage SettingsPage { get; }
}
