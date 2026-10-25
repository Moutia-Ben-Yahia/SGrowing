import os
import re

def insert_sections(filepath, class_diagram, db_schema, seq_diagram):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    # 1. Insert Class Diagram into Section 2
    if 'classDiagram' not in content:
        sec3_match = re.search(r'\n## 3\.\s+Technology Stack', content)
        if sec3_match:
            idx = sec3_match.start()
            content = content[:idx] + "\n" + class_diagram.strip() + "\n\n---\n" + content[idx:]

    # 2. Insert Detailed DB Schema into Section 6
    if 'Comprehensive Database Schema Specification' not in content:
        sec_next_match = re.search(r'\n## [789]\.\s+', content)
        if sec_next_match:
            idx = sec_next_match.start()
            content = content[:idx] + "\n" + db_schema.strip() + "\n\n---\n" + content[idx:]

    # 3. Insert Sequence Diagram into Section 7 / before Strategic Value
    if 'sequenceDiagram' not in content:
        sec_strat_match = re.search(r'\n## [789]\.\s+Strategic Value', content)
        if sec_strat_match:
            idx = sec_strat_match.start()
            content = content[:idx] + "\n" + seq_diagram.strip() + "\n\n---\n" + content[idx:]

    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(content)
    print(f"Updated {filepath} successfully!")

# ==========================================
# DOMAIN 7: LIFECYCLE & JOURNEYS
# ==========================================

# 1. dittofeed
dittofeed_cd = """
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
"""

dittofeed_db = """
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
"""

dittofeed_seq = """
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
"""

# 2. laudspeaker
laudspeaker_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class JourneyEngine {
        +evaluateCustomer(customerId, event) Void
        +advanceStep(customerId, stepId) Void
    }

    class CustomerTracker {
        +trackEvent(customerId, eventName, data) Void
    }

    JourneyEngine --> CustomerTracker : monitors
```
"""

laudspeaker_db = """
### 6.1 Comprehensive Database Schema Specification

Laudspeaker tracks multi-channel steps and customer transitions in PostgreSQL.

#### Table: `journeys`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Journey ID |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Campaign name |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'active'` | Status |
"""

laudspeaker_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor User as User
    participant Tracker as CustomerTracker
    participant Engine as JourneyEngine
    participant DB as PostgreSQL Database

    User->>Tracker: Trigger event: "Added to Cart"
    Tracker->>DB: Log event
    Tracker->>Engine: evaluateCustomer(customerId)
    Engine->>DB: Advance journey step & queue notification
```
"""

# 3. notifuse
notifuse_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class NotificationDispatcher {
        +dispatch(notification) DeliveryReport
        +fallback(channel, notification) Void
    }

    class UserPreferenceService {
        +getPreferredChannel(userId) String
    }

    NotificationDispatcher --> UserPreferenceService : consults
```
"""

notifuse_db = """
### 6.1 Comprehensive Database Schema Specification

Notifuse stores user preferences and dispatch histories.

#### Table: `notifications`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Notification ID |
| `user_id` | `UUID` | `NOT NULL` | - | Recipient user |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'delivered'` | Status |
"""

notifuse_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor App as App
    participant Dispatcher as NotificationDispatcher
    participant Prefs as UserPreferenceService
    actor Gateway as SMS / Push Gateway

    App->>Dispatcher: Send notification to user
    Dispatcher->>Prefs: Check user preference
    Prefs-->>Dispatcher: Channel = SMS
    Dispatcher->>Gateway: Relay via Twilio
    Gateway-->>Dispatcher: Delivered
```
"""

# 4. parcelvoy
parcelvoy_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class JourneyWorker {
        +consumeEvent(event) Void
        +dispatchAction(action) Void
    }

    class AudienceSegmenter {
        +matches(user, segment) Boolean
    }

    JourneyWorker --> AudienceSegmenter : validates
```
"""

parcelvoy_db = """
### 6.1 Comprehensive Database Schema Specification

Parcelvoy models audiences and communication templates in PostgreSQL.

#### Table: `campaigns`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Campaign ID |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Campaign name |
| `channel` | `VARCHAR(50)` | `NOT NULL` | `'email'` | Medium |
"""

parcelvoy_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    participant Event as Inbound Event
    participant Worker as JourneyWorker
    participant Provider as Email Provider

    Event->>Worker: Event received
    Worker->>Worker: Evaluate segment filters
    Worker->>Provider: Send campaign message
    Provider-->>Worker: Accepted
```
"""

# 5. mautic
mautic_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class LeadModel {
        +createLead(data) Lead
        +adjustPoints(leadId, delta) Void
    }

    class CampaignExecutioner {
        +triggerCampaign(campaignId) Void
        +executeEvent(event, lead) Void
    }

    class Lead {
        +Int id
        +String email
        +String firstname
        +Int points
    }

    class Campaign {
        +Int id
        +String name
        +Boolean isPublished
    }

    CampaignExecutioner --> LeadModel : mutates
    CampaignExecutioner "1" *-- "many" Campaign : executes
```
"""

mautic_db = """
### 6.1 Comprehensive Database Schema Specification

Mautic utilizes MySQL with Doctrine ORM to coordinate marketing automation.

#### Table: `leads`
Marketing contacts tracked with behavioral scoring points.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `INT UNSIGNED` | `PRIMARY KEY AUTO_INCREMENT` | - | Lead identifier |
| `email` | `VARCHAR(191)` | `NULLABLE` | `NULL` | Contact email |
| `firstname` | `VARCHAR(191)` | `NULLABLE` | `NULL` | First name |
| `points` | `INT` | `NOT NULL` | `0` | Lead score points |
| `date_added` | `DATETIME` | `NOT NULL` | - | Creation date |

*Indexes:* `idx_mautic_email` (`(email)`), `idx_mautic_points` (`(points)`).
"""

mautic_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    participant Cron as Mautic Cron
    participant Exec as CampaignExecutioner
    participant DB as MySQL Database
    participant Mailer as Swiftmailer / Symfony Mailer
    actor Lead as Contact

    Cron->>Exec: Run `mautic:campaigns:trigger`
    Exec->>DB: Query leads matching campaign trigger events
    DB-->>Exec: Return eligible leads
    loop For Each Lead
        Exec->>Mailer: Send drip campaign email
        Mailer->>Lead: Transmit email
        Exec->>DB: UPDATE leads SET points = points + 10
        Exec->>DB: INSERT into campaign_lead_event_log
    end
```
"""

# ==========================================
# DOMAIN 8: WORKFLOWS, E-SIGN & SEO
# ==========================================

# 6. n8n
n8n_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class WorkflowExecute {
        +run(workflowData, executionMode) Promise~Execution~
        +runNode(nodeName, nodeInput) Promise~NodeOutput~
    }

    class NodeExecuteFunctions {
        +getNodeParameter(paramName) Any
        +helpers: RequestHelper
    }

    class WebhookManager {
        +registerWebhook(workflowId, path) Void
        +handleWebhook(req, res) Promise~Void~
    }

    class WorkflowEntity {
        +String id
        +String name
        +Boolean active
        +JSON nodes
        +JSON connections
    }

    class ExecutionEntity {
        +String id
        +String workflowId
        +String mode
        +String status
        +JSON data
    }

    WebhookManager --> WorkflowExecute : triggers
    WorkflowExecute --> WorkflowEntity : executes
    WorkflowExecute --> ExecutionEntity : records
```
"""

n8n_db = """
### 6.1 Comprehensive Database Schema Specification

n8n manages workflow topologies, credentials, and execution logs in PostgreSQL or SQLite.

#### Table: `workflow_entity`
Stores executable visual workflow definitions and node connections.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `VARCHAR(255)` | `PRIMARY KEY` | - | Unique workflow ID |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Workflow title |
| `active` | `BOOLEAN` | `NOT NULL` | `false` | Active webhook listener flag |
| `nodes` | `JSON` | `NOT NULL` | `'[]'` | Configured node definitions |
| `connections`| `JSON` | `NOT NULL` | `'{}'` | Directed graph edge connections |
| `createdAt` | `DATETIME` | `NOT NULL` | `NOW()` | Creation timestamp |

*Indexes:* Primary key on `id`.

---

#### Table: `execution_entity`
Stores historical run outputs and error states for workflow executions.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `VARCHAR(255)` | `PRIMARY KEY` | - | Execution run ID |
| `workflowId` | `VARCHAR(255)` | `NOT NULL, FK -> workflow_entity(id)` | - | Associated workflow |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'running'` | Execution state (`running`, `success`, `error`) |
| `mode` | `VARCHAR(50)` | `NOT NULL` | `'webhook'` | Trigger mode (`webhook`, `manual`, `trigger`) |
| `startedAt` | `DATETIME` | `NOT NULL` | `NOW()` | Start timestamp |
| `stoppedAt` | `DATETIME` | `NULLABLE` | `NULL` | Completion timestamp |

*Indexes:* `idx_execution_workflow` (`(workflowId)`), `idx_execution_status` (`(status)`).
"""

n8n_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

The following sequence shows how n8n receives an inbound webhook, initializes node execution contexts, runs nodes topologically, and records results.

```mermaid
sequenceDiagram
    autonumber
    actor External as Webhook Caller
    participant Webhook as WebhookManager
    participant Engine as WorkflowExecute
    participant DB as Database (TypeORM)
    actor Service as External API (Slack / CRM)

    External->>Webhook: POST /webhook/{path} (Payload JSON)
    Webhook->>DB: Query active workflow matching path
    DB-->>Webhook: Return workflow graph (nodes & connections)
    
    Webhook->>Engine: run(workflow, executionMode='webhook', initialData)
    Engine->>DB: INSERT into execution_entity (status='running')
    
    loop Topological Node Traversal
        Engine->>Engine: Execute Node 1 (Filter / Transform data)
        Engine->>Service: Execute Node 2 (POST message to Slack / CRM)
        Service-->>Engine: 200 OK
    end
    
    Engine->>DB: UPDATE execution_entity SET status='success', stoppedAt=NOW()
    Webhook-->>External: 200 OK (Workflow Execution Succeeded)
```
"""

# 7. documenso
documenso_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class DocumentService {
        +createDocument(file, title) Document
        +addRecipient(docId, email) Recipient
        +sendForSignature(docId) Void
    }

    class SignatureEngine {
        +applySignature(docId, fieldId, signatureData) Void
        +cryptographicallySignPdf(docId) Buffer
    }

    class Document {
        +String id
        +String title
        +String status
        +List~Recipient~ recipients
    }

    DocumentService --> Document : manages
    DocumentService --> SignatureEngine : signs
```
"""

documenso_db = """
### 6.1 Comprehensive Database Schema Specification

Documenso runs on PostgreSQL with Prisma ORM for e-signature tracking.

#### Table: `Document`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `TEXT` | `PRIMARY KEY` | `cuid()` | Document ID |
| `title` | `TEXT` | `NOT NULL` | - | Document title |
| `status` | `TEXT` | `NOT NULL` | `'DRAFT'` | State (`DRAFT`, `PENDING`, `COMPLETED`) |
| `documentDataId`| `TEXT` | `NOT NULL` | - | S3 file reference |
"""

documenso_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor Sender as Document Creator
    participant App as Documenso App
    participant Engine as SignatureEngine
    actor Signer as Signer

    Sender->>App: Uploads PDF & places signature box
    App->>Signer: Emails invitation link
    Signer->>App: Opens doc & draws signature
    App->>Engine: cryptographicallySignPdf(docId)
    Engine-->>App: Signed PDF with audit hash
    App-->>Sender: Both parties notified with completed PDF
```
"""

# 8. docuseal
docuseal_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class SubmissionService {
        +createSubmission(templateId, submitters) Submission
        +signDocument(submitterId, fields) Void
    }

    class Submission {
        +BigInt id
        +BigInt templateId
        +String status
    }

    SubmissionService --> Submission : manages
```
"""

docuseal_db = """
### 6.1 Comprehensive Database Schema Specification

Docuseal stores templates, submitters, and completed submissions in PostgreSQL.

#### Table: `submissions`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `BIGINT` | `PRIMARY KEY AUTO_INCREMENT` | - | Submission ID |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'pending'` | Status |
| `created_at` | `TIMESTAMP` | `NOT NULL` | `NOW()` | Timestamp |
"""

docuseal_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor Admin as Admin
    participant Svc as SubmissionService
    actor Signer as Signer

    Admin->>Svc: Send contract to client
    Svc->>Signer: Delivery via email
    Signer->>Svc: Sign on mobile browser
    Svc-->>Admin: Submission marked completed
```
"""

# 9. OpenSign
opensign_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class SignDocService {
        +uploadDocument(file) SignDoc
        +inviteSigners(docId, signers) Void
    }

    class CertificateGenerator {
        +generateAuditTrailPdf(docId) Buffer
    }

    SignDocService --> CertificateGenerator : seals
```
"""

opensign_db = """
### 6.1 Comprehensive Database Schema Specification

OpenSign manages signer verification and certified documents.

#### Table: `documents`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `VARCHAR(255)` | `PRIMARY KEY` | - | Doc ID |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Doc name |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'pending'` | Status |
"""

opensign_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    participant App as OpenSign App
    participant Cert as CertificateGenerator
    actor Signer as Signer

    App->>Signer: Invitation
    Signer->>App: Submits e-signature
    App->>Cert: Seal PDF with completion certificate
    Cert-->>App: Sealed Document
```
"""

# 10. papermark
papermark_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class DocumentService {
        +uploadDocument(file) Document
        +createSecureLink(docId, options) Link
    }

    class PageViewTracker {
        +recordView(linkId, pageNumber, duration) View
    }

    DocumentService --> PageViewTracker : tracks
```
"""

papermark_db = """
### 6.1 Comprehensive Database Schema Specification

Papermark stores pitch decks, links, and viewer analytics with Prisma on PostgreSQL.

#### Table: `Link`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `TEXT` | `PRIMARY KEY` | `cuid()` | Unique link slug |
| `documentId` | `TEXT` | `NOT NULL` | - | Target pitch deck |
| `isPasswordProtected`| `BOOLEAN` | `NOT NULL` | `false` | Security flag |
| `viewsCount` | `INTEGER` | `NOT NULL` | `0` | Total view counter |
"""

papermark_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor Founder as Founder
    participant App as Papermark
    actor Investor as Investor

    Founder->>App: Uploads deck & generates link
    Investor->>App: Opens deck link
    App->>App: Track page viewing time (Page 1: 45s, Page 2: 120s)
    App-->>Founder: Real-time notification: Investor viewing slide 2
```
"""

# 11. open-seo
openseo_cd = """
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
"""

openseo_db = """
### 6.1 Comprehensive Database Schema Specification

Open-SEO stores domain audits and technical SEO scores.

#### Table: `audits`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Audit ID |
| `url` | `TEXT` | `NOT NULL` | - | Analyzed URL |
| `score` | `FLOAT` | `NOT NULL` | `0.0` | Overall SEO score |
"""

openseo_seq = """
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
"""

# 12. seonaut
seonaut_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class Crawler {
        +startCrawl(domain) CrawlSession
        +parseLinks(html) List~Url~
    }

    class IssueDetector {
        +findBrokenLinks(urls) List~Issue~
    }

    Crawler --> IssueDetector : analyzes
```
"""

seonaut_db = """
### 6.1 Comprehensive Database Schema Specification

Seonaut stores web crawler sessions and identified technical issues.

#### Table: `crawled_urls`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Page ID |
| `url` | `TEXT` | `NOT NULL` | - | Crawled URL |
| `status_code` | `INTEGER` | `NOT NULL` | `200` | HTTP status code |
"""

seonaut_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    participant Crawler as Seonaut Crawler
    participant Site as Target Site
    participant DB as SQLite DB

    Crawler->>Site: Request /sitemap.xml
    Crawler->>Site: Crawl child pages concurrently
    Crawler->>DB: Store status codes and missing canonical tags
```
"""

# 13. serpbear
serpbear_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class ScraperService {
        +checkKeywordRank(keyword, domain) Int
    }

    class RankHistoryTracker {
        +logRank(keywordId, rank) Void
    }

    ScraperService --> RankHistoryTracker : saves
```
"""

serpbear_db = """
### 6.1 Comprehensive Database Schema Specification

Serpbear tracks SERP keyword rankings in SQLite.

#### Table: `keywords`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `INTEGER` | `PRIMARY KEY AUTOINCREMENT` | - | Keyword ID |
| `keyword` | `VARCHAR(255)` | `NOT NULL` | - | Search query |
| `domain` | `VARCHAR(255)` | `NOT NULL` | - | Tracked domain |
| `current_rank`| `INTEGER` | `NOT NULL` | `0` | Google rank position |
"""

serpbear_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    participant Cron as Cron Job
    participant Scraper as ScraperService
    participant Google as Google Search / Scraping API

    Cron->>Scraper: Check rank for "cold email software"
    Scraper->>Google: Query SERP results
    Google-->>Scraper: Rank position = 4
    Scraper->>Scraper: Save to historical rank chart
```
"""

# Execute insertions
insert_sections('docs/wiki/dittofeed.md', dittofeed_cd, dittofeed_db, dittofeed_seq)
insert_sections('docs/wiki/laudspeaker.md', laudspeaker_cd, laudspeaker_db, laudspeaker_seq)
insert_sections('docs/wiki/notifuse.md', notifuse_cd, notifuse_db, notifuse_seq)
insert_sections('docs/wiki/parcelvoy.md', parcelvoy_cd, parcelvoy_db, parcelvoy_seq)
insert_sections('docs/wiki/mautic.md', mautic_cd, mautic_db, mautic_seq)
insert_sections('docs/wiki/n8n.md', n8n_cd, n8n_db, n8n_seq)
insert_sections('docs/wiki/documenso.md', documenso_cd, documenso_db, documenso_seq)
insert_sections('docs/wiki/docuseal.md', docuseal_cd, docuseal_db, docuseal_seq)
insert_sections('docs/wiki/OpenSign.md', opensign_cd, opensign_db, opensign_seq)
insert_sections('docs/wiki/papermark.md', papermark_cd, papermark_db, papermark_seq)
insert_sections('docs/wiki/open-seo.md', openseo_cd, openseo_db, openseo_seq)
insert_sections('docs/wiki/seonaut.md', seonaut_cd, seonaut_db, seonaut_seq)
insert_sections('docs/wiki/serpbear.md', serpbear_cd, serpbear_db, serpbear_seq)
