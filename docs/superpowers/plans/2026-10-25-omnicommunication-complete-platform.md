# Implementation Plan: Complete AI Omnicommunication Platform
**Date:** 2026-10-25  
**Spec Reference:** `docs/superpowers/specs/2026-10-25-omnicommunication-complete-platform-design.md`

---

## Plan Structure & Horizons

- **Phase 3: Event-Driven Customer Journeys & Lifecycle Automation (`dittofeed` model)**
  - Task 3.1: Core Entities (`CustomerEvent`, `CustomerJourney`, `JourneyStep`, `JourneyEnrollment`)
  - Task 3.2: `JourneyExecutionService` with step delays & multi-channel template rendering
  - Task 3.3: Journeys Minimal API endpoints & xUnit tests
- **Phase 4: Feedback, CSAT & Surveys Engine (`Formbricks` model)**
  - Task 4.1: Core Entities (`SurveyForm`, `SurveyResponse`, `SurveyType`, `FeedbackSentiment`)
  - Task 4.2: `FeedbackService` with closed-loop low-CSAT alerting & CRM tagging
  - Task 4.3: Feedback Minimal API endpoints & xUnit tests
- **Phase 5: Self-Service Help Center & AI RAG Bot (`Chatwoot` model)**
  - Task 5.1: Core Entities (`HelpCategory`, `HelpArticle`)
  - Task 5.2: `KnowledgeBaseService` and `HelpCenterRagService` (AI deflection with source citation)
  - Task 5.3: Knowledge Base Minimal API endpoints & xUnit tests
- **Phase 6: Frontend Studios & Unified Navigation (Angular 19)**
  - Task 6.1: `journey-studio.component.ts` (Visual lifecycle canvas & triggers)
  - Task 6.2: `feedback-studio.component.ts` (Form builder & CSAT scorecards)
  - Task 6.3: `knowledge-studio.component.ts` (Article editor & public deflection preview)
  - Task 6.4: App navigation routing & build verification (`npm run build`)
- **Phase 7: Production Docker Compose & Local Stack Orchestration**
  - Task 7.1: Backend Dockerfile (.NET 10)
  - Task 7.2: Frontend Dockerfile & NGINX configuration
  - Task 7.3: `docker-compose.yml` (API, Web, Postgres, Redis, Evolution-API)
- **Phase 8: Master End-to-End Test Suite & Verification**
  - Task 8.1: Master E2E lifecycle test verifying Event -> Journey -> Outbox -> CSAT -> AI Help Center
  - Task 8.2: Final test run (`dotnet test`) and Angular production build
