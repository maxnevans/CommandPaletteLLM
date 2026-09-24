# Build and run a development build

## Requirements

To use a development build, you need:

- Windows 11 with [PowerToys](https://learn.microsoft.com/windows/powertoys/install) and Command Palette installed.
- Windows Developer Mode enabled.
- An OpenAI-compatible endpoint with `/v1/models` and `/v1/chat/completions` routes.

To build the extension, you also need:

- .NET 10 SDK.
- Visual Studio with C# Windows App SDK/WinUI and MSIX packaging support. Microsoft lists the current environment requirements in its [Command Palette extension guide](https://learn.microsoft.com/windows/powertoys/command-palette/creating-an-extension).

The solution targets x64 and ARM64.

## Restore, build, and test

Clone or open the repository, then restore and verify it:

```powershell
dotnet restore CommandPaletteLLM.sln
dotnet build CommandPaletteLLM.sln -c Debug -p:Platform=x64
dotnet test CommandPaletteLLM.sln -c Debug -p:Platform=x64
```

## Deploy and run

Open `CommandPaletteLLM.sln` in Visual Studio, select **Debug** and **x64**, then choose **Build → Deploy CommandPaletteLLM**. A normal build produces the binaries but does not register the extension with Windows.

After the first deployment—or after redeploying a changed build—open Command Palette, run **Reload Command Palette Extension**, and select **Command Palette LLM (Development)**. Visual Studio uses the isolated `maxnevans.CommandPaletteLLM.Development` package identity, so this development deployment does not replace the Store package or share its settings. Microsoft documents this development loop in [How to create Command Palette extensions](https://learn.microsoft.com/windows/powertoys/command-palette/creating-an-extension#understanding-the-extension-project-structure).

If you previously deployed **Command Palette LLM (Visual Studio)**, uninstall it once from **Windows Settings → Apps → Installed apps** before deploying the renamed development package. Windows treats the new package identity as a separate installation.

For ARM64, replace `x64` with `ARM64` in the commands and Visual Studio configuration.

## Development data

Configuration files are created only after settings are saved. An unpackaged development or EXE build stores them under `%LOCALAPPDATA%\CommandPaletteLLM`.

The development package has an isolated package identity and does not share settings with the Store package.

## Troubleshooting

### The extension does not appear

- Confirm that you used **Deploy**, not only **Build** or the unpackaged launch profile.
- Run **Reload Command Palette Extension** after deployment.
- Make sure the selected Visual Studio platform matches the machine architecture.

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
