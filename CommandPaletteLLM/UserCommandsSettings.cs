using Microsoft.CommandPalette.Extensions;
using System;

namespace CommandPaletteLLM;

internal sealed partial class UserCommandsSettings : ICommandSettings
{
    public UserCommandsSettings(
        UserCommandStore store,
        LlmProviderSettingsStore providerSettingsStore,
        GlobalSettingsStore globalSettingsStore,
        JsonRequestTemplateStore requestTemplateStore,
        Action commandsChanged,
        Action globalSettingsChanged,
        Action providerSettingsChanged,
        Action requestTemplatesChanged)
    {
        SettingsPage = new UserCommandsSettingsPage(
            store,
            providerSettingsStore,
            globalSettingsStore,
            requestTemplateStore,
            commandsChanged,
            globalSettingsChanged,
            providerSettingsChanged,
            requestTemplatesChanged);
    }

    public IContentPage SettingsPage { get; }
}
