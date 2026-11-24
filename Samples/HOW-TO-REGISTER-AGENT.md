# How to Register a New Agent with the Simplified Modelfile

This guide shows you how to create and register a new Ollama agent using `OllamaWinUiSkillAgent.Modelfile.simplified`.

## Quick Answer (TL;DR)

```bash
ollama create <agent-name> -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

Example:
```bash
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

Done! The agent is now registered and ready to use.

---

## Step-by-Step Registration

### Step 1: Ensure Ollama is Running

**Terminal 1 (Keep running)**:
```bash
ollama serve
```

**Output should show**:
```
2026/11/23 06:59:25 Listening on 127.0.0.1:11434
```

### Step 2: Verify Required Files

**Terminal 2 (New terminal)**:
```bash
cd C:\src\WinUiAuto

# Verify the Modelfile exists
dir .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

**Output should show**:
```
OllamaWinUiSkillAgent.Modelfile.simplified
```

### Step 3: Choose an Agent Name

Pick a name for your agent. Standard naming:
- `winui-skill-agent` - Default Calculator agent
- `winui-skill-agent-v2` - Version 2 of the agent
- `winui-automation-expert` - Custom variant
- `calculator-expert` - Domain-specific variant

**Rule**: Names must be lowercase with hyphens (no spaces or underscores)

### Step 4: Register the Agent

```bash
ollama create <agent-name> -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

**Example** (replace `<agent-name>` with your chosen name):
```bash
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

**Expected output**:
```
Pulling model layers...
Building modelfile...
Creating model...
Success! Model created.
```

### Step 5: Verify Registration

```bash
ollama list
```

**Expected output**:
```
NAME                        ID              SIZE    MODIFIED
winui-skill-agent:latest    abc123...       4.2GB   Just now
```

---

## Usage After Registration

### Method 1: Direct Ollama (Planning Only)

```bash
ollama run winui-skill-agent "Explain how you would compute 45*67"
```

**Note**: This shows the plan but cannot execute real clicks. It's useful for testing the prompt.

### Method 2: With OllamaMcpHost (Full Execution)

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
```

---

## Customizing Your Agent

### Create a Custom Variant

You can create multiple agents with different system prompts.

#### Option A: Create from File
```bash
# Edit the Modelfile
notepad .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified

# Modify the SYSTEM prompt as needed

# Save and register
ollama create my-custom-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

#### Option B: Create from Base Model with Custom Prompt
```bash
# Create a new Modelfile
notepad .\Samples\OllamaWinUiSkillAgent-Custom.Modelfile
```

Add custom content:
```
FROM qwen3:latest

PARAMETER temperature 0.1
PARAMETER num_ctx 32768

SYSTEM """
You are MyCustomWinUiAgent, specialized for [your domain].

[Your custom system prompt here]
"""
```

Then register:
```bash
ollama create my-custom-agent -f .\Samples\OllamaWinUiSkillAgent-Custom.Modelfile
```

---

## Troubleshooting Agent Registration

### Error: "Modelfile not found"
**Cause**: Wrong path  
**Solution**:
```bash
# Verify file exists
dir .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified

# Use correct path
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

### Error: "Ollama is not running"
**Cause**: Ollama server not started  
**Solution**:
```bash
# Terminal 1: Start Ollama
ollama serve

# Wait 2 seconds for startup
# Terminal 2: Try registration again
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

### Error: "Base model not found (qwen3)"
**Cause**: Model not downloaded  
**Solution**:
```bash
ollama pull qwen3

# Wait for download to complete, then retry
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

### Error: "Agent already exists"
**Cause**: Agent with that name already registered  
**Solution**: Either
```bash
# Option 1: Use different name
ollama create winui-skill-agent-v2 -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified

# Option 2: Delete existing agent first
ollama rm winui-skill-agent
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

---

## Verifying Your Agent Works

### Test 1: Check Registration
```bash
ollama list | findstr winui-skill-agent
```

**Expected**: Shows your agent in the list

### Test 2: Test Prompt (Planning Only)
```bash
ollama run winui-skill-agent "What would you do to compute 45*67?"
```

**Expected**: Agent describes the workflow but doesn't execute

### Test 3: Full Test (With MCP Host)
```bash
# Terminal 1: Keep Ollama running
ollama serve

# Terminal 2: Run MCP host
dotnet run --project .\Samples\OllamaMcpHost\OllamaMcpHost.csproj -c Release --no-build -- \
  --model winui-skill-agent:latest \
  --skill .\Samples\Calculator.winui-skill.json

# At prompt:
>>> compute 45+67
>>> RUN
>>> CLOSE
```

**Expected**: 
- Dry run succeeds
- Real execution opens Calculator
- Display shows 112
- Cleanup succeeds

---

## Creating Multiple Agents

You can create multiple agents with different behaviors:

### Agent 1: Default (Calculator Focused)
```bash
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

### Agent 2: Aggressive (More Willing to Attempt)
1. Create: `OllamaWinUiSkillAgent-Aggressive.Modelfile`
2. Change system prompt: Remove some safety checks
3. Register:
```bash
ollama create winui-skill-agent-aggressive -f .\Samples\OllamaWinUiSkillAgent-Aggressive.Modelfile
```

### Agent 3: Conservative (Extra Safety Checks)
1. Create: `OllamaWinUiSkillAgent-Conservative.Modelfile`
2. Change system prompt: Add more validation requirements
3. Register:
```bash
ollama create winui-skill-agent-conservative -f .\Samples\OllamaWinUiSkillAgent-Conservative.Modelfile
```

### Switch Between Agents
```bash
# Use Agent 1
ollama run winui-skill-agent

# Use Agent 2
ollama run winui-skill-agent-aggressive

# Use Agent 3
ollama run winui-skill-agent-conservative
```

---

## Upgrading/Updating an Agent

### Update the Modelfile
```bash
# Edit the Modelfile
notepad .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified

# Delete the old agent
ollama rm winui-skill-agent

# Register the updated version
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified

# Verify
ollama list | findstr winui-skill-agent
```

---

## Managing Registered Agents

### List All Agents
```bash
ollama list
```

### View Agent Details
```bash
ollama show winui-skill-agent
```

**Output shows**:
- Base model (qwen3)
- Parameters (temperature, num_ctx, num_predict)
- System prompt
- Size

### Delete an Agent
```bash
ollama rm winui-skill-agent
```

### Export Agent Definition
```bash
# View the full definition
ollama show winui-skill-agent --modelfile
```

---

## Command Reference

| Command | Purpose | Example |
|---------|---------|---------|
| `ollama create` | Register new agent | `ollama create winui-skill-agent -f Modelfile.simplified` |
| `ollama list` | Show all agents | `ollama list` |
| `ollama run` | Run agent | `ollama run winui-skill-agent "compute 45+67"` |
| `ollama show` | View agent details | `ollama show winui-skill-agent` |
| `ollama rm` | Delete agent | `ollama rm winui-skill-agent` |
| `ollama pull` | Download base model | `ollama pull qwen3` |
| `ollama serve` | Start Ollama server | `ollama serve` |

---

## Configuration Options in Modelfile

You can customize these parameters when creating agents:

### Temperature
```
PARAMETER temperature 0        # Deterministic (always same answer)
PARAMETER temperature 0.5      # Balanced
PARAMETER temperature 1.0      # Creative (may vary)
```

### Context Window
```
PARAMETER num_ctx 8192         # Smaller (faster, less context)
PARAMETER num_ctx 32768        # Larger (slower, more context)
```

### Output Tokens
```
PARAMETER num_predict -1       # Unlimited
PARAMETER num_predict 8192     # Limit output
```

**Note**: These are already optimized in `OllamaWinUiSkillAgent.Modelfile.simplified`

---

## Quick Start Checklist

- [ ] Ollama running: `ollama serve` in Terminal 1
- [ ] Base model available: `ollama pull qwen3`
- [ ] Modelfile exists: `dir .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified`
- [ ] Choose agent name: e.g., `winui-skill-agent`
- [ ] Run registration command: `ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified`
- [ ] Verify: `ollama list | findstr winui-skill-agent`
- [ ] Test prompt: `ollama run winui-skill-agent "compute 45+67"`
- [ ] ✓ Agent is ready!

---

## See Also

- **Main workflow**: `Samples/AGENT-WORKFLOW-SIMPLIFIED.md`
- **Quick start**: `Samples/README-SIMPLIFIED.md`
- **Full guide**: `Samples/IMPLEMENTATION-GUIDE.md`
- **Modelfile reference**: `Samples/OllamaWinUiSkillAgent.Modelfile.simplified`
