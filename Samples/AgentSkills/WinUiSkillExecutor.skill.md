# WinUI Skill Executor Skill

Use this skill to validate and execute a prepared `.winui-skill.json` through WinUiAutomation MCP.

## Safety procedure

1. Inspect the current desktop with `get_snapshot` or `query_snapshot`.
2. Run the complete prepared skill with `run_skill` and `playbackOptions.dryRun: true`.
3. Review every task result. A dry run must not report parsing, element-not-found, stability, cancellation, or assertion failures.
4. Describe the exact real input that will be sent and request explicit approval.
5. After approval, run the same skill with `dryRun: false`.
6. Inspect the returned playback and assertion results.
7. Verify important output with `locate`, `query_snapshot`, or `get_snapshot`.
8. Execute cleanup only after explaining it and receiving approval when cleanup sends input.

## Failure handling

- Stop at the first failed or canceled task.
- Report task index, action, target identifier, locate attempts, and MCP error.
- Do not repeat a partially executed skill without inspecting the current UI state.
- Never report success because MCP returned a response; success requires task and assertion evidence.
