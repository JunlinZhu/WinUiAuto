# WinUI Skill Agent - Simplified Documentation

This folder contains simplified workflow documentation and templates for the WinUI Skill Agent.

## Files

### Primary Documentation
- **AGENT-WORKFLOW-SIMPLIFIED.md** - Complete simplified workflow guide
  - Architecture overview
  - Workflow steps (1-5)
  - Key files and responsibilities
  - MCP integration details
  - Configuration examples
  - Troubleshooting guide

- **OllamaWinUiSkillAgent.Modelfile.simplified** - Simplified Ollama model definition
  - Cleaner system prompt
  - Better organized instructions
  - Clearer procedural steps
  - Safety constraints emphasized

### Skill Instructions (Simplified)
Located in `AgentSkills/` subfolder:

- **CalculatorExpression.skill.md.simplified** - Expression parsing guide
  - Supported expressions table
  - Transformation process
  - Example transformations
  - Error handling
  - Validation checklist

- **WinUiSkillEditor.skill.md.simplified** - Skill adaptation guide
  - 8-step modification process
  - Deep-copy preservation
  - Node path template handling
  - Pattern examples
  - Validation checklist

- **WinUiSkillExecutor.skill.md.simplified** - Safe execution guide
  - 6-phase execution workflow
  - Dry-run validation details
  - Approval gate procedures
  - Failure handling
  - MCP tool reference

## Quick Start

### 1. Prerequisites
```bash
# Terminal 1: Start Ollama
ollama serve

# Terminal 2: Build everything
cd C:\src\WinUiAuto

# Build native library
msbuild .\UiAutoLib\UiAutoLib.vcxproj /t:Build /p:Configuration=Release /p:Platform=x64 /m

# Build MCP server
dotnet build .\WinUiAutomation.McpServer\WinUiAutomation.McpServer.csproj -c Release -p:Platform=x64

# Build MCP host
dotnet build .\Samples\OllamaMcpHost\OllamaMcpHost.csproj -c Release

# Pull model
ollama pull qwen3
```

### 2. Create Ollama Model
```bash
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

### 3. Run with Skill Instructions (Recommended)
```bash
dotnet run --project .\Samples\OllamaMcpHost\OllamaMcpHost.csproj -c Release --no-build -- \
  --model winui-skill-agent:latest \
  --skill .\Samples\CalculatorButtonLibrary.winui-skill.json \
  --skills-folder .\Samples\AgentSkills
```

### 4. Enter Requests at Prompt
```
>>> compute 45*67
>>> compute sqrt(144)
>>> compute ln(2.718)
>>> /quit
```

## Simplified Workflow

```
User Request (compute 45*67)
	   ↓
Agent loads skill files and instructions
	   ↓
Agent parses expression and builds task sequence
	   ↓
Agent composes complete skill JSON
	   ↓
Dry-run validation (get_snapshot → run_skill with dryRun=true)
	   ↓
Request approval: "Ready to send real input?"
	   ↓
Real execution: run_skill with dryRun=false
	   ↓
Result verification: query_snapshot
	   ↓
Request cleanup approval
	   ↓
Complete
```

## Key Improvements Over Original

### Clarity
- ✓ Single-purpose skill files
- ✓ Clear step-by-step procedures
- ✓ Separated concerns (parsing, editing, execution)
- ✓ Consistent terminology

### Safety
- ✓ Explicit dry-run requirement
- ✓ Approval gates before real input
- ✓ Failure handling in one place
- ✓ No silent success claims

### Maintainability
- ✓ Shorter files (easier to read in full)
- ✓ Examples for each pattern
- ✓ Validation checklists
- ✓ Common issues with solutions

### Workflow
- ✓ Agent clearly loads skill files first
- ✓ Agent clearly loads instruction files
- ✓ Agent clearly composes JSON
- ✓ Agent clearly validates before approval
- ✓ Approval required before real execution

## Changes from Original

| Aspect | Original | Simplified |
|--------|----------|-----------|
| Modelfile size | 43 lines | 95 lines (but clearer structure) |
| System prompt | Compressed, dense | Organized with headers and examples |
| Skill files | Detailed, comprehensive | Focused, step-by-step |
| Approval gates | Implicit | Explicit (RUN, CLOSE, etc.) |
| Error handling | General principles | Specific procedures |
| Examples | Minimal | Multiple per file |
| Checklists | None | Validation checklists included |

## MCP Server Interface

The agent uses these MCP tools:

| Tool | Purpose | Input | Output |
|------|---------|-------|--------|
| `get_snapshot` | Capture desktop state | (none) | UI tree JSON |
| `query_snapshot` | Search for element | properties | Match results |
| `locate` | Resolve node path | path array | Element info or error |
| `run_skill` | Execute skill JSON | skill, dryRun flag | Task results |
| `load_source_skill` | Load skill context | filepath | Skill JSON |
| `perform_action` | Direct UI action | action spec | Action result |
| `get_screenshot` | Capture visual | (none) | Screenshot data |

## Configuration Examples

### Mode 1: Deterministic Calculator (Fast, No Instructions)
```bash
dotnet run --project .\Samples\OllamaMcpHost\OllamaMcpHost.csproj -c Release --no-build -- \
  --model winui-skill-agent:latest \
  --skill .\Samples\Calculator.winui-skill.json
```
- Use: `compute 45+67`
- Fast: Parses locally, skips Ollama for basic arithmetic
- Simple: No instruction files needed

### Mode 2: Ollama-Driven with Instructions (Recommended)
```bash
dotnet run --project .\Samples\OllamaMcpHost\OllamaMcpHost.csproj -c Release --no-build -- \
  --model winui-skill-agent:latest \
  --skill .\Samples\CalculatorButtonLibrary.winui-skill.json \
  --skills-folder .\Samples\AgentSkills
```
- Use: `compute sqrt(144)`, `compute ln(e)`, scientific expressions
- Flexible: All requests go through Ollama
- Complete: Skill instructions available for context

### Mode 3: Custom MCP Host
Implement your own MCP client with:
- System prompt: `OllamaWinUiSkillAgent.Modelfile.simplified` SYSTEM block
- Instructions: Load from `AgentSkills/*.simplified` files
- MCP server: Connect to `WinUiAutomation.McpServer`

## Troubleshooting

### "Element not found" in dry-run
- Verify Calculator is not running from previous session
- Verify windows or elements match recorded paths
- Try running original skill first to confirm baseline

### Expression not recognized
- Load `CalculatorExpression.skill.md.simplified` for supported syntax
- Scientific expressions may need `--skills-folder` to work
- Simple arithmetic works without instructions

### Approval prompt missing
- Ensure host has `--skills-folder` and agent-skills files
- Check that agent received dry-run results
- Verify MCP server is running

### Result verification fails
- Use `query_snapshot` to check actual display
- Display value may differ from expected
- Calculator may show error or different result

## Switching from Original to Simplified

1. Keep existing files for reference
2. Replace Modelfile: Use `OllamaWinUiSkillAgent.Modelfile.simplified`
3. Create new model: `ollama create winui-skill-agent-v2 -f OllamaWinUiSkillAgent.Modelfile.simplified`
4. Use `.simplified` skill files
5. Run host with: `--model winui-skill-agent-v2:latest`

## For Developers

### Extending Skills
- Add new patterns to relevant `.skill.md.simplified` file
- Include example transformations
- Add to validation checklist
- Test with agent and OllamaMcpHost

### Adding New Domains
- Create `{Domain}.skill.md.simplified` file
- Follow same structure: purpose, inputs, process, examples, validation
- Register in Modelfile system prompt
- Test through agent request

### Modifying Safety Procedures
- Edit `WinUiSkillExecutor.skill.md.simplified`
- Keep approval gates explicit
- Ensure dry-run before real execution
- Document any new failure modes

## See Also

- **Original documentation**: `docs/AI-WORKFLOW-SKILL.md`
- **MCP server**: `WinUiAutomation.McpServer/`
- **MCP host**: `Samples/OllamaMcpHost/`
- **Recorded skills**: `Samples/*.winui-skill.json`
- **Button library**: `Samples/CalculatorButtonLibrary.winui-skill.json`
