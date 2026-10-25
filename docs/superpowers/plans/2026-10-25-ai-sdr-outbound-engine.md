# Autonomous AI SDR & Cold Outbound Engine Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the complete Autonomous AI SDR & Cold Outbound Engine (.NET 10 & Angular 19), including Sender Mailbox Deliverability & Warmup, Multi-Step Cadences, AI Personalization Hook Generation, Inbound Reply Sentiment Classifier, Deal Pipeline Creation, and Cadence Dispatcher.

**Architecture:** Event-Driven Modular Monolith, PostgreSQL persistence with EF Core, Outbox pattern for asynchronous email/WhatsApp dispatching, Mailbox rotation with daily send pacing, and closed-loop handoff to Sub-Project 1 Shared Inbox and Deals CRM.

**Tech Stack:** .NET 10 (ASP.NET Core, EF Core, xUnit, FluentAssertions), PostgreSQL, Angular 19 (Signals, Tailwind).

## Global Constraints

- Backend must compile cleanly with `dotnet build` using .NET 10.
- All database queries must be scoped to `TenantId`.
- Outbound sends must strictly adhere to daily mailbox quotas and warmup ramp-up limits.
- Inbound positive prospect replies must automatically create a Deal and hand off to the Shared Inbox.
- Code organization must follow Vertical Slices.

---

### Task 1: Sender Mailbox & Warmup Deliverability Engine

**Files:**
- Create: `src/backend/MarketingStudio.Core/Entities/SenderAccount.cs`
- Create: `src/backend/MarketingStudio.Core/Entities/WarmupRecord.cs`
- Modify: `src/backend/MarketingStudio.Api/Data/MarketingStudioDbContext.cs`
- Create: `src/backend/MarketingStudio.Api/Modules/Outbound/MailboxService.cs`
- Create: `tests/MarketingStudio.Tests/Outbound/MailboxWarmupTests.cs`

**Interfaces:**
- Produces: `SenderAccount`, `WarmupRecord`, Mailbox daily quota validation and rotation algorithms.

- [ ] **Step 1: Write failing tests for mailbox quota rotation and warmup ramp calculations**
- [ ] **Step 2: Run tests to confirm failure**
- [ ] **Step 3: Implement SenderAccount, WarmupRecord, DbContext mappings, and MailboxService**
- [ ] **Step 4: Run tests to confirm pass**
- [ ] **Step 5: Commit mailbox & deliverability engine**

---

### Task 2: Multi-Step Cadence & Sequence Orchestration

**Files:**
- Create: `src/backend/MarketingStudio.Core/Entities/OutboundCampaign.cs`
- Create: `src/backend/MarketingStudio.Core/Entities/CadenceStep.cs`
- Create: `src/backend/MarketingStudio.Core/Entities/ProspectEnrollment.cs`
- Modify: `src/backend/MarketingStudio.Api/Data/MarketingStudioDbContext.cs`
- Create: `src/backend/MarketingStudio.Api/Modules/Outbound/CreateCampaignSlice.cs`
- Create: `src/backend/MarketingStudio.Api/Modules/Outbound/EnrollProspectsSlice.cs`
- Create: `tests/MarketingStudio.Tests/Outbound/CadenceOrchestrationTests.cs`

**Interfaces:**
- Produces: Campaign creation, step definition, prospect enrollment, next-run scheduling.

- [ ] **Step 1: Write failing tests for sequence step delays and prospect enrollment**
- [ ] **Step 2: Run tests to confirm failure**
- [ ] **Step 3: Implement OutboundCampaign, CadenceStep, ProspectEnrollment, and slices**
- [ ] **Step 4: Run tests to confirm pass**
- [ ] **Step 5: Commit cadence orchestration**

---

### Task 3: Autonomous AI SDR Personalization & Hook Generator

**Files:**
- Create: `src/backend/MarketingStudio.Api/Modules/Outbound/IAiSdrService.cs`
- Create: `src/backend/MarketingStudio.Api/Modules/Outbound/AiSdrService.cs`
- Create: `tests/MarketingStudio.Tests/Outbound/AiSdrPersonalizationTests.cs`

**Interfaces:**
- Consumes: `Contact` and `Company` details.
- Produces: Personalized 1-to-1 icebreaker hook, pain point analysis, customized template rendering.

- [ ] **Step 1: Write failing tests for AI SDR research hook generation and template variable substitution**
- [ ] **Step 2: Run tests to confirm failure**
- [ ] **Step 3: Implement IAiSdrService, prompt synthesis, and template renderer**
- [ ] **Step 4: Run tests to confirm pass**
- [ ] **Step 5: Commit AI SDR service**

---

### Task 4: Inbound Reply Sentiment Classifier & Closed-Loop Deal Handoff

**Files:**
- Create: `src/backend/MarketingStudio.Core/Entities/Deal.cs`
- Modify: `src/backend/MarketingStudio.Api/Data/MarketingStudioDbContext.cs`
- Create: `src/backend/MarketingStudio.Api/Modules/Outbound/ReplySentimentClassifier.cs`
- Create: `src/backend/MarketingStudio.Api/Modules/Outbound/ProspectReplyHandoffService.cs`
- Create: `tests/MarketingStudio.Tests/Outbound/SentimentAndHandoffTests.cs`

**Interfaces:**
- Consumes: Inbound messages from prospects.
- Produces: Sentiment classification (`Interested`, `Unsubscribe`, `OutOfOffice`), automatic CRM `Deal` creation, sequence termination, and Shared Inbox notification.

- [ ] **Step 1: Write failing tests for reply sentiment parsing and Deal pipeline creation**
- [ ] **Step 2: Run tests to confirm failure**
- [ ] **Step 3: Implement Deal entity, ReplySentimentClassifier, and ProspectReplyHandoffService**
- [ ] **Step 4: Run tests to confirm pass**
- [ ] **Step 5: Commit sentiment & closed-loop handoff**

---

### Task 5: Cadence Dispatcher Background Service

**Files:**
- Create: `src/backend/MarketingStudio.Api/Modules/Outbound/CadenceDispatcherService.cs`
- Create: `tests/MarketingStudio.Tests/Outbound/CadenceDispatcherTests.cs`

**Interfaces:**
- Consumes: Due `ProspectEnrollment` records, `SenderAccount` rotation pool.
- Produces: Outbound message rendering, Outbox enqueuing, step progression (`NextRunAtUtc`).

- [ ] **Step 1: Write failing tests for batch cadence dispatching and mailbox quota deduction**
- [ ] **Step 2: Run tests to confirm failure**
- [ ] **Step 3: Implement CadenceDispatcherService with concurrency-safe selection**
- [ ] **Step 4: Run tests to confirm pass**
- [ ] **Step 5: Commit cadence dispatcher**

---

### Task 6: Outbound Minimal API Endpoints & Registration

**Files:**
- Modify: `src/backend/MarketingStudio.Api/Program.cs`
- Create: `tests/MarketingStudio.Tests/Outbound/OutboundApiEndpointTests.cs`

**Interfaces:**
- Produces: `/api/outbound/mailboxes`, `/api/outbound/campaigns`, `/api/outbound/campaigns/{id}/enroll`.

- [ ] **Step 1: Write failing tests for outbound API endpoints**
- [ ] **Step 2: Run tests to confirm failure**
- [ ] **Step 3: Register services and endpoints in Program.cs**
- [ ] **Step 4: Run tests to confirm pass**
- [ ] **Step 5: Commit API endpoints**

---

### Task 7: Angular 19 Outbound & AI SDR Studio UI

**Files:**
- Create: `src/frontend/marketing-studio-web/src/app/features/outbound/outbound-studio.component.ts`
- Modify: `src/frontend/marketing-studio-web/src/app/app.routes.ts`
- Modify: `src/frontend/marketing-studio-web/src/app/features/inbox/inbox-shell.component.ts`

**Interfaces:**
- Consumes: Outbound API endpoints.
- Produces: Campaign builder view, Mailbox health & warmup scoreboard, Deal pipeline view.

- [ ] **Step 1: Create OutboundStudioComponent with Campaign Builder, Mailbox Warmup Scoreboard, and Pipeline Kanban**
- [ ] **Step 2: Wire up navigation link in InboxShellComponent**
- [ ] **Step 3: Verify Angular compiler builds cleanly (`npm run build`)**
- [ ] **Step 4: Commit Angular Outbound Studio**

---

### Task 8: End-to-End Outbound AI SDR Pipeline Test & Full Audit

**Files:**
- Create: `tests/MarketingStudio.Tests/E2E/OutboundSdrEndToEndTests.cs`

**Interfaces:**
- Tests the complete outbound loop:
  1. Mailbox pool configured with warmup ramp-up.
  2. Campaign created with 2-step cadence.
  3. Lead enrolled -> AI SDR synthesizes personalized hook.
  4. Dispatcher runs -> sends Step 1 via rotated mailbox.
  5. Prospect replies: "I am interested, let's schedule a call."
  6. Sentiment classifier detects `Interested` -> creates $5,000 Deal in CRM, stops sequence, and assigns conversation to Shared Inbox.

- [ ] **Step 1: Write comprehensive Outbound AI SDR E2E test**
- [ ] **Step 2: Execute all tests (`dotnet test`)**
- [ ] **Step 3: Build Angular client (`npm run build`)**
- [ ] **Step 4: Commit and finalize Sub-Project 2**
