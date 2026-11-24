# Agent Registration - Quick Reference Card

## Registration Command (One Line)

```bash
ollama create <agent-name> -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

**Replace `<agent-name>` with your chosen name:**
```bash
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

---

## Prerequisites (Before Registration)

```bash
# Terminal 1: Start Ollama server (keep running)
ollama serve

# Terminal 2: Verify base model is available
ollama pull qwen3

# Verify working directory
cd C:\src\WinUiAuto

# Verify Modelfile exists
dir .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

---

## Full Workflow (Copy & Paste Ready)

```bash
# Step 1: Terminal 1 - Start Ollama
ollama serve

# Step 2: Terminal 2 - In C:\src\WinUiAuto directory
cd C:\src\WinUiAuto

# Step 3: Ensure base model
ollama pull qwen3

# Step 4: Register agent
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified

# Step 5: Verify registration
ollama list

# Step 6: Test agent (optional)
ollama run winui-skill-agent "What would you do to compute 45*67?"
```

---

## Common Use Cases

### Case 1: First Time Setup
```bash
# In Terminal 2:
ollama pull qwen3
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
ollama list
```

### Case 2: Create Multiple Variants
```bash
# Version 1
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified

# Version 2
ollama create winui-skill-agent-v2 -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified

# Custom variant
ollama create my-custom-agent -f .\Samples\OllamaWinUiSkillAgent-Custom.Modelfile
```

### Case 3: Update Existing Agent
```bash
ollama rm winui-skill-agent
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

### Case 4: Use with MCP Host
```bash
# Terminal 1: Ollama
ollama serve

# Terminal 2: Register agent
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified

# Terminal 3: Run MCP host
dotnet run --project .\Samples\OllamaMcpHost\OllamaMcpHost.csproj -c Release --no-build -- \
  --model winui-skill-agent:latest \
  --skill .\Samples\CalculatorButtonLibrary.winui-skill.json \
  --skills-folder .\Samples\AgentSkills

# At >>> prompt
compute 45*67
RUN
CLOSE
/quit
```

---

## Troubleshooting Quick Fixes

| Problem | Solution |
|---------|----------|
| "Modelfile not found" | Use correct path: `.\Samples\OllamaWinUiSkillAgent.Modelfile.simplified` |
| "Ollama not running" | Start in Terminal 1: `ollama serve` |
| "Base model not found" | Download first: `ollama pull qwen3` |
| "Agent exists" | Delete first: `ollama rm winui-skill-agent` |
| "Command not found" | Ensure Ollama is in PATH or use full path |

---

## Verification Steps

```bash
# Check Ollama is running
ollama list

# Check agent was registered (shows in list)
ollama list | findstr winui-skill-agent

# View agent definition
ollama show winui-skill-agent

# Test agent (planning only, no execution)
ollama run winui-skill-agent "compute 45+67"
```

---

## Customization Examples

### Change Temperature (Creativity)
Edit `OllamaWinUiSkillAgent.Modelfile.simplified`:
```
PARAMETER temperature 0.1      # More deterministic
PARAMETER temperature 0.7      # More creative
```

### Change Context Size
Edit `OllamaWinUiSkillAgent.Modelfile.simplified`:
```
PARAMETER num_ctx 8192         # Smaller (faster)
PARAMETER num_ctx 65536        # Larger (more context)
```

### Change System Prompt
Edit the `SYSTEM """..."""` block in Modelfile

Then register:
```bash
ollama create my-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

---

## Common Agent Names

| Name | Purpose | Register |
|------|---------|----------|
| `winui-skill-agent` | Default | `ollama create winui-skill-agent -f Modelfile.simplified` |
| `winui-skill-agent-v2` | Version 2 | `ollama create winui-skill-agent-v2 -f Modelfile.simplified` |
| `calculator-expert` | Calculator specialist | `ollama create calculator-expert -f Modelfile.simplified` |
| `ui-automation` | General UI | `ollama create ui-automation -f Modelfile.simplified` |

---

## Naming Rules

✓ **Valid**:
- `winui-skill-agent`
- `my-agent`
- `agent-v2`
- `calculator-expert`

✗ **Invalid**:
- `WinUI Skill Agent` (spaces)
- `winui_skill_agent` (underscores)
- `agent@home` (special chars)

---

## Real-World Examples

### Example 1: Brand New Installation
```bash
# Terminal 1
ollama serve

# Terminal 2
cd C:\src\WinUiAuto
ollama pull qwen3
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
ollama list
```

### Example 2: Upgrade Existing Agent
```bash
# Edit Modelfile if needed
notepad .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified

# Delete old version
ollama rm winui-skill-agent

# Register updated version
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified

# Verify
ollama list
```

### Example 3: Create Test Agent
```bash
ollama create test-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
ollama run test-agent "compute 10+5"
ollama rm test-agent
```

### Example 4: Use with Host
```bash
# Terminal 1
ollama serve

# Terminal 2
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified

# Terminal 3
dotnet run --project .\Samples\OllamaMcpHost\OllamaMcpHost.csproj -c Release --no-build -- \
  --model winui-skill-agent:latest \
  --skill .\Samples\Calculator.winui-skill.json

# At >>> prompt
compute 45+67
```

---

## Key Points to Remember

✓ Agent is just a registered model configuration  
✓ Based on `qwen3` model (must be downloaded first)  
✓ Defined by Modelfile (OllamaWinUiSkillAgent.Modelfile.simplified)  
✓ Can create multiple agents with different names  
✓ Registration is fast (seconds)  
✓ Can delete and re-register anytime  
✓ System prompt is in the Modelfile  
✓ Parameters are customizable  

---

## Next Steps After Registration

1. **Quick test**: `ollama run winui-skill-agent "compute 45+67"`
2. **Full test**: Use with OllamaMcpHost (see HOW-TO-REGISTER-AGENT.md)
3. **Production**: Deploy with your application
4. **Customize**: Edit Modelfile and re-register as needed

---

**Reference**: See `HOW-TO-REGISTER-AGENT.md` for detailed instructions and troubleshooting.
