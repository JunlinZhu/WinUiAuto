# WinUiAutomation.McpServer

This console host exposes the runtime over newline-delimited JSON-RPC on stdin/stdout. It provides `get_snapshot`, `query_snapshot`, `run_skill`, `locate`, `perform_action`, and `get_screenshot`; responses use the existing `UiSnapshot`, `UiLocateResult`, `PlaybackResult`, and `SkillRunResult` JSON shapes.

Run it from an interactive Windows desktop session:

```powershell
dotnet run --project .\WinUiAutomation.McpServer\WinUiAutomation.McpServer.csproj -c Release
```

Create one host process per automation session. The process initializes native UI Automation on startup and must remain on an STA-compatible hosting thread when embedded in another server. Review skill JSON and configure `AutomationScope`/dry-run options before allowing input-producing tools.
