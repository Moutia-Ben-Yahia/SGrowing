# ever-gauzy (Ever Gauzy Platform) - Architecture & Technical Wiki

> **Open Business Management Platform for Collaborative, On-Demand and Sharing Economies (ERP / CRM / HRM / ATS / PM)**  
> GitHub: [ever-co/ever-gauzy](https://github.com/ever-co/ever-gauzy.git) | Stack: TypeScript, NestJS, TypeORM / MikroORM / Prisma, PostgreSQL / SQLite, Redis, BullMQ, Angular 18+, Electron, Docker, Nx Monorepo | License: AGPL-3.0

---

## 1. Executive Summary & Value Proposition

**ever-gauzy** (developed by Ever Co.) is an enterprise-grade, open-source business management suite unifying Enterprise Resource Planning (ERP), Customer Relationship Management (CRM), Human Resources Management (HRM), Applicant Tracking Systems (ATS), Project Management (PM), and Employee Productivity & Time Tracking. 

Unlike single-purpose tools that create data silos between project delivery, employee hours, client billing, and revenue accounting, Ever Gauzy coordinates the complete operational lifecycle from employee onboarding and desktop activity logging to client project milestones, invoice generation, and financial accounting.

### Core Problems Solved
- **Fragmented ERP / HRM / CRM Toolchains**: Eliminates the operational overhead of synchronizing separate CRM tools, project trackers (Jira/Asana), timesheet recorders, and billing accounting platforms.
- **Automated Time & Desktop Activity Auditing**: Bundles desktop time-tracking agents (Windows/macOS/Linux Electron) capturing keyboard/mouse activity metrics, application tracking, and automated blurred screenshots.
- **Strict Multi-Tenant & Multi-Organization Scoping**: Built from the ground up with multi-tenant tenancy guarantees, allowing holding companies to manage multiple subsidiary organizations, branches, and cross-company employees.
- **Headless API-First Architecture**: Exposes comprehensive REST and GraphQL APIs enabling external client frontends (such as [Ever Teams](ever-teams.md)) and mobile apps to interact directly with core business logic.
- **Extensible Plugin Ecosystem**: Features an isolated plugin composition engine allowing dynamic loading of custom domain features without modifying the core codebase.

---

## 2. High-Level System Architecture

Ever Gauzy is architected as an Nx monorepo featuring a modular NestJS API core (`apps/api`), dedicated background workers (`apps/worker`), Model Context Protocol integrations (`apps/mcp`), an Angular enterprise dashboard (`apps/gauzy`), and desktop productivity apps (`apps/desktop`).

```mermaid
flowchart TD
    subgraph ClientTier ["Frontend & Desktop Client Tier"]
        AngularWeb["Angular Enterprise Dashboard (apps/gauzy)"]
        DesktopApp["Electron Desktop App (apps/desktop)"]
        DesktopTimer["Activity & Screenshot Tracker (desktop-timer)"]
        ExternalClients["Ever Teams / Mobile Clients / Headless APIs"]
    end

    subgraph IngressGateway ["API Gateway & Middleware Layer"]
        ReverseProxy["Nginx / Cloud Ingress Proxy"]
        NestGateway["NestJS API Server (apps/api :3000)"]
        AuthGuard["JWT / OAuth2 / API Key Guards"]
        TenantInterceptor["Tenant & Organization Context Scoper"]
        MCPGateway["Model Context Protocol Gateway (apps/mcp)"]
    end

    subgraph GauzyCoreModules ["Core Domain Modules (packages/core/src/lib/*)"]
        EmployeeModule["Employee & Team Management"]
        TimeTrackModule["Time Tracking & Timesheet Engine"]
        TaskProjectModule["Projects, Milestones & Kanban Tasks"]
        CrmModule["CRM Contacts, Leads & Deals"]
        BillingModule["Invoices, Estimates & Accounting"]
        AtsModule["ATS Candidates, Job Posts & Interviews"]
        PluginSystem["Dynamic Plugin Composition Engine"]
    end

    subgraph AsyncAndQueues ["Async Processing Tier"]
        RedisBus[(Redis - PubSub & Event Broker :6379)]
        BullQueue["BullMQ Worker Cluster (apps/worker)"]
        ScheduledJobs["Cron & Timesheet Aggregation Daemons"]
    end

    subgraph PersistenceTier ["Data & Storage Layer"]
        RelationalDB[(PostgreSQL 14+ / SQLite / MySQL Store)]
        ObjectStorage["S3 / MinIO (Screenshots, PDFs, Contracts)"]
    end

    AngularWeb -->|HTTP REST / WebSocket| ReverseProxy
    DesktopApp -->|Activity Telemetry & Auth| ReverseProxy
    DesktopTimer -->|Periodic Screenshot Sync| ReverseProxy
    ExternalClients -->|Headless API Calls| ReverseProxy

    ReverseProxy --> NestGateway
    NestGateway --> AuthGuard
    AuthGuard --> TenantInterceptor
    TenantInterceptor --> GauzyCoreModules

    MCPGateway -->|AI Agent Tool Calls| TenantInterceptor

    GauzyCoreModules --> RelationalDB
    GauzyCoreModules --> ObjectStorage
    GauzyCoreModules --> RedisBus

    RedisBus --> BullQueue
    BullQueue --> ScheduledJobs
    ScheduledJobs --> RelationalDB
```

---

### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class Tenant {
        +UUID id
        +String name
        +List~Organization~ organizations
        +List~User~ users
    }

    class Organization {
        +UUID id
        +UUID tenantId
        +String name
        +String currency
        +List~Employee~ employees
        +List~Project~ projects
        +List~Invoice~ invoices
    }

    class Employee {
        +UUID id
        +UUID userId
        +UUID organizationId
        +String jobTitle
        +Float billRate
        +Float payRate
        +List~TimeLog~ timeLogs
        +logTime(taskId, duration) TimeLog
    }

    class Project {
        +UUID id
        +UUID organizationId
        +String name
        +String billingType
        +List~Task~ tasks
        +calculateBudgetBurn() Float
    }

    class Task {
        +UUID id
        +UUID projectId
        +String title
        +String status
        +Int estimateHours
        +List~TimeLog~ timeLogs
    }

    class TimeLog {
        +UUID id
        +UUID employeeId
        +UUID taskId
        +DateTime startedAt
        +DateTime stoppedAt
        +Int durationSeconds
        +Boolean isBillable
        +List~TimeSlot~ timeSlots
    }

    class Invoice {
        +UUID id
        +UUID organizationId
        +UUID contactId
        +String invoiceNumber
        +Float totalAmount
        +String status
        +generateFromTimesheets(timeLogIds) Void
    }

    Tenant "1" *-- "many" Organization : owns
    Organization "1" *-- "many" Employee : employs
    Organization "1" *-- "many" Project : executes
    Organization "1" *-- "many" Invoice : issues
    Project "1" *-- "many" Task : breaks_into
    Employee "1" *-- "many" TimeLog : records
    Task "1" *-- "many" TimeLog : tracks
    Invoice ..> TimeLog : aggregates
```

---

## 3. Technology Stack & Key Dependencies

- **Monorepo Architecture**: Nx Monorepo (`nx.json`, `lerna.json`), Yarn Workspaces
- **Backend Language & Runtime**: TypeScript 5+, Node.js 20+
- **Application Framework**: NestJS 10+ (`@nestjs/core`, `@nestjs/common`, `@nestjs/swagger`, `@nestjs/typeorm`, `@nestjs/bullmq`)
- **Database ORM**: Dual abstraction with TypeORM and MikroORM support across PostgreSQL 14+, MySQL, and SQLite (WAL mode)
- **Queue & Async Processing**: BullMQ backed by Redis for worker background processing (`apps/worker`)
- **Frontend Architecture**: Angular 18+ Single Page Application with `@nebular/theme` and RxJS state streams
- **Desktop Application**: Electron 28+ wrapper with native OS hooks for idle detection, window title capture, and screencasting
- **AI & Integrations**: Model Context Protocol (MCP) server endpoints (`apps/mcp`, `apps/server-mcp`) enabling LLMs to execute Gauzy operations
- **Storage & Documents**: S3-compatible object storage (MinIO/AWS S3) and PDF generation engines (PDFKit)

---

## 4. Repository Layout & Key Modules

```
ever-gauzy/
├── apps/
│   ├── api/                           # Core NestJS HTTP & WebSocket REST API server
│   │   └── src/                       # Application entry point, plugins, migrations, and seeds
│   ├── gauzy/                         # Angular enterprise web application
│   │   └── src/app/                   # Modules: Dashboard, Employees, Tasks, Invoices, CRM, ATS
│   ├── worker/                        # Background BullMQ worker for timesheet/report aggregation
│   ├── desktop/                       # Electron desktop client shell
│   ├── desktop-timer/                 # Native tray timer widget for activity recording
│   ├── mcp/                           # Model Context Protocol server for AI agent workflows
│   └── server/                        # Bundled backend and static asset serving runner
├── packages/
│   ├── core/                          # Domain business logic (70+ feature modules in src/lib/)
│   │   └── src/lib/
│   │       ├── employee/              # Employee records, skills, departments, and payroll rates
│   │       ├── time-tracking/         # Activity logs, timeslots, screenshot capture, and timer
│   │       ├── tasks/                 # Task creation, priority, kanban transitions, and estimates
│   │       ├── invoice/               # Invoicing, line items, recurring billing, and PDF export
│   │       ├── contact/               # CRM client contacts, accounts, and vendor entities
│   │       ├── candidate/             # ATS candidate recruitment funnel and interview scheduling
│   │       └── tenant/                # Multi-tenancy isolation and tenant API key management
│   ├── contracts/                     # Shared TypeScript interfaces, DTOs, and API payloads
│   ├── auth/                          # Passport.js strategies, JWT handling, and RBAC guards
│   ├── plugin/                        # Plugin definition, lifecycle hooks, and extension API
│   ├── ui-core/                       # Reusable UI component libraries
│   └── utils/                         # Date helpers, math utilities, and currency formatters
├── docker-compose.yml                 # Local development container orchestration
├── nx.json                            # Nx workspace build targets and cache rules
└── package.json                       # Monorepo dependencies and CLI scripts
```

---

## 5. Core Features & Capabilities

### 5.1 Comprehensive Multi-Tenancy & Data Isolation
Gauzy enforces multi-tier hierarchy:
- **Tenant Level**: The root boundary (e.g., enterprise holding company or SaaS account).
- **Organization Level**: Business units, subsidiaries, or franchise branches operating with distinct currencies, tax rules, and employee rosters.
- **Role-Based Access Control (RBAC)**: Fine-grained permissions controlling access to salary data, client billing, financial reports, and administrative settings.

### 5.2 Desktop Productivity & Proof-of-Work Telemetry
Designed for remote and hybrid teams, Gauzy includes native desktop productivity monitoring:
- **Active Time Logging**: Tracks start, stop, pause, and elapsed time against specific project tasks.
- **Activity Metrics**: Calculates keyboard activity percentages and mouse movement density per 10-minute timeslot (`time-tracking`).
- **Automated Screenshot Audits**: Periodically captures desktop screens, applying optional blur filters for privacy before uploading to S3 storage.

### 5.3 Integrated Time-to-Invoice Financial Pipeline
Timesheets seamlessly flow into accounts receivable:
1. Developers and contractors track hours on project tasks.
2. Managers approve weekly timesheets (`request-approval`).
3. The billing engine converts approved billable hours into itemized client invoices with tax lines, currency conversions, and payment links.

### 5.4 AI Agent Compatibility via MCP
Ever Gauzy natively packages a Model Context Protocol (MCP) server. AI agents (such as Claude Code, AutoGPT, or custom bots) can query employee availability, retrieve project sprint velocity, schedule interviews, and log hours autonomously.

---

## 6. Data Models & Schema Architecture

```mermaid
erDiagram
    TENANTS ||--o{ ORGANIZATIONS : "contains"
    ORGANIZATIONS ||--o{ EMPLOYEES : "employs"
    ORGANIZATIONS ||--o{ PROJECTS : "funds"
    ORGANIZATIONS ||--o{ INVOICES : "issues"
    
    PROJECTS ||--o{ TASKS : "contains"
    EMPLOYEES ||--o{ TIME_LOGS : "logs"
    TASKS ||--o{ TIME_LOGS : "charged to"

    TENANTS {
        uuid id PK
        string name
        boolean is_active
        timestamptz created_at
    }

    ORGANIZATIONS {
        uuid id PK
        uuid tenant_id FK
        string name
        string default_currency
        string tax_id
        timestamptz created_at
    }

    EMPLOYEES {
        uuid id PK
        uuid organization_id FK
        uuid user_id FK
        string job_title
        decimal bill_rate
        decimal pay_rate
        boolean is_active
    }

    PROJECTS {
        uuid id PK
        uuid organization_id FK
        string name
        string billing_type "Hourly, Fixed, NonBillable"
        decimal budget
        string status
    }

    TASKS {
        uuid id PK
        uuid project_id FK
        string title
        string status "Todo, InProgress, InReview, Done"
        int estimate_hours
        int priority
    }

    TIME_LOGS {
        uuid id PK
        uuid employee_id FK
        uuid task_id FK
        timestamptz started_at
        timestamptz stopped_at
        int duration_seconds
        boolean is_billable
        string source "Web, Desktop, Mobile"
    }

    INVOICES {
        uuid id PK
        uuid organization_id FK
        string invoice_number UK
        decimal total_amount
        string currency
        string status "Draft, Sent, Paid, Overdue"
        date due_date
    }
```

---

## 7. Operational Workflows & Sequence Diagrams

### 7.1 Desktop Time-Tracking & Activity Capture Lifecycle

```mermaid
sequenceDiagram
    autonumber
    actor Employee as Team Member
    participant Desktop as Desktop Timer App
    participant API as Gauzy NestJS API
    participant Storage as S3 Object Storage
    participant Redis as Redis Queue
    participant DB as PostgreSQL Database

    Employee->>Desktop: Click "Start Timer" on Task-402
    Desktop->>API: POST /api/v1/timesheet/timer/start (taskId)
    API->>DB: INSERT into time_logs (status: RUNNING)
    API-->>Desktop: Timer started (timeLogId: uuid)

    loop Every 10 Minutes (Activity Slice)
        Desktop->>Desktop: Calculate keypress % & mouse movement
        Desktop->>Desktop: Capture screen & apply privacy blur
        Desktop->>Storage: Upload screenshot image (PNG)
        Storage-->>Desktop: Asset URL
        Desktop->>API: POST /api/v1/time-tracking/time-slot (timeLogId, activityLevel, assetUrl)
        API->>DB: INSERT into time_slots
    end

    Employee->>Desktop: Click "Stop Timer"
    Desktop->>API: POST /api/v1/timesheet/timer/stop
    API->>DB: UPDATE time_logs (duration_seconds, stopped_at)
    API->>Redis: Enqueue timesheet.recalculate_burn (projectId)
    API-->>Desktop: Timer stopped (Duration: 3h 20m)
```

---

## 8. Strategic Value & Takeaways for MarketingStudio / SGrowing

### Reusable Architectural Patterns
1. **Tenant-Scoped Monorepo Structure**: The `@gauzy/core` package cleanly encapsulates multi-tenant business logic with strict isolation decorators, preventing cross-organization data leakage.
2. **Headless Time & Billing Engine**: The time-log aggregation and invoice calculation engine can be repurposed in MarketingStudio to track agency billable hours, client campaign budgets, and contractor remuneration.
3. **Model Context Protocol (MCP) Integration**: The pre-built MCP server in `apps/mcp` serves as an architectural blueprint for exposing internal MarketingStudio capabilities to external autonomous AI agents.
