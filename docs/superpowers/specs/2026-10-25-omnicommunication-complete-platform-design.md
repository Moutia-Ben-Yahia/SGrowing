# Architecture Design Spec: Complete AI Omnicommunication Platform
**Date:** 2026-10-25  
**Scope:** Completing Horizons 3, 4, 5, and 6 to deliver the unified enterprise platform.

---

## 1. Executive Summary

This architecture integrates the existing **Inbound Omnichannel Inbox (WhatsApp/Email/WebChat)** and **Autonomous AI SDR Outbound Engine** with the three remaining platform capabilities requested in `docs/functionnalities_needed.md`:
1. **Horizon 3: Event-Driven Customer Journeys & Lifecycle Automation** (modeled after *dittofeed* & *Customer.io*).
2. **Horizon 4: Interactive Feedback, CSAT & Surveys Engine** (modeled after *Formbricks* & *Typeform*).
3. **Horizon 5: Self-Service Help Center & AI RAG Knowledge Base** (modeled after *Chatwoot* & *Chaskiq*).
4. **Horizon 6: Production Docker Containerization & Deployment Orchestration** (multi-container stack: API, Web, Postgres, Redis, Evolution-API).

```
+------------------------------------------------------------------------------------------------+
|                                MARKETINGSTUDIO UNIFIED PLATFORM                                |
+------------------------------------------------------------------------------------------------+
|  1. Shared Omnichannel Inbox   |  2. Autonomous AI SDR Outbound  |  3. Customer Journeys (dittofeed) |
|  - Meta & Evolution-API WA     |  - Mailbox Warmup & Health      |  - Behavioral Event Ingestion   |
|  - Inbound Email & Web Chat    |  - Multi-touch Cadences         |  - DAG Trigger/Delay/Filter/Act |
|  - AI Copilot Drafts & SignalR |  - Closed-loop CRM Deal Handoff |  - Dynamic Segment Cohorts      |
+------------------------------------------------------------------------------------------------+
|  4. Feedback & CSAT Engine     |  5. Help Center & AI RAG Bot    |  6. Infrastructure & Deployment |
|  - Micro-surveys (CSAT/NPS)    |  - Markdown Article Manager     |  - Docker Compose Stack         |
|  - Sentiment Alerts in CRM     |  - Semantic Search Deflection   |  - PostgreSQL 16 + Redis + Nginx|
|  - Embeddable Widget Handlers  |  - Zero-wait WebChat Bot        |  - .NET 10 Web API + Angular 19 |
+------------------------------------------------------------------------------------------------+
```

---

## 2. Horizon 3: Event-Driven Customer Journeys (`dittofeed` / `Customer.io` Model)

### Domain Entities
- `CustomerEvent`:
  - `Id`, `TenantId`, `ContactId`, `EventName` (e.g. `user.signed_up`, `trial.expiring`, `cart.abandoned`), `Properties` (JSONB metadata), `OccurredAtUtc`.
- `CustomerJourney`:
  - `Id`, `TenantId`, `Name`, `TriggerEvent`, `Status` (`Draft`, `Active`, `Paused`), `CreatedAtUtc`.
- `JourneyStep`:
  - `Id`, `JourneyId`, `StepOrder`, `StepType` (`Delay`, `SendEmail`, `SendWhatsApp`, `ConditionBranch`), `Configuration` (JSON metadata: delay seconds, template, branch condition).
- `JourneyEnrollment`:
  - `Id`, `TenantId`, `JourneyId`, `ContactId`, `CurrentStepOrder`, `Status` (`Active`, `Completed`, `Cancelled`), `NextExecutionAtUtc`.

### Services & Slices
- `EventIngressService`: Records incoming events and triggers matching active journeys.
- `JourneyExecutionService`: Evaluates due journey steps, applies delays/conditions, renders multi-channel templates, and enqueues to the durable `Outbox`.
- Minimal API: `POST /api/events`, `GET /api/journeys`, `POST /api/journeys`, `POST /api/journeys/{id}/trigger`.

---

## 3. Horizon 4: Feedback, CSAT & Surveys Engine (`Formbricks` Model)

### Domain Entities
- `SurveyForm`:
  - `Id`, `TenantId`, `Title`, `Type` (`CSAT`, `NPS`, `CustomForm`), `Questions` (JSON structure: question text, rating scale, options), `IsActive`, `CreatedAtUtc`.
- `SurveyResponse`:
  - `Id`, `TenantId`, `SurveyFormId`, `ContactId`, `Score` (1-10 or 1-5), `FeedbackText`, `Sentiment` (`Positive`, `Neutral`, `Negative`), `CreatedAtUtc`.

### Services & Closed-Loop Alerting
- `FeedbackService`: Ingests survey submissions, calculates sentiment.
- Closed-Loop Rule: If `Score <= 2` or `Sentiment == Negative`, automatically create a high-priority retention ticket/tag `at-risk` on the Contact and broadcast an alert via SignalR `OmniHub` to Customer Success agents.
- Minimal API: `GET /api/feedback/surveys`, `POST /api/feedback/surveys`, `POST /api/feedback/submit`.

---

## 4. Horizon 5: Self-Service Help Center & AI RAG Bot (`Chatwoot` Model)

### Domain Entities
- `HelpCategory`:
  - `Id`, `TenantId`, `Name`, `Slug`, `OrderIndex`, `Icon`.
- `HelpArticle`:
  - `Id`, `TenantId`, `CategoryId`, `Title`, `Slug`, `ContentMarkdown`, `IsPublished`, `ViewCount`, `CreatedAtUtc`.

### Services & Knowledge Deflection
- `KnowledgeBaseService`: CRUD for categories and articles, full-text / keyword indexing.
- `HelpCenterRagService`: Given a customer question from Web Chat or WhatsApp, retrieves the most relevant article paragraphs and synthesizes a high-confidence answer with reference links. If confidence $< 70\%$, gracefully routes to a human agent in the Shared Inbox.
- Minimal API: `GET /api/kb/articles`, `POST /api/kb/articles`, `POST /api/kb/query`.

---

## 5. Horizon 6: Docker Compose & Production Topology

- `docker-compose.yml`:
  - `marketing-studio-api`: .NET 10 ASP.NET Core Web API on port `5000`.
  - `marketing-studio-web`: Angular 19 frontend served via NGINX on port `4200` with reverse proxy to API.
  - `postgres`: PostgreSQL 16 on port `5432`.
  - `redis`: Redis 7 on port `6379`.
  - `evolution-api`: Evolution API WhatsApp gateway on port `8080`.
- Dockerfiles:
  - `src/backend/Dockerfile`: Multi-stage .NET 10 SDK build and chiseled ASP.NET runtime.
  - `src/frontend/Dockerfile`: Multi-stage Node.js build and Alpine NGINX runtime.

---

## 6. Verification & Acceptance Criteria
- Every backend service tested with xUnit & FluentAssertions.
- Total test count expands to cover all 3 new domains (Journeys, Feedback, KnowledgeBase).
- Angular 19 frontend compiles with 0 errors (`npm run build`).
- Complete E2E integration test exercising Event Journey $\rightarrow$ Outbox $\rightarrow$ Feedback CSAT alert $\rightarrow$ Help Center AI RAG deflection.
