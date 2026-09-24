<p align="center">
  <img src="CommandPaletteLLM/Assets/AppIcon.master.png" alt="Command Palette LLM icon" width="128">
</p>

<h1 align="center">Command Palette LLM</h1>

<p align="center">
  Turn any OpenAI-compatible LLM into fast, reusable commands inside PowerToys Command Palette.
</p>

Command Palette LLM is a Windows Command Palette extension for creating small, focused AI tools: summarize selected text, rewrite a sentence, generate several alternatives, classify input into rich results, or build any other prompt-driven command you need. It works with local servers such as llama.cpp as well as hosted services that expose an OpenAI-compatible Chat Completions API.

> [!WARNING]
> **Alpha / pre-release software:** Command Palette LLM is not a stable release.
> Features, behavior, and the settings format may change without backward
> compatibility. Before updating, use **Settings → Export** and keep the
> exported file in a safe place. A future alpha version may require manual
> settings migration even when a backup is available.

## Install

Install Command Palette LLM from Microsoft Store using WinGet:

```powershell
winget install --id 9PK5TNWKQ00Q --source msstore
```

After the community manifest is accepted, the human-readable WinGet package can be installed with:

```powershell
winget install --id maxnevans.CommandPaletteLLM --source winget
```

> [!CAUTION]
> The community WinGet installer is unsigned. It uses a self-signed,
> metadata-only sparse package to register the identity Command Palette needs
> for discovery. Setup requests administrator approval to trust that package
> certificate in Windows' machine-wide Trusted People store and removes it on
> uninstall. Windows can still show a SmartScreen warning or block the installer
> under organization policy.

Microsoft PowerToys with Command Palette enabled is required. You can also use the [Microsoft Store listing](https://apps.microsoft.com/detail/9PK5TNWKQ00Q) or see the [user manual](docs/manual.md) for more information. Do not keep the Store and community WinGet variants installed at the same time.

## Features

- Create and edit commands without changing code.
- Choose a single prompt or an ordered pipeline with named steps and per-step providers and request settings.
- Save reusable user-step templates and copy them into new pipeline steps.
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

## Documentation

- [User manual](docs/manual.md) — installation, provider and command configuration, pipelines, advanced output, privacy, and user troubleshooting.
- [Build and run a development build](docs/build.md) — prerequisites, restore, build, test, deployment, and development troubleshooting.
- [Publishing](docs/publishing.md) — Microsoft Store and WinGet setup, release tagging, publication, and verification.

## Responsible use and disclaimer

Command Palette LLM is a general-purpose interface to language-model providers.
It does not verify or endorse provider responses. Model output may be inaccurate,
incomplete, misleading, unsafe, or unsuitable for its intended purpose. Review
and independently verify output before relying on it, especially for
safety-critical, legal, medical, financial, or security decisions.

You are responsible for the prompts, providers, models, endpoints, credentials,
and request settings you configure; for provider terms, privacy practices,
availability, and charges; for complying with applicable laws and third-party
rights; and for how you use generated output or act on it. Do not use the
application to violate the law or harm others.

To the fullest extent permitted by applicable law, the publisher is not
responsible for loss, damage, claims, costs, or other consequences resulting
from use of the application, configured services, generated output, or actions
taken based on that output. This notice does not exclude liability that cannot
lawfully be excluded.

## License

Copyright © 2026 maxnevans. The source code is proprietary; viewing it does not
grant permission to use, copy, modify, compile, or distribute it.
