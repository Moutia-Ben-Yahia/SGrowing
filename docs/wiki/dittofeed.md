# dittofeed - Architecture & Technical Wiki

> **Open-Source Omnichannel Customer Engagement & Event-Driven Journey Platform**
> GitHub: [dittofeed/dittofeed](https://github.com/dittofeed/dittofeed.git) | Stack: TypeScript, Fastify, Next.js, Temporal.io, ClickHouse, PostgreSQL | License: MIT

---

## 1. Executive Summary & Value Proposition

**dittofeed** is a modern, event-driven customer engagement platform engineered as an open-source alternative to Customer.io, Braze, and Segment. It enables marketing and growth teams to track user events across web and mobile apps, define dynamic customer segments, and trigger multi-channel user journeys (Email, SMS, Push, Webhook) with extreme temporal durability.

### Core Problems Solved
- **Event-Driven Journey Orchestration**: Seamlessly execute branching customer workflows that wait for specific user actions (e.g., "Wait 3 days OR until user creates a project").
- **Extreme Durability with Temporal**: Uses Temporal.io to manage long-running workflow state, guaranteeing zero lost journeys even across server crashes and redeployments.
- **High-Velocity Event Ingestion**: Couples ClickHouse for massive, append-only time-series analytics with PostgreSQL for relational workspace metadata.
- **Segment-Level Dynamic Triggers**: Continuously evaluates user traits and event frequency to automatically enter or exit users from engagement funnels.

---

## 2. High-Level System Architecture

dittofeed's architecture is built around three complementary engines: an event ingestion API, a ClickHouse analytics warehouse, and a Temporal workflow worker engine.

```mermaid
flowchart TD
    subgraph Ingestion ["Client Apps & Ingestion"]
        WebApps["Web Apps (Segment / Reverse ETL)"]
        MobileSDK["Mobile SDKs & Telemetry"]
    end

    subgraph DittofeedCluster ["dittofeed Engine"]
        APIServer["Fastify API Server (packages/api)"]
        Dashboard["Next.js Dashboard (packages/dashboard)"]
        
        subgraph StorageLayer ["Multi-Model Storage"]
            Postgres[(PostgreSQL - Meta & Config)]
            ClickHouse[(ClickHouse - Time Series Events)]
            Kafka[(Kafka / Ingestion Queue)]
        end
        
        subgraph Orchestration ["Journey & Workflow Orchestration"]
            Temporal["Temporal.io Server"]
            Worker["Dittofeed Worker (packages/worker)"]
        end
    end

    subgraph Providers ["Delivery Providers"]
        Sendgrid["SendGrid / SES / Postmark"]
        Twilio["Twilio SMS"]
        CustomWebhook["Third-Party Webhooks"]
    end

    WebApps -->|POST /api/public/apps/identify| APIServer
    WebApps -->|POST /api/public/apps/track| APIServer
    Dashboard -->|REST / Admin| APIServer
    
    APIServer --> Postgres
    APIServer --> Kafka
    Kafka --> ClickHouse
    
    APIServer -->|Trigger Journey| Temporal
    Temporal --> Worker
    Worker -->|Query User History| ClickHouse
    Worker -->|Read Segment Rules| Postgres
    Worker --> Sendgrid
    Worker --> Twilio
    Worker --> CustomWebhook
```

---

### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class WorkflowOrchestrator {
        +TemporalClient temporal
        +triggerJourney(journeyId, userId) Promise~Void~
        +signalUserEvent(userId, eventName, data) Promise~Void~
    }

    class SegmentEngine {
        +ClickHouseClient clickhouse
        +evaluateSegmentMembership(userId, segmentId) Promise~Boolean~
        +refreshSegmentAssignments(segmentId) Promise~Int~
    }

    class JourneyNodeRunner {
        +executeNode(node, userContext) NodeExecutionResult
        +dispatchMessage(templateId, channel, user) Promise~Receipt~
    }

    class Journey {
        +UUID id
        +UUID workspaceId
        +String name
        +String status
        +JSON definition
    }

    class Segment {
        +UUID id
        +UUID workspaceId
        +String name
        +JSON definition
    }

    WorkflowOrchestrator --> JourneyNodeRunner : executes
    JourneyNodeRunner --> SegmentEngine : evaluates_conditions
    Journey "1" *-- "many" Segment : references
```

---

## 3. Technology Stack & Key Dependencies

- **Language**: TypeScript (Node.js 20+)
- **API Framework**: Fastify (high-throughput JSON routing)
- **Frontend Dashboard**: Next.js, React, TailwindCSS, Shadcn UI
- **Databases**:
  - ClickHouse (analytical database for user event streams and segment calculation)
  - PostgreSQL (relational database for users, journeys, templates, segments)
- **Workflow Orchestration**: Temporal.io (fault-tolerant workflow executions)
- **ORM**: Prisma for PostgreSQL queries
- **Message Broker**: Apache Kafka or native queue for high-volume ingest

---

## 4. Repository Layout & Key Modules

```
dittofeed/
├── packages/
│   ├── admin-cli/           # CLI tool for database migrations and tenant ops
│   ├── api/                 # Fastify HTTP ingestion & management API
│   ├── backend-lib/         # Shared domain logic, ClickHouse queries, Prisma models
│   │   ├── src/
│   │   │   ├── segments.ts  # Segment evaluation SQL generators
│   │   │   ├── journeys.ts  # Journey step transition logic
│   │   │   └── clickhouse.ts# ClickHouse connection pool & event queries
│   ├── dashboard/           # Next.js web application for marketers
│   ├── emailo/              # Email template parser and compiler
│   ├── isomorphic-lib/      # Universal types and validation schemas
│   ├── lite/                # SQLite/single-node local testing build
│   └── worker/              # Temporal workflow workers and BullMQ runners
├── docker-compose.yaml      # Complete multi-service development compose
└── helm-charts/             # Kubernetes production deployment charts
```

---

## 5. Core Features & Capabilities

1. **Visual Customer Journey Canvas**:
   - Multi-node journeys: Message Node, Delay Node (wait N hours/days), Wait Until Event Node (e.g. wait up to 7 days for `checkout_completed`), Branch/Split Node.
2. **Real-Time Segment Calculation**:
   - Generates optimized ClickHouse SQL to compute whether a user matches traits (`user.plan == 'pro'`) or behavior (`user performed 'signed_in' >= 3 times in last 7 days`).
3. **Multi-Channel Message Dispatch**:
   - Email (SendGrid, AWS SES, Postmark, Resend, SMTP).
   - SMS (Twilio).
   - Custom Webhook triggers (syncing back to CRM or triggering internal microservices).
4. **Subscription Center & Frequency Capping**:
   - Manage opt-in channels per user and prevent email fatigue through sending limits.

---

## 6. Data Models & Schema Architecture

dittofeed's relational model in `packages/backend-lib/prisma/schema.prisma` coordinates with ClickHouse tables:

```mermaid
erDiagram
    Workspace ||--o{ Segment : "defines"
    Workspace ||--o{ Journey : "executes"
    Workspace ||--o{ MessageTemplate : "maintains"
    Workspace ||--o{ UserProperty : "tracks"
    Journey ||--o{ JourneyDraft : "versions"

    Workspace {
        uuid id PK
        string name
        string type
    }

    Segment {
        uuid id PK
        uuid workspaceId FK
        string name
        jsonb definition
    }

    Journey {
        uuid id PK
        uuid workspaceId FK
        string name
        string status
        jsonb definition
    }

    MessageTemplate {
        uuid id PK
        uuid workspaceId FK
        string name
        string type
        jsonb definition
    }
```

---

### 6.1 Comprehensive Database Schema Specification

Dittofeed uses PostgreSQL for operational configurations and ClickHouse for high-scale event processing.

#### Table: `journeys`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Journey ID |
| `workspace_id` | `UUID` | `NOT NULL` | - | Workspace isolation |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Campaign journey name |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'Draft'` | State (`Draft`, `Running`, `Paused`) |
| `definition` | `JSONB` | `NOT NULL` | `'{}'` | Visual flow nodes & edges |

*Indexes:* `idx_journeys_workspace` (`(workspace_id)`).

---

#### Table: `segments`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Segment ID |
| `workspace_id` | `UUID` | `NOT NULL` | - | Workspace ID |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Segment title |
| `definition` | `JSONB` | `NOT NULL` | `'{}'` | Behavioral filter criteria |

*Indexes:* `idx_segments_workspace` (`(workspace_id)`).

---

## 7. API Architecture & Key Endpoints

dittofeed exposes Segment-compatible event tracking APIs:

- `POST /api/public/apps/identify`: Associate user attributes and traits:
  ```json
  {
    "userId": "usr_9918",
    "traits": {
      "email": "sarah@techcorp.io",
      "plan": "growth",
      "company": "TechCorp"
    }
  }
  ```
- `POST /api/public/apps/track`: Ingest behavioral events:
  ```json
  {
    "userId": "usr_9918",
    "event": "Project Created",
    "properties": { "template": "nextjs", "teamSize": 4 }
  }
  ```

---

### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor User as App User
    participant Ingest as Dittofeed Event Ingestion
    participant CH as ClickHouse Event Store
    participant Temporal as Temporal Workflow Engine
    participant Runner as JourneyNodeRunner
    actor Channel as Email / Push Provider

    User->>Ingest: POST /api/events (event: 'Signed Up', userId: 'u_10')
    Ingest->>CH: INSERT into user_events
    Ingest->>Temporal: Signal Journey Workflow for 'u_10'
    Temporal->>Runner: Execute Journey Step: Evaluate "New User Welcome"
    Runner->>CH: Query Segment Membership (Is user in 'Activated' segment?)
    CH-->>Runner: Segment condition met (TRUE)
    Runner->>Channel: Dispatch Welcome Email via Resend/SendGrid
    Channel-->>Runner: 200 OK
    Runner->>Temporal: Complete workflow step & schedule follow-up delay (3 days)
```

---

## 8. Strategic Value & Takeaways for MarketingStudio

### Reusable Architectural Patterns
1. **Temporal for Resilient Journeys**: Using Temporal workflows ensures that complex multi-week marketing automations never lose state or double-send if a pod restarts.
2. **Dual-Store Architecture (Postgres + ClickHouse)**: Keep relational metadata in PostgreSQL while streaming millions of tracking events into ClickHouse for instantaneous segment evaluation.
3. **Segment-Compatible Ingestion Schema**: Support `identify` and `track` payloads so MarketingStudio can drop into any application with zero code rewrite.
