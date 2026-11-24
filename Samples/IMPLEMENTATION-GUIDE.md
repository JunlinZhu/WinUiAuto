# Implementation Guide: Simplified WinUI Skill Agent

This guide explains how to implement or modify the simplified workflow.

## Architecture Overview

```
┌────────────────────────────────────────────────────────────┐
│                     Ollama (Local LLM)                      │
│  winui-skill-agent model (OllamaWinUiSkillAgent.Modelfile) │
└────────────────────────────────────────────────────────────┘
						   ↕ MCP Protocol
┌────────────────────────────────────────────────────────────┐
│              OllamaMcpHost (C# Bridge)                      │
│  - Calls Ollama /api/chat with tool definitions            │
│  - Forwards tool_calls to MCP server                       │
│  - Loads and catalogs skill instructions                   │
│  - Enforces approval gates (RUN, CLOSE)                    │
└────────────────────────────────────────────────────────────┘
				   ↕ Stdin/Stdout JSON-RPC
┌────────────────────────────────────────────────────────────┐
│          WinUiAutomation.McpServer (.NET)                  │
│  - Runs UI tasks from skill JSON                           │
│  - Captures snapshots and element locations               │
│  - Implements: run_skill, get_snapshot, locate, etc.      │
└────────────────────────────────────────────────────────────┘
						   ↓ Windows API
┌────────────────────────────────────────────────────────────┐
│          UiAutoLib.dll (Native C++)                        │
│  - Windows UI Automation (UIA) API                         │
│  - Element location and interaction                        │
│  - Keyboard input, mouse clicks, etc.                      │
└────────────────────────────────────────────────────────────┘
```

## Component Responsibilities

### 1. OllamaWinUiSkillAgent.Modelfile
**Location**: `Samples/OllamaWinUiSkillAgent.Modelfile`

**Responsibility**: Define the agent's behavior and system instructions.

**Key sections**:
- `FROM qwen3:latest` - Base model
- `PARAMETER` - Model settings (temperature=0, num_ctx=32768)
- `SYSTEM` - Agent instructions and workflow

**Workflow defined in SYSTEM**:
1. Load context (skill files, instructions)
2. Understand request (parse expression)
3. Compose skill (build task sequence)
4. Validate (dry-run)
5. Request approval
6. Execute (after approval)
7. Verify result
8. Cleanup (if needed)

**Key constraints in SYSTEM**:
- Never invent node paths
- Use complete skill JSON (not file paths)
- Require explicit approval before dryRun=false
- Report actual MCP results, not assumptions

### 2. Skill Instruction Files

#### CalculatorExpression.skill.md
**Responsibility**: Guide expression parsing and button mapping.

**When loaded**: User requests Calculator-related tasks.

**Content structure**:
- Supported expressions table
- Transformation process (8 steps)
- Task structure examples
- Validation checklist
- Error cases and handling

**Key guidance**:
- One click task per digit/operator
- Use recorded node paths from library
- Preserve launch/cleanup tasks
- Reject unsupported syntax

#### WinUiSkillEditor.skill.md
**Responsibility**: Guide skill JSON adaptation.

**When loaded**: User requests to modify an existing skill.

**Content structure**:
- Input/output specification
- 8-step procedure
- Deep-copy preservation strategy
- Node path template handling
- Validation checklist

**Key guidance**:
- Never modify source file
- Identify only variable range
- Use recorded paths as templates
- Update only automationId and name for new buttons
- Remove stale result names

#### WinUiSkillExecutor.skill.md
**Responsibility**: Guide safe execution with approval gates.

**When loaded**: Before any real input is sent.

**Content structure**:
- 6-phase procedure (inspection, dry-run, approval, execution, verification, cleanup)
- Detailed failure handling
- Task result interpretation
- MCP tool reference
- Common issues with solutions

**Key guidance**:
- Dry-run must pass completely
- Explicit approval required (RUN, CLOSE, etc.)
- Report actual MCP results
- Stop at first failure

### 3. OllamaMcpHost
**Location**: `Samples/OllamaMcpHost/`

**Responsibility**: Bridge Ollama and MCP server, enforce safety procedures.

**Key functions**:
1. Load skill JSON file
2. Catalog skill instruction files (if `--skills-folder` provided)
3. Start MCP server as child process
4. Call Ollama /api/chat with available tools
5. Forward tool_calls to MCP server
6. Enforce approval gates before dryRun=false
7. Provide `load_skill_instructions` tool for instruction loading

**Configuration options**:
- `--model` - Ollama model (e.g., winui-skill-agent:latest)
- `--skill` - Initial skill JSON file
- `--skills-folder` - Directory of .md instruction files
- `--ollama-url` - Ollama API endpoint (default: http://localhost:11434)
- `--configuration` - Build config (default: Release)
- `--platform` - Platform (default: x64)
- `--num-predict` - Output token limit (default: 8192)

**Tool definitions** (exposed to Ollama):
```json
{
  "get_snapshot": "Capture current desktop UI tree",
  "query_snapshot": "Search desktop for element",
  "locate": "Resolve node path to element",
  "run_skill": "Execute skill JSON (requires approval for dryRun=false)",
  "load_source_skill": "Load skill JSON for context",
  "load_skill_instructions": "Load .md instruction file by path",
  "perform_action": "Execute direct UI action",
  "get_screenshot": "Capture visual screenshot"
}
```

**Approval enforcement**:
- If `run_skill` called with `dryRun: false`
- Host prompts user: "RUN to continue, CLOSE to cancel"
- Waits for user response
- Only forwards to MCP if user typed "RUN"

### 4. WinUiAutomation.McpServer
**Location**: `WinUiAutomation.McpServer/`

**Responsibility**: Implement MCP tools for UI automation.

**Key tools implemented**:
- `run_skill(skill, playbackOptions)` - Execute task sequence from skill JSON
- `get_snapshot()` - Capture desktop UI tree
- `query_snapshot(properties)` - Find element in snapshot
- `locate(target_path)` - Resolve node path on current desktop
- `load_source_skill(filepath)` - Load skill file
- `perform_action(action)` - Execute action without skill context
- `get_screenshot()` - Capture visual screenshot

**Input/Output**:
- Input: JSON-RPC from MCP host (stdin)
- Output: JSON-RPC responses (stdout)
- Protocol: Newline-delimited JSON-RPC

### 5. UiAutoLib.dll
**Location**: `UiAutoLib/UiAutoLib.vcxproj` (C++ native)

**Responsibility**: Low-level Windows UI Automation.

**Key APIs**:
- Windows UI Automation (UIA) API
- Element location by properties
- Click, type, drag operations
- Snapshot capture
- Error handling

**Called by**: `WinUiAutomation.McpServer` through P/Invoke

## Workflow Execution Flow

### User: `compute 45*67`

**Step 1: Agent receives request**
```
Ollama agent loads:
- Calculator.winui-skill.json (or CalculatorButtonLibrary.winui-skill.json)
- CalculatorExpression.skill.md from --skills-folder
```

**Step 2: Agent parses expression**
```
Expression: "45*67"
Parsed: [4, 5, *, 6, 7]
Button sequence: ["num4Button", "num5Button", "multiplyButton", "num6Button", "num7Button", "equalButton"]
```

**Step 3: Agent builds skill JSON**
```
1. Copy source skill tasks
2. Find arithmetic range (old 1+2 tasks)
3. Replace with new button clicks
4. Preserve launch, navigation, cleanup
5. Create complete skill JSON in memory
```

**Step 4: OllamaMcpHost enforces dry-run**
```
Agent calls: run_skill(skill, dryRun=true)
Host forwards to MCP server
MCP server executes tasks without real input
Returns: All task results (success/failure)
Agent reviews: All tasks successful
```

**Step 5: Agent requests approval**
```
Agent message: "I will now:
1. Click Search
2. Type CALCULATOR
3. Click Open Navigation
4. Click Scientific
5. Click: 4, 5, *, 6, 7, =

Ready? Type RUN to proceed."
```

**Step 6: User approves**
```
User types: RUN
Host receives: "RUN"
```

**Step 7: Real execution**
```
Agent calls: run_skill(skill, dryRun=false)
Host forwards to MCP server
MCP server executes tasks (real clicks, real input)
Returns: Task results, final UI state
```

**Step 8: Agent verifies result**
```
Agent calls: query_snapshot with result element properties
Result: Display shows "3015"
Agent message: "45 * 67 = 3015 ✓ Correct"
```

**Step 9: Agent requests cleanup**
```
Agent message: "Ready to close Calculator? Type CLOSE."
User types: CLOSE
Agent calls: run_skill(close_skill, dryRun=false)
Host forwards, MCP server executes close
```

**Step 10: Complete**
```
Agent: "Calculation complete. Calculator closed."
Host returns to >>> prompt
```

## File and Directory Structure

```
Samples/
├── OllamaWinUiSkillAgent.Modelfile          [Primary - defines agent]
├── OllamaWinUiSkillAgent.Modelfile.simplified [Alternative - cleaner]
├── AGENT-WORKFLOW-SIMPLIFIED.md              [Complete workflow docs]
├── README-SIMPLIFIED.md                      [Quick start guide]
├── Calculator.winui-skill.json               [Legacy example skill]
├── CalculatorButtonLibrary.winui-skill.json  [Button library]
├── Calculator-45x67.winui-skill.json         [Generated example]
│
├── AgentSkills/                              [Instruction files]
│   ├── CalculatorExpression.skill.md         [Original - detailed]
│   ├── CalculatorExpression.skill.md.simplified [Alternative - clear]
│   ├── WinUiSkillEditor.skill.md             [Original - detailed]
│   ├── WinUiSkillEditor.skill.md.simplified  [Alternative - clear]
│   ├── WinUiSkillExecutor.skill.md           [Original - detailed]
│   └── WinUiSkillExecutor.skill.md.simplified [Alternative - clear]
│
└── OllamaMcpHost/                            [C# bridge]
	├── OllamaMcpHost.csproj
	├── Program.cs                            [Main entry point]
	├── OllamaClient.cs                       [Ollama API calls]
	├── McpForwarder.cs                       [MCP tool forwarding]
	└── README.md
```

## Extending the System

### Add New Calculator Functions
1. Record the button in `CalculatorButtonLibrary.winui-skill.json`
2. Add entry to `CalculatorExpression.skill.md` table
3. Update button ID and parent path documentation
4. Test with agent

### Add New Application Domain
1. Create `{AppName}Tool.skill.md.simplified` file
2. Document supported operations and node paths
3. Create recorded `.winui-skill.json` files
4. Add tool description to Modelfile SYSTEM section
5. Test through OllamaMcpHost

### Modify Safety Procedures
1. Update `WinUiSkillExecutor.skill.md.simplified`
2. Add new phase or modify existing phase
3. Update Modelfile workflow if needed
4. Test with recorded skills
5. Update phase diagrams in documentation

### Improve Agent Reasoning
1. Modify Modelfile SYSTEM prompt
2. Add more explicit constraints
3. Add example transformations
4. Test with complex expressions
5. Measure improvement with test cases

## Testing Checklist

### Setup Test
- [ ] Ollama running: `ollama serve`
- [ ] Model pulled: `ollama pull qwen3`
- [ ] Model created: `ollama create winui-skill-agent -f Modelfile.simplified`
- [ ] MCP server builds: `dotnet build WinUiAutomation.McpServer -c Release`
- [ ] Host builds: `dotnet build OllamaMcpHost -c Release`

### Functional Test
- [ ] Host starts: `dotnet run --project OllamaMcpHost -c Release --no-build -- ...`
- [ ] Prompt appears: `>>> `
- [ ] Simple arithmetic: `compute 45+67` → 112
- [ ] Scientific: `compute sqrt(144)` → 12 (with --skills-folder)
- [ ] Approval gate: Host prompts "RUN" before real execution
- [ ] Result verification: Display shows correct answer
- [ ] Cleanup: Calculator closes on CLOSE command

### Dry-Run Test
- [ ] Dry-run passes: No task failures
- [ ] All tasks report success
- [ ] No actual Calculator windows opened
- [ ] Result snapshot can be queried
- [ ] User can cancel with CLOSE

### Error Handling Test
- [ ] Unsupported expression rejected: "Unsupported syntax"
- [ ] Dry-run failure stops execution
- [ ] MCP errors reported accurately
- [ ] Element-not-found handled gracefully

### Instruction Loading Test (with --skills-folder)
- [ ] Instructions cataloged on startup
- [ ] Agent can call load_skill_instructions
- [ ] Loaded instructions available in context
- [ ] Scientific expressions work
- [ ] Instructions don't interfere with execution

## Performance Considerations

### Token Usage
- Large node paths can consume tokens
- OllamaMcpHost uses `--num-predict 8192` default
- Complex expressions may need more tokens
- Use compact Calculator catalog to reduce tokens

### Execution Time
- Dry-run typically 2-5 seconds per task
- Real execution 1-2 seconds per click
- Result verification adds 1-2 seconds
- Total flow: ~30-60 seconds for Calculator

### Model Performance
- qwen3 recommended (balanced speed/quality)
- Smaller models may struggle with complex expressions
- Temperature=0 ensures deterministic output
- Num_ctx=32768 allows long instruction files

## Security and Safety

### Input Validation
- All node paths come from recorded skills
- Never invent or guess paths
- User approval required before real execution
- Dry-run validation before changes

### Approval Gates
- Required before `dryRun: false`
- Required before cleanup actions
- Explicit response expected (RUN, CLOSE)
- No automatic progression

### Error Handling
- Stop at first failure
- Report full error details
- Never skip failed tasks
- Ask user what to do

### No Credentials
- No passwords in skills
- No sensitive data in node paths
- Local execution only
- No external API calls
