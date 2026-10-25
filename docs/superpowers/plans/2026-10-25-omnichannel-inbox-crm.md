# Omnichannel Inbox & CRM Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the unified .NET 10 and Angular 19 Inbound Omnichannel Inbox, CRM Core, Dual WhatsApp/Email/Webchat Channel Adapters, Transactional Outbox Engine, SignalR Real-Time Hub, and Hybrid AI Copilot.

**Architecture:** Event-Driven Modular Monolith with Vertical Slice Architecture in .NET 10, PostgreSQL persistence with JSONB custom fields, Transactional Outbox pattern for zero-loss message processing, SignalR for sub-100ms bidirectional event streaming, and an Angular 19 reactive standalone UI.

**Tech Stack:** .NET 10 (ASP.NET Core, EF Core, SignalR, xUnit), PostgreSQL, Angular 19 (TypeScript, Signals, Tailwind CSS).

## Global Constraints

- Backend must compile cleanly with `dotnet build` using .NET 10.
- All database queries must be scoped to `TenantId`.
- Channel webhooks must immediately write through the Transactional Outbox to prevent dropped messages.
- Real-time communication must use ASP.NET Core SignalR.
- Code organization must follow Vertical Slices (organized by business feature, not generic `Controllers/` or `Services/`).

---

### Task 1: Scaffolding .NET 10 Solution & Core Foundation

**Files:**
- Create: `MarketingStudio.sln`
- Create: `src/backend/MarketingStudio.Core/MarketingStudio.Core.csproj`
- Create: `src/backend/MarketingStudio.Core/Common/Result.cs`
- Create: `src/backend/MarketingStudio.Core/Tenancy/ITenantContext.cs`
- Create: `src/backend/MarketingStudio.Core/Outbox/IOutbox.cs`
- Create: `src/backend/MarketingStudio.Api/MarketingStudio.Api.csproj`
- Create: `src/backend/MarketingStudio.Api/Program.cs`
- Create: `tests/MarketingStudio.Tests/MarketingStudio.Tests.csproj`
- Create: `tests/MarketingStudio.Tests/Common/ResultTests.cs`

**Interfaces:**
- Produces: `Result<T>`, `ITenantContext`, `IOutbox`, API Host bootstrapping.

- [ ] **Step 1: Write the failing test for Result<T>**
- [ ] **Step 2: Run test to confirm failure**
- [ ] **Step 3: Implement Core project, Result<T>, ITenantContext, and API scaffolding**
- [ ] **Step 4: Run test to confirm pass**
- [ ] **Step 5: Commit scaffolding**

---

### Task 2: Database Persistence & Transactional Outbox Engine

**Files:**
- Create: `src/backend/MarketingStudio.Core/Entities/Tenant.cs`
- Create: `src/backend/MarketingStudio.Core/Entities/User.cs`
- Create: `src/backend/MarketingStudio.Core/Entities/OutboxMessage.cs`
- Create: `src/backend/MarketingStudio.Api/Data/MarketingStudioDbContext.cs`
- Create: `src/backend/MarketingStudio.Api/Services/OutboxProcessorHostedService.cs`
- Create: `tests/MarketingStudio.Tests/Outbox/OutboxProcessorTests.cs`

**Interfaces:**
- Consumes: `IOutbox`, `ITenantContext`.
- Produces: `MarketingStudioDbContext`, `OutboxMessage` lifecycle, asynchronous background outbox dispatcher.

- [ ] **Step 1: Write unit test verifying outbox message serialization and status transition**
- [ ] **Step 2: Run test to confirm failure**
- [ ] **Step 3: Implement EF Core DbContext, Outbox entities, and OutboxProcessorHostedService**
- [ ] **Step 4: Run test to confirm pass**
- [ ] **Step 5: Commit persistence & outbox engine**

---

### Task 3: CRM Vertical Slices (Contacts, Companies, JSONB Custom Fields)

**Files:**
- Create: `src/backend/MarketingStudio.Core/Entities/Contact.cs`
- Create: `src/backend/MarketingStudio.Core/Entities/Company.cs`
- Create: `src/backend/MarketingStudio.Core/Entities/Activity.cs`
- Create: `src/backend/MarketingStudio.Api/Modules/CRM/CreateContactSlice.cs`
- Create: `src/backend/MarketingStudio.Api/Modules/CRM/GetContactSlice.cs`
- Create: `src/backend/MarketingStudio.Api/Modules/CRM/SearchContactsSlice.cs`
- Create: `tests/MarketingStudio.Tests/CRM/ContactSlicesTests.cs`

**Interfaces:**
- Consumes: `MarketingStudioDbContext`, `ITenantContext`.
- Produces: Contact CRUD endpoints, JSONB attribute validation, customer 360 search.

- [ ] **Step 1: Write failing tests for Contact creation and JSONB validation**
- [ ] **Step 2: Run tests to confirm failure**
- [ ] **Step 3: Implement Contact entity, DbContext mappings, and CreateContact / GetContact slices**
- [ ] **Step 4: Run tests to confirm pass**
- [ ] **Step 5: Commit CRM vertical slices**

---

### Task 4: Omnichannel Messaging & Conversation State Machine

**Files:**
- Create: `src/backend/MarketingStudio.Core/Entities/Inbox.cs`
- Create: `src/backend/MarketingStudio.Core/Entities/Conversation.cs`
- Create: `src/backend/MarketingStudio.Core/Entities/Message.cs`
- Create: `src/backend/MarketingStudio.Api/Modules/Conversations/SendMessageSlice.cs`
- Create: `src/backend/MarketingStudio.Api/Modules/Conversations/ChangeStatusSlice.cs`
- Create: `src/backend/MarketingStudio.Api/Modules/Conversations/GetConversationMessagesSlice.cs`
- Create: `tests/MarketingStudio.Tests/Conversations/ConversationStateMachineTests.cs`

**Interfaces:**
- Consumes: `MarketingStudioDbContext`, `ITenantContext`.
- Produces: Conversation lifecycle state machine (`Open`, `Snoozed`, `Resolved`), normalized messages.

- [ ] **Step 1: Write failing tests for conversation state transitions and message threading**
- [ ] **Step 2: Run tests to confirm failure**
- [ ] **Step 3: Implement Conversation entities, status state machine, and message slices**
- [ ] **Step 4: Run tests to confirm pass**
- [ ] **Step 5: Commit conversation engine**

---

### Task 5: Channel Adapters & Ingress Webhooks (WhatsApp Cloud, Evolution-API, Email)

**Files:**
- Create: `src/backend/MarketingStudio.Core/Channels/IChannelAdapter.cs`
- Create: `src/backend/MarketingStudio.Core/Channels/InboundChannelMessage.cs`
- Create: `src/backend/MarketingStudio.Api/Modules/Channels/WhatsAppCloudAdapter.cs`
- Create: `src/backend/MarketingStudio.Api/Modules/Channels/EvolutionApiAdapter.cs`
- Create: `src/backend/MarketingStudio.Api/Modules/Channels/EmailAdapter.cs`
- Create: `src/backend/MarketingStudio.Api/Modules/Channels/WebhookIngressEndpoints.cs`
- Create: `tests/MarketingStudio.Tests/Channels/ChannelAdapterTests.cs`

**Interfaces:**
- Consumes: Channel webhooks (Meta Cloud API, Evolution-API Baileys, Email parser).
- Produces: Normalized `InboundChannelMessage`, atomic insertion into Transactional Outbox.

- [ ] **Step 1: Write failing tests verifying parser accuracy for Meta WhatsApp, Evolution-API, and Email payloads**
- [ ] **Step 2: Run tests to confirm failure**
- [ ] **Step 3: Implement channel adapters, signature verifiers, and webhook ingress endpoints**
- [ ] **Step 4: Run tests to confirm pass**
- [ ] **Step 5: Commit channel adapters**

---

### Task 6: Real-Time SignalR Hub (`OmniHub`)

**Files:**
- Create: `src/backend/MarketingStudio.Api/Hubs/IOmniClient.cs`
- Create: `src/backend/MarketingStudio.Api/Hubs/OmniHub.cs`
- Create: `src/backend/MarketingStudio.Api/Services/RealtimeNotifier.cs`
- Create: `tests/MarketingStudio.Tests/Hubs/SignalRHubTests.cs`

**Interfaces:**
- Consumes: Domain events from Outbox / Conversation slices.
- Produces: WebSocket / SignalR streaming of `OnMessageReceived`, `OnConversationUpdated`, `OnTypingIndicator`.

- [ ] **Step 1: Write failing tests for SignalR notification dispatching**
- [ ] **Step 2: Run tests to confirm failure**
- [ ] **Step 3: Implement OmniHub, client interface, and RealtimeNotifier**
- [ ] **Step 4: Run tests to confirm pass**
- [ ] **Step 5: Commit SignalR real-time hub**

---

### Task 7: Hybrid AI Copilot & Autonomous Auto-Responder

**Files:**
- Create: `src/backend/MarketingStudio.Core/Entities/AiDraft.cs`
- Create: `src/backend/MarketingStudio.Api/Modules/AI/IAiCopilotService.cs`
- Create: `src/backend/MarketingStudio.Api/Modules/AI/AiCopilotService.cs`
- Create: `src/backend/MarketingStudio.Api/Modules/AI/ApproveAiDraftSlice.cs`
- Create: `tests/MarketingStudio.Tests/AI/AiCopilotTests.cs`

**Interfaces:**
- Consumes: New incoming messages, knowledge base context.
- Produces: `AiDraft` generation, confidence scoring (>= 0.85 auto-dispatch, < 0.85 copilot suggestion), draft approval slice.

- [ ] **Step 1: Write failing tests for AI draft generation and confidence threshold routing**
- [ ] **Step 2: Run tests to confirm failure**
- [ ] **Step 3: Implement AiCopilotService, prompt builder, confidence evaluator, and approval slice**
- [ ] **Step 4: Run tests to confirm pass**
- [ ] **Step 5: Commit AI Copilot engine**

---

### Task 8: Angular 19 Standalone Client (Shared Inbox & CRM UI)

**Files:**
- Create: `src/frontend/marketing-studio-web/package.json`
- Create: `src/frontend/marketing-studio-web/src/app/core/services/signalr.service.ts`
- Create: `src/frontend/marketing-studio-web/src/app/core/services/api.service.ts`
- Create: `src/frontend/marketing-studio-web/src/app/features/inbox/inbox-shell.component.ts`
- Create: `src/frontend/marketing-studio-web/src/app/features/inbox/conversation-list.component.ts`
- Create: `src/frontend/marketing-studio-web/src/app/features/inbox/message-thread.component.ts`
- Create: `src/frontend/marketing-studio-web/src/app/features/inbox/contact-panel.component.ts`
- Create: `src/frontend/marketing-studio-web/src/app/features/inbox/ai-copilot-bar.component.ts`

**Interfaces:**
- Consumes: ASP.NET Core Web API endpoints and SignalR `OmniHub`.
- Produces: Modern Linear/Notion-style reactive Shared Inbox UI with real-time updates and AI draft approvals.

- [ ] **Step 1: Scaffold Angular 19 application structure and configuration**
- [ ] **Step 2: Implement SignalR client service and API integration**
- [ ] **Step 3: Build Conversation List, Message Thread, Composer, and AI Copilot bar components**
- [ ] **Step 4: Verify client builds cleanly with Angular compiler (`npm run build`)**
- [ ] **Step 5: Commit Angular client**

---

### Task 9: End-to-End System Verification & Audit

**Files:**
- Create: `tests/MarketingStudio.Tests/E2E/EndToEndFlowTests.cs`

**Interfaces:**
- Tests the complete customer journey:
  1. WhatsApp Inbound Webhook arrives.
  2. Outbox stores and deduplicates message.
  3. Contact is resolved/created with JSONB custom fields.
  4. Conversation is assigned to Inbox.
  5. SignalR streams message in real-time.
  6. AI Copilot drafts reply with confidence score.
  7. Human approves draft -> Outbound message queued and dispatched.

- [ ] **Step 1: Write the comprehensive end-to-end integration test**
- [ ] **Step 2: Execute all backend and integration tests (`dotnet test`)**
- [ ] **Step 3: Verify Angular build and bundle status**
- [ ] **Step 4: Commit end-to-end test suite and final verification**
