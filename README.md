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

## Studio recording workflows

Build the native x64 library and managed solution using the commands above, then launch `WinUiAutomation.Studio.exe` from the solution's x64 Release output. Recording and playback require an interactive, unlocked Windows desktop. In Studio, use **Record** to capture actions, select recorded tasks to inspect their node paths, use **Verify** from the node-path text box's context menu to check a path, and use **Play** to replay the recording.

### 1. Generate and run C# automation

The generated C# sample in [`templates/WinUiAutomation.GeneratedApp/Program.cs`](templates/WinUiAutomation.GeneratedApp/Program.cs) demonstrates a Search-to-Copilot recording. To create or update it:

1. Start Studio and begin a new recording.
2. Left-click the **Search** button.
3. Type `copilot` and press **Return**.
4. Wait for the Copilot app to start.
5. Right-click the taskbar button **Microsoft 365 Copilot - 1 running window pinned**.
6. Left-click **Close window** in the context menu.
7. Click **Stop recording**.
8. Click **C#** to generate the method and copy it to the clipboard. Paste the generated code into `Program.cs`, replacing the sample method as needed.
9. Name the method `RunGeneratedAutomation_SearchCopilot`; make sure `Main` calls it.
10. Run the generated app:

```powershell
dotnet run --project .\templates\WinUiAutomation.GeneratedApp\WinUiAutomation.GeneratedApp.csproj -c Release
```

The Search/Copilot targets depend on the Windows version and installed apps, so verify or update the recorded paths on the machine where the automation will run. Review generated code before running: it sends real mouse and keyboard input.

### 2. Save an automation skill and use it in an AI workflow

Studio's **Skill** button creates versioned automation-skill JSON (`.winui-skill.json`), not a Markdown `SKILL.md` file. The JSON is the executable recording that the repository's runtime and MCP server understand.

1. Record and verify the UI actions as in the first scenario, then stop recording.
2. Click **Skill**. Studio copies the versioned skill JSON to the clipboard and displays it in the **Skill JSON** tab.
3. Double-click the top **Node Path** text box. If it is empty, choose a saved `.winui-skill.json` file to load. If it contains recorded UI node paths, choose a location to save the recorded UI paths and actions as a `.winui-skill.json` file, such as `SearchCopilot.winui-skill.json`.
4. Configure an MCP-capable AI client to start the repository's server in the same interactive desktop session. The server command is:

```powershell
dotnet run --project .\WinUiAutomation.McpServer\WinUiAutomation.McpServer.csproj -c Release
```

5. Give the AI client access to the saved JSON. Ask it to review the skill and invoke the MCP `run_skill` tool with the parsed contents of the file as the `skill` parameter. The client must pass the file contents; the MCP server does not automatically load a file by path. The client can first request `get_snapshot` to inspect the current desktop and use `playbackOptions.dryRun` to validate targets without sending input.
6. Check the `run_skill` result and any errors before relying on the workflow. A non-dry run performs real UI actions. Keep the desktop unlocked and review the skill before allowing execution.

The MCP server provides the execution tools; the AI client supplies the model and workflow. See [`WinUiAutomation.McpServer`](WinUiAutomation.McpServer/README.md) for hosting notes and [`WinUiAutomation.Runtime`](WinUiAutomation.Runtime/README.md) for the skill format and playback options.

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
