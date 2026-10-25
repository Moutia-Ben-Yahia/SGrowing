import os
import re

def insert_domain2(filepath, class_diagram, db_schema, seq_diagram):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    # 1. Insert Class Diagram into Section 2
    if 'classDiagram' not in content:
        sec3_match = re.search(r'\n## 3\.\s+Technology Stack', content)
        if sec3_match:
            idx = sec3_match.start()
            content = content[:idx] + "\n" + class_diagram.strip() + "\n\n---\n" + content[idx:]
        else:
            print(f"Warning: Section 3 not found in {filepath}")

    # 2. Insert Detailed DB Schema into Section 6
    if 'Comprehensive Database Schema Specification' not in content:
        sec_next_match = re.search(r'\n## [78]\.\s+', content)
        if sec_next_match:
            idx = sec_next_match.start()
            content = content[:idx] + "\n" + db_schema.strip() + "\n\n---\n" + content[idx:]
        else:
            print(f"Warning: Next section not found in {filepath}")

    # 3. Insert Sequence Diagram into Section 7 (or at end of API section / before Strategic Value)
    if 'sequenceDiagram' not in content:
        sec_strat_match = re.search(r'\n## [78]\.\s+Strategic Value', content)
        if sec_strat_match:
            idx = sec_strat_match.start()
            content = content[:idx] + "\n" + seq_diagram.strip() + "\n\n---\n" + content[idx:]
        else:
            print(f"Warning: Strategic Value section not found in {filepath}")

    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(content)
    print(f"Updated {filepath} successfully!")

# 3. crm (Frappe CRM)
crm_cd = """
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
"""

crm_db = """
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
"""

crm_seq = """
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
"""

# 4. laravel-crm
laravel_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class Lead {
        +BigInt id
        +String title
        +String description
        +Int lead_status_id
        +BigInt person_id
        +BigInt organization_id
        +validate() Boolean
        +convertToDeal() Deal
    }

    class Deal {
        +BigInt id
        +String title
        +Decimal amount
        +String currency
        +Int deal_stage_id
        +Date close_date
        +recalculateForecast() Void
    }

    class Person {
        +BigInt id
        +String first_name
        +String last_name
        +String email
        +String phone
        +BigInt organization_id
    }

    class Organization {
        +BigInt id
        +String name
        +String address
        +getDeals() List~Deal~
    }

    class DealPipelineService {
        +updateStage(dealId, stageId) Deal
        +calculatePipelineValue() Float
    }

    class ActivityService {
        +logActivity(type, recordId, details) Activity
    }

    Lead --> Deal : converts_to
    Organization "1" *-- "many" Person : employs
    Organization "1" *-- "many" Deal : owns
    DealPipelineService --> Deal : updates
    ActivityService ..> Lead : logs
```
"""

laravel_db = """
### 6.1 Comprehensive Database Schema Specification

Laravel-CRM is structured using Laravel Eloquent ORM on MySQL with standard auto-incrementing integer keys and soft-delete capabilities.

#### Table: `leads`
Stores pre-sales prospect leads captured via manual entry, CSV import, or marketing integrations.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `BIGINT UNSIGNED`| `PRIMARY KEY AUTO_INCREMENT` | - | Unique lead identifier |
| `title` | `VARCHAR(255)` | `NOT NULL` | - | Descriptive title for the lead |
| `person_id` | `BIGINT UNSIGNED`| `NULLABLE, FK -> people(id)` | `NULL` | Linked contact individual |
| `organization_id`| `BIGINT UNSIGNED`| `NULLABLE, FK -> organizations(id)`| `NULL` | Prospective business organization |
| `lead_status_id`| `INTEGER UNSIGNED`| `NOT NULL` | `1` | FK to status reference table |
| `amount` | `DECIMAL(12, 2)`| `NULLABLE` | `NULL` | Potential deal size |
| `created_at` | `TIMESTAMP` | `NOT NULL` | `NOW()` | Eloquent timestamp |
| `updated_at` | `TIMESTAMP` | `NOT NULL` | `NOW()` | Eloquent timestamp |

*Indexes:* `idx_leads_person` (`(person_id)`), `idx_leads_org` (`(organization_id)`).

---

#### Table: `deals`
Active sales pipeline opportunities progressing toward closing.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `BIGINT UNSIGNED`| `PRIMARY KEY AUTO_INCREMENT` | - | Unique deal identifier |
| `title` | `VARCHAR(255)` | `NOT NULL` | - | Deal project title |
| `amount` | `DECIMAL(12, 2)`| `NOT NULL` | `0.00` | Commercial deal value |
| `deal_stage_id` | `INTEGER UNSIGNED`| `NOT NULL` | `1` | Current Kanban pipeline stage |
| `person_id` | `BIGINT UNSIGNED`| `NULLABLE, FK -> people(id)` | `NULL` | Primary point of contact |
| `organization_id`| `BIGINT UNSIGNED`| `NULLABLE, FK -> organizations(id)`| `NULL` | Corporate client reference |
| `close_date` | `DATE` | `NULLABLE` | `NULL` | Expected closing date |

*Indexes:* `idx_deals_stage` (`(deal_stage_id)`), `idx_deals_org` (`(organization_id)`).
"""

laravel_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

The following sequence diagram details the Eloquent model lifecycle when a lead is qualified, converted to a deal, and dispatched through notifications.

```mermaid
sequenceDiagram
    autonumber
    actor Rep as Sales Representative
    participant Controller as Laravel CRM Controller
    participant LeadModel as Lead (Eloquent)
    participant DealService as DealPipelineService
    participant DB as MySQL Database
    participant Event as Laravel Event Dispatcher
    participant Mail as Notification Mailer

    Rep->>Controller: POST /laravel-crm/leads/{id}/convert-to-deal
    Controller->>LeadModel: findOrFail(id)
    LeadModel->>DB: SELECT * FROM leads WHERE id = ?
    DB-->>LeadModel: Lead data record
    
    Controller->>DealService: createDealFromLead(lead)
    DealService->>DB: INSERT into deals (title, amount, deal_stage_id, person_id)
    DB-->>DealService: Deal inserted (id: 42)
    
    DealService->>LeadModel: update(['lead_status_id' => Converted])
    LeadModel->>DB: UPDATE leads SET lead_status_id = 4 WHERE id = ?
    
    Controller->>Event: dispatch(new DealCreated(deal))
    Event->>Mail: Send LeadConvertedNotification to Account Executive
    Mail-->>Rep: Email dispatched successfully
    Controller-->>Rep: Redirect to /laravel-crm/deals/42
```
"""

# 5. DeskcommCRM
deskcomm_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class TicketService {
        +createTicket(contactId, subject, body) Ticket
        +assignToAgent(ticketId, agentId) Void
        +updateSla(ticketId) SlaStatus
    }

    class ContactManager {
        +findOrCreateContact(email, phone) Contact
        +getContactTimeline(contactId) List~Interaction~
    }

    class DeskChannelRouter {
        +routeInboundMessage(channel, payload) Message
        +dispatchOutbound(ticketId, channel, body) SendReceipt
    }

    class Ticket {
        +UUID id
        +String subject
        +String status
        +String priority
        +UUID contactId
        +UUID agentId
    }

    class Contact {
        +UUID id
        +String name
        +String email
        +String phone
    }

    TicketService --> ContactManager : associates
    DeskChannelRouter --> TicketService : generates_tickets
    Ticket "many" --> "1" Contact : belongs_to
```
"""

deskcomm_db = """
### 6.1 Comprehensive Database Schema Specification

DeskcommCRM persists customer desk interactions and support tickets within PostgreSQL with indexing on ticket statuses and agent assignments.

#### Table: `tickets`
Central helpdesk and sales enquiry tickets.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Unique ticket record ID |
| `subject` | `VARCHAR(255)` | `NOT NULL` | - | Subject line / issue summary |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'open'` | Lifecycle state (`open`, `pending`, `resolved`, `closed`) |
| `priority` | `VARCHAR(50)` | `NOT NULL` | `'normal'` | Urgency (`low`, `normal`, `high`, `urgent`) |
| `contact_id` | `UUID` | `NOT NULL, FK -> contacts(id)` | - | Submitting customer profile |
| `agent_id` | `UUID` | `NULLABLE, FK -> users(id)` | `NULL` | Assigned support representative |
| `created_at` | `TIMESTAMPTZ` | `NOT NULL` | `NOW()` | Ingestion timestamp |

*Indexes:* `idx_tickets_status` (`(status)`), `idx_tickets_contact` (`(contact_id)`).

---

#### Table: `contacts`
Customer directory profiles linked across omnichannel channels.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Contact profile ID |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Customer full name |
| `email` | `VARCHAR(255)` | `NOT NULL, UNIQUE` | - | Primary email address |
| `phone` | `VARCHAR(50)` | `NULLABLE` | `NULL` | Telephone contact |
| `created_at` | `TIMESTAMPTZ` | `NOT NULL` | `NOW()` | Registration timestamp |

*Indexes:* `idx_contacts_email` (`UNIQUE (email)`).
"""

deskcomm_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

The following sequence details how DeskcommCRM ingests inbound messages, creates tickets, and alerts agents.

```mermaid
sequenceDiagram
    autonumber
    actor Customer as Inbound Customer
    participant Router as DeskChannelRouter
    participant TicketSvc as TicketService
    participant ContactMgr as ContactManager
    participant DB as PostgreSQL Database
    actor Agent as Desk Agent

    Customer->>Router: Inbound Email / Webhook Message
    Router->>ContactMgr: findOrCreateContact(email, phone)
    ContactMgr->>DB: SELECT or INSERT contact
    DB-->>ContactMgr: Contact record (contact_id)
    
    Router->>TicketSvc: createTicket(contact_id, subject, body)
    TicketSvc->>DB: INSERT into tickets (subject, status='open', contact_id)
    DB-->>TicketSvc: Ticket created (ticket_id)
    
    TicketSvc->>Router: Notify available agents
    Router-->>Agent: Live push notification on desk dashboard
    Agent->>TicketSvc: assignToAgent(ticket_id, agent_id)
    TicketSvc->>DB: UPDATE tickets SET agent_id = ?
```
"""

# 6. Quickly
quickly_cd = """
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
"""

quickly_db = """
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
"""

quickly_seq = """
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
"""

# 7. qrev
qrev_cd = """
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
"""

qrev_db = """
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
"""

qrev_seq = """
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
"""

# 8. DenchClaw
dench_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class PipelineStateMachine {
        +validateStageTransition(dealId, targetStage) Boolean
        +executeTransitionHooks(dealId, newStage) Void
    }

    class ContactManager {
        +createContact(data) DenchContact
        +linkToCompany(contactId, companyId) Void
    }

    class CustomFieldEngine {
        +getFieldsForEntity(entityType) List~CustomField~
        +validateFieldValue(fieldDef, value) Boolean
    }

    class DenchDeal {
        +UUID id
        +String title
        +Float value
        +String stage
        +UUID companyId
    }

    class DenchContact {
        +UUID id
        +String firstName
        +String lastName
        +String email
    }

    PipelineStateMachine --> DenchDeal : orchestrates
    ContactManager --> DenchContact : manages
    CustomFieldEngine ..> DenchDeal : validates_schema
```
"""

dench_db = """
### 6.1 Comprehensive Database Schema Specification

DenchClaw stores sales pipelines and custom dynamic attributes within PostgreSQL tables.

#### Table: `dench_deals`
Sales opportunities tracked through rigid state-machine stage gates.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Unique deal identifier |
| `title` | `VARCHAR(255)` | `NOT NULL` | - | Opportunity title |
| `value` | `NUMERIC(12, 2)`| `NOT NULL` | `0.00` | Contract value |
| `stage` | `VARCHAR(50)` | `NOT NULL` | `'lead'` | Current pipeline state |
| `company_id` | `UUID` | `NULLABLE` | `NULL` | Linked company identifier |
| `created_at` | `TIMESTAMPTZ` | `NOT NULL` | `NOW()` | Creation timestamp |

*Indexes:* `idx_dench_deals_stage` (`(stage)`).

---

#### Table: `dench_contacts`
Prospects and decision-makers associated with business accounts.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Contact identifier |
| `first_name` | `VARCHAR(100)` | `NOT NULL` | - | First name |
| `last_name` | `VARCHAR(100)` | `NOT NULL` | - | Last name |
| `email` | `VARCHAR(255)` | `NOT NULL, UNIQUE` | - | Email address |

*Indexes:* `idx_dench_contacts_email` (`UNIQUE (email)`).
"""

dench_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

The following sequence demonstrates DenchClaw's pipeline state machine enforcing stage criteria before advancing a deal.

```mermaid
sequenceDiagram
    autonumber
    actor Rep as Sales Representative
    participant UI as DenchClaw Dashboard
    participant StateMachine as PipelineStateMachine
    participant CustomEngine as CustomFieldEngine
    participant DB as PostgreSQL Database

    Rep->>UI: Drag Deal from 'Discovery' to 'Contract Sent'
    UI->>StateMachine: validateStageTransition(dealId, 'Contract Sent')
    StateMachine->>CustomEngine: Check required fields (e.g. Legal Entity, Tax ID)
    CustomEngine->>DB: Query field values for dealId
    DB-->>CustomEngine: Values present
    CustomEngine-->>StateMachine: All stage validation gates passed
    
    StateMachine->>DB: UPDATE dench_deals SET stage = 'Contract Sent'
    DB-->>StateMachine: Commit transition
    StateMachine-->>UI: Transition approved & rendered
```
"""

# 9. relaticle
relaticle_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class RelationshipGraphService {
        +calculateClosenessScore(personId) Float
        +detectDormantRelationships() List~Person~
        +recommendTouchpoints() List~Reminder~
    }

    class InteractionTimeline {
        +logInteraction(personId, type, notes) Interaction
        +getTimeline(personId) List~Interaction~
    }

    class Person {
        +String id
        +String name
        +String email
        +String company
        +Float closenessScore
        +Date lastContactedAt
    }

    class Interaction {
        +String id
        +String personId
        +String type
        +String notes
        +DateTime timestamp
    }

    RelationshipGraphService --> Person : analyzes
    InteractionTimeline --> Interaction : records
    Interaction "many" --> "1" Person : refers_to
```
"""

relaticle_db = """
### 6.1 Comprehensive Database Schema Specification

Relaticle models personal CRM interactions and relationship graphs using Prisma with PostgreSQL.

#### Table: `Person`
Individual contact profiles in the personal/executive network.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `TEXT` | `PRIMARY KEY` | `cuid()` | Unique person ID |
| `name` | `TEXT` | `NOT NULL` | - | Full name |
| `email` | `TEXT` | `NULLABLE, UNIQUE` | `NULL` | Email address |
| `company` | `TEXT` | `NULLABLE` | `NULL` | Business organization |
| `closenessScore`| `DOUBLE PRECISION`| `NOT NULL` | `0.5` | Connection strength metric (0.0 - 1.0) |
| `lastContactedAt`| `TIMESTAMP(3)` | `NULLABLE` | `NULL` | Timestamp of last engagement |

*Indexes:* `Person_email_key` (`UNIQUE (email)`), `Person_closenessScore_idx` (`(closenessScore)`).

---

#### Table: `Interaction`
Chronological log of meetings, coffees, calls, and email exchanges.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `TEXT` | `PRIMARY KEY` | `cuid()` | Interaction record ID |
| `personId` | `TEXT` | `NOT NULL, FK -> Person(id)` | - | Associated contact |
| `type` | `TEXT` | `NOT NULL` | `'note'` | Medium (`coffee`, `call`, `email`, `note`) |
| `notes` | `TEXT` | `NOT NULL` | - | Conversational takeaway notes |
| `timestamp` | `TIMESTAMP(3)` | `NOT NULL` | `CURRENT_TIMESTAMP` | Occurrence date |

*Indexes:* `Interaction_personId_idx` (`(personId)`).
"""

relaticle_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

The following sequence details how Relaticle logs social touchpoints, updates relationship closeness metrics, and reschedules future reminders.

```mermaid
sequenceDiagram
    autonumber
    actor User as Executive / Networking Rep
    participant App as Relaticle Web App
    participant Timeline as InteractionTimeline
    participant Graph as RelationshipGraphService
    participant DB as PostgreSQL Database

    User->>App: Log Touchpoint (Person: Alice, Type: "Coffee", Notes: "Discussed partnership")
    App->>Timeline: logInteraction(personId, "coffee", notes)
    Timeline->>DB: INSERT into Interaction (personId, type, notes, timestamp=NOW())
    DB-->>Timeline: Interaction stored
    
    Timeline->>Graph: calculateClosenessScore(personId)
    Graph->>Graph: Recalculate score based on recency & frequency
    Graph->>DB: UPDATE Person SET closenessScore = 0.9, lastContactedAt = NOW()
    
    Graph->>DB: INSERT into Reminder (personId, triggerDate = NOW() + 30 days)
    DB-->>Graph: Reminder scheduled
    Graph-->>App: UI updated with refreshed relationship health score
```
"""

# 10. trycrm (Comp AI CRM)
trycrm_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class CompCRMService {
        +SupabaseClient client
        +ingestLead(leadData) Promise~Lead~
        +enrichLeadProfile(leadId) Promise~EnrichedLead~
        +trackDealProgress(dealId, stage) Promise~Deal~
    }

    class LeadScorer {
        +calculateFitScore(lead, company) Float
        +assignTier(score) String
    }

    class CompanyEnricher {
        +lookupCompanyByDomain(domain) CompanyData
    }

    class Lead {
        +UUID id
        +String email
        +String name
        +UUID companyId
        +Float fitScore
        +String tier
    }

    class Company {
        +UUID id
        +String name
        +String domain
        +Int employeeCount
    }

    CompCRMService --> LeadScorer : evaluates_leads
    CompCRMService --> CompanyEnricher : fetches_firmographics
    CompCRMService --> Lead : manages
    Lead "many" --> "1" Company : belongs_to
```
"""

trycrm_db = """
### 6.1 Comprehensive Database Schema Specification

TryCRM (Comp AI CRM) uses Supabase-backed PostgreSQL with Row Level Security (RLS) policies isolating user records and company data.

#### Table: `leads`
Prospective leads scored automatically using AI firmographic data.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Lead profile identifier |
| `email` | `VARCHAR(255)` | `NOT NULL, UNIQUE` | - | Business email address |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Lead full name |
| `company_id` | `UUID` | `NULLABLE, FK -> companies(id)` | `NULL` | Associated enterprise company |
| `fit_score` | `FLOAT` | `NOT NULL` | `0.0` | Algorithmic ICP match score |
| `tier` | `VARCHAR(20)` | `NOT NULL` | `'Tier 3'` | Priority classification (`Tier 1`, `Tier 2`, `Tier 3`) |
| `created_at` | `TIMESTAMPTZ` | `NOT NULL` | `NOW()` | Registration timestamp |

*Indexes:* `idx_trycrm_email` (`UNIQUE (email)`), `idx_trycrm_tier` (`(tier)`).

---

#### Table: `companies`
Firmographic profiles with automatic employee count and funding enrichment.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Company ID |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Corporate name |
| `domain` | `VARCHAR(255)` | `NOT NULL, UNIQUE` | - | Corporate domain |
| `employee_count`| `INTEGER` | `NULLABLE` | `NULL` | Headcount estimate |
| `industry` | `VARCHAR(100)` | `NULLABLE` | `NULL` | Industry sector |

*Indexes:* `idx_companies_domain` (`UNIQUE (domain)`).
"""

trycrm_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

The following sequence details how TryCRM automatically ingests an inbound lead, enriches firmographic data, and scores ICP fit.

```mermaid
sequenceDiagram
    autonumber
    actor Prospect as Inbound Lead
    participant API as TryCRM API Handler
    participant Svc as CompCRMService
    participant Enricher as CompanyEnricher
    participant Scorer as LeadScorer
    participant DB as PostgreSQL (Supabase)
    actor SDR as Sales Development Rep

    Prospect->>API: POST /api/leads/signup (name: "John Doe", email: "john@stripe.com")
    API->>Svc: ingestLead(data)
    
    Svc->>Enricher: lookupCompanyByDomain("stripe.com")
    Enricher-->>Svc: Company details (Fintech, 8000 employees)
    Svc->>DB: INSERT into companies (name, domain, employee_count)
    DB-->>Svc: Company persisted (company_id)
    
    Svc->>Scorer: calculateFitScore(lead, company)
    Scorer-->>Svc: Fit Score: 0.95 (Tier 1 Enterprise)
    
    Svc->>DB: INSERT into leads (name, email, company_id, fit_score=0.95, tier='Tier 1')
    DB-->>Svc: Lead saved
    
    Svc-->>SDR: Push notification: High-Value Tier 1 Lead Ingested
    API-->>Prospect: 201 Created (Success)
```
"""

# Execute insertions
insert_domain2('docs/wiki/crm.md', crm_cd, crm_db, crm_seq)
insert_domain2('docs/wiki/laravel-crm.md', laravel_cd, laravel_db, laravel_seq)
insert_domain2('docs/wiki/DeskcommCRM.md', deskcomm_cd, deskcomm_db, deskcomm_seq)
insert_domain2('docs/wiki/Quickly.md', quickly_cd, quickly_db, quickly_seq)
insert_domain2('docs/wiki/qrev.md', qrev_cd, qrev_db, qrev_seq)
insert_domain2('docs/wiki/DenchClaw.md', dench_cd, dench_db, dench_seq)
insert_domain2('docs/wiki/relaticle.md', relaticle_cd, relaticle_db, relaticle_seq)
insert_domain2('docs/wiki/trycrm.md', trycrm_cd, trycrm_db, trycrm_seq)
