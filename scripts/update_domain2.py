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

# Domain 2 definitions
# 1. twenty
twenty_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class WorkspaceService {
        +TypeORMManager manager
        +getWorkspace(id) Promise~Workspace~
        +createWorkspace(name) Promise~Workspace~
        +provisionTenantSchema(workspaceId) Promise~Void~
    }

    class ObjectMetadataService {
        +MetadataRegistry registry
        +getDynamicFields(objectName) List~FieldMetadata~
        +registerCustomField(object, fieldDef) Promise~FieldMetadata~
        +validateCustomRecord(objectName, payload) ValidationResult
    }

    class GraphqlResolverService {
        +AuthGuard auth
        +resolveQuery(query, workspaceContext) Promise~Response~
        +resolveMutation(mutation, workspaceContext) Promise~Response~
    }

    class EventBusService {
        +BullMQQueue queue
        +publishWorkspaceEvent(eventType, payload) Promise~Void~
        +subscribe(eventType, handler) Void
    }

    class Person {
        +UUID id
        +UUID workspaceId
        +String firstName
        +String lastName
        +List~Email~ emails
        +List~Phone~ phones
        +UUID companyId
        +String city
    }

    class Company {
        +UUID id
        +UUID workspaceId
        +String name
        +String domainName
        +Int employees
        +String address
    }

    class Opportunity {
        +UUID id
        +UUID workspaceId
        +String name
        +Int amount
        +String stage
        +Float probability
        +UUID companyId
        +UUID pointOfContactId
    }

    class FieldMetadata {
        +UUID id
        +UUID workspaceId
        +String objectType
        +String name
        +String label
        +String type
        +Boolean isCustom
    }

    WorkspaceService --> ObjectMetadataService : configures
    GraphqlResolverService --> WorkspaceService : authorizes
    GraphqlResolverService --> ObjectMetadataService : introspects
    GraphqlResolverService --> EventBusService : emits
    Company "1" *-- "many" Person : employs
    Company "1" *-- "many" Opportunity : relates_to
    Person "1" o-- "many" Opportunity : primary_contact
```
"""

twenty_db = """
### 6.1 Comprehensive Database Schema Specification

Twenty uses PostgreSQL with a metadata-driven architecture where standard CRM entities and user-defined custom fields are bound to multi-tenant workspace schemas.

#### Table: `core.workspace`
Represents the tenant isolation boundary encompassing users, permissions, custom objects, and billing tiers.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Unique workspace identifier |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Commercial name of workspace |
| `subdomain` | `VARCHAR(100)` | `NOT NULL, UNIQUE` | - | Subdomain used for URL routing |
| `logo` | `TEXT` | `NULLABLE` | `NULL` | Organization brand image URL |
| `metadata_version`| `INTEGER` | `NOT NULL` | `1` | Schema version controlling custom field migrations |
| `created_at` | `TIMESTAMPTZ` | `NOT NULL` | `NOW()` | Workspace registration timestamp |
| `updated_at` | `TIMESTAMPTZ` | `NOT NULL` | `NOW()` | Modification timestamp |

*Indexes:* `idx_workspace_subdomain` (`UNIQUE (subdomain)`).

---

#### Table: `core.person`
Standard person entity representing leads, contacts, and prospects.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Contact record identifier |
| `workspace_id` | `UUID` | `NOT NULL, FK -> core.workspace(id)` | - | Tenant partitioning key |
| `first_name` | `VARCHAR(255)` | `NOT NULL` | `''` | Given name |
| `last_name` | `VARCHAR(255)` | `NOT NULL` | `''` | Family name |
| `emails` | `JSONB` | `NOT NULL` | `'[]'` | Array of email objects (`{ primary: bool, address: str }`) |
| `phones` | `JSONB` | `NOT NULL` | `'[]'` | Array of phone objects (`{ primary: bool, number: str }`) |
| `company_id` | `UUID` | `NULLABLE, FK -> core.company(id)` | `NULL` | Employer company reference |
| `job_title` | `VARCHAR(255)` | `NULLABLE` | `NULL` | Professional job title |
| `city` | `VARCHAR(100)` | `NULLABLE` | `NULL` | Geographic city location |
| `created_at` | `TIMESTAMPTZ` | `NOT NULL` | `NOW()` | Record creation timestamp |

*Indexes:* `idx_person_workspace` (`(workspace_id)`), `idx_person_company` (`(company_id)`), `idx_person_emails` (`USING gin (emails)`).

---

#### Table: `core.opportunity`
Deals and revenue opportunities tracked across custom pipeline stages.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Opportunity identifier |
| `workspace_id` | `UUID` | `NOT NULL, FK -> core.workspace(id)` | - | Tenant boundary key |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Opportunity title / deal label |
| `amount` | `NUMERIC(15, 2)`| `NOT NULL` | `0.00` | Commercial value of deal |
| `currency` | `VARCHAR(3)` | `NOT NULL` | `'USD'` | ISO 4217 currency code |
| `stage` | `VARCHAR(100)` | `NOT NULL` | `'PROSPECT'`| Active pipeline stage |
| `close_date` | `DATE` | `NULLABLE` | `NULL` | Expected deal close date |
| `company_id` | `UUID` | `NULLABLE, FK -> core.company(id)` | `NULL` | Linked prospective company |
| `point_of_contact_id`| `UUID` | `NULLABLE, FK -> core.person(id)` | `NULL` | Primary prospect contact |
| `created_at` | `TIMESTAMPTZ` | `NOT NULL` | `NOW()` | Record creation timestamp |

*Indexes:* `idx_opportunity_workspace_stage` (`(workspace_id, stage)`), `idx_opportunity_close_date` (`(close_date)`).
"""

twenty_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

The following sequence details how the GraphQL API resolves a multi-tenant opportunity mutation, executes custom field validation, persists through TypeORM, and broadcasts event updates.

```mermaid
sequenceDiagram
    autonumber
    actor User as CRM User (Web App)
    participant Gateway as NestJS GraphQL Gateway
    participant Auth as Auth & Workspace Guard
    participant Metadata as ObjectMetadataService
    participant ORM as TypeORM Persistence
    participant DB as PostgreSQL Database
    participant Bus as BullMQ Event Bus
    participant WS as WebSocket Subscription Server

    User->>Gateway: GraphQL Mutation: `createOpportunity(input: {name, amount, stage})`
    Gateway->>Auth: Validate JWT & extract workspaceId
    Auth-->>Gateway: Authenticated (workspace: ws_123)
    
    Gateway->>Metadata: Introspect dynamic custom fields for 'opportunity'
    Metadata-->>Gateway: Field specifications returned (valid)
    
    Gateway->>ORM: Construct Opportunity entity instance
    ORM->>DB: INSERT into core.opportunity (workspace_id, name, amount, stage)
    DB-->>ORM: Entity persisted (id: opp_789)
    
    Gateway->>Bus: publishWorkspaceEvent('opportunity.created', {id: 'opp_789'})
    Bus->>WS: Push notification to connected workspace clients
    WS-->>User: Real-time UI update on Kanban board
    Gateway-->>User: GraphQL response { id: 'opp_789', status: 'SUCCESS' }
```
"""

# 2. espocrm
espocrm_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class EntityManager {
        +PDO pdo
        +Metadata metadata
        +getEntity(entityType, id) Entity
        +saveEntity(entity) Boolean
        +deleteEntity(entityType, id) Boolean
        +createEntity(entityType, data) Entity
    }

    class LeadService {
        +EntityManager entityManager
        +AclManager acl
        +createLead(data) Lead
        +convertLeadToAccount(leadId) ConvertResult
        +checkDuplicates(email, phone) List~Lead~
    }

    class AccountService {
        +EntityManager entityManager
        +getAccountDetails(accountId) Account
        +calculateTotalRevenue(accountId) Float
    }

    class AclManager {
        +User user
        +check(entityType, action) Boolean
        +checkScope(scope, permission) Boolean
    }

    class WorkflowManager {
        +processEvent(entityType, action, entity) Void
        +evaluateCondition(rule, entity) Boolean
        +triggerAction(actionDef, entity) Void
    }

    class Account {
        +String id
        +String name
        +String industry
        +String billingAddressCity
        +Float annualRevenue
    }

    class Lead {
        +String id
        +String firstName
        +String lastName
        +String emailAddress
        +String status
        +String source
    }

    class Opportunity {
        +String id
        +String name
        +String stage
        +Float amount
        +String accountId
        +Date closeDate
    }

    LeadService --> EntityManager : delegates
    AccountService --> EntityManager : delegates
    LeadService --> AclManager : enforces
    EntityManager --> WorkflowManager : emits_hooks
    Account "1" *-- "many" Opportunity : generates
    Lead ..> Account : converts_to
```
"""

espocrm_db = """
### 6.1 Comprehensive Database Schema Specification

EspoCRM runs on MySQL / MariaDB using a metadata-driven entity schema with soft deletes (`deleted = 0`) and automatic association mapping.

#### Table: `lead`
Stores inbound marketing leads prior to qualification or account conversion.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `VARCHAR(24)` | `PRIMARY KEY` | - | 24-character hexadecimal unique identifier |
| `name` | `VARCHAR(255)` | `NOT NULL` | `''` | Full composite name of the lead |
| `first_name` | `VARCHAR(100)` | `NOT NULL` | `''` | Given name |
| `last_name` | `VARCHAR(100)` | `NOT NULL` | `''` | Family name |
| `email_address` | `VARCHAR(255)` | `NULLABLE` | `NULL` | Primary business email |
| `phone_number` | `VARCHAR(100)` | `NULLABLE` | `NULL` | Direct contact telephone number |
| `status` | `VARCHAR(100)` | `NOT NULL` | `'New'` | Lead qualification stage (`New`, `Assigned`, `In Process`, `Converted`, `Dead`) |
| `source` | `VARCHAR(100)` | `NULLABLE` | `NULL` | Marketing origin (`Website`, `Cold Outreach`, `Referral`) |
| `assigned_user_id`| `VARCHAR(24)` | `NULLABLE, FK -> user(id)` | `NULL` | Sales representative responsible for follow-up |
| `deleted` | `TINYINT(1)` | `NOT NULL` | `0` | Soft delete marker (1 = deleted, 0 = active) |
| `created_at` | `DATETIME` | `NOT NULL` | - | Creation timestamp |

*Indexes:* `idx_lead_email` (`(email_address)`), `idx_lead_status` (`(status)`), `idx_lead_assigned` (`(assigned_user_id)`).

---

#### Table: `account`
Represents customer organizations and prospective enterprise accounts.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `VARCHAR(24)` | `PRIMARY KEY` | - | Hexadecimal unique account ID |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Organization commercial name |
| `website` | `VARCHAR(255)` | `NULLABLE` | `NULL` | Corporate domain URL |
| `type` | `VARCHAR(100)` | `NOT NULL` | `'Prospect'` | Account classification (`Customer`, `Partner`, `Prospect`) |
| `industry` | `VARCHAR(100)` | `NULLABLE` | `NULL` | Industry vertical (e.g. `Technology`, `Healthcare`) |
| `billing_address_city`| `VARCHAR(100)`| `NULLABLE` | `NULL` | City for billing purposes |
| `deleted` | `TINYINT(1)` | `NOT NULL` | `0` | Soft delete flag |
| `created_at` | `DATETIME` | `NOT NULL` | - | Creation timestamp |

*Indexes:* `idx_account_name` (`(name)`), `idx_account_type` (`(type)`).

---

#### Table: `opportunity`
Revenue pipeline records tracking sales stage progression and financial value.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `VARCHAR(24)` | `PRIMARY KEY` | - | Hexadecimal unique opportunity ID |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Deal title |
| `stage` | `VARCHAR(100)` | `NOT NULL` | `'Prospecting'` | Sales stage (`Prospecting`, `Proposal`, `Negotiation`, `Closed Won`) |
| `amount` | `DECIMAL(12, 2)`| `NOT NULL` | `0.00` | Financial value of deal |
| `account_id` | `VARCHAR(24)` | `NULLABLE, FK -> account(id)` | `NULL` | Parent account record |
| `close_date` | `DATE` | `NULLABLE` | `NULL` | Projected closing date |
| `probability` | `INT` | `NOT NULL` | `10` | Win probability percentage (0-100) |
| `deleted` | `TINYINT(1)` | `NOT NULL` | `0` | Soft delete flag |
| `created_at` | `DATETIME` | `NOT NULL` | - | Creation timestamp |

*Indexes:* `idx_opportunity_stage` (`(stage)`), `idx_opportunity_account` (`(account_id)`).
"""

espocrm_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

The following sequence traces an inbound lead submission through REST API processing, duplicate verification, workflow rule evaluation, and lead conversion.

```mermaid
sequenceDiagram
    autonumber
    actor Prospect as Webhook / Lead Source
    participant API as EspoCRM REST API
    participant LeadSvc as LeadService
    participant Acl as AclManager
    participant ORM as EntityManager
    participant DB as MySQL Database
    participant Workflow as WorkflowManager
    actor Rep as Assigned Sales Rep

    Prospect->>API: POST /api/v1/Lead (firstName, lastName, email)
    API->>Acl: Verify API user permission for 'create' on 'Lead'
    Acl-->>API: Authorized
    
    API->>LeadSvc: createLead(data)
    LeadSvc->>LeadSvc: checkDuplicates(email)
    alt No Duplicate Found
        LeadSvc->>ORM: createEntity('Lead', data)
        ORM->>DB: INSERT into lead (name, email_address, status='New', deleted=0)
        DB-->>ORM: Row inserted (id: 64aef...)
        
        ORM->>Workflow: processEvent('Lead', 'afterSave', lead)
        Workflow->>Workflow: Evaluate rule: "Auto-Assign New Leads by Territory"
        Workflow->>ORM: Update lead assigned_user_id = rep_101
        ORM->>DB: UPDATE lead SET assigned_user_id='rep_101', status='Assigned'
        
        Workflow-->>Rep: Emit desktop notification / email task
    else Duplicate Exists
        LeadSvc-->>API: Return existing lead ID with 409 Conflict / Merged
    end
    API-->>Prospect: 201 Created (Lead ID)
```
"""

# Execute insertions
insert_domain2('docs/wiki/twenty.md', twenty_cd, twenty_db, twenty_seq)
insert_domain2('docs/wiki/espocrm.md', espocrm_cd, espocrm_db, espocrm_seq)
