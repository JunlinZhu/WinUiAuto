import json
import copy
import re
import subprocess
import tempfile
from datetime import datetime
from pathlib import Path

import ollama

MODEL = "winui-skill-agent:latest"
SKILL_RUNNER = (
    Path(__file__).resolve().parent
    / "WinUiAutomation.SkillRunner"
    / "bin"
    / "Release"
    / "net8.0-windows"
    / "WinUiAutomation.SkillRunner.exe"
)


def run_skill(skill: dict) -> str:
    """Preview a generated skill, then run it only after explicit console approval."""
    if not isinstance(skill, dict):
        return "Rejected: skill must be a JSON object."
    if skill.get("format") != "win-ui-automation-skill":
        return "Rejected: skill format must be 'win-ui-automation-skill'."
    schema_version = skill.get("schemaVersion")
    if type(schema_version) is not int or not 1 <= schema_version <= 2:
        return "Rejected: schemaVersion must be the integer 1 or 2 (not a quoted string)."
    if not isinstance(skill.get("tasks"), list) or not skill["tasks"]:
        return "Rejected: skill must contain a non-empty tasks array."
    for index, task in enumerate(skill["tasks"], start=1):
        if not isinstance(task, dict):
            return f"Rejected: task {index} must be a JSON object."
        if "path" in task or "nodePath" in task:
            return (
                f"Rejected: task {index} must use 'target' (a recorded node-path array), not 'path' or 'nodePath'. "
                "For Calculator library requests, use run_calculator_buttons instead of authoring skill JSON."
            )
    if not SKILL_RUNNER.is_file():
        return f"Skill runner executable not found: {SKILL_RUNNER}"

    with tempfile.TemporaryDirectory(prefix="winui-skill-") as temp_dir:
        skill_path = Path(temp_dir) / "skill.winui-skill.json"
        skill_path.write_text(json.dumps(skill), encoding="utf-8")

        try:
            preview = subprocess.run(
                [str(SKILL_RUNNER), str(skill_path), "--dry-run"],
                capture_output=True,
                text=True,
                timeout=180,
                check=False,
            )
        except subprocess.TimeoutExpired:
            return "Dry run timed out; no UI input was sent."
        except OSError as error:
            return f"Could not launch the skill runner: {error}"

        print("\n--- Skill dry-run ---")
        if preview.stdout:
            print(preview.stdout, end="" if preview.stdout.endswith("\n") else "\n")
        if preview.stderr:
            print(preview.stderr, end="" if preview.stderr.endswith("\n") else "\n")
        preview_details = "\n".join(
            part.strip() for part in (preview.stdout, preview.stderr) if part.strip()
        )
        if preview.returncode != 0:
            if "Skill failed:" in preview_details or not any(
                line.startswith("Task ") for line in preview.stdout.splitlines()
            ):
                return f"Dry run failed with exit code {preview.returncode}; no UI input was sent.\n{preview_details}"
            print("Dry run could not resolve every task. Since dry run sends no input, later targets may depend on earlier actions.")

        approval = input("Type RUN to execute the real skill anyway, or anything else to cancel: ")
        if approval.strip() != "RUN":
            return f"Canceled by the user after dry run; no UI input was sent.\n{preview_details}"

        try:
            execution = subprocess.run(
                [str(SKILL_RUNNER), str(skill_path)],
                capture_output=True,
                text=True,
                timeout=300,
                check=False,
            )
        except subprocess.TimeoutExpired:
            return "Skill execution timed out. Check the desktop before retrying."
        except OSError as error:
            return f"Could not launch the skill runner: {error}"

        output = execution.stdout.strip()
        errors = execution.stderr.strip()
        details = "\n".join(part for part in (output, errors) if part)
        return f"Skill runner exit code: {execution.returncode}\n{details}" if details else f"Skill runner exit code: {execution.returncode}."


def run_skill_file(skill_file: str) -> str:
    """Load a local skill JSON file and pass it through the guarded runner."""
    path = Path(skill_file.strip().strip('"')).expanduser()
    try:
        skill = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, UnicodeError) as error:
        return f"Could not read skill file '{path}': {error}"
    except json.JSONDecodeError as error:
        return f"Invalid JSON in skill file '{path}': {error}"

    return run_skill(skill)


CALCULATOR_BUTTON_NAMES = {
    **{f"num{digit}Button": name for digit, name in enumerate(("Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine"))},
    "plusButton": "Plus",
    "minusButton": "Minus",
    "multiplyButton": "Multiply by",
    "divideButton": "Divide by",
    "modButton": "Modulo",
    "equalButton": "Equals",
    "xpower2Button": "Square",
    "invertButton": "Reciprocal",
    "absButton": "Absolute value",
    "expButton": "Exponential",
    "squareRootButton": "Square root",
    "powerButton": "'X' to the exponent",
    "powerOf10Button": "Ten to the exponent",
    "logBase10Button": "Log",
    "logBaseEButton": "Natural log",
}


def run_calculator_expression(expression: str, skill_file: str) -> str:
    """Generate a click-based Calculator skill from a basic arithmetic expression."""
    if not isinstance(expression, str):
        return "Rejected: expression must be a string."

    normalized_expression = expression.strip().replace("×", "*").replace("÷", "/")
    normalized_expression = re.sub(r"\b(?:modulo|mod)\b", "%", normalized_expression, flags=re.IGNORECASE)
    normalized_expression = re.sub(r"\s+", "", normalized_expression)
    if not re.fullmatch(r"\d+(?:[+*/%-]\d+)*", normalized_expression):
        return (
            "Rejected: expression must contain integers and +, -, *, /, or modulo operators. "
            "For scientific operations, use run_calculator_buttons with recorded button IDs; "
            "sqrt(121) uses num1Button, num2Button, num1Button, squareRootButton, equalButton."
        )

    expression_buttons = {
        "+": "plusButton",
        "-": "minusButton",
        "*": "multiplyButton",
        "/": "divideButton",
        "%": "modButton",
        "=": "equalButton",
    }
    button_ids = [
        f"num{character}Button" if character.isdigit() else expression_buttons[character]
        for character in normalized_expression + "="
    ]
    return _run_calculator_button_sequence(
        button_ids,
        skill_file,
        f"Calculator {normalized_expression}",
        f"Enter {normalized_expression} by clicking Calculator buttons.",
    )


def run_calculator_buttons(buttons: list[str], skill_file: str, description: str = "") -> str:
    """Compose and run a skill from an ordered sequence of recorded Calculator buttons."""
    if not isinstance(buttons, list) or not buttons or not all(isinstance(button, str) for button in buttons):
        return "Rejected: buttons must be a non-empty array of button IDs."
    if buttons[-1] != "equalButton":
        return "Rejected: the button sequence must end with equalButton."
    unknown_buttons = [button for button in buttons if button not in CALCULATOR_BUTTON_NAMES]
    if unknown_buttons:
        return f"Rejected: unsupported Calculator button ID(s): {', '.join(unknown_buttons)}."
    if not isinstance(description, str) or not isinstance(skill_file, str):
        return "Rejected: description and skill_file must be strings."

    skill_name = f"Calculator {description.strip()}" if description.strip() else "Calculator button sequence"
    return _run_calculator_button_sequence(
        buttons,
        skill_file,
        skill_name,
        description.strip() or "Run the requested Calculator button sequence.",
    )


def _run_calculator_button_sequence(
    button_ids: list[str], skill_file: str, skill_name: str, description: str
) -> str:
    source_path = Path(skill_file.strip().strip('"')).expanduser()
    try:
        source_skill = json.loads(source_path.read_text(encoding="utf-8-sig"))
    except (OSError, UnicodeError) as error:
        return f"Could not read Calculator button library '{source_path}': {error}"
    except json.JSONDecodeError as error:
        return f"Invalid JSON in Calculator button library '{source_path}': {error}"

    if not isinstance(source_skill, dict) or source_skill.get("format") != "win-ui-automation-skill":
        return "Rejected: the reference file is not a WinUI automation skill."
    schema_version = source_skill.get("schemaVersion")
    if type(schema_version) is not int or not 1 <= schema_version <= 2:
        return "Rejected: the reference skill must have an integer schemaVersion of 1 or 2."
    source_tasks = source_skill.get("tasks")
    if not isinstance(source_tasks, list):
        return "Rejected: the reference skill has no tasks array."

    tasks_by_id = {}
    for task in source_tasks:
        if not isinstance(task, dict):
            continue
        target = task.get("target")
        if isinstance(target, list) and target and isinstance(target[-1], dict):
            automation_id = target[-1].get("automationId")
            if isinstance(automation_id, str):
                tasks_by_id.setdefault(automation_id, task)

    def task_index(automation_id: str, start: int = 0) -> int:
        return next(
            (index for index in range(start, len(source_tasks))
             if isinstance(source_tasks[index], dict)
             and isinstance(source_tasks[index].get("target"), list)
             and source_tasks[index]["target"]
             and isinstance(source_tasks[index]["target"][-1], dict)
             and source_tasks[index]["target"][-1].get("automationId") == automation_id),
            -1,
        )

    search_index = task_index("SearchButton")
    keyboard_index = next(
        (index for index in range(search_index + 1, len(source_tasks))
         if isinstance(source_tasks[index], dict)
         and isinstance(source_tasks[index].get("action"), str)
         and source_tasks[index]["action"].casefold() == "keyboardinput"),
        -1,
    ) if search_index >= 0 else -1
    navigation_index = task_index("TogglePaneButton", keyboard_index + 1)
    scientific_index = task_index("Scientific", navigation_index + 1)
    if min(search_index, keyboard_index, navigation_index, scientific_index) < 0:
        return "Rejected: the reference skill must record Calculator launch and Scientific-mode navigation."
    if not all(key in tasks_by_id for key in ("num1Button", "equalButton", "CalculatorResults")):
        return "Rejected: the reference skill is missing required Calculator number, Equals, or result targets."

    def make_click_task(automation_id: str) -> dict:
        if automation_id not in tasks_by_id:
            raise ValueError(
                f"Button '{automation_id}' has no recorded target in the supplied library. "
                "Ask the user for its recorded node path; do not guess."
            )
        click_task = copy.deepcopy(tasks_by_id[automation_id])
        click_task["action"] = "leftClick"
        click_task["keyboardInputBase64"] = None
        return click_task

    try:
        startup_tasks = [
            copy.deepcopy(source_tasks[index])
            for index in (search_index, keyboard_index, navigation_index, scientific_index)
        ]
        click_tasks = [make_click_task(button_id) for button_id in button_ids]
    except (KeyError, TypeError, ValueError) as error:
        return f"Could not compose Calculator skill: {error}"

    result_task = copy.deepcopy(tasks_by_id["CalculatorResults"])
    result_task["target"][-1].pop("name", None)
    generated_skill = copy.deepcopy(source_skill)
    generated_skill["name"] = skill_name
    generated_skill["description"] = description
    generated_skill["tasks"] = startup_tasks + click_tasks + [result_task]
    if "Close" in tasks_by_id:
        generated_skill["tasks"].append(copy.deepcopy(tasks_by_id["Close"]))

    slug = re.sub(r"[^A-Za-z0-9]+", "-", skill_name).strip("-")[:80] or "Calculator-skill"
    output_directory = source_path.parent / "GeneratedSkills"
    output_path = output_directory / (
        f"{slug}-{datetime.now().strftime('%Y%m%d-%H%M%S-%f')}.winui-skill.json"
    )
    try:
        output_directory.mkdir(parents=True, exist_ok=True)
        output_path.write_text(json.dumps(generated_skill, indent=2) + "\n", encoding="utf-8")
    except OSError as error:
        return f"Could not save generated skill '{output_path}': {error}"

    result = run_skill(generated_skill)
    return f"Generated skill file: {output_path}\n{result}"


TOOLS = [
    {
        "type": "function",
        "function": {
            "name": "run_calculator_buttons",
            "description": (
                "Compose, save, preview, and run a Calculator skill using only targets recorded in the supplied library. "
                "Use for scientific operations instead of run_calculator_expression or handwritten run_skill JSON. "
                "For square root of 121, use num1Button, num2Button, num1Button, squareRootButton, equalButton. "
                "The host requires RUN approval before real input. Missing targets are rejected, never guessed."
            ),
            "parameters": {
                "type": "object",
                "properties": {
                    "buttons": {
                        "type": "array",
                        "items": {"type": "string", "enum": list(CALCULATOR_BUTTON_NAMES)},
                        "minItems": 1,
                        "description": "Ordered button IDs including the Button suffix; end with equalButton.",
                    },
                    "skill_file": {
                        "type": "string",
                        "description": "Path to the recorded Calculator button library supplied by the user.",
                    },
                    "description": {"type": "string", "description": "The requested calculation, such as square root of 121."},
                },
                "required": ["buttons", "skill_file"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "run_calculator_expression",
            "description": (
                "Compose a new click-by-click Calculator skill from a recorded Calculator button library, "
                "save it separately, preview it, and run only after the user types RUN. Use this for basic "
                "integer arithmetic requests, including modulo, that name a Calculator library file. Do not replay the library itself."
            ),
            "parameters": {
                "type": "object",
                "properties": {
                    "expression": {
                        "type": "string",
                        "description": "The requested integer arithmetic expression, such as 56*78 or 123%5. Use % or the word mod for modulo.",
                    },
                    "skill_file": {
                        "type": "string",
                        "description": "Path to the recorded Calculator button library .winui-skill.json file.",
                    },
                },
                "required": ["expression", "skill_file"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "run_skill",
            "description": (
                "Preview and, after the user types RUN in the host terminal, execute a WinUI "
                "automation skill. The skill must use the win-ui-automation-skill JSON format. "
                "Do not invent UI node paths: ask the user for a skill or known target paths "
                "when they are not available in the conversation."
            ),
            "parameters": {
                "type": "object",
                "properties": {
                    "skill": {
                        "type": "object",
                        "description": (
                            "A complete win-ui-automation-skill JSON document with format, numeric integer schemaVersion (2), "
                            "and tasks. Each task uses action and target (a recorded node-path array), NOT path or nodePath. "
                            "Use run_calculator_buttons for Calculator scientific requests with a library."
                        ),
                    }
                },
                "required": ["skill"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "run_skill_file",
            "description": "Preview and, after the user types RUN in the host terminal, execute a local win-ui-automation-skill JSON file. Use this when the user supplies a skill file path.",
            "parameters": {
                "type": "object",
                "properties": {
                    "skill_file": {
                        "type": "string",
                        "description": "Path to a local .winui-skill.json file provided by the user.",
                    }
                },
                "required": ["skill_file"],
            },
        },
    },
]

SYSTEM_MESSAGE = """You help run WinUI automation skills using ONLY the tools advertised by this host, not MCP shorthand or snapshot tools. When the user requests integer arithmetic in Calculator, including modulo, and provides a Calculator button-library path, call run_calculator_expression with the expression and that path; represent modulo as % or mod. For scientific operations, call run_calculator_buttons with ordered recorded button IDs and the supplied library path. To calculate square root of 121, use buttons ["num1Button", "num2Button", "num1Button", "squareRootButton", "equalButton"]. For log of 678 (base 10), use ["num6Button", "num7Button", "num8Button", "logBase10Button", "equalButton"]; natural log uses logBaseEButton. Enter the operand BEFORE the unary function. Button IDs must include the Button suffix. Do not author skill JSON for Calculator library requests, even if a composition tool rejects the request; report missing paths and ask the user rather than guessing. When the user supplies or updates a library path after a failure, call the composition tool again: the host reloads the file on every call. Do not claim the file is still incomplete based on an earlier error, or speculate about alternate button IDs. Do not call run_skill_file on the library unless the user explicitly asks to replay the entire recording. For other local skill files the user asks to run, use run_skill_file. For a generated skill not based on a Calculator library, supply the complete skill JSON object to run_skill and set schemaVersion to the numeric integer 2. Each task must use action and target (a recorded node-path array), never path or nodePath. UI targets must come from supplied recordings; never invent paths or claim you can inspect the desktop. The host previews the generated skill and requires the user to type RUN before real input is sent. Do not ask for a separate approval before calling a tool; the host handles approval. Report the tool result accurately, including failures; never invent a Calculator result."""


def _parse_text_tool_call(content: str) -> dict | None:
    if not content.lstrip().startswith("{"):
        return None
    request = json.loads(content)
    if not isinstance(request, dict) or set(request) != {"name", "arguments"}:
        raise ValueError("Expected a single JSON tool request containing only name and arguments.")
    name = request["name"]
    function = next((tool["function"] for tool in TOOLS if tool["function"]["name"] == name), None)
    if function is None:
        raise ValueError(f"Unknown tool: {name!r}.")
    arguments = request["arguments"]
    if not isinstance(arguments, dict):
        raise ValueError("Tool arguments must be a JSON object.")
    parameters = function["parameters"]
    missing = set(parameters["required"]) - set(arguments)
    unknown = set(arguments) - set(parameters["properties"])
    if missing or unknown:
        raise ValueError(f"Invalid arguments for {name}: missing {sorted(missing)}, unknown {sorted(unknown)}.")
    types = {"string": str, "object": dict, "array": list}
    for key, value in arguments.items():
        schema = parameters["properties"][key]
        if not isinstance(value, types[schema["type"]]):
            raise ValueError(f"Argument {key} must be a JSON {schema['type']}.")
        if schema["type"] == "array":
            if len(value) < schema.get("minItems", 0):
                raise ValueError(f"Argument {key} must not be empty.")
            items = schema["items"]
            if not all(isinstance(item, types[items["type"]]) and item in items["enum"] for item in value):
                raise ValueError(f"Argument {key} must contain only advertised button IDs.")
    return {"function": {"name": name, "arguments": arguments}}


def main() -> None:
    if not SKILL_RUNNER.is_file():
        raise FileNotFoundError(f"Skill runner executable not found: {SKILL_RUNNER}")

    messages = [{"role": "system", "content": SYSTEM_MESSAGE}]
    print(f"Ollama model: {MODEL}")
    print("Ask to run a skill. Type /exit to quit.")

    while True:
        prompt = input("\nYou: ").strip()
        if prompt.casefold() in {"/exit", "/quit"}:
            return
        if not prompt:
            continue

        candidate_path = Path(prompt.strip().strip('"')).expanduser()
        if candidate_path.suffix.casefold() == ".json" and candidate_path.is_file():
            print(f"\n{run_skill_file(str(candidate_path))}")
            continue

        messages.append({"role": "user", "content": prompt})
        while True:
            response = ollama.chat(
                model=MODEL,
                messages=messages,
                tools=TOOLS,
            )
            message = response["message"]
            tool_calls = message.get("tool_calls") or []
            if not tool_calls:
                try:
                    text_call = _parse_text_tool_call(message.get("content") or "")
                except ValueError as error:
                    print(f"\nRejected model tool request: {error}")
                    messages.append(message)
                    break
                if text_call is not None:
                    tool_calls = [text_call]
                    message = {"role": "assistant", "content": "", "tool_calls": tool_calls}
                    print("\nModel returned a JSON-text tool request; validated against advertised tools.")
            if not tool_calls:
                print(f"\nAssistant: {message.get('content', '')}")
                messages.append(message)
                break

            messages.append(message)
            for call in tool_calls:
                name = call["function"]["name"]
                arguments = call["function"].get("arguments", {})
                print(f"\nCalling tool: {name}")
                if name == "run_calculator_expression":
                    result = run_calculator_expression(**arguments)
                elif name == "run_calculator_buttons":
                    result = run_calculator_buttons(**arguments)
                elif name == "run_skill":
                    result = run_skill(**arguments)
                elif name == "run_skill_file":
                    result = run_skill_file(**arguments)
                else:
                    result = f"Unknown tool: {name}"
                messages.append({"role": "tool", "tool_name": name, "content": result})


if __name__ == "__main__":
    main()