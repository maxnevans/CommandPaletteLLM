# Microsoft Store listing — English (United States)

## Product name

Command Palette LLM

## Description

Command Palette LLM Preview integrates with Microsoft PowerToys Command Palette
to turn local language models into fast, reusable productivity commands.

Connect an OpenAI-compatible endpoint running on your own PC, create focused
commands such as summarize or rewrite, and invoke them without leaving Command
Palette. You control each prompt template, model, delay, result count, and where
the command appears. Hosted OpenAI-compatible endpoints are also supported.

Provider credentials are encrypted for your Windows account. The extension has
no publisher-operated backend, advertising, analytics, or telemetry. Prompts go
directly to the endpoint you configure.

This is preview software. Microsoft PowerToys with Command Palette enabled is
required and is not installed by this package. Use is permitted only for
personal, educational, charitable, and nonprofit purposes; commercial and
for-profit use requires separate authorization.

Command Palette LLM is an independent third-party extension and is not
affiliated with or endorsed by Microsoft or OpenAI.

## Product features

- Create reusable prompt-driven Command Palette commands without changing code
- Connect multiple local or hosted OpenAI-compatible providers
- Keep provider API keys encrypted with Windows per-user protection
- Send prompts directly to the provider configured for each command
- Display plain text or structured titles, subtitles, details, sections, and tags
- Request multiple response variations and copy results immediately
- Assign custom icons and choose top-level or fallback command placement
- Keep commands and provider configuration locally on the PC

## Category

Productivity

## Search terms

command palette; local LLM; AI; prompts; productivity; PowerToys; OpenAI compatible

## Website

https://github.com/maxnevans/CommandPaletteLLM

## Support

https://github.com/maxnevans/CommandPaletteLLM/issues

## Privacy policy

https://github.com/maxnevans/CommandPaletteLLM/blob/main/PRIVACY.md

## Additional license terms

https://github.com/maxnevans/CommandPaletteLLM/blob/main/BINARY-LICENSE.txt

## Pricing and availability

- Base price: Free
- Trial: None
- Markets: All eligible markets
- Audience: Public
- Discoverability: Available and discoverable in Microsoft Store
- Release: As soon as certification completes
- Stop acquisition: Never

## Certification notes / additional testing information

Command Palette LLM is an extension for Microsoft PowerToys Command Palette and
does not expose a standalone application window. Install the latest stable
PowerToys, enable Command Palette, and reload Command Palette extensions after
installing this package.

Open Command Palette, open Settings, select Command Palette LLM, and configure a
provider. The default URL expects an OpenAI-compatible local endpoint at
`http://127.0.0.1:8080/v1`. Create a command containing `{}` in its prompt
template, save it, reload the extension if necessary, and invoke the command
from Command Palette. Certification can validate the settings experience
without an external account. Network completions require a tester-controlled
OpenAI-compatible endpoint.

The package supports x64 and ARM64 Windows 11 build 22000 or newer. It declares
internet access because users may configure a network endpoint. It contains no
publisher telemetry or backend.

## Screenshot order and captions

1. Create reusable AI commands directly in Command Palette.
2. Connect a local OpenAI-compatible model while keeping control of your data.
3. Invoke focused tools without leaving your keyboard-driven workflow.
4. Turn model output into rich, structured Command Palette results.
