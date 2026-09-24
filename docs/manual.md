# Command Palette LLM manual

Command Palette LLM turns OpenAI-compatible LLMs into reusable commands inside PowerToys Command Palette. It works with local servers such as llama.cpp and hosted services that expose an OpenAI-compatible Chat Completions API.

> [!WARNING]
> **Alpha / pre-release software:** Command Palette LLM is not a stable release.
> Features, behavior, and the settings format may change without backward
> compatibility. Before updating, use **Settings → Export** and keep the
> exported file in a safe place. A future alpha version may require manual
> settings migration even when a backup is available.

## Install

Command Palette LLM is published in Microsoft Store and can be installed through WinGet's built-in `msstore` source:

```powershell
winget install --id 9PK5TNWKQ00Q --source msstore
```

The same signed Store package is available from the [Microsoft Store listing](https://apps.microsoft.com/detail/9PK5TNWKQ00Q). Microsoft PowerToys with Command Palette enabled is required separately.

After the community manifest is accepted, an unsigned GitHub installer is available through WinGet's community source under the human-readable identifier:

```powershell
winget install --id maxnevans.CommandPaletteLLM --source winget
```

> [!CAUTION]
> The community installer is unsigned. It registers a self-signed,
> metadata-only sparse package so Command Palette can discover the extension.
> Setup requests administrator approval to trust its public certificate in the
> computer's Trusted People store and removes it on uninstall. Windows may show
> **Unknown publisher**, display a SmartScreen warning, or block the outer
> installer under policy. The Microsoft Store package is signed by Microsoft.

Use one distribution channel at a time. Export settings before switching, uninstall the existing variant, install the other variant, and import the backup.

## Configure a provider

Open the **Command Palette LLM** extension settings and expand **LLM providers**. The initial provider is ready for a typical local llama.cpp server:

| Setting | Default | Purpose |
| --- | --- | --- |
| Name | `Local LLM` | Friendly name shown when assigning a provider. |
| Base URL | `http://127.0.0.1:8080/v1` | OpenAI-compatible API root. A full `/chat/completions` URL is also accepted. |
| Model | `local-model` | Value sent in the request's `model` field. |
| API key | Empty | Optional bearer token. Leave the field empty when editing to keep the saved key. |

The extension appends `/chat/completions` when needed and checks the corresponding `/models` endpoint before sending a prompt. Add more providers when different commands should use different local models or services.

## Create a JSON request template

Use **JSON Request Templates** to reuse provider-specific request fields across commands. Add a template, assign it to an LLM provider, and enter a static JSON object such as:

```json
{
  "temperature": 0.7,
  "chat_template_kwargs": {
    "enable_thinking": true,
    "reasoning_effort": "medium"
  }
}
```

Template names must be unique within their provider. A command can select only a template assigned to the same provider, and **None** disables template use. Use **Apply provider** while editing a command to refresh the template list without saving the command. Deleting or reassigning a template resets affected commands to **None**.

## Create a step template

Use **Step templates** to save a user step's name, prompt, provider, JSON request template, and custom request arguments. Template names are unique. Adding a step from a template copies its current values into a new, independent pipeline step; editing or deleting the template later does not change steps already created from it. The copied JSON request-template selection remains a normal live reference to that separate JSON request template.

## Create a command

In the extension settings, enter a command name, select **Add**, and edit the new command. Each command supports:

| Option | Description |
| --- | --- |
| Command name | The top-level command and fallback label. Names must be unique. |
| Command mode | Default keeps the existing single prompt; Pipeline chains named steps. Select **Apply mode** to switch the editor. |
| Prompt format | The exact prompt sent to the provider. Every `{}` is replaced with the user's query. |
| Custom request arguments | Optional provider-specific JSON body fields, including reasoning controls or `n`. |
| JSON request template | Optional reusable request fields assigned to the selected provider. |
| Advanced output | Interprets a compatible JSON response as rich Command Palette metadata. |
| Send delay | Wait time after typing before a request is sent. The default is 650 ms. |
| LLM provider | Provider used for this command. |
| Fallback command | Shows the command's response among results on Command Palette's root page. |
| Custom icon | Copies a supported image into the extension's local storage and uses it for the command. |

For example, a command named **Summarize** might use:

```text
Summarize the following text in one concise sentence:

{}
```

Use `{{` and `}}` when the prompt itself needs literal braces. A template may contain `{}` more than once; `{0}` and unmatched braces are rejected.

### Chain steps in a pipeline

Edit a command, select **Pipeline of steps**, and choose **Apply mode**. The command keeps its name, send delay, fallback setting, custom icon, and Command Palette ID. Configure aliases such as `>>` using Command Palette's native alias settings.

Choose **None (new user step)**, the application-provided system step, or a saved template from the **Step source** picker, then select **Add step**. **None** is the default and creates a blank user step. User steps have their own prompt, provider, JSON request template, and custom request arguments. Each step appears as a card inside its command, with **Edit** / **Collapse**, move, and **Remove** actions. The card summary shows both its provider and JSON request template.

Adding a step opens its card and preserves other editors and unsaved inputs. Collapsing a step keeps its edits; **Save changes** on the command saves the chain. Each card shows where its input comes from: **Search Content** for the first step, then the name of the preceding step. Steps run from top to bottom.

In the first step, `{}` receives the search text; later steps receive the preceding step's complete response. Multiple provider choices are joined with blank lines, so the next step can structure all variations together. Requests run sequentially; the send delay applies once before the chain. Changing the query cancels the chain, and a failed step stops it without publishing intermediate content.

For a **Translate** command, a pipeline could be:

1. **Generate** — ask for 5–7 translations and supporting content.
2. **Structure** — reorganize those translations with another prompt.
3. **Advanced output format** — use the built-in system step to request the existing advanced output JSON format.

The advanced output system step is always available to add. It can be removed from an individual pipeline, repeated, or moved anywhere in the chain. Its shared system prompt is editable in **System steps → Advanced output format**, with save, cancel, and reset-to-default actions. This permanent card has no delete action. An empty prompt disables system-message injection. Its provider and request arguments are configurable per occurrence in a pipeline. Existing customized global prompts are retained as this shared system-step prompt.

In Pipeline mode, the advanced-output toggle never injects a prompt; only an explicitly included formatting step sends it. Default mode retains automatic injection when both **Enable advanced output** and **Use global system prompt for advanced output** are enabled. Pipeline step cards have no advanced-output toggles. **Enable advanced output** remains on the command card and controls how its final responses are displayed.

The last step connects to output automatically. With **Enable advanced output** on, the existing advanced-output rules apply: valid JSON becomes rich results, invalid array entries become error results on the command page, and root fallback uses the first valid entry. Other invalid formatted output follows the regular text rules. With the option off, all responses use regular output, including JSON. A pipeline does not need a formatting step to return advanced JSON.

**Save changes** persists the chain; **Cancel editing** discards its edits. Switching back to **Default** restores the original single-prompt settings, including the separate global-system-prompt toggle, while retaining the saved pipeline for later use. Existing commands start in Default mode. Switching to Pipeline for the first time copies the current prompt and request settings into the first step. Add a formatting step explicitly if you want the shared system prompt in the pipeline. Default-mode commands can disable **Use global system prompt** and include JSON-format instructions directly in their prompt instead.

The [Command Palette SDK](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extensibility-overview) provides command, alias, and fallback integration, but no LLM pipeline model or editor. Pipelines therefore use the extension's settings file and form; only commands appear as native Command Palette commands, while steps belong to their command and are edited within it.

### Custom request arguments

Custom request arguments must be a JSON object. They are merged over the selected JSON request template and added as top-level fields to the `/chat/completions` request body. Request JSON is static, so `{}` in string values remains literal. For example:

```json
{
  "chat_template_kwargs": {
    "enable_thinking": true,
    "reasoning_effort": "medium",
    "preserve_thinking": false
  },
  "n": 2
}
```

The extension always controls `model` and `messages`; those property names are rejected in templates and custom arguments regardless of casing. Command-specific objects recursively override template objects. Arrays and primitives replace the corresponding template value; setting a matching command value to `null` removes that field from the final request. Other fields affect only the JSON body, not the provider URL or HTTP headers.

If `n` is omitted, the provider chooses its default number of responses. Dedicated command pages show every returned choice, while fallback results use the first choice. This is independent of advanced-output arrays requested through the user's natural-language prompt.

## Use a command

There are two ways to invoke a configured tool:

1. Select its top-level command, type the input, and wait for one or more response items.
2. If fallback mode is enabled, type directly on Command Palette's root page and select the matching inline result.

Selecting a completed response copies its full text to the clipboard. When a query changes or you move between the root and a command page, the extension cancels work that is no longer relevant so an older response cannot replace the current one.

If the endpoint is offline, the command page displays its connection state and retries periodically. Root fallback results remain hidden until the assigned provider is available.

## Advanced output

Advanced output lets the model control how results appear in Command Palette. In Default mode, **Enable advanced output** controls result parsing and injects the shared system prompt when **Use global system prompt for advanced output** is also enabled. In Pipeline mode, **Enable advanced output** only controls presentation of the final response; system-prompt injection belongs exclusively to the **Advanced output format** step, which runs independently of that toggle.

The expected response is either one JSON result object or an ordered array of result objects. A result object uses these case-sensitive fields:

```json
{
  "title": "Result title",
  "subtitle": "Supporting text",
  "details": "Full content shown in details and copied when selected",
  "section": "Section name",
  "tags": ["tag one", "tag two"]
}
```

`title`, `subtitle`, `details`, and `section` must be strings; `tags` must be an array of strings. Extra properties are ignored. Plain JSON and a single unlabelled or `json` Markdown code fence are accepted. For example, a request for alternatives may return:

```json
[
  { "title": "First variation" },
  { "title": "Second variation", "details": "Full second result" }
]
```

Dedicated command pages display every array element in order and flatten arrays from multiple provider choices in choice order. Invalid array elements remain in place as error results whose details and copy action contain the invalid JSON. An empty array produces no results. Root-page fallback commands skip invalid elements and display only the first valid object from the first provider choice; they remain hidden if none exists.

The system prompt is shared. Edit it from **System steps → Advanced output format** in extension settings, or reset it to the supplied default. This application-provided step card cannot be deleted. Saving an empty prompt disables system-message injection while leaving command-level advanced response parsing unchanged.

The supplied default is:

```text
Return only JSON, with no Markdown or surrounding text. A result object uses lowercase, case-sensitive fields: "title" (required concise result), "subtitle" (optional supporting text), "details" (optional full content), "section" (optional group name), and "tags" (optional array of strings). Omit unused optional fields. If the user's prompt asks in natural language for variations, alternatives, options, or multiple versions, return a JSON array containing one result object per variation; an empty array is allowed. Otherwise, return one result object. Choose the array form only from the user's wording, not request parameters such as "n".
```

If a standalone object, the JSON document, or its root is invalid, the response is still shown using the normal text-result layout instead of being discarded.

## Local data and privacy

Configuration files are created only after settings are saved. Windows redirects the application data path for the MSIX build to:

```text
%LOCALAPPDATA%\Packages\maxnevans.CommandPaletteLLM_9aav5gzbpx8nj\LocalCache\Local\CommandPaletteLLM
```

The effective directory contains:

```text
CommandPaletteLLM/
├── settings.json
└── Icons/
```

API keys are encrypted for the current Windows user with Windows DPAPI before they are stored in the `Providers` section of `settings.json`. Global settings, providers, JSON request templates, step templates, and commands are stored together in that file. Existing `commands.json`, `providers.json`, and `provider.json` files are migrated automatically.

Prompts are sent only to the provider assigned to the command, and a non-local provider must be explicitly authorized in settings before the extension contacts it. The extension has no separate telemetry or cloud backend. See [PRIVACY.md](../PRIVACY.md) for the full data flow.

## Troubleshooting

### A provider stays unavailable

- Verify that the base URL is reachable and exposes `/models`.
- Check the model identifier and API key.
- If the base URL ends in `/chat/completions`, the extension derives the health-check URL from the same API root.

### Responses arrive while typing

Increase the command's send delay. Lower values feel faster but can submit more requests when the provider responds quickly.
