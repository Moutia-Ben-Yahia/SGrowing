# ADR-004: Dual-Layer Swarm Persistence and Cryptographic HITL Approval Tokens

## Status
**Accepted & Implemented** (Commit `218df4b`, `3389a23`)

## Context
Autonomous AI agents executing complex multi-step missions (e.g. ad spend rebalancing, copy generation, outbound email blasts) require strict state persistence to resume across restarts, and rigorous Human-in-the-Loop (HITL) authorization gates for high-risk financial actions.

## Decision
1. **Dual-Layer Swarm Mission State (`IAutonomousSwarmPersistenceService`)**:
   - `AgentMission` (macro-goal) and `AgentMissionStep` (decomposed execution tasks) persisted in `ai.AgentMissions` and `ai.AgentMissionSteps`.
   - Tool execution audit ledger in `ai.AiToolExecutionLogs` capturing tool names, arguments, token usage, and durations.
2. **Cryptographic One-Time Approval Tokens (`IApprovalGatekeeperService`)**:
   - High-risk operations (e.g. ad spend adjustment > $500, bulk blast > 5,000 recipients) trigger `ApprovalRiskLevel.High`.
   - Generates 256-bit cryptographic one-time approval token.
   - Pauses mission in status `PausedForApproval` until valid token is presented by human operator.

## Consequences
- **Positive**: Guaranteed zero unintended ad spend without human sign-off; full resilience across daemon reboots.
- **Trade-off**: Requires operator intervention for spend reallocations exceeding threshold.
