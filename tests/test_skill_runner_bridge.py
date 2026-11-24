import copy
import json
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import Run_WinUiAutomation_SkillRunner as bridge


LIBRARY = Path(bridge.__file__).resolve().parent / "Samples" / "CalculatorButtonLibrary.winui-skill.json"
SQRT_121_BUTTONS = ["num1Button", "num2Button", "num1Button", "squareRootButton", "equalButton"]


class CalculatorBridgeTests(unittest.TestCase):
    def setUp(self):
        self.temp_directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp_directory.cleanup)
        self.library_path = Path(self.temp_directory.name) / "library.winui-skill.json"
        self.source = json.loads(LIBRARY.read_text(encoding="utf-8-sig"))
        self.save_library()

    def save_library(self):
        self.library_path.write_text(json.dumps(self.source), encoding="utf-8")

    def test_square_root_uses_exact_recorded_paths_and_saves_separate_skill(self):
        original = self.library_path.read_bytes()
        with patch.object(bridge, "run_skill", return_value="Canceled by the user.") as run_skill:
            result = bridge.run_calculator_buttons(
                SQRT_121_BUTTONS, str(self.library_path), "square root of 121"
            )

        generated = run_skill.call_args.args[0]
        self.assertEqual(generated["schemaVersion"], 2)
        self.assertEqual(generated["name"], "Calculator square root of 121")
        self.assertEqual(len(generated["tasks"]), 11)
        recorded = {
            task["target"][-1]["automationId"]: task
            for task in reversed(self.source["tasks"])
            if task.get("target") and "automationId" in task["target"][-1]
        }
        self.assertEqual(generated["tasks"][:4], self.source["tasks"][:4])
        for task, button in zip(generated["tasks"][4:9], SQRT_121_BUTTONS):
            expected = copy.deepcopy(recorded[button])
            expected["action"] = "leftClick"
            expected["keyboardInputBase64"] = None
            self.assertEqual(task, expected)
            self.assertNotIn("path", task)
            self.assertNotIn("nodePath", task)
        expected_result = copy.deepcopy(recorded["CalculatorResults"])
        expected_result["target"][-1].pop("name", None)
        self.assertEqual(generated["tasks"][9], expected_result)
        self.assertEqual(generated["tasks"][10], recorded["Close"])
        saved_files = list((self.library_path.parent / "GeneratedSkills").glob("*.json"))
        self.assertEqual(len(saved_files), 1)
        self.assertEqual(json.loads(saved_files[0].read_text(encoding="utf-8")), generated)
        self.assertEqual(self.library_path.read_bytes(), original)
        self.assertIn(str(saved_files[0]), result)
        self.assertIn("Canceled by the user", result)

    def test_library_records_every_digit_and_log_678_uses_exact_paths(self):
        recorded = {
            task["target"][-1]["automationId"]: task
            for task in reversed(self.source["tasks"])
            if task.get("target") and "automationId" in task["target"][-1]
        }
        for digit in range(10):
            self.assertIn(f"num{digit}Button", recorded)
        buttons = ["num6Button", "num7Button", "num8Button", "logBase10Button", "equalButton"]
        original = self.library_path.read_bytes()
        with patch.object(bridge, "run_skill", return_value="Canceled.") as run_skill:
            result = bridge.run_calculator_buttons(buttons, str(self.library_path), "log of 678")
        generated = run_skill.call_args.args[0]
        self.assertEqual(generated["name"], "Calculator log of 678")
        self.assertEqual(len(generated["tasks"]), 11)
        for task, button in zip(generated["tasks"][4:9], buttons):
            expected = copy.deepcopy(recorded[button])
            expected["action"] = "leftClick"
            expected["keyboardInputBase64"] = None
            self.assertEqual(task, expected)
        self.assertEqual(self.library_path.read_bytes(), original)
        self.assertIn("Generated skill file:", result)

    def test_retry_reloads_updated_library_after_missing_digit_error(self):
        complete_source = copy.deepcopy(self.source)
        self.source["tasks"] = [
            task for task in self.source["tasks"]
            if not task.get("target") or task["target"][-1].get("automationId") != "num6Button"
        ]
        self.save_library()
        buttons = ["num6Button", "num7Button", "num8Button", "logBase10Button", "equalButton"]
        with patch.object(bridge, "run_skill", return_value="Canceled.") as run_skill:
            first_result = bridge.run_calculator_buttons(buttons, str(self.library_path), "log of 678")
            self.assertIn("Button 'num6Button' has no recorded target", first_result)
            run_skill.assert_not_called()
            self.source = complete_source
            self.save_library()
            second_result = bridge.run_calculator_buttons(buttons, str(self.library_path), "log of 678")
        run_skill.assert_called_once()
        self.assertIn("Generated skill file:", second_result)
        self.assertNotIn("no recorded target", second_result)
        self.assertEqual(
            [task["target"][-1]["automationId"] for task in run_skill.call_args.args[0]["tasks"][4:9]],
            buttons,
        )

    def test_missing_recorded_digit_or_scientific_button_is_not_guessed(self):
        original = copy.deepcopy(self.source)
        for missing_button in ("num2Button", "squareRootButton"):
            with self.subTest(button=missing_button):
                self.source = copy.deepcopy(original)
                self.source["tasks"] = [
                    task for task in self.source["tasks"]
                    if not task.get("target") or task["target"][-1].get("automationId") != missing_button
                ]
                self.save_library()
                with patch.object(bridge, "run_skill") as run_skill:
                    result = bridge.run_calculator_buttons(SQRT_121_BUTTONS, str(self.library_path))
                run_skill.assert_not_called()
                self.assertIn(f"Button '{missing_button}' has no recorded target", result)
                self.assertIn("Ask the user", result)
                self.assertFalse((self.library_path.parent / "GeneratedSkills").exists())

    def test_basic_arithmetic_and_modulo_still_use_recorded_buttons(self):
        for expression, expected in (
            ("12*1", ["num1Button", "num2Button", "multiplyButton", "num1Button", "equalButton"]),
            ("12 mod 1", ["num1Button", "num2Button", "modButton", "num1Button", "equalButton"]),
        ):
            with self.subTest(expression=expression):
                with patch.object(bridge, "run_skill", return_value="Canceled.") as run_skill:
                    bridge.run_calculator_expression(expression, str(self.library_path))
                tasks = run_skill.call_args.args[0]["tasks"][4:-2]
                self.assertEqual([task["target"][-1]["automationId"] for task in tasks], expected)

    def test_invalid_sequences_and_sqrt_expression_do_not_execute(self):
        with patch.object(bridge, "run_skill") as run_skill:
            for buttons in ([], ["num1", "equalButton"], ["squareRootButton"]):
                with self.subTest(buttons=buttons):
                    self.assertTrue(bridge.run_calculator_buttons(buttons, str(self.library_path)).startswith("Rejected:"))
            result = bridge.run_calculator_expression("sqrt(121)", str(self.library_path))
        self.assertIn("run_calculator_buttons", result)
        run_skill.assert_not_called()

    def test_wrong_path_property_is_rejected_before_launch(self):
        for property_name in ("path", "nodePath"):
            with self.subTest(property=property_name):
                skill = {
                    "format": "win-ui-automation-skill", "schemaVersion": 2,
                    "tasks": [{"action": "leftClick", property_name: []}],
                }
                with patch.object(bridge.subprocess, "run") as launch:
                    result = bridge.run_skill(skill)
                self.assertIn("must use 'target'", result)
                launch.assert_not_called()

    def test_scientific_tool_is_registered_and_dispatched_by_chat_host(self):
        tool = next(tool for tool in bridge.TOOLS if tool["function"]["name"] == "run_calculator_buttons")
        parameters = tool["function"]["parameters"]
        self.assertEqual(parameters["required"], ["buttons", "skill_file"])
        self.assertIn("squareRootButton", parameters["properties"]["buttons"]["items"]["enum"])
        arguments = {"buttons": SQRT_121_BUTTONS, "skill_file": str(self.library_path)}
        responses = [
            {"message": {"role": "assistant", "tool_calls": [
                {"function": {"name": "run_calculator_buttons", "arguments": arguments}}
            ]}},
            {"message": {"role": "assistant", "content": "Canceled by the user."}},
        ]
        with patch.object(bridge, "SKILL_RUNNER", self.library_path), \
                patch.object(bridge.ollama, "chat", side_effect=responses) as chat, \
                patch.object(bridge, "run_calculator_buttons", return_value="Canceled by the user.") as compose, \
                patch("builtins.input", side_effect=["Calculate square root of 121 using the library", "/exit"]), \
                patch("builtins.print"):
            bridge.main()
        compose.assert_called_once_with(**arguments)
        self.assertEqual(chat.call_count, 2)
        for call in chat.call_args_list:
            self.assertNotIn("think", call.kwargs)
        tool_results = [message for message in chat.call_args.kwargs["messages"] if message["role"] == "tool"]
        self.assertEqual(tool_results, [{"role": "tool", "tool_name": "run_calculator_buttons", "content": "Canceled by the user."}])

    def test_non_thinking_model_is_called_without_thinking_option(self):
        def non_thinking_chat(**arguments):
            if "think" in arguments:
                raise bridge.ollama.ResponseError("model does not support thinking", 400)
            return {"message": {"role": "assistant", "content": "Please provide a recorded skill path."}}

        with patch.object(bridge, "SKILL_RUNNER", self.library_path), \
                patch.object(bridge, "MODEL", "qwen2.5-coder:7b"), \
                patch.object(bridge.ollama, "chat", side_effect=non_thinking_chat) as chat, \
                patch("builtins.input", side_effect=["Calculate log of 567", "/exit"]), \
                patch("builtins.print") as output:
            bridge.main()
        chat.assert_called_once()
        self.assertEqual(chat.call_args.kwargs["model"], "qwen2.5-coder:7b")
        self.assertEqual(chat.call_args.kwargs["tools"], bridge.TOOLS)
        self.assertNotIn("think", chat.call_args.kwargs)
        output.assert_any_call("\nAssistant: Please provide a recorded skill path.")

    def test_qwen_json_text_tool_request_uses_guarded_dispatch(self):
        arguments = {
            "buttons": ["num5Button", "num6Button", "num7Button", "logBase10Button", "equalButton"],
            "skill_file": str(self.library_path),
        }
        responses = [
            {"message": {"role": "assistant", "content": json.dumps({"name": "run_calculator_buttons", "arguments": arguments})}},
            {"message": {"role": "assistant", "content": "Canceled by the user."}},
        ]
        with patch.object(bridge, "SKILL_RUNNER", self.library_path), \
                patch.object(bridge.ollama, "chat", side_effect=responses) as chat, \
                patch.object(bridge, "run_calculator_buttons", return_value="Canceled by the user.") as compose, \
                patch("builtins.input", side_effect=["Calculate log of 567 using the library", "/exit"]), \
                patch("builtins.print") as output:
            bridge.main()
        compose.assert_called_once_with(**arguments)
        self.assertEqual(chat.call_count, 2)
        for call in chat.call_args_list:
            self.assertNotIn("think", call.kwargs)
        output.assert_any_call("\nModel returned a JSON-text tool request; validated against advertised tools.")
        assistant_call = chat.call_args.kwargs["messages"][2]
        self.assertEqual(assistant_call, {
            "role": "assistant", "content": "", "tool_calls": [
                {"function": {"name": "run_calculator_buttons", "arguments": arguments}}
            ],
        })
        self.assertEqual(chat.call_args.kwargs["messages"][3], {
            "role": "tool", "tool_name": "run_calculator_buttons", "content": "Canceled by the user.",
        })

    def test_json_text_parser_rejects_unknown_tools_and_invalid_arguments(self):
        valid = {"name": "run_calculator_buttons", "arguments": {
            "buttons": ["num5Button", "equalButton"], "skill_file": str(self.library_path),
        }}
        invalid = [
            {"name": "execute_python", "arguments": {}},
            {**valid, "extra": True},
            {"name": "run_calculator_buttons", "arguments": []},
            {"name": "run_calculator_buttons", "arguments": {"buttons": ["equalButton"]}},
            {"name": "run_calculator_buttons", "arguments": {**valid["arguments"], "dryRun": False}},
            {"name": "run_calculator_buttons", "arguments": {**valid["arguments"], "skill_file": 123}},
            {"name": "run_calculator_buttons", "arguments": {**valid["arguments"], "buttons": "num5Button"}},
            {"name": "run_calculator_buttons", "arguments": {**valid["arguments"], "buttons": []}},
            {"name": "run_calculator_buttons", "arguments": {**valid["arguments"], "buttons": [5]}},
            {"name": "run_calculator_buttons", "arguments": {**valid["arguments"], "buttons": ["inventedButton"]}},
        ]
        for request in invalid:
            with self.subTest(request=request), self.assertRaises(ValueError):
                bridge._parse_text_tool_call(json.dumps(request))
        with self.assertRaises(ValueError):
            bridge._parse_text_tool_call('{"name":')
        self.assertIsNone(bridge._parse_text_tool_call("Canceled by the user."))
        self.assertEqual(bridge._parse_text_tool_call(json.dumps(valid)), {"function": valid})

    def test_invalid_json_text_tool_request_is_reported_without_execution(self):
        response = {"message": {"role": "assistant", "content": '{"name":"execute_python","arguments":{}}'}}
        with patch.object(bridge, "SKILL_RUNNER", self.library_path), \
                patch.object(bridge.ollama, "chat", return_value=response), \
                patch.object(bridge.subprocess, "run") as launch, \
                patch("builtins.input", side_effect=["Calculate log of 567", "/exit"]), \
                patch("builtins.print") as output:
            bridge.main()
        launch.assert_not_called()
        output.assert_any_call("\nRejected model tool request: Unknown tool: 'execute_python'.")

    def test_dry_run_requires_run_approval_and_schema_failure_never_executes(self):
        for approval, expected_launches in (("RUN", 2), ("NO", 1)):
            with self.subTest(approval=approval):
                preview = subprocess.CompletedProcess([], 0, "Skill: test (dry run)\n", "")
                with patch.object(bridge, "SKILL_RUNNER", self.library_path), \
                        patch.object(bridge.subprocess, "run", return_value=preview) as launch, \
                        patch("builtins.input", return_value=approval), patch("builtins.print"):
                    bridge.run_skill(self.source)
                self.assertEqual(launch.call_count, expected_launches)
                self.assertEqual(launch.call_args_list[0].args[0][-1], "--dry-run")
                if expected_launches == 2:
                    self.assertEqual(launch.call_args_list[1].args[0], launch.call_args_list[0].args[0][:-1])

        failure = subprocess.CompletedProcess([], 1, "", "Skill failed: JsonException: bad schema")
        with patch.object(bridge, "SKILL_RUNNER", self.library_path), \
                patch.object(bridge.subprocess, "run", return_value=failure) as launch, \
                patch("builtins.input") as approval, patch("builtins.print"):
            result = bridge.run_skill(self.source)
        self.assertIn("Dry run failed", result)
        launch.assert_called_once()
        approval.assert_not_called()


if __name__ == "__main__":
    unittest.main()