<p align="center">
  <img src="CommandPaletteLLM/Assets/AppIcon.master.png" alt="Command Palette LLM icon" width="128">
</p>

<h1 align="center">Command Palette LLM</h1>

<p align="center">
  Turn any OpenAI-compatible LLM into fast, reusable commands inside PowerToys Command Palette.
</p>

Command Palette LLM is a Windows Command Palette extension for creating small, focused AI tools: summarize selected text, rewrite a sentence, generate several alternatives, classify input into rich results, or build any other prompt-driven command you need. It works with local servers such as llama.cpp as well as hosted services that expose an OpenAI-compatible Chat Completions API.

> [!NOTE]
> This project currently ships as source code rather than a prebuilt installer. See [Build and deploy](#build-and-deploy) for local installation.

## Features

- Create and edit commands without changing code.
- Choose a single prompt or an ordered pipeline with named steps and per-step providers and request settings.
- Build multiline prompt templates with `{}` placeholders for the current query.
- Configure multiple OpenAI-compatible providers and assign a provider per command.
- Use each tool as a dedicated top-level command, an inline fallback result, or both.
- Debounce requests while typing with a configurable delay from 0 to 10,000 ms.
- Request between 1 and 10 response variations on a command page.
- Copy a result by selecting it; longer responses also receive a details view.
- Turn model-produced JSON into native titles, subtitles, details, sections, and tags.
- Give individual commands custom ICO, PNG, JPG, JPEG, or SVG icons.
- Monitor provider availability, cancel stale requests, and retry unavailable endpoints.
- Keep all command and provider configuration in local files.

## Requirements

To use a development build, you need:

- Windows 11 with [PowerToys](https://learn.microsoft.com/windows/powertoys/install) and Command Palette installed.
- Windows Developer Mode enabled.
- An OpenAI-compatible endpoint with `/v1/models` and `/v1/chat/completions` routes.

To build the extension, you also need:

- .NET 10 SDK.
- Visual Studio with C# Windows App SDK/WinUI and MSIX packaging support. Microsoft lists the current environment requirements in its [Command Palette extension guide](https://learn.microsoft.com/windows/powertoys/command-palette/creating-an-extension).

The solution targets x64 and ARM64.

## Build and deploy

Clone or open the repository, then restore and verify it:

```powershell
dotnet restore CommandPaletteLLM.sln
dotnet build CommandPaletteLLM.sln -c Debug -p:Platform=x64
dotnet test CommandPaletteLLM.sln -c Debug -p:Platform=x64
```

Open `CommandPaletteLLM.sln` in Visual Studio, select **Debug** and **x64**, then choose **Build → Deploy CommandPaletteLLM**. A normal build produces the binaries but does not register the extension with Windows.

After the first deployment—or after redeploying a changed build—open Command Palette, run **Reload Command Palette Extension**, and select **Command Palette LLM (Development)**. Visual Studio uses the isolated `maxnevans.CommandPaletteLLM.Development` package identity, so this development deployment does not replace the Store package or share its settings. Microsoft documents this development loop in [How to create Command Palette extensions](https://learn.microsoft.com/windows/powertoys/command-palette/creating-an-extension#understanding-the-extension-project-structure).

If you previously deployed **Command Palette LLM (Visual Studio)**, uninstall it once from **Windows Settings → Apps → Installed apps** before deploying the renamed development package. Windows treats the new package identity as a separate installation.

For ARM64, replace `x64` with `ARM64` in the commands and Visual Studio configuration.

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

Use **JSON Request Templates** to reuse provider-specific request fields across
commands. Add a template, assign it to an LLM provider, and enter a static JSON
object such as:

```json
{
  "temperature": 0.7,
  "chat_template_kwargs": {
    "enable_thinking": true,
    "reasoning_effort": "medium"
  }
}
```

Template names must be unique within their provider. A command can select only a
template assigned to the same provider, and **None** disables template use. Use
**Apply provider** while editing a command to refresh the template list without
saving the command. Deleting or reassigning a template resets affected commands
to **None**.

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

### Chain steps in a pipeline

Edit a command, select **Pipeline of steps**, and choose **Apply mode**. The command
keeps its name, send delay, fallback setting, custom icon, and Command Palette ID.
Configure aliases such as `>>` using Command Palette's native alias settings.

Choose **User step** or an application-provided system step from the **Step type**
picker, then select **Add step**. User steps have their own prompt, provider, JSON
request template, and custom request arguments. Each step appears as a card
inside its command, with **Edit** / **Collapse**, move, and **Remove** actions.
The card summary shows both its provider and JSON request template.
Adding a step opens its card and preserves other editors and unsaved inputs.
Collapsing a step keeps its edits; **Save changes** on the command saves the chain.
Each card shows where its input comes from: **Search Content** for the first step,
then the name of the preceding step. Steps run from top to bottom.
In the first step, `{}` receives the search text; later steps receive the preceding
step's complete response. Multiple provider choices are joined with blank lines,
so the next step can structure all variations together. Requests run sequentially;
the send delay applies once before the chain. Changing the query cancels the chain,
and a failed step stops it without publishing intermediate content.

For a **Translate** command, a pipeline could be:

1. **Generate** — ask for 5–7 translations and supporting content.
2. **Structure** — reorganize those translations with another prompt.
3. **Advanced output format** — use the built-in system step to request the existing
   advanced output JSON format.

The advanced output system step is always available to add. It can be removed from
an individual pipeline, repeated, or moved anywhere in the chain. Its shared system
prompt is editable in **System steps → Advanced output format**, with save, cancel,
and reset-to-default actions. This permanent card has no delete action. An empty
prompt disables system-message injection. Its provider and request arguments are
configurable per occurrence in a pipeline. Existing customized global prompts are
retained as this shared system-step prompt. In Pipeline mode, the advanced-output
toggle never injects a prompt; only an explicitly included formatting step sends it.
Default mode retains automatic injection when both **Enable advanced output** and
**Use global system prompt for advanced output** are enabled.
Pipeline step cards have no advanced-output toggles. **Enable advanced output**
remains on the command card and controls how its final responses are displayed.

The last step connects to output automatically. With **Enable advanced output** on,
the existing advanced-output rules apply: valid JSON becomes rich results, invalid
array entries become error results on the command page, and root fallback uses the
first valid entry. Other invalid formatted output follows the regular text rules.
With the option off, all responses use regular output, including JSON. A pipeline
does not need a formatting step to return advanced JSON.

**Save changes** persists the chain; **Cancel editing** discards its edits. Switching
back to **Default** restores the original single-prompt settings, including the
separate global-system-prompt toggle, while retaining
the saved pipeline for later use. Existing
commands start in Default mode. Switching to Pipeline for the first time copies the
current prompt and request settings into the first step. Add a formatting step
explicitly if you want the shared system prompt in the pipeline. Default-mode
commands can disable **Use global system prompt** and include JSON-format
instructions directly in their prompt instead.

The [Command Palette SDK](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extensibility-overview)
provides command, alias, and fallback integration, but no LLM pipeline model or
editor. Pipelines therefore use the extension's existing settings file and form;
only commands appear as native Command Palette commands, while steps belong to
their command and are edited within it.

Use `{{` and `}}` when the prompt itself needs literal braces. A template may contain `{}` more than once; `{0}` and unmatched braces are rejected.

Custom request arguments must be a JSON object. They are merged over the selected
JSON request template and added as top-level fields to the `/chat/completions`
request body. Request JSON is static, so `{}` in string values remains literal.
For example:

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

The extension always controls `model` and `messages`; those property names are
rejected in templates and custom arguments regardless of casing. Command-specific
objects recursively override template objects. Arrays and primitives replace the
corresponding template value; setting a matching command value to `null` removes
that field from the final request. Other fields affect only
the JSON body, not the provider URL or HTTP headers. If `n` is omitted,
the provider chooses its default number of responses. Dedicated command pages show
every returned choice, while fallback results use the first choice. This is independent
of advanced-output arrays requested through the user's natural-language prompt.

## Use a command

There are two ways to invoke a configured tool:

1. Select its top-level command, type the input, and wait for one or more response items.
2. If fallback mode is enabled, type directly on Command Palette's root page and select the matching inline result.

Selecting a completed response copies its full text to the clipboard. When a query changes or you move between the root and a command page, the extension cancels work that is no longer relevant so an older response cannot replace the current one.

If the endpoint is offline, the command page displays its connection state and retries periodically. Root fallback results remain hidden until the assigned provider is available.

## Advanced output

Advanced output lets the model control how results appear in Command Palette. In Default mode, **Enable advanced output** controls result parsing and injects the shared system prompt when **Use global system prompt for advanced output** is also enabled. In Pipeline mode, **Enable advanced output** only controls presentation of the final response; system-prompt injection belongs exclusively to the **Advanced output format** step, which runs independently of that toggle. The expected response is either one JSON result object or an ordered array of result objects. A result object uses these case-sensitive fields:

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

Configuration files are created only after settings are saved. An unpackaged
development or EXE build stores them under `%LOCALAPPDATA%\CommandPaletteLLM`.
Windows redirects the same path for the MSIX build to:

```text
%LOCALAPPDATA%\Packages\maxnevans.CommandPaletteLLM_9aav5gzbpx8nj\LocalCache\Local\CommandPaletteLLM
```

The effective directory contains:

```text
CommandPaletteLLM/
├── settings.json
└── Icons/
```

API keys are encrypted for the current Windows user with Windows DPAPI before
they are stored in the `Providers` section of `settings.json`. Global settings,
providers, JSON request templates, and commands are stored together in that file. Existing
`commands.json`, `providers.json`, and `provider.json` files are migrated
automatically. Prompts are sent only to the provider
assigned to the command, and a non-local provider must be explicitly authorized
in settings before the extension contacts it. The extension has no separate
telemetry or cloud backend. See [PRIVACY.md](PRIVACY.md) for the full data flow.

## Build a Microsoft Store submission

The package uses the Partner Center identity reserved for this product:

```text
Name:                 maxnevans.CommandPaletteLLM
Publisher:            CN=D8B7BDC7-1445-4AE6-BEEC-E9C2D0FD7ACD
PublisherDisplayName: maxnevans
```

Visual Studio 2026 exposes packaging as **Pack** on the project context menu.
The selected solution configuration and platform determine which package it
creates. Prepare Store packages as follows:

1. Confirm that the package identity above still exactly matches **Partner Center → Command Palette LLM → Product management → Product identity**. The values are case-sensitive.
2. Set the package version in `CommandPaletteLLM\Package.appxmanifest`. Use four numeric components, keep the final component `0`, and choose a version higher than the previous Store submission.
3. In the Visual Studio toolbar, select **Release** and **x64**.
4. In Solution Explorer, right-click the `CommandPaletteLLM` project and select **Pack**.
5. Confirm that the build log ends with `PackageSuccessfullyCreated` and names `CommandPaletteLLM\AppPackages\CommandPaletteLLM_<version>_x64.msixupload`.
6. Change the toolbar platform to **ARM64** and select **Pack** again. Confirm that `CommandPaletteLLM_<version>_arm64.msixupload` was created in the same directory.
7. Upload both top-level `.msixupload` files under **Partner Center → Command Palette LLM → Product release → Start submission → Packages**. Partner Center selects the applicable architecture for each device.

The Release settings in `CommandPaletteLLM.csproj` select `StoreOnly` packaging,
create a separate upload for each architecture, and leave signing disabled so
Microsoft Store can sign the certified packages. Do not upload files from an
`*_Test` directory. Those directories contain local sideloading artifacts and
installation scripts. Likewise, a filename containing `_Debug` was built with
the development package identity and is not a Store submission.

The expected output directory after preparing both architectures is:

```text
CommandPaletteLLM/AppPackages/
├── CommandPaletteLLM_<version>_x64.msixupload
├── CommandPaletteLLM_<version>_arm64.msixupload
├── CommandPaletteLLM_<version>_x64_Test/       Local test artifacts
└── CommandPaletteLLM_<version>_arm64_Test/     Local test artifacts
```

Before uploading, install and exercise the x64 test package on an x64 system,
test the ARM64 package on ARM64 hardware when available, and run the current
Windows App Certification Kit. The `AppPackages` and `BundleArtifacts`
directories are generated output and must remain uncommitted.

Microsoft signs the package after certification. Complete Pricing and availability, Properties, Age ratings, Store listings, and Submission options before selecting **Submit for certification**.

The Store listing and certification notes must explain that this is an extension requiring Microsoft PowerToys with Command Palette enabled. Include instructions for opening Command Palette and exercising at least one configured LLM command so certification can test the extension.

The public preview version is `0.0.1`; its Store package version is `0.0.1.0`.
The fourth component is required by the MSIX package identity format and is
kept at zero for Store submissions.

Store distribution is the only maintained end-user installation path. The repository does not produce or support a private, self-signed, or unsigned installer.

## Troubleshooting

### The extension does not appear

- Confirm that you used **Deploy**, not only **Build** or the unpackaged launch profile.
- Run **Reload Command Palette Extension** after deployment.
- Make sure the selected Visual Studio platform matches the machine architecture.

### A provider stays unavailable

- Verify that the base URL is reachable and exposes `/models`.
- Check the model identifier and API key.
- If the base URL ends in `/chat/completions`, the extension derives the health-check URL from the same API root.

### Responses arrive while typing

Increase the command's send delay. Lower values feel faster but can submit more requests when the provider responds quickly.

### Remove the development deployment

Uninstall **Command Palette LLM (Development)** from **Windows Settings → Apps → Installed apps**, then reload Command Palette. This removes the registered package and may remove its isolated virtualized package data; back up its configuration first if needed.

## Project structure

```text
CommandPaletteLLM.sln
├── CommandPaletteLLM/          Extension, settings UI, API client, and MSIX metadata
│   ├── Assets/                 App and package artwork
│   └── Pages/                  Command and settings pages
└── CommandPaletteLLM.Tests/    xUnit behavior and integration-style unit tests
```

## License

Copyright © 2026 maxnevans. The source code is proprietary; viewing it does not
grant permission to use, copy, modify, compile, or distribute it. Official
compiled binaries may be used for personal, educational, charitable, and
nonprofit purposes under [BINARY-LICENSE.txt](BINARY-LICENSE.txt). Commercial
and for-profit use is not permitted without separate authorization.
