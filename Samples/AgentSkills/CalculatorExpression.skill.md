# Calculator Expression Skill

Use this skill when adapting the saved Calculator skill for a new arithmetic or scientific expression.

## Supported expression syntax

Allow digits, decimal point, spaces, parentheses, and these operators:

- `+`
- `-`
- `*`
- `/`
- `**` for powers, such as `10**3` or `2**5`.
- `mod` for modulo, such as `17 mod 5`.

Support these scientific forms (function names are case-insensitive):

| Expression | Meaning | Recorded automation ID | Button order |
| --- | --- | --- | --- |
| `ln(x)` | Natural logarithm | `logBaseEButton` | Enter `x`, then Natural log |
| `log(x)` | Base-10 logarithm | `logBase10Button` | Enter `x`, then Log |
| `10**x` | Ten to the power `x` | `powerOf10Button` | Enter `x`, then Ten to the exponent |
| `x**y` | `x` to the power `y` | `powerButton` | Enter `x`, click power, enter `y` |
| `sqrt(x)` or `square root of x` | Square root | `squareRootButton` | Enter `x`, then Square root |
| `square(x)` or `x square` | `x` squared | `xpower2Button` | Enter `x`, then Square |
| `1/x` | Reciprocal | `invertButton` | Enter `x`, then Reciprocal |
| `abs(x)` or `\|x\|` | Absolute value | `absButton` | Enter `x`, then Absolute value |
| `exp(m,n)` | Scientific notation: `m * 10**n` | `expButton` | Enter mantissa `m`, click Exponential, enter integer exponent `n` |
| `x mod y` | Calculator modulo operation | `modButton` | Enter `x`, click Modulo, enter `y` |

Normalize `squre root of x`, `squre root x`, and `square root x` to the square-root form, and `x squre` to the square form. Here `x`, `y`, and `m` are numeric operands or supported subexpressions, not literal variable names; require concrete values before generating tasks.

Calculator's `expButton` is the scientific-notation entry button (`Exp`/`EE`), **not** the mathematical function `exp(x) = e**x`. Do not translate `exp(x)` into a single `expButton` click. Ask whether the user means scientific notation or `e**x`; the latter requires a separately validated task sequence.

Reject unsupported syntax rather than inventing button targets. For real-valued calculations, require `x > 0` for `ln`/`log`, `x >= 0` for square root, nonzero reciprocal/division operands and modulo divisors, and integer `n` for `exp(m,n)`. Do not claim success when Calculator reports a domain error.

## Task transformation

Starting from `Samples\CalculatorButtonLibrary.winui-skill.json` (or a saved Calculator skill containing all required recorded targets):

1. Preserve Search and Calculator launch tasks.
2. Preserve Open Navigation and Scientific Calculator tasks.
3. Parse the expression into numbers, operators, functions, and parentheses. Replace the recorded arithmetic task range with explicit `leftClick` tasks in Calculator entry order. Do not click once per character of function names, `mod`, or `**`; these are single operations.
4. Add an explicit `Equals` click task using `equalButton` after the complete expression.
5. For digits, use a number-pad target path and the appropriate `num0Button` through `num9Button` automation ID.
6. Reuse each operation's complete recorded target path, preserving its recorded name and ancestors. Library `mouseHover` tasks supply targets only: convert the derived action to `leftClick`, rather than retaining hover actions. Basic operator IDs are:
   - `+` → `plusButton`
   - `-` → `minusButton`
   - `*` → `multiplyButton`
   - `/` → `divideButton`
   - Decimal point uses `decimalButton`; use a validated number-pad template if it is not recorded.
   - Parentheses use `openParenthesisButton` and `closeParenthesisButton`.
   - Scientific operations use the IDs and order in the table above.
   - Apply unary operations to the entered operand, not before it. Group compound operands with recorded parentheses; preserve expression grouping and precedence. Treat `**` as one power operation, not two multiplication clicks.
7. Preserve the result task and any recorded close task. If no `Close` target is recorded, leave Calculator open; do not invent cleanup. Exclude unused button-library hover tasks from execution.
8. Remove the old result name if it contains a fixed value such as `Display is 3`; retain `CalculatorResults`.
9. Save the derived skill separately, for example `Samples\Calculator-Scientific.winui-skill.json`. Dry-run the derived tasks, inspect the result, and obtain approval before sending real input.

For `12*34`, the expected arithmetic task order is:

```text
num1Button → num2Button → multiplyButton → num3Button → num4Button → equalButton
```

Scientific examples (launch/navigation and result/cleanup tasks omitted):

```text
ln(2):       num2Button → logBaseEButton → equalButton
log(100):    num1Button → num0Button → num0Button → logBase10Button → equalButton
10**3:      num3Button → powerOf10Button → equalButton
2**5:       num2Button → powerButton → num5Button → equalButton
sqrt(9):    num9Button → squareRootButton → equalButton
square(4):  num4Button → xpower2Button → equalButton
1/4:        num4Button → invertButton → equalButton
abs(5):     num5Button → absButton → equalButton
exp(2,3):   num2Button → expButton → num3Button → equalButton
17 mod 5:   num1Button → num7Button → modButton → num5Button → equalButton
```

## Host support

These instructions describe agent-generated skill transformations. Start `OllamaMcpHost` with `--skills-folder .\Samples\AgentSkills` to let Ollama load this file through `load_skill_instructions` when a Calculator prompt needs it. The host catalogs filenames at startup and reads the contents only when the model requests them. Folder mode sends all prompts to Ollama and disables the deterministic arithmetic shortcut; selection and transformation depend on the model's native tool-calling support.

After loading relevant instructions, continue with MCP tools; loading this file is not execution. For basic arithmetic, use `run_skill` with `calculatorExpression` (for example `45+67`) and explicit `playbackOptions.dryRun: true`. For scientific operations, use `calculatorButtons` with an ordered sequence from the supplied catalog ending in `equalButton`. For `compute squre root 121`, send `{"calculatorButtons":["num1Button","num2Button","num1Button","squareRootButton","equalButton"],"playbackOptions":{"dryRun":true}}`. The host derives the complete recorded-target skill, converting hover targets to clicks and preserving launch/navigation/result/cleanup. Missing scientific targets are errors; do not invent them. Choose exactly one of `skill`, `calculatorExpression`, or `calculatorButtons`. Real execution of the same sequence requires `dryRun: false` and host approval. Use `load_source_skill` only if complete JSON is necessary for other transformations.

Without `--skills-folder`, the deterministic shortcut only handles digits, decimal points, spaces, and `+`, `-`, `*`, `/` (with an optional `compute` prefix). Updating this document does not add a scientific parser to that shortcut. In particular, do not send `**` through the basic shortcut: it would generate two multiplication clicks. Scientific requests require an agent-derived, validated skill or a separate host implementation.
