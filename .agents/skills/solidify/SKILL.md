---
name: solidify
description: "Release quality gate and SOLID architecture evaluator. Analyzes Git diffs, runs multi-peer reviews with role-swap (Peer A, Peer B, Arbiter C), evaluates SOLID principles (50% of quality score: Single Responsibility, Open-Closed, Liskov Substitution, Interface Segregation, Dependency Inversion), checks release gates (tests, security, static quality, performance, migrations, env changes, lighthouse, zap/k6), supports JEV System One deterministic decision engine, and emits canonical JSON release reports, HTML dashboards, and PDF reports with PASS/WARN/FAIL verdicts."
license: MIT
metadata:
  version: "1.0.0"
  author: daraujo85
  repository: "https://github.com/daraujo85/solidify"
---

# Solidify

Solidify is a release quality gate and architectural compliance engine. It analyzes repository Git diffs, orchestrates multi-peer reviews with blind role-swapping, computes an objective Release Quality Score centered on **SOLID** design principles (accounting for 50% of the overall score), evaluates release risk gates, and emits verifiable JSON reports, standalone HTML dashboards, and PDF release documents.

---

## 1. Quick Reference & Core Commands

Execute Solidify from the working directory using the local binary or wrapper:

| Task | Command | Description |
|---|---|---|
| **Diagnostics** | `solidify doctor` | Validates local prerequisites (Docker, Git, JEV, analyzers, stores). |
| **Initialization** | `solidify init` | Scaffolds `.solidify/` directory and `solidify.json` configuration. |
| **Execution** | `solidify run --profile <profile>` | Runs diff analysis, analyzers, peer reviews, and gate evaluation. |
| **Report Export** | `solidify report --format json --run <id>` | Exports or pretty-prints canonical `release-report.json`. |
| **PDF Generation** | `solidify report --format pdf --run <id> --out <path>` | Compiles an audit-grade release certificate / PDF report. |
| **Web Dashboard** | `solidify dashboard --port 8080` | Serves the local interactive web UI over `.solidify/out`. |
| **MCP Server** | `solidify mcp serve` | Starts the stdio MCP server for autonomous agent workflows. |
| **MCP Setup** | `solidify mcp setup` | Emits configuration snippets for agent clients (Claude Code, Antigravity, etc.). |

---

## 2. Profiles and Gate Thresholds

Solidify supports three operational profiles calibrated for different development stages:

| Profile | Min Score | Peer Reviews | Arbiter | Artifacts | Intended Use |
|---|---|---|---|---|---|
| `quick` | **60** | Peer A only | No | JSON | Dev branch smoke check, PR pre-commit, fast CI. |
| `release` | **75** | Peer A + Peer B (blind) | Yes (Arbiter C) | JSON + HTML + PDF | Staging & Production releases, sprint sign-offs. |
| `contractual` | **85** | 2 Distinct External Models | Yes (Arbiter C) | JSON + PDF Certificate | Audited releases, regulated environments, client handoffs. |

---

## 3. The Quality Score Formula

The Solidify Score aggregates multiple architectural and engineering dimensions into a 0–100 score:

```text
Quality Score = (SOLID × 0.50) + (Security × 0.15) + (Static Quality × 0.15) + (Tests × 0.10) + (Performance × 0.05) + (Frontend × 0.05)
```

### The SOLID Pillars (50% of Total Score)
* **S — Single Responsibility Principle**: Detects God-classes, high cyclomatic complexity growth, multiple reason-to-change violations in diffs.
* **O — Open-Closed Principle**: Detects brittle switch statements on types, hardcoded conditionals instead of polymorphism or strategies.
* **L — Liskov Substitution Principle**: Identifies improper inheritance, thrown `NotImplementedException`, broken contracts or altered preconditions.
* **I — Interface Segregation Principle**: Flags bloated interfaces, unused interface method implementations, tight client couplings.
* **D — Dependency Inversion Principle**: Checks direct instantiations of volatile classes, missing abstractions, tight database or hardware bindings.

### Gate Verdicts
* **PASS**: Score meets profile threshold, zero critical/high security issues, no test regressions.
* **WARN**: Score meets threshold with non-blocking warnings (e.g., minor SOLID degradation or unacknowledged migrations).
* **FAIL**: Score below threshold, critical vulnerabilities, test suite failure, or unhandled breaking changes.

---

## 4. Autonomous Agent Workflow (Protocol)

When an AI agent runs Solidify to audit code or evaluate a release, it must follow this canonical sequence:

```bash
# 1. Environment Diagnostics
solidify doctor --json

# 2. Inspect Git Diff & Build Context
# Compares working tree or target branch against base reference (e.g. main)
solidify run --profile quick --dir . --base origin/main --head HEAD

# 3. Export Canonical Artifact
solidify report --format json --run latest

# 4. (Optional) Compile Audit PDF
solidify report --format pdf --run latest --out ./release-report.pdf
```

### Multi-Peer & Arbiter Architecture
1. **Evidence Collection**: Solidify collects Git diffs, changed components, and static metrics.
2. **Peer A Review**: The first model or agent reviews the bounded diff against the SOLID rubric.
3. **Peer B Review (Role Swap)**: A second model evaluates the same diff independently, blind to Peer A's findings.
4. **Divergence Mapping**: Solidify computes discrepancies between Peer A and Peer B.
5. **Arbiter C Review**: An independent arbiter resolves divergences with the evidence bundle and produces the final consensus.

---

## 5. JEV System One Integration (Optional)

Solidify integrates with **JEV System One** (TypeSafe AI deterministic sub-300ms decision engine). Enable via environment variable:

```bash
export JEV_API_KEY="apikey_..."
```

### 1. Gate Applicability via JEV
Decides the 8 gates (sonar, tests, security, lighthouse, zap, k6, migration, env) deterministically:
```json
{
  "applicability": {
    "jev": {
      "enabled": true,
      "api_key_env": "JEV_API_KEY",
      "mode": "advisory"
    }
  }
}
```

### 2. Anti-Regression Analyzer (`jev_regression`)
Checks the release diff against 4 regression signals (reducing score by 15 points per signal):
1. Public contract / signature breaking change (Critical).
2. Functional logic weakening or deletion (High).
3. Weakened test assertions to bypass CI (High).
4. Side-effects on shared state or schema (Medium).

---

## 6. Configuration Reference (`solidify.json`)

To scaffold a configuration file in the project root:
```bash
solidify init
```

Example configuration structure:
```json
{
  "schema_version": "1.0.0",
  "project": {
    "name": "search_a_church",
    "default_base": "origin/main",
    "artifact_dir": ".solidify"
  },
  "analysis": {
    "default_profile": "quick",
    "use_merge_base": true
  },
  "profiles": {
    "quick": { "require_peer_a": true, "require_peer_b": false, "require_arbiter": false },
    "release": { "require_peer_a": true, "require_peer_b": true, "require_arbiter": true, "generate_pdf": true },
    "contractual": { "require_peer_a": true, "require_peer_b": true, "require_arbiter": true, "generate_pdf": true }
  },
  "scoring": {
    "weights": { "solid": 50, "static_quality": 15, "security": 15, "tests": 10, "performance": 5, "frontend": 5 },
    "gate": {
      "minimum_quality_score": 75,
      "minimum_solid_score": 70,
      "fail_on_new_security_severities": ["critical", "high"],
      "fail_on_test_failure": true
    }
  }
}
```

---

## 7. MCP Tools Reference

When running `solidify mcp serve`, the following tools are exposed over JSON-RPC stdio:
* `solidify_begin_review`: Starts a review session, runs applicable analyzers, and yields `run_id` and evidence manifest.
* `solidify_get_evidence_manifest`: Fetches the structural summary and file refs of the run.
* `solidify_get_diff_chunk`: Retrieves hunk-level diffs with before/after line context.
* `solidify_get_context`: Bounded file exploration for evaluating inheritance, contracts, or LSP compliance.
* `solidify_submit_peer_review`: Submits Peer A / Peer B review validating against `peer-review.schema.json`.

---

## 8. Binary Location & Execution

The Solidify binary is located at:
* **Windows**: `<skill-path>/bin/solidify.exe` (or `solidify.cmd` / `solidify.ps1`)
* **POSIX / WSL**: `<skill-path>/bin/solidify`

All subcommands exit with code `0` on success and non-zero on error or gate failure.
