# Privacy Policy for Command Palette LLM

Effective: September 12, 2026

Command Palette LLM is published by **maxnevans**. This policy describes how
the application handles information.

## Summary

Command Palette LLM has no publisher-operated backend, advertising, analytics,
or telemetry. The publisher does not receive your prompts, responses, provider
settings, or API credentials through the application.

## Information stored on your device

The application creates configuration files only after settings are saved. The
installed Store and community builds run with package identity, so Windows
redirects command definitions, provider settings, and imported command icons
beneath the package's `LocalCache\Local\CommandPaletteLLM` directory. An
unpackaged development build uses `%LOCALAPPDATA%\CommandPaletteLLM`. Provider
API keys are encrypted for the current Windows user with Windows Data Protection
API (DPAPI). They are decrypted only in the application process when
authenticating to a provider you configured.

The application does not retain submitted prompt values or model responses.

## Information sent to configured providers

When you invoke a command, the application sends the resulting prompt directly
from your PC to the OpenAI-compatible endpoint assigned to that command. It may
also request the endpoint's `/models` route to check availability. An optional
API key is sent as a bearer credential.

You choose each endpoint by configuring and saving its URL in extension
settings, and you choose which provider each command uses. Invoking a command
sends its resulting prompt to the assigned provider. You can stop future
transmissions by assigning the command to another provider or deleting it.

The operator of a configured endpoint processes data under its own terms and
privacy policy. Review those terms before sending personal, confidential, or
sensitive information. The publisher does not control configured providers.

## Retention and deletion

Delete commands, providers, credentials, and imported icons from extension
settings when they are no longer needed. Uninstalling the MSIX package may also
remove its virtualized configuration directory. Back up that directory before
uninstalling if you need to preserve settings. For an unpackaged development
build, remove `%LOCALAPPDATA%\CommandPaletteLLM` to delete any remaining data.

## Children

The application is a general productivity tool and is not directed to children.

## Changes and support

Material changes will be published in this repository with a revised effective
date. For privacy questions, open a GitHub issue without including secrets or
personal data:

https://github.com/maxnevans/CommandPaletteLLM/issues
