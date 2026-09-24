using System;
using System.Collections.Generic;
using System.Linq;

namespace CommandPaletteLLM;

internal sealed class StepTemplateStore
{
    private readonly object _sync = new();
    private readonly SettingsDocumentStore? _settingsDocumentStore;
    private List<StepTemplateDefinition> _templates;

    internal StepTemplateStore()
    {
        _templates = [];
    }

    internal StepTemplateStore(SettingsDocumentStore settingsDocumentStore)
    {
        _settingsDocumentStore = settingsDocumentStore;
        _templates = settingsDocumentStore.GetStepTemplates()
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
            _templates = _settingsDocumentStore.GetStepTemplates()
                .Select(template => template.Clone())
                .ToList();
        }
    }

    public IReadOnlyList<StepTemplateDefinition> GetTemplates()
    {
        lock (_sync)
        {
            return _templates.Select(template => template.Clone()).ToArray();
        }
    }

    public StepTemplateDefinition? Get(string templateId)
    {
        lock (_sync)
        {
            return _templates.FirstOrDefault(template =>
                string.Equals(template.Id, templateId, StringComparison.Ordinal))?.Clone();
        }
    }

    public void ReplaceAll(IEnumerable<StepTemplateDefinition> templates)
    {
        var replacement = templates.Select(template => template.Clone()).ToList();

        lock (_sync)
        {
            _settingsDocumentStore?.ReplaceStepTemplates(replacement);
            _templates = replacement;
        }
    }

    internal static bool IsValid(StepTemplateDefinition template)
    {
        if (template is null ||
            string.IsNullOrWhiteSpace(template.Id) ||
            string.IsNullOrWhiteSpace(template.Name) ||
            string.IsNullOrWhiteSpace(template.ProviderId) ||
            string.IsNullOrWhiteSpace(template.Prompt) ||
            !OutputFormatter.TryFormat(template.Prompt, string.Empty, out _, out _) ||
            !CustomRequestArguments.TryParse(
                template.CustomRequestArguments,
                out var arguments,
                out _))
        {
            return false;
        }

        arguments?.Dispose();
        return true;
    }
}
