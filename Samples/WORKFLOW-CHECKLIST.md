# WinUI Skill Agent - Workflow Checklist

Use this checklist to verify you have everything needed and to track progress through the workflow.

## Pre-Workflow Setup Checklist

### Environment Prerequisites
- [ ] Windows 10 or later with interactive unlocked desktop
- [ ] .NET 8 SDK installed
- [ ] Visual C++ build tools installed
- [ ] Calculator app available on system
- [ ] Ollama installed (`ollama --version` returns version)

### Repository Setup
- [ ] Cloned WinUiAuto repository
- [ ] Working directory: `C:\src\WinUiAuto`
- [ ] Git repository initialized

### Build Prerequisites
- [ ] Native library builds: 
  ```bash
  msbuild .\UiAutoLib\UiAutoLib.vcxproj /t:Build /p:Configuration=Release /p:Platform=x64 /m
  ```
  Result: `✓ Build succeeded`

- [ ] MCP server builds:
  ```bash
  dotnet build .\WinUiAutomation.McpServer\WinUiAutomation.McpServer.csproj -c Release -p:Platform=x64
  ```
  Result: `✓ Build succeeded`

- [ ] MCP host builds:
  ```bash
  dotnet build .\Samples\OllamaMcpHost\OllamaMcpHost.csproj -c Release
  ```
  Result: `✓ Build succeeded`

### Ollama Setup
- [ ] Ollama service started: `ollama serve`
  Result: `✓ Ollama is listening on http://localhost:11434`

- [ ] Model pulled: `ollama pull qwen3`
  Result: `✓ Pulling model qwen3...`

- [ ] Model created from simplified Modelfile:
  ```bash
  ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
  ```
  Result: `✓ Model created`

- [ ] Model verified: `ollama list`
  Result: Shows `winui-skill-agent:latest` in list

---

## Mode 1: Deterministic Calculator (Fast, No Instructions)

### Configuration
- [ ] Run command:
  ```bash
  dotnet run --project .\Samples\OllamaMcpHost\OllamaMcpHost.csproj -c Release --no-build -- \
	--model winui-skill-agent:latest \
	--skill .\Samples\Calculator.winui-skill.json
  ```

### Startup
- [ ] Host starts successfully
- [ ] Prompt appears: `>>> `
- [ ] No errors in console output

### Test: Basic Arithmetic
- [ ] Type: `compute 45+67`
- [ ] Result: Calculator opens
- [ ] Expected: `>>> Dry run successful...`
- [ ] Type: `RUN` to approve
- [ ] Expected: Calculator shows `112`
- [ ] Type: `CLOSE` to cleanup
- [ ] Expected: `>>> Calculation complete. Calculator closed.`

### Test: Multiple Expressions
- [ ] Type: `compute 12*34`
- [ ] Expected: Dry run → approval → execution → result `408`
- [ ] Type: `compute 100-33`
- [ ] Expected: Dry run → approval → execution → result `67`

### Troubleshooting (Mode 1)
- [ ] Issue: `Element not found` in dry-run
  - Action: Close any open Calculator window
  - Retry: Run command again

- [ ] Issue: `Connection refused`
  - Action: Verify Ollama running: `ollama serve` in another terminal
  - Retry: Wait 2 seconds for Ollama to initialize

- [ ] Issue: `Model not found`
  - Action: Verify model created: `ollama list`
  - Retry: `ollama create winui-skill-agent -f Modelfile.simplified`

---

## Mode 2: Ollama-Driven with Instructions (Full Features)

### Configuration
- [ ] Run command:
  ```bash
  dotnet run --project .\Samples\OllamaMcpHost\OllamaMcpHost.csproj -c Release --no-build -- \
	--model winui-skill-agent:latest \
	--skill .\Samples\CalculatorButtonLibrary.winui-skill.json \
	--skills-folder .\Samples\AgentSkills
  ```

### Startup
- [ ] Host starts successfully
- [ ] Prompt appears: `>>> `
- [ ] No errors in console output
- [ ] Console shows: `Cataloged skill instructions...`

### Test: Basic Arithmetic
- [ ] Type: `compute 45+67`
- [ ] Expected: Ollama processes through instruction files
- [ ] Expected: `Loaded skill instructions: CalculatorExpression.skill.md.simplified`
- [ ] Expected: Dry run → approval gate
- [ ] Type: `RUN`
- [ ] Expected: Calculator shows `112`
- [ ] Type: `CLOSE`
- [ ] Expected: Cleanup complete

### Test: Scientific Functions
- [ ] Type: `compute sqrt(144)`
- [ ] Expected: Loads CalculatorExpression.skill.md
- [ ] Expected: Dry run shows num1, num4, num4, squareRootButton, equals sequence
- [ ] Type: `RUN`
- [ ] Expected: Calculator shows `12`
- [ ] Type: `CLOSE`

- [ ] Type: `compute ln(2.718)`
- [ ] Expected: Dry run shows num2, decimal, num7, num1, num8, logBaseEButton sequence
- [ ] Type: `RUN`
- [ ] Expected: Calculator shows `~1`
- [ ] Type: `CLOSE`

- [ ] Type: `compute 2**5`
- [ ] Expected: Dry run shows num2, powerButton, num5, equalButton
- [ ] Type: `RUN`
- [ ] Expected: Calculator shows `32`
- [ ] Type: `CLOSE`

### Test: Error Cases
- [ ] Type: `compute 1/0`
- [ ] Expected: Agent rejects with error message about division by zero
- [ ] No RUN prompt appears (dry run not attempted)

- [ ] Type: `compute sqrt(-4)`
- [ ] Expected: Agent rejects with error about domain (sqrt requires x ≥ 0)
- [ ] No RUN prompt appears (dry run not attempted)

### Troubleshooting (Mode 2)
- [ ] Issue: `load_skill_instructions` fails
  - Check: `Samples/AgentSkills/` directory exists
  - Check: `.md.simplified` files present in directory
  - Retry: Run host command again

- [ ] Issue: Ollama taking 30+ seconds
  - Expected: Large token count in prompt processing
  - Wait: Ollama processing prompt tokens
  - Check: Ollama logs show `prompt processing`

- [ ] Issue: Expression not recognized by Ollama
  - Likely cause: Model struggles with this expression
  - Try: Simpler expression first
  - Fallback: Use Mode 1 for basic arithmetic

---

## Workflow Execution Phases

### Phase 1: Resource Loading
- [ ] Skill JSON loaded from `--skill` parameter
- [ ] Skill instructions cataloged from `--skills-folder`
- [ ] Ollama model ready at `--ollama-url` (default: http://localhost:11434)
- [ ] MCP server started as child process

### Phase 2: Request Processing
- [ ] User enters expression at `>>>` prompt
- [ ] Agent loads relevant skill instructions
- [ ] Agent parses expression
- [ ] Agent builds skill JSON (shown in dry-run results)
- [ ] Expected: Skill JSON displayed with task sequence

### Phase 3: Dry-Run Validation
- [ ] Skill executed with `playbackOptions.dryRun: true`
- [ ] All tasks report success (or specific failure)
- [ ] Expected: "Dry run successful"
- [ ] Or: "Dry run FAILED" (stop here, do not proceed)

### Phase 4: Approval Gate
- [ ] Agent shows planned real actions in simple language
- [ ] Example: "I will click Search, type CALCULATOR, etc."
- [ ] Host prompts: `RUN` to continue, `CLOSE` to cancel
- [ ] Expected: Waiting for your response

### Phase 5: Real Execution
- [ ] If user typed `RUN`: Skill executed with `dryRun: false`
- [ ] Expected: Real clicks, real keyboard input sent
- [ ] Expected: Calculator window opens and responds
- [ ] Expected: Task results shown

### Phase 6: Result Verification
- [ ] Agent queries desktop for result display
- [ ] Expected: "Display shows <value> ✓ Correct" or similar
- [ ] Expected: User can verify visually on screen

### Phase 7: Cleanup (If Needed)
- [ ] Agent identifies cleanup needed (close application)
- [ ] Agent explains cleanup action
- [ ] Host prompts: `CLOSE` to proceed, `CANCEL` to leave open
- [ ] Expected: Waiting for your response
- [ ] If `CLOSE`: Cleanup executed, Calculator closed
- [ ] Expected: "Cleanup complete" or similar

---

## Approval Gate Interactions

### Dry-Run Approval
```
Agent: "Dry run successful. Real execution will:
1. Click Search button
2. Type CALCULATOR
3. Click Open Navigation
4. ...

Ready? Type RUN to proceed."

You type: RUN
Result: Real execution starts
```

### Cleanup Approval
```
Agent: "Calculation complete. Ready to close Calculator?
Type CLOSE to exit."

You type: CLOSE
Result: Calculator closed, returned to >>> prompt
```

### Cancellation
```
Agent: "Real execution will: ..."

You type: CLOSE
Result: Dry run complete, real execution cancelled
	   Returned to >>> prompt
```

---

## Expression Validation

### Basic Arithmetic (All Modes)
- [ ] `45+67` → 112
- [ ] `100-33` → 67
- [ ] `12*34` → 408
- [ ] `48/8` → 6
- [ ] `3.14*2` → 6.28

### Scientific (Mode 2 with Instructions)
- [ ] `sqrt(144)` → 12
- [ ] `sqrt(2)` → ~1.414
- [ ] `2**5` → 32
- [ ] `10**3` → 1000
- [ ] `ln(1)` → 0
- [ ] `log(100)` → 2
- [ ] `17 mod 5` → 2
- [ ] `abs(-5)` → 5

### Complex
- [ ] `(10+5)*2` → 30
- [ ] `2*3+4` → 10
- [ ] `sqrt(16)*2` → 8

---

## Post-Workflow Verification

### Verification Checklist
- [ ] All dry-run validations passed
- [ ] All real executions completed successfully
- [ ] All results verified correctly
- [ ] All cleanup actions completed
- [ ] No errors in console output
- [ ] Calculator closed properly
- [ ] Host returned to `>>>` prompt

### Performance Notes
- [ ] Dry-run time: Typically 2-5 seconds
- [ ] Real execution time: Typically 20-40 seconds
- [ ] Result verification: Typically 1-2 seconds
- [ ] Total per expression: Typically 30-60 seconds

### Success Indicators
- [ ] ✓ Dry run shows "successful"
- [ ] ✓ Approval gate prompts "RUN"
- [ ] ✓ Real execution completes without errors
- [ ] ✓ Result verification shows correct answer
- [ ] ✓ Cleanup prompt asks for CLOSE confirmation
- [ ] ✓ Calculator closes cleanly
- [ ] ✓ Returned to >>> prompt

---

## Exit and Cleanup

### Normal Exit
- [ ] Type: `/quit` or `exit` at `>>>` prompt
- [ ] Expected: Host application exits cleanly
- [ ] Expected: MCP server terminates
- [ ] Expected: Return to Command Prompt

### Abnormal Exit
- [ ] Press: `Ctrl+C` to interrupt
- [ ] Expected: Host terminates
- [ ] Manual cleanup: Check for orphaned Calculator windows
- [ ] Manual cleanup: Kill any stray MCP server processes if needed

### State After Exit
- [ ] [ ] Ollama still running (optional, can keep running)
- [ ] [ ] All Calculator windows closed
- [ ] [ ] No dangling MCP server processes
- [ ] [ ] Next run will start fresh with clean state

---

## Documentation Reference Quick Links

| Need | Document | Section |
|------|----------|---------|
| Quick start | README-SIMPLIFIED.md | Quick Start |
| Workflow details | AGENT-WORKFLOW-SIMPLIFIED.md | Complete workflow |
| Architecture | IMPLEMENTATION-GUIDE.md | Architecture overview |
| Expression parsing | CalculatorExpression.skill.md.simplified | Supported expressions |
| Error handling | WinUiSkillExecutor.skill.md.simplified | Failure handling |
| Troubleshooting | README-SIMPLIFIED.md | Troubleshooting table |

---

## Checklist Completion

- [ ] All pre-workflow checks passed
- [ ] Mode 1 OR Mode 2 tested successfully
- [ ] All test expressions working
- [ ] Approval gates functioning
- [ ] Cleanup procedures working
- [ ] Exit procedures verified
- [ ] Ready for regular use

---

**Workflow Status**: ✓ Ready  
**Last Checklist Date**: _________________  
**Notes**: _________________________________________________________________
