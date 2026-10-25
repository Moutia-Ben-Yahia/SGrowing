# MarketingStudio — Autonomous AI Omnicommunication Platform

An enterprise-grade, multi-tenant AI Omnicommunication & Autonomous Outreach platform built with **.NET 10** (C#, ASP.NET Core Minimal APIs, EF Core, PostgreSQL), **Angular 19** (Signals, Standalone Components, Tailwind CSS), and native **SignalR** real-time streaming.

---

## 🌟 Platform Pillars

1. **Inbound Omnichannel Shared Inbox & CRM Core**
   - Native dual WhatsApp support (**Meta Cloud API** + **Evolution-API**).
   - Inbound Email (SendGrid / Postmark / SMTP) and Web Chat widgets.
   - SignalR real-time websocket message delivery.
   - Hybrid AI Copilot draft suggestion and 1-click human agent approval.

2. **Autonomous AI SDR & Cold Outbound Engine (`AgentSDR` + `warmbly` + `OpenOutreach`)**
   - Mailbox warmup and deliverability ramp-up protection with health score monitoring.
   - Multi-touch cadence sequencing with AI-personalized icebreaker synthesis.
   - Inbound reply sentiment classification (`Interested`, `Unsubscribe`, `OutOfOffice`).
   - Closed-loop CRM Deal creation ($5,000+ opportunity) and automatic inbox handoff when a lead replies positively.

3. **Event-Driven Customer Journeys (`dittofeed` / `Customer.io`)**
   - Behavioral product and customer event ingestion (`user.signed_up`, `trial.expiring`, `cart.abandoned`).
   - Multi-step DAG lifecycle automations with delays and multi-channel template rendering (WhatsApp & Email).
   - Durable transactional Outbox pattern guaranteeing zero message drops.

4. **Customer Feedback, CSAT & Surveys (`Formbricks`)**
   - Interactive in-chat micro-surveys (CSAT 1-5★, NPS 0-10).
   - Real-time sentiment classification.
   - Negative score sentinel: automatically tags contacts as `"at-risk"` and alerts Customer Success agents.

5. **Self-Service Help Center & Zero-Wait AI RAG Bot (`Chatwoot` / `Chaskiq`)**
   - Category and Markdown documentation management.
   - Zero-wait AI deflection: searches knowledge base, synthesizes answers with source citations, and resolves questions before reaching human agents.

---

## 🚀 Quickstart

### Option A: Run with Docker Compose (Recommended)

Start the entire multi-service topology in one command:
```bash
docker compose up -d
```
Services started:
- **Web App (Angular 19):** `http://localhost:4200`
- **Backend API (.NET 10):** `http://localhost:5000`
- **PostgreSQL 16:** `localhost:5432`
- **Redis 7:** `localhost:6379`
- **Evolution-API (WhatsApp):** `http://localhost:8080`

### Option B: Local Development

1. **Backend (.NET 10):**
   ```bash
   dotnet build
   dotnet run --project src/backend/MarketingStudio.Api
   ```
2. **Frontend (Angular 19):**
   ```bash
   cd src/frontend/marketing-studio-web
   npm install
   npm start
   ```

3. **Run Automated Tests:**
   ```bash
   dotnet test
   ```

---

## 🏛 Architecture & Engineering Standards

- **Vertical Slice Architecture:** High cohesion per feature module (`AI`, `Channels`, `Conversations`, `CRM`, `Outbound`, `Journeys`, `Feedback`, `KnowledgeBase`).
- **Strict Multi-Tenancy:** Isolation enforced via `TenantId` query filters and scoped tenant context.
- **Transactional Outbox:** Guaranteed at-least-once message delivery for webhook ingestion, cadence dispatches, and journey actions.
- **Signals-Driven UI:** Angular 19 reactive state management with `signal` and `computed`.
