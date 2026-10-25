# Quickly - Architecture & Technical Wiki

> **High-Velocity Open-Source Cold Email & Multi-Inbox Sequencing Engine**  
> GitHub: [abdelftahzowail/Quickly](https://github.com/abdelftahzowail/Quickly.git) | Stack: Python 3.11+, FastAPI, SQLAlchemy 2.0 (Async), PostgreSQL 15+, Next.js 14, Caddy 2 | License: MIT

---

## 1. Executive Summary & Value Proposition

**Quickly** is an open-source, self-hosted cold outbound sales platform designed as a complete, sovereign alternative to enterprise SaaS platforms such as Instantly, Smartlead, and Lemlist. Built to eliminate monthly per-seat licensing, arbitrary contact tier limits, and vendor data custody, Quickly equips sales organizations, growth agencies, and outbound development teams with enterprise-grade multi-inbox rotation, dynamic sequence scheduling, and automated AI reply classification.

Rather than relying on low-reputation shared SMTP relays that trigger spam filters, Quickly executes outreach directly through Google Workspace (Gmail API) and Microsoft 365 (Microsoft Graph API) via OAuth 2.0. This ensures all outbound messages leverage the native infrastructure, cryptographic DKIM signatures, and enterprise domain reputation of the host organization.

### Core Problems Solved
- **Shared SMTP IP Reputation Degradation**: Bypasses shared commercial SMTP pools by routing emails directly through authenticated Google Workspace and Microsoft 365 OAuth 2.0 APIs.
- **Cold Outbound Scaling & Inbox Burnout**: Protects sender domain reputation through distributed multi-inbox round-robin rotation with strict per-inbox daily sending limits and randomized send-time jitter.
- **Manual Prospect Response Triage**: Automates reply analysis using a 19-provider AI classification engine that tags incoming emails into six actionable intent states (`interested`, `not_interested`, `out_of_office`, `wrong_person`, `auto_reply`, `unsubscribed`).
- **Workflow & Agentic Tool Fragmentation**: Native Model Context Protocol (MCP) streamable server at `/api/mcp` and dedicated custom n8n community node package (`n8n-node/`) enable zero-friction integration with autonomous AI SDR agents and automation flows.
- **Tracking Domain SSL Friction**: Implements "Quickly Beacon" alongside automated Caddy reverse-proxy certificates for seamless custom click/open tracking subdomains.

---

## 2. High-Level System Architecture

Quickly decouples the frontend client interface, workflow automation triggers, and AI agents from an asynchronous FastAPI execution engine backed by SQLAlchemy 2.0 async sessions and PostgreSQL.

```mermaid
flowchart TD
    subgraph ClientAndAgents ["Clients, Automations & Agents"]
        WebUI["Next.js 14 Dashboard"]
        N8NNode["n8n Custom Nodes (n8n-node)"]
        MCPAgent["Autonomous AI Agents (Cursor / Claude via MCP)"]
        ExternalWebhooks["Webhook Consumers (CRM / Slack)"]
    end

    subgraph EdgeProxy ["Edge & Reverse Proxy"]
        CaddyProxy["Caddy 2 SSL Reverse Proxy"]
    end

    subgraph QuicklyCore ["Quickly Backend Engine (FastAPI)"]
        APIGateway["FastAPI HTTP & Streaming Router"]
        AuthGuard["JWT & API Key Auth Middleware"]
        MCPHandler["MCP Streamable HTTP Server (/api/mcp)"]
        
        subgraph CorePipelines ["Execution & Dispatch Services"]
            QueueEngine["Queue Slot Reservation Engine"]
            SchedulerWorker["Async Background Scheduler (jobs.py)"]
            ProviderMatcher["DNS MX Provider Resolver"]
            EmailDispatcher["OAuth / SMTP Sender Service"]
            AIClassifier["AI Reply Classification Engine (19 Providers)"]
            TrackingEngine["Open / Click Beacon Tracker"]
        end
        
        subgraph PersistenceLayer ["Async Database Tier"]
            AsyncORM["SQLAlchemy 2.0 Async Session"]
        end
    end

    subgraph ExternalGateways ["External Messaging & AI Providers"]
        GmailAPI["Google Workspace Gmail API (OAuth2)"]
        GraphAPI["Microsoft 365 Graph API (OAuth2)"]
        SMTPServer["Custom SMTP / IMAP Servers"]
        LLMProviders["AI Providers (Ollama, OpenAI, Claude, Groq)"]
        DNSResolver["Public DNS Resolvers (MX Lookup)"]
    end

    subgraph Storage ["Persistent Database"]
        PostgresDB[(PostgreSQL 15+ / SQLite)]
    end

    WebUI -->|HTTP / REST| CaddyProxy
    N8NNode -->|REST API| CaddyProxy
    MCPAgent -->|SSE / POST /api/mcp| CaddyProxy

    CaddyProxy --> APIGateway
    APIGateway --> AuthGuard
    APIGateway --> MCPHandler
    
    AuthGuard --> QueueEngine
    AuthGuard --> TrackingEngine
    SchedulerWorker --> QueueEngine
    
    QueueEngine --> ProviderMatcher
    ProviderMatcher --> DNSResolver
    QueueEngine --> EmailDispatcher
    
    EmailDispatcher --> GmailAPI
    EmailDispatcher --> GraphAPI
    EmailDispatcher --> SMTPServer
    
    GmailAPI -->|Inbound Webhook / Sync| AIClassifier
    GraphAPI -->|Inbound Webhook / Sync| AIClassifier
    AIClassifier --> LLMProviders
    AIClassifier -->|Fire Webhook Event| ExternalWebhooks
    
    QueueEngine --> AsyncORM
    AIClassifier --> AsyncORM
    TrackingEngine --> AsyncORM
    AsyncORM --> PostgresDB
```

---

### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class QuickLeadService {
        +fastCreateLead(data) Lead
        +batchImportLeads(leadList) BatchResult
        +searchLeads(query) List~Lead~
    }

    class TaskScheduler {
        +scheduleFollowUp(leadId, dueDate) Task
        +getPendingTasks(userId) List~Task~
    }

    class InteractionLogger {
        +logNote(leadId, note) Void
        +logCall(leadId, duration, outcome) Void
    }

    class Lead {
        +UUID id
        +String name
        +String company
        +String email
        +String status
    }

    class Task {
        +UUID id
        +UUID leadId
        +String title
        +Date dueDate
        +Boolean isCompleted
    }

    QuickLeadService --> Lead : manages
    TaskScheduler --> Task : schedules
    Task "many" --> "1" Lead : belongs_to
    InteractionLogger ..> Lead : records_events
```

---

## 3. Technology Stack & Key Dependencies

- **Language & Runtime**: Python 3.11+, TypeScript (Node.js 18+)
- **API Framework**: FastAPI, Uvicorn (ASGI), Pydantic v2
- **Database & ORM**: PostgreSQL 15+ (production), SQLite (development), SQLAlchemy 2.0 Async Engine, Alembic
- **Driver**: `asyncpg` (PostgreSQL async binary driver), `aiosqlite`
- **Frontend**: Next.js 14 (App Router), React 18, TailwindCSS, Lucide React, TanStack React Query
- **Authentication**: JWT (`python-jose`), Passlib (bcrypt), OAuth 2.0 PKCE flow (`google-auth`, `msal`)
- **Workflow & AI Protocol**: Model Context Protocol (MCP) streamable HTTP server, custom n8n node integration
- **AI Classification SDKs**: Pluggable HTTP client supporting 19 inference endpoints: Ollama (local/offline), OpenAI, Anthropic Claude, Groq, Mistral, Together AI, OpenRouter, DeepSeek, and Perplexity
- **Edge & Networking**: Caddy 2 with automatic Let's Encrypt TLS provisioning, `dnspython` for MX record inspection

---

## 4. Repository Layout & Key Modules

```
Quickly/
├── app/                              # FastAPI core application package
│   ├── routers/                      # Modular API controllers
│   │   ├── analytics.py              # Campaign stats, funnel metrics, step telemetry
│   │   ├── auth.py                   # JWT login, refresh tokens, and user credentials
│   │   ├── campaigns.py              # Sequence authoring, schedule timing, enrollments
│   │   ├── inboxes.py                # Mailbox credentials, daily caps, jitter settings
│   │   ├── leads.py                  # Lead ingestion, tag assignment, custom variables
│   │   ├── office365_webhook.py      # Microsoft Graph notification listener
│   │   ├── schedule.py               # Time-window calculation and slot distribution
│   │   ├── tracking.py               # Pixel opens (/o/) and link click redirects (/c/)
│   │   ├── unibox.py                 # Multi-inbox threaded conversational interface
│   │   └── system_health.py          # Token validation, tracking reachability, provider pings
│   ├── ai_classifier.py              # 19-provider LLM response parsing and sentiment categorization
│   ├── beacon_client.py              # Dedicated tracking domain proxy communication
│   ├── beacon_sync.py                # Hostname synchronization and verification
│   ├── email_provider.py             # MX query module for recipient inbox provider matching
│   ├── jobs.py                       # Scheduled dispatch loop and asynchronous worker tasks
│   ├── mcp_leads.py                  # Model Context Protocol tools for autonomous AI agents
│   ├── models.py                     # SQLAlchemy 2.0 relational database models
│   ├── queue_logic.py                # Inbox capacity balancing, jitter, and reservation algorithm
│   ├── schemas.py                    # Pydantic v2 input, update, and serialized output models
│   └── webhooks.py                   # Outbound event publisher for 15 lifecycle triggers
├── frontend/                         # Next.js 14 modern sales dashboard
│   ├── app/                          # App router pages (campaigns, unibox, inboxes, analytics)
│   └── components/                   # Sequence builder, timeline charts, template variable injector
├── n8n-node/                         # First-party n8n community node package
│   ├── nodes/Quickly/                # Quickly n8n trigger and action node definitions
│   └── package.json                  # Node packaging for n8n registry
├── Caddyfile                         # Production zero-config HTTPS reverse proxy
├── docker-compose.yml                # Production multi-container composition
└── Dockerfile                        # Multi-stage Python build with non-root security context
```

---

## 5. Core Features & Capabilities

### Multi-Step Cadence Sequencing & A/B Variant Testing
Campaigns allow unlimited sequential outreach touches with configurable wait days between intervals. Each sequence step supports multivariate A/B testing with distinct subject lines, preview text, and email bodies (plain text or rich HTML). Quickly automatically splits outbound prospect volume equally across active variants, aggregates granular performance telemetry (open rate, click-through rate, reply rate), and allows users to designate winning templates.

### Smart Multi-Inbox Rotation & Provider-Matching Routing
To scale cold email without risking domain burnout, Quickly distributes campaign volume across unlimited connected mailboxes. The queue reservation system respects per-inbox daily limits (e.g., 40 emails/day), minimum wait intervals (e.g., 8 minutes), and randomized jitter (e.g., 0–180 seconds). Furthermore, Quickly executes a real-time DNS MX lookup on prospect domains: Google Workspace leads are automatically routed through Gmail inboxes, and Outlook/Exchange leads route through Microsoft 365 inboxes, maximizing deliverability.

### Unified Unibox & AI Intent Classification
The Unibox aggregates inbound messages from all connected Google, Microsoft, and SMTP accounts into a single threaded interface. Incoming messages are processed in real time by an asynchronous AI classifier. Supporting 19 distinct inference backends (including fully air-gapped local Ollama instances), the system extracts sentiment and tags replies into six states:
- `interested`: Prioritized for immediate account executive intervention.
- `not_interested`: Prospect is gracefully suppressed from future sequences.
- `out_of_office`: Outreach is postponed until the auto-responder return date.
- `wrong_person`: Triggers prospect reassignment and account research.
- `auto_reply`: Filtered out without interrupting sequence progression.
- `unsubscribed`: Automatically adds email to workspace-wide unsubscribe ledger.

### Lead Verification & Deliverability Protection
Prior to dispatch, Quickly evaluates lead addresses against configurable email verification endpoints. Leads with invalid MX records, disposable inboxes, or high bounce risk are flagged and excluded. In addition, inbound bounce notifications (DSN messages) received via webhooks or mailbox polling automatically transition the prospect to `bounced` status, halting all further sequence touches across all campaigns.

### Quickly Beacon & Custom Tracking Domains
Email open and link tracking pixels often suffer deliverability degradation when sharing a centralized domain. Quickly features "Beacon" architecture: users configure a CNAME pointing their custom branded domain (e.g., `track.domain.com`) to the Quickly host. Caddy automatically provisions Let's Encrypt TLS certificates, allowing link redirects and transparent tracking pixels to execute under the sender's own domain name.

### Agent-Native Model Context Protocol (MCP) Server
Quickly exposes a remote Streamable HTTP MCP server at `/api/mcp`. Autonomous AI agents (operating in Cursor, Claude Desktop, or custom agent frameworks) can connect directly using Bearer authentication to list campaigns, enroll enriched prospects, inspect sequence performance, and retrieve replies without scraping or manual API wrapper construction.

---

## 6. Data Models & Schema Architecture

Quickly maintains a relational schema using SQLAlchemy 2.0 with foreign key cascades, unique constraints, and optimized indexes on lookups.

```mermaid
erDiagram
    INBOX ||--o{ CAMPAIGN_INBOX : participates
    INBOX ||--o{ QUEUE_SLOT : executes
    INBOX ||--o{ EMAIL_LOG : sends
    
    CAMPAIGN ||--o{ CAMPAIGN_INBOX : utilizes
    CAMPAIGN ||--o{ SEQUENCE : defines
    CAMPAIGN ||--o{ CAMPAIGN_LEAD : enrolls
    CAMPAIGN ||--o{ EMAIL_LOG : records
    CAMPAIGN ||--o{ LEAD_REPLY : receives
    
    SEQUENCE ||--o{ SEQUENCE_VARIANT : contains
    
    LEAD ||--o{ CAMPAIGN_LEAD : assigned
    LEAD ||--o{ EMAIL_LOG : targeted
    LEAD ||--o{ LEAD_REPLY : generates
    
    CAMPAIGN_LEAD ||--o{ QUEUE_SLOT : schedules
    
    INBOX {
        int id PK
        string email UK
        string display_name
        int max_emails_per_day
        int wait_minutes_between
        int max_jitter_seconds
        string provider
        string tracking_domain
        string beacon_base_url
    }

    CAMPAIGN {
        int id PK
        string public_id UK
        string name
        boolean paused
        int priority
        json sending_days
        string sending_hours_start
        string sending_hours_end
    }

    LEAD {
        int id PK
        string email UK
        string name
        json custom_data
        string status
        string email_verification_status
        string provider
        datetime created_at
    }

    SEQUENCE {
        int id PK
        int campaign_id FK
        int position
        string subject
        text body
        boolean is_html
        int wait_days_after_previous
        string sequence_type
    }

    SEQUENCE_VARIANT {
        int id PK
        int sequence_id FK
        string label
        string subject
        text body
        boolean is_html
    }

    CAMPAIGN_LEAD {
        int id PK
        int campaign_id FK
        int lead_id FK
        datetime enrolled_at
        string enrollment_status
        string interest_status
        boolean sending_paused
    }

    QUEUE_SLOT {
        int id PK
        int campaign_lead_id FK
        int inbox_id FK
        int sequence_index
        datetime scheduled_date
        datetime slot_time
        string status
    }

    EMAIL_LOG {
        int id PK
        int lead_id FK
        int campaign_id FK
        int inbox_id FK
        int sequence_index
        int variant_id FK
        string open_token UK
        datetime sent_at
    }

    LEAD_REPLY {
        int id PK
        int lead_id FK
        int campaign_id FK
        string reply_classification
        text snippet
        datetime replied_at
    }
```

### Core Schema Entity Specifications

| Entity | Primary Key | Key Attributes & Constraints | Purpose |
| :--- | :--- | :--- | :--- |
| `Inbox` | `id` (Integer) | `email` (Unique String), `provider` (gmail, office365, smtp), `max_emails_per_day` (Int, def: 50), `wait_minutes_between` (Int), `max_jitter_seconds` (Int) | Stores authenticated sending mailbox credentials, daily quota limits, and deliverability throttle rules. |
| `Campaign` | `id` (Integer) | `public_id` (Unique String), `name` (String), `paused` (Boolean), `sending_days` (JSON Array [0..6]), `sending_hours_start` ("09:00"), `sending_hours_end` ("17:00") | Governs cadence operational schedules, timezone windows, and global execution states. |
| `Lead` | `id` (Integer) | `email` (Indexed String), `custom_data` (JSON Object for merge tags), `status` (active, unsubscribed, bounced, replied), `email_verification_status` (valid, risky, invalid) | Master prospect record storing dynamic custom variables (company, title, tech stack) for personalization. |
| `Sequence` | `id` (Integer) | `campaign_id` (FK), `position` (Int), `subject` (String, nullable for replies), `wait_days_after_previous` (Int), `is_html` (Boolean) | Defines step touchpoints in the cold outbound sequence pipeline. |
| `CampaignLead` | `id` (Integer) | `campaign_id` (FK), `lead_id` (FK), Unique(`campaign_id`, `lead_id`), `enrollment_status` (active, completed, paused), `interest_status` (interested, neutral, etc.) | Tracks individual lead lifecycle state within a specific campaign run. |
| `QueueSlot` | `id` (Integer) | `campaign_lead_id` (FK), `inbox_id` (FK), `sequence_index` (Int), `slot_time` (DateTime), `status` (pending, executing, sent) | Pre-computed sending reservations preventing race conditions across concurrent workers. |

---

### 6.1 Comprehensive Database Schema Specification

Quickly utilizes lightweight, indexed relational schemas for low-latency CRUD actions and fast search filtering.

#### Table: `leads`
Fast-ingestion prospect leads with tagging and status categorisation.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Unique lead record ID |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Prospect contact name |
| `company` | `VARCHAR(255)` | `NULLABLE` | `NULL` | Business company name |
| `email` | `VARCHAR(255)` | `NOT NULL` | - | Contact email |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'new'` | Lead state (`new`, `active`, `closed`) |
| `created_at` | `TIMESTAMPTZ` | `NOT NULL` | `NOW()` | Ingestion timestamp |

*Indexes:* `idx_quickly_email` (`(email)`), `idx_quickly_status` (`(status)`).

---

#### Table: `tasks`
Sales rep follow-up obligations and calendar reminders.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Task ID |
| `lead_id` | `UUID` | `NOT NULL, FK -> leads(id)` | - | Associated lead |
| `title` | `VARCHAR(255)` | `NOT NULL` | - | Actionable task summary |
| `due_date` | `DATE` | `NOT NULL` | - | Deadline for completion |
| `is_completed`| `BOOLEAN` | `NOT NULL` | `false` | Completion status flag |

*Indexes:* `idx_tasks_lead` (`(lead_id)`), `idx_tasks_due` (`(due_date)`).

---

## 7. API Architecture, Webhooks & Key Endpoints

All administrative API endpoints enforce JWT access tokens (`Authorization: Bearer <token>`) or API keys (`X-API-Key: <key>`). Public endpoints are strictly limited to tracking pixels (`/o/{token}`) and click redirects (`/c/{token}`).

### 1. Create Outbound Campaign
```http
POST /api/campaigns
Content-Type: application/json
Authorization: Bearer eyJhbGciOiJIUzI1Ni...

{
  "name": "Q4 Enterprise SaaS Expansion",
  "priority": 1,
  "sending_days": [0, 1, 2, 3, 4],
  "sending_hours_start": "09:00",
  "sending_hours_end": "17:00",
  "inbox_ids": [1, 3, 5]
}
```
**Response (201 Created):**
```json
{
  "id": 12,
  "public_id": "cmp_8fa9102c",
  "name": "Q4 Enterprise SaaS Expansion",
  "paused": false,
  "priority": 1,
  "created_at": "2026-10-24T10:00:00Z"
}
```

### 2. Bulk Lead Ingestion with Custom Data
```http
POST /api/leads/bulk
Content-Type: application/json
Authorization: Bearer eyJhbGciOiJIUzI1Ni...

{
  "campaign_id": 12,
  "leads": [
    {
      "email": "sarah.chen@acmecorp.com",
      "name": "Sarah Chen",
      "custom_data": {
        "company": "Acme Corp",
        "title": "VP of Revenue Operations",
        "recent_funding": "Series B",
        "crm_tool": "Salesforce"
      }
    }
  ]
}
```
**Response (200 OK):**
```json
{
  "enrolled_count": 1,
  "duplicate_count": 0,
  "invalid_count": 0
}
```

### 3. Outbound Webhook Event Notification
When events occur, Quickly sends an HTTP POST request to registered webhook URLs:
```json
{
  "event": "lead.replied",
  "timestamp": "2026-10-24T10:14:22Z",
  "data": {
    "campaign_id": 12,
    "lead_id": 412,
    "lead_email": "sarah.chen@acmecorp.com",
    "inbox_id": 3,
    "inbox_email": "alex.growth@marketingstudio.internal",
    "classification": "interested",
    "sentiment_score": 0.94,
    "subject": "Re: Automating Acme Corp's pipeline",
    "snippet": "Thanks for reaching out Alex. We are looking into this right now. Are you free Thursday at 2 PM?"
  }
}
```

---

### 7.5 End-to-End Operational Lifecycle Sequence

The following sequence shows the high-speed lead ingestion and instant task scheduling workflow.

```mermaid
sequenceDiagram
    autonumber
    actor Operator as Sales Rep
    participant App as Quickly Web App
    participant Service as QuickLeadService
    participant DB as Relational Database
    participant Scheduler as TaskScheduler

    Operator->>App: Submits Quick-Add Lead Form
    App->>Service: fastCreateLead({name, email, company})
    Service->>DB: INSERT into leads (name, email, company, status='new')
    DB-->>Service: Lead created (lead_id)
    
    Service->>Scheduler: scheduleFollowUp(lead_id, dueDate=NOW() + 3 days)
    Scheduler->>DB: INSERT into tasks (lead_id, title='First Outreach Call')
    DB-->>Scheduler: Task stored
    
    Service-->>App: Lead and Task confirmed
    App-->>Operator: Display instant success state
```

---

## 8. Strategic Value & Takeaways for MarketingStudio

### Reusable Architectural Patterns
1. **DNS Provider-Matching Email Routing**:  
   Quickly's MX resolution pattern (`app/email_provider.py`) allows MarketingStudio to dynamically route cold outreach through mailboxes hosted on the same infrastructure as the recipient (Google-to-Google, Microsoft-to-Microsoft). This dramatically reduces spam flagging and maximizes inbox placement rates.
2. **Jitter-Augmented Pre-Allocated Queue Slots**:  
   Rather than executing immediate cron sends that generate unnatural burst patterns, Quickly calculates time slots with randomized seconds jitter (`app/queue_logic.py`). MarketingStudio should replicate this pre-allocation strategy to make automated SDR volume indistinguishable from human sales activity.
3. **Multi-Model Sentiment Adapter Pattern**:  
   Quickly’s unified AI classifier interface (`app/ai_classifier.py`) separates prompt templating from inference execution across 19 providers. MarketingStudio can use this pattern to let customers toggle between cost-effective local LLMs (Ollama) for routine auto-reply filtering and high-reasoning cloud LLMs (Claude 3.5 Sonnet / GPT-4o) for complex objection analysis.
4. **First-Party Workflow Automation Nodes**:  
   The inclusion of a native n8n node package (`n8n-node/`) establishes an ecosystem multiplier. MarketingStudio can provide pre-built n8n and Zapier integration packages that let growth teams construct sophisticated multi-stage sales funnels without writing custom webhook middleware.
