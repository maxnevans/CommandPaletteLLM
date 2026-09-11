# Repository Guidelines

## Project Structure & Module Organization

`CommandPaletteLLM.sln` contains one Windows Command Palette extension targeting .NET 10. Source lives in `CommandPaletteLLM/`: `Program.cs` hosts the COM server, `CommandPaletteLLM.cs` implements the extension entry point, and `CommandPaletteLLMCommandsProvider.cs` exposes commands. Put pages in `CommandPaletteLLM/Pages/` and images in `CommandPaletteLLM/Assets/`. MSIX metadata is defined by `Package.appxmanifest` and `app.manifest`; publish settings live under `Properties/PublishProfiles/`. Central build and dependency settings belong in `Directory.Build.props` and `Directory.Packages.props`.

Tests live in the sibling `CommandPaletteLLM.Tests/` project and are included in the solution.

## Build, Test, and Development Commands

- `dotnet restore CommandPaletteLLM.sln` restores packages through the repository's `nuget.config`.
- `dotnet build CommandPaletteLLM.sln -c Debug -p:Platform=x64` builds the common local configuration and runs the configured .NET analyzers.
- `dotnet build CommandPaletteLLM.sln -c Release -p:Platform=x64` validates trimming and release packaging constraints.
- `dotnet test CommandPaletteLLM.sln -c Debug -p:Platform=x64` runs all test projects once tests are added.
- `dotnet publish CommandPaletteLLM/CommandPaletteLLM.csproj -c Release -p:Platform=x64 -r win-x64` creates an x64 publish output.

Use Visual Studio's Debug/x64 configuration to deploy and exercise the extension. Build ARM64 by replacing `x64` with `ARM64` and `win-x64` with `win-arm64` where applicable.

## Coding Style & Naming Conventions

Follow the existing C# style: four-space indentation, file-scoped namespaces, nullable reference types, and one primary type per file. Use `PascalCase` for types, methods, and public members; use `_camelCase` for private fields and `camelCase` for parameters and locals. Prefer concise expression-bodied members where readability improves. Keep toolkit pages under `Pages/`, and run `dotnet format CommandPaletteLLM.sln` before submitting broad formatting changes.

## Command Palette SDK Integration

Before implementing extension-owned settings, routing, aliases, fallback controls, persistence, or other platform behavior, check whether Command Palette already provides a native mechanism. Use the native mechanism as the single source of truth and do not add a duplicate extension control. If a native mechanism cannot meet a requirement, document the gap and user-visible tradeoff before introducing custom behavior.

## Testing Guidelines

Use a .NET test framework (xUnit is preferred) and name test classes after the subject, for example `CommandPaletteLLMPageTests`. Name tests by behavior, such as `GetItems_ReturnsPlaceholderCommand`. Cover new command routing and page-item behavior; manually verify registration, icons, and packaged launch behavior in Command Palette.

## Commit & Pull Request Guidelines

The repository has no commit history yet, so no established convention exists. Use short, imperative subjects (for example, `Add prompt history command`) and keep commits focused. Pull requests should explain user-visible behavior, list build and test commands run, link related issues, and include screenshots for Command Palette UI changes. Call out manifest, package-version, runtime-identifier, or publish-profile changes explicitly.

## Security & Configuration

Do not commit credentials, signing certificates, generated packages, or user-specific Visual Studio files. Keep package versions centralized and review NuGet audit output when dependencies change.
