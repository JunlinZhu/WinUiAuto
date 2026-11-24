# WinUiAutomation.SkillRunner

Run a versioned `.winui-skill.json` file from a Windows console without starting the WPF Studio UI. Playback and skill validation are provided by `WinUiAutomation.Runtime`.

Build the native `UiAutoLib` for x64 first, then build or publish the runner:

```powershell
msbuild .\UiAutoLib\UiAutoLib.vcxproj /t:Build /p:Configuration=Release /p:Platform=x64 /p:PlatformToolset=v143
dotnet publish .\WinUiAutomation.SkillRunner\WinUiAutomation.SkillRunner.csproj -c Release -r win-x64 --self-contained false
```

Run a skill (the executable and `UiAutoLib.dll` must be together):

```powershell
.\WinUiAutomation.SkillRunner\bin\Release\net8.0-windows\win-x64\publish\WinUiAutomation.SkillRunner.exe .\Samples\Calculator.winui-skill.json
```

Pass `--dry-run` to validate and locate skill targets without sending mouse or keyboard input. The process exits with code `0` on success, `1` if skill validation, playback, or assertions fail, and `2` for command-line or skill-file access errors. It requires an interactive, unlocked Windows desktop and runs on an STA thread.

Python callers can launch it directly and check its exit code:

```python
import subprocess

subprocess.run(
	[r".\WinUiAutomation.SkillRunner.exe", r".\skill.winui-skill.json"],
	check=True,
)```

## Ollama chat bridge

From the repository root, start the interactive bridge (install the Python `ollama` package if needed):

```cmd
python .\Run_WinUiAutomation_SkillRunner.py
```

Then enter a request such as:

```text
Use the Calculator UI to compute 12 * 21 by clicking the buttons. Use "C:\src\WinUiAuto\Samples\CalculatorButtonLibrary.winui-skill.json" as the recorded target library.
```

Modulo is supported too; for example, ask it to calculate `121 mod 2` using the same button library. The bridge uses the recorded `modButton` target and saves the generated skill separately.

Scientific operations use the `run_calculator_buttons` tool. For square root of 121, the sequence is `num1Button`, `num2Button`, `num1Button`, `squareRootButton`, `equalButton`. Every button target must be present in the supplied recording; missing targets are rejected with a request for a recorded path, not guessed.

The supplied library records all digits 0 through 9. The previously missing digits were captured from a live Calculator UI snapshot and verified with the native locator, not derived from another digit's path. For `log of 678` (base 10), use `num6Button`, `num7Button`, `num8Button`, `logBase10Button`, `equalButton`. Natural logarithm uses `logBaseEButton` instead.

If a chat still reports missing digits after the library is updated, retry the request with the library path in a new conversation. The host reloads the file for each composition call; a previous tool error is not evidence that the updated library is still incomplete.

The bridge generates a separate click-by-click skill under `Samples\GeneratedSkills`, previews it, and asks you to type `RUN` before sending UI input. It reuses recorded launch, navigation, digit, operator, result, and cleanup targets. The original button-library file is not executed or modified. Update `MODEL` in `Run_WinUiAutomation_SkillRunner.py` to select your local Ollama model.

Skill tasks use `target` for their recorded node-path arrays, not `path` or `nodePath`. The runtime intentionally rejects unknown JSON properties. Calculator library requests should use the composition tools instead of model-authored skill JSON.

The supplied Modelfile uses `qwen2.5-coder:7b`, which does not support Ollama's thinking option. The bridge omits `think` so each model uses its own default behavior; it does not force thinking on non-thinking models. The Python `MODEL` remains `winui-skill-agent:latest`, the custom model created by the following command.

Some Qwen Coder responses contain a tool request as JSON text rather than a native tool call. The bridge explicitly reports and validates a single object containing only `name` and `arguments` against its advertised tools before dispatching it. Unknown tools, extra or missing arguments, and invalid argument types are rejected. The same dry-run and `RUN` approval safeguards apply; arbitrary JSON or code is not executed.

After changing the Modelfile or if your model was created from the older MCP-only instructions, refresh it and restart the Python bridge:

```powershell
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.fixed
```

Run the bridge regression tests without Ollama inference or desktop input:

```powershell
python -m unittest discover -s tests -p test_skill_runner_bridge.py
```
