This classification is based on the folder names, not yet on a full audit of the source code or READMEs.

| Platform domain                            | Repositories to investigate                                                                                                                                                            |
| ------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| CRM & customer management                  | `twenty`, `espocrm`, `frappe-crm`, `relaticle`, `trycrm`, `laravel-crm`, `DeskcommCRM`                                                                                                 |
| Outbound & sales prospecting               | `AgentSDR`, `OpenOutreach`, `Open-Cold-Dialer`, `warm bly` (folder: `warmbly`), `linkedin-outreach-mcp`                                                                                |
| Marketing automation & lifecycle messaging | `mautic`, `dittofeed`, `laudspeaker`, `parcelvoy`, `notifuse`, `listmonk`                                                                                                              |
| Email infrastructure & delivery            | `BillionMail`, `postal`, `useSend`, `postbase`, `Quickly`                                                                                                                              |
| Social media management                    | `postiz-app`, `mixpost`, `trypost`, `openpost`                                                                                                                                         |
| Customer support & shared inboxes          | `chatwoot`, `chaskiq`, `freescout`, `libredesk`, `zammad`, `helpdesk`                                                                                                                  |
| Chatbots & conversational AI               | `botpress`, `ChatbotX`, `brightbean-chat`, `dograh`                                                                                                                                    |
| Forms, surveys & feedback                  | `formbricks`, `OpnForm`, `heyform`, `formizee`, `dsforms`, `LimeSurvey`, `survey-library`, `fider`                                                                                     |
| Content, SEO & funnels                     | `Ghost`, `openfunnels`, `autonnel`, `open-seo`, `seonaut`, `serpbear`                                                                                                                  |
| Workflow automation                        | `n8n`                                                                                                                                                                                  |
| Telephony & messaging integrations         | `asterisk`, `jasmin`, `playsms`, `textbee`, `evolution-api`, `OpenWA`, `wppconnect`, `aiogram`, `grammY`, `discord.js`, `ntfy`, `ringee-app`, `VoicEra`, `TomoriBot`, `Red-DiscordBot` |
| Scheduling & appointments                  | `cal.diy`, `calnode`                                                                                                                                                                   |
| Signatures, contracts & document sharing   | `documenso`, `docuseal`, `OpenSign`, `papermark`                                                                                                                                       |
| Analytics, commerce & other capabilities   | `countly-server`, `spree`, `Logiq`, `qrev`, `DenchClaw`, `logchimp`                                                                                                                    |

----------------------------------------------------------------------------------

Then I would build Inspire from scratch as a multi-tenant Marketing, Sales & Automation platform using .NET 10, Vertical Slice Architecture, and Angular. Your existing repositories become research material, not runtime dependencies.

The key decision: use a modular monolith for the initial deployment, with strict module boundaries and an architecture that allows specific modules to become independent microservices when justified. You can define the service boundaries from Day 1 without paying the operational cost of dozens of distributed services before you need them.

# 1. Recommended technology stack

[File:Dot-net-original-wordmark.svg - Wikimedia Commons](https://images.openai.com/static-rsc-4/q4S4ICL0hEy8fp7GNePQ2JJRwSMr3mbVJ1wCb6DL-gbqWu_Z2L5cK-xFGRRuxZzou3yoMndwBFoYIji1ZZ_VoY3vLm_Bnep4ITBtFIQV__Lw1L4uDweLkLOcZ1pyFp5krzqZaZo8fIkaR2nxKKJs37_GWgcU3tttw9gs5JR8xQs?purpose=inline)

Backend — .NET 10 / ASP.NET Core

Vertical slices organized by business feature, CQRS where useful, explicit module boundaries, FluentValidation, and integration testing.

[Oneture Technologies | Helping You Go Digital](https://images.openai.com/static-rsc-4/9X0l3NaAfdpkPA1mGaJr5oKqyLycoH4ZyYGRtBNxVjJRKK51oyOdGU5jqTgkPItBKkwF3q2NxRFoiTTdeNl3O9ZAVijK66xAdJfDde_l5_JojQn9iw6W2_9lV7Y5sANpsF9ff5VUfJ0VJ-q4NF_0LKYeRyhXH_nDckvsLJKeTq4?purpose=inline)

Frontend — Angular + TypeScript

Standalone components, feature-based organization, lazy-loaded routes, reactive forms, signals for local UI state, and generated API clients.

[3CX Logo .PNG and .AI, .EPS, .CDR, .PDF, .SVG - iconLogoVector](https://images.openai.com/static-rsc-4/Cf2kqzUgFfVPW4W4AiLnyVM3Ti7ZhuE5RLGSkRQH72GD67jeLmynBU6-9igKSo67lUHNAWaVIixeX-TNQlXeWvIo0SioEVo2aFcMZUImKsDEfCR8QYVslUR-jZcImH_gWZeVFhN1tyyHvY4bP-sDD33Em7SnGwlDvrXtXo0DJGI?purpose=inline)

Data — PostgreSQL

EF Core for relational persistence, explicit tenant isolation, database migrations, transactional boundaries, and separate ownership for future services.

[NATS Logo PNG Vector (SVG) Free Download](https://images.openai.com/static-rsc-4/AZqelFp5NraozPGWyY2ZPnE7eTK4ka2YXIiB7LGLO32s9R8ZfKGVrtpmdRDSENDe30jibHkPYZxb6c8VUoRhluNwl9rxmqlt237pPq_8sse83a3E7wg3149qwbDGDBltLJMzcTH0VZKv6-mPYEu8s6Oa0k6kWY8NuZVC-Uws1uE?purpose=inline)

Messaging — NATS JetStream

Asynchronous domain integration, durable events, retry handling, and background processing. Start with a small number of well-defined event streams.

Supporting tools:

| Concern             | Recommendation                                                                    |
| ------------------- | --------------------------------------------------------------------------------- |
| API contracts       | OpenAPI with generated Angular clients                                            |
| Authentication      | OpenID Connect / OAuth 2.0; evaluate Keycloak                                     |
| Database access     | EF Core and Npgsql                                                                |
| Validation          | FluentValidation                                                                  |
| Logging and tracing | OpenTelemetry                                                                     |
| Application metrics | Prometheus-compatible metrics                                                     |
| Cache               | Redis when needed                                                                 |
| File storage        | S3-compatible object storage                                                      |
| Background jobs     | Hosted services initially; durable workflow engine for complex long-running flows |
| Testing             | xUnit, integration tests, Testcontainers                                          |
| Frontend testing    | Angular unit tests and Playwright E2E                                             |
| Deployment          | Docker, CI/CD, Coolify                                                            |
| Repository          | .NET solution plus Angular workspace in a monorepo                                |

## 2. Architecture: vertical slices inside business modules

Do not organize the entire backend around generic folders such as `Controllers`, `Services`, `Repositories`, and `DTOs`. Organize code around the use cases that deliver business value.

For example:

```
src/
├── backend/
│   ├── Inspire.Api/
│   ├── BuildingBlocks/
│   │   ├── Identity/
│   │   ├── Tenancy/
│   │   ├── Authorization/
│   │   ├── Messaging/
│   │   ├── Observability/
│   │   └── Contracts/
│   └── Modules/
│       ├── CRM/
│       │   ├── Contacts/
│       │   │   ├── CreateContact/
│       │   │   ├── UpdateContact/
│       │   │   ├── GetContact/
│       │   │   └── SearchContacts/
│       │   ├── Companies/
│       │   ├── Leads/
│       │   └── Segments/
│       ├── Marketing/
│       │   ├── Campaigns/
│       │   ├── Audiences/
│       │   ├── Forms/
│       │   └── LandingPages/
│       ├── Outreach/
│       ├── Sales/
│       ├── Automation/
│       ├── Integrations/
│       ├── Analytics/
│       ├── AI/
│       └── Billing/
└── frontend/
    └── inspire-web/
```

A vertical slice contains the request, validation, authorization, business logic, persistence and response for one use case. Share infrastructure where appropriate, but don't force every feature into an identical abstraction.

For example, `CRM/Contacts/CreateContact` should own the create-contact workflow. It should not need to depend on a universal `IRepository<T>` or a generic service layer that obscures the actual operation.

## 3. Define module boundaries before writing features

Platform Core

Identity, organizations, memberships, permissions, audit, settings and entitlements.

Customer & Revenue

CRM, companies, contacts, leads, sales pipelines, activities, deals and quotations.

Marketing & Engagement

Campaigns, content, SEO, forms, landing pages, email, social publishing and conversational channels.

Automation & Intelligence

Workflow execution, event processing, AI agents, lead scoring, personalization and decision support.

Integration & Analytics

Provider adapters, external APIs, event ingestion, attribution, funnels, cohorts and revenue reporting.

Each module should have an explicit public contract. Other modules must interact through those contracts or asynchronous events, never by reaching into private implementation details.

You can deploy all modules in one application initially while retaining a path to independent deployment. When a module becomes a microservice, its data ownership and integration contracts should already be clear.

## 4. Angular frontend architecture

I recommend organizing Angular by business feature rather than technical type.

```
src/app/
├── core/
│   ├── auth/
│   ├── tenant-context/
│   ├── api/
│   ├── permissions/
│   └── error-handling/
├── shell/
│   ├── navigation/
│   ├── workspace-switcher/
│   └── layout/
├── shared/
│   ├── ui/
│   ├── forms/
│   ├── tables/
│   └── utilities/
├── features/
│   ├── dashboard/
│   ├── crm/
│   ├── marketing/
│   ├── outreach/
│   ├── sales/
│   ├── automation/
│   ├── analytics/
│   ├── integrations/
│   ├── ai-agents/
│   └── settings/
└── app.routes.ts
```

Use:

- Lazy-loaded routes for major modules.
- Angular signals for local and derived UI state.
- RxJS for asynchronous streams and event-oriented interactions.
- Reactive forms for complex data entry.
- Route guards for navigation convenience, backed by server-side authorization.
- Generated API clients from OpenAPI to reduce frontend/backend contract drift.
- A consistent design system for tables, filters, forms, dashboards, dialogs and workflow builders.

Avoid putting business rules exclusively in Angular. Tenant permissions, campaign eligibility, pricing rules and workflow authorization must be enforced on the server.

## 5. Build these foundations first

The first release should establish the infrastructure that every future feature will depend on.

1. Repository and build foundation: solution structure, Angular workspace, CI pipeline, code formatting, analyzers and local development environment.
2. Identity and multi-tenancy: organizations, workspaces, memberships, role-based access, tenant context and server-side isolation tests.
3. Data and API conventions: PostgreSQL, migrations, API versioning, error contracts, pagination, filtering and generated clients.
4. Module contracts: explicit ownership, internal module APIs, integration events, idempotency and transactional outbox.
5. Operational foundation: structured logs, traces, metrics, health checks, audit records, backups and restore testing.
6. First complete business workflow: create a contact, qualify a lead, initiate an approved outreach sequence, track a response, move a deal through a pipeline and report the result.

## 6. Three architectural decisions to get right

Multi-tenancy: every query, command, background job, event handler, cache key, search operation and export must respect the tenant boundary. Never trust a tenant ID supplied by the browser without verifying membership and authorization.

Events and automation: distinguish internal domain events from public integration events. Use durable messaging only where asynchronous processing is appropriate, with idempotent consumers and an outbox to prevent lost events between database commits and message publication.

Microservices: do not split services merely because they exist as modules. Split when independent scaling, deployment, reliability, team ownership or security boundaries justify the operational overhead. Email delivery, event processing and long-running workflow execution are plausible early candidates for independent workers.

## 7. The first engineering artifact

Before generating thousands of lines of code, establish an architecture specification with:

- System context and module dependency diagrams.
- A bounded-context and data-ownership matrix.
- Tenant and authorization threat model.
- API conventions and event schemas.
- Repository and vertical-slice conventions.
- Migration and testing strategy.
- Deployment topology and environment configuration.
- Acceptance criteria for the first end-to-end workflow.

That specification becomes the shared contract for you and any coding agents implementing the platform.

My recommendation: use .NET 10 + Angular, vertical slices within modular business domains, PostgreSQL as the initial system of record, and asynchronous messaging for cross-module work. Design for future microservices from Day 1, but keep the initial deployment operationally manageable. This gives you the flexibility to build the full marketing and sales platform without turning its architecture into the biggest obstacle to shipping it.
