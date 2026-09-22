using Microsoft.CommandPalette.Extensions;
using System;

namespace CommandPaletteLLM;

internal sealed partial class UserCommandsSettings : ICommandSettings
{
    public UserCommandsSettings(
        UserCommandStore store,
        LlmProviderSettingsStore providerSettingsStore,
        GlobalSettingsStore globalSettingsStore,
        Action commandsChanged,
        Action globalSettingsChanged,
        Action providerSettingsChanged)
    {
        SettingsPage = new UserCommandsSettingsPage(
            store,
            providerSettingsStore,
            globalSettingsStore,
            commandsChanged,
            globalSettingsChanged,
            providerSettingsChanged);
    }

    public IContentPage SettingsPage { get; }
}
