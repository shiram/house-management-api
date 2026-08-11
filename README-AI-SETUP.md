# House Management — AI Development Setup

This directory contains the initial AI-assisted engineering control plane for the House Management project.

## Files

- `AGENTS.md` — permanent engineering rules for AI agents
- `ARCHITECTURE.md` — architectural decisions and module boundaries
- `TASKS.md` — ordered development queue
- `STATUS.md` — current project/workspace state
- `DEVSWARM-SETUP.md` — DevSwarm operating guide
- `autonomous-dev.ps1` — PowerShell loop for Copilot CLI autopilot
- `.github/copilot-instructions.md` — Copilot-specific repository instructions

## First run

1. Put these files at the repository root.
2. Commit them.
3. Open the repository in DevSwarm.
4. Create an architecture-audit workspace.
5. Ask the agent to inspect the existing API without changing it.
6. Review the audit.
7. Start with one implementation task.
8. Only after the workflow is stable, increase autonomous task count.

## PowerShell

Preview the next task without executing:

```powershell
.\autonomous-dev.ps1 -DryRun
```

Run one task:

```powershell
.\autonomous-dev.ps1 -MaxTasks 1
```

Run a specific task:

```powershell
.\autonomous-dev.ps1 -TaskId T050
```

Run up to three tasks:

```powershell
.\autonomous-dev.ps1 -MaxTasks 3
```

For safety, the script requires a clean Git working tree and does not automatically commit.

## Important

Autonomous coding should be used for bounded implementation work.

Human review remains mandatory for:

- authentication
- authorization
- database migrations
- booking concurrency
- anonymous client security
- rate limiting
- CORS
- payments
- production deployment
