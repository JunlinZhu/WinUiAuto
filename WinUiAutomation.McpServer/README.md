# WinUiAutomation.McpServer

This console host exposes the runtime over newline-delimited JSON-RPC on stdin/stdout. It provides `get_snapshot`, `query_snapshot`, `run_skill`, `locate`, `perform_action`, and `get_screenshot`; responses use the existing `UiSnapshot`, `UiLocateResult`, `PlaybackResult`, and `SkillRunResult` JSON shapes.

The host has a runtime dependency on the architecture-matched `UiAutoLib.dll`, which is loaded by `WinUiAutomation.Windows` through P/Invoke. Under Visual Studio/MSBuild, `WinUiAutomation.McpServer` builds `UiAutoLib.vcxproj` first and copies the DLL into its output directory. The project reference is skipped by the .NET CLI because it cannot load Visual C++ targets; therefore, build the native project with Visual Studio/MSBuild first, then `dotnet build` can build the MCP project and copy the already-built DLL. The MCP build fails clearly if the DLL is missing.

Run it from an interactive Windows desktop session:

```powershell
dotnet run --project .\WinUiAutomation.McpServer\WinUiAutomation.McpServer.csproj -c Release
```

Create one host process per automation session. The process initializes native UI Automation on startup and must remain on an STA-compatible hosting thread when embedded in another server. Review skill JSON and configure `AutomationScope`/dry-run options before allowing input-producing tools.
