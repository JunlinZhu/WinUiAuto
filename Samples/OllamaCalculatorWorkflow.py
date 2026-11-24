"""Run Calculator.winui-skill.json through Ollama tool calling.

This is a local bridge: Ollama calls the Python functions, and the functions
forward requests to WinUiAutomation.McpServer over its stdio JSON-RPC protocol.
"""

import argparse
import base64
import copy
import json
import re
import subprocess
import sys
import urllib.error
import urllib.request
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_SKILL = REPOSITORY_ROOT / "Samples" / "Calculator.winui-skill.json"
MCP_PROJECT = REPOSITORY_ROOT / "WinUiAutomation.McpServer" / "WinUiAutomation.McpServer.csproj"


class McpClient:
    def __init__(self) -> None:
        self._next_id = 1
        self._process = subprocess.Popen(
            [
                "dotnet",
                "run",
                "--project",
                str(MCP_PROJECT),
                "-c",
                "Release",
                "-p:Platform=x64",
                "--no-build",
            ],
            cwd=REPOSITORY_ROOT,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=None,
            text=True,
            encoding="utf-8",
            bufsize=1,
        )

    def call(self, method: str, params: dict) -> dict:
        if self._process.stdin is None or self._process.stdout is None:
            raise RuntimeError("The MCP server pipes were not created.")

        request_id = self._next_id
        self._next_id += 1
        print(f"MCP: {method} ...", flush=True)
        self._process.stdin.write(
            json.dumps(
                {"jsonrpc": "2.0", "id": request_id, "method": method, "params": params}
            )
            + "\n"
        )
        self._process.stdin.flush()
        response_line = self._process.stdout.readline()
        if not response_line:
            raise RuntimeError("The MCP server exited without returning a response.")

        response = json.loads(response_line)
        if "error" in response:
            raise RuntimeError(response["error"].get("message", "MCP request failed."))
        print(f"MCP: {method} complete", flush=True)
        return response.get("result", {})

    def close(self) -> None:
        if self._process.stdin is not None:
            self._process.stdin.close()
        self._process.terminate()
        self._process.wait(timeout=10)


def ollama_chat(model: str, messages: list[dict], tools: list[dict]) -> dict:
    payload = {"model": model, "messages": messages, "tools": tools, "stream": False}
    payload_size = len(json.dumps(payload).encode("utf-8"))
    print(
        f"Ollama: sending request ({len(messages)} messages, {payload_size:,} bytes) ...",
        flush=True,
    )
    request = urllib.request.Request(
        "http://localhost:11434/api/chat",
        data=json.dumps(payload).encode("utf-8"),
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(request, timeout=600) as response:
            result = json.load(response)
            print("Ollama: response received", flush=True)
            return result
    except urllib.error.URLError as error:
        raise RuntimeError(
            "Could not reach Ollama at http://localhost:11434. Start Ollama and pull the model first."
        ) from error


def tool_definition(name: str, description: str, properties: dict | None = None) -> dict:
    return {
        "type": "function",
        "function": {
            "name": name,
            "description": description,
            "parameters": {
                "type": "object",
                "properties": properties or {},
            },
        },
    }


def encode_calculator_expression(expression: str) -> str:
    """Encode a basic arithmetic expression as recorded WinUI key events."""
    key_codes = {
        **{str(digit): 0x60 + digit for digit in range(10)},
        "+": 0x6B,
        "-": 0x6D,
        "*": 0x6A,
        "/": 0x6F,
        ".": 0x6E,
        "=": 0x0D,
    }
    data = bytearray()
    for character in expression.replace(" ", "") + "=":
        virtual_key = key_codes[character]
        data.extend((0, virtual_key, 1, virtual_key))
    return base64.b64encode(data).decode("ascii")


def build_click_expression_skill(skill: dict, expression: str) -> dict:
    """Create a derived skill with one click task per calculator key."""
    derived = copy.deepcopy(skill)
    tasks = derived["tasks"]
    def leaf_automation_id(task: dict) -> str | None:
        target = task.get("target") or []
        return target[-1].get("automationId") if target else None

    task_by_id = {
        automation_id: task
        for task in tasks
        if (automation_id := leaf_automation_id(task)) is not None
    }
    templates = {
        "1": task_by_id.get("num1Button", tasks[4]),
        "2": task_by_id.get("num2Button", tasks[6]),
        "+": task_by_id.get("plusButton", tasks[5]),
        "=": task_by_id.get("equalButton", tasks[7]),
    }
    button_properties = {
        "0": ("num0Button", "Zero"),
        "1": ("num1Button", "One"),
        "2": ("num2Button", "Two"),
        "3": ("num3Button", "Three"),
        "4": ("num4Button", "Four"),
        "5": ("num5Button", "Five"),
        "6": ("num6Button", "Six"),
        "7": ("num7Button", "Seven"),
        "8": ("num8Button", "Eight"),
        "9": ("num9Button", "Nine"),
        "+": ("plusButton", "Plus"),
        "-": ("minusButton", "Minus"),
        "*": ("multiplyButton", "Multiply"),
        "/": ("divideButton", "Divide"),
        ".": ("decimalButton", "Decimal separator"),
        "=": ("equalButton", "Equals"),
    }

    click_tasks = []
    for character in expression.replace(" ", "") + "=":
        template = templates.get(character)
        if template is None and character in ("-", "*", "/"):
            template = templates["+"]
        if template is None and character == ".":
            template = templates["1"]
        if template is None:
            template = templates["1"]
        task = copy.deepcopy(template)
        task["action"] = "leftClick"
        task["keyboardInputBase64"] = None
        leaf = task["target"][-1]
        leaf["automationId"], leaf["name"] = button_properties[character]
        for property_name in ("position", "x", "y", "w", "h", "lx", "ly"):
            leaf.pop(property_name, None)
        click_tasks.append(task)

    result_index = next(
        (index for index, task in enumerate(tasks) if leaf_automation_id(task) == "CalculatorResults"),
        len(tasks) - 2,
    )
    close_index = next(
        (index for index, task in enumerate(tasks) if leaf_automation_id(task) == "Close"),
        len(tasks) - 1,
    )
    derived["tasks"] = tasks[:4] + click_tasks + [tasks[result_index], tasks[close_index]]
    # The recorded result name contains the original value ("Display is 3").
    # AutomationId is stable across calculations, so avoid a stale value.
    derived["tasks"][-2]["target"][-1].pop("name", None)
    return derived


def summarize_skill_result(result: dict) -> dict:
    """Return execution status without sending recorded node paths back to Ollama."""
    playback = result.get("playback") or {}
    task_results = playback.get("tasks") or []
    assertions = result.get("completionAssertions") or []
    return {
        key: result.get(key)
        for key in (
            "skillName",
            "succeeded",
            "playbackSucceeded",
            "assertionsSucceeded",
            "wasCanceled",
            "error",
        )
        if key in result
    } | {
        "playback": {
            "succeededCount": playback.get("succeededCount"),
            "wasCanceled": playback.get("wasCanceled"),
            "tasks": [
                {
                    key: task.get(key)
                    for key in ("index", "status", "usedParentFallback", "locateAttempts", "inputSent")
                    if key in task
                }
                for task in task_results
            ],
        },
        "completionAssertions": [
            {
                key: assertion.get(key)
                for key in ("index", "status", "attempts", "elapsed")
                if key in assertion
            }
            for assertion in assertions
        ],
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--model", default="qwen3", help="Ollama model name.")
    parser.add_argument("--skill", type=Path, default=DEFAULT_SKILL)
    parser.add_argument(
        "--expression",
        default="1+2",
        help="Arithmetic expression to enter in Calculator (digits and + - * / . only).",
    )
    parser.add_argument(
        "--output-skill",
        type=Path,
        help="Save a derived skill with explicit calculator click tasks.",
    )
    parser.add_argument(
        "--export-only",
        action="store_true",
        help="Write --output-skill and exit without starting Ollama or MCP.",
    )
    args = parser.parse_args()

    expression = args.expression.replace(" ", "")
    if not expression or not re.fullmatch(r"[0-9+*/.-]+", expression):
        raise RuntimeError(
            "--expression must contain only digits, decimal points, spaces, and + - * /."
        )

    skill_path = args.skill.resolve()
    skill = json.loads(skill_path.read_text(encoding="utf-8"))
    if skill.get("format") != "win-ui-automation-skill":
        raise RuntimeError("The selected file is not a WinUI automation skill.")

    derived_skill = build_click_expression_skill(skill, expression)
    if args.output_skill is not None:
        output_skill_path = args.output_skill.resolve()
        output_skill_path.write_text(
            json.dumps(derived_skill, indent=2) + "\n",
            encoding="utf-8",
        )
        print(f"Updated skill written to {output_skill_path}", flush=True)
    if args.export_only:
        if args.output_skill is None:
            raise RuntimeError("--export-only requires --output-skill.")
        return 0

    mcp = McpClient()
    try:
        tools = [
            tool_definition(
                "get_desktop_snapshot",
                "Inspect the current desktop before using the Calculator skill. Do not perform input.",
            ),
            tool_definition(
                "dry_run_calculator_skill",
                "Validate the saved Calculator skill's initial Search and launch tasks without sending mouse or keyboard input. Later tasks depend on the Calculator window opened by those tasks and cannot be validated together in one dry run.",
            ),
            tool_definition(
                "run_calculator_until_result",
                "Execute the saved Calculator skill through the Equals/result task, but leave Calculator open for verification. This sends real input and requires user approval.",
            ),
            tool_definition(
                "read_calculator_result",
                "Resolve the saved Calculator result target and return its current UI state. Do not perform input.",
            ),
            tool_definition(
                "close_calculator",
                "Execute the saved Calculator skill's final close-window task. This sends real input and requires user approval.",
            ),
        ]

        messages = [
            {
                "role": "system",
                "content": (
                    "You are controlling a local Windows Calculator through a saved UI automation skill. "
                    "The skill file is already loaded by the host; do not invent node paths or keyboard data. "
                    "First call get_desktop_snapshot, then dry_run_calculator_skill to validate the initial launch prefix. "
                    "Only after the dry run succeeds, call run_calculator_until_result. "
                    "Then call read_calculator_result, report the displayed value, and finally call close_calculator. "
                    "Never claim success from a tool call that reports failure."
                ),
            },
            {
                "role": "user",
                "content": (
                    f"Use the saved skill at {skill_path} to open Calculator, switch to Scientific mode, "
                    f"calculate {expression}, report the result, and close Calculator."
                ),
            },
        ]

        def invoke_tool(name: str) -> dict:
            if name == "get_desktop_snapshot":
                snapshot = mcp.call(
                    "get_snapshot",
                    {
                        "options": {
                            "maxDepth": 4,
                            "maxElements": 100,
                            "maxVisitedElements": 500,
                            "flags": 2,
                        }
                    },
                )
                relevant_elements = [
                    {
                        key: element.get(key)
                        for key in ("controlType", "name", "automationId", "className")
                        if element.get(key) is not None
                    }
                    for element in snapshot.get("elements", [])
                    if element.get("name")
                    or element.get("automationId")
                    or element.get("controlType") in ("Window", "Button", "Text")
                ][:30]
                return {
                    "schemaVersion": snapshot.get("schemaVersion"),
                    "visitedCount": snapshot.get("visitedCount"),
                    "elementCount": snapshot.get("elementCount"),
                    "truncated": snapshot.get("truncated"),
                    "relevantElements": relevant_elements,
                }
            if name == "dry_run_calculator_skill":
                launch_skill = copy.deepcopy(skill)
                launch_skill["tasks"] = launch_skill["tasks"][:2]
                preflight = summarize_skill_result(mcp.call(
                    "run_skill",
                    {"skill": launch_skill, "playbackOptions": {"dryRun": True}},
                ))
                preflight["message"] = (
                    "Only the initial two launch tasks were dry-run. "
                    "The remaining tasks depend on UI state created by those tasks."
                )
                preflight["validatedTaskCount"] = 2
                return preflight
            if name == "run_calculator_until_result":
                answer = input(
                    "The dry run passed. Type RUN to send real input to Calculator: "
                )
                if answer.strip().upper() != "RUN":
                    return {"succeeded": False, "canceled": True, "message": "User declined execution."}
                interaction_skill = derived_skill
                return summarize_skill_result(mcp.call(
                    "run_skill",
                    {"skill": interaction_skill, "playbackOptions": {"dryRun": False}},
                ))
            if name == "read_calculator_result":
                result_task = skill["tasks"][-2]
                return mcp.call("locate", {"nodePath": result_task["target"]})
            if name == "close_calculator":
                answer = input("Type CLOSE to close Calculator: ")
                if answer.strip().upper() != "CLOSE":
                    return {"succeeded": False, "canceled": True, "message": "User declined cleanup."}
                cleanup_skill = copy.deepcopy(skill)
                cleanup_skill["tasks"] = cleanup_skill["tasks"][-1:]
                return summarize_skill_result(mcp.call(
                    "run_skill",
                    {"skill": cleanup_skill, "playbackOptions": {"dryRun": False}},
                ))
            raise RuntimeError(f"Unknown tool requested by Ollama: {name}")

        for _ in range(12):
            response = ollama_chat(args.model, messages, tools)
            message = response.get("message", {})
            messages.append(message)
            tool_calls = message.get("tool_calls", [])
            if not tool_calls:
                print(message.get("content", ""))
                return 0

            for call in tool_calls:
                name = call.get("function", {}).get("name")
                try:
                    result = invoke_tool(name)
                except Exception as error:  # surface the failure to the model and user
                    result = {"succeeded": False, "error": str(error)}
                    print(f"Tool {name} failed: {error}", file=sys.stderr)
                messages.append(
                    {
                        "role": "tool",
                        "tool_name": name,
                        "content": json.dumps(result),
                    }
                )

                # The launch preflight is deterministic. Do not wait for the
                # model to decide whether to request the next state-changing tool.
                if name == "dry_run_calculator_skill" and result.get("succeeded"):
                    print(
                        "Preflight passed; requesting approval before starting Calculator.",
                        flush=True,
                    )
                    try:
                        real_result = invoke_tool("run_calculator_until_result")
                    except Exception as error:
                        real_result = {"succeeded": False, "error": str(error)}
                        print(f"Tool run_calculator_until_result failed: {error}", file=sys.stderr)
                    messages.append(
                        {
                            "role": "tool",
                            "tool_name": "run_calculator_until_result",
                            "content": json.dumps(real_result),
                        }
                    )

        raise RuntimeError("Ollama did not finish the workflow within 12 tool rounds.")
    finally:
        mcp.close()


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (RuntimeError, OSError, json.JSONDecodeError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        raise SystemExit(1)