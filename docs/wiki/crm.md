# crm (Frappe CRM) - Architecture & Technical Wiki

> **Modern Open-Source CRM Engineered on the Frappe Framework, Python & Reactive Vue 3**  
> GitHub: [frappe/crm](https://github.com/frappe/crm.git) | Stack: Python 3.10+, Frappe Framework, MariaDB 10.6+, Redis, Celery / RQ, Vue 3.5, Vite 8, TailwindCSS | License: AGPL-3.0

---

## 1. Executive Summary & Value Proposition

**crm** (commercially known as **Frappe CRM**) is a high-performance, modern open-source CRM developed by Frappe Technologies. Engineered to replace bloated, sluggish legacy CRM platforms, Frappe CRM couples the extensible Python-based Frappe Framework with a snappy, reactive Vue 3 Single-Page Application (`frappe-ui`). By treating customer interactions as a unified, real-time omnichannel timeline, Frappe CRM natively brings together email threads, WhatsApp messages, Twilio/Exotel VoIP telephony call recordings, internal notes, and SLA response tracking into a single view.

### Core Problems Solved
- **Sluggish Enterprise UI/UX**: Overcomes the friction of traditional multi-page enterprise software through an instantaneous, keyboard-friendly Vue 3 interface powered by Pinia and TailwindCSS.
- **SLA & Response-Time Breaches**: Implements built-in Service Level Agreements (SLAs) with automated first-response and rolling-response timers, preventing sales leads from going cold.
- **Fragmented Communication Silos**: Consolidates emails, WhatsApp chats, live phone calls, SMS, and team comments into a chronologically indexed activity stream.
- **Complex Schema Extensibility**: Leverages Frappe's declarative JSON DocType metadata system, allowing organizations to create custom fields, automated assignment rules, and territory geofences without modifying core code.
- **Omnichannel Telephony & Messaging**: Integrates browser-native VoIP calling via the Twilio Voice SDK and Exotel alongside bi-directional WhatsApp business messaging.

---

## 2. High-Level System Architecture

Frappe CRM is structured around a decoupled architecture where the Python Frappe backend manages metadata schemas (DocTypes), role-based permissions, database queries, and async queues, while the frontend is an independent Vite-bundled Vue 3 application interacting via REST APIs and Socket.io WebSockets.

```mermaid
flowchart TD
    subgraph ClientTier ["Frontend Client (Vue 3 / frappe-ui)"]
        VueSPA["Frappe CRM SPA (Vite / Vue 3.5 / Pinia)"]
        KanbanBoard["Interactive Kanban Engine (vuedraggable / Sortable)"]
        DialerSoftphone["Browser Softphone (Twilio Voice SDK)"]
        TerritoryMap["Territory Map Engine (Leaflet / Leaflet-Draw)"]
    end

    subgraph ApiGateway ["Frappe Gateway & Web Server Tier"]
        NginxProxy["Nginx Reverse Proxy & Static Asset Server"]
        GunicornWSGI["Gunicorn WSGI Application Server (Python)"]
        SocketNode["Socket.io Realtime Daemon (Node.js)"]
    end

    subgraph FrappeCore ["Frappe Core Framework & CRM Engine"]
        DocTypeEngine["DocType ORM & Schema Engine"]
        AclEngine["Role & Permission Guard (System Manager, Sales User)"]
        WhitelistedAPIs["Frappe Whitelisted RPC API (/api/method/...)"]
        
        subgraph BusinessServices ["Domain Services (crm/api/)"]
            ActivityService["Unified Activity & Timeline Engine (activities.py)"]
            DocQueryService["Generic Document & Filter Engine (doc.py)"]
            FormLayoutEngine["Form Layout & Script Runtime (form.py)"]
            DashboardAnalytics["Sales Funnel & Metrics Engine (dashboard.py)"]
            WhatsAppService["WhatsApp Webhook & Message Processor (whatsapp.py)"]
            SlaSupervisor["SLA Escalation & Rolling Response Worker"]
        end
    end

    subgraph AsyncAndCache ["Async Queues & State"]
        RedisQueue[(Redis - Celery / RQ Job Queue)]
        RedisCache[(Redis - Document Cache & Socket.io PubSub)]
        WorkerPool["Background Worker Processes (frappe.tasks)"]
    end

    subgraph DatabaseTier ["Persistence Layer"]
        MariaDB[(MariaDB 10.6+ InnoDB Database)]
        FileStore["Public / Private File Attachments (/sites/...)"]
    end

    VueSPA -->|HTTP REST / Whitelist RPC| NginxProxy
    KanbanBoard -->|Stage Mutations / Bulk Reorder| NginxProxy
    DialerSoftphone -->|WebRTC Audio Stream| GunicornWSGI
    VueSPA <-->|Realtime Socket Events| SocketNode

    NginxProxy --> GunicornWSGI
    GunicornWSGI --> WhitelistedAPIs
    WhitelistedAPIs --> AclEngine
    AclEngine --> DocTypeEngine

    DocTypeEngine --> BusinessServices
    BusinessServices --> DocTypeEngine
    DocTypeEngine --> MariaDB
    BusinessServices --> FileStore

    BusinessServices --> RedisCache
    BusinessServices --> RedisQueue
    RedisQueue --> WorkerPool
    WorkerPool --> SlaSupervisor
    WorkerPool --> MariaDB
    WorkerPool --> SocketNode
```

---

### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class CRMLead {
        +String name
        +String lead_name
        +String email_id
        +String mobile_no
        +String status
        +String territory
        +validate() Void
        +on_update() Void
        +create_deal() CRMDeal
    }

    class CRMDeal {
        +String name
        +String deal_name
        +Float deal_value
        +String status
        +String pipeline_stage
        +Date expected_closure_date
        +transition_stage(new_stage) Void
    }

    class CRMOrganization {
        +String name
        +String organization_name
        +String website
        +Int annual_revenue
        +get_contacts() List~CRMContact~
    }

    class CRMContact {
        +String name
        +String first_name
        +String last_name
        +String email
        +String organization
    }

    class ActivityManager {
        +log_activity(doc_type, doc_name, activity_type) Void
        +calculate_sla_status(lead_name) String
    }

    class PipelineController {
        +get_kanban_data(pipeline_id) JSON
        +reorder_deals(deal_id, new_position) Void
    }

    CRMLead --> CRMDeal : converts_to
    CRMOrganization "1" *-- "many" CRMContact : employs
    CRMOrganization "1" *-- "many" CRMDeal : closes
    ActivityManager ..> CRMLead : monitors_sla
    PipelineController --> CRMDeal : controls_stages
```

---

## 3. Technology Stack & Key Dependencies

- **Backend Language & Runtime**: Python 3.10+ (type hints, dataclasses, asynchronous workers)
- **Backend Framework**: Frappe Framework v15+ (metaprogramming, DocType ORM, hook system, background tasks)
- **Database & Storage**: MariaDB 10.6+ (InnoDB with UTF8MB4), Redis (caching, session store, Socket.io PubSub, RQ queues)
- **Asynchronous Task Processing**: Python RQ (Redis Queue) and Celery workers scheduled via `frappe.tasks` hooks
- **Real-Time Layer**: Socket.io (Node.js daemon) integrated via Frappe PubSub hooks for real-time document change broadcasts
- **Frontend Architecture**:
  - Framework: Vue 3.5+ (Composition API, `<script setup>`), Vite 8
  - UI Library: `frappe-ui` (1.0.0-rc.2), Headless UI (`@headlessui/vue`), TailwindCSS
  - State Management: Pinia 2.0+
  - Rich Text & Editors: TipTap 3.26 (`@tiptap/core`, `@tiptap/extension-paragraph`)
  - Drag-and-Drop: `vuedraggable` 4.1, `sortablejs` 1.15
  - Telephony & Audio: `@twilio/voice-sdk` 2.10
  - GIS & Mapping: `leaflet` 1.9, `leaflet-draw` 1.0 (territory polygon boundaries)

---

## 4. Repository Layout & Key Modules

```
crm/
├── crm/                              # Python backend application package
│   ├── api/                          # Whitelisted HTTP endpoints and business controllers
│   │   ├── activities.py             # Communication timeline, notes, call logs, WhatsApp, emails
│   │   ├── doc.py                    # Filterable query builder, bulk operations, linked documents
│   │   ├── form.py                   # Dynamic layouts, custom fields, form scripting hooks
│   │   ├── dashboard.py              # Sales analytics, pipeline conversion, SLA breach metrics
│   │   ├── whatsapp.py               # WhatsApp Cloud API and Twilio WhatsApp inbound/outbound
│   │   └── assignment_rule.py        # Automated round-robin lead distribution logic
│   ├── fcrm/                         # Core CRM module declarations
│   │   └── doctype/                  # JSON schema and Python controller classes
│   │       ├── crm_lead/             # CRM Lead (qualification, scoring, SLA timers)
│   │       ├── crm_deal/             # CRM Deal (sales pipeline stages, currency, products)
│   │       ├── crm_organization/     # B2B Companies / Accounts
│   │       ├── crm_contacts/         # People / Contacts linked to organizations
│   │       ├── crm_task/             # Follow-up tasks and calendar events
│   │       ├── fcrm_note/            # Internal collaborative notes with TipTap JSON
│   │       ├── crm_call_log/         # Inbound/outbound telephony call logs and recordings
│   │       └── crm_service_level_agreement/ # SLA definition, response targets, holidays
│   ├── hooks.py                      # Frappe event hooks, scheduled tasks, and app routing
│   ├── install.py                    # Database seeding, default pipelines, and initial roles
│   └── overrides/                    # Overrides for Frappe core DocTypes (User, Communication)
├── frontend/                         # Modern Vue 3 SPA frontend
│   ├── src/
│   │   ├── components/               # UI components (Kanban, Omnichannel Timeline, Lead Modal)
│   │   ├── views/                    # Top-level route views (Leads, Deals, Organizations, Settings)
│   │   ├── stores/                   # Pinia state stores (lead.js, deal.js, call.js)
│   │   └── utils/                    # Formatters, telephony handlers, and socket listeners
│   ├── package.json                  # Frontend dependencies and Vite configuration
│   └── vite.config.js                # Vite build config with base assets pathing
└── pyproject.toml                    # Python project configuration and package dependencies
```

---

## 5. Core Features & Capabilities

### 5.1 Dynamic Field Creation via Frappe DocType Engine
In Frappe CRM, entities are defined as JSON schemas within `crm/fcrm/doctype/`. Custom attributes can be provisioned through the administrative UI without requiring code deployments. The `crm_fields_layout` and `crm_form_script` DocTypes allow administrators to:
- Define dynamic fields (`Data`, `Select`, `Currency`, `Link`, `Check`, `Geolocation`).
- Insert custom client-side validation scripts that execute within the Vue form lifecycle.
- Set conditional display rules (e.g., reveal `Lost Reason` dropdown only when status transitions to `Lost`).

### 5.2 Interactive Sales Pipelines & Kanban Drag-and-Drop
Deals are organized into visual pipelines with customizable stages:
- The Vue frontend utilizes `vuedraggable` and `sortablejs` to enable smooth card reordering.
- Stage transitions trigger automated backend recalculations via `crm/api/doc.py`: updating probability weights, recalculating total stage value, and logging status changes in `crm_status_change_log`.
- Built-in multi-currency conversion dynamically computes deal amounts based on live exchange rates.

### 5.3 Automated Lead SLA & Rolling Response Tracking
Frappe CRM solves lead neglect through algorithmic SLA enforcement:
- **First Response Time**: Measures the duration from lead creation to the first outbound email, call, or WhatsApp message.
- **Rolling Response Time**: Tracks ongoing responsiveness (`crm_rolling_response_time`), checking whether inbound inquiries receive a reply within designated business hours.
- **Automated Escalation**: When an SLA timer expires, the `crm_service_level_agreement` daemon flags the lead as `SLA Failed` and notifies the sales manager via Socket.io.

### 5.4 Omnichannel Customer Timeline (`activities.py`)
Rather than isolating communications into disparate tabs, Frappe CRM computes a unified timeline feed by joining:
- Inbound/outbound emails synced from connected IMAP/SMTP accounts.
- WhatsApp chat messages received via webhooks (`whatsapp.py`).
- VoIP call recordings with in-browser audio players (`crm_call_log`).
- Internal team mentions and TipTap collaborative rich-text notes (`fcrm_note`).

### 5.5 Geographic Sales Territory Management
Using Leaflet and Leaflet Draw (`leaflet-draw`), Frappe CRM enables sales managers to draw spatial polygon boundaries on a map. When a lead or deal is created with geographic coordinates, spatial point-in-polygon queries automatically assign the record to the appropriate territorial sales team.

---

## 6. Data Models & Schema Architecture

Frappe CRM models relationships using Frappe's declarative `Link` and `Table` (child doctype) architecture.

```mermaid
erDiagram
    CRM_ORGANIZATION ||--o{ CRM_CONTACTS : "employs"
    CRM_ORGANIZATION ||--o{ CRM_DEAL : "associated with"
    CRM_LEAD ||--o| CRM_ORGANIZATION : "converted to"
    CRM_LEAD ||--o| CRM_CONTACTS : "converted to"
    CRM_LEAD ||--o| CRM_DEAL : "converted to"
    
    CRM_DEAL ||--o{ CRM_TASK : "generates"
    CRM_LEAD ||--o{ CRM_TASK : "requires"
    
    CRM_LEAD ||--o{ CRM_CALL_LOG : "logged against"
    CRM_DEAL ||--o{ CRM_CALL_LOG : "logged against"
    
    CRM_LEAD ||--o{ FCRM_NOTE : "annotated with"
    CRM_DEAL ||--o{ FCRM_NOTE : "annotated with"

    CRM_LEAD {
        string name PK "Autoname series (e.g. LEAD-0001)"
        string salutation
        string first_name
        string last_name
        string email
        string mobile_no
        string organization FK
        string website
        string territory
        string industry
        string job_title
        string source
        string lead_owner FK
        string status "Open, Contacted, Qualified, Converted, Lost"
        boolean converted
        int lead_score
        string lead_temperature "Hot, Warm, Cold"
        decimal total
        decimal net_total
        string sla FK
        string sla_status "Pending, First Response Due, Fulfilled, Failed"
        datetime first_responded_on
        int first_response_time
        datetime creation
        datetime modified
    }

    CRM_ORGANIZATION {
        string name PK "Organization ID"
        string organization_name
        string website
        string territory
        string industry
        decimal annual_revenue
        int no_of_employees
        string organization_logo
        datetime creation
    }

    CRM_CONTACTS {
        string name PK "Contact ID"
        string organization FK
        string first_name
        string last_name
        string email
        string mobile_no
        string phone
        string job_title
        string gender
        datetime creation
    }

    CRM_DEAL {
        string name PK "Autoname series (e.g. DEAL-0001)"
        string organization FK
        string contact FK
        string lead FK
        string status FK "Deal Stage (Prospect, Discovery, Proposal, Won, Lost)"
        string deal_owner FK
        int probability
        decimal deal_value
        decimal expected_deal_value
        string currency
        decimal exchange_rate
        date expected_closure_date
        datetime closed_date
        datetime creation
    }

    CRM_TASK {
        string name PK "Task ID"
        string title
        text description
        string status "Backlog, Todo, In Progress, Done, Cancelled"
        string priority "Low, Medium, High, Urgent"
        datetime due_date
        string assigned_to FK
        string reference_doctype
        string reference_docname
        datetime creation
    }

    FCRM_NOTE {
        string name PK "Note ID"
        string title
        longtext content "TipTap JSON / HTML content"
        string reference_doctype
        string reference_docname
        string added_by FK
        datetime creation
    }

    CRM_CALL_LOG {
        string name PK "Call ID"
        string call_type "Inbound, Outbound"
        string from_number
        string to_number
        string status "Completed, Missed, Busy, Failed"
        int duration "Duration in seconds"
        string recording_url
        string telephony_provider "Twilio, Exotel"
        string reference_doctype
        string reference_docname
        datetime creation
    }
```

### Core Schema Entity Details
1. **CRM Lead (`crm/fcrm/doctype/crm_lead/crm_lead.json`)**:
   - Primary key generated via `naming_series` (e.g. `CRM-LEAD-.YYYY.-.#####`).
   - Tracks engagement metrics: `lead_score`, `lead_temperature`, and `rolling_responses`.
   - Links directly to SLA management via `sla` and `sla_status`.
2. **CRM Deal (`crm/fcrm/doctype/crm_deal/crm_deal.json`)**:
   - `deal_value` and `expected_deal_value` (computed based on probability percentage).
   - Relational links to `organization`, `contact`, and source `lead`.
3. **Communication & Activity Logging**:
   - Polymorphic linking via `reference_doctype` (`CRM Lead`, `CRM Deal`, `CRM Organization`) and `reference_docname`.

---

### 6.1 Comprehensive Database Schema Specification

Frappe CRM persists data within MariaDB / PostgreSQL tables prefixed with `tab`, implementing document-level schema definitions via DocTypes.

#### Table: `tabCRM Lead`
Represents inbound prospective leads undergoing evaluation and SLA qualification.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `name` | `VARCHAR(140)` | `PRIMARY KEY` | - | Unique Frappe document ID (e.g. `LEAD-2026-0001`) |
| `lead_name` | `VARCHAR(140)` | `NOT NULL` | - | Prospect full contact name |
| `email_id` | `VARCHAR(140)` | `NULLABLE` | `NULL` | Prospect business email |
| `mobile_no` | `VARCHAR(50)` | `NULLABLE` | `NULL` | Primary phone number |
| `company_name` | `VARCHAR(140)` | `NULLABLE` | `NULL` | Prospective employer company |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'Open'` | State (`Open`, `Contacted`, `Qualified`, `Lost`) |
| `territory` | `VARCHAR(140)` | `NULLABLE` | `NULL` | Sales territory region |
| `sla_status` | `VARCHAR(50)` | `NOT NULL` | `'Within SLA'`| Response SLA indicator (`Within SLA`, `Breached`) |
| `creation` | `DATETIME(6)` | `NOT NULL` | `NOW()` | Record creation timestamp |
| `modified` | `DATETIME(6)` | `NOT NULL` | `NOW()` | Document update timestamp |

*Indexes:* `idx_lead_email` (`(email_id)`), `idx_lead_status` (`(status)`).

---

#### Table: `tabCRM Deal`
Commercial opportunities progressing through defined pipeline stages.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `name` | `VARCHAR(140)` | `PRIMARY KEY` | - | Unique deal ID (e.g. `DEAL-2026-0001`) |
| `deal_name` | `VARCHAR(140)` | `NOT NULL` | - | Commercial deal title |
| `deal_value` | `DECIMAL(18, 6)`| `NOT NULL` | `0.000000`| Contract value |
| `currency` | `VARCHAR(10)` | `NOT NULL` | `'USD'` | Transaction currency code |
| `pipeline_stage`| `VARCHAR(140)` | `NOT NULL` | `'Prospecting'`| Active Kanban column stage |
| `organization` | `VARCHAR(140)` | `NULLABLE` | `NULL` | Target organization document link |
| `expected_closure_date`| `DATE` | `NULLABLE` | `NULL` | Forecast close target date |
| `probability` | `DECIMAL(5, 2)` | `NOT NULL` | `0.00` | Percentage win probability |

*Indexes:* `idx_deal_stage` (`(pipeline_stage)`), `idx_deal_org` (`(organization)`).

---

## 7. API Architecture, Webhooks & Key Endpoints

Frappe CRM utilizes Frappe’s Remote Procedure Call (RPC) mechanism (`@frappe.whitelist()`). Endpoints are accessed via `/api/method/<python_module_path>` and accept session cookies, API Secret headers (`Authorization: token <api_key>:<api_secret>`), or Bearer tokens.

### Key Endpoints

| HTTP Method | Endpoint | Description |
|:---|:---|:---|
| `POST` | `/api/method/crm.api.doc.get_data` | Filter and paginate CRM records with sorting, field projections, and quick filters |
| `POST` | `/api/method/frappe.client.insert` | Create a new record (DocType instance) |
| `POST` | `/api/method/frappe.client.set_value` | Update specific field values on an existing record |
| `GET` | `/api/method/crm.api.activities.get_activities` | Fetch chronological communication timeline for a record |
| `POST` | `/api/method/crm.api.activities.add_note` | Add a collaborative note to a lead or deal |
| `POST` | `/api/method/crm.api.whatsapp.send_message` | Send outbound WhatsApp template or freeform chat |
| `GET` | `/api/method/crm.api.dashboard.get_deal_funnel` | Retrieve aggregate conversion metrics for the sales funnel |
| `POST` | `/api/method/crm.api.doc.delete_bulk_docs` | Bulk delete records with cascading link cleanups |

### Sample Request: Query Leads (`POST /api/method/crm.api.doc.get_data`)
```json
{
  "doctype": "CRM Lead",
  "fields": [
    "name",
    "first_name",
    "last_name",
    "email",
    "organization",
    "status",
    "lead_score",
    "sla_status"
  ],
  "filters": [
    ["CRM Lead", "status", "!=", "Lost"],
    ["CRM Lead", "lead_score", ">=", 50]
  ],
  "order_by": "modified desc",
  "page_length": 20
}
```

### Sample Response: Query Leads
```json
{
  "message": {
    "rows": [
      {
        "name": "CRM-LEAD-2026-00142",
        "first_name": "Marcus",
        "last_name": "Vance",
        "email": "m.vance@apexcloud.io",
        "organization": "Apex Cloud Infrastructure",
        "status": "Qualified",
        "lead_score": 85,
        "sla_status": "Fulfilled",
        "_activity_counts": {
          "calls": 2,
          "emails": 4,
          "notes": 1
        }
      }
    ],
    "total_count": 1
  }
}
```

---

### 7.5 End-to-End Operational Lifecycle Sequence

The following sequence illustrates how Frappe REST endpoints process lead submissions, execute DocType lifecycle hooks, enforce SLA timing, and broadcast updates over Socket.io.

```mermaid
sequenceDiagram
    autonumber
    actor WebForm as External Web Form
    participant FrappeAPI as Frappe REST Gateway
    participant DocType as CRM Lead DocType
    participant Activity as ActivityManager
    participant DB as MariaDB / PostgreSQL
    participant Socket as Frappe Socket.io Server
    actor Agent as Sales Representative

    WebForm->>FrappeAPI: POST /api/method/crm.api.doc.create_doc (Lead Data)
    FrappeAPI->>DocType: Instantiate new Lead Document
    DocType->>DocType: Run validate() hook (sanitize & verify email)
    DocType->>DB: INSERT into `tabCRM Lead` (name, lead_name, email_id, status='Open')
    DB-->>DocType: Record committed
    
    DocType->>Activity: log_activity('Lead', lead_id, 'Created via Web Form')
    Activity->>DB: INSERT into `tabCRM Activity`
    
    DocType->>Socket: Emit `crm:lead_created` event
    Socket-->>Agent: Real-time UI toast notification on dashboard
    
    Agent->>FrappeAPI: POST /api/method/crm.api.doc.update (Transition to Qualified)
    FrappeAPI->>DocType: Update pipeline_stage and SLA status
    DocType->>DB: UPDATE `tabCRM Lead` SET status='Qualified'
    DocType-->>FrappeAPI: 200 OK (Updated Document)
```

---

## 8. Strategic Value & Takeaways for MarketingStudio

### Reusable Architectural Patterns
1. **Algorithmic SLA & Response Time Engine**:
   - Frappe CRM demonstrates how to operationalize lead responsiveness. Instead of simply collecting leads, MarketingStudio should incorporate first-response timers and rolling SLA checkers (`first_response_time`, `sla_status`). If an inbound marketing inquiry is not claimed within a defined threshold (e.g. 15 minutes), the system automatically triggers an escalation webhook or reassigns the lead to an on-call SDR.
2. **Aggregated Multi-Channel Activity Feed**:
   - The unified timeline implementation in `activities.py` is an ideal architectural pattern for MarketingStudio. Rather than storing communications in isolated tables with independent query patterns, MarketingStudio should aggregate email events, WhatsApp messages, telephony recordings, form submissions, and marketing page views into a unified event projection.
3. **Decoupled Vue 3 + Pinia Single-Page Application**:
   - By breaking away from traditional server-rendered templates and implementing a decoupled Vite + Vue 3 architecture (`frappe-ui`), Frappe CRM achieves desktop-class responsiveness. MarketingStudio should leverage an optimistic client-side store pattern for its pipeline Kanban boards, ensuring cards move instantly while background syncing updates the database.
4. **Geographic Territory Boundaries via GeoJSON**:
   - The integration of Leaflet and spatial polygon coordinates directly inside the CRM gives sales teams geographic clarity. MarketingStudio can use this pattern to route marketing leads based on IP geolocation or postal code polygons directly to regional field sales teams.
