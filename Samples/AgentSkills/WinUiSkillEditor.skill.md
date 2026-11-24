# WinUI Skill Editor Skill

Use this skill when a user asks to adapt an existing `.winui-skill.json` to new input or a related workflow.

## Inputs

- `skillPath`: source `.winui-skill.json` path
- `request`: requested UI action or data change
- `outputPath`: optional derived skill path

## Procedure

1. Load and parse `skillPath`.
2. Preserve the source object by deep-copying it.
3. Identify the task range that represents the variable user input.
4. Update only that range.
5. Preserve task order, launch tasks, navigation tasks, cleanup tasks, delays, and assertions.
6. Use recorded node paths as templates. For a new button, change the leaf `automationId` and `name`, but retain the correct parent control path. Operators must use an operator parent such as `StandardOperators`; they must not use a number-pad parent.
7. Remove stale result names such as `Display is 3` when the result value changes, while retaining stable identifiers such as `CalculatorResults`.
8. Validate the resulting JSON before returning it.
9. Write `outputPath` only when requested; never overwrite the source by default.

## Output

Return:

- The derived skill JSON
- A concise task-diff summary
- The output path, if written
- Any unresolved target assumptions
