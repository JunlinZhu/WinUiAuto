# Agent Registration - Visual Quick Reference

## The One Command You Need

```bash
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

✓ That registers your agent!

---

## Complete Setup Flow (Copy & Paste)

```bash
# Terminal 1 - Start Ollama (keep running)
ollama serve

# Terminal 2 - Register agent
cd C:\src\WinUiAuto
ollama pull qwen3
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
ollama list
```

---

## Documentation by Time Available

### 🏃 2 Minutes (Just Do It)
```
File: AGENT-REGISTRATION-QUICKCARD.md
Copy the command, run it, done!
```

### 🚶 10 Minutes (Understand)
```
File: HOW-TO-REGISTER-AGENT.md
Step-by-step guide with examples
```

### 🧘 20 Minutes (Deep Dive)
```
File: AGENT-REGISTRATION-COMPLETE.md
Complete reference with troubleshooting
```

---

## Registration Checklist

```
Pre-Registration:
  ☐ Ollama running: ollama serve
  ☐ Working directory: C:\src\WinUiAuto
  ☐ Base model ready: ollama pull qwen3

Registration:
  ☐ Run: ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified

Verification:
  ☐ Check: ollama list
  ☐ Test: ollama run winui-skill-agent "compute 45+67"

Done! ✓
```

---

## Common Scenarios

### Scenario 1: Brand New Setup
```bash
# 1. Start Ollama
ollama serve

# 2. In new terminal
cd C:\src\WinUiAuto
ollama pull qwen3

# 3. Register
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified

# 4. Verify
ollama list
```

### Scenario 2: Multiple Variants
```bash
# Version 1
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified

# Version 2
ollama create winui-skill-agent-v2 -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified

# Custom
ollama create my-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified

# List all
ollama list
```

### Scenario 3: Update Existing
```bash
# Delete old
ollama rm winui-skill-agent

# Create new
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified

# Verify
ollama list
```

### Scenario 4: Use with MCP Host
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
RUN
CLOSE
```

---

## Troubleshooting Quick Fixes

| Problem | Fix |
|---------|-----|
| "Modelfile not found" | Use: `.\Samples\OllamaWinUiSkillAgent.Modelfile.simplified` |
| "Ollama not running" | Run: `ollama serve` in Terminal 1 |
| "Base model not found" | Run: `ollama pull qwen3` |
| "Agent exists" | Run: `ollama rm winui-skill-agent` first |
| "Command not found" | Verify Ollama installed and in PATH |

---

## File Structure

```
Samples/
├── OllamaWinUiSkillAgent.Modelfile.simplified    ← Agent definition
├── AGENT-REGISTRATION-QUICKCARD.md                ← 2 min reference
├── HOW-TO-REGISTER-AGENT.md                       ← 10 min guide
├── AGENT-REGISTRATION-COMPLETE.md                 ← Complete reference
└── [other documentation files]
```

---

## Commands Reference

```bash
# Register
ollama create <name> -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified

# List all
ollama list

# View details
ollama show <name>

# Run (planning only)
ollama run <name> "compute 45+67"

# Delete
ollama rm <name>

# Download model
ollama pull qwen3

# Start server
ollama serve
```

---

## Naming Guide

✓ Valid names:
- `winui-skill-agent`
- `my-agent`
- `agent-v2`
- `calculator-expert`

✗ Invalid:
- `WinUI Skill Agent` (spaces)
- `my_agent` (underscores)
- `agent@home` (special chars)

---

## Real Examples

### Example 1: First Time
```bash
ollama pull qwen3
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
ollama list
```

### Example 2: Test & Prod
```bash
ollama create winui-skill-agent-test -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
ollama create winui-skill-agent-prod -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
ollama list
```

### Example 3: Custom Variant
```bash
# Edit Modelfile
notepad .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified

# Register
ollama create my-custom-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

---

## Customization Examples

### Change Creativity (Temperature)
Edit Modelfile, change:
```
PARAMETER temperature 0.3      # More predictable
PARAMETER temperature 0.7      # More creative
```

### Change Speed
Edit Modelfile, change:
```
PARAMETER num_ctx 8192         # Faster
PARAMETER num_ctx 65536        # More context
```

Then register with new name:
```bash
ollama create my-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

---

## Key Points

✓ Agent is a registered model configuration  
✓ Based on qwen3 (must download first)  
✓ Defined by Modelfile  
✓ One command to register  
✓ Can create many variants  
✓ Can customize behavior  
✓ Fast to register (seconds)  
✓ Can delete and recreate anytime  

---

## Documentation Roadmap

```
JUST WANT TO REGISTER?
↓
Read: AGENT-REGISTRATION-QUICKCARD.md (2 min)
Copy: ollama create winui-skill-agent ...
Run!

WANT TO UNDERSTAND?
↓
Read: HOW-TO-REGISTER-AGENT.md (10 min)
Follow: Step-by-step guide
Test: Examples

WANT EVERYTHING?
↓
Read: AGENT-REGISTRATION-COMPLETE.md (20 min)
Learn: All variations, customizations, troubleshooting

READY TO BUILD?
↓
Read: AGENT-WORKFLOW-SIMPLIFIED.md
Read: IMPLEMENTATION-GUIDE.md
Deploy!
```

---

## Success Indicators

✓ Agent listed in `ollama list`  
✓ Command accepted: `ollama run <name> "..."`  
✓ Response received from agent  
✓ Ready to use with OllamaMcpHost  

---

**Next Step**: Open `AGENT-REGISTRATION-QUICKCARD.md` and copy the command!

---

**Files in This Delivery**:
- AGENT-REGISTRATION-QUICKCARD.md (this quick ref)
- HOW-TO-REGISTER-AGENT.md (detailed guide)
- AGENT-REGISTRATION-COMPLETE.md (complete reference)
- OllamaWinUiSkillAgent.Modelfile.simplified (the agent definition)
- Plus 12 workflow & reference documentation files

**Total**: 16 files, ~135 KB of documentation
