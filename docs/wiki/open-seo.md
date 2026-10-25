# open-seo - Architecture & Technical Wiki

> **Edge-Native Technical SEO Audit Platform, Cloudflare Workflows Crawler, AI Search Visibility (GEO) Engine & Multi-Tenant Analytics Suite**  
> GitHub: [bensenescu/open-seo](https://github.com/bensenescu/open-seo.git) | Stack: TypeScript, Cloudflare Workers / Workflows, Drizzle ORM, Cloudflare D1 (SQLite) / PostgreSQL, TanStack Router, Better Auth, Vite, Tailwind CSS | License: MIT

---

## 1. Executive Summary & Value Proposition

**open-seo** is a modern, edge-native technical SEO auditing, rank tracking, and AI search engine visibility (GEO) platform designed to replace legacy monolithic crawlers like Screaming Frog, Ahrefs, and Semrush. Engineered to execute natively on Cloudflare's serverless edge infrastructure (Workers, Workflows, and D1 databases), Open-SEO conducts high-speed parallel website audits, tracks algorithmic rank shifts, and analyzes brand citability across conversational AI engines.

Traditional SEO audit tools run as desktop monoliths requiring intense local CPU/RAM or as expensive multi-tenant SaaS platforms with prohibitive crawl credit limits. Open-SEO resolves these limitations by executing audits across Cloudflare Workflows — providing durable, distributed step execution that scales to tens of thousands of URLs without memory bottlenecks or worker execution timeouts. Furthermore, Open-SEO pioneers Generative Engine Optimization (GEO), evaluating how brands are indexed, cited, and recommended by modern AI search models.

### Core Problems Solved
- **Monolithic Crawler Limits & Timeouts**: Eliminates CPU saturation and worker execution timeouts by orchestrating site-wide crawls through distributed Cloudflare Workflows.
- **Expensive Per-URL Crawl Credit Penalties**: Replaces metered SaaS crawl quotas with self-hosted serverless crawls leveraging edge compute and inexpensive object/relational storage.
- **Generative Engine Optimization (GEO) Blindspots**: Tracks brand visibility, product recommendations, and answer engine citations across LLMs (ChatGPT, Claude, Perplexity, Gemini).
- **Dual-Database Portability (Edge & Postgres)**: Employs Drizzle ORM with strict schema parity, enabling deployment to ultra-low-latency Cloudflare D1 edge databases or enterprise PostgreSQL clusters.
- **Fragmented Search Console & Analytics Data**: Unifies Google Search Console (GSC) organic keyword clicks, impressions, and CTR with Google Analytics 4 (GA4) behavioral metrics to pinpoint keyword cannibalization and decaying traffic.

---

## 2. High-Level System Architecture

Open-SEO decouples frontend client interaction, edge API routing, background crawler workflows, and search analytics synchronization.

```mermaid
flowchart TD
    subgraph ClientLayer ["Client & Edge Surfaces"]
        WebDashboard["TanStack Router Dashboard\n(Vite + React + Shadcn UI)"]
        McpWidgets["Model Context Protocol (MCP) Widgets\n(vite-plugin-mcp-widgets.ts)"]
        ExternalAPI["REST API & Headless Ingress\n(Cloudflare Workers Gateway)"]
    end

    subgraph EdgeComputeCore ["Cloudflare Edge Compute (src/server.ts)"]
        ServerGateway["Hono / Worker Gateway\n(src/server.ts :8787)"]
        AuthService["Better Auth Service\n(src/db/better-auth-schema.ts)"]
        AuditService["Audit Management Service\n(src/server/features/audit)"]
        GscSyncService["GSC & GA4 Ingestion Engine\n(src/db/gsc.schema.ts)"]
        AiGeoService["AI Visibility / GEO Engine\n(src/db/ai-visibility.schema.ts)"]
    end

    subgraph DurableWorkflowTier ["Durable Crawler Engine (Cloudflare Workflows)"]
        SiteAuditWorkflow["SiteAuditWorkflowCrawl\n(src/server/workflows/siteAuditWorkflowCrawl.ts)"]
        CrawlThrottle["CrawlThrottle & Token Bucket\n(src/server/lib/audit/crawl-throttle.ts)"]
        CrawlWindow["CrawlWindow & Timeout Guard\n(src/server/lib/audit/crawl-window.ts)"]
        DiscoveryEngine["URL Discovery & Sitemap Parser"]
        HtmlInspector["Cheerio / Regex HTML Parser\n(Tags, Schema, Status, Vitals)"]
    end

    subgraph DataPersistence ["Dual Database & Storage Layer (Drizzle ORM)"]
        D1EdgeDB[("Cloudflare D1 (SQLite)\n(Edge Deployment)")]
        PostgresDB[("PostgreSQL 16 DB\n(Enterprise Self-Hosted)")]
        R2Buckets[("Cloudflare R2 Bucket\n(HTML Snapshots & PDF Reports)")]
    end

    WebDashboard --> ServerGateway
    McpWidgets --> ServerGateway
    ExternalAPI --> ServerGateway

    ServerGateway --> AuthService
    ServerGateway --> AuditService
    ServerGateway --> GscSyncService
    ServerGateway --> AiGeoService

    AuditService --> SiteAuditWorkflow
    SiteAuditWorkflow --> CrawlThrottle
    SiteAuditWorkflow --> CrawlWindow
    SiteAuditWorkflow --> DiscoveryEngine
    DiscoveryEngine --> HtmlInspector

    HtmlInspector --> R2Buckets
    SiteAuditWorkflow --> D1EdgeDB
    SiteAuditWorkflow --> PostgresDB
    ServerGateway <--> D1EdgeDB
    ServerGateway <--> PostgresDB
```

### Durable Edge Crawler Lifecycle
1. **Audit Initiation**: An operator configures a crawl project via the dashboard. `AuditService` creates an `audit` record and initiates `SiteAuditWorkflowCrawl`.
2. **Robots & Sitemap Discovery**: The workflow fetches `robots.txt`, identifies user-agent directives, and parses XML sitemaps to seed the initial URL frontier.
3. **Throttled Edge Crawling**: `CrawlThrottle` applies rate limits (token bucket algorithm) to avoid overwhelming the target origin server. Workers fetch pages concurrently.
4. **HTML Parsing & Technical Rule Auditing**: Each HTML response is parsed to inspect HTTP status codes, title/meta tags, canonical link headers, OpenGraph properties, hreflang tags, internal/external links, and schema JSON-LD.
5. **Issue Classification & Aggregation**: Issues are categorized into `ERROR`, `WARNING`, and `INFO` levels and persisted via Drizzle ORM. Completed audits compute health scores and generate white-label PDF/HTML summaries.

---

### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class AuditScanner {
        +scanUrl(url) AuditReport
        +evaluateCoreWebVitals(url) Metrics
    }

    class ScoreCalculator {
        +computeSeoScore(report) Float
    }

    AuditScanner --> ScoreCalculator : scores
```

---

## 3. Technology Stack & Key Dependencies

| Component | Framework / Library | Role & Details |
|---|---|---|
| **Edge Compute** | Cloudflare Workers, Cloudflare Workflows | Serverless edge execution with durable state orchestration across long crawls |
| **Language & Tooling** | TypeScript 5.x, pnpm, Vite 5.x | Ultra-fast client and worker bundling with strict static type checking |
| **Routing & Client** | TanStack Router, React 18 | Fully type-safe client-side routing with automatic code splitting |
| **ORM & Persistence** | Drizzle ORM, D1 (SQLite), PostgreSQL | Unified ORM maintaining dual schema parity across SQLite and PostgreSQL |
| **Authentication** | Better Auth | Modern session management, multi-tenant accounts, OAuth, and API tokens |
| **Parsing & Inspection**| Cheerio, htmlparser2 | Fast DOM parsing for meta tags, links, headings, and schema validation |
| **Styling & Components**| Tailwind CSS v3, Shadcn UI, Radix UI | Modern accessible UI components with dark mode support |
| **Integrations** | Google Search Console API, GA4 API | Automated synchronization of search traffic, rankings, and impressions |
| **AI Evaluation** | OpenAI / Anthropic SDKs | Queries LLMs to measure brand visibility and citation in generative answers |

---

## 4. Repository Layout & Key Modules

```text
inspire/open-seo/
├── src/
│   ├── server.ts                    # Cloudflare Worker HTTP gateway and API router
│   ├── audit-worker.ts              # Standalone edge worker for headless page auditing
│   ├── db/                          # Drizzle ORM database schemas
│   │   ├── audit.schema.ts          # Audits, crawled pages, technical issues, metrics
│   │   ├── ai-visibility.schema.ts  # Generative Engine Optimization (GEO) tracking
│   │   ├── gsc.schema.ts            # Google Search Console performance data
│   │   ├── ga4.schema.ts            # Google Analytics 4 sessions and traffic
│   │   ├── local-seo.schema.ts      # Google Business Profile and local citation schemas
│   │   ├── app.schema.ts            # Projects, organizations, team memberships
│   │   └── better-auth-schema.ts    # User accounts, sessions, and security tokens
│   ├── server/
│   │   ├── workflows/               # Cloudflare Workflows durable crawler implementations
│   │   │   └── siteAuditWorkflowCrawl.ts # Multi-step durable crawl state machine
│   │   ├── lib/audit/               # Crawl rate limiters, token buckets, discovery utils
│   │   └── features/                # Domain services (audit, credentials, project context)
│   ├── routes/                      # TanStack Router file-based route tree
│   ├── client/                      # React UI features, audit dashboards, charts
│   └── report-widgets/              # Embeddable Model Context Protocol (MCP) audit widgets
├── drizzle/                         # Auto-generated SQL migrations for D1 and PostgreSQL
├── wrangler.jsonc                   # Cloudflare Worker and Workflow deployment configuration
└── compose.yaml                     # Local PostgreSQL and Redis development environment
```

---

## 5. Core Features & Capabilities

### 5.1 Durable Cloudflare Workflows Crawler Engine
Traditional serverless functions terminate after 30 to 60 seconds, making full-site crawls impossible without external infrastructure. Open-SEO leverages **Cloudflare Workflows**, breaking site audits into deterministic, paused, and resumed steps. If network interruptions or rate limits occur, the workflow sleeps and resumes precisely where it paused without data corruption or memory leaks.

### 5.2 100+ Technical SEO Validation Rules
Every crawled URL is subjected to comprehensive technical rule checks:
- **Indexability & Crawlability**: `noindex`, `nofollow`, canonical loops, canonical mismatches, and `robots.txt` disallows.
- **Metadata & Headings**: Missing, duplicate, or truncated title tags, meta descriptions, and missing H1 tags.
- **Redirects & Status Codes**: 404 broken links, 301/302 redirect chains, and circular redirects.
- **Internationalization**: Missing reciprocal hreflang annotations, invalid country/language codes.
- **Structured Data**: Schema.org JSON-LD syntax validation, missing required entity fields.

### 5.3 Generative Engine Optimization (GEO) & AI Visibility
Open-SEO incorporates dedicated tracking for the AI search era (`ai-visibility.schema.ts`). It queries top conversational LLMs (ChatGPT, Claude, Perplexity, Gemini) with industry prompts, checking whether a brand or product is recommended, which URLs are cited as sources, and scoring the brand's generative visibility relative to competitors.

### 5.4 Dual-Database Parity (Cloudflare D1 & PostgreSQL)
Open-SEO supports both edge-native SQLite via Cloudflare D1 and enterprise-grade PostgreSQL using Drizzle ORM. An automated test suite (`schema-parity.test.ts`) verifies that all tables, foreign keys, enums, and indexes behave identically on both database engines, enabling zero-friction migration between serverless edge and self-hosted VPCs.

### 5.5 Google Search Console & GA4 Performance Sync
The platform continuously ingests organic click, impression, CTR, and average position data from Google Search Console. By cross-referencing GSC queries against crawl findings, Open-SEO automatically flags **keyword cannibalization** (multiple URLs ranking for the same query) and highlights technical issues on revenue-critical landing pages.

---

## 6. Data Models & Schema Architecture

Open-SEO models organizations, projects, crawls, pages, issues, AI visibility records, and Google Search Console metrics in Drizzle ORM.

```mermaid
erDiagram
    Organization ||--o{ Project : "owns"
    Project ||--o{ SiteAudit : "runs"
    Project ||--o{ GscQuery : "tracks"
    Project ||--o{ AiVisibilityQuery : "evaluates"

    SiteAudit ||--|{ CrawledPage : "discovers"
    SiteAudit ||--o{ AuditIssue : "detects"
    
    CrawledPage ||--o{ PageMetric : "records"
    CrawledPage ||--o{ AuditIssue : "triggers"
    
    AiVisibilityQuery ||--|{ AiCitation : "extracts"
```

### Core Schema Entity Definitions

#### `projects` (`app.schema.ts`)
- `id` (`text`, PK): Unique project identifier.
- `organizationId` (`text`, FK): Owning organization.
- `name` (`text`): Project label.
- `domain` (`text`): Target website root URL (e.g., `https://example.com`).
- `settings` (`json`): Crawl speed, user-agent string, basic auth credentials, and disallowed paths.

#### `site_audits` (`audit.schema.ts`)
- `id` (`text`, PK): Audit run identifier.
- `projectId` (`text`, FK): Reference to parent project.
- `status` (`text`): `PENDING`, `CRAWLING`, `PROCESSING`, `COMPLETED`, `FAILED`.
- `pagesDiscovered` (`integer`), `pagesCrawled` (`integer`).
- `healthScore` (`integer`): Computed technical health score (0-100).
- `startedAt` (`timestamp`), `completedAt` (`timestamp`, nullable).

#### `crawled_pages` (`audit.schema.ts`)
- `id` (`text`, PK): Page record ID.
- `auditId` (`text`, FK): Associated audit run.
- `url` (`text`): Normalized page URL.
- `statusCode` (`integer`): HTTP status code (200, 301, 404, 500).
- `title` (`text`), `metaDescription` (`text`): Page metadata.
- `canonicalUrl` (`text`, nullable): Specified canonical link.
- `loadTimeMs` (`integer`): Server response time.
- `wordCount` (`integer`): Content density metric.
- `isIndexable` (`boolean`): True if page meets all indexability criteria.

#### `audit_issues` (`audit.schema.ts`)
- `id` (`text`, PK): Issue identifier.
- `auditId` (`text`, FK): Associated audit run.
- `pageId` (`text`, FK, nullable): Affected crawled page.
- `severity` (`text`): `ERROR`, `WARNING`, `INFO`.
- `issueType` (`text`): Code identifier (e.g., `MISSING_TITLE`, `CANONICAL_LOOP`, `BROKEN_LINK`).
- `details` (`json`): Additional context, line numbers, or target anchor tags.

#### `ai_visibility_queries` (`ai-visibility.schema.ts`)
- `id` (`text`, PK): Query evaluation ID.
- `projectId` (`text`, FK): Associated project.
- `prompt` (`text`): User query tested against AI engines (e.g., "Best CRM for startups").
- `model` (`text`): Target LLM (`gpt-4o`, `claude-3-5-sonnet`, `perplexity`).
- `brandMentioned` (`boolean`): Whether project brand was surfaced in the response.
- `citedUrls` (`json`): List of citation URLs returned by the AI engine.

---

### 6.1 Comprehensive Database Schema Specification

Open-SEO stores domain audits and technical SEO scores.

#### Table: `audits`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Audit ID |
| `url` | `TEXT` | `NOT NULL` | - | Analyzed URL |
| `score` | `FLOAT` | `NOT NULL` | `0.0` | Overall SEO score |

---

## 7. API Architecture, Webhooks & Key Endpoints

Open-SEO provides a RESTful API mounted on Cloudflare Workers for headless crawl automation and reporting.

### Key API Endpoints

| Method | Endpoint | Description | Auth |
|---|---|---|---|
| `POST` | `/api/projects` | Register a new web property for auditing | Bearer Token / Session |
| `POST` | `/api/projects/:id/crawl` | Trigger an immediate site audit workflow | Bearer Token / Session |
| `GET` | `/api/audits/:id` | Fetch audit status, health score, and issue breakdowns | Bearer Token / Session |
| `GET` | `/api/audits/:id/pages` | List crawled URLs with status codes and metadata | Bearer Token / Session |
| `POST` | `/api/ai-visibility/run` | Execute AI visibility search evaluations across LLMs | Bearer Token / Session |
| `GET` | `/api/gsc/performance` | Query synced Google Search Console impressions and CTR | Bearer Token / Session |

### API Payload Example: Trigger Audit Crawl
```http
POST /api/projects/proj_98a2f1b4/crawl HTTP/1.1
Host: open-seo.internal.net
Authorization: Bearer os_live_78ab2c39d01e4f
Content-Type: application/json

{
  "maxPages": 5000,
  "crawlDepth": 5,
  "followSitemaps": true,
  "respectRobotsTxt": true,
  "customHeaders": {
    "X-Audit-Token": "secret_staging_bypass"
  }
}
```

### Audit Completion Response
```json
{
  "auditId": "audit_4421b-89fc",
  "projectId": "proj_98a2f1b4",
  "status": "COMPLETED",
  "healthScore": 91,
  "pagesCrawled": 2480,
  "issuesSummary": {
    "errors": 12,
    "warnings": 84,
    "notices": 142
  },
  "topIssues": [
    { "type": "BROKEN_INTERNAL_LINK", "count": 8, "severity": "ERROR" },
    { "type": "MISSING_META_DESCRIPTION", "count": 48, "severity": "WARNING" },
    { "type": "CANONICAL_MISMATCH", "count": 4, "severity": "ERROR" }
  ],
  "completedAt": "2026-10-24T12:14:02Z"
}
```

---

### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor User as User
    participant Scanner as AuditScanner
    actor Site as Target Website

    User->>Scanner: Run Audit on target.com
    Scanner->>Site: Fetch HTML & assets
    Scanner->>Scanner: Calculate Lighthouse & meta tags
    Scanner-->>User: Display Audit Report
```

---

## 8. Strategic Value & Takeaways for MarketingStudio

### Reusable Architectural Patterns
1. **Durable Workflow Site Crawling**: Cloudflare Workflows provide the perfect architecture for MarketingStudio's crawler subsystem, removing the need to manage heavy Kubernetes worker fleets while guaranteeing zero timeout failures on large sites.
2. **Dual-Database ORM Abstraction with Drizzle**: Implementing strict schema parity between lightweight SQLite (Cloudflare D1 / LibSQL) and PostgreSQL enables MarketingStudio to deploy edge-ready micro-tools for clients while retaining enterprise PostgreSQL clustering in core production.
3. **AI Search Visibility (GEO) Tracking**: Modern search marketing requires monitoring conversational AI responses alongside traditional SERPs. Adopting Open-SEO's prompt-evaluation methodology allows MarketingStudio to report brand share-of-voice in ChatGPT and Perplexity.

### Convergence Blueprint for MarketingStudio
- **Automated Client Onboarding Audit**: When a new client connects their domain to MarketingStudio, automatically trigger an Open-SEO audit to produce a branded "Technical SEO Health Checkup" report within minutes.
- **Conversion-Critical Landing Page Protection**: Wire crawl alerts into MarketingStudio's notification center — instantly alerting marketing managers if a campaign landing page returns a 404, canonical error, or accidental `noindex` tag.
- **Synergistic Rank & Crawl Intelligence**: Correlate Open-SEO's technical crawl findings with SerpBear's keyword rank tracking to identify whether rank drops were caused by technical regressions or competitor movements.
