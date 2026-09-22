using System;
using System.Collections.Generic;
using System.Linq;

namespace CommandPaletteLLM;

internal sealed class JsonRequestTemplateStore
{
    private readonly object _sync = new();
    private readonly SettingsDocumentStore? _settingsDocumentStore;
    private List<JsonRequestTemplateDefinition> _templates;

    internal JsonRequestTemplateStore()
    {
        _templates = [];
    }

    internal JsonRequestTemplateStore(SettingsDocumentStore settingsDocumentStore)
    {
        _settingsDocumentStore = settingsDocumentStore;
        _templates = settingsDocumentStore.GetJsonRequestTemplates()
            .Select(template => template.Clone())
            .ToList();
    }

    internal void ReloadFromDocument()
    {
        if (_settingsDocumentStore is null)
        {
            return;
        }

        lock (_sync)
        {
            _templates = _settingsDocumentStore.GetJsonRequestTemplates()
                .Select(template => template.Clone())
                .ToList();
        }
    }

    public IReadOnlyList<JsonRequestTemplateDefinition> GetTemplates()
    {
        lock (_sync)
        {
            return _templates.Select(template => template.Clone()).ToArray();
        }
    }

    public JsonRequestTemplateDefinition? Get(string templateId)
    {
        lock (_sync)
        {
            return _templates.FirstOrDefault(template =>
                string.Equals(template.Id, templateId, StringComparison.Ordinal))?.Clone();
        }
    }

    public void ReplaceAll(IEnumerable<JsonRequestTemplateDefinition> templates)
    {
        var replacement = templates.Select(template => template.Clone()).ToList();

        lock (_sync)
        {
            _settingsDocumentStore?.ReplaceJsonRequestTemplates(replacement);
            _templates = replacement;
        }
    }
}
