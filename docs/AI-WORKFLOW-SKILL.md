# Use a WinUI automation skill in an AI workflow

This walkthrough uses [`Samples/Calculator.winui-skill.json`](../Samples/Calculator.winui-skill.json) as the example. The file is a versioned, executable WinUI automation skill. An AI client does not need to generate mouse coordinates or keyboard events itself; it can inspect the skill, check the current desktop, and ask the MCP server to execute the recorded workflow.

## What the example does

The Calculator skill records this sequence:

1. Click the Windows Search button on the taskbar.
2. Type `CALCULATOR` and press Enter. The keyboard input is stored in `keyboardInputBase64`.
3. Click **Open Navigation** in Calculator.
4. Click **Scientific Calculator**.
5. Click **1**, **Plus**, **2**, and **Equals**.
6. Locate the Calculator result text, whose recorded name is `Display is 3`.
7. Close Calculator.

Each `leftClick` contains a node path: an ordered list of UI Automation elements from the desktop to the target control. The path uses stable properties such as `controlType`, `automationId`, `className`, and `name`; the `x`, `y`, `w`, and `h` values are recorded bounds and are not a substitute for resolving the path at run time.

The sample has `schemaVersion: 2` and currently has an empty `completionAssertions` array. Therefore, the AI workflow should explicitly verify the result after playback, or add a version-2 completion assertion when creating a derived skill.

## Prerequisites

- Windows 10 or later with an interactive, unlocked desktop.
- A built repository and .NET 8 SDK.
- An MCP-capable AI client that can start a local stdio server and call its tools.
- Calculator available on the machine.

## New developer: exact local Ollama walkthrough

This section describes the prompt-driven agent setup first. The commands below use `12*34`; replace the expression with another supported expression when needed.

### Important architecture note

There are two separate local components:

1. **Ollama agent:** the `winui-skill-agent` model contains the agent system prompt and decides how to update and validate a skill.
2. **MCP-capable host:** a host application must expose `get_snapshot`, `query_snapshot`, `locate`, and `run_skill` to the agent and forward the complete updated skill JSON to `WinUiAutomation.McpServer`.

The plain `ollama run` command can converse with the agent, but it cannot start or register MCP tools by itself. Registering the Ollama model is therefore necessary for the prompt, but it is not sufficient for real UI execution. Use `Samples\OllamaMcpHost` for a no-Python .NET host, or use the Python bridge as a reference adapter.

### Can Python be removed?

Yes. The repository includes `Samples\OllamaMcpHost`, a C#/.NET replacement for the Python adapter. The `OllamaWinUiSkillAgent.Modelfile` and `.winui-skill.json` files still cannot replace an MCP client by themselves:

- The **Modelfile** defines the Ollama model, system prompt, and model parameters.
- The **skill JSON** defines recorded UI tasks and targets.
- An **MCP client/host** must still start `WinUiAutomation.McpServer`, expose its tools to the model, forward tool calls, and return tool results to Ollama.

Therefore this command is useful for conversation and planning only:

```cmd
ollama run winui-skill-agent
```

It cannot click Calculator or execute `run_skill` on its own. To avoid Python, use the included .NET host:

1. `Samples\\OllamaMcpHost`, which uses Ollama's HTTP API and the MCP stdio client.
2. An MCP-capable desktop AI client configured with the `winui-skill-agent` model.
3. An MCP integration built into an IDE or local agent framework.

The replacement host must implement the same responsibilities: load the skill JSON, send Ollama the prompt and available MCP tools, forward `run_skill` with the complete updated skill object, and enforce dry-run plus approval before real input. No Python-specific behavior is required by `WinUiAutomation.McpServer`.

### 1. Open Command Prompt window 1 and start Ollama

Press **Windows+R**, type `cmd`, and press Enter. Run:

```cmd
cd /d C:\src\WinUiAuto
ollama serve
```

Leave this window open. If Ollama is already running and reports that port `11434` is in use, leave the existing Ollama service running and continue.

### 2. Open Command Prompt window 2 and enter the repository

Open another Command Prompt and run:

```cmd
cd /d C:\src\WinUiAuto
```

All remaining commands run in this window.

### 3. Confirm the model and native runtime are ready

Run:

```cmd
ollama pull qwen3
msbuild .\UiAutoLib\UiAutoLib.vcxproj /t:Build /p:Configuration=Release /p:Platform=x64 /m
dotnet build .\WinUiAutomation.McpServer\WinUiAutomation.McpServer.csproj -c Release -p:Platform=x64
```

The native build must complete before the MCP server can load `UiAutoLib.dll`. If `dotnet build` reports that Visual C++ targets are unavailable, run the native and managed builds from the **Developer Command Prompt for Visual Studio** instead.

### 4. Create and register the local Ollama agent

Run:

```cmd
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile
ollama list
```

`winui-skill-agent` is now registered in the local Ollama model store. Test the prompt without UI execution:

```cmd
ollama run winui-skill-agent "Explain how you would safely adapt Samples\\Calculator.winui-skill.json for 12*34. Do not execute anything."
```

The response should describe loading the skill, replacing the arithmetic tasks with `1`, `2`, `*`, `3`, `4`, and `Equals`, dry-running, requesting approval, and verifying the result.

### 5. Register MCP tools with the agent host

"Configure the MCP host" means telling the application that owns the agent conversation how to launch the WinUiAutomation MCP server. This is **not** configured in the Ollama Modelfile or in `Calculator.winui-skill.json`.

Depending on the host, configuration is either entered in a settings dialog or stored in a host-specific JSON file. There is no universal MCP configuration-file location. If the host provides a UI, enter these values:

```text
Server name: WinUiAutomation
Command: dotnet
Arguments: run --project C:\src\WinUiAuto\WinUiAutomation.McpServer\WinUiAutomation.McpServer.csproj -c Release -p:Platform=x64 --no-build
Working directory: C:\src\WinUiAuto
```

If the host requests JSON, a typical equivalent entry is:

```json
{
  "mcpServers": {
	"WinUiAutomation": {
	  "command": "dotnet",
	  "args": [
		"run",
		"--project",
		"C:\\src\\WinUiAuto\\WinUiAutomation.McpServer\\WinUiAutomation.McpServer.csproj",
		"-c",
		"Release",
		"-p:Platform=x64",
		"--no-build"
	  ],
	  "cwd": "C:\\src\\WinUiAuto"
	}
  }
}
```

Use the file location and property names required by your selected host; some hosts call `cwd` `workingDirectory`, and some do not support a working-directory property. Do not create a repository-root `mcp.json` unless the host documentation specifically says to use that location.

Expose these MCP tools to `winui-skill-agent`:

```text
get_snapshot
query_snapshot
locate
run_skill
perform_action
get_screenshot
```

The host must load the contents of `Samples\\Calculator.winui-skill.json`, include those contents in the agent context, and send any derived skill object as the `skill` property of `run_skill`. Passing only a filename will not work.

If the host supports an agent configuration, set its system prompt to the contents of:

```text
Samples\\OllamaWinUiSkillAgent.prompt.md
```

Also provide these supporting skill instructions as agent resources:

```text
Samples\\AgentSkills\\WinUiSkillEditor.skill.md
Samples\\AgentSkills\\CalculatorExpression.skill.md
Samples\\AgentSkills\\WinUiSkillExecutor.skill.md
```

### 6. Export and inspect an AI-updated Calculator skill

The original recording is fixed to `1 + 2`. Generate a derived skill for `12*34` without changing the original file:

```cmd
python .\Samples\OllamaCalculatorWorkflow.py --skill .\Samples\Calculator.winui-skill.json --expression "12*34" --output-skill .\Samples\Calculator-12x34.winui-skill.json --export-only
```

Open the result in an editor:

```cmd
notepad .\Samples\Calculator-12x34.winui-skill.json
```

Confirm the arithmetic tasks are:

```text
num1Button → num2Button → multiplyButton → num3Button → num4Button → equalButton
```

Close Notepad after reviewing the file. The source `Calculator.winui-skill.json` remains unchanged.

### 7. Read the agent prompt and supporting skills

The reusable agent instructions are:

```text
Samples\OllamaWinUiSkillAgent.prompt.md
Samples\AgentSkills\WinUiSkillEditor.skill.md
Samples\AgentSkills\CalculatorExpression.skill.md
Samples\AgentSkills\WinUiSkillExecutor.skill.md
```

View the main prompt with:

```cmd
notepad .\Samples\OllamaWinUiSkillAgent.prompt.md
```

These files define how an MCP-capable agent should load a skill, derive updated tasks, dry-run it, request approval, execute it, and verify the result. They are prompt resources; the existing Python bridge is still the runnable local adapter.

### 8. Start the agent conversation through the MCP-capable host

In the MCP-capable host configured in step 5, select the registered model `winui-skill-agent`, attach or load:

```cmd
Samples\\Calculator.winui-skill.json
```

Send this user request:

```text
Load Samples\\Calculator.winui-skill.json. Adapt it to calculate 12*34. Create a derived skill with explicit click tasks, validate it with a dry run, show me the planned task sequence, and wait for my approval before sending real input. After execution, verify that the result is 408 and ask for approval before closing Calculator.
```

The host should start the MCP server using the command in step 5. Do not start a second MCP server if the host manages that process.

When prompted, type:

```text
RUN
```

The workflow opens Calculator, selects Scientific mode, clicks `1`, `2`, `*`, `3`, `4`, and `Equals`, then leaves the result visible. After Ollama verifies the result, type:

```text
CLOSE
```

### 9. Expected result

The Calculator display should show `408`. The terminal should show successful playback and then return to the Command Prompt prompt after cleanup.

If the `multiplyButton` task fails, stop the run, close Calculator, rebuild the native and MCP projects, and rerun from the original skill file. Do not manually skip the failed task.

### 10. No-Python .NET Ollama host

The repository includes `Samples\\OllamaMcpHost`, which provides an interactive terminal conversation without Python. It starts `WinUiAutomation.McpServer` itself, so do not start a second MCP server in another window.

Build and start it from the repository root:

```cmd
dotnet build .\\Samples\\OllamaMcpHost\\OllamaMcpHost.csproj -c Release
dotnet run --project .\\Samples\\OllamaMcpHost\\OllamaMcpHost.csproj -c Release --no-build -- --model winui-skill-agent:latest --skill .\\Samples\\Calculator.winui-skill.json
```

At the `>>>` prompt, enter:

```text
compute 45+67
```

The host forwards Ollama tool calls to MCP over stdin/stdout and prompts `RUN` before real input. Type `/quit` to exit. Ollama must be running at `http://localhost:11434`.

To let Ollama select supporting Markdown instructions for each prompt, add `--skills-folder .\Samples\AgentSkills` to the host command. The folder is cataloged recursively; contents are loaded only through the host-local `load_skill_instructions` tool when the model requests a relevant file. Multiple instruction files can be loaded per conversation. This mode routes even basic arithmetic through Ollama instead of the deterministic Calculator shortcut, and requires native Ollama tool calls. See `Samples\OllamaMcpHost\README.md` for the complete command and limitations.

### 11. Reference-only Python adapter

If an MCP-capable host is not available, the repository includes `Samples\\OllamaCalculatorWorkflow.py` as a reference adapter that connects Ollama's HTTP API to the MCP server. It is not the prompt-driven agent and is not required when using a host that supports MCP tools directly.

Build the native library and managed solution as described in the [getting-started guide](GETTING-STARTED.md). Start the MCP server from the repository root:

```powershell
dotnet run --project .\WinUiAutomation.McpServer\WinUiAutomation.McpServer.csproj -c Release
```

Keep this process attached to the same interactive Windows session as the AI client. The server reads newline-delimited JSON-RPC requests from stdin and writes responses to stdout.

## Configure the AI client

Give the client:

1. The contents of `Samples/Calculator.winui-skill.json` as a resource or file attachment.
2. The MCP server command above.
3. A policy that requires inspection and dry-run validation before real input.
4. A policy that treats `run_skill` as an input-producing operation and requires explicit approval for non-dry execution.

A useful system instruction for the AI workflow is:

> For Windows UI tasks, prefer an existing `.winui-skill.json`. Inspect its actions and targets, call `get_snapshot` or `locate` to validate the current desktop, run it with `playbackOptions.dryRun: true`, and only then request approval before running it without dry-run. After execution, verify the requested result with a snapshot or assertion. Report every failed task instead of assuming success.

## Legacy reference: Python Ollama-to-MCP adapter

Ollama is the local model server; it is not an MCP client. The included bridge script connects the two: Ollama chooses among safe tools, and the script forwards those tool calls to `WinUiAutomation.McpServer`. The script loads `Samples\Calculator.winui-skill.json` itself, so you do not have to paste the long node paths into a prompt.

Follow this exact checklist. Use two Command Prompt windows. Keep both windows open until the workflow finishes.

### 1. Open Command Prompt window 1 and start Ollama

Press **Windows+R**, type `cmd`, and press Enter. Run:

```cmd
cd /d C:\src\WinUiAuto
ollama serve
```

Leave this window running. If Ollama is already running as a Windows application, `ollama serve` may report that port `11434` is already in use; that is okay. Open a second Command Prompt window.

### 2. Open Command Prompt window 2 and go to the repository

Press **Windows+R**, type `cmd`, and press Enter. Run:

```cmd
cd /d C:\src\WinUiAuto
```

All commands below must be run in this second window.

### 3. Download the Ollama model

Run this once. It downloads the default tool-capable model used by the example:

```cmd
ollama pull qwen3
```

Wait until the download completes. If you already use a different Ollama model that supports tool calling, replace `qwen3` with that model name in both this command and step 6.

### 4. Build the native library

`WinUiAutomation.McpServer` uses native `UiAutoLib.dll` through the Windows UI automation API. The MCP project copies that DLL into its output folder, and the Python bridge starts the x64 Release output with `--no-build`, so build the native x64 DLL first. Run:

```cmd
msbuild .\UiAutoLib\UiAutoLib.vcxproj /t:Build /p:Configuration=Release /p:Platform=x64 /p:PlatformToolset=v143 /m
```

Wait for `Build succeeded`. If `msbuild` is not recognized, close window 2 and open **Developer Command Prompt for Visual Studio** from the Start menu, then run `cd /d C:\src\WinUiAuto` before repeating this step.

### 5. Build the managed solution

Because Visual Studio/MSBuild can build the native C++ project reference, use it for the first build. In the same **Developer Command Prompt for Visual Studio** window, run:

```cmd
msbuild .\WinUiAuto.sln /t:Build /p:Configuration=Release /p:Platform=x64 /m
```

Wait for `Build succeeded`. Keep the Windows desktop unlocked. Close any existing Calculator window; the skill searches for Calculator through Windows Search.

After the native DLL has been built, this also works from a regular Command Prompt with the .NET CLI:

```cmd
dotnet build .\WinUiAutomation.McpServer\WinUiAutomation.McpServer.csproj -c Release -p:Platform=x64
```

After a successful build, this file should exist:

```text
C:\src\WinUiAuto\WinUiAutomation.McpServer\bin\x64\Release\net8.0-windows\UiAutoLib.dll
```

If it does not exist, do not start the Python workflow. Rebuild step 4 and confirm that the native build output is `C:\src\WinUiAuto\UiAutoLib\x64\Release\UiAutoLib.dll`.

### 6. Start the Ollama session

Run this command in Command Prompt window 2:

```cmd
python .\Samples\OllamaCalculatorWorkflow.py --model qwen3 --skill .\Samples\Calculator.winui-skill.json
```

To calculate a different expression, add `--expression`. The saved skill is not modified; the bridge keeps its launch, Scientific-mode, result, and cleanup tasks, and replaces the recorded `1 + 2` button clicks with generated keyboard input:

```cmd
python .\Samples\OllamaCalculatorWorkflow.py --model llama3.2:latest --skill .\Samples\Calculator.winui-skill.json --expression "12*34"
```

The expression supports digits, decimal points, spaces, and `+`, `-`, `*`, and `/` operators. To export the AI/host-updated skill instead of running it, use:

```cmd
python .\Samples\OllamaCalculatorWorkflow.py --skill .\Samples\Calculator.winui-skill.json --expression "12*34" --output-skill .\Samples\Calculator-12x34.winui-skill.json --export-only
```

The exported skill contains explicit click tasks in this order: `1`, `2`, `*`, `3`, `4`, `Equals`. The original `Calculator.winui-skill.json` remains unchanged.

The script now starts the repository MCP server automatically. Do not start a third MCP server manually.

### 7. Follow the prompts

The local Ollama model performs these steps:

1. Ollama calls `get_desktop_snapshot`; the bridge sends `get_snapshot` to the MCP server.
2. Ollama calls `dry_run_calculator_skill`; the bridge sends only the first two Search/launch tasks to `run_skill` with `dryRun: true`. A complete skill cannot be dry-run as one batch when later tasks depend on UI state created by earlier tasks.
3. If the launch preflight succeeds, the bridge immediately prints `The dry run passed. Type RUN to send real input to Calculator:`. It does not wait for Ollama to request the next state-changing operation. Type `RUN` and press Enter to permit real mouse and keyboard input.
4. The bridge runs the saved launch and Scientific-mode tasks, enters the requested expression, presses Equals, and leaves the result visible because the supplied skill has no completion assertion and otherwise closes the window immediately.
5. Ollama calls `read_calculator_result`; the bridge resolves the result target from the saved skill and returns its UI state.
6. Ollama reports the result, then window 2 prints `Type CLOSE to close Calculator:`. Type `CLOSE` and press Enter to execute the saved final close task.

The model is not asked to reproduce the Base64 keyboard data or node paths. The bridge reads and forwards them unchanged from `Calculator.winui-skill.json`.

At the end, window 2 should return to the `C:\src\WinUiAuto>` prompt. You can then close both Command Prompt windows.

### 8. What success looks like

The final Ollama response should say that Calculator was opened in Scientific mode, `1 + 2` was entered, and the display showed `3`. The terminal should show no tool failure and should return to the Command Prompt prompt after the cleanup approval.

If the launch preflight reports a missing target, stop. Check that the desktop is unlocked and that Windows Search is available, then re-record the affected task in Studio rather than allowing the real run. Later tasks are resolved during the approved sequential playback after Calculator has been opened.

### 9. Troubleshooting

- **`Could not reach Ollama`:** start Ollama or `ollama serve`, then retry.
- **Model not found:** run `ollama pull qwen3` or pass the exact installed model name with `--model`.
- **MCP server exits immediately:** rebuild the x64 native library and solution, then rerun the Python script from the repository root.
- **The script appears to hang before any prompt:** wait for the `Ollama: response received` and `MCP: ... complete` messages. The bridge sends only a small relevant-control summary instead of the full desktop tree and prints the request byte size. If the second request is unexpectedly large, update the bridge or check that Ollama is responding at `http://localhost:11434/api/tags`.
- **The request after `MCP: run_skill complete` times out:** the bridge now sends only per-task statuses and counts to Ollama, not the recorded node paths. Confirm that the printed request size stays small (normally a few kilobytes), then retry.
- **`Type RUN` never appears:** inspect the launch preflight result printed by the bridge and do not approve execution.
- **Calculator is already open:** close it and rerun from a clean desktop; the first recorded task searches for and opens Calculator.

The bridge source is [`Samples/OllamaCalculatorWorkflow.py`](../Samples/OllamaCalculatorWorkflow.py). It uses only Python's standard library; no Ollama Python package is required.

For a prompt-driven agent that can load and derive skills before sending them to MCP, use [`Samples/OllamaWinUiSkillAgent.prompt.md`](../Samples/OllamaWinUiSkillAgent.prompt.md) together with the reusable instructions in [`Samples/AgentSkills`](../Samples/AgentSkills). The prompt requires the agent to validate the derived skill, request approval, send the updated JSON to MCP, and verify the result.

## Recommended AI workflow

### 1. Understand the user's goal

For a request such as “open Calculator, switch to Scientific mode, calculate 1 + 2, and tell me the result,” the AI should identify that the supplied skill already covers the interaction. It should not rewrite the node paths or decode and regenerate the keyboard input.

### 2. Parse and inspect the skill

The AI reads the JSON and checks:

- `format` is `win-ui-automation-skill`.
- `schemaVersion` is supported (`2` in this example).
- Every task has a supported `action`.
- Click tasks have a non-empty `target` node path.
- Keyboard input is present only as Base64 data and is passed through unchanged.
- Delays and offsets are reasonable for the current workflow.
- `completionAssertions` is empty, so a post-run verification is required.

The client passes either the parsed JSON object or the JSON text as the `skill` property. The MCP server accepts both forms and does not load a file by path.

### 3. Inspect the current desktop

Call the MCP `get_snapshot` tool before execution. Use the returned tree to confirm that the expected desktop and Search control are present. If the user supplied a root window or an automation scope, pass it in the request so the AI cannot accidentally target an unrelated application.

Conceptually, the request is:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "get_snapshot",
  "params": {
	"options": {
	  "maxDepth": 8
	}
  }
}
```

The exact snapshot option names depend on the client/server version. If the snapshot does not show the expected Search button or Calculator window, stop and ask the user to prepare the desktop rather than executing blindly.

### 4. Resolve important targets

Before playback, the AI can call `locate` for a target node path, especially the first Search button and the final `CalculatorResults` text. This confirms that the recorded path resolves on the current machine. A failed locate indicates that the recording is stale, the application is not ready, or the desktop layout/application version differs.

### 5. Perform a dry run

For a stateful recording such as Calculator, do not dry-run the complete task list as one batch. Dry-run the initial launch prefix, then execute the dependent tasks only after the user approves real input:

```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "run_skill",
  "params": {
	"skill": {
	  "format": "win-ui-automation-skill",
	  "schemaVersion": 2,
	  "name": null,
	  "description": null,
		"tasks": [
		"the first two tasks from Calculator.winui-skill.json"
	  ],
	  "completionAssertions": []
	},
	"playbackOptions": {
	  "dryRun": true
	}
  }
}
```

Do not replace the task array with the string shown above in a real request; it is abbreviated only to keep the example readable. The real request must contain the unchanged first two task objects from the sample file. A dry run resolves those launch targets and reports their status without sending mouse or keyboard input.

The AI should inspect the returned `SkillRunResult` and stop if parsing, locating, stability, cancellation, or assertion errors are reported.

### 6. Request approval and execute

Tell the user exactly what will happen: Calculator will be opened, Scientific mode selected, `1 + 2` entered, and Calculator closed. After approval, call the same `run_skill` request with `dryRun` omitted or set to `false`.

The server executes tasks in order. Recorded `delayBeforeMilliseconds` values are honored subject to the runtime playback limits. The AI must not issue duplicate clicks if a task partially succeeds; it should inspect the result and current snapshot first.

### 7. Verify the result

Because this sample has no completion assertion, verify the result independently:

1. Immediately after the equals task, call `get_snapshot` or `locate` for the recorded `CalculatorResults` path.
2. Confirm that the resolved text/name contains `3` (the recording reports `Display is 3`).
3. If verification must happen before Calculator closes, split the sample into an interaction skill ending at **Equals** and a separate cleanup step; the supplied sample records **Close Calculator** as its final task.
4. Report the observed result and the per-task execution status to the user.

For a reusable derived skill, add a version-2 assertion instead of relying only on the AI's interpretation:

```json
{
  "name": "Calculator displays 3",
  "kind": "textEquals",
  "target": [
	{
	  "controlType": "Pane",
	  "className": "#32769",
	  "name": "Desktop 1",
	  "position": 0
	},
	{
	  "controlType": "Window",
	  "className": "ApplicationFrameWindow",
	  "name": "Calculator",
	  "position": 1
	},
	{
	  "controlType": "Window",
	  "className": "Windows.UI.Core.CoreWindow",
	  "name": "Calculator",
	  "position": 2
	},
	{
	  "controlType": "Custom",
	  "automationId": "NavView",
	  "position": 1
	},
	{
	  "controlType": "Group",
	  "className": "LandmarkTarget",
	  "position": 1
	},
	{
	  "controlType": "Text",
	  "automationId": "CalculatorResults",
	  "position": 2
	}
  ],
  "expectedValue": "3",
  "timeoutMilliseconds": 5000,
  "pollIntervalMilliseconds": 250
}
```

Use the complete node path from the current recording when adding an assertion. The shortened properties above illustrate the shape; do not remove path segments unless `locate` confirms that the shorter path is unique and stable.

## Failure handling and safety

- **Skill validation error:** report the parser error and do not execute a partially repaired skill.
- **Target not found:** refresh the snapshot, check that the desktop is unlocked and Calculator is installed, then ask whether to re-record the affected task.
- **Unstable bounds:** wait for the application to settle and retry only according to the configured playback policy.
- **Unexpected window:** stop, capture a snapshot or screenshot, and request user review.
- **Dry-run failure:** never “try anyway” with real input.
- **Partial execution:** report the task index and result; do not automatically replay from the beginning because clicks and keyboard input may have changed application state.
- **Sensitive UI:** restrict the automation scope and do not expose screenshots, node paths, or typed text to an external service unless the user approved it.

## Minimal agent pseudocode

```text
skill = read("Samples/Calculator.winui-skill.json")
validate(skill.format, skill.schemaVersion, skill.tasks)

snapshot = mcp.get_snapshot()
confirm_expected_desktop(snapshot, skill.tasks[0])

dry_run = mcp.run_skill(skill=skill, playbackOptions={dryRun: true})
if not dry_run.succeeded:
	report(dry_run)
	stop

ask_user_for_approval("Run Calculator skill; this sends real input.")
run = mcp.run_skill(skill=skill, playbackOptions={dryRun: false})
if not run.succeeded:
	report(run)
	stop

result = mcp.locate(nodePath=calculator_results_path)
report(read_display_value(result))
```

For production workflows, prefer an assertion-bearing skill, an explicit automation scope, an audit sink, and a dedicated interactive desktop session. See the [runtime skill format](../WinUiAutomation.Runtime/README.md) and [MCP server notes](../WinUiAutomation.McpServer/README.md) for the underlying contracts.