# WinUI Skill Agent - Simplified Workflow - Master Index

## Overview

The WinUI Skill Agent workflow has been simplified and reorganized for improved clarity, usability, and maintainability. All original capabilities are preserved; only documentation and system prompts have been enhanced.

**Start here**: [README-SIMPLIFIED.md](#readme-simplified)

## Files by Purpose

### 🚀 Getting Started
**Goal**: Understand and run the system in 10 minutes

| File | Purpose | Read Time |
|------|---------|-----------|
| [README-SIMPLIFIED.md](README-SIMPLIFIED.md) | Quick start guide, file overview, quick reference | 5 min |
| [AGENT-WORKFLOW-SIMPLIFIED.md](AGENT-WORKFLOW-SIMPLIFIED.md) | Complete workflow explanation, examples, configuration | 10 min |

**Next step**: Follow quick-start in README-SIMPLIFIED.md

---

### 📚 Learning and Understanding
**Goal**: Deep understanding of the system

| File | Purpose | Read Time |
|------|---------|-----------|
| [IMPLEMENTATION-GUIDE.md](IMPLEMENTATION-GUIDE.md) | Architecture, component responsibilities, testing | 20 min |
| [COMPARISON.md](COMPARISON.md) | Original vs. simplified comparison, adoption path | 10 min |
| [SIMPLIFICATION-SUMMARY.md](SIMPLIFICATION-SUMMARY.md) | What was done, improvements, recommendations | 5 min |

**Next step**: After quick start, read IMPLEMENTATION-GUIDE.md for deeper understanding

---

### 🛠️ Implementation and Extension
**Goal**: Extend or modify the system

| File | Purpose | Read Time |
|------|---------|-----------|
| [IMPLEMENTATION-GUIDE.md](IMPLEMENTATION-GUIDE.md) | Architecture, component design, testing checklist | 20 min |
| Simplified skill files (see below) | Domain-specific guidance for extensions | Variable |
| [OllamaWinUiSkillAgent.Modelfile.simplified](OllamaWinUiSkillAgent.Modelfile.simplified) | Agent system prompt (modify for custom behavior) | 10 min |

**Next step**: Review IMPLEMENTATION-GUIDE.md extending section, then modify skill files

---

### ⚠️ Troubleshooting
**Goal**: Fix issues quickly

| Resource | Location | Help With |
|----------|----------|-----------|
| Troubleshooting table | README-SIMPLIFIED.md | Common issues and solutions |
| Error handling | IMPLEMENTATION-GUIDE.md | Detailed error procedures |
| Failure handling | AgentSkills/WinUiSkillExecutor.skill.md.simplified | How agent handles failures |
| Common issues table | AgentSkills/WinUiSkillExecutor.skill.md.simplified | Specific scenarios |

**Next step**: Check README-SIMPLIFIED.md troubleshooting table first

---

## Files in Detail

### In Samples/ root directory

#### [OllamaWinUiSkillAgent.Modelfile.simplified](OllamaWinUiSkillAgent.Modelfile.simplified)
**What**: Ollama model definition (system prompt)
**Use when**: Creating the Ollama model
**Key sections**:
- Role definition
- Supported UI actions
- 8-step workflow (numbered and explicit)
- Safety constraints
- Domain-specific guidance (Calculator, general UI)
- Error handling

**Command**:
```bash
ollama create winui-skill-agent -f OllamaWinUiSkillAgent.Modelfile.simplified
```

#### [README-SIMPLIFIED.md](README-SIMPLIFIED.md)
**What**: Quick start guide and file overview
**Length**: ~200 lines
**Sections**:
- Overview
- Files by purpose (table)
- Quick start (4 steps)
- Simplified workflow diagram
- Key improvements summary
- Changes from original
- MCP server interface
- Configuration examples (2 modes)
- Troubleshooting table
- Switching/developer info

**Start here if**: You have 5 minutes

#### [AGENT-WORKFLOW-SIMPLIFIED.md](AGENT-WORKFLOW-SIMPLIFIED.md)
**What**: Complete workflow explanation
**Length**: ~400 lines
**Sections**:
- Overview
- Workflow architecture (5 phases)
- Key files and responsibilities
- Supported expressions table
- Execution flow diagram
- Safety procedures
- Configuration examples (2 modes)
- Troubleshooting guide
- Next steps

**Start here if**: You have 15 minutes

#### [IMPLEMENTATION-GUIDE.md](IMPLEMENTATION-GUIDE.md)
**What**: Architecture, components, and implementation details
**Length**: ~600 lines
**Sections**:
- Architecture overview with diagram
- Component responsibilities (5 components in detail)
- Workflow execution flow walkthrough
- File and directory structure
- Extending the system (4 patterns)
- Testing checklist (20+ items)
- Performance considerations
- Security and safety

**Start here if**: You need to understand or modify the system

#### [COMPARISON.md](COMPARISON.md)
**What**: Original vs. simplified comparison
**Length**: ~350 lines
**Sections**:
- Philosophy differences
- File structure comparison
- Documentation organization
- Content comparison by topic
- Workflow clarity comparison
- Safety and approval gates
- Capability comparison
- Adoption path
- Recommendations

**Start here if**: You want to understand what changed

#### [SIMPLIFICATION-SUMMARY.md](SIMPLIFICATION-SUMMARY.md)
**What**: Summary of simplification work
**Length**: ~250 lines
**Sections**:
- What was done
- Deliverables (5 categories)
- Workflow improvements
- Key enhancements (4 categories)
- File organization
- How to use
- Backward compatibility
- Testing recommendations
- Conclusion

**Start here if**: You want a high-level overview

---

### In Samples/AgentSkills/ directory

#### [CalculatorExpression.skill.md.simplified](AgentSkills/CalculatorExpression.skill.md.simplified)
**What**: Guide for parsing and transforming arithmetic expressions
**Length**: ~80 lines
**When loaded**: User requests Calculator-related tasks
**Sections**:
- Supported expressions table (basic + scientific)
- Transformation process (8 steps)
- Task structure examples
- Example transformations
- Validation checklist
- Error cases with specific handling

**Use for**: Understanding how to map expressions to button clicks

#### [WinUiSkillEditor.skill.md.simplified](AgentSkills/WinUiSkillEditor.skill.md.simplified)
**What**: Guide for adapting existing skills to new requests
**Length**: ~160 lines
**When loaded**: User requests to modify an existing skill
**Sections**:
- Procedure (8 detailed steps)
- Complete transformation example
- Input/output specification table
- Validation checklist
- Safety rules
- Common patterns with examples
- MCP integration guidance

**Use for**: Understanding how to adapt skills safely

#### [WinUiSkillExecutor.skill.md.simplified](AgentSkills/WinUiSkillExecutor.skill.md.simplified)
**What**: Guide for safe skill execution with approval gates
**Length**: ~130 lines
**When loaded**: Before executing any skill
**Sections**:
- Safety procedure (6 phases)
- Each phase with detailed instructions
- Failure handling section
- Task result interpretation
- MCP tools reference table
- Common issues with solutions
- Notes on safety

**Use for**: Understanding execution safety procedures

---

## Quick Navigation

### By Task

**"I want to run a Calculator expression"**
1. README-SIMPLIFIED.md → Quick start section
2. AGENT-WORKFLOW-SIMPLIFIED.md → Supported expressions

**"I want to understand the system"**
1. README-SIMPLIFIED.md (5 min overview)
2. IMPLEMENTATION-GUIDE.md (20 min deep dive)
3. Component responsibility sections
4. Architecture diagram

**"I want to extend or modify"**
1. IMPLEMENTATION-GUIDE.md → Extending the system section
2. Relevant skill file (CalculatorExpression, Editor, or Executor)
3. Testing checklist
4. COMPARISON.md → For context

**"I'm having a problem"**
1. README-SIMPLIFIED.md → Troubleshooting table
2. Relevant skill file → Common issues
3. IMPLEMENTATION-GUIDE.md → Error handling section
4. AGENT-WORKFLOW-SIMPLIFIED.md → Troubleshooting guide

**"I want to know what changed"**
1. SIMPLIFICATION-SUMMARY.md (5 min overview)
2. COMPARISON.md (10 min detailed comparison)
3. README-SIMPLIFIED.md → Changes from original section

---

## Workflow at a Glance

```
User Request: "compute 45*67"
		↓
Agent loads:
  • Skill JSON (CalculatorButtonLibrary)
  • Instructions (CalculatorExpression.skill.md)
		↓
Agent processes:
  1. Parses: 4, 5, *, 6, 7
  2. Maps to: num4Button, num5Button, multiplyButton, num6Button, num7Button, equalButton
  3. Builds: Complete skill JSON with all tasks
		↓
Agent validates (DRY RUN):
  • Calls run_skill with dryRun=true
  • Reviews all task results
		↓
Agent requests approval:
  "I will click Search, type CALCULATOR, etc.
   Ready? Type RUN"
		↓
User approves: "RUN"
		↓
Agent executes (REAL):
  • Calls run_skill with dryRun=false
  • Reports task results
		↓
Agent verifies:
  • Queries desktop for result
  • Displays: "45 * 67 = 3015 ✓"
		↓
Agent requests cleanup: "Type CLOSE"
		↓
User approves: "CLOSE"
		↓
Agent cleans up and returns to prompt
```

---

## Configuration Quick Reference

### Mode 1: Fast (Deterministic Calculator)
```bash
dotnet run --project .\Samples\OllamaMcpHost\OllamaMcpHost.csproj -c Release --no-build -- \
  --model winui-skill-agent:latest \
  --skill .\Samples\Calculator.winui-skill.json
```
- Use: `compute 45+67` (basic arithmetic)
- Fast: No Ollama for basic expressions
- Best for: Simple arithmetic, quick testing

### Mode 2: Full (Ollama-Driven with Instructions)
```bash
dotnet run --project .\Samples\OllamaMcpHost\OllamaMcpHost.csproj -c Release --no-build -- \
  --model winui-skill-agent:latest \
  --skill .\Samples\CalculatorButtonLibrary.winui-skill.json \
  --skills-folder .\Samples\AgentSkills
```
- Use: `compute sqrt(144)`, `compute ln(e)` (scientific)
- Flexible: All requests through Ollama
- Best for: Scientific expressions, complex workflows

---

## Reference

### Key Directories
- **Documentation**: This folder (Samples/)
- **Skills**: Samples/AgentSkills/
- **Recorded skills**: Samples/*.winui-skill.json
- **MCP server**: WinUiAutomation.McpServer/
- **MCP host**: Samples/OllamaMcpHost/

### Key Commands

**Create model**:
```bash
ollama create winui-skill-agent -f Samples/OllamaWinUiSkillAgent.Modelfile.simplified
```

**Run with instructions**:
```bash
dotnet run --project Samples/OllamaMcpHost/OllamaMcpHost.csproj -c Release --no-build -- \
  --model winui-skill-agent:latest \
  --skill Samples/CalculatorButtonLibrary.winui-skill.json \
  --skills-folder Samples/AgentSkills
```

**At prompt**:
```
>>> compute 45*67
>>> compute sqrt(144)
>>> /quit
```

---

## Related Documentation

- **Original workflow** (reference): `docs/AI-WORKFLOW-SKILL.md`
- **MCP server details**: `WinUiAutomation.McpServer/`
- **MCP host details**: `Samples/OllamaMcpHost/README.md`

---

## Document Statistics

| Document | Lines | Category | Purpose |
|----------|-------|----------|---------|
| README-SIMPLIFIED.md | 200 | Quick Start | File guide + quick reference |
| AGENT-WORKFLOW-SIMPLIFIED.md | 400 | Main Doc | Complete workflow |
| IMPLEMENTATION-GUIDE.md | 600 | Reference | Architecture + implementation |
| COMPARISON.md | 350 | Reference | Original vs. simplified |
| SIMPLIFICATION-SUMMARY.md | 250 | Summary | What was done |
| CalculatorExpression.skill.md.simplified | 80 | Skill | Expression guidance |
| WinUiSkillEditor.skill.md.simplified | 160 | Skill | Skill editing guidance |
| WinUiSkillExecutor.skill.md.simplified | 130 | Skill | Execution safety |
| **Total** | **~2170** | **8 files** | **Complete docs** |

---

## Support

For issues or questions:
1. Check the relevant troubleshooting section
2. Review the appropriate skill file
3. Consult IMPLEMENTATION-GUIDE.md
4. Reference original docs if needed
5. Check OllamaMcpHost README.md for host-specific issues

---

**Last updated**: 2026-11-22  
**Version**: 1.0 (Simplified Release)  
**Status**: Complete and tested
