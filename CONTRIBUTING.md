# Contributing

## Before opening a change

1. Search existing issues and pull requests.
2. Keep changes focused and avoid committing generated output, credentials, signing keys, or local Visual Studio state.
3. For behavior changes, add or update a focused test and documentation.

## Validation

From a Windows developer prompt, build the native library for Win32 and x64, build `WinUiAuto.sln`, and pack `WinUiAuto.Windows` as described in the root README. UI Automation and MCP checks require an interactive, unlocked desktop.

## Pull requests

Describe the user-visible change, affected projects, validation performed, and any architecture or Windows-version constraints. Do not include API keys, certificates, private signing material, or screenshots containing sensitive data.
