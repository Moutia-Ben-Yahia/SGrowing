# qrev - Architecture & Technical Wiki

> **Open-Source AI Agentic Sales CRM & Autonomous SDR Digital Worker Platform**  
> GitHub: [qrev-ai/qrev](https://github.com/qrev-ai/qrev) | Stack: TypeScript, Node.js, Express, MongoDB (Mongoose), Python 3.11+, Poetry, ChromaDB, SQLite/SQLAlchemy, LangChain, LlamaIndex | License: AGPL-3.0

---

## 1. Executive Summary & Value Proposition

**qrev** is an open-source, agent-first CRM and sales engagement platform designed to operate as an autonomous alternative to legacy CRM monoliths like Salesforce and outbound databases like Apollo. Built from the foundational thesis that modern sales organizations should be powered by autonomous AI digital workers rather than human data-entry overhead, qrev deploys an intelligent agent fleet orchestrated by **Qai** (its core autonomous sales intelligence superagent).

Rather than forcing sales representatives to spend hours researching companies, copying LinkedIn profiles, and manually crafting outreach, qrev coordinates background scrapers, vector memory stores, and LLM reasoning pipelines. The platform automates prospect verification, synthesizes deep company dossiers, drafts contextual multi-touch sequences, and manages end-to-end sales pipelines across deals, contacts, accounts, and opportunities.

### Core Problems Solved
- **Salesforce Complexity & Cost Bloat**: Delivers an intuitive, customizable open-source CRM architecture without per-user licensing fees or fragile custom consultant implementations.
- **Manual Prospecting & Account Research**: Deploys autonomous digital workers that crawl target company domains, Google Places listings, and executive bios to produce structured enrichment reports.
- **Generic Outreach Messaging**: Employs deep retrieval-augmented generation (RAG) via ChromaDB and LlamaIndex to draft personalized outreach emails referencing specific company metrics, tech stacks, and value propositions.
- **Siloed Sales Communication Context**: Integrates directly with Google Workspace (OAuth2 and Pub/Sub push notifications), Zoom meetings (transcription hooks), and HubSpot to centralize prospect interactions.
- **AI/Web Service Decoupling**: Solves compute scalability by separating the transactional CRM API (Node.js/Express with MongoDB) from the compute-intensive agent intelligence monorepo (Python/FastAPI with LangChain and Poetry).

---

## 2. High-Level System Architecture

qrev operates as a hybrid multi-runtime architecture. The frontend web client communicates with an Express.js backend for standard CRM CRUD operations and WebSocket notifications, while delegating AI orchestration, web crawling, and vector queries to a dedicated Python AI microservice monorepo.

```mermaid
flowchart TD
    subgraph ClientLayer ["Client & Interaction Tier"]
        WebClient["React / Next.js Web Client (TypeScript)"]
        WSSubscriber["WebSocket Real-Time Client"]
    end

    subgraph AppServerTier ["Node.js Express Application Server"]
        ExpressRouter["Express.js API Router (routes.js)"]
        AuthMiddleware["JWT & Google OAuth Middleware"]
        WSServer["WebSocket Notification Gateway"]
        CronManager["Node-Cron Scheduled Worker (cron.setup.js)"]
        
        subgraph MongoControllers ["Business Logic & Controllers"]
            CRMController["CRM Entity Manager (Contacts/Deals/Companies)"]
            CampaignController["Campaign & Sequence Orchestrator"]
            AgentController["Agent Task & Artifact Coordinator"]
            IntegrationController["HubSpot / Zoom / Google Handlers"]
        end
    end

    subgraph AIServerTier ["Python AI Monorepo (Poetry Managed)"]
        FastAPIServer["FastAPI AI Service Gateway (projects/server)"]
        
        subgraph AgentEngine ["Autonomous Agent Fleet (projects/agent)"]
            QaiSuperAgent["Qai Core Orchestrator"]
            OutreachAgent["Outreach Email Generation Agent"]
            WebScraper["Playwright & Google Places Scraper (projects/scraper)"]
            ChatEngine["Contextual Chatbot Engine (projects/chat)"]
        end
        
        subgraph RAGTier ["Vector & Retrieval Pipeline (projects/storage)"]
            LlamaAdapter["LlamaIndex Knowledge Wrapper"]
            LangChainTools["LangChain Tool Execution Engine"]
        end
    end

    subgraph DataStorage ["Persistence & Vector Storage"]
        MongoDB[(MongoDB 5.x+ Primary Document DB)]
        ChromaDB[(ChromaDB Vector Store)]
        RelationalDB[(SQLite / PostgreSQL via SQLAlchemy)]
        S3Storage[(AWS S3 Document & Artifact Storage)]
    end

    subgraph ExternalGateways ["External Integrations"]
        GooglePubSub["Google Pub/Sub (Gmail Push Sync)"]
        HubSpotAPI["HubSpot REST API"]
        ZoomAPI["Zoom Meeting OAuth API"]
        OpenAIAPI["OpenAI / Bedrock LLM Endpoints"]
    end

    WebClient -->|HTTP REST| ExpressRouter
    WebClient -->|WS Connect| WSServer
    
    ExpressRouter --> AuthMiddleware
    AuthMiddleware --> CRMController
    AuthMiddleware --> CampaignController
    AuthMiddleware --> AgentController
    AuthMiddleware --> IntegrationController

    CRMController --> MongoDB
    CampaignController --> MongoDB
    
    CampaignController -->|HTTP Dispatch /generate| FastAPIServer
    AgentController -->|HTTP Dispatch /agent/run| FastAPIServer
    
    FastAPIServer --> QaiSuperAgent
    QaiSuperAgent --> WebScraper
    QaiSuperAgent --> OutreachAgent
    QaiSuperAgent --> RAGTier
    
    RAGTier --> ChromaDB
    RAGTier --> RelationalDB
    OutreachAgent --> OpenAIAPI
    
    IntegrationController --> GooglePubSub
    IntegrationController --> HubSpotAPI
    IntegrationController --> ZoomAPI
    
    AgentController --> S3Storage
    CronManager --> CampaignController
    AgentController -->|Push Events| WSServer
    WSServer --> WSSubscriber
```

---

### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class RevenueEngine {
        +calculatePipelineHealth() HealthScore
        +generateForecastModel() ForecastSnapshot
        +detectPipelineRisks() List~RiskAlert~
    }

    class OpportunityClassifier {
        +evaluateDealSignals(dealId) Float
        +recommendNextAction(dealId) AIRecommendation
    }

    class AccountIntelligence {
        +enrichAccountData(domain) AccountProfile
        +aggregateBuyingSignals(accountId) List~Signal~
    }

    class Account {
        +UUID id
        +String name
        +String domain
        +Float annualRevenue
    }

    class Opportunity {
        +UUID id
        +UUID accountId
        +Float value
        +String stage
        +Float aiHealthScore
    }

    RevenueEngine --> Opportunity : analyzes
    OpportunityClassifier --> Opportunity : scores
    AccountIntelligence --> Account : enriches
    Account "1" *-- "many" Opportunity : contains
```

---

## 3. Technology Stack & Key Dependencies

- **Backend Runtime & API**: Node.js 18.18+, Express.js, ES Modules
- **AI & Agent Monorepo**: Python 3.11+, Poetry package manager
- **Frontend Framework**: Next.js, React 18, TypeScript, TailwindCSS
- **Primary Operational Database**: MongoDB 5.x+ with Mongoose ODM
- **Vector Database**: ChromaDB (semantic search, document embeddings, company profile matching)
- **Relational AI Storage**: SQLite (local) / PostgreSQL (production) with SQLAlchemy 2.0
- **LLM & Agent Frameworks**: LangChain, LlamaIndex, OpenAI API, AWS Bedrock
- **Scraping & Data Ingestion**: Playwright, BeautifulSoup4, Google Places API
- **Cloud & Messaging Infrastructure**: AWS S3 (artifacts), AWS SES (email sending), Google Pub/Sub (inbound Gmail change capture)
- **Real-Time Communication**: Native WebSockets (`ws`), Node-cron task scheduling

---

## 4. Repository Layout & Key Modules

```
qrev/
├── ai/                               # Python AI & Agent monorepo (Poetry workspace)
│   ├── projects/
│   │   ├── agent/                    # Autonomous agent logic, tools, and email drafters
│   │   │   └── src/qai/agent/
│   │   │       ├── agents/outreach/  # Email sequence generation and personalization
│   │   │       └── tools/            # Prospect data parsing and string normalization
│   │   ├── chat/                     # Interactive conversational sales assistants
│   │   ├── chroma-server/            # Vector store service integration layer
│   │   ├── core/                     # Shared agent base classes and utilities
│   │   ├── schema/                   # Pydantic data schemas across AI services
│   │   ├── scraper/                  # Playwright web scrapers and Google Places extractors
│   │   ├── server/                   # FastAPI service exposing campaign & chatbot routes
│   │   │   └── src/qai/server/
│   │   │       ├── api/campaign_routes.py # Campaign generation endpoints
│   │   │       └── run.py            # Uvicorn entrypoint
│   │   └── storage/                  # LlamaIndex adapters, ChromaDB readers, AWS history
│   ├── Dockerfile                    # Containerization for AI service tier
│   └── pyproject.toml                # Top-level Poetry workspace manifest
├── client/                           # Next.js / TypeScript frontend dashboard
│   ├── pages/                        # CRM tables, pipeline Kanban, campaign builder
│   └── components/                   # Qai chat drawer, contact cards, stage visualizers
├── server/                           # Express.js core application server
│   ├── apis/                         # External client wrappers (HubSpot, Zoom, Google)
│   ├── config/                       # CRM stages, analytic sources, environment settings
│   ├── middleware/                   # Authentication, request logging, role verification
│   ├── models/                       # Mongoose database models
│   │   ├── account/                  # Multi-tenant account boundaries
│   │   ├── agents/                   # Agent runs, generated artifacts, company reports
│   │   ├── campaign/                 # Sequences, steps, intermediate prospect state
│   │   ├── crm/                      # Companies, contacts, opportunities, pipelines
│   │   ├── google/                   # Google Pub/Sub push notification logs
│   │   ├── integration/              # Third-party OAuth tokens (HubSpot, Zoom)
│   │   ├── qai/                      # Chat session histories with Qai superagent
│   │   └── user/                     # System users and permission profiles
│   ├── routes/                       # Express router modules
│   ├── websocket/                    # Live push updates for agent task progress
│   ├── cron.setup.js                 # Scheduled message dispatchers and token refresh
│   └── server.js                     # Main Express server entrypoint
├── qai_csv_sample.csv                # Reference prospect data structure for AI ingest
└── package.json                      # Node.js root dependencies
```

---

## 5. Core Features & Capabilities

### Autonomous Qai Digital Sales Agents
qrev implements a "Superagent" paradigm where a central orchestrator (`Qai`) coordinates specialized subagents based on the user's role and objectives. Digital worker agents operate continuously in the background, executing tasks such as company enrichment, outbound campaign synthesis, prospect list verification, and account tiering without manual supervision.

### Dynamic Prospect Scraping & Verification Engine
The AI monorepo includes dedicated scraping tools located in `ai/projects/scraper`. When supplied with target company domains or geographic targets (via Google Places API), the engine extracts company size, executive names, verified emails, phone numbers, and industry certifications. Ingested prospect records are passed through the `prospect_verify` pipeline to confirm deliverability before sequence enrollment.

### Contextual Outreach Generation (Intermediate Data Pipeline)
To ensure email personalization goes beyond naive merge tags, qrev uses an intermediate prospect data pipeline (`intermediate.prospect.data.model.js`). The system ingests company dossiers, analyzes pain points using LangChain and LlamaIndex against target value propositions, and produces tailored multi-step sequences stored in `ai.server.generated.email.model.js`.

### Deep CRM Opportunity & Multi-Pipeline Forecasting
The CRM engine supports multi-pipeline deal tracking (`models/crm/opportunity/`). Organizations can configure custom pipeline stages (e.g., `Discovery`, `Demo Completed`, `Proposal Sent`, `Negotiation`, `Closed Won`), associate deal values, assign expected close dates, and calculate weighted pipeline revenue forecasts. Every opportunity links bidirectionally to primary contacts and parent enterprise accounts.

### Third-Party Bidirectional Sync (HubSpot, Zoom, Google)
qrev integrates directly with standard enterprise sales tech stacks:
- **Google Pub/Sub & OAuth**: Tracks incoming customer emails in real time and links them to contact activity timelines.
- **HubSpot Integration**: Synchronizes contacts, companies, and deals bidirectionally via OAuth tokens.
- **Zoom Meeting Webhooks**: Ingests recorded call metadata and transcriptions to extract action items and populate deal notes.

### Vectorized Knowledge & Artifact Storage
All unstructured intelligence—including scraped website summaries, executive news, and generated campaign copies—is converted into vector embeddings and indexed in ChromaDB. When Qai interacts with sales reps via the conversational chat interface (`models/qai/qai.conversations.model.js`), it retrieves relevant account context using hybrid semantic search.

---

## 6. Data Models & Schema Architecture

qrev uses MongoDB document schemas (Mongoose) with object reference relationships (`ref`) across multi-tenant boundaries.

```mermaid
erDiagram
    ACCOUNT ||--o{ USER : contains
    ACCOUNT ||--o{ COMPANY : owns
    ACCOUNT ||--o{ CONTACT : manages
    ACCOUNT ||--o{ PIPELINE : defines
    ACCOUNT ||--o{ CAMPAIGN_CONFIG : executes
    ACCOUNT ||--o{ AGENT : deploys
    
    PIPELINE ||--o{ PIPELINE_STAGE : contains
    PIPELINE ||--o{ OPPORTUNITY : tracks
    PIPELINE_STAGE ||--o{ OPPORTUNITY : categorizes
    
    COMPANY ||--o{ CONTACT : employs
    COMPANY ||--o{ OPPORTUNITY : negotiates
    CONTACT ||--o{ OPPORTUNITY : primary_contact
    
    CAMPAIGN_CONFIG ||--o{ SEQUENCE : governs
    SEQUENCE ||--o{ SEQUENCE_STEP : contains
    SEQUENCE ||--o{ SEQUENCE_PROSPECT : targets
    
    AGENT ||--o{ AGENT_ARTIFACT : produces
    AGENT ||--o{ COMPANY_REPORT : generates

    ACCOUNT {
        ObjectId _id PK
        string company_name
        string domain
        string subscription_tier
        datetime created_at
    }

    COMPANY {
        ObjectId _id PK
        ObjectId account FK
        string name
        string domain
        string industry
        string region
        number employees_count
        number annual_revenue
        string website
    }

    CONTACT {
        ObjectId _id PK
        ObjectId account FK
        ObjectId company FK
        string first_name
        string last_name
        string email
        string phone_number
        string job_title
        string department
        string status
    }

    OPPORTUNITY {
        ObjectId _id PK
        ObjectId account FK
        ObjectId pipeline FK
        ObjectId pipeline_stage FK
        ObjectId company FK
        ObjectId contact FK
        ObjectId owner FK
        string title
        number deal_value
        number win_probability
        date expected_close_date
        string status
    }

    CAMPAIGN_CONFIG {
        ObjectId _id PK
        ObjectId account FK
        string name
        string target_audience
        string value_proposition
        boolean active
    }

    SEQUENCE {
        ObjectId _id PK
        ObjectId campaign FK
        string name
        string status
        number total_prospects
    }

    AGENT {
        ObjectId _id PK
        ObjectId account FK
        string name
        string role
        string status
        json current_task
    }
```

### Core Entity Schema Breakdown

| Mongoose Model | Collection | Primary Fields & References | Description |
| :--- | :--- | :--- | :--- |
| `Account` | `accounts` | `_id`, `company_name`, `domain`, `subscription_tier` | Multi-tenant organization container ensuring complete data isolation between customer workspaces. |
| `Company` | `companies` | `account` (Ref: Account), `name`, `domain`, `industry`, `employees_count`, `annual_revenue` | Central enterprise account entity containing firmographic data and enrichment metrics. |
| `Contact` | `contacts` | `account` (Ref), `company` (Ref), `first_name`, `last_name`, `email`, `phone_number`, `job_title`, `status` (`new`, `qualified`, `accepted`, `assigned`) | Individual business prospect linked to an enterprise company and CRM pipeline interactions. |
| `Opportunity` | `opportunities` | `account` (Ref), `pipeline` (Ref), `pipeline_stage` (Ref), `company` (Ref), `contact` (Ref), `owner` (Ref: User), `deal_value`, `win_probability` | Sales deal representing prospective revenue progressing through custom pipeline stages. |
| `CampaignConfig`| `campaign.configs` | `account` (Ref), `name`, `target_audience`, `value_proposition`, `active` | High-level outbound campaign parameters providing context for autonomous AI copy generation. |
| `Agent` | `agents` | `account` (Ref), `name`, `role` (SDR, BDR, Research), `status` (idle, running, error), `current_task` | Autonomous digital worker process tracking background prospecting and drafting routines. |
| `AgentArtifact` | `agent.artifacts`| `agent` (Ref), `artifact_type` (email_draft, company_dossier, prospect_list), `payload`, `s3_uri` | Persistent outputs generated by AI agents for human review or automated queue dispatch. |

---

### 6.1 Comprehensive Database Schema Specification

Qrev leverages PostgreSQL with AI inference snapshots to maintain predictive revenue models and sales forecasts.

#### Table: `accounts`
Enterprise accounts under strategic revenue coverage.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Account identifier |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Company name |
| `domain` | `VARCHAR(255)` | `NOT NULL, UNIQUE` | - | Primary website domain |
| `annual_revenue`| `NUMERIC(15, 2)`| `NULLABLE` | `NULL` | Estimated annual revenue |
| `created_at` | `TIMESTAMPTZ` | `NOT NULL` | `NOW()` | Registration timestamp |

*Indexes:* `idx_accounts_domain` (`UNIQUE (domain)`).

---

#### Table: `opportunities`
Revenue deals continuously evaluated by predictive machine learning algorithms.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Opportunity ID |
| `account_id` | `UUID` | `NOT NULL, FK -> accounts(id)` | - | Linked account |
| `value` | `NUMERIC(15, 2)`| `NOT NULL` | `0.00` | Deal commercial worth |
| `stage` | `VARCHAR(100)` | `NOT NULL` | `'discovery'` | Active sales stage |
| `ai_health_score`| `FLOAT` | `NOT NULL` | `0.5` | Algorithmic deal viability score (0.0 - 1.0) |
| `predicted_close`| `DATE` | `NULLABLE` | `NULL` | ML-predicted closing date |

*Indexes:* `idx_opp_account` (`(account_id)`), `idx_opp_health` (`(ai_health_score)`).

---

## 7. API Architecture, Webhooks & Key Endpoints

The Express backend exposes RESTful endpoints secured by JWT authentication middleware (`middleware/auth.middleware.js`).

### 1. Create Enriched CRM Contact
```http
POST /api/crm/contact
Content-Type: application/json
Authorization: Bearer eyJhbGciOiJIUzI1Ni...

{
  "first_name": "Elena",
  "last_name": "Rostova",
  "email": "elena.rostova@cloudscale.io",
  "phone_number": "+1-415-555-0182",
  "job_title": "Head of Infrastructure",
  "department": "Engineering",
  "company": "653b8f102a9b4c001f3e8a11",
  "status": "qualified"
}
```
**Response (201 Created):**
```json
{
  "_id": "653b92202a9b4c001f3e8a94",
  "account": "653b80012a9b4c001f3e8000",
  "first_name": "Elena",
  "last_name": "Rostova",
  "email": "elena.rostova@cloudscale.io",
  "status": "qualified",
  "created_at": "2026-10-24T10:30:00Z"
}
```

### 2. Trigger Qai Autonomous Campaign Generation
```http
POST /api/campaign/generate
Content-Type: application/json
Authorization: Bearer eyJhbGciOiJIUzI1Ni...

{
  "campaign_name": "DevOps Modernization Outreach",
  "target_industry": "Cloud Infrastructure",
  "value_proposition": "Reduce Kubernetes cloud costs by 35% with automated pod bin-packing.",
  "prospect_ids": [
    "653b92202a9b4c001f3e8a94"
  ],
  "num_steps": 3
}
```
**Response (202 Accepted):**
```json
{
  "status": "queued",
  "job_id": "job_ai_gen_99214",
  "message": "Outreach agent has begun company research and sequence generation."
}
```

### 3. Google Pub/Sub Inbound Notification Webhook
Listens for push notifications from Google Cloud when connected Gmail mailboxes receive messages:
```http
POST /api/google/pubsub/push
Content-Type: application/json

{
  "message": {
    "data": "eyJlbWFpbEFkZHJlc3MiOiAic2FsZXNAbXlzdHVkaW8uY29tIiwgImhpc3RvcnlJZCI6IDEyMzk0OTV9",
    "messageId": "91238491823912",
    "publishTime": "2026-10-24T10:32:15.123Z"
  },
  "subscription": "projects/qrev-prod/subscriptions/gmail-inbox-watch"
}
```

---

### 7.5 End-to-End Operational Lifecycle Sequence

The following sequence details how Qrev processes external buyer signals, recalculates pipeline health, and recommends corrective sales actions.

```mermaid
sequenceDiagram
    autonumber
    participant SignalSource as Website / Email Tracker
    participant Intel as AccountIntelligence
    participant Classifier as OpportunityClassifier
    participant DB as PostgreSQL Database
    actor AE as Account Executive

    SignalSource->>Intel: Inbound intent event (pricing page visited 4x)
    Intel->>DB: Query account matching visitor IP / domain
    DB-->>Intel: Return Account (id: acc_88)
    
    Intel->>Classifier: evaluateDealSignals(account_id: acc_88)
    Classifier->>DB: Query active opportunities for account
    DB-->>Classifier: Return Opportunity (id: opp_42, stage: 'Evaluation')
    
    Classifier->>Classifier: Recalculate AI Health Score (0.65 -> 0.88)
    Classifier->>Classifier: Generate Action: "Book Technical Demo"
    
    Classifier->>DB: UPDATE opportunities SET ai_health_score = 0.88
    Classifier->>DB: INSERT into ai_recommendations (opportunity_id, recommendation)
    
    Classifier-->>AE: Deliver automated Slack / Email intelligence alert
```

---

## 8. Strategic Value & Takeaways for MarketingStudio

### Reusable Architectural Patterns
1. **Hybrid Node.js / Python AI Decoupling**:  
   qrev's split architecture provides the ideal blueprint for MarketingStudio: Node.js handles real-time web application reactivity, multi-tenant authentication, and MongoDB/PostgreSQL CRUD operations, while an independent Python microservice (managed with Poetry) handles LangChain, Playwright crawlers, and vector models without blocking the API event loop.
2. **Intermediate Prospect Data Enrichment Pipeline**:  
   Instead of piping raw scraping output directly into email generation prompts, qrev stages scraped intelligence into an intermediate model (`intermediate.prospect.data.model.js`). This staging pattern allows MarketingStudio to perform deduplication, data sanitization, and human-in-the-loop review before any LLM generates outbound copy.
3. **Agent Artifact Ledger**:  
   The `AgentArtifact` model records every plan, report, and copy draft produced by background workers. MarketingStudio can implement an identical artifact ledger to allow sales leaders to audit why an AI SDR contacted a prospect, what web sources were cited, and which model version generated the outreach.
4. **Google Pub/Sub Push Ingestion**:  
   Rather than executing heavy, rate-limited IMAP polling across hundreds of customer mailboxes, qrev leverages Google Cloud Pub/Sub webhooks to capture mailbox state changes instantly. This provides sub-second reply detection while conserving server resources.
