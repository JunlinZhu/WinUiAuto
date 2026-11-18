# WinUiAutomation.Runtime

`WinUiAutomation.Runtime` executes recorded or agent-generated Windows UI task sequences without depending on WPF or Studio.

```csharp
using UiAutomation.Windows;
using WinUiAutomation.Runtime;

using var automation = new WinUiAutomation();
var playback = new AutomationPlayback(automation);
var sequence = new AutomationSequence
{
	Name = "Open settings",
	Tasks =
	[
		new AutomationTask
		{
			Action = AutomationTaskAction.LeftClick,
			NodePath = nodePath,
			OffsetX = 10,
			OffsetY = 10
		}
	]
};

PlaybackResult result = playback.Play(sequence, cancellationToken: cancellationToken);
```

`PlaybackOptions` controls the initial delay, locate retries, stable-bounds checks, post-action delay, maximum recorded task delay, and same-bounds parent fallback. Recorded task delays are capped at 30 seconds by default so long recorder idle periods do not stall playback. `PlaybackResult` reports each task as succeeded, not found, unstable, or canceled. Use `IAutomationDriver` to test workflows without a live desktop.

## Versioned skill JSON

`AutomationSkillJson` is the durable interchange format for Studio, runners, and agents. Version 2 adds completion assertions; version 1 remains readable. Node paths are embedded JSON arrays, action names are camel-case strings, delays are milliseconds, and keyboard input is explicitly Base64.

```csharp
AutomationSkill skill = AutomationSkill.FromSequence(sequence);
string json = AutomationSkillJson.Serialize(skill);
AutomationSequence loaded = AutomationSkillJson.Deserialize(json).ToSequence();

var runner = new AutomationSkillRunner(automation);
SkillRunResult run = runner.RunJson(json, cancellationToken: cancellationToken);
```

Readers reject unknown formats, versions, and properties rather than silently changing workflow behavior.

```json
{
  "format": "win-ui-automation-skill",
	"schemaVersion": 2,
  "name": "Open Settings",
  "tasks": [
	{
	  "action": "leftClick",
	  "target": [
		{ "controlType": "Window", "name": "Settings", "position": 1 },
		{ "controlType": "Button", "automationId": "System", "position": 1 }
	  ],
	  "offsetX": 24,
	  "offsetY": 12,
	  "delayBeforeMilliseconds": 0
	}
  ],
  "completionAssertions": [
	{
	  "name": "System page is visible",
	  "kind": "elementExists",
	  "target": [
		{ "controlType": "Window", "name": "Settings", "position": 1 },
		{ "controlType": "Text", "name": "System", "position": 1 }
	  ],
	  "timeoutMilliseconds": 5000,
	  "pollIntervalMilliseconds": 250
	}
  ]
}
```

Assertions run after playback and are combined with AND semantics. `elementExists` succeeds when the target resolves; `elementNotExists` succeeds when it does not. Each result reports success, timeout, or cancellation plus attempts, elapsed time, and the last locate result.

`valueEquals` and `textEquals` compare `expectedValue` with the located target's value or name/value. `screenshotChanged` compares a captured PNG with `expectedScreenshotBase64` and is intended for custom-rendered surfaces.

## Hosting constraints and safety

Create one `WinUiAutomation` instance per process/session and dispose it when the host exits. Native operations are serialized and should be called from an STA thread (a dedicated STA worker is recommended for a long-lived MCP host). The desktop must remain interactive and unlocked.

`PlaybackOptions.DryRun` resolves and records targets without sending mouse or keyboard input. Configure `AutomationScope` with allowed process IDs or window handles when using snapshots or screenshots, and provide an `IAutomationAuditSink` to `AutomationSkillRunner` to record every executed task. Skill parsing and native UI failures returned through `RunJson`/`Run` are represented by `SkillRunResult.Error` so a host can serialize a clean response.
