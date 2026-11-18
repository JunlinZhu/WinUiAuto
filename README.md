# WinUiAuto

Windows UI Automation libraries and tools for capturing, locating, inspecting, and replaying UI elements through reusable node paths.

## Projects

- `UiAutoLib` - native Windows UI Automation and input library for x86 and x64.
- `WinUiAuto.Windows` - packable .NET 8 Windows API with native runtime assets.
- `WinUiAutomation.Runtime` - bounded playback and versioned automation skills.
- `WinUiAutomation.McpServer` - JSON-RPC/stdio tools for snapshots, locating, actions, screenshots, and skill execution.
- `WinUiAutomation.Runtime.Tests` - deterministic runtime tests using `IAutomationDriver` fakes.
- `WinUiAutomation.Studio` - WPF recorder and editor.
- `templates\WinUiAutomation.GeneratedApp` - console template for running C# copied from Studio's Generate Code command.

## Requirements

- Windows 10 or later
- Visual Studio 2022 or later with Desktop C++, .NET desktop, and WPF workloads
- .NET 8 SDK
- An interactive, unlocked Windows desktop session for UI Automation and MCP use

## Build

Build the native library for both architectures, then build the managed solution:

```powershell
msbuild .\UiAutoLib\UiAutoLib.vcxproj /t:Build /p:Configuration=Release /p:Platform=Win32 /p:PlatformToolset=v143 /m
msbuild .\UiAutoLib\UiAutoLib.vcxproj /t:Build /p:Configuration=Release /p:Platform=x64 /p:PlatformToolset=v143 /m
msbuild .\WinUiAuto.sln /t:Restore,Build /p:Configuration=Release /p:Platform=x64 /m
```

The same commands are run by [GitHub Actions](.github/workflows/build.yml). Legacy projects are kept in `UieFinder.sln`; the supported package and automation projects are in `WinUiAuto.sln`.

## Package

Build the native libraries first, then create the NuGet package:

```powershell
dotnet pack .\WinUiAuto.Windows\UiAutomation.Windows.csproj -c Release -o .\artifacts\packages
```

See [`docs/GETTING-STARTED.md`](docs/GETTING-STARTED.md) for C# and PowerShell examples and [`docs/PUBLISHING.md`](docs/PUBLISHING.md) for release steps.

## Security and permissions

UI automation can read and modify the active desktop. Run tools with the least privilege required, review automation actions before execution, and never place model credentials in source control. See [`SECURITY.md`](SECURITY.md).

## Contributing

Please read [`CONTRIBUTING.md`](CONTRIBUTING.md) before opening an issue or pull request. Changes should build from a clean checkout and should include focused tests or documentation when behavior changes.

## License

This project is licensed under the MIT License. See [`LICENSE`](LICENSE).
