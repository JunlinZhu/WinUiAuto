# WinUiAuto code paths

This document records the implemented call stacks for recording, playback, skill generation, MCP, and AI-driven execution. `->` means a synchronous call unless a thread or process boundary is shown explicitly.

## 1. Node path recording

### Mouse path capture and task creation

```text
WPF MainWindow.Window_Loaded
  -> UiAutoLib.Init
  -> new UiAutomation.Windows.WinUiAutomation

MainWindow.btnRecord_Click (Record)
  -> MouseEventHandler.Init
	 -> MouseEventHandler.RecorderThread [background STA thread]
	 -> MouseKeyboardHook.StartHook

MouseKeyboardHook callback
  -> MouseEventHandler.MouseMove
	 -> RecorderThread wakes on s_eventRecordNow
		-> UiAutoLib.CaptureUiPath
		   -> WinUiAutomation.CapturePath
			  -> UiAutoLib.dll!CaptureUiPath
				 -> BuildPathToUi.PathFromLeafToRoot
					or BuildPathToUi.PathFromRootToLeaf
				 -> JSON node-path string + screen bounds
		-> MouseEventHandler.s_strUiNodePath

MouseKeyboardHook.LeftMouseUp / RightMouseUp / wheel resolution
  -> MouseEventHandler.CreateUiTask
	 -> UiTreeNode.UiTreeNodesFromString
	 -> new RecordedUiTask(pathNodes, action)
		-> OutlookName
		-> RefreshTargetFieldsFromLastNode
	 -> MainWindow.AddRecordedUi
		-> calculate RecordedGapMs
		-> RecordedUiTask.s_listRecordedUi.Add
		-> UiTreeNode.AddToUiTree
		-> NativeMethods.PostMessage(UpdateTreeView)
		   -> MainWindow.RunOnUiThread
			  -> refresh recorded-task ComboBox and node TreeView
```

Keyboard input follows the hook path independently:

```text
MouseKeyboardHook keyboard callback
  -> KeyboardEventHandler records key pairs
  -> MouseEventHandler.RecorderThread observes pending keys
  -> KeyboardEventHandler.PublishKeyboardInput
	 -> new RecordedUiTask(KeyboardInput, base64, lock states)
	 -> MainWindow.AddRecordedUi
```

The node path is therefore captured by the native UI Automation layer, while the Studio model and tree are built after the input event is converted into a `RecordedUiTask`.

## 2. Playback of recorded node paths from WinUiAutomation.Studio

```text
MainWindow.btnVerify_Click (Play)
  -> VerifyNodePath.StartVerifyThread
	 -> PlayRecordedUiTasks [background STA thread]
		-> copy RecordedUiTask.s_listRecordedUi under lock
		-> RecordedUiTask.ToAutomationTask for each task
		   -> action mapping
		   -> GetJson(removeName/removeClassName/removeAutoId)
		   -> AutomationTask { NodePath, offsets, deltas, delay }
		-> new AutomationPlayback(UiAutoLib.Automation)
		-> AutomationPlayback.Play(tasks, progress, cancellationToken)
		   -> wait initial/task delay
		   -> AutomationPlayback.Locate
			  -> IAutomationDriver.Locate
				 -> WinUiAutomation.Locate
					-> UiAutoLib.dll!LocateUiByPath
					-> UiLocateResult (bounds, state, found)
		   -> WaitForStableBounds
			  -> repeated Locate calls until bounds are stable
		   -> IAutomationDriver.PerformMouseAction or SendKeyboardInput
			  -> WinUiAutomation.PerformMouseAction / SendKeyboardInput
				 -> native MouseTask / KeyboardTask
		   -> PlaybackResult
		-> MainWindow.ScrollToTask for progress
  -> VerifyNodePath finally
	 -> MainWindow.FinishedPlaying
		-> restore Play button and Studio controls
```

Cancellation calls `AutomationPlayback`'s registered `AbortLocate`, which reaches `WinUiAutomation.AbortLocate` and the native `AbortLocateUiByPath` entry point.

## 3. Generate UI automation C# code and run the generated code

Studio generates a standalone C# method containing node paths, locate calls, recorded delays, action calls, and failure handling. The button named `btnGenerateCode` calls `GenerateCSCode`:

```text
MainWindow.btnGenerateCode_Click
	-> MainWindow.GenerateCSCode
	 -> stop recording if necessary
	 -> for each RecordedUiTask
		-> RecordedUiTask.CreateCSCode
		   -> emit description and recorded Thread.Sleep
		   -> emit node-path raw string
		   -> emit automation.Locate
		   -> emit Found branch and automation.PerformMouseAction
		   -> emit failure branch and return
	 -> Clipboard.SetText(generated C# method)
```

The generated method is pasted into a console application's `Program` class and called from `Main`. The repository includes `templates\\WinUiAutomation.GeneratedApp` for this purpose. The runtime call stack of the pasted code is:

```text
generated C# method
	 -> new UiAutomation.Windows.WinUiAutomation
	 -> automation.Locate(nodePath, UiLocateOptions)
		-> WinUiAutomation.Locate
		   -> UiAutoLib.dll!LocateUiByPath
		   -> UiLocateResult
	 -> if Found
		-> automation.PerformMouseAction
		   -> WinUiAutomation.PerformMouseAction
			  -> UiAutoLib.dll!MouseTask
	 -> else write failure and return
```

Keyboard tasks emit `automation.SendKeyboardInput`; the native path is `WinUiAutomation.SendKeyboardInput -> UiAutoLib.dll!KeyboardTask`. The generated source is copied to the clipboard rather than compiled or launched by Studio.

## 4. Generate SKILL and MCP

### Skill generation

Studio's skill generation path is:

```text
recorded Studio tasks
  -> RecordedUiTask.GetSkillJson
	 -> AutomationSkill.FromSequence
	 -> AutomationSkillJson.Serialize
	 -> .winui-skill.json file via SaveRecordedUiTask
		or clipboard/text preview via btnWinAppDriverCode_Click
```

`AutomationSkillJson` is the durable interchange format. It declares `format`, `schemaVersion`, `tasks`, optional metadata, and optional completion assertions.

### MCP server startup and tool dispatch

The repository does not generate an MCP server from a Studio recording. The MCP host is a separately built executable:

```text
Process entry point
  -> WinUiAutomation.McpServer.Program.Main
	 -> new UiAutomation.Windows.WinUiAutomation
	 -> new StdioMcpServer(automation)
		-> new AutomationSkillRunner(automation)
	 -> StdioMcpServer.Run(Console.In, Console.Out)
		-> read one JSON-RPC line
		-> Handle(line)
		   -> tools/list -> ToolDefinitions
		   -> get_snapshot -> WinUiAutomation.GetSnapshot
		   -> query_snapshot -> WinUiAutomation.QuerySnapshot
		   -> locate -> WinUiAutomation.Locate
		   -> perform_action -> WinUiAutomation.PerformMouseAction
		   -> get_screenshot -> WinUiAutomation.GetScreenshot
		   -> run_skill -> AutomationSkillRunner.RunJson
		-> serialize JSON-RPC response and flush stdout
```

Thus “generate SKILL and MCP” is two separate outputs: Studio creates a skill document; the existing MCP server exposes that document for execution through `run_skill`.

## 5. AI workflow using generated SKILL

The AI client is outside this repository. The implemented repository-side portion of the workflow is:

```text
AI client / agent [external process]
  -> start WinUiAutomation.McpServer
  -> JSON-RPC tools/list
	 <- tool definitions
  -> get_snapshot or query_snapshot
	 -> StdioMcpServer.Handle
	 -> WinUiAutomation.GetSnapshot / QuerySnapshot
	 <- bounded UI structure
  -> AI selects targets and produces win-ui-automation-skill JSON
  -> optional local validation of skill JSON
  -> run_skill { skill, playbackOptions }
	 -> StdioMcpServer.RunSkill
		-> AutomationSkillRunner.RunJson
		   -> AutomationSkillJson.Deserialize + validation
		   -> AutomationSkillRunner.Run
			  -> AutomationPlayback.Play
				 -> Locate node paths with retries
				 -> stable-bounds checks
				 -> native mouse/keyboard input
			  -> completion assertion polling
		   <- SkillRunResult
  <- JSON-RPC response
  -> AI interprets PlaybackResult, assertions, Error, and WasCanceled
  -> AI may request another snapshot or issue a corrected run_skill request
```

Safety and observability hooks in this path are `PlaybackOptions.DryRun`, `AutomationScope` allow-lists, `IAutomationAuditSink`, completion assertions, and the structured `SkillRunResult`. The MCP server itself does not contain an LLM or skill planner; it is the execution boundary used by an external AI client.

## Source entry-point index

- Recording: `WinUiAutomation.Studio\MainWindow.xaml.cs`, `MouseEventHandler.cs`, `KeyboardEventHandler.cs`
- Native path capture/locate: `WinUiAuto.Windows\UiAutomation.cs`, `UIAutoLib\UiAutoLib.cpp`, `UIAutoLib\BuildPathToUi.cpp`
- Studio playback: `WinUiAutomation.Studio\VerifyNodePath.cs`
- Playback engine: `WinUiAutomation.Runtime\AutomationPlayback.cs`
- Skill model and serialization: `WinUiAutomation.Runtime\AutomationSkill.cs`
- Skill execution and assertions: `WinUiAutomation.Runtime\AutomationSkillRunner.cs`
- MCP JSON-RPC host: `WinUiAutomation.McpServer\Program.cs`
- Generated C# template: `templates\WinUiAutomation.GeneratedApp\Program.cs`
