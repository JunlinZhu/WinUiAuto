# How to Register a New Agent - Quick Answer

## Direct Answer

To register a new agent using the simplified Modelfile:

```bash
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

That's the complete command. The agent is now registered.

---

## 30-Second Setup

```bash
# Terminal 1 (keep running)
ollama serve

# Terminal 2 (new terminal)
cd C:\src\WinUiAuto
ollama pull qwen3
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
ollama list
```

✓ Done! Your agent is registered.

---

## Verify It Works

```bash
# Check it's in the list
ollama list

# Test it (planning only, no execution)
ollama run winui-skill-agent "What would you do to compute 45*67?"
```

---

## Use It with MCP Host (Full Execution)

```bash
# Terminal 3 (new terminal)
dotnet run --project .\Samples\OllamaMcpHost\OllamaMcpHost.csproj -c Release --no-build -- \
  --model winui-skill-agent:latest \
  --skill .\Samples\Calculator.winui-skill.json

# At >>> prompt
>>> compute 45+67
>>> RUN
>>> CLOSE
>>> /quit
```

---

## Documentation for More Details

| Need | Read This | Time |
|------|-----------|------|
| Quick reference card | `AGENT-REGISTRATION-QUICKCARD.md` | 2 min |
| Step-by-step guide | `HOW-TO-REGISTER-AGENT.md` | 10 min |
| Complete reference | `AGENT-REGISTRATION-COMPLETE.md` | 20 min |
| Visual summary | `AGENT-REGISTRATION-VISUAL.md` | 5 min |

---

## Common Tasks

### Create Multiple Agents
```bash
ollama create agent-v1 -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
ollama create agent-v2 -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
ollama create agent-custom -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
ollama list
```

### Update Existing Agent
```bash
ollama rm winui-skill-agent
ollama create winui-skill-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

### Customize Behavior
```bash
# Edit the Modelfile
notepad .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified

# Change PARAMETER temperature, num_ctx, or SYSTEM prompt
# Then register with new name
ollama create my-custom-agent -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified
```

---

## If Something Fails

| Error | Solution |
|-------|----------|
| "Modelfile not found" | Use correct path: `.\Samples\OllamaWinUiSkillAgent.Modelfile.simplified` |
| "Ollama not running" | Start Ollama: `ollama serve` in another terminal |
| "Base model not found" | Download it: `ollama pull qwen3` |
| "Agent exists" | Delete first: `ollama rm winui-skill-agent` |

---

## Key Points

✓ **Command**: `ollama create <name> -f .\Samples\OllamaWinUiSkillAgent.Modelfile.simplified`  
✓ **Time**: 30 seconds to register  
✓ **Prerequisites**: Ollama running, qwen3 downloaded  
✓ **Verification**: `ollama list` shows your agent  
✓ **Usage**: `ollama run <name> "..."`  or with OllamaMcpHost  
✓ **Customization**: Edit Modelfile, re-register with new name  
✓ **Multiple agents**: Yes, unlimited variants  

---

## What Gets Registered?

The Modelfile (`OllamaWinUiSkillAgent.Modelfile.simplified`) contains:

- **Base model**: qwen3
- **Parameters**: temperature=0, num_ctx=32768, num_predict=-1
- **System prompt**: 8-step workflow for Windows UI automation
- **Configuration**: All safety checks and procedures

All of this is bundled as one registered agent.

---

## Next Steps

1. **Just register**: Copy the one-line command above, run it
2. **Understand it**: Read `HOW-TO-REGISTER-AGENT.md`
3. **Use it**: Follow "Use It with MCP Host" section above
4. **Customize**: Edit Modelfile and re-register as needed
5. **Deploy**: Use in your application or workflow

---

## See Also

- **Main workflow**: `AGENT-WORKFLOW-SIMPLIFIED.md`
- **Quick start**: `README-SIMPLIFIED.md`
- **Architecture**: `IMPLEMENTATION-GUIDE.md`
- **Troubleshooting**: `INDEX.md`

---

**You're all set!** The agent is registered and ready to use.
