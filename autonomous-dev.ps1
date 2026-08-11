[CmdletBinding()]
param(
    [int]$MaxTasks = 1,
    [int]$MaxAutopilotContinues = 12,
    [string]$TaskId = "",
    [switch]$DryRun,
    [switch]$SkipBuild,
    [switch]$SkipCommit
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$Message) {
    Write-Host "`n==> $Message" -ForegroundColor Cyan
}

function Assert-Command([string]$Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found in PATH."
    }
}

function Get-TaskQueue {
    $content = Get-Content -Path ".\TASKS.md" -Raw

    # Expected format:
    # - [ ] T123 — Task description
    # - [~] T123 — Task description
    $matches = [regex]::Matches(
        $content,
        '(?m)^- \[ \] (T\d+) — (.+)$'
    )

    return $matches | ForEach-Object {
        [PSCustomObject]@{
            Id          = $_.Groups[1].Value
            Description = $_.Groups[2].Value.Trim()
        }
    }
}

function Get-TaskById([string]$Id) {
    $tasks = Get-TaskQueue
    return $tasks | Where-Object { $_.Id -eq $Id } | Select-Object -First 1
}

function Get-NextTask {
    if ($TaskId) {
        $task = Get-TaskById $TaskId

        if (-not $task) {
            throw "Task '$TaskId' is not pending, does not exist, or is not in the expected format."
        }

        return $task
    }

    return Get-TaskQueue | Select-Object -First 1
}

function Invoke-CopilotTask($Task) {
    $prompt = @"
You are the implementation agent for the House Management project.

You MUST read these files before changing anything:
- AGENTS.md
- ARCHITECTURE.md
- TASKS.md
- STATUS.md

Your assigned task is:

$($Task.Id) — $($Task.Description)

Rules:
1. Implement ONLY this task unless a tiny dependency is strictly required.
2. Inspect existing code before editing.
3. Preserve the existing authentication/JWT implementation unless this task explicitly concerns it.
4. Follow the modular-monolith + vertical-slice architecture.
5. Keep API contracts and business rules server-side.
6. Add/update tests appropriate to the task.
7. Run the relevant build and tests.
8. Fix compilation/test failures before finishing.
9. Run 'git diff --check'.
10. Update TASKS.md and mark ONLY this task [x] when it is actually complete.
11. Update STATUS.md only if project status materially changes.
12. Do not mark the task complete if it is blocked.
13. Do not reset, revert or delete unrelated work.
14. Do not commit changes for another task.
15. If a major architectural/security/database decision is required, stop and document the blocker.

Definition of done:
- implementation complete
- tests/build pass
- diff reviewed
- TASKS.md updated
- no unrelated changes

When finished, provide a concise summary of files changed, tests run, and any remaining concerns.
"@

    if ($DryRun) {
        Write-Host "`n--- COPILOT PROMPT ---`n$prompt`n-----------------------" -ForegroundColor Yellow
        return 0
    }

    Write-Step "Running Copilot Autopilot for $($Task.Id)"

    & copilot `
        --autopilot `
        --allow-all `
        --max-autopilot-continues $MaxAutopilotContinues `
        -p $prompt

    return $LASTEXITCODE
}

function Assert-TaskCompleted([string]$Id) {
    $content = Get-Content -Path ".\TASKS.md" -Raw

    $pattern = "(?m)^- \[x\] $([regex]::Escape($Id)) — "
    if ($content -notmatch $pattern) {
        throw "Agent returned, but $Id was not marked [x] in TASKS.md. Stopping for human review."
    }
}

function Invoke-Builds {
    if ($SkipBuild) {
        Write-Host "Build checks skipped by -SkipBuild." -ForegroundColor Yellow
        return
    }

    Write-Step "Running backend build"

    $apiProject = Get-ChildItem -Path ".\src" -Filter "*.csproj" -Recurse -ErrorAction SilentlyContinue |
        Select-Object -First 1

    if ($apiProject) {
        & dotnet build $apiProject.FullName --no-restore
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed."
        }
    }
    else {
        Write-Host "No .csproj found under .\src. Skipping backend build." -ForegroundColor Yellow
    }

    $angularProject = Get-ChildItem -Path ".\frontend" -Filter "angular.json" -Recurse -ErrorAction SilentlyContinue |
        Select-Object -First 1

    if ($angularProject) {
        Write-Step "Running Angular build"

        Push-Location $angularProject.Directory.FullName
        try {
            & npx ng build
            if ($LASTEXITCODE -ne 0) {
                throw "Angular build failed."
            }
        }
        finally {
            Pop-Location
        }
    }
    else {
        Write-Host "No Angular project found under .\frontend. Skipping Angular build." -ForegroundColor Yellow
    }
}

function Show-GitSummary {
    Write-Step "Git status"

    & git status --short

    Write-Step "Diff check"

    & git diff --check
    if ($LASTEXITCODE -ne 0) {
        throw "git diff --check failed."
    }
}

Assert-Command "git"
Assert-Command "copilot"

if (-not (Test-Path ".\AGENTS.md")) {
    throw "AGENTS.md not found. Run this script from the repository root."
}

if (-not (Test-Path ".\TASKS.md")) {
    throw "TASKS.md not found. Run this script from the repository root."
}

if (-not (Test-Path ".\ARCHITECTURE.md")) {
    throw "ARCHITECTURE.md not found. Run this script from the repository root."
}

$gitStatus = @(git status --porcelain)

if ($gitStatus.Count -gt 0) {
    throw @"
Working tree is not clean.

The autonomous loop requires a clean workspace so an agent cannot accidentally
mix existing work with the task it is executing.

Commit/stash your existing changes first, or run the agent manually in DevSwarm.
"@
}

for ($i = 1; $i -le $MaxTasks; $i++) {

    $task = Get-NextTask

    if (-not $task) {
        Write-Host "`nNo pending tasks remain. Project queue is complete." -ForegroundColor Green
        break
    }

    Write-Host "`n----------------------------------------" -ForegroundColor DarkGray
    Write-Host "Task $i / $MaxTasks" -ForegroundColor DarkGray
    Write-Host "$($task.Id) — $($task.Description)" -ForegroundColor Green
    Write-Host "----------------------------------------" -ForegroundColor DarkGray

    $exitCode = Invoke-CopilotTask $task

    if ($exitCode -ne 0) {
        throw "Copilot exited with code $exitCode while processing $($task.Id)."
    }

    if ($DryRun) {
        break
    }

    Assert-TaskCompleted $task.Id
    Invoke-Builds
    Show-GitSummary

    if (-not $SkipCommit) {
        Write-Step "Commit policy"

        Write-Host @"
The script does not automatically create a commit.

Review the diff and commit from DevSwarm/your Git client when satisfied.

Recommended:
git add .
git commit -m "feat: $($task.Description)"
"@ -ForegroundColor Yellow
    }

    Write-Host "`n$($task.Id) completed successfully." -ForegroundColor Green

    # Prevent a specifically requested TaskId from being repeated.
    if ($TaskId) {
        break
    }
}

Write-Host "`nAutonomous development run finished." -ForegroundColor Green
