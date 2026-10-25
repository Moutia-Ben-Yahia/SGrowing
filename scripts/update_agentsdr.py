import os
import re

def update_agentsdr():
    path = 'docs/wiki/AgentSDR.md'
    with open(path, 'r', encoding='utf-8') as f:
        content = f.read()

    # 1. Class Diagram to insert into Section 2 or 6
    class_diagram = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class OrganizationContext {
        +UUID organizationId
        +UUID userId
        +String role
        +validate() Boolean
        +withOrgContext(handler) Promise
    }

    class OutreachScheduler {
        +UUID campaignId
        +buildDailyQueue(orgId) Promise~Int~
        +calculateLeadPriority(lead, step) Int
        +evaluateSequenceDelays(lastContacted, waitDays) Boolean
        +resetExhaustedMailboxQuotas(orgId) Promise~Void~
    }

    class TickSenderEngine {
        +Int batchSize
        +lockNextQueueBatch(mailboxId) Promise~List~
        +resolveMergeTags(template, leadData) String
        +executeSend(mailbox, lead, emailBody) Promise~SendResult~
        +recordActivity(leadId, messageId, status) Promise~Void~
    }

    class MailboxManager {
        +UUID mailboxId
        +String emailAddress
        +Int dailySendLimit
        +Int todayEmailsSent
        +rotateMailbox(orgId, channel) OutreachMailbox
        +checkWorkingHours(mailbox, targetTz) Boolean
        +enforceSendGap(mailboxId, minSeconds) Boolean
        +impersonateServiceAccount(email) GmailClient
    }

    class IntentClassifier {
        +String apiKey
        +classifyReply(emailContent) IntentCategory
        +updateLeadPipelineStage(leadId, category) Promise~Void~
        +extractActionableEntities(emailText) EntityMap
        +pauseActiveSequence(leadId) Promise~Void~
    }

    class GridFormulaRunner {
        +QuickJSRuntime runtime
        +executeFormula(formulaCode, rowContext) Any
        +evaluateApolloEnrichment(domain, apiKey) Promise~Object~
        +sanitizeInput(rawExpression) String
    }

    class CallSessionCoordinator {
        +UUID sessionId
        +String agentId
        +String prospectPhone
        +initiateWebRTCSession(agentSocket) CallSession
        +streamAudioToRecordingWorker(stream) Void
        +recordDisposition(sessionId, dispositionNotes) Promise~Void~
    }

    class OutreachCampaign {
        +UUID id
        +UUID organizationId
        +String name
        +String status
        +List~Step~ sequence
        +addLead(personId) Promise~Lead~
        +pause() Void
        +resume() Void
    }

    class OutreachMailbox {
        +UUID id
        +UUID organizationId
        +String emailAddress
        +Int dailySendLimit
        +Int todayEmailsSent
        +Timestamp nextEmailTime
        +hasCapacity() Boolean
    }

    class OutreachLead {
        +UUID id
        +UUID personId
        +UUID campaignId
        +UUID mailboxId
        +String sequenceStatus
        +Int currentStep
        +Timestamp nextSendAt
        +advanceStep() Void
        +markReplied() Void
    }

    OrganizationContext --> OutreachScheduler : authorizes
    OutreachScheduler ..> OutreachCampaign : evaluates
    OutreachScheduler ..> OutreachMailbox : allocates
    TickSenderEngine --> MailboxManager : acquires sender
    TickSenderEngine --> OutreachLead : mutates state
    IntentClassifier --> OutreachLead : pauses on reply
    GridFormulaRunner ..> OutreachLead : enriches
    CallSessionCoordinator --> OutreachLead : attaches call logs
    OutreachCampaign "1" *-- "many" OutreachLead : contains
    OutreachMailbox "1" o-- "many" OutreachLead : assigned_to
```
"""

    # 2. Detailed Database Schema to insert into Section 6
    db_schema_details = """
### 6.1 Comprehensive Database Schema Specification

The underlying persistence architecture relies on PostgreSQL 15+ managed via Drizzle ORM with fail-closed multi-tenancy keys (`organization_id`).

#### Table: `public.organizations`
Master tenant container enforcing tenant boundaries across campaigns, mailboxes, and CRM records.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Unique immutable organization identifier |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Commercial display name of the workspace |
| `slug` | `VARCHAR(100)` | `NOT NULL, UNIQUE` | - | URL-safe slug for subdomains and API routing |
| `logo_url` | `TEXT` | `NULLABLE` | `NULL` | Public asset CDN URL for organization branding |
| `metadata` | `JSONB` | `NOT NULL` | `'{}'` | Tenant configuration, feature flags, and billing limits |
| `created_at` | `TIMESTAMPTZ` | `NOT NULL` | `NOW()` | Audit creation timestamp |
| `updated_at` | `TIMESTAMPTZ` | `NOT NULL` | `NOW()` | Row modification timestamp |

*Indexes:* `idx_org_slug` (`UNIQUE btree (slug)`), `idx_org_created_at` (`btree (created_at)`).

---

#### Table: `public.outreach_mailboxes`
Stores provisioned sending accounts authenticated via Google Workspace Domain-Wide Delegation or SMTP.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Unique mailbox identifier |
| `organization_id` | `UUID` | `NOT NULL, FK -> organizations(id) ON DELETE CASCADE` | - | Parent tenant isolation key |
| `email_address` | `VARCHAR(255)` | `NOT NULL` | - | Outbound sending address (e.g., rep@company.com) |
| `display_name` | `VARCHAR(255)` | `NOT NULL` | - | Sender identity displayed in prospect mail clients |
| `provider` | `VARCHAR(50)` | `NOT NULL` | `'google'` | Authentication provider (`google`, `smtp`, `microsoft`) |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'active'` | Operational status (`active`, `paused`, `error`, `exhausted`) |
| `daily_send_limit` | `INTEGER` | `NOT NULL` | `30` | Maximum cold emails dispatched per 24-hour cycle |
| `today_emails_sent`| `INTEGER` | `NOT NULL` | `0` | Dispatched counter; reset daily at midnight tenant TZ |
| `min_gap_seconds` | `INTEGER` | `NOT NULL` | `180` | Required idle spacing between consecutive dispatches |
| `next_email_time` | `TIMESTAMPTZ` | `NULLABLE` | `NULL` | Next permissible dispatch timestamp enforcing gap rule |
| `working_hours` | `JSONB` | `NOT NULL` | `'{"mon":{"start":"09:00","end":"17:00"}}'` | Active sending window definitions in local timezone |
| `timezone` | `VARCHAR(50)` | `NOT NULL` | `'UTC'` | Timezone governing working hours and quota reset |
| `last_history_id` | `VARCHAR(100)` | `NULLABLE` | `NULL` | Gmail Pub/Sub sync cursor for delta message polling |
| `created_at` | `TIMESTAMPTZ` | `NOT NULL` | `NOW()` | Registration timestamp |

*Indexes:* `idx_mailboxes_org_status` (`btree (organization_id, status)`), `idx_mailboxes_email` (`btree (email_address)`), `idx_mailboxes_next_send` (`btree (next_email_time)`).

---

#### Table: `public.outreach_campaigns`
Defines multi-step drip campaigns, sequence cadence, and target audience configurations.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Unique campaign identifier |
| `organization_id` | `UUID` | `NOT NULL, FK -> organizations(id) ON DELETE CASCADE` | - | Tenant scope key |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Operator-defined campaign label |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'draft'` | Lifecycle state (`draft`, `active`, `paused`, `completed`) |
| `sequence` | `JSONB` | `NOT NULL` | `'[]'` | Ordered array of sequence steps, wait days, templates |
| `track_opens` | `BOOLEAN` | `NOT NULL` | `false` | Enable/disable 1x1 tracking pixel insertion |
| `track_clicks` | `BOOLEAN` | `NOT NULL` | `false` | Enable/disable link wrapping for click tracking |
| `stop_on_reply` | `BOOLEAN` | `NOT NULL` | `true` | Automatically pause lead upon receiving response |
| `created_at` | `TIMESTAMPTZ` | `NOT NULL` | `NOW()` | Record creation timestamp |
| `updated_at` | `TIMESTAMPTZ` | `NOT NULL` | `NOW()` | Last sequence edit timestamp |

*Indexes:* `idx_campaigns_org_status` (`btree (organization_id, status)`), `idx_campaigns_created` (`btree (created_at)`).

---

#### Table: `public.outreach_leads`
Join record linking individual prospect contacts (`people`) to specific outbound campaigns.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Unique campaign enrollment identifier |
| `organization_id` | `UUID` | `NOT NULL, FK -> organizations(id) ON DELETE CASCADE` | - | Multi-tenant organization scope key |
| `person_id` | `UUID` | `NOT NULL, FK -> people(id) ON DELETE RESTRICT` | - | Associated human prospect entity |
| `campaign_id` | `UUID` | `NOT NULL, FK -> outreach_campaigns(id) ON DELETE CASCADE` | - | Target campaign sequence |
| `mailbox_id` | `UUID` | `NULLABLE, FK -> outreach_mailboxes(id) ON DELETE SET NULL` | `NULL` | Pinned or dynamically rotated sending mailbox |
| `sequence_status` | `VARCHAR(50)` | `NOT NULL` | `'pending'` | Current state (`pending`, `initial_sent`, `in_follow_up`, `completed`, `replied`, `bounced`) |
| `current_step` | `INTEGER` | `NOT NULL` | `1` | Zero-indexed or 1-indexed pointer to sequence array |
| `next_send_at` | `TIMESTAMPTZ` | `NULLABLE` | `NOW()` | Earliest scheduled dispatch time for current step |
| `last_sent_at` | `TIMESTAMPTZ` | `NULLABLE` | `NULL` | Timestamp of previous email dispatch |
| `custom_fields` | `JSONB` | `NOT NULL` | `'{}'` | Custom CSV/API merge tags (`{{company}}`, `{{painPoint}}`) |
| `created_at` | `TIMESTAMPTZ` | `NOT NULL` | `NOW()` | Enrollment timestamp |

*Indexes:* `idx_leads_org_campaign` (`btree (organization_id, campaign_id)`), `idx_leads_status_next_send` (`btree (sequence_status, next_send_at)`), `idx_leads_person_campaign` (`UNIQUE btree (person_id, campaign_id)`).

---

#### Table: `public.outreach_mailbox_queue`
High-concurrency relational queue locked via `FOR UPDATE SKIP LOCKED` during tick runs.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Unique queue entry identifier |
| `organization_id` | `UUID` | `NOT NULL, FK -> organizations(id) ON DELETE CASCADE` | - | Tenant partitioning key |
| `mailbox_id` | `UUID` | `NOT NULL, FK -> outreach_mailboxes(id) ON DELETE CASCADE` | - | Mailbox reserved for this dispatch item |
| `lead_id` | `UUID` | `NOT NULL, FK -> outreach_leads(id) ON DELETE CASCADE` | - | Target recipient lead record |
| `step_number` | `INTEGER` | `NOT NULL` | `1` | Step sequence index to execute |
| `priority` | `INTEGER` | `NOT NULL` | `100` | Dispatch priority (lower number executes first) |
| `scheduled_for` | `TIMESTAMPTZ` | `NOT NULL` | `NOW()` | Timestamp after which the item becomes eligible |
| `locked_at` | `TIMESTAMPTZ` | `NULLABLE` | `NULL` | Worker lease acquisition timestamp |
| `locked_by` | `VARCHAR(100)` | `NULLABLE` | `NULL` | Worker instance hostname / PID |

*Indexes:* `idx_queue_lock_fetch` (`btree (mailbox_id, scheduled_for, priority)` WHERE `locked_at IS NULL`), `idx_queue_lead` (`btree (lead_id)`).

---

#### Table: `public.crm_conversations`
Unified conversation thread aggregating inbound and outbound interactions across all channels.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Unique conversation thread identifier |
| `organization_id` | `UUID` | `NOT NULL, FK -> organizations(id) ON DELETE CASCADE` | - | Tenant boundary key |
| `person_id` | `UUID` | `NOT NULL, FK -> people(id) ON DELETE CASCADE` | - | Linked prospect entity |
| `channel` | `VARCHAR(50)` | `NOT NULL` | `'email'` | Communication medium (`email`, `linkedin`, `whatsapp`, `call`) |
| `sentiment` | `VARCHAR(50)` | `NULLABLE` | `NULL` | AI sentiment result (`positive`, `neutral`, `negative`) |
| `intent` | `VARCHAR(50)` | `NULLABLE` | `NULL` | Classified intent (`interested`, `objection`, `wrong_contact`, `unsubscribe`) |
| `is_unread` | `BOOLEAN` | `NOT NULL` | `true` | Flag indicating unhandled inbound messages |
| `last_message_at` | `TIMESTAMPTZ` | `NOT NULL` | `NOW()` | Most recent message timestamp for inbox sorting |

*Indexes:* `idx_conv_org_unread` (`btree (organization_id, is_unread)`), `idx_conv_person` (`btree (person_id)`), `idx_conv_last_message` (`btree (last_message_at DESC)`).
"""

    # 3. Sequence Diagram to insert into Section 7
    sequence_diagram = """
### 7.5 End-to-End Operational Lifecycle Sequence

The following sequence illustrates the complete operational workflow from daily queue generation, atomic row-locked tick dispatch, Google Workspace transmission, and inbound webhook sentiment triage.

```mermaid
sequenceDiagram
    autonumber
    actor Operator as Sales Operator
    participant Cron as Cron / Slot Scheduler
    participant Builder as Queue Builder Engine
    participant TickWorker as Outbound Tick Worker
    participant DB as PostgreSQL Database
    participant GoogleGW as Google Workspace API
    participant PubSub as Google Cloud Pub/Sub
    participant WebhookHandler as Inbound Webhook Service
    participant AIClassifier as AI Intent Classifier

    Note over Operator, DB: Phase 1: Campaign Creation & Queue Ingestion
    Operator->>DB: Create Campaign & Import Leads (outreach_leads)
    Cron->>Builder: POST /api/outreach/build-queue (Trigger daily run)
    Builder->>DB: Query active campaigns & leads where next_send_at <= NOW()
    Builder->>DB: Evaluate mailbox daily limits & allocate leads
    Builder->>DB: INSERT into outreach_mailbox_queue (mailbox_id, lead_id, priority)
    DB-->>Builder: Queue populated successfully

    Note over TickWorker, GoogleGW: Phase 2: Atomic Row-Locked Dispatch Loop
    loop Every 60 Seconds
        Cron->>TickWorker: POST /api/outreach/tick (Cron Secret Auth)
        TickWorker->>DB: BEGIN TRANSACTION
        TickWorker->>DB: SELECT * FROM outreach_mailbox_queue<br/>WHERE scheduled_for <= NOW() AND locked_at IS NULL<br/>ORDER BY priority ASC LIMIT 20 FOR UPDATE SKIP LOCKED
        DB-->>TickWorker: Return locked queue rows
        
        loop For Each Locked Queue Row
            TickWorker->>DB: Check mailbox today_emails_sent < daily_send_limit AND min_gap passed
            alt Mailbox is Ready
                TickWorker->>TickWorker: Resolve merge tags ({{company}}, {{name}})
                TickWorker->>GoogleGW: Impersonate Service Account -> users.messages.send()
                GoogleGW-->>TickWorker: 200 OK (Message ID, Thread ID)
                TickWorker->>DB: UPDATE outreach_mailboxes SET today_emails_sent += 1, next_email_time = NOW() + gap
                TickWorker->>DB: UPDATE outreach_leads SET sequence_status = 'initial_sent', current_step += 1
                TickWorker->>DB: INSERT into outreach_emails (lead_id, mailbox_id, message_id)
                TickWorker->>DB: DELETE FROM outreach_mailbox_queue WHERE id = row.id
            else Mailbox In Gap / Exhausted
                TickWorker->>DB: UPDATE outreach_mailbox_queue SET scheduled_for = NOW() + 60s
            end
        end
        TickWorker->>DB: COMMIT TRANSACTION
    end

    Note over PubSub, AIClassifier: Phase 3: Inbound Reply & AI Intent Classification
    GoogleGW->>PubSub: Push Notification (New Email in Mailbox History)
    PubSub->>WebhookHandler: POST /api/outreach/webhooks/gmail-watch
    WebhookHandler->>GoogleGW: Fetch email body via historyId delta
    GoogleGW-->>WebhookHandler: Inbound message payload
    WebhookHandler->>DB: Find lead matching sender email in outreach_leads
    WebhookHandler->>AIClassifier: Analyze reply text & classify sentiment/intent
    AIClassifier-->>WebhookHandler: Intent: "interested", Sentiment: "positive"
    WebhookHandler->>DB: UPDATE outreach_leads SET sequence_status = 'replied' (Sequence Paused)
    WebhookHandler->>DB: INSERT into crm_conversations (person_id, channel, sentiment, intent)
    WebhookHandler->>DB: UPDATE crm_records SET stage = 'Meeting Scheduled'
    WebhookHandler-->>Operator: Push UI notification (WebSocket / In-App Badge)
```
"""

    # Insert class_diagram after Section 2's flowchart TD
    if '### 2.1 Core Domain & Service Class Architecture' not in content:
        # Find end of flowchart TD block
        flowchart_end = content.find('```\n\n---\n\n## 3. Technology Stack')
        if flowchart_end != -1:
            content = content[:flowchart_end + 4] + class_diagram + content[flowchart_end + 4:]
        else:
            # Fallback insertion
            content = content.replace('## 3. Technology Stack', class_diagram + '\n---\n\n## 3. Technology Stack')

    # Insert db_schema_details into Section 6
    if '### 6.1 Comprehensive Database Schema Specification' not in content:
        # Find Section 7 start
        sec7_start = content.find('## 7. API Architecture')
        if sec7_start != -1:
            content = content[:sec7_start] + db_schema_details + '\n---\n\n' + content[sec7_start:]
        else:
            content = content.replace('## 7. API Architecture', db_schema_details + '\n---\n\n## 7. API Architecture')

    # Insert sequence_diagram into Section 7
    if '### 7.5 End-to-End Operational Lifecycle Sequence' not in content:
        sec8_start = content.find('## 8. Strategic Value')
        if sec8_start != -1:
            content = content[:sec8_start] + sequence_diagram + '\n---\n\n' + content[sec8_start:]
        else:
            content = content.replace('## 8. Strategic Value', sequence_diagram + '\n---\n\n## 8. Strategic Value')

    with open(path, 'w', encoding='utf-8') as f:
        f.write(content)
    print('Updated AgentSDR.md successfully!')

update_agentsdr()
