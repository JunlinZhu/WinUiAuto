# Agent Registration Guide - Complete Reference

## Quick Answer

To register a new agent using the simplified Modelfile:

```bash
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

That's it! The agent is now ready to use.

---

## Detailed Steps

### Prerequisites

Before registering an agent, ensure:

1. **Ollama is installed and running**
   ```bash
   # Terminal 1 (keep running)
   ollama serve
   ```
   Expected: `Listening on 127.0.0.1:11434`

2. **Base model is available**
   ```bash
   # Terminal 2
   ollama pull qwen3
   ```
   Expected: Download completes in 1-2 minutes

3. **You're in the correct directory**
   ```bash
   cd C:\src\WinUiAuto
   ```

4. **Modelfile exists**
   ```bash
   dir .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
   ```
   Expected: File is listed

---

## Registration Process

### Step 1: Choose Agent Name

Standard naming conventions:
- `winui-skill-agent` - Default/recommended
- `winui-skill-agent-v2` - Version 2
- `calculator-expert` - Domain-specific
- `winui-automation` - General UI automation
- `my-custom-agent` - Custom variant

**Naming rules**:
- Use lowercase
- Use hyphens to separate words (not underscores)
- No spaces or special characters
- Must be unique (no duplicates)

### Step 2: Run Registration Command

```bash
ollama create <your-agent-name> -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

**Concrete example**:
```bash
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

**What happens**:
1. Ollama reads the Modelfile
2. Loads the base model (qwen3)
3. Applies parameters (temperature, context size, etc.)
4. Registers the system prompt
5. Creates the agent configuration

**Expected output**:
```
Pulling model layers...
Building modelfile...
Creating model...
Success! Model created.
```

### Step 3: Verify Registration

```bash
ollama list
```

**Expected output**:
```
NAME                        ID              SIZE    MODIFIED
winui-skill-agent:latest    abc123def456    4.2GB   Just now
```

The agent is now ready to use!

---

## Using Your Registered Agent

### Option 1: Direct Usage (Planning Only)

```bash
ollama run winui-skill-agent "Explain how you would compute 45*67"
```

**Use case**: Testing the agent prompt, understanding workflow, no actual UI execution

### Option 2: With MCP Host (Full Execution)

```bash
dotnet run --project .\Samples\OllamaMcpHost\OllamaMcpHost.csproj -c Release --no-build -- \
  --model winui-skill-agent:latest \
  --skill .\Samples\CalculatorButtonLibrary.winui-skill.json \
  --skills-folder .\Samples\AgentSkills
```

Then at the `>>>` prompt:
```
>>> compute 45*67
>>> RUN
>>> CLOSE
>>> /quit
```

**Use case**: Full workflow including dry-run, approval, real execution, and verification

### Option 3: Programmatic Usage

In your application, reference the registered agent:
```
Model name: winui-skill-agent:latest
API endpoint: http://localhost:11434/api/chat
```

---

## Managing Multiple Agents

You can register multiple agents with different configurations:

### Create Agent Variants

**Variant 1: Default (Balanced)**
```bash
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

**Variant 2: Conservative (Extra Safety)**
```bash
# 1. Create new Modelfile
notepad .\Samples\OllamaWinUiSkillAgent-Conservative.Modelfile

# 2. Add custom system prompt with more checks
# 3. Register
ollama create winui-skill-agent-conservative -f .\Samples\OllamaWinUiSkillAgent-Conservative.Modelfile
```

**Variant 3: Aggressive (Less Cautious)**
```bash
# Same process as Conservative, but with fewer checks
ollama create winui-skill-agent-aggressive -f .\Samples\OllamaWinUiSkillAgent-Aggressive.Modelfile
```

### Switch Between Agents

```bash
# Use default
ollama run winui-skill-agent

# Use conservative
ollama run winui-skill-agent-conservative

# Use aggressive
ollama run winui-skill-agent-aggressive
```

### List All Agents

```bash
ollama list
```

Output shows all registered agents:
```
NAME                                 ID              SIZE    MODIFIED
winui-skill-agent:latest             abc123...       4.2GB   10 min ago
winui-skill-agent-conservative:latest def456...       4.2GB   5 min ago
winui-skill-agent-aggressive:latest  ghi789...       4.2GB   Just now
```

### Delete an Agent

```bash
ollama rm winui-skill-agent
```

---

## Customizing Agent Behavior

### Modify Temperature (Creativity Level)

Edit the Modelfile:
```bash
notepad .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

Change:
```
PARAMETER temperature 0        # Current: Deterministic
```

To:
```
PARAMETER temperature 0.3      # More creative but predictable
PARAMETER temperature 0.7      # Creative and varied
PARAMETER temperature 1.0      # Very creative (may hallucinate)
```

Then register:
```bash
ollama create my-creative-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

### Modify System Prompt

Edit the Modelfile:
```bash
notepad .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

Find the `SYSTEM """` section and modify the instructions.

For example, add more safety checks:
```
## Extra Safety Checks

Before ANY real execution:
- Ask for explicit confirmation twice
- Wait 5 seconds for user response
- Check for dangerous patterns
```

Then register:
```bash
ollama create my-safe-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

### Modify Context Window

Change:
```
PARAMETER num_ctx 32768       # Current: 32K tokens
```

To:
```
PARAMETER num_ctx 8192        # Smaller (faster but less context)
PARAMETER num_ctx 65536       # Larger (slower but more context)
```

Then register:
```bash
ollama create my-fast-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

---

## Troubleshooting

### "Modelfile not found"
**Cause**: Incorrect file path or working directory

**Solution**:
```bash
# Verify working directory
cd C:\src\WinUiAuto

# Verify file exists
dir .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified

# Use correct path in command
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

### "Ollama is not running"
**Cause**: Ollama server not started

**Solution**:
```bash
# Terminal 1: Start Ollama
ollama serve

# Wait 2 seconds for startup

# Terminal 2: Try registration
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

### "Base model 'qwen3' not found"
**Cause**: Model not downloaded

**Solution**:
```bash
ollama pull qwen3

# Wait for download (1-2 minutes)

# Try registration again
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

### "Model with this name already exists"
**Cause**: Agent already registered with this name

**Solution**:
```bash
# Option 1: Use different name
ollama create winui-skill-agent-v2 -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified

# Option 2: Delete existing agent first
ollama rm winui-skill-agent
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

### "Command 'ollama' not found"
**Cause**: Ollama not in system PATH

**Solution**:
```bash
# Option 1: Use full path
"C:\Program Files\Ollama\ollama" create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified

# Option 2: Add Ollama to PATH environment variable
# Then try: ollama create winui-skill-agent ...
```

---

## Verification and Testing

### Test 1: Verify Registration
```bash
ollama list
```

Output should show your agent in the list.

### Test 2: View Agent Details
```bash
ollama show winui-skill-agent
```

Output shows:
- Base model (qwen3)
- Parameters
- System prompt
- Size and modification time

### Test 3: Test Prompt (Planning)
```bash
ollama run winui-skill-agent "What would you do to compute 45*67?"
```

Expected: Agent describes the workflow

### Test 4: Test with MCP Host (Full Execution)
```bash
# Terminal 1: Ollama
ollama serve

# Terminal 2: Register agent
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified

# Terminal 3: Run MCP host
dotnet run --project .\Samples\OllamaMcpHost\OllamaMcpHost.csproj -c Release --no-build -- \
  --model winui-skill-agent:latest \
  --skill .\Samples\Calculator.winui-skill.json

# At >>> prompt
compute 45+67
RUN
CLOSE
/quit
```

Expected:
- Dry run succeeds
- Real execution opens Calculator
- Display shows 112
- Cleanup succeeds

---

## Complete Workflow Examples

### Example 1: First-Time Setup (5 minutes)
```bash
# Terminal 1
ollama serve

# Terminal 2
cd C:\src\WinUiAuto
ollama pull qwen3
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
ollama list
```

✓ Agent is now registered and ready!

### Example 2: Upgrade Existing Agent (3 minutes)
```bash
# Edit Modelfile if needed
notepad .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified

# Delete old version
ollama rm winui-skill-agent

# Register new version
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified

# Verify
ollama list
```

✓ Agent is now upgraded!

### Example 3: Create Test/Production Pair (5 minutes)
```bash
# Create test agent
ollama create winui-skill-agent-test -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified

# Create production agent
ollama create winui-skill-agent-prod -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified

# List both
ollama list | findstr winui-skill-agent

# Test with test agent
ollama run winui-skill-agent-test "compute 45+67"

# When satisfied, use prod agent
ollama run winui-skill-agent-prod "compute 45+67"
```

✓ Both agents are registered!

### Example 4: Full End-to-End (10 minutes)
```bash
# Terminal 1: Start Ollama
ollama serve

# Terminal 2: Setup
cd C:\src\WinUiAuto
ollama pull qwen3

# Terminal 2: Register agent
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified

# Terminal 2: Verify
ollama list

# Terminal 3: Run with MCP host
dotnet run --project .\Samples\OllamaMcpHost\OllamaMcpHost.csproj -c Release --no-build -- \
  --model winui-skill-agent:latest \
  --skill .\Samples\CalculatorButtonLibrary.winui-skill.json \
  --skills-folder .\Samples\AgentSkills

# At >>> prompt
compute 45*67

# When asked
RUN

# View result, then
CLOSE

# Exit
/quit
```

✓ Complete workflow with real execution!

---

## Reference Materials

**Quick cards**:
- `AGENT-REGISTRATION-QUICKCARD.md` - One-page reference
- `HOW-TO-REGISTER-AGENT.md` - Detailed step-by-step guide

**Workflow documentation**:
- `AGENT-WORKFLOW-SIMPLIFIED.md` - How the workflow works
- `README-SIMPLIFIED.md` - Quick start guide
- `IMPLEMENTATION-GUIDE.md` - Architecture and implementation

**Modelfile**:
- `OllamaWinUiSkillAgent.Modelfile.simplified` - The agent definition

---

## Command Summary

| Command | Purpose |
|---------|---------|
| `ollama serve` | Start Ollama server |
| `ollama pull qwen3` | Download base model |
| `ollama create <name> -f <modelfile>` | Register agent |
| `ollama list` | Show all agents |
| `ollama run <name> "<prompt>"` | Run agent |
| `ollama show <name>` | View agent details |
| `ollama rm <name>` | Delete agent |

---

## Checklist for New Agent Registration

- [ ] Ollama running (Terminal 1: `ollama serve`)
- [ ] Base model available (`ollama pull qwen3`)
- [ ] Working directory correct (`cd C:\src\WinUiAuto`)
- [ ] Modelfile exists (dir .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified)
- [ ] Choose agent name (e.g., winui-skill-agent)
- [ ] Run registration command
- [ ] Verify with `ollama list`
- [ ] Test with `ollama run <name> "compute 45+67"`
- [ ] ✓ Agent is ready!

---

**Start here**: Choose your use case above and follow the example. For more details, see `HOW-TO-REGISTER-AGENT.md`.
