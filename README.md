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

After the first deployment—or after redeploying a changed build—open Command Palette, run **Reload Command Palette Extension**, and select **Command Palette LLM**. Microsoft documents this development loop in [How to create Command Palette extensions](https://learn.microsoft.com/windows/powertoys/command-palette/creating-an-extension#understanding-the-extension-project-structure).

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

## Create a command

In the extension settings, enter a command name, select **Add**, and edit the new command. Each command supports:

| Option | Description |
| --- | --- |
| Command name | The top-level command and fallback label. Names must be unique. |
| Prompt format | The exact prompt sent to the provider. Every `{}` is replaced with the user's query. |
| Advanced output | Interprets a compatible JSON response as rich Command Palette metadata. |
| Send delay | Wait time after typing before a request is sent. The default is 650 ms. |
| Response variations | Number of choices requested through the OpenAI `n` field. Dedicated command pages support 1–10 results; fallback results always request one. |
| LLM provider | Provider used for this command. |
| Fallback command | Shows the command's response among results on Command Palette's root page. |
| Custom icon | Copies a supported image into the extension's local storage and uses it for the command. |

For example, a command named **Summarize** might use:

```text
Summarize the following text in one concise sentence:

{}
```

Use `{{` and `}}` when the prompt itself needs literal braces. A template may contain `{}` more than once; `{0}` and unmatched braces are rejected.

## Use a command

There are two ways to invoke a configured tool:

1. Select its top-level command, type the input, and wait for one or more response items.
2. If fallback mode is enabled, type directly on Command Palette's root page and select the matching inline result.

Selecting a completed response copies its full text to the clipboard. When a query changes or you move between the root and a command page, the extension cancels work that is no longer relevant so an older response cannot replace the current one.

If the endpoint is offline, the command page displays its connection state and retries periodically. Root fallback results remain hidden until the assigned provider is available.

## Advanced output

Advanced output lets the model control how a result appears in Command Palette. Enable it for a command and explicitly ask the model to return a JSON object using these optional, case-sensitive fields:

```json
{
  "title": "Result title",
  "subtitle": "Supporting text",
  "details": "Full content shown in details and copied when selected",
  "section": "Section name",
  "tags": ["tag one", "tag two"]
}
```

`title`, `subtitle`, `details`, and `section` must be strings; `tags` must be an array of strings. Extra properties are ignored. Plain JSON and a single unlabelled or `json` Markdown code fence are accepted.

The extension deliberately does not inject formatting instructions into prompts. A practical prompt therefore ends with something like:

```text
Return only a JSON object with lowercase title, subtitle, details, section, and tags fields.
```

If parsing fails, the response is still shown using the normal text-result layout instead of being discarded.

## Local data and privacy

Configuration is stored under `%LOCALAPPDATA%\CommandPaletteLLM`:

```text
CommandPaletteLLM/
├── commands.json
├── providers.json
└── Icons/
```

API keys are stored in `providers.json` as local plain text. Treat that file as sensitive and do not commit or share it. Prompts are sent only to the provider assigned to the command; the extension has no separate telemetry or cloud backend.

## Publish

Create a trimmed x64 publish output with:

```powershell
dotnet publish CommandPaletteLLM/CommandPaletteLLM.csproj -c Release -p:Platform=x64 -r win-x64
```

Use `ARM64` and `win-arm64` for an ARM64 package. Release publishing and signing still require the appropriate publisher identity and certificate; the development manifest values are not production publishing credentials.

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

Uninstall **Command Palette LLM** from **Windows Settings → Apps → Installed apps**, then reload Command Palette. This removes the registered package; repository files and the local configuration directory remain separate.

## Project structure

```text
CommandPaletteLLM.sln
├── CommandPaletteLLM/          Extension, settings UI, API client, and MSIX metadata
│   ├── Assets/                 App and package artwork
│   └── Pages/                  Command and settings pages
└── CommandPaletteLLM.Tests/    xUnit behavior and integration-style unit tests
```

## License

Copyright © 2026. All rights reserved. This repository is proprietary software; viewing the source does not grant permission to use, copy, modify, compile, or distribute it. See [LICENSE](LICENSE) for the complete terms and licensing contact guidance.
