# DevSwarm Setup Guide — House Management

## Recommended approach

Use DevSwarm as the orchestration layer and Git worktrees as the isolation boundary.

DevSwarm creates isolated workspaces/branches, each with its own terminal, filesystem and runtime. This makes it appropriate for running several agents in parallel.

Do not create one giant "do everything" agent.

Instead, use small, bounded workstreams.

---

## Step 1 — Add the repository

Open DevSwarm and add the House Management Git repository.

The repository should contain:

```text
AGENTS.md
TASKS.md
ARCHITECTURE.md
STATUS.md
autonomous-dev.ps1
```

The existing `.devswarm` directory can remain managed by DevSwarm.

Do not manually edit `.devswarm` internals unless DevSwarm documentation explicitly requires it.

---

## Step 2 — Verify the AI assistant

Your screenshot shows a DevSwarm workspace with `.devswarm` and the existing `src/HouseManagement.Api` project.

Connect the coding assistant you already use.

For Copilot CLI, verify:

```powershell
copilot --help
```

and:

```powershell
copilot -p "Read AGENTS.md and ARCHITECTURE.md. Summarize the project constraints without changing files."
```

Do this once before enabling autonomous execution.

---

## Step 3 — First workspace: architecture audit

Create:

```text
feature/architecture-audit
```

Prompt:

```text
Read AGENTS.md, ARCHITECTURE.md, TASKS.md and STATUS.md.

Inspect the existing HouseManagement.Api project.

Do not modify code.

Identify:
1. what has already been implemented,
2. what conflicts with the proposed architecture,
3. what should be preserved,
4. what should be changed later,
5. any risks in the existing authentication/JWT implementation.

Update STATUS.md with findings only.
```

Review the result yourself.

---

## Step 4 — Create independent workspaces

Recommended initial workspaces:

```text
feature/backend-foundation
feature/service-catalog
feature/househelp-directory
feature/angular-shell
test/integration-foundation
```

Do not start Booking until the core domain model and availability decisions are agreed.

---

## Step 5 — Use task-scoped prompts

Every agent should receive one task at a time.

Example:

```text
Implement T050 only.

Read:
- AGENTS.md
- ARCHITECTURE.md
- TASKS.md

Inspect existing code before editing.

Implement the task completely.

Run appropriate tests/build.

Do not implement unrelated tasks.

Update TASKS.md only for T050.

Stop if you encounter an architectural decision that requires human approval.
```

---

## Step 6 — Autonomous execution

For Copilot CLI, the current CLI supports programmatic prompts and an autopilot mode.

The included `autonomous-dev.ps1` uses this approach.

Example:

```powershell
.\autonomous-dev.ps1 -MaxTasks 1
```

Start with ONE task.

After you trust the workflow:

```powershell
.\autonomous-dev.ps1 -MaxTasks 3
```

Do not begin with 20 tasks.

---

## Step 7 — Review DevSwarm workspaces

Use DevSwarm Review Mode to inspect the workspace diff.

Review:

```text
git diff --check
git diff
dotnet build
dotnet test
```

For Angular tasks:

```text
npm install
npx ng build
npm test
```

Merge only after review.

---

## Step 8 — Parallel execution strategy

Good parallel example:

```text
             Lead
              |
      +-------+-------+
      |       |       |
   Services HouseHelp Angular
      |       |       |
      +-------+-------+
              |
           Booking
              |
        Operations UI
              |
             QA
```

Bad parallel example:

```text
Agent A -> HouseContext.cs
Agent B -> HouseContext.cs
Agent C -> Program.cs
Agent D -> authentication
```

Too much collision risk.

---

## Step 9 — Human approval points

Always review:

- authentication
- authorization
- database migrations
- booking concurrency
- anonymous client security
- rate limiting
- CORS
- file uploads
- payments
- production deployment

These are not good candidates for unattended merging.

---

## Step 10 — Recommended agent roles

### Lead / Architect

Reads:

```text
AGENTS.md
ARCHITECTURE.md
TASKS.md
STATUS.md
```

Coordinates work.

### Backend Builder

Owns:

```text
src/HouseManagement.Api
```

### Angular Builder

Owns:

```text
frontend/house-management-web
```

### QA Agent

Owns:

```text
tests
validation
security review
regression
```

The QA agent should not redesign features unless a defect requires it.

---

## Step 11 — Never let the autonomous loop become a rewrite loop

If an agent says:

```text
"I need to restructure the entire application"
```

stop.

Create a new architecture task:

```text
TXXX — Architecture proposal: ...
```

Review it manually.

Only then allow implementation.

---

## Step 12 — Daily workflow

A productive daily DevSwarm workflow is:

```text
1. Review TASKS.md
2. Pick 2–4 independent tasks
3. Create isolated workspaces
4. Start agents
5. Continue designing/reviewing while agents work
6. Inspect diffs
7. Run tests
8. Merge good work
9. Update TASKS.md
10. Start next workstream
```

The objective is not to remove yourself from engineering.

The objective is to remove yourself from repetitive prompting while keeping architecture, security and merge decisions under your control.
