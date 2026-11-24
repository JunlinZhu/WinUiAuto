# OllamaMcpHost

`OllamaMcpHost` is a no-Python local bridge between Ollama and `WinUiAutomation.McpServer`.

It:

- loads a `.winui-skill.json` file;
- optionally catalogs a folder of Markdown skill instructions and lets Ollama load relevant files on demand;
- starts the MCP server as a child process;
- exposes the MCP tools to Ollama through `/api/chat` tool calling;
- forwards tool calls over the MCP server's stdin/stdout JSON-RPC transport; and
- asks for `RUN` approval before a `run_skill` request with `dryRun: false`.

First build the solution as `Release|x64` in Visual Studio to produce the native `UiAutoLib.dll`. Then, from the repository root, build and run it with:

```cmd
dotnet build .\WinUiAutomation.McpServer\WinUiAutomation.McpServer.csproj -c Release -p:Platform=x64
dotnet build .\Samples\OllamaMcpHost\OllamaMcpHost.csproj -c Release
dotnet run --project .\Samples\OllamaMcpHost\OllamaMcpHost.csproj -c Release --no-build -- --model winui-skill-agent:latest --skill .\Samples\CalculatorButtonLibrary.winui-skill.json
```

The host launches MCP with `--no-build`, defaulting to `Release` and `x64`. Building the host alone does not build MCP; a MCP build without `-p:Platform=x64` uses a different output directory. If you change the host's `--configuration` or `--platform`, build MCP with those same values. Keep the `--` separator before host arguments.

Then enter a natural-language request:

```text
45+67
compute 45+67
```

Without `--skills-folder`, bare expressions such as `45+67` and requests matching `compute <digits/operators>` use a deterministic host-side skill derivation without contacting Ollama. This preserves the recorded launch and Calculator targets, creates one click task per digit/operator plus Equals, performs the launch dry run, and asks for `RUN` before real input. Scientific requests still go through Ollama, but recognized Calculator source skills use a compact button catalog rather than embedding the full recorded JSON. The deterministic shortcut is used because some local models return tool calls as ordinary text instead of Ollama `tool_calls`.

Recorded button-library hover targets are reused as click targets, not executed as hover tasks. Cleanup is included only when the source skill records a `Close` target. The recorded result's old display name is removed so it does not require the previous expression's answer.

## On-demand Markdown skill instructions

Pass `--skills-folder` to make local instructions available to Ollama:

```cmd
dotnet run --project .\Samples\OllamaMcpHost\OllamaMcpHost.csproj -c Release --no-build -- --model winui-skill-agent:latest --skill .\Samples\CalculatorButtonLibrary.winui-skill.json --skills-folder .\Samples\AgentSkills
```

`--skill` still loads one recorded JSON skill. `--skills-folder` accepts a directory, including a quoted path containing spaces. The host recursively catalogs `.md` files (skipping symbolic links/junctions) and supplies their folder-relative names, not their contents, to the model. A missing folder or a folder with no Markdown files is an error.

For each prompt, Ollama can call the host-local `load_skill_instructions` tool with a cataloged `path`. For example, Calculator requests can load `CalculatorExpression.skill.md`; editing or execution requests can also load `WinUiSkillEditor.skill.md` or `WinUiSkillExecutor.skill.md`. Multiple files can be loaded in one conversation; unrelated prompts need not load any. Selection depends on the model supporting native Ollama tool calls, not returning tool calls as ordinary text. Use descriptive filenames to help selection.

Contents are read only when requested and returned as tool context for the current conversation. The console prints `Loaded skill instructions: <path>` for each successful load. Unknown paths, empty files, and read failures are reported as tool errors. Files added after startup require a host restart; edits to cataloged files are read on the next load.

In folder mode, **all prompts go through Ollama**, including `45+67`, so the deterministic Calculator shortcut is disabled. This allows instruction selection but requires a responsive model. Markdown instructions do not implement a deterministic scientific parser or guarantee successful execution; the agent must still derive valid recorded-target tasks, dry-run them, and obtain approval for real input. Omit `--skills-folder` to restore the basic arithmetic shortcut.

For basic arithmetic in folder mode, Ollama can call `run_skill` with compact arguments instead of regenerating the full task JSON:

```json
{"calculatorExpression":"45+67","playbackOptions":{"dryRun":true}}
```

For scientific Calculator requests, the model can instead supply an ordered button sequence. For `compute squre root 121` (or `compute square root 121`):

```json
{"calculatorButtons":["num1Button","num2Button","num1Button","squareRootButton","equalButton"],"playbackOptions":{"dryRun":true}}
```

For `compute ln(10)`, use the recorded natural-log button rather than `calculatorExpression`:

```json
{"calculatorButtons":["num1Button","num0Button","logBaseEButton","equalButton"],"playbackOptions":{"dryRun":true}}
```

With a detected Calculator source skill, whether or not `--skills-folder` is used, the model initially sees a compact button catalog instead of the full source JSON. The host expands `calculatorExpression` or `calculatorButtons` into a complete skill and forwards it to MCP as `skill`, preserving launch/navigation/result/cleanup. Digits and basic operators use the existing templates; scientific buttons must have recorded targets. Unknown IDs and sequences not ending in `equalButton` are errors. Recorded hover targets become click targets. The model still determines expression meaning, button ordering, and domain validity; there is no host-side scientific expression parser.

Use exactly one of `skill`, `calculatorExpression`, or `calculatorButtons`. Both shorthand forms require an explicit boolean `dryRun`; real input still requires `RUN` approval. For edits or other tasks that need complete JSON, the model can call the local `load_source_skill` tool on demand. Non-Calculator source skills retain the full initial JSON context. Loading instruction files alone does not execute a request. `MCP: <method>` is printed when a call is dispatched to the server.

Ollama logs showing `prompt processing` and increasing `n_tokens` indicate progress, not a stalled generation. Sending thousands of recorded-path tokens can take minutes on a small GPU; the compact Calculator catalog avoids sending the full recorded JSON for recognized Calculator skills. `--num-predict -1` affects output generation only and cannot speed up prompt ingestion. Restart the host after rebuilding to use these changes; exit the running host before a normal build if its executable is locked.

The host starts `WinUiAutomation.McpServer` automatically. Do not start a second MCP server manually. Ollama must be running at `http://localhost:11434`; use `--ollama-url` to change that address. Use `/quit` to exit.

The host requests non-thinking tool responses so interactive MCP calls do not spend several minutes in an unbounded reasoning phase. It prints `Ollama: request ...` and `Ollama: response received` around each model request. The response budget defaults to 8192 tokens; use `--num-predict <positive integer>` to change it, or `--num-predict -1` to remove the explicit output-token cap. The model's context window, stop tokens, and the ten-minute host timeout still apply. This per-request option overrides `PARAMETER num_predict` in a Modelfile. The former 384-token limit could truncate a complete `run_skill` argument. Truncated responses now report an error and dispatch no tools from that response. Empty responses are reported and retried once, then return to the prompt if still empty; this does not silently substitute execution or replay earlier UI actions.

Other requests still use Ollama. If an Ollama request exceeds the ten-minute HTTP timeout, the host reports the timeout and returns to the prompt instead of exiting. Any earlier tool actions in that conversation are not undone.

The MCP project must already have a Release x64 native `UiAutoLib.dll` build, just as it does for the existing Python bridge.
