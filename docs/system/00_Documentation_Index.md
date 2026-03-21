# 00 — Documentation Index
**Project:** Salinlahi
**Version:** 1.0
**Date:** 2026-03-19
**Owner:** Jon Wayne Cabusbusan (Systems Lead)
**Review Cadence:** End of each sprint (every two weeks)

---

## Purpose

This index is the single entry point for all Salinlahi system documentation. Every document listed here must remain consistent with the authoritative source hierarchy:

1. `docs/capstone/Salinlahi.md` — Project Study Report (highest authority)
2. `docs/capstone/GDD.md` — Game Design Document
3. Live implementation in `Assets/Scripts/`
4. `docs/capstone/TDD.md` — Technical Design Document

---

## Document Map

| # | File | Description | Owner | Status |
|---|------|-------------|-------|--------|
| 00 | `00_Documentation_Index.md` | This file. Master index and governance rules. | Systems Lead | Live |
| 01 | `01_System_Overview.md` | Product purpose, scope, non-goals, runtime context, architecture summary | Systems Lead | Live |
| 02 | `02_Architecture_and_Runtime_Flow.md` | Scene lifecycle, manager lifecycle, event-driven interactions, sequence flows | Systems Lead | Live |
| 03 | `03_Core_Systems.md` | GameManager, SceneLoader, AudioManager, EventBus, Singleton policy | Systems Lead | Live |
| 04 | `04_Gameplay_Systems.md` | Enemy lifecycle, movement, combat resolution, win/lose, wave progression | Gameplay Dev | Live |
| 05 | `05_Data_Contracts_and_ScriptableObjects.md` | All SO schemas, field definitions, validation rules, authoring guidelines | Designer/Owner | Live |
| 06 | `06_UI_UX_and_Player_Flow.md` | Screen flows, HUD behavior, game over flow, accessibility | UI/UX Dev | Live |
| 07 | `07_Content_Pipeline.md` | Characters, enemies, levels, waves, naming conventions, asset deps | Designer/Owner | Live |
| 08 | `08_Mobile_Performance_and_Offline_Constraints.md` | Runtime budgets, pooling strategy, offline guarantees, device risks | Systems Lead | Live |
| 09 | `09_Test_Strategy_and_Acceptance_Criteria.md` | Test matrix, functional test cases, regression checklist, UAT criteria | QA (whole team) | Live |
| 10 | `10_Requirements_Traceability_Matrix.md` | Req ID → source → evidence → test → gap status | Systems Lead | Live |
| 11 | `11_Risks_Dependencies_and_Mitigations.md` | Technical risks, production deps, mitigations, owners | Scrum Master | Live |
| 12 | `12_Glossary_and_Naming_Standard.md` | Canonical terms, forbidden synonyms, naming rules | Systems Lead | Live |
| 13 | `13_Document_Change_Log.md` | Versioned history of all doc changes | Systems Lead | Live |

---

## Review and Update Rules

1. Documents are updated at the **end of each sprint** or when an implementation decision contradicts a prior entry.
2. Any contradiction between documents must be escalated to the Systems Lead within one working day.
3. The `10_Requirements_Traceability_Matrix.md` is the authoritative gap tracker. P0 gaps block sprint sign-off.
4. Generating new documentation that invents unimplemented features is prohibited.

---

## Team Ownership

| Role | Name | Responsibility |
|------|------|----------------|
| Product Owner / Designer | Chad Andrada | Level data, wave configs, character assets, content pipeline |
| Systems Lead (Scrum Master) | Jon Wayne Cabusbusan | Core systems, architecture, documentation governance |
| UI/UX Developer | Jeff Andre Millan | UI scripts, screen flows, HUD, accessibility |
| Audio / Polish / Build | Ian Clyde Tejada | Audio assets, build pipeline, platform submissions |

[EVIDENCE: docs/capstone/GDD.md, §6.1 Milestones — team size 4, roles defined]
