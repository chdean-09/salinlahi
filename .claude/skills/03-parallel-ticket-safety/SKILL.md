---
name: 03-parallel-ticket-safety
description: Compare planned READY Salinlahi tickets and classify pairs as SAFE, SOFT_CONFLICT, or BLOCKED for parallel implementation, using plan metadata plus known Unity/repo conflict hotspots. Decides concurrency only; never plans, implements, or integrates.
---

# Parallel Ticket Safety (Salinlahi)

Input: the READY tickets' implementation plans (each plan's **Parallel Orchestration Metadata** section: expected files, expected systems, integration risk). Output: parallel groups + conflict classifications + an integration-order hint.

## Classification

- `SAFE` — no shared hard dependency and no expected file/system overlap.
- `SOFT_CONFLICT` — logically independent but likely to touch the same file or shared system. Parallel implementation may proceed; integration must be serialized with re-sync/re-validate, and the second branch re-reviewed if the first changes shared behavior.
- `BLOCKED` — one ticket needs the other's unfinished output, or both must edit the same Unity serialized asset (see below).
- `UNKNOWN` — plans lack enough evidence; say what is missing.

Do not serialize tickets merely for same-epic, same-feature-area, or similar titles. Require concrete overlap evidence.

## Repository conflict knowledge (verified 2026-08-23)

**High-collision C# files** (repeatedly conflicted across past PRs):
`Assets/Scripts/Core/ProgressManager.cs`, `Core/GameManager.cs`, `Core/EventBus.cs`, `Core/SaveManager.cs`, `Gameplay/LevelFlowController.cs`, `Data/LevelConfigSO.cs`, `Data/Validation/CampaignConfigValidator.cs`, and `docs/backlog/technical-work.md`. Two plans touching any of these → at minimum `SOFT_CONFLICT`.

**Unity serialized assets — stricter rule.** `.unity`, `.prefab`, `.asset` use Force Text + UnityYAMLMerge attributes, but the local UnityYAMLMerge mergespec is broken on this machine (merge driver fails; fallback is manual/plumbing resolution). Therefore:
- two tickets editing the **same** scene/prefab/`.asset` → `BLOCKED` for concurrent work (run sequentially);
- same-directory but different assets → `SAFE`;
- either ticket editing `ProjectSettings/*`, `Packages/manifest.json`, or `packages-lock.json` → `SOFT_CONFLICT` with everything else that ships that wave.

**Shared foundations:** a ticket introducing/altering a shared interface, event, or persistence contract (`Data/Persistence/*`, `Data/Learning/*`, EventBus events) is `SOFT_CONFLICT` with consumers planned in the same wave and should integrate **first**.

## Output contract

```
PARALLEL GROUP
- SALIN-a, SALIN-b, SALIN-c

SOFT_CONFLICT
- SALIN-b ↔ SALIN-c   reason: both touch ProgressManager.cs

BLOCKED
- SALIN-d ← SALIN-a   reason: needs a's new API / same scene file

INTEGRATION ORDER HINT
1. SALIN-a (unblocks d; shared foundation)
2. SALIN-b  3. SALIN-c (re-sync after b)
```
