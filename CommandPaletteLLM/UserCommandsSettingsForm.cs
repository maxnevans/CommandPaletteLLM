using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CommandPaletteLLM;

internal sealed partial class UserCommandsSettingsForm : FormContent
{
    private const string SeparatorImageUrl = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAA+gAAAAfCAYAAAB03OfYAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAACcSURBVHhe7dcxDQAACAQxpL9z2DEAQ5uciKsCAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA+C1JS5IkSZKk2/avAwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAADwzQHTAoH7+gNkAAAAASUVORK5CYII=";
    private const string SectionSeparatorImageUrl = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAA+gAAAA+CAYAAAC4LDFLAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAEeSURBVHhe7dkxDQAwDAPB8Adlau3eLeqQDHfSg7BcBQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAADAhyRHkiRJkiTNZqBLkiRJkrSg91AHAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAKDjAnBCLsjc6V35AAAAAElFTkSuQmCC";
    private const string TimelineImageUrl = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACAAAAABCAYAAAC/iqxnAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAAUSURBVBhXY2CgEFRUVExDFyMFAAB81wH/qjhvZAAAAABJRU5ErkJggg==";

    private readonly UserCommandStore _store;
    private readonly LlmProviderSettingsStore _providerSettingsStore;
    private readonly JsonRequestTemplateStore _requestTemplateStore;
    private readonly StepTemplateStore _stepTemplateStore;
    private readonly GlobalSettingsStore _globalSettingsStore;
    private readonly Action _commandsChanged;
    private readonly Action _globalSettingsChanged;
    private readonly Action _providerSettingsChanged;
    private readonly Action _requestTemplatesChanged;
    private readonly CommandIconStore _iconStore;
    private readonly ISettingsFileLauncher _settingsFileLauncher;
    private readonly HashSet<string> _expandedCommandIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _expandedProviderIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _expandedRequestTemplateIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _expandedStepTemplateIds = new(StringComparer.Ordinal);
    private readonly JsonObject _draftInputs = [];
    private bool _globalSettingsExpanded;

    public UserCommandsSettingsForm(
        UserCommandStore store,
        LlmProviderSettingsStore providerSettingsStore,
        GlobalSettingsStore globalSettingsStore,
        Action commandsChanged,
        Action globalSettingsChanged,
        Action providerSettingsChanged)
        : this(
            store,
            providerSettingsStore,
            globalSettingsStore,
            new JsonRequestTemplateStore(),
            new StepTemplateStore(),
            commandsChanged,
            globalSettingsChanged,
            providerSettingsChanged,
            () => { },
            new SettingsFileLauncher())
    {
    }

    internal UserCommandsSettingsForm(
        UserCommandStore store,
        LlmProviderSettingsStore providerSettingsStore,
        GlobalSettingsStore globalSettingsStore,
        Action commandsChanged,
        Action globalSettingsChanged,
        Action providerSettingsChanged,
        ISettingsFileLauncher settingsFileLauncher)
        : this(
            store,
            providerSettingsStore,
            globalSettingsStore,
            new JsonRequestTemplateStore(),
            new StepTemplateStore(),
            commandsChanged,
            globalSettingsChanged,
            providerSettingsChanged,
            () => { },
            settingsFileLauncher)
    {
    }

    public UserCommandsSettingsForm(
        UserCommandStore store,
        LlmProviderSettingsStore providerSettingsStore,
        GlobalSettingsStore globalSettingsStore,
        JsonRequestTemplateStore requestTemplateStore,
        Action commandsChanged,
        Action globalSettingsChanged,
        Action providerSettingsChanged,
        Action requestTemplatesChanged)
        : this(
            store,
            providerSettingsStore,
            globalSettingsStore,
            requestTemplateStore,
            new StepTemplateStore(),
            commandsChanged,
            globalSettingsChanged,
            providerSettingsChanged,
            requestTemplatesChanged,
            new SettingsFileLauncher())
    {
    }

    internal UserCommandsSettingsForm(
        UserCommandStore store,
        LlmProviderSettingsStore providerSettingsStore,
        GlobalSettingsStore globalSettingsStore,
        JsonRequestTemplateStore requestTemplateStore,
        Action commandsChanged,
        Action globalSettingsChanged,
        Action providerSettingsChanged,
        Action requestTemplatesChanged,
        ISettingsFileLauncher settingsFileLauncher)
        : this(
            store,
            providerSettingsStore,
            globalSettingsStore,
            requestTemplateStore,
            new StepTemplateStore(),
            commandsChanged,
            globalSettingsChanged,
            providerSettingsChanged,
            requestTemplatesChanged,
            settingsFileLauncher)
    {
    }

    public UserCommandsSettingsForm(
        UserCommandStore store,
        LlmProviderSettingsStore providerSettingsStore,
        GlobalSettingsStore globalSettingsStore,
        JsonRequestTemplateStore requestTemplateStore,
        StepTemplateStore stepTemplateStore,
        Action commandsChanged,
        Action globalSettingsChanged,
        Action providerSettingsChanged,
        Action requestTemplatesChanged)
        : this(
            store,
            providerSettingsStore,
            globalSettingsStore,
            requestTemplateStore,
            stepTemplateStore,
            commandsChanged,
            globalSettingsChanged,
            providerSettingsChanged,
            requestTemplatesChanged,
            new SettingsFileLauncher())
    {
    }

    internal UserCommandsSettingsForm(
        UserCommandStore store,
        LlmProviderSettingsStore providerSettingsStore,
        GlobalSettingsStore globalSettingsStore,
        JsonRequestTemplateStore requestTemplateStore,
        StepTemplateStore stepTemplateStore,
        Action commandsChanged,
        Action globalSettingsChanged,
        Action providerSettingsChanged,
        Action requestTemplatesChanged,
        ISettingsFileLauncher settingsFileLauncher)
    {
        _store = store;
        _providerSettingsStore = providerSettingsStore;
        _globalSettingsStore = globalSettingsStore;
        _requestTemplateStore = requestTemplateStore;
        _stepTemplateStore = stepTemplateStore;
        _commandsChanged = commandsChanged;
        _globalSettingsChanged = globalSettingsChanged;
        _providerSettingsChanged = providerSettingsChanged;
        _requestTemplatesChanged = requestTemplatesChanged;
        _settingsFileLauncher = settingsFileLauncher;
        _iconStore = new CommandIconStore(store.StorageDirectory);
        Refresh();
    }

    public override CommandResult SubmitForm(string inputs, string data) =>
        Submit(inputs, ReadActionId(data) ?? ReadActionId(inputs));

    public override CommandResult SubmitForm(string payload) =>
        Submit(payload, ReadActionId(payload));

    private CommandResult Submit(string payload, string? actionId)
    {
        try
        {
            var input = JsonNode.Parse(payload)?.AsObject();
            if (input is null)
            {
                return ShowError("Command settings could not be read.");
            }

            CaptureDraftInputs(input);

            if (actionId?.StartsWith("pipeline:", StringComparison.Ordinal) == true)
            {
                return EditPipeline(actionId);
            }

            if (string.Equals(actionId, "reveal-settings-file", StringComparison.Ordinal))
            {
                return OpenSettingsFile(reveal: true);
            }

            if (string.Equals(actionId, "open-settings-file", StringComparison.Ordinal))
            {
                return OpenSettingsFile(reveal: false);
            }

            if (string.Equals(actionId, "import-settings", StringComparison.Ordinal))
            {
                return ImportSettings();
            }

            if (string.Equals(actionId, "export-settings", StringComparison.Ordinal))
            {
                return ExportSettings();
            }

            if (string.Equals(actionId, "reload-settings", StringComparison.Ordinal))
            {
                return ReloadSettings();
            }

            if (string.Equals(actionId, "toggle-settings-format", StringComparison.Ordinal))
            {
                return ToggleSettingsFormatting();
            }

            if (string.Equals(actionId, "edit-global", StringComparison.Ordinal))
            {
                _globalSettingsExpanded = true;
                Refresh();
                return CommandResult.KeepOpen();
            }

            if (string.Equals(actionId, "cancel-global", StringComparison.Ordinal))
            {
                _globalSettingsExpanded = false;
                RemoveDraftInput("advancedOutputSystemPrompt");
                Refresh();
                return CommandResult.KeepOpen();
            }

            if (string.Equals(actionId, "save-global", StringComparison.Ordinal))
            {
                var settings = _globalSettingsStore.Get();
                settings.AdvancedOutputSystemPrompt = ReadText(
                    input,
                    "advancedOutputSystemPrompt",
                    settings.AdvancedOutputSystemPrompt);
                _globalSettingsStore.Replace(settings);
                _globalSettingsExpanded = false;
                RemoveDraftInput("advancedOutputSystemPrompt");
                _globalSettingsChanged();
                Refresh();
                return CommandResult.KeepOpen();
            }

            if (string.Equals(actionId, "reset-global", StringComparison.Ordinal))
            {
                _globalSettingsStore.ResetAdvancedOutputSystemPrompt();
                _globalSettingsExpanded = false;
                RemoveDraftInput("advancedOutputSystemPrompt");
                _globalSettingsChanged();
                Refresh();
                return CommandResult.KeepOpen();
            }

            const string editPrefix = "edit:";
            if (actionId?.StartsWith(editPrefix, StringComparison.Ordinal) == true)
            {
                return SetCommandEditorVisibility(actionId[editPrefix.Length..], isExpanded: true);
            }

            const string cancelPrefix = "cancel:";
            if (actionId?.StartsWith(cancelPrefix, StringComparison.Ordinal) == true)
            {
                return SetCommandEditorVisibility(actionId[cancelPrefix.Length..], isExpanded: false);
            }

            const string editProviderPrefix = "edit-provider:";
            if (actionId?.StartsWith(editProviderPrefix, StringComparison.Ordinal) == true)
            {
                return SetProviderEditorVisibility(actionId[editProviderPrefix.Length..], isExpanded: true);
            }

            const string cancelProviderPrefix = "cancel-provider:";
            if (actionId?.StartsWith(cancelProviderPrefix, StringComparison.Ordinal) == true)
            {
                return SetProviderEditorVisibility(actionId[cancelProviderPrefix.Length..], isExpanded: false);
            }

            const string editRequestTemplatePrefix = "edit-request-template:";
            if (actionId?.StartsWith(editRequestTemplatePrefix, StringComparison.Ordinal) == true)
            {
                return SetRequestTemplateEditorVisibility(
                    actionId[editRequestTemplatePrefix.Length..],
                    isExpanded: true);
            }

            const string cancelRequestTemplatePrefix = "cancel-request-template:";
            if (actionId?.StartsWith(cancelRequestTemplatePrefix, StringComparison.Ordinal) == true)
            {
                return SetRequestTemplateEditorVisibility(
                    actionId[cancelRequestTemplatePrefix.Length..],
                    isExpanded: false);
            }

            const string editStepTemplatePrefix = "edit-step-template:";
            if (actionId?.StartsWith(editStepTemplatePrefix, StringComparison.Ordinal) == true)
            {
                return SetStepTemplateEditorVisibility(
                    actionId[editStepTemplatePrefix.Length..],
                    isExpanded: true);
            }

            const string cancelStepTemplatePrefix = "cancel-step-template:";
            if (actionId?.StartsWith(cancelStepTemplatePrefix, StringComparison.Ordinal) == true)
            {
                return SetStepTemplateEditorVisibility(
                    actionId[cancelStepTemplatePrefix.Length..],
                    isExpanded: false);
            }

            if (string.Equals(actionId, "add", StringComparison.Ordinal))
            {
                return Add(input);
            }

            if (string.Equals(actionId, "add-provider", StringComparison.Ordinal))
            {
                return AddProvider(input);
            }

            if (string.Equals(actionId, "add-request-template", StringComparison.Ordinal))
            {
                return AddRequestTemplate(input);
            }

            if (string.Equals(actionId, "add-step-template", StringComparison.Ordinal))
            {
                return AddStepTemplate(input);
            }

            const string saveProviderPrefix = "save-provider:";
            if (actionId?.StartsWith(saveProviderPrefix, StringComparison.Ordinal) == true)
            {
                return SaveProvider(input, actionId[saveProviderPrefix.Length..]);
            }

            const string clearProviderKeyPrefix = "clear-provider-key:";
            if (actionId?.StartsWith(clearProviderKeyPrefix, StringComparison.Ordinal) == true)
            {
                return ClearProviderApiKey(actionId[clearProviderKeyPrefix.Length..]);
            }

            const string deleteProviderPrefix = "delete-provider:";
            if (actionId?.StartsWith(deleteProviderPrefix, StringComparison.Ordinal) == true)
            {
                return DeleteProvider(actionId[deleteProviderPrefix.Length..]);
            }

            const string saveRequestTemplatePrefix = "save-request-template:";
            if (actionId?.StartsWith(saveRequestTemplatePrefix, StringComparison.Ordinal) == true)
            {
                return SaveRequestTemplate(input, actionId[saveRequestTemplatePrefix.Length..]);
            }

            const string deleteRequestTemplatePrefix = "delete-request-template:";
            if (actionId?.StartsWith(deleteRequestTemplatePrefix, StringComparison.Ordinal) == true)
            {
                return DeleteRequestTemplate(actionId[deleteRequestTemplatePrefix.Length..]);
            }

            const string saveStepTemplatePrefix = "save-step-template:";
            if (actionId?.StartsWith(saveStepTemplatePrefix, StringComparison.Ordinal) == true)
            {
                return SaveStepTemplate(input, actionId[saveStepTemplatePrefix.Length..]);
            }

            const string deleteStepTemplatePrefix = "delete-step-template:";
            if (actionId?.StartsWith(deleteStepTemplatePrefix, StringComparison.Ordinal) == true)
            {
                return DeleteStepTemplate(actionId[deleteStepTemplatePrefix.Length..]);
            }

            const string applyStepTemplateProviderPrefix = "apply-step-template-provider:";
            if (actionId?.StartsWith(applyStepTemplateProviderPrefix, StringComparison.Ordinal) == true)
            {
                return ApplyStepTemplateProvider(input, actionId[applyStepTemplateProviderPrefix.Length..]);
            }

            if (string.Equals(actionId, "save-provider", StringComparison.Ordinal))
            {
                return SaveLegacyProvider(input);
            }

            const string savePrefix = "save:";
            if (actionId?.StartsWith(savePrefix, StringComparison.Ordinal) == true)
            {
                return Save(input, actionId[savePrefix.Length..]);
            }

            const string applyProviderPrefix = "apply-provider:";
            if (actionId?.StartsWith(applyProviderPrefix, StringComparison.Ordinal) == true)
            {
                return ApplyCommandProvider(input, actionId[applyProviderPrefix.Length..]);
            }

            const string pickIconPrefix = "pick-icon:";
            if (actionId?.StartsWith(pickIconPrefix, StringComparison.Ordinal) == true)
            {
                return PickCommandIcon(actionId[pickIconPrefix.Length..]);
            }

            const string deletePrefix = "delete:";
            if (actionId?.StartsWith(deletePrefix, StringComparison.Ordinal) == true)
            {
                return Delete(actionId[deletePrefix.Length..]);
            }

            return ShowError("Unknown settings action.");
        }
        catch (JsonException)
        {
            return ShowError("Command settings could not be read.");
        }
        catch (IOException exception)
        {
            return ShowError($"The settings operation failed: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            return ShowError($"The settings operation failed: {exception.Message}");
        }
        catch (Win32Exception exception)
        {
            return ShowError($"The settings file could not be opened: {exception.Message}");
        }
        catch (COMException exception)
        {
            return ShowError($"The Windows file picker failed: {exception.Message}");
        }
    }

    private CommandResult OpenSettingsFile(bool reveal)
    {
        var status = _globalSettingsStore.GetFileStatus();
        if (!status.Exists || status.FilePath is null)
        {
            return ShowError("The settings file has not been created yet. Save a setting first.");
        }

        if (reveal)
        {
            _settingsFileLauncher.Reveal(status.FilePath);
        }
        else
        {
            _settingsFileLauncher.Open(status.FilePath);
        }

        return CommandResult.KeepOpen();
    }

    private CommandResult ImportSettings()
    {
        var settingsDocumentStore = _globalSettingsStore.SettingsDocumentStore;
        if (settingsDocumentStore is null)
        {
            return ShowError("Settings import is unavailable without persistent settings storage.");
        }

        var destinationPath = settingsDocumentStore.ImportFilePath;
        if (destinationPath is null)
        {
            return ShowError("Settings import is unavailable without a settings file location.");
        }

        if (!_settingsFileLauncher.PickAndImportFile(destinationPath))
        {
            return CommandResult.KeepOpen();
        }

        return ReloadSettings();
    }

    private CommandResult ExportSettings()
    {
        var settingsDocumentStore = _globalSettingsStore.SettingsDocumentStore;
        if (settingsDocumentStore is null)
        {
            return ShowError("Settings export is unavailable without persistent settings storage.");
        }

        var destinationPath = _settingsFileLauncher.PickExportFile();
        if (destinationPath is null)
        {
            return CommandResult.KeepOpen();
        }

        settingsDocumentStore.Export(destinationPath);
        return CommandResult.KeepOpen();
    }

    private CommandResult ReloadSettings()
    {
        var settingsDocumentStore = _globalSettingsStore.SettingsDocumentStore;
        if (settingsDocumentStore is null)
        {
            return ShowError("Settings reload is unavailable without persistent settings storage.");
        }

        settingsDocumentStore.Reload();
        ApplyReloadedSettings();
        return CommandResult.KeepOpen();
    }

    private CommandResult ToggleSettingsFormatting()
    {
        var settingsDocumentStore = _globalSettingsStore.SettingsDocumentStore;
        if (settingsDocumentStore is null)
        {
            return ShowError("Settings formatting is unavailable without unified settings storage.");
        }

        settingsDocumentStore.ToggleFormatting();
        Refresh();
        return CommandResult.KeepOpen();
    }

    private void ApplyReloadedSettings()
    {
        _pipelineDrafts.Clear();
        _expandedStepIds.Clear();
        _draftInputs.Clear();
        _expandedCommandIds.Clear();
        _expandedProviderIds.Clear();
        _expandedRequestTemplateIds.Clear();
        _expandedStepTemplateIds.Clear();
        _globalSettingsExpanded = false;
        _store.ReloadFromDocument();
        _providerSettingsStore.ReloadFromDocument();
        _requestTemplateStore.ReloadFromDocument();
        _stepTemplateStore.ReloadFromDocument();
        _globalSettingsStore.ReloadFromDocument();
        _providerSettingsChanged();
        _requestTemplatesChanged();
        _commandsChanged();
        Refresh();
    }

    private CommandResult Add(JsonObject input)
    {
        var commands = _store.GetCommands().Select(command => command.Clone()).ToList();
        commands.Add(new UserCommandDefinition
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = ReadText(input, "newName"),
            OutputFormat = "{}",
            SendDelayMilliseconds = 650,
            EnableAdvancedOutput = false,
            UseGlobalAdvancedOutputSystemPrompt = true,
            ProviderId = _providerSettingsStore.Get().Id,
            Exposure = CommandExposure.FallbackCommand,
            EnableGlobalFallback = true,
        });

        _draftInputs.Remove("newName");
        return ValidateAndSave(commands);
    }

    private CommandResult AddProvider(JsonObject input)
    {
        var providers = _providerSettingsStore.GetProviders()
            .Select(provider => provider.Clone())
            .ToList();
        providers.Add(new LlmProviderSettings
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = ReadText(input, "newProviderName"),
        });

        _draftInputs.Remove("newProviderName");
        return ValidateAndSaveProviders(providers);
    }

    private CommandResult AddRequestTemplate(JsonObject input)
    {
        var templates = _requestTemplateStore.GetTemplates()
            .Select(template => template.Clone())
            .ToList();
        templates.Add(new JsonRequestTemplateDefinition
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = ReadText(input, "newRequestTemplateName"),
            ProviderId = _providerSettingsStore.Get().Id,
            RequestArguments = "{}",
        });

        _draftInputs.Remove("newRequestTemplateName");
        return ValidateAndSaveRequestTemplates(templates);
    }

    private CommandResult AddStepTemplate(JsonObject input)
    {
        var templates = _stepTemplateStore.GetTemplates()
            .Select(template => template.Clone())
            .ToList();
        templates.Add(new StepTemplateDefinition
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = ReadText(input, "newStepTemplateName"),
            Prompt = "{}",
            ProviderId = _providerSettingsStore.Get().Id,
        });

        _draftInputs.Remove("newStepTemplateName");
        return ValidateAndSaveStepTemplates(templates);
    }

    private CommandResult SaveStepTemplate(JsonObject input, string templateId)
    {
        var templates = _stepTemplateStore.GetTemplates()
            .Select(template => template.Clone())
            .ToList();
        var template = templates.FirstOrDefault(item =>
            string.Equals(item.Id, templateId, StringComparison.Ordinal));
        if (template is null)
        {
            return ShowError("The step template no longer exists.");
        }

        var key = $"stepTemplate_{template.Id}_";
        template.Name = ReadText(input, key + "name", template.Name);
        template.Prompt = ReadText(input, key + "prompt", template.Prompt);
        template.ProviderId = ReadText(input, key + "provider", template.ProviderId);
        template.RequestTemplateId = ReadText(input, key + "requestTemplate", template.RequestTemplateId);
        template.CustomRequestArguments = ReadText(input, key + "arguments", template.CustomRequestArguments);
        var requestTemplate = _requestTemplateStore.Get(template.RequestTemplateId);
        if (requestTemplate is null ||
            !string.Equals(requestTemplate.ProviderId, template.ProviderId, StringComparison.Ordinal))
        {
            template.RequestTemplateId = string.Empty;
        }

        return ValidateAndSaveStepTemplates(templates, template.Id);
    }

    private CommandResult DeleteStepTemplate(string templateId)
    {
        var existingTemplates = _stepTemplateStore.GetTemplates();
        var templates = existingTemplates
            .Where(template => !string.Equals(template.Id, templateId, StringComparison.Ordinal))
            .ToArray();
        if (templates.Length == existingTemplates.Count)
        {
            return ShowError("The step template no longer exists.");
        }

        _stepTemplateStore.ReplaceAll(templates);
        _expandedStepTemplateIds.Remove(templateId);
        RemoveDraftInputsForStepTemplate(templateId);
        foreach (var key in _draftInputs.Select(property => property.Key)
            .Where(key => key.StartsWith("newStepSource_", StringComparison.Ordinal) &&
                string.Equals(_draftInputs[key]?.ToString(), $"template:{templateId}", StringComparison.Ordinal))
            .ToArray())
        {
            _draftInputs[key] = string.Empty;
        }

        Refresh();
        return CommandResult.KeepOpen();
    }

    private CommandResult ApplyStepTemplateProvider(JsonObject input, string templateId)
    {
        var template = _stepTemplateStore.Get(templateId);
        if (template is null)
        {
            return ShowError("The step template no longer exists.");
        }

        var key = $"stepTemplate_{template.Id}_";
        var providerId = ReadText(input, key + "provider", template.ProviderId);
        if (_providerSettingsStore.Get(providerId) is null)
        {
            return ShowError($"Step template '{template.Name}' must use an existing LLM provider.");
        }

        var requestTemplateId = ReadText(input, key + "requestTemplate", template.RequestTemplateId);
        var requestTemplate = _requestTemplateStore.Get(requestTemplateId);
        if (requestTemplate is null ||
            !string.Equals(requestTemplate.ProviderId, providerId, StringComparison.Ordinal))
        {
            _draftInputs[key + "requestTemplate"] = string.Empty;
        }

        _expandedStepTemplateIds.Add(templateId);
        Refresh();
        return CommandResult.KeepOpen();
    }

    private CommandResult ValidateAndSaveStepTemplates(
        IReadOnlyList<StepTemplateDefinition> templates,
        string? savedTemplateId = null)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var template in templates)
        {
            template.Name = template.Name.Trim();
            if (string.IsNullOrWhiteSpace(template.Name))
            {
                return ShowError("Every step template must have a name.");
            }

            if (!names.Add(template.Name))
            {
                return ShowError($"Step template names must be unique. '{template.Name}' is used more than once.");
            }

            if (_providerSettingsStore.Get(template.ProviderId) is null)
            {
                return ShowError($"Step template '{template.Name}' must use an existing LLM provider.");
            }

            if (string.IsNullOrWhiteSpace(template.Prompt))
            {
                return ShowError($"Step template '{template.Name}' must have a prompt.");
            }

            if (!OutputFormatter.TryFormat(template.Prompt, string.Empty, out _, out var promptError))
            {
                return ShowError($"Step template '{template.Name}' has an invalid prompt: {promptError}");
            }

            var requestTemplate = _requestTemplateStore.Get(template.RequestTemplateId);
            if (!string.IsNullOrEmpty(template.RequestTemplateId) &&
                (requestTemplate is null ||
                    !string.Equals(requestTemplate.ProviderId, template.ProviderId, StringComparison.Ordinal)))
            {
                return ShowError($"Step template '{template.Name}' must use a JSON request template assigned to the same provider.");
            }

            if (!CustomRequestArguments.TryParse(
                template.CustomRequestArguments,
                out var arguments,
                out var argumentsError))
            {
                return ShowError($"Step template '{template.Name}': {argumentsError}");
            }

            arguments?.Dispose();
        }

        _stepTemplateStore.ReplaceAll(templates);
        if (savedTemplateId is not null)
        {
            _expandedStepTemplateIds.Remove(savedTemplateId);
            RemoveDraftInputsForStepTemplate(savedTemplateId);
        }

        Refresh();
        return CommandResult.KeepOpen();
    }

    private CommandResult SaveRequestTemplate(JsonObject input, string templateId)
    {
        var templates = _requestTemplateStore.GetTemplates()
            .Select(template => template.Clone())
            .ToList();
        var template = templates.FirstOrDefault(item =>
            string.Equals(item.Id, templateId, StringComparison.Ordinal));
        if (template is null)
        {
            return ShowError("The JSON request template no longer exists.");
        }

        template.Name = ReadText(
            input,
            $"requestTemplateName_{template.Id}",
            template.Name);
        template.ProviderId = ReadText(
            input,
            $"requestTemplateProvider_{template.Id}",
            template.ProviderId);
        template.RequestArguments = ReadText(
            input,
            $"requestTemplateArguments_{template.Id}",
            template.RequestArguments);

        var commands = _store.GetCommands().Select(command => command.Clone()).ToList();
        foreach (var command in commands.Where(command =>
            string.Equals(command.RequestTemplateId, template.Id, StringComparison.Ordinal) &&
            !string.Equals(command.ProviderId, template.ProviderId, StringComparison.Ordinal)))
        {
            command.RequestTemplateId = string.Empty;
        }

        foreach (var step in commands.SelectMany(command => command.Steps)
            .Where(step => string.Equals(step.RequestTemplateId, templateId, StringComparison.Ordinal) &&
                !string.Equals(step.ProviderId, template.ProviderId, StringComparison.Ordinal)))
        {
            step.RequestTemplateId = string.Empty;
        }

        var stepTemplates = _stepTemplateStore.GetTemplates().Select(item => item.Clone()).ToList();
        foreach (var stepTemplate in stepTemplates.Where(item =>
            string.Equals(item.RequestTemplateId, templateId, StringComparison.Ordinal) &&
                !string.Equals(item.ProviderId, template.ProviderId, StringComparison.Ordinal)))
        {
            stepTemplate.RequestTemplateId = string.Empty;
        }

        return ValidateAndSaveRequestTemplates(templates, commands, stepTemplates, template.Id);
    }

    private CommandResult DeleteRequestTemplate(string templateId)
    {
        var existingTemplates = _requestTemplateStore.GetTemplates();
        var templates = existingTemplates
            .Where(template => !string.Equals(template.Id, templateId, StringComparison.Ordinal))
            .ToArray();
        if (templates.Length == existingTemplates.Count)
        {
            return ShowError("The JSON request template no longer exists.");
        }

        var commands = _store.GetCommands().Select(command => command.Clone()).ToList();
        foreach (var command in commands.Where(command =>
            string.Equals(command.RequestTemplateId, templateId, StringComparison.Ordinal)))
        {
            command.RequestTemplateId = string.Empty;
        }

        foreach (var step in commands.SelectMany(command => command.Steps)
            .Where(step => string.Equals(step.RequestTemplateId, templateId, StringComparison.Ordinal)))
        {
            step.RequestTemplateId = string.Empty;
        }

        var stepTemplates = _stepTemplateStore.GetTemplates().Select(item => item.Clone()).ToList();
        foreach (var stepTemplate in stepTemplates.Where(item =>
            string.Equals(item.RequestTemplateId, templateId, StringComparison.Ordinal)))
        {
            stepTemplate.RequestTemplateId = string.Empty;
        }

        _requestTemplateStore.ReplaceAll(templates);
        _stepTemplateStore.ReplaceAll(stepTemplates);
        _store.ReplaceAll(commands);
        _expandedRequestTemplateIds.Remove(templateId);
        RemoveDraftInputsForRequestTemplate(templateId);
        _requestTemplatesChanged();
        _commandsChanged();
        Refresh();
        return CommandResult.KeepOpen();
    }

    private CommandResult ValidateAndSaveRequestTemplates(
        List<JsonRequestTemplateDefinition> templates,
        List<UserCommandDefinition>? commands = null,
        List<StepTemplateDefinition>? stepTemplates = null,
        string? savedTemplateId = null)
    {
        var validationError = ValidateRequestTemplates(
            templates,
            _providerSettingsStore.GetProviders());
        if (validationError is not null)
        {
            return ShowError(validationError);
        }

        _requestTemplateStore.ReplaceAll(templates);
        if (stepTemplates is not null)
        {
            _stepTemplateStore.ReplaceAll(stepTemplates);
        }
        if (commands is not null)
        {
            _store.ReplaceAll(commands);
            _commandsChanged();
        }

        if (savedTemplateId is not null)
        {
            _expandedRequestTemplateIds.Remove(savedTemplateId);
            RemoveDraftInputsForRequestTemplate(savedTemplateId);
        }

        _requestTemplatesChanged();
        Refresh();
        return CommandResult.KeepOpen();
    }

    private CommandResult SaveProvider(JsonObject input, string providerId)
    {
        var providers = _providerSettingsStore.GetProviders()
            .Select(provider => provider.Clone())
            .ToList();
        var provider = providers.FirstOrDefault(item =>
            string.Equals(item.Id, providerId, StringComparison.Ordinal));
        if (provider is null)
        {
            return ShowError("The provider no longer exists.");
        }

        var apiKey = ReadText(input, $"providerApiKey_{provider.Id}");
        provider.Name = ReadText(input, $"providerName_{provider.Id}", provider.Name);
        provider.BaseUrl = ReadText(input, $"providerBaseUrl_{provider.Id}", provider.BaseUrl).Trim();
        provider.Model = ReadText(input, $"providerModel_{provider.Id}", provider.Model).Trim();
        if (!string.IsNullOrEmpty(apiKey))
        {
            provider.ApiKey = apiKey;
        }

        return ValidateAndSaveProviders(providers, provider.Id);
    }

    private CommandResult SaveLegacyProvider(JsonObject input)
    {
        var existing = _providerSettingsStore.Get();
        var apiKey = ReadText(input, "providerApiKey");
        var settings = new LlmProviderSettings
        {
            BaseUrl = ReadText(input, "providerBaseUrl", existing.BaseUrl).Trim(),
            Model = ReadText(input, "providerModel", existing.Model).Trim(),
            Id = existing.Id,
            Name = existing.Name,
            ApiKey = string.IsNullOrEmpty(apiKey) ? existing.ApiKey : apiKey,
        };
        if (!LlmProviderSettingsStore.IsValid(settings))
        {
            return ShowError("Provider base URL must be an absolute HTTP or HTTPS URL, and model is required.");
        }

        _providerSettingsStore.Replace(settings);
        _providerSettingsChanged();
        Refresh();
        return CommandResult.KeepOpen();
    }

    private CommandResult ClearProviderApiKey(string providerId)
    {
        var providers = _providerSettingsStore.GetProviders()
            .Select(provider => provider.Clone())
            .ToList();
        var provider = providers.FirstOrDefault(item =>
            string.Equals(item.Id, providerId, StringComparison.Ordinal));
        if (provider is null)
        {
            return ShowError("The provider no longer exists.");
        }

        provider.ApiKey = string.Empty;
        _draftInputs.Remove($"providerApiKey_{provider.Id}");
        _providerSettingsStore.ReplaceAll(providers);
        _providerSettingsChanged();
        Refresh();
        return CommandResult.KeepOpen();
    }

    private CommandResult DeleteProvider(string providerId)
    {
        var providers = _providerSettingsStore.GetProviders();
        if (providers.Count == 1)
        {
            return ShowError("At least one LLM provider is required.");
        }

        if (_store.GetCommands().Any(command =>
            string.Equals(command.ProviderId, providerId, StringComparison.Ordinal) ||
            command.Steps.Any(step => string.Equals(step.ProviderId, providerId, StringComparison.Ordinal))))
        {
            return ShowError("Assign commands to another provider before deleting this provider.");
        }

        if (_stepTemplateStore.GetTemplates().Any(template =>
            string.Equals(template.ProviderId, providerId, StringComparison.Ordinal)))
        {
            return ShowError("Assign step templates to another provider before deleting this provider.");
        }

        _providerSettingsStore.ReplaceAll(providers.Where(provider =>
            !string.Equals(provider.Id, providerId, StringComparison.Ordinal)));
        _requestTemplateStore.ReplaceAll(_requestTemplateStore.GetTemplates().Where(template =>
            !string.Equals(template.ProviderId, providerId, StringComparison.Ordinal)));
        _expandedProviderIds.Remove(providerId);
        RemoveDraftInputsForProvider(providerId);
        _providerSettingsChanged();
        _requestTemplatesChanged();
        Refresh();
        return CommandResult.KeepOpen();
    }

    private CommandResult ValidateAndSaveProviders(
        List<LlmProviderSettings> providers,
        string? savedProviderId = null)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in providers)
        {
            provider.Name = provider.Name.Trim();
            if (!LlmProviderSettingsStore.IsValid(provider))
            {
                return ShowError("Every provider needs a unique name, an absolute HTTP or HTTPS URL, and a model.");
            }

            if (!names.Add(provider.Name))
            {
                return ShowError($"Provider names must be unique. '{provider.Name}' is used more than once.");
            }
        }

        _providerSettingsStore.ReplaceAll(providers);
        if (savedProviderId is not null)
        {
            _expandedProviderIds.Remove(savedProviderId);
            RemoveDraftInputsForProvider(savedProviderId);
        }

        _providerSettingsChanged();
        Refresh();
        return CommandResult.KeepOpen();
    }

    private CommandResult Save(JsonObject input, string commandId)
    {
        input = _draftInputs;
        var existing = _store.GetCommands().FirstOrDefault(command => command.Id == commandId);
        if (existing is not null && input[$"mode_{commandId}"] is not null &&
            ReadText(input, $"mode_{commandId}") != GetPipelineDraft(existing).Mode.ToString())
        {
            EditPipeline($"pipeline:{commandId}:mode");
        }

        var commands = _store.GetCommands().Select(command => command.Clone()).ToList();
        var command = commands.FirstOrDefault(
            command => string.Equals(command.Id, commandId, StringComparison.Ordinal));
        if (command is null)
        {
            return ShowError("The command no longer exists.");
        }

        command.Name = ReadText(input, $"name_{command.Id}", command.Name);
        if (_pipelineDrafts.TryGetValue(commandId, out var pipelineDraft))
        {
            command.Mode = pipelineDraft.Mode;
            command.Steps = pipelineDraft.Steps.Select(step => step.Clone()).ToList();
        }

        ReadStepInputs(command, _draftInputs);
        if (command.Mode == CommandMode.Pipeline)
        {
            var stepError = ValidatePipeline(command);
            if (stepError is not null)
            {
                return ShowError(stepError);
            }
        }
        command.OutputFormat = ReadText(input, $"format_{command.Id}", command.OutputFormat);
        command.CustomRequestArguments = ReadText(
            input,
            $"requestArguments_{command.Id}",
            command.CustomRequestArguments);
        var advancedOutputKey = $"advancedOutput_{command.Id}";
        if (input[advancedOutputKey] is not null)
        {
            command.EnableAdvancedOutput = ReadBoolean(input, advancedOutputKey);
        }

        var globalAdvancedOutputPromptKey = $"globalAdvancedOutputPrompt_{command.Id}";
        if (input[globalAdvancedOutputPromptKey] is not null)
        {
            command.UseGlobalAdvancedOutputSystemPrompt = ReadBoolean(
                input,
                globalAdvancedOutputPromptKey);
        }

        var delayKey = $"delay_{command.Id}";
        if (input[delayKey] is not null)
        {
            if (!TryReadInteger(input, delayKey, out var delay))
            {
                return ShowError($"Command '{command.Name}' must have a whole-number send delay.");
            }

            command.SendDelayMilliseconds = delay;
        }

        var providerKey = $"provider_{command.Id}";
        command.ProviderId = ReadText(input, providerKey, command.ProviderId);
        if (_providerSettingsStore.Get(command.ProviderId) is null)
        {
            return ShowError($"Command '{command.Name}' must use an existing LLM provider.");
        }

        var requestTemplateKey = $"requestTemplate_{command.Id}";
        var requestTemplateId = ReadText(
            input,
            requestTemplateKey,
            command.RequestTemplateId);
        var requestTemplate = string.IsNullOrEmpty(requestTemplateId)
            ? null
            : _requestTemplateStore.Get(requestTemplateId);
        command.RequestTemplateId = requestTemplate is not null && string.Equals(
            requestTemplate.ProviderId,
            command.ProviderId,
            StringComparison.Ordinal)
                ? requestTemplate.Id
                : string.Empty;

        var fallbackKey = $"fallback_{command.Id}";
        if (input[fallbackKey] is not null)
        {
            command.EnableGlobalFallback = ReadBoolean(input, fallbackKey);
            command.Exposure = command.EnableGlobalFallback
                ? CommandExposure.FallbackCommand
                : CommandExposure.None;
        }

        var iconKey = $"icon_{command.Id}";
        var storedIconPath = command.IconPath;
        if (input[iconKey] is not null &&
            !_iconStore.TryImport(
                command.Id,
                ReadText(input, iconKey),
                command.IconPath,
                out storedIconPath,
                out var iconError))
        {
            return ShowError($"Command '{command.Name}': {iconError}");
        }
        else if (input[iconKey] is not null)
        {
            command.IconPath = storedIconPath;
        }

        var validationError = Validate(commands);
        if (validationError is not null)
        {
            return ShowError(validationError);
        }

        _store.ReplaceAll(commands);
        _expandedCommandIds.Remove(commandId);
        RemoveDraftInputsForCommand(commandId);
        _commandsChanged();
        Refresh();
        return CommandResult.KeepOpen();
    }

    private CommandResult ApplyCommandProvider(JsonObject input, string commandId)
    {
        var command = _store.GetCommands().FirstOrDefault(command =>
            string.Equals(command.Id, commandId, StringComparison.Ordinal));
        if (command is null)
        {
            return ShowError("The command no longer exists.");
        }

        var providerId = ReadText(input, $"provider_{command.Id}", command.ProviderId);
        if (_providerSettingsStore.Get(providerId) is null)
        {
            return ShowError($"Command '{command.Name}' must use an existing LLM provider.");
        }

        var requestTemplateKey = $"requestTemplate_{command.Id}";
        var requestTemplateId = ReadText(
            input,
            requestTemplateKey,
            command.RequestTemplateId);
        var requestTemplate = string.IsNullOrEmpty(requestTemplateId)
            ? null
            : _requestTemplateStore.Get(requestTemplateId);
        if (requestTemplate is null || !string.Equals(
            requestTemplate.ProviderId,
            providerId,
            StringComparison.Ordinal))
        {
            _draftInputs[requestTemplateKey] = string.Empty;
        }

        _expandedCommandIds.Add(commandId);
        Refresh();
        return CommandResult.KeepOpen();
    }

    private CommandResult PickCommandIcon(string commandId)
    {
        if (!_store.GetCommands().Any(command =>
            string.Equals(command.Id, commandId, StringComparison.Ordinal)))
        {
            return ShowError("The command no longer exists.");
        }

        var iconPath = _settingsFileLauncher.PickIconFile();
        if (iconPath is not null)
        {
            _draftInputs[$"icon_{commandId}"] = iconPath;
            Refresh();
        }

        return CommandResult.KeepOpen();
    }

    private CommandResult ValidateAndSave(List<UserCommandDefinition> commands)
    {

        var validationError = Validate(commands);
        if (validationError is not null)
        {
            return ShowError(validationError);
        }

        _store.ReplaceAll(commands);
        _commandsChanged();
        Refresh();

        return CommandResult.KeepOpen();
    }

    private CommandResult Delete(string commandId)
    {
        var existingCommands = _store.GetCommands();
        var commands = existingCommands
            .Where(command => !string.Equals(command.Id, commandId, StringComparison.Ordinal))
            .ToArray();

        if (commands.Length == existingCommands.Count)
        {
            return ShowError("The command no longer exists.");
        }

        _store.ReplaceAll(commands);
        _expandedCommandIds.Remove(commandId);
        RemoveDraftInputsForCommand(commandId);
        _commandsChanged();
        Refresh();
        return CommandResult.KeepOpen();
    }

    private CommandResult ShowError(string message)
    {
        Refresh(message);
        return CommandResult.KeepOpen();
    }

    private void Refresh(string? errorMessage = null)
    {
        TemplateJson = BuildCard(
            _globalSettingsStore.Get(),
            _globalSettingsStore.GetFileStatus(),
            _providerSettingsStore.GetProviders(),
            _requestTemplateStore.GetTemplates(),
            _stepTemplateStore.GetTemplates(),
            _store.GetCommands().Select(GetPipelineDraft).ToArray(),
            _globalSettingsExpanded,
            _expandedProviderIds,
            _expandedRequestTemplateIds,
            _expandedStepTemplateIds,
            _expandedCommandIds,
            _draftInputs,
            errorMessage).ToJsonString();
        DataJson = "{}";
        StateJson = "{}";
    }

    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "The card builder adds only primitive values and explicit JsonNode instances.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "The card builder adds only primitive values and explicit JsonNode instances.")]
    private JsonObject BuildCard(
        GlobalSettings globalSettings,
        SettingsFileStatus settingsFileStatus,
        IReadOnlyList<LlmProviderSettings> providers,
        IReadOnlyList<JsonRequestTemplateDefinition> requestTemplates,
        IReadOnlyList<StepTemplateDefinition> stepTemplates,
        UserCommandDefinition[] commands,
        bool expandedGlobalSettings,
        HashSet<string> expandedProviderIds,
        HashSet<string> expandedRequestTemplateIds,
        HashSet<string> expandedStepTemplateIds,
        HashSet<string> expandedCommandIds,
        JsonObject draftInputs,
        string? errorMessage)
    {
        var body = new JsonArray
        {
            new JsonObject
            {
                ["type"] = "TextBlock",
                ["text"] = "Global",
                ["size"] = "Large",
                ["weight"] = "Bolder",
            },
            BuildSettingsFileActions(settingsFileStatus),
            BuildSeparator(isSectionSeparator: true),
            new JsonObject
            {
                ["type"] = "TextBlock",
                ["text"] = "LLM providers",
                ["size"] = "Large",
                ["weight"] = "Bolder",
            },
            new JsonObject
            {
                ["type"] = "TextBlock",
                ["text"] = "Connect one or more OpenAI-compatible chat-completions APIs, including llama.cpp servers.",
                ["wrap"] = true,
            },
        };

        foreach (var provider in providers)
        {
            body.Add(BuildProviderEditor(
                provider,
                providers.Count > 1,
                expandedProviderIds.Contains(provider.Id),
                draftInputs));
        }

        body.Add(BuildSeparator());
        body.Add(new JsonObject
        {
            ["type"] = "TextBlock",
            ["text"] = "Add an LLM provider",
            ["weight"] = "Bolder",
        });
        body.Add(BuildTextInput(
            "newProviderName",
            "Provider name",
            string.Empty,
            "For example: Local llama.cpp",
            isRequired: false));
        body.Add(new JsonObject
        {
            ["type"] = "ActionSet",
            ["actions"] = new JsonArray
            {
                BuildSubmitAction("Add provider", "add-provider"),
            },
        });

        body.Add(BuildSeparator(isSectionSeparator: true));
        body.Add(new JsonObject
        {
            ["type"] = "TextBlock",
            ["text"] = "JSON Request Templates",
            ["size"] = "Large",
            ["weight"] = "Bolder",
        });
        body.Add(new JsonObject
        {
            ["type"] = "TextBlock",
            ["text"] = "Reuse provider-specific JSON request fields across commands. Command-specific request arguments override the selected template.",
            ["wrap"] = true,
        });

        if (requestTemplates.Count == 0)
        {
            body.Add(new JsonObject
            {
                ["type"] = "TextBlock",
                ["text"] = "No JSON request templates have been created yet.",
                ["isSubtle"] = true,
                ["wrap"] = true,
            });
        }

        foreach (var requestTemplate in requestTemplates)
        {
            body.Add(BuildRequestTemplateEditor(
                requestTemplate,
                providers,
                expandedRequestTemplateIds.Contains(requestTemplate.Id),
                draftInputs));
        }

        body.Add(BuildSeparator());
        body.Add(new JsonObject
        {
            ["type"] = "TextBlock",
            ["text"] = "Add a JSON request template",
            ["weight"] = "Bolder",
        });
        body.Add(BuildTextInput(
            "newRequestTemplateName",
            "Template name",
            string.Empty,
            "For example: Creative responses",
            isRequired: false));
        body.Add(new JsonObject
        {
            ["type"] = "ActionSet",
            ["actions"] = new JsonArray
            {
                BuildSubmitAction("Add template", "add-request-template"),
            },
        });

        body.Add(BuildSeparator(isSectionSeparator: true));
        body.Add(new JsonObject
        {
            ["type"] = "TextBlock",
            ["text"] = "Step templates",
            ["size"] = "Large",
            ["weight"] = "Bolder",
        });
        body.Add(new JsonObject
        {
            ["type"] = "TextBlock",
            ["text"] = "Reuse user-step settings when building pipelines. Creating a step copies the template; later template changes do not affect it.",
            ["wrap"] = true,
        });

        if (stepTemplates.Count == 0)
        {
            body.Add(new JsonObject
            {
                ["type"] = "TextBlock",
                ["text"] = "No step templates have been created yet.",
                ["isSubtle"] = true,
                ["wrap"] = true,
            });
        }

        foreach (var stepTemplate in stepTemplates)
        {
            body.Add(BuildStepTemplateEditor(
                stepTemplate,
                providers,
                requestTemplates,
                expandedStepTemplateIds.Contains(stepTemplate.Id),
                draftInputs));
        }

        body.Add(BuildSeparator());
        body.Add(new JsonObject
        {
            ["type"] = "TextBlock",
            ["text"] = "Add a step template",
            ["weight"] = "Bolder",
        });
        body.Add(BuildTextInput(
            "newStepTemplateName",
            "Template and step name",
            string.Empty,
            "For example: Translate",
            isRequired: false));
        body.Add(new JsonObject
        {
            ["type"] = "ActionSet",
            ["actions"] = new JsonArray
            {
                BuildSubmitAction("Add step template", "add-step-template"),
            },
        });

        body.Add(BuildSeparator(isSectionSeparator: true));
        body.Add(new JsonObject
        {
            ["type"] = "TextBlock",
            ["text"] = "Commands",
            ["size"] = "Large",
            ["weight"] = "Bolder",
        });
        body.Add(new JsonObject
        {
            ["type"] = "TextBlock",
            ["text"] = "Every command remains available in Command Palette's Commands list for aliases and alias activation. Enable fallback separately for commands that should process fallback queries.",
            ["wrap"] = true,
        });

        if (commands.Length == 0)
        {
            body.Add(new JsonObject
            {
                ["type"] = "TextBlock",
                ["text"] = "No commands have been created yet.",
                ["isSubtle"] = true,
                ["wrap"] = true,
            });
        }

        foreach (var command in commands)
        {
            body.Add(BuildCommandEditor(
                command,
                providers,
                requestTemplates,
                stepTemplates,
                expandedCommandIds.Contains(command.Id),
                draftInputs));
        }

        body.Add(BuildSeparator());
        body.Add(new JsonObject
        {
            ["type"] = "TextBlock",
            ["text"] = "Add a command",
            ["weight"] = "Bolder",
        });
        body.Add(BuildTextInput(
            "newName",
            "Command name",
            string.Empty,
            "For example: Greeting",
            isRequired: false));
        body.Add(new JsonObject
        {
            ["type"] = "ActionSet",
            ["actions"] = new JsonArray
            {
                BuildSubmitAction("Add command", "add"),
            },
        });

        body.Add(BuildSeparator(isSectionSeparator: true));
        body.Add(new JsonObject
        {
            ["type"] = "TextBlock",
            ["text"] = "System steps",
            ["size"] = "Large",
            ["weight"] = "Bolder",
        });
        body.Add(BuildAdvancedOutputSystemStepEditor(
            globalSettings,
            expandedGlobalSettings,
            draftInputs));

        if (!string.IsNullOrEmpty(errorMessage))
        {
            body.Add(new JsonObject
            {
                ["type"] = "TextBlock",
                ["text"] = errorMessage,
                ["color"] = "Attention",
                ["wrap"] = true,
            });
        }

        return new JsonObject
        {
            ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json",
            ["type"] = "AdaptiveCard",
            ["version"] = "1.5",
            ["body"] = body,
        };
    }

    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "The card builder adds only primitive values and explicit JsonNode instances.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "The card builder adds only primitive values and explicit JsonNode instances.")]
    private static JsonObject BuildAdvancedOutputSystemStepEditor(
        GlobalSettings settings,
        bool isExpanded,
        JsonObject draftInputs)
    {
        var prompt = settings.AdvancedOutputSystemPrompt;
        var status = string.IsNullOrWhiteSpace(prompt)
            ? "Disabled"
            : string.Equals(
                prompt,
                GlobalSettings.DefaultAdvancedOutputSystemPrompt,
                StringComparison.Ordinal)
                ? "Default"
                : "Customized";

        return new JsonObject
        {
            ["type"] = "Container",
            ["id"] = "system_step_advanced_output",
            ["style"] = "emphasis",
            ["spacing"] = "Medium",
            ["items"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "Container",
                    ["id"] = "global_display",
                    ["isVisible"] = !isExpanded,
                    ["items"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "ColumnSet",
                            ["columns"] = new JsonArray
                            {
                                new JsonObject
                                {
                                    ["type"] = "Column",
                                    ["width"] = "stretch",
                                    ["items"] = new JsonArray
                                    {
                                        new JsonObject
                                        {
                                            ["type"] = "TextBlock",
                                            ["text"] = "Advanced output format · System step",
                                            ["weight"] = "Bolder",
                                            ["wrap"] = true,
                                        },
                                        new JsonObject
                                        {
                                            ["type"] = "TextBlock",
                                            ["text"] = status,
                                            ["isSubtle"] = true,
                                            ["spacing"] = "Small",
                                        },
                                    },
                                },
                                new JsonObject
                                {
                                    ["type"] = "Column",
                                    ["width"] = "auto",
                                    ["verticalContentAlignment"] = "Center",
                                    ["items"] = new JsonArray
                                    {
                                        new JsonObject
                                        {
                                            ["type"] = "ActionSet",
                                            ["horizontalAlignment"] = "Right",
                                            ["actions"] = new JsonArray
                                            {
                                                BuildSubmitAction(
                                                    "✏",
                                                    "edit-global",
                                                    "Edit shared system-step prompt",
                                                    associatedInputs: "none"),
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
                new JsonObject
                {
                    ["type"] = "Container",
                    ["id"] = "global_editor",
                    ["isVisible"] = isExpanded,
                    ["items"] = new JsonArray
                    {
                        BuildTextInput(
                            "advancedOutputSystemPrompt",
                            "Advanced output system prompt",
                            ReadText(draftInputs, "advancedOutputSystemPrompt", prompt),
                            "Leave empty to disable automatic prompt injection.",
                            isRequired: false,
                            isMultiline: true),
                        new JsonObject
                        {
                            ["type"] = "TextBlock",
                            ["text"] = "This built-in step cannot be deleted. Its system prompt is shared by pipelines that include Advanced output format and by default-mode commands with both advanced output and Use global system prompt enabled. An empty value disables injection. In Pipeline mode, the advanced-output toggle controls parsing only; the formatting step sends the prompt.",
                            ["isSubtle"] = true,
                            ["wrap"] = true,
                            ["spacing"] = "Small",
                        },
                        new JsonObject
                        {
                            ["type"] = "ActionSet",
                            ["horizontalAlignment"] = "Right",
                            ["actions"] = new JsonArray
                            {
                                BuildSubmitAction(
                                    "Reset to default",
                                    "reset-global",
                                    associatedInputs: "none"),
                                BuildSubmitAction("✓", "save-global", "Save shared system-step prompt"),
                                BuildSubmitAction(
                                    "✕",
                                    "cancel-global",
                                    "Cancel editing",
                                    associatedInputs: "none"),
                            },
                        },
                    },
                },
            },
        };
    }

    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "The card builder adds only primitive values and explicit JsonNode instances.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "The card builder adds only primitive values and explicit JsonNode instances.")]
    private static JsonObject BuildSettingsFileActions(SettingsFileStatus status)
    {
        var statusText = status.Exists && status.LastModified is not null
            ? $"✓ Created · Last modified: {status.LastModified:yyyy-MM-dd HH:mm:ss}"
            : "⚠ Not created yet";
        return new JsonObject
        {
            ["type"] = "Container",
            ["style"] = "emphasis",
            ["spacing"] = "Medium",
            ["items"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "TextBlock",
                    ["text"] = "Settings",
                    ["weight"] = "Bolder",
                    ["wrap"] = true,
                },
                new JsonObject
                {
                    ["type"] = "TextBlock",
                    ["text"] = "Contains global settings, LLM providers, and commands.",
                    ["isSubtle"] = true,
                    ["spacing"] = "Small",
                    ["wrap"] = true,
                },
                new JsonObject
                {
                    ["type"] = "TextBlock",
                    ["text"] = statusText,
                    ["color"] = status.Exists ? "Good" : "Warning",
                    ["spacing"] = "Small",
                    ["wrap"] = true,
                },
                new JsonObject
                {
                    ["type"] = "ActionSet",
                    ["actions"] = new JsonArray
                    {
                        BuildSubmitAction(
                            "📂 Reveal",
                            "reveal-settings-file",
                            status.Exists
                                ? "Reveal the current settings.json file in the default file explorer."
                                : null,
                            associatedInputs: "none",
                            isEnabled: status.Exists),
                        BuildSubmitAction(
                            "✎ Edit",
                            "open-settings-file",
                            status.Exists
                                ? "Edit the current settings.json file in the default text editor."
                                : null,
                            associatedInputs: "none",
                            isEnabled: status.Exists),
                        BuildSubmitAction(
                            "⇩ Import",
                            "import-settings",
                            "Select a settings.json file to import and overwrite the current settings.",
                            associatedInputs: "none"),
                    },
                },
                new JsonObject
                {
                    ["type"] = "ActionSet",
                    ["spacing"] = "Small",
                    ["actions"] = new JsonArray
                    {
                        BuildSubmitAction(
                            "⇧ Export",
                            "export-settings",
                            status.Exists
                                ? "Export the current settings so they can be restored later with Import."
                                : null,
                            associatedInputs: "none",
                            isEnabled: status.Exists),
                        BuildSubmitAction(
                            "↻ Reload",
                            "reload-settings",
                            status.Exists
                                ? "Reload settings from the current settings.json file."
                                : "Look for settings.json in the extension settings folder and load it.",
                            associatedInputs: "none"),
                        BuildSubmitAction(
                            status.BeautifulFormatting
                                ? "Format compactly"
                                : "Format beautifully",
                            "toggle-settings-format",
                            status.Exists
                                ? status.BeautifulFormatting
                                    ? "Rewrite settings.json in compact form."
                                    : "Rewrite settings.json with four spaces per indentation level."
                                : null,
                            associatedInputs: "none",
                            isEnabled: status.Exists),
                    },
                },
                new JsonObject
                {
                    ["type"] = "TextBlock",
                    ["text"] = "⚠ Manual changes to settings.json are not detected automatically. Select Reload to load them.",
                    ["color"] = "Warning",
                    ["spacing"] = "Medium",
                    ["wrap"] = true,
                },
                new JsonObject
                {
                    ["type"] = "TextBlock",
                    ["text"] = "⚠ Due to Command Palette settings behavior, completely close this extension settings tab after reloading for changes to take effect. For example, open Gallery or select Installed again to return to the extensions list.",
                    ["color"] = "Warning",
                    ["spacing"] = "Small",
                    ["wrap"] = true,
                },
            },
        };
    }

    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "The card builder adds only primitive values and explicit JsonNode instances.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "The card builder adds only primitive values and explicit JsonNode instances.")]
    private static JsonObject BuildProviderEditor(
        LlmProviderSettings provider,
        bool canDelete,
        bool isExpanded,
        JsonObject draftInputs)
    {
        var displayId = $"provider_display_{provider.Id}";
        var editorId = $"provider_editor_{provider.Id}";
        var actions = new JsonArray
        {
            BuildSubmitAction(
                "✏",
                $"edit-provider:{provider.Id}",
                "Edit provider"),
        };
        if (canDelete)
        {
            actions.Add(BuildSubmitAction(
                "✕",
                $"delete-provider:{provider.Id}",
                "Delete provider",
                associatedInputs: "none",
                style: "destructive"));
        }

        return new JsonObject
        {
            ["type"] = "Container",
            ["style"] = "emphasis",
            ["spacing"] = "Medium",
            ["items"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "Container",
                    ["id"] = displayId,
                    ["isVisible"] = !isExpanded,
                    ["items"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "ColumnSet",
                            ["columns"] = new JsonArray
                            {
                                new JsonObject
                                {
                                    ["type"] = "Column",
                                    ["width"] = "stretch",
                                    ["items"] = new JsonArray
                                    {
                                        new JsonObject
                                        {
                                            ["type"] = "TextBlock",
                                            ["text"] = provider.Name,
                                            ["weight"] = "Bolder",
                                            ["wrap"] = true,
                                        },
                                        new JsonObject
                                        {
                                            ["type"] = "TextBlock",
                                            ["text"] = $"{provider.Model} · {provider.BaseUrl}",
                                            ["isSubtle"] = true,
                                            ["spacing"] = "Small",
                                            ["wrap"] = true,
                                        },
                                    },
                                },
                                new JsonObject
                                {
                                    ["type"] = "Column",
                                    ["width"] = "auto",
                                    ["verticalContentAlignment"] = "Center",
                                    ["items"] = new JsonArray
                                    {
                                        new JsonObject
                                        {
                                            ["type"] = "ActionSet",
                                            ["horizontalAlignment"] = "Right",
                                            ["actions"] = actions,
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
                new JsonObject
                {
                    ["type"] = "Container",
                    ["id"] = editorId,
                    ["isVisible"] = isExpanded,
                    ["items"] = new JsonArray
                    {
                        BuildTextInput($"providerName_{provider.Id}", "Provider name", ReadText(draftInputs, $"providerName_{provider.Id}", provider.Name), string.Empty),
                        BuildTextInput($"providerBaseUrl_{provider.Id}", "API base URL", ReadText(draftInputs, $"providerBaseUrl_{provider.Id}", provider.BaseUrl), "http://127.0.0.1:8080/v1"),
                        BuildTextInput($"providerModel_{provider.Id}", "Model", ReadText(draftInputs, $"providerModel_{provider.Id}", provider.Model), "local-model"),
                        BuildPasswordInput(
                            $"providerApiKey_{provider.Id}",
                            "API key (optional)",
                            string.IsNullOrEmpty(provider.ApiKey)
                                ? "Optional bearer token"
                                : "Saved; leave blank to keep it",
                            ReadText(draftInputs, $"providerApiKey_{provider.Id}")),
                        new JsonObject
                        {
                            ["type"] = "TextBlock",
                            ["text"] = "Prompts are sent directly to this provider. Its operator may process or retain them under its own terms and privacy policy. API keys are protected for your Windows account.",
                            ["isSubtle"] = true,
                            ["wrap"] = true,
                            ["spacing"] = "Small",
                        },
                        new JsonObject
                        {
                            ["type"] = "ActionSet",
                            ["horizontalAlignment"] = "Right",
                            ["actions"] = new JsonArray
                            {
                                BuildSubmitAction("Clear API key", $"clear-provider-key:{provider.Id}"),
                                BuildSubmitAction("✓", $"save-provider:{provider.Id}", "Save provider"),
                                BuildSubmitAction(
                                    "✕",
                                    $"cancel-provider:{provider.Id}",
                                    "Cancel editing"),
                            },
                        },
                    },
                },
            },
        };
    }

    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "The card builder adds only primitive values and explicit JsonNode instances.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "The card builder adds only primitive values and explicit JsonNode instances.")]
    private static JsonObject BuildRequestTemplateEditor(
        JsonRequestTemplateDefinition requestTemplate,
        IReadOnlyList<LlmProviderSettings> providers,
        bool isExpanded,
        JsonObject draftInputs)
    {
        var displayId = $"request_template_display_{requestTemplate.Id}";
        var editorId = $"request_template_editor_{requestTemplate.Id}";
        return new JsonObject
        {
            ["type"] = "Container",
            ["style"] = "emphasis",
            ["spacing"] = "Medium",
            ["items"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "Container",
                    ["id"] = displayId,
                    ["isVisible"] = !isExpanded,
                    ["items"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "ColumnSet",
                            ["columns"] = new JsonArray
                            {
                                new JsonObject
                                {
                                    ["type"] = "Column",
                                    ["width"] = "stretch",
                                    ["items"] = new JsonArray
                                    {
                                        new JsonObject
                                        {
                                            ["type"] = "TextBlock",
                                            ["text"] = requestTemplate.Name,
                                            ["weight"] = "Bolder",
                                            ["wrap"] = true,
                                        },
                                        new JsonObject
                                        {
                                            ["type"] = "TextBlock",
                                            ["text"] = GetProviderName(requestTemplate.ProviderId, providers),
                                            ["isSubtle"] = true,
                                            ["spacing"] = "Small",
                                        },
                                        new JsonObject
                                        {
                                            ["type"] = "TextBlock",
                                            ["text"] = requestTemplate.RequestArguments,
                                            ["fontType"] = "Monospace",
                                            ["isSubtle"] = true,
                                            ["spacing"] = "Small",
                                            ["wrap"] = true,
                                        },
                                    },
                                },
                                new JsonObject
                                {
                                    ["type"] = "Column",
                                    ["width"] = "auto",
                                    ["verticalContentAlignment"] = "Center",
                                    ["items"] = new JsonArray
                                    {
                                        new JsonObject
                                        {
                                            ["type"] = "ActionSet",
                                            ["horizontalAlignment"] = "Right",
                                            ["actions"] = new JsonArray
                                            {
                                                BuildSubmitAction(
                                                    "✏",
                                                    $"edit-request-template:{requestTemplate.Id}",
                                                    "Edit JSON request template"),
                                                BuildSubmitAction(
                                                    "✕",
                                                    $"delete-request-template:{requestTemplate.Id}",
                                                    "Delete JSON request template",
                                                    associatedInputs: "none",
                                                    style: "destructive"),
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
                new JsonObject
                {
                    ["type"] = "Container",
                    ["id"] = editorId,
                    ["isVisible"] = isExpanded,
                    ["items"] = new JsonArray
                    {
                        BuildTextInput(
                            $"requestTemplateName_{requestTemplate.Id}",
                            "Template name",
                            ReadText(
                                draftInputs,
                                $"requestTemplateName_{requestTemplate.Id}",
                                requestTemplate.Name),
                            string.Empty),
                        BuildChoiceInput(
                            $"requestTemplateProvider_{requestTemplate.Id}",
                            "LLM provider",
                            ReadText(
                                draftInputs,
                                $"requestTemplateProvider_{requestTemplate.Id}",
                                requestTemplate.ProviderId),
                            providers.Select(provider => (provider.Name, provider.Id))),
                        BuildTextInput(
                            $"requestTemplateArguments_{requestTemplate.Id}",
                            "JSON request object",
                            ReadText(
                                draftInputs,
                                $"requestTemplateArguments_{requestTemplate.Id}",
                                requestTemplate.RequestArguments),
                            "{\"temperature\":0.7}",
                            isMultiline: true),
                        new JsonObject
                        {
                            ["type"] = "TextBlock",
                            ["text"] = "Enter a static JSON object. The protected model and messages fields are not allowed.",
                            ["isSubtle"] = true,
                            ["wrap"] = true,
                            ["spacing"] = "Small",
                        },
                        new JsonObject
                        {
                            ["type"] = "ActionSet",
                            ["horizontalAlignment"] = "Right",
                            ["actions"] = new JsonArray
                            {
                                BuildSubmitAction(
                                    "✓",
                                    $"save-request-template:{requestTemplate.Id}",
                                    "Save JSON request template"),
                                BuildSubmitAction(
                                    "✕",
                                    $"cancel-request-template:{requestTemplate.Id}",
                                    "Cancel editing"),
                            },
                        },
                    },
                },
            },
        };
    }

    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "The card builder adds only primitive values and explicit JsonNode instances.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "The card builder adds only primitive values and explicit JsonNode instances.")]
    private static JsonObject BuildStepTemplateEditor(
        StepTemplateDefinition stepTemplate,
        IReadOnlyList<LlmProviderSettings> providers,
        IReadOnlyList<JsonRequestTemplateDefinition> requestTemplates,
        bool isExpanded,
        JsonObject draftInputs)
    {
        var key = $"stepTemplate_{stepTemplate.Id}_";
        var providerId = ReadText(draftInputs, key + "provider", stepTemplate.ProviderId);
        var matchingRequestTemplates = requestTemplates
            .Where(template => string.Equals(template.ProviderId, providerId, StringComparison.Ordinal))
            .ToArray();
        var requestTemplateId = ReadText(
            draftInputs,
            key + "requestTemplate",
            stepTemplate.RequestTemplateId);
        if (!matchingRequestTemplates.Any(template =>
            string.Equals(template.Id, requestTemplateId, StringComparison.Ordinal)))
        {
            requestTemplateId = string.Empty;
        }

        return new JsonObject
        {
            ["type"] = "Container",
            ["style"] = "emphasis",
            ["spacing"] = "Medium",
            ["items"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "Container",
                    ["id"] = $"step_template_display_{stepTemplate.Id}",
                    ["isVisible"] = !isExpanded,
                    ["items"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "ColumnSet",
                            ["columns"] = new JsonArray
                            {
                                new JsonObject
                                {
                                    ["type"] = "Column",
                                    ["width"] = "stretch",
                                    ["items"] = new JsonArray
                                    {
                                        new JsonObject
                                        {
                                            ["type"] = "TextBlock",
                                            ["text"] = stepTemplate.Name,
                                            ["weight"] = "Bolder",
                                            ["wrap"] = true,
                                        },
                                        new JsonObject
                                        {
                                            ["type"] = "TextBlock",
                                            ["text"] = $"{GetProviderName(stepTemplate.ProviderId, providers)} · {GetRequestTemplateName(stepTemplate.RequestTemplateId, requestTemplates)}",
                                            ["isSubtle"] = true,
                                            ["spacing"] = "Small",
                                            ["wrap"] = true,
                                        },
                                        new JsonObject
                                        {
                                            ["type"] = "TextBlock",
                                            ["text"] = stepTemplate.Prompt,
                                            ["fontType"] = "Monospace",
                                            ["isSubtle"] = true,
                                            ["spacing"] = "Small",
                                            ["wrap"] = true,
                                        },
                                    },
                                },
                                new JsonObject
                                {
                                    ["type"] = "Column",
                                    ["width"] = "auto",
                                    ["verticalContentAlignment"] = "Center",
                                    ["items"] = new JsonArray
                                    {
                                        new JsonObject
                                        {
                                            ["type"] = "ActionSet",
                                            ["actions"] = new JsonArray
                                            {
                                                BuildSubmitAction("✏", $"edit-step-template:{stepTemplate.Id}", "Edit step template"),
                                                BuildSubmitAction(
                                                    "✕",
                                                    $"delete-step-template:{stepTemplate.Id}",
                                                    "Delete step template",
                                                    associatedInputs: "none",
                                                    style: "destructive"),
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
                new JsonObject
                {
                    ["type"] = "Container",
                    ["id"] = $"step_template_editor_{stepTemplate.Id}",
                    ["isVisible"] = isExpanded,
                    ["items"] = new JsonArray
                    {
                        BuildTextInput(
                            key + "name",
                            "Template and step name",
                            ReadText(draftInputs, key + "name", stepTemplate.Name),
                            string.Empty),
                        BuildTextInput(
                            key + "prompt",
                            "Prompt format",
                            ReadText(draftInputs, key + "prompt", stepTemplate.Prompt),
                            "Use {} for input, {{ for {, and }} for }.",
                            isMultiline: true),
                        BuildChoiceInput(
                            key + "provider",
                            "LLM provider",
                            providerId,
                            providers.Select(provider => (provider.Name, provider.Id))),
                        new JsonObject
                        {
                            ["type"] = "ActionSet",
                            ["actions"] = new JsonArray(BuildSubmitAction(
                                "Apply provider",
                                $"apply-step-template-provider:{stepTemplate.Id}")),
                        },
                        BuildChoiceInput(
                            key + "requestTemplate",
                            "JSON request template",
                            requestTemplateId,
                            new[] { ("None", string.Empty) }.Concat(
                                matchingRequestTemplates.Select(template => (template.Name, template.Id)))),
                        BuildTextInput(
                            key + "arguments",
                            "Custom request arguments (optional)",
                            ReadText(draftInputs, key + "arguments", stepTemplate.CustomRequestArguments),
                            "{\"temperature\":0.7}",
                            isRequired: false,
                            isMultiline: true),
                        new JsonObject
                        {
                            ["type"] = "ActionSet",
                            ["horizontalAlignment"] = "Right",
                            ["actions"] = new JsonArray
                            {
                                BuildSubmitAction("✓", $"save-step-template:{stepTemplate.Id}", "Save step template"),
                                BuildSubmitAction("✕", $"cancel-step-template:{stepTemplate.Id}", "Cancel editing"),
                            },
                        },
                    },
                },
            },
        };
    }

    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "The card builder adds only primitive values and explicit JsonNode instances.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "The card builder adds only primitive values and explicit JsonNode instances.")]
    private JsonObject BuildCommandEditor(
        UserCommandDefinition command,
        IReadOnlyList<LlmProviderSettings> providers,
        IReadOnlyList<JsonRequestTemplateDefinition> requestTemplates,
        IReadOnlyList<StepTemplateDefinition> stepTemplates,
        bool isExpanded,
        JsonObject draftInputs)
    {
        var displayId = $"display_{command.Id}";
        var editorId = $"editor_{command.Id}";
        var draftProviderId = ReadText(
            draftInputs,
            $"provider_{command.Id}",
            command.ProviderId);
        var matchingTemplates = requestTemplates
            .Where(template => string.Equals(
                template.ProviderId,
                draftProviderId,
                StringComparison.Ordinal))
            .ToArray();
        var draftRequestTemplateId = ReadText(
            draftInputs,
            $"requestTemplate_{command.Id}",
            command.RequestTemplateId);
        if (!matchingTemplates.Any(template => string.Equals(
            template.Id,
            draftRequestTemplateId,
            StringComparison.Ordinal)))
        {
            draftRequestTemplateId = string.Empty;
        }

        var card = new JsonObject
        {
            ["type"] = "Container",
            ["style"] = "emphasis",
            ["spacing"] = "Medium",
            ["items"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "Container",
                    ["id"] = displayId,
                    ["isVisible"] = !isExpanded,
                    ["items"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "ColumnSet",
                            ["columns"] = new JsonArray
                            {
                                new JsonObject
                                {
                                    ["type"] = "Column",
                                    ["width"] = "stretch",
                                    ["items"] = new JsonArray
                                    {
                                        new JsonObject
                                        {
                                            ["type"] = "TextBlock",
                                            ["text"] = command.Name,
                                            ["weight"] = "Bolder",
                                            ["wrap"] = true,
                                        },
                                        new JsonObject
                                        {
                                            ["type"] = "TextBlock",
                                            ["text"] = command.OutputFormat,
                                            ["fontType"] = "Monospace",
                                            ["isSubtle"] = true,
                                            ["spacing"] = "Small",
                                            ["wrap"] = true,
                                        },
                                        new JsonObject
                                        {
                                            ["type"] = "TextBlock",
                                            ["text"] = $"{GetExposureTitle(command.EffectiveExposure)} · {GetProviderName(command.ProviderId, providers)} · {GetRequestTemplateName(command.RequestTemplateId, requestTemplates)} · {command.SendDelayMilliseconds} ms{(string.IsNullOrWhiteSpace(command.CustomRequestArguments) ? string.Empty : " · Custom request arguments")}{(command.EnableAdvancedOutput ? " · Advanced output" : string.Empty)}",
                                            ["isSubtle"] = true,
                                            ["spacing"] = "Small",
                                        },
                                    },
                                },
                                new JsonObject
                                {
                                    ["type"] = "Column",
                                    ["width"] = "auto",
                                    ["verticalContentAlignment"] = "Center",
                                    ["items"] = new JsonArray
                                    {
                                        new JsonObject
                                        {
                                            ["type"] = "ActionSet",
                                            ["horizontalAlignment"] = "Right",
                                            ["actions"] = new JsonArray
                                            {
                                                BuildSubmitAction(
                                                    "✏",
                                                    $"edit:{command.Id}",
                                                    "Edit command"),
                                                BuildSubmitAction(
                                                    "✕",
                                                    $"delete:{command.Id}",
                                                    "Delete command",
                                                    associatedInputs: "none",
                                                    style: "destructive"),
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
                new JsonObject
                {
                    ["type"] = "Container",
                    ["id"] = editorId,
                    ["isVisible"] = isExpanded,
                    ["items"] = new JsonArray
                    {
                        BuildTextInput(
                            $"name_{command.Id}",
                            "Command name",
                            ReadText(draftInputs, $"name_{command.Id}", command.Name),
                            string.Empty),
                        BuildSeparator(),
                        BuildTextInput(
                            $"format_{command.Id}",
                            "Prompt format",
                            ReadText(draftInputs, $"format_{command.Id}", command.OutputFormat),
                            "Use {} for the search term, {{ for {, and }} for }.",
                            isMultiline: true),
                        BuildSeparator(),
                        BuildChoiceInput(
                            $"provider_{command.Id}",
                            "LLM provider",
                            draftProviderId,
                            providers.Select(provider => (provider.Name, provider.Id))),
                        new JsonObject
                        {
                            ["type"] = "ActionSet",
                            ["actions"] = new JsonArray
                            {
                                BuildSubmitAction(
                                    "Apply provider",
                                    $"apply-provider:{command.Id}",
                                    "Refresh JSON request templates for the selected provider"),
                            },
                        },
                        BuildSeparator(),
                        new JsonObject
                        {
                            ["type"] = "TextBlock",
                            ["text"] = "Select an optional reusable request object for this provider. The template supplies base values; custom request arguments below override matching values recursively. Set a matching custom value to null to omit that field from the request. The extension always controls model and messages.",
                            ["isSubtle"] = true,
                            ["wrap"] = true,
                            ["spacing"] = "Small",
                        },
                        BuildChoiceInput(
                            $"requestTemplate_{command.Id}",
                            "JSON request template",
                            draftRequestTemplateId,
                            new[] { ("None", string.Empty) }.Concat(
                                matchingTemplates.Select(template => (template.Name, template.Id)))),
                        BuildTextInput(
                            $"requestArguments_{command.Id}",
                            "Custom request arguments (optional)",
                            ReadText(
                                draftInputs,
                                $"requestArguments_{command.Id}",
                                command.CustomRequestArguments),
                            "{\"chat_template_kwargs\":{\"enable_thinking\":true}}",
                            isRequired: false,
                            isMultiline: true),
                        new JsonObject
                        {
                            ["type"] = "TextBlock",
                            ["text"] = "Enter a JSON object with provider-specific request fields, such as reasoning options or n. The protected model and messages fields are not allowed. Values are sent in the JSON body only.",
                            ["isSubtle"] = true,
                            ["wrap"] = true,
                            ["spacing"] = "Small",
                        },
                        BuildSeparator(),
                        BuildAdvancedOutputInput(
                            $"advancedOutput_{command.Id}",
                            ReadBoolean(
                                draftInputs,
                                $"advancedOutput_{command.Id}",
                                command.EnableAdvancedOutput),
                            $"globalAdvancedOutputPrompt_{command.Id}",
                            ReadBoolean(draftInputs, $"globalAdvancedOutputPrompt_{command.Id}",
                                command.UseGlobalAdvancedOutputSystemPrompt)),
                        BuildSeparator(),
                        BuildNumberInput(
                            $"delay_{command.Id}",
                            "Send delay (milliseconds)",
                            ReadInteger(draftInputs, $"delay_{command.Id}", command.SendDelayMilliseconds),
                            0,
                            10_000),
                        BuildSeparator(),
                        BuildToggleInput(
                            $"fallback_{command.Id}",
                            "Enable as fallback command",
                            ReadBoolean(
                                draftInputs,
                                $"fallback_{command.Id}",
                                command.EffectiveExposure != CommandExposure.None)),
                        BuildSeparator(),
                        BuildIconInput(command, draftInputs),
                        BuildSeparator(),
                        new JsonObject
                        {
                            ["type"] = "ActionSet",
                            ["horizontalAlignment"] = "Right",
                            ["actions"] = new JsonArray
                            {
                                BuildSubmitAction(
                                    "✓",
                                    $"save:{command.Id}",
                                    "Save changes"),
                                BuildSubmitAction(
                                    "✕",
                                    $"cancel:{command.Id}",
                                    "Cancel editing"),
                            },
                        },
                    },
                },
            },
        };
        ConfigureCommandModeCard(card, command, providers, requestTemplates, stepTemplates, draftInputs);
        return card;
    }

    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "The card builder adds only primitive values and explicit JsonNode instances.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "The card builder adds only primitive values and explicit JsonNode instances.")]
    private static JsonObject BuildIconInput(
        UserCommandDefinition command,
        JsonObject draftInputs) =>
        new()
        {
            ["type"] = "Container",
            ["items"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "TextBlock",
                    ["text"] = "Custom icon file (optional)",
                    ["wrap"] = true,
                },
                new JsonObject
                {
                    ["type"] = "ColumnSet",
                    ["spacing"] = "Small",
                    ["columns"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "Column",
                            ["width"] = "auto",
                            ["verticalContentAlignment"] = "Center",
                            ["items"] = new JsonArray
                            {
                                new JsonObject
                                {
                                    ["type"] = "ActionSet",
                                    ["actions"] = new JsonArray
                                    {
                                        BuildSubmitAction(
                                            "📂",
                                            $"pick-icon:{command.Id}",
                                            "Import a custom icon file"),
                                    },
                                },
                            },
                        },
                        new JsonObject
                        {
                            ["type"] = "Column",
                            ["width"] = "stretch",
                            ["spacing"] = "Small",
                            ["verticalContentAlignment"] = "Center",
                            ["items"] = new JsonArray
                            {
                                new JsonObject
                                {
                                    ["type"] = "Input.Text",
                                    ["id"] = $"icon_{command.Id}",
                                    ["value"] = ReadText(
                                        draftInputs,
                                        $"icon_{command.Id}",
                                        command.IconPath),
                                    ["placeholder"] = "ICO, PNG, JPG, or SVG path; copied into extension storage when saved",
                                },
                            },
                        },
                    },
                },
            },
        };

    private static JsonObject BuildTextInput(
        string id,
        string label,
        string value,
        string placeholder,
        bool isRequired = true,
        bool isMultiline = false) =>
        new()
        {
            ["type"] = "Input.Text",
            ["id"] = id,
            ["label"] = label,
            ["value"] = value,
            ["placeholder"] = placeholder,
            ["isRequired"] = isRequired,
            ["isMultiline"] = isMultiline,
            ["errorMessage"] = $"{label} is required.",
        };

    private static JsonObject BuildPasswordInput(
        string id,
        string label,
        string placeholder,
        string value) =>
        new()
        {
            ["type"] = "Input.Text",
            ["id"] = id,
            ["label"] = label,
            ["placeholder"] = placeholder,
            ["value"] = value,
            ["style"] = "password",
        };

    private static JsonObject BuildNumberInput(
        string id,
        string label,
        int value,
        int minimum,
        int maximum) =>
        new()
        {
            ["type"] = "Input.Number",
            ["id"] = id,
            ["label"] = label,
            ["value"] = value,
            ["min"] = minimum,
            ["max"] = maximum,
            ["isRequired"] = true,
            ["errorMessage"] = $"{label} must be between {minimum} and {maximum}.",
        };

    private static JsonObject BuildChoiceInput(
        string id,
        string label,
        string value,
        IEnumerable<(string Title, string Value)> choices) =>
        new()
        {
            ["type"] = "Input.ChoiceSet",
            ["id"] = id,
            ["label"] = label,
            ["style"] = "compact",
            ["value"] = value,
            ["choices"] = new JsonArray(choices
                .Select(choice => (JsonNode)new JsonObject
                {
                    ["title"] = choice.Title,
                    ["value"] = choice.Value,
                })
                .ToArray()),
        };

    private static JsonObject BuildSeparator(bool isSectionSeparator = false) => new()
    {
        ["type"] = "Image",
        ["url"] = isSectionSeparator ? SectionSeparatorImageUrl : SeparatorImageUrl,
        ["altText"] = string.Empty,
        ["width"] = "stretch",
        ["height"] = isSectionSeparator ? "62px" : "31px",
        ["spacing"] = "None",
    };

    private static JsonObject BuildToggleInput(string id, string title, bool value) =>
        new()
        {
            ["type"] = "Input.Toggle",
            ["id"] = id,
            ["title"] = title,
            ["value"] = value ? "true" : "false",
            ["valueOn"] = "true",
            ["valueOff"] = "false",
        };

    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "The help card adds only primitive values and explicit JsonNode instances.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "The help card adds only primitive values and explicit JsonNode instances.")]
    private static JsonObject BuildAdvancedOutputInput(
        string id,
        bool value,
        string globalPromptId,
        bool useGlobalPrompt)
    {
        var helpId = $"{id}_help";
        return new JsonObject
        {
            ["type"] = "Container",
            ["items"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "TextBlock",
                    ["text"] = "In Default mode, advanced output controls result parsing and sends the shared system prompt when Use global system prompt is also enabled. Turn that switch off to supply JSON-format instructions directly in Prompt format. In Pipeline mode, only an Advanced output format step sends the system prompt; the advanced-output toggle controls parsing only.",
                    ["isSubtle"] = true,
                    ["wrap"] = true,
                },
                new JsonObject
                {
                    ["type"] = "ColumnSet",
                    ["spacing"] = "None",
                    ["columns"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "Column",
                            ["width"] = "auto",
                            ["verticalContentAlignment"] = "Center",
                            ["items"] = new JsonArray
                            {
                                BuildToggleInput(id, "Enable advanced output format", value),
                            },
                        },
                        new JsonObject
                        {
                            ["type"] = "Column",
                            ["width"] = "auto",
                            ["spacing"] = "Small",
                            ["verticalContentAlignment"] = "Center",
                            ["items"] = new JsonArray
                            {
                                new JsonObject
                                {
                                    ["type"] = "RichTextBlock",
                                    ["inlines"] = new JsonArray
                                    {
                                        new JsonObject
                                        {
                                            ["type"] = "TextRun",
                                            ["text"] = "ⓘ",
                                            ["isSubtle"] = true,
                                            ["selectAction"] = new JsonObject
                                            {
                                                ["type"] = "Action.ToggleVisibility",
                                                ["tooltip"] = "JSON result object or ordered array with optional title, subtitle, details, section, and tags fields. Click for details.",
                                                ["targetElements"] = new JsonArray(helpId),
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
                BuildToggleInput(globalPromptId, "Use global system prompt for advanced output", useGlobalPrompt),
                BuildAdvancedOutputHelp(helpId),
            },
        };
    }

    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "The help content adds only primitive values and explicit JsonNode instances.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "The help content adds only primitive values and explicit JsonNode instances.")]
    private static JsonObject BuildAdvancedOutputHelp(string id) =>
        new()
        {
            ["type"] = "Container",
            ["id"] = id,
            ["isVisible"] = false,
            ["spacing"] = "Small",
            ["style"] = "emphasis",
            ["items"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "TextBlock",
                    ["text"] = "Advanced output format",
                    ["weight"] = "Bolder",
                    ["wrap"] = true,
                },
                new JsonObject
                {
                    ["type"] = "TextBlock",
                    ["text"] = "The response must be one JSON result object, or an ordered array of result objects when the prompt asks for variations. All five fields are optional. If present, title, subtitle, details, and section must be strings; tags must be an array of strings. Property names are lowercase and case-sensitive. Extra properties are ignored.",
                    ["wrap"] = true,
                },
                new JsonObject
                {
                    ["type"] = "TextBlock",
                    ["text"] = "{\n  \"title\": \"Result title\",\n  \"subtitle\": \"Supporting text\",\n  \"details\": \"Full content copied when selected\",\n  \"section\": \"Section name\",\n  \"tags\": [\"tag one\", \"tag two\"]\n}",
                    ["fontType"] = "Monospace",
                    ["wrap"] = true,
                },
                new JsonObject
                {
                    ["type"] = "TextBlock",
                    ["text"] = "Arrays preserve element order and may be empty. Invalid array elements appear as error results with their original JSON in details; root fallback results skip them and use the first valid object. A single unlabelled or json Markdown code fence is accepted. Other invalid formatted output uses the normal output rules. Edit the shared prompt in the Advanced output format system-step card. Default-mode commands can inject it using their separate prompt toggle; pipelines inject it only through the formatting step.",
                    ["isSubtle"] = true,
                    ["wrap"] = true,
                },
            },
        };

    private static string GetProviderName(
        string providerId,
        IReadOnlyList<LlmProviderSettings> providers) =>
        providers.FirstOrDefault(provider =>
            string.Equals(provider.Id, providerId, StringComparison.Ordinal))?.Name ??
        providers[0].Name;

    private static string GetRequestTemplateName(
        string requestTemplateId,
        IReadOnlyList<JsonRequestTemplateDefinition> requestTemplates) =>
        string.IsNullOrEmpty(requestTemplateId)
            ? "No request template"
            : requestTemplates.FirstOrDefault(template => string.Equals(
                template.Id,
                requestTemplateId,
                StringComparison.Ordinal))?.Name ?? "No request template";

    private static string GetExposureTitle(CommandExposure exposure) => exposure switch
    {
        CommandExposure.None => "Command only",
        _ => "Fallback command",
    };

    private static JsonObject BuildSubmitAction(
        string title,
        string actionId,
        string? tooltip = null,
        string? associatedInputs = null,
        string? style = null,
        bool isEnabled = true)
    {
        var action = new JsonObject
        {
            ["type"] = "Action.Submit",
            ["id"] = actionId,
            ["title"] = title,
            ["data"] = new JsonObject { ["actionId"] = actionId },
        };

        if (!string.IsNullOrEmpty(tooltip))
        {
            action["tooltip"] = tooltip;
        }

        if (!string.IsNullOrEmpty(associatedInputs))
        {
            action["associatedInputs"] = associatedInputs;
        }

        if (!string.IsNullOrEmpty(style))
        {
            action["style"] = style;
        }

        if (!isEnabled)
        {
            action["isEnabled"] = false;
        }

        return action;
    }

    private static string? Validate(IReadOnlyList<UserCommandDefinition> commands)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var command in commands)
        {
            if (string.IsNullOrWhiteSpace(command.Name))
            {
                return "Every command must have a name.";
            }

            command.Name = command.Name.Trim();
            if (!names.Add(command.Name))
            {
                return $"Command names must be unique. '{command.Name}' is used more than once.";
            }

            if (command.Mode == CommandMode.Pipeline)
            {
                if (command.Steps.Count == 0 || !command.Steps.All(UserCommandStore.IsValidStep))
                {
                    return $"Command '{command.Name}' needs valid, named steps with a prompt and provider.";
                }

                if (command.SendDelayMilliseconds is < 0 or > 10_000)
                {
                    return $"Command '{command.Name}' must have a send delay between 0 and 10000 milliseconds.";
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(command.OutputFormat))
            {
                return $"Command '{command.Name}' must have an output format.";
            }

            if (!OutputFormatter.TryFormat(command.OutputFormat, string.Empty, out _, out var error))
            {
                return $"Command '{command.Name}' has an invalid output format: {error}";
            }

            if (command.SendDelayMilliseconds is < 0 or > 10_000)
            {
                return $"Command '{command.Name}' must have a send delay between 0 and 10000 milliseconds.";
            }

            if (!CustomRequestArguments.TryParse(
                command.CustomRequestArguments,
                out var customArguments,
                out var customArgumentsError))
            {
                return $"Command '{command.Name}': {customArgumentsError}";
            }

            customArguments?.Dispose();
        }

        return null;
    }

    private static string? ValidateRequestTemplates(
        IReadOnlyList<JsonRequestTemplateDefinition> templates,
        IReadOnlyList<LlmProviderSettings> providers)
    {
        var providerIds = providers.Select(provider => provider.Id).ToHashSet(StringComparer.Ordinal);
        var namesByProvider = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var template in templates)
        {
            template.Name = template.Name.Trim();
            if (string.IsNullOrWhiteSpace(template.Name))
            {
                return "Every JSON request template must have a name.";
            }

            if (!providerIds.Contains(template.ProviderId))
            {
                return $"JSON request template '{template.Name}' must use an existing LLM provider.";
            }

            if (!namesByProvider.TryGetValue(template.ProviderId, out var names))
            {
                names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                namesByProvider.Add(template.ProviderId, names);
            }

            if (!names.Add(template.Name))
            {
                return $"JSON request template names must be unique per provider. '{template.Name}' is used more than once.";
            }

            if (!CustomRequestArguments.TryParse(
                template.RequestArguments,
                out var arguments,
                out var argumentsError,
                "Template request arguments"))
            {
                return $"JSON request template '{template.Name}': {argumentsError}";
            }

            arguments?.Dispose();
        }

        return null;
    }

    private CommandResult SetCommandEditorVisibility(string commandId, bool isExpanded)
    {
        if (!_store.GetCommands().Any(command =>
            string.Equals(command.Id, commandId, StringComparison.Ordinal)))
        {
            return ShowError("The command no longer exists.");
        }

        if (isExpanded)
        {
            _expandedCommandIds.Add(commandId);
        }
        else
        {
            _expandedCommandIds.Remove(commandId);
            RemoveDraftInputsForCommand(commandId);
        }

        Refresh();
        return CommandResult.KeepOpen();
    }

    private CommandResult SetProviderEditorVisibility(string providerId, bool isExpanded)
    {
        if (_providerSettingsStore.Get(providerId) is null)
        {
            return ShowError("The provider no longer exists.");
        }

        if (isExpanded)
        {
            _expandedProviderIds.Add(providerId);
        }
        else
        {
            _expandedProviderIds.Remove(providerId);
            RemoveDraftInputsForProvider(providerId);
        }

        Refresh();
        return CommandResult.KeepOpen();
    }

    private CommandResult SetRequestTemplateEditorVisibility(
        string templateId,
        bool isExpanded)
    {
        if (_requestTemplateStore.Get(templateId) is null)
        {
            return ShowError("The JSON request template no longer exists.");
        }

        if (isExpanded)
        {
            _expandedRequestTemplateIds.Add(templateId);
        }
        else
        {
            _expandedRequestTemplateIds.Remove(templateId);
            RemoveDraftInputsForRequestTemplate(templateId);
        }

        Refresh();
        return CommandResult.KeepOpen();
    }

    private CommandResult SetStepTemplateEditorVisibility(string templateId, bool isExpanded)
    {
        if (_stepTemplateStore.Get(templateId) is null)
        {
            return ShowError("The step template no longer exists.");
        }

        if (isExpanded)
        {
            _expandedStepTemplateIds.Add(templateId);
        }
        else
        {
            _expandedStepTemplateIds.Remove(templateId);
            RemoveDraftInputsForStepTemplate(templateId);
        }

        Refresh();
        return CommandResult.KeepOpen();
    }

    private void CaptureDraftInputs(JsonObject input)
    {
        foreach (var property in input)
        {
            if (!string.Equals(property.Key, "actionId", StringComparison.Ordinal))
            {
                _draftInputs[property.Key] = property.Value?.DeepClone();
            }
        }
    }

    private void RemoveDraftInputsForCommand(string commandId)
    {
        _pipelineDrafts.Remove(commandId);
        _expandedStepIds.RemoveWhere(id => id.StartsWith($"step_card_{commandId}_", StringComparison.Ordinal));
        foreach (var key in _draftInputs.Select(property => property.Key)
            .Where(key => key.StartsWith($"step_{commandId}_", StringComparison.Ordinal)).ToArray())
        {
            RemoveDraftInput(key);
        }

        RemoveDraftInput($"mode_{commandId}");
        RemoveDraftInput($"newStepSource_{commandId}");
        RemoveDraftInput($"name_{commandId}");
        RemoveDraftInput($"format_{commandId}");
        RemoveDraftInput($"requestArguments_{commandId}");
        RemoveDraftInput($"advancedOutput_{commandId}");
        RemoveDraftInput($"globalAdvancedOutputPrompt_{commandId}");
        RemoveDraftInput($"delay_{commandId}");
        RemoveDraftInput($"provider_{commandId}");
        RemoveDraftInput($"requestTemplate_{commandId}");
        RemoveDraftInput($"fallback_{commandId}");
        RemoveDraftInput($"icon_{commandId}");
    }

    private void RemoveDraftInputsForProvider(string providerId)
    {
        RemoveDraftInput($"providerName_{providerId}");
        RemoveDraftInput($"providerBaseUrl_{providerId}");
        RemoveDraftInput($"providerModel_{providerId}");
        RemoveDraftInput($"providerApiKey_{providerId}");
    }

    private void RemoveDraftInputsForRequestTemplate(string templateId)
    {
        RemoveDraftInput($"requestTemplateName_{templateId}");
        RemoveDraftInput($"requestTemplateProvider_{templateId}");
        RemoveDraftInput($"requestTemplateArguments_{templateId}");
    }

    private void RemoveDraftInputsForStepTemplate(string templateId)
    {
        var prefix = $"stepTemplate_{templateId}_";
        foreach (var key in _draftInputs.Select(property => property.Key)
            .Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).ToArray())
        {
            RemoveDraftInput(key);
        }
    }

    private void RemoveDraftInput(string key) => _draftInputs.Remove(key);

    private static int ReadInteger(JsonObject input, string key, int fallback) =>
        TryReadInteger(input, key, out var value) ? value : fallback;

    private static string ReadText(JsonObject input, string key, string fallback = "") =>
        input[key]?.GetValue<string>() ?? fallback;

    private static bool ReadBoolean(JsonObject input, string key, bool fallback = false) =>
        input[key] is null
            ? fallback
            : string.Equals(input[key]?.ToString(), "true", StringComparison.OrdinalIgnoreCase);

    private static bool TryReadInteger(JsonObject input, string key, out int value)
    {
        if (input[key] is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue(out value))
            {
                return true;
            }

            if (jsonValue.TryGetValue<string>(out var text) && int.TryParse(text, out value))
            {
                return true;
            }
        }

        value = 0;
        return false;
    }

    private static string? ReadActionId(string payload)
    {
        try
        {
            return JsonNode.Parse(payload)?["actionId"]?.GetValue<string>();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
