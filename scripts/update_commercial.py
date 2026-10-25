import os
import re

def insert_commercial(filepath, class_diagram, db_schema, seq_diagram):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    # Ensure no em-dashes or double hyphens in new text
    for text in [class_diagram, db_schema, seq_diagram]:
        assert '—' not in text, "Prose em dash forbidden!"
        assert ' -- ' not in text, "Spaced double hyphen forbidden!"

    # 1. Insert Class Diagram into Section 2
    if 'classDiagram' not in content:
        sec2_match = re.search(r'\n## 2\.\s+', content)
        if sec2_match:
            # find next subheader or paragraph in section 2
            sec3_match = re.search(r'\n## 3\.\s+', content)
            if sec3_match:
                idx = sec3_match.start()
                content = content[:idx] + "\n" + class_diagram.strip() + "\n\n---\n" + content[idx:]

    # 2. Insert Detailed DB Schema into Section 2 or 6
    if 'Comprehensive Database Schema Specification' not in content:
        sec3_match = re.search(r'\n## 3\.\s+', content)
        if sec3_match:
            idx = sec3_match.start()
            content = content[:idx] + "\n" + db_schema.strip() + "\n\n---\n" + content[idx:]

    # 3. Insert Sequence Diagram into Section 3 / 4 / before last section
    if 'sequenceDiagram' not in content:
        # insert before the final Section or before Reusable Architectural Patterns
        patterns = [r'\n## [456]\.\s+Architectural Comparison', r'\n## [456]\.\s+Strategic', r'\n## [45]\.\s+API, Webhooks', r'\n## [45]\.\s+Reverse-Engineered']
        target_idx = None
        for p in patterns:
            m = re.search(p, content)
            if m:
                target_idx = m.start()
                break
        if target_idx is not None:
            content = content[:target_idx] + "\n" + seq_diagram.strip() + "\n\n---\n" + content[target_idx:]
        else:
            # fallback append before last section
            last_sec = list(re.finditer(r'\n## \d+\.\s+', content))[-1]
            idx = last_sec.start()
            content = content[:idx] + "\n" + seq_diagram.strip() + "\n\n---\n" + content[idx:]

    # Final check for prose dashes in the whole updated content
    # Note: mermaid syntax may have --> or -.-, but prose shouldn't have em dash
    lines = content.splitlines()
    for i, line in enumerate(lines):
        if '—' in line:
            lines[i] = line.replace('—', ':')
        if ' -- ' in line:
            lines[i] = line.replace(' -- ', ': ')
    content = '\n'.join(lines)

    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(content)
    print(f"Updated {filepath} successfully!")

# Commercial Definitions
# 1. instantly-ai
instantly_cd = """
### 2.1 Core Domain and Service Class Architecture

```mermaid
classDiagram
    class Workspace {
        +UUID id
        +String name
        +String billingPlan
        +List~EmailAccount~ emailAccounts
        +List~Campaign~ campaigns
        +checkSendingCredits() Boolean
    }

    class EmailAccount {
        +UUID id
        +UUID workspaceId
        +String emailAddress
        +String provider
        +Int dailyLimit
        +Int sentToday
        +Boolean warmupEnabled
        +rotateDailyQuota() Void
    }

    class WarmupNode {
        +UUID accountId
        +Float healthScore
        +Int rampUpDay
        +String peerMeshId
        +sendWarmupEmail(peerNode) Void
        +rescueFromSpamFolder(msgId) Void
    }

    class Campaign {
        +UUID id
        +UUID workspaceId
        +String name
        +String status
        +List~Step~ sequence
        +List~CampaignLead~ leads
        +start() Void
        +pause() Void
    }

    class CampaignLead {
        +UUID id
        +UUID campaignId
        +String email
        +String status
        +Int currentStep
        +advance() Void
        +markReplied() Void
    }

    class RotationEngine {
        +selectOptimalSender(workspaceId, leadDomain) EmailAccount
        +enforceEspMatching(senderProvider, leadProvider) Boolean
    }

    class UniboxConversation {
        +UUID id
        +UUID leadId
        +String sentiment
        +String dealStage
        +assignRep(userId) Void
    }

    Workspace "1" *-- "many" EmailAccount : manages
    Workspace "1" *-- "many" Campaign : owns
    EmailAccount "1" *-- "1" WarmupNode : maintains
    Campaign "1" *-- "many" CampaignLead : enrolls
    RotationEngine ..> EmailAccount : selects
    CampaignLead "1" --> "1" UniboxConversation : creates_on_reply
```
"""

instantly_db = """
### 2.2 Comprehensive Database Schema Specification

Instantly orchestrates distributed horizontal sending pools and Unibox triage within multi-tenant relational schemas.

#### Table: `workspaces`
Master workspace isolating campaign accounts, credits, and custom tracking domains.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Unique workspace identifier |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Commercial workspace label |
| `billing_plan` | `VARCHAR(50)` | `NOT NULL` | `'growth'` | Subscription tier (`growth`, `hypergrowth`, `enterprise`) |
| `daily_email_credits`| `INTEGER` | `NOT NULL` | `5000` | Global daily account sending cap |
| `created_at` | `TIMESTAMPTZ` | `NOT NULL` | `NOW()` | Registration timestamp |

*Indexes:* Primary key on `id`.

---

#### Table: `email_accounts`
Sender mailbox credentials authenticated via Google Workspace OAuth, Microsoft 365, or SMTP/IMAP.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Sender mailbox ID |
| `workspace_id` | `UUID` | `NOT NULL, FK -> workspaces(id)` | - | Parent workspace |
| `email_address` | `VARCHAR(255)` | `NOT NULL` | - | Mailbox email (e.g. `rep@domain-outreach.com`) |
| `provider` | `VARCHAR(50)` | `NOT NULL` | `'google'` | Provider (`google`, `microsoft`, `custom_smtp`) |
| `daily_limit` | `INTEGER` | `NOT NULL` | `30` | Maximum cold emails per 24 hours |
| `sent_today` | `INTEGER` | `NOT NULL` | `0` | Outbound volume sent today |
| `warmup_status` | `VARCHAR(50)` | `NOT NULL` | `'active'` | Warmup mesh state (`active`, `paused`, `disabled`) |
| `health_score` | `FLOAT` | `NOT NULL` | `98.5` | Inbox deliverability score (0.0 - 100.0) |

*Indexes:* `idx_email_acc_workspace` (`(workspace_id)`), `idx_email_acc_health` (`(health_score)`).

---

#### Table: `campaigns`
Multi-step drip campaigns, sequence schedules, and assigned sender account pools.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Campaign ID |
| `workspace_id` | `UUID` | `NOT NULL, FK -> workspaces(id)` | - | Tenant scope key |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Campaign title |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'draft'` | State (`draft`, `active`, `paused`, `completed`) |
| `sequence` | `JSONB` | `NOT NULL` | `'[]'` | Ordered array of sequence steps and wait intervals |

*Indexes:* `idx_instantly_campaigns_status` (`(status)`).
"""

instantly_seq = """
### 3.1 End-to-End Operational Lifecycle Sequence

The following sequence details how Instantly distributes cold email campaigns across rotating secondary inboxes and routes prospect replies into the centralized Unibox.

```mermaid
sequenceDiagram
    autonumber
    actor Rep as Sales Representative
    participant UI as Instantly Web Dashboard
    participant Engine as Campaign Rotation Engine
    participant DB as Distributed Database
    participant SenderPool as Rotating Inboxes (Pool of 50)
    actor Prospect as Prospect Mailbox
    participant Unibox as Instantly Unibox Service

    Rep->>UI: Launch Campaign (5,000 Leads, 50 Connected Inboxes)
    UI->>DB: UPDATE campaigns SET status = 'active'
    
    loop Dispatch Loop (Every 60s)
        Engine->>DB: Query eligible leads WHERE status = 'pending'
        DB-->>Engine: Return lead batch
        loop For Each Lead
            Engine->>Engine: Select optimal sender account (ESP Matching: Google to Google)
            Engine->>SenderPool: Transmit email via authenticated OAuth
            SenderPool->>Prospect: Deliver cold email
            SenderPool-->>Engine: Delivery confirmed
            Engine->>DB: UPDATE email_accounts SET sent_today = sent_today + 1
            Engine->>DB: UPDATE campaign_leads SET current_step = 1, status = 'sent'
        end
    end

    Prospect-->>SenderPool: Inbound Reply ("Interested! Can we talk Thursday?")
    SenderPool->>Unibox: Webhook / IMAP Sync (New Inbound Message)
    Unibox->>Unibox: Run AI Sentiment Classifier (Result: "Positive / Interested")
    Unibox->>DB: UPDATE campaign_leads SET status = 'replied' (Pause sequence)
    Unibox->>DB: INSERT into unibox_conversations (deal_stage = 'Meeting Request')
    Unibox-->>Rep: Real-time UI notification on Unibox dashboard
```
"""

# 2. clay-com
clay_cd = """
### 2.1 Core Domain and Service Class Architecture

```mermaid
classDiagram
    class Workspace {
        +UUID id
        +String name
        +List~Workbook~ workbooks
        +Int creditBalance
    }

    class Table {
        +UUID id
        +String name
        +List~Column~ columns
        +List~Row~ rows
        +addRow(data) Row
    }

    class Column {
        +UUID id
        +String name
        +String type
        +JSON formulaOrProvider
    }

    class WaterfallOrchestrator {
        +executeWaterfall(row, providersList) CellResult
        +queryProvider(providerName, inputs) ProviderResponse
    }

    class ClaygentRunner {
        +runAgentPrompt(rowContext, instructions) AgentOutput
        +browseWeb(url) String
    }

    Workspace "1" *-- "many" Table : organizes
    Table "1" *-- "many" Column : defines
    Table "1" *-- "many" Row : contains
    WaterfallOrchestrator ..> Column : executes_enrichment
    ClaygentRunner ..> Column : executes_ai
```
"""

clay_db = """
### 2.2 Comprehensive Database Schema Specification

Clay persists dynamic spreadsheets, waterfall configurations, and cell values in scalable document and relational stores.

#### Table: `tables`
Spreadsheet tables holding lead enrichment pipelines.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Table ID |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Table name |
| `workspace_id` | `UUID` | `NOT NULL` | - | Workspace key |
| `created_at` | `TIMESTAMPTZ` | `NOT NULL` | `NOW()` | Creation timestamp |

---

#### Table: `columns`
Column definitions including formulas, waterfall providers, and Claygent prompts.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Column ID |
| `table_id` | `UUID` | `NOT NULL` | - | Parent table |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Column header label |
| `type` | `VARCHAR(50)` | `NOT NULL` | `'text'` | Type (`text`, `enrichment`, `waterfall`, `ai_prompt`) |
| `config` | `JSONB` | `NOT NULL` | `'{}'` | Provider cascading rules and API parameters |
"""

clay_seq = """
### 3.1 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor GrowthRep as Growth Engineer
    participant Clay as Clay Table Engine
    participant Waterfall as WaterfallOrchestrator
    participant Hunter as Hunter.io API
    participant Dropcontact as Dropcontact API
    participant Claygent as Claygent AI Web Agent

    GrowthRep->>Clay: Import 1,000 domains (e.g. acme.com)
    GrowthRep->>Clay: Configure Waterfall Column "Work Email" (Hunter -> Dropcontact)
    
    loop For Each Row
        Clay->>Waterfall: Find work email for John Doe at acme.com
        Waterfall->>Hunter: Query Hunter API
        Hunter-->>Waterfall: Result: null (No email found)
        Waterfall->>Dropcontact: Cascade query to Dropcontact API
        Dropcontact-->>Waterfall: Result: john.doe@acme.com (Verified)
        Waterfall->>Clay: Write cell value "john.doe@acme.com"
        
        Clay->>Claygent: Trigger AI Column: "Find Acme's latest funding announcement"
        Claygent->>Claygent: Live web search & HTML scraping
        Claygent-->>Clay: Write cell value "Raised $25M Series B in June 2025"
    end
    Clay-->>GrowthRep: Table enrichment 100% completed
```
"""

# 3. apollo-io
apollo_cd = """
### 2.1 Core Domain and Service Class Architecture

```mermaid
classDiagram
    class ApolloOrg {
        +UUID id
        +String name
        +Int creditBalance
    }

    class SearchFilterEngine {
        +queryContacts(filters) SearchResults
        +verifyEmailMx(email) VerificationStatus
    }

    class Sequencer {
        +enrollContacts(contactIds, sequenceId) Void
        +dispatchNextStep() Void
    }

    ApolloOrg --> SearchFilterEngine : queries
    ApolloOrg --> Sequencer : runs_outreach
```
"""

apollo_db = """
### 2.2 Comprehensive Database Schema Specification

#### Table: `contacts`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `VARCHAR(100)` | `PRIMARY KEY` | - | Contact ID |
| `first_name` | `VARCHAR(100)` | `NOT NULL` | - | Given name |
| `last_name` | `VARCHAR(100)` | `NOT NULL` | - | Family name |
| `email` | `VARCHAR(255)` | `NOT NULL` | - | Verified email |
| `email_status` | `VARCHAR(50)` | `NOT NULL` | `'verified'` | Status (`verified`, `guessed`, `unavailable`) |
| `company_name` | `VARCHAR(255)` | `NOT NULL` | - | Company |
"""

apollo_seq = """
### 3.1 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor SDR as Sales Rep
    participant Search as Apollo B2B Database (250M Contacts)
    participant Verifier as Live Email Verifier
    participant Sequencer as Apollo Sequencer Engine
    actor Lead as Lead Inbox

    SDR->>Search: Filter: Title='VP Sales', Location='US', Headcount='50-200'
    Search-->>SDR: 2,400 matching verified prospects
    SDR->>Verifier: Request real-time SMTP/MX verification
    Verifier-->>SDR: 98% validity confirmed
    SDR->>Sequencer: Enroll leads in "SaaS Outbound 4-Step Sequence"
    Sequencer->>Lead: Step 1 cold email dispatched
    Sequencer-->>SDR: Leads enrolled and sequence active
```
"""

# 4. lemlist-com
lemlist_cd = """
### 2.1 Core Domain and Service Class Architecture

```mermaid
classDiagram
    class LemlistCampaign {
        +UUID id
        +String name
        +List~Step~ steps
    }

    class DynamicImageEngine {
        +renderImage(templateId, leadData) ImageUrl
    }

    class LemwarmMesh {
        +verifyHealth(mailboxId) Float
    }

    LemlistCampaign --> DynamicImageEngine : generates_media
    LemlistCampaign --> LemwarmMesh : checks_reputation
```
"""

lemlist_db = """
### 2.2 Comprehensive Database Schema Specification

#### Table: `campaigns`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `VARCHAR(50)` | `PRIMARY KEY` | - | Campaign ID |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Campaign title |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'active'` | Campaign state |
"""

lemlist_seq = """
### 3.1 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor Rep as Rep
    participant Engine as DynamicImageEngine
    participant Lemwarm as Lemwarm Health Pool
    participant Outbound as Lemlist Sender
    actor Lead as Prospect

    Rep->>Outbound: Start multi-channel drip
    Outbound->>Lemwarm: Confirm inbox health score >= 90
    Lemwarm-->>Outbound: Score = 98 (Approved)
    Outbound->>Engine: Generate personalized coffee mug image with Lead's logo
    Engine-->>Outbound: Returns dynamic image URL
    Outbound->>Lead: Transmit personalized cold email
```
"""

# 5. heyreach-io
heyreach_cd = """
### 2.1 Core Domain and Service Class Architecture

```mermaid
classDiagram
    class AgencyWorkspace {
        +UUID id
        +String name
        +List~LinkedInSenderAccount~ senders
    }

    class SafeModeGuard {
        +checkDailyLimits(accountId) Boolean
        +rotateProxy(accountId) ProxyConfig
    }

    AgencyWorkspace --> SafeModeGuard : protects_accounts
```
"""

heyreach_db = """
### 2.2 Comprehensive Database Schema Specification

#### Table: `linkedin_accounts`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Sender ID |
| `profile_name` | `VARCHAR(255)` | `NOT NULL` | - | LinkedIn name |
| `daily_invite_limit`| `INTEGER` | `NOT NULL` | `25` | Daily limit |
"""

heyreach_seq = """
### 3.1 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor Agency as Agency Operator
    participant Heyreach as Heyreach Multi-Account Engine
    participant Guard as SafeModeGuard
    participant LinkedIn as LinkedIn Cloud Node

    Agency->>Heyreach: Launch campaign across 10 LinkedIn accounts
    Heyreach->>Guard: Verify individual account safe sending limits
    Guard-->>Heyreach: All 10 accounts approved with dedicated 4G residential proxies
    loop Distributed Outreach
        Heyreach->>LinkedIn: Dispatch 20 connection requests per account per day
        LinkedIn-->>Heyreach: Connection requests sent with human randomized delays
    end
```
"""

# 6. respond-io
respond_cd = """
### 2.1 Core Domain and Service Class Architecture

```mermaid
classDiagram
    class Space {
        +UUID id
        +String name
        +List~Channel~ channels
    }

    class WorkflowBot {
        +executeNode(node, contact) Void
    }

    Space --> WorkflowBot : runs_automation
```
"""

respond_db = """
### 2.2 Comprehensive Database Schema Specification

#### Table: `conversations`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Conversation ID |
| `channel_type` | `VARCHAR(50)` | `NOT NULL` | - | Channel (`whatsapp`, `telegram`, `messenger`) |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'open'` | Status |
"""

respond_seq = """
### 3.1 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor Customer as WhatsApp User
    participant Gateway as WhatsApp Cloud API
    participant Respond as Respond.io Server
    participant Bot as WorkflowBot
    actor Agent as Support Agent

    Customer->>Gateway: Sends WhatsApp message ("Hello, I need pricing")
    Gateway->>Respond: Inbound webhook payload
    Respond->>Bot: Trigger automated qualification flow
    Bot-->>Customer: "What is your company size?"
    Customer-->>Bot: "50-100 employees"
    Bot->>Respond: Assign conversation to Enterprise Sales queue
    Respond-->>Agent: Ring agent workstation with conversation context
```
"""

# 7. sent-dm
sentdm_cd = """
### 2.1 Core Domain and Service Class Architecture

```mermaid
classDiagram
    class InstagramProfile {
        +String username
        +String igId
    }

    class DmCampaign {
        +String keywordTrigger
        +String responseText
    }

    InstagramProfile --> DmCampaign : runs
```
"""

sentdm_db = """
### 2.2 Comprehensive Database Schema Specification

#### Table: `dm_campaigns`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Campaign ID |
| `keyword_trigger` | `VARCHAR(100)` | `NOT NULL` | - | Trigger word (e.g. `GROWTH`) |
| `response_message`| `TEXT` | `NOT NULL` | - | Automated DM response |
"""

sentdm_seq = """
### 3.1 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor Follower as Instagram Follower
    participant IG as Meta Instagram Graph API
    participant Sent as Sent.dm Webhook Engine

    Follower->>IG: Comments "GROWTH" on Creator's Reel
    IG->>Sent: Webhook: `comment_created` (text: "GROWTH")
    Sent->>Sent: Match keyword trigger
    Sent->>IG: POST /me/messages (Send direct message with free guide link)
    IG-->>Follower: Automated DM delivered in 2 seconds
```
"""

# 8. hubspot
hubspot_cd = """
### 2.1 Core Domain and Service Class Architecture

```mermaid
classDiagram
    class HubSpotPortal {
        +BigInt portalId
        +String domain
        +List~Contact~ contacts
        +List~Deal~ deals
    }

    class Contact {
        +BigInt id
        +String email
        +String firstname
        +String lifecycleStage
    }

    class Deal {
        +BigInt id
        +String dealname
        +Decimal amount
        +String dealstage
    }

    class WorkflowEngine {
        +enrollObject(objectType, objectId, workflowId) Void
    }

    HubSpotPortal "1" *-- "many" Contact : stores
    HubSpotPortal "1" *-- "many" Deal : tracks
    WorkflowEngine ..> Contact : automates
```
"""

hubspot_db = """
### 2.2 Comprehensive Database Schema Specification

HubSpot coordinates CRM records, multi-touch events, and deal pipelines within multi-tenant schemas.

#### Table: `contacts`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `BIGINT` | `PRIMARY KEY` | - | Contact record ID |
| `portal_id` | `BIGINT` | `NOT NULL` | - | Customer portal ID |
| `email` | `VARCHAR(255)` | `NOT NULL` | - | Email address |
| `firstname` | `VARCHAR(100)` | `NULLABLE` | `NULL` | First name |
| `lifecycle_stage`| `VARCHAR(50)` | `NOT NULL` | `'lead'` | Stage (`subscriber`, `lead`, `mql`, `sql`, `customer`) |

*Indexes:* `idx_hs_email` (`(email)`), `idx_hs_portal` (`(portal_id)`).
"""

hubspot_seq = """
### 3.1 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor Visitor as Web Visitor
    participant Form as HubSpot Embedded Form
    participant Core as HubSpot Core Engine
    participant Workflow as Workflow Engine
    actor Rep as Sales Representative

    Visitor->>Form: Submits eBook download form
    Form->>Core: Ingest contact data
    Core->>Core: Deduplicate contact by email
    Core->>Workflow: Enroll contact in "Nurture Workflow"
    Workflow->>Core: Update lifecycle_stage = 'MQL'
    Workflow->>Rep: Create sales task: "Follow up with new MQL"
    Core-->>Visitor: Deliver eBook via automated email
```
"""

# 9. gohighlevel
ghl_cd = """
### 2.1 Core Domain and Service Class Architecture

```mermaid
classDiagram
    class Agency {
        +String id
        +String name
        +List~SubAccount~ subAccounts
    }

    class SubAccount {
        +String id
        +String name
        +String twilioSid
    }

    class GhlWorkflow {
        +String id
        +JSON triggerConditions
        +List~Action~ actions
    }

    Agency "1" *-- "many" SubAccount : provisions
    SubAccount "1" *-- "many" GhlWorkflow : executes
```
"""

ghl_db = """
### 2.2 Comprehensive Database Schema Specification

#### Table: `sub_accounts`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `VARCHAR(64)` | `PRIMARY KEY` | - | Location / SubAccount ID |
| `agency_id` | `VARCHAR(64)` | `NOT NULL` | - | Parent Agency |
| `business_name` | `VARCHAR(255)` | `NOT NULL` | - | Business client title |
| `phone_number` | `VARCHAR(50)` | `NULLABLE` | `NULL` | Provisioned Twilio number |
"""

ghl_seq = """
### 3.1 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor Lead as Facebook Lead Ad
    participant Webhook as GHL Webhook Ingestion
    participant Workflow as GHL Automation Engine
    participant Twilio as Twilio Gateway
    actor Agent as Local Business Owner

    Lead->>Webhook: Facebook Lead Form Submitted
    Webhook->>Workflow: Trigger: "New Lead Form"
    Workflow->>Twilio: Immediate SMS to Lead ("Hi! Thanks for contacting us.")
    Workflow->>Twilio: Automated Call Bridge to Agent ("Press 1 to connect with Lead")
    Agent-->>Twilio: Agent presses 1
    Twilio->>Lead: Rings Lead phone and bridges conversation
```
"""

# 10. kartra
kartra_cd = """
### 2.1 Core Domain and Service Class Architecture

```mermaid
classDiagram
    class KartraAccount {
        +String id
        +String companyName
    }

    class KartraFunnel {
        +String id
        +String name
        +List~Page~ pages
    }

    KartraAccount "1" *-- "many" KartraFunnel : owns
```
"""

kartra_db = """
### 2.2 Comprehensive Database Schema Specification

#### Table: `funnels`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `VARCHAR(64)` | `PRIMARY KEY` | - | Funnel ID |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Title |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'active'` | Status |
"""

kartra_seq = """
### 3.1 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor Buyer as Buyer
    participant Checkout as Kartra Checkout Page
    participant Billing as Stripe / PayPal
    participant Member as Membership System

    Buyer->>Checkout: Submits order ($297 Course)
    Checkout->>Billing: Process transaction
    Billing-->>Checkout: Payment approved
    Checkout->>Member: Provision membership login credentials
    Member-->>Buyer: Welcome email with access link
```
"""

# 11. systeme-io
systeme_cd = """
### 2.1 Core Domain and Service Class Architecture

```mermaid
classDiagram
    class SystemeAccount {
        +BigInt id
        +String email
        +List~Funnel~ funnels
    }

    class Funnel {
        +BigInt id
        +String name
        +List~Step~ steps
    }

    SystemeAccount "1" *-- "many" Funnel : owns
```
"""

systeme_db = """
### 2.2 Comprehensive Database Schema Specification

#### Table: `funnels`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `BIGINT` | `PRIMARY KEY` | - | Funnel ID |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Funnel name |
"""

systeme_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor Visitor as Visitor
    participant Step1 as Squeeze Page
    participant DB as Systeme Database
    participant Step2 as Thank You Page

    Visitor->>Step1: Enter email for free checklist
    Step1->>DB: Add email to newsletter list
    Step1-->>Visitor: 302 Redirect to Order Bump Page
```
"""

# 12. zoho-marketing-plus
zoho_cd = """
### 2.1 Core Domain and Service Class Architecture

```mermaid
classDiagram
    class ZohoOrganization {
        +BigInt orgId
        +String companyName
    }

    class AttributionEngine {
        +calculateMultiTouchAttribution(dealId) AttributionReport
    }

    ZohoOrganization --> AttributionEngine : utilizes
```
"""

zoho_db = """
### 2.2 Comprehensive Database Schema Specification

#### Table: `campaign_touchpoints`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `BIGINT` | `PRIMARY KEY` | - | Touchpoint ID |
| `lead_id` | `BIGINT` | `NOT NULL` | - | Lead ID |
| `channel` | `VARCHAR(50)` | `NOT NULL` | - | Channel |
| `weight` | `FLOAT` | `NOT NULL` | `1.0` | Attribution weight |
"""

zoho_seq = """
### 3.1 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor Prospect as Buyer
    participant Ads as Google Ads / Webinar
    participant Zoho as Zoho Marketing Plus
    participant CRM as Zoho CRM

    Prospect->>Ads: Clicks Google Ad (Touchpoint 1)
    Prospect->>Ads: Attends Webinar (Touchpoint 2)
    Prospect->>Zoho: Books Demo (Touchpoint 3)
    Zoho->>CRM: Deal Won ($50,000)
    Zoho->>Zoho: Calculate W-Shaped Attribution across Touchpoints
```
"""

# Execute insertions
insert_commercial('docs/wiki/instantly-ai.md', instantly_cd, instantly_db, instantly_seq)
insert_commercial('docs/wiki/clay-com.md', clay_cd, clay_db, clay_seq)
insert_commercial('docs/wiki/apollo-io.md', apollo_cd, apollo_db, apollo_seq)
insert_commercial('docs/wiki/lemlist-com.md', lemlist_cd, lemlist_db, lemlist_seq)
insert_commercial('docs/wiki/heyreach-io.md', heyreach_cd, heyreach_db, heyreach_seq)
insert_commercial('docs/wiki/respond-io.md', respond_cd, respond_db, respond_seq)
insert_commercial('docs/wiki/sent-dm.md', sentdm_cd, sentdm_db, sentdm_seq)

insert_commercial('docs/wiki/hubspot.md', hubspot_cd, hubspot_db, hubspot_seq)
insert_commercial('docs/wiki/gohighlevel.md', ghl_cd, ghl_db, ghl_seq)
insert_commercial('docs/wiki/kartra.md', kartra_cd, kartra_db, kartra_seq)
insert_commercial('docs/wiki/systeme-io.md', systeme_cd, systeme_db, systeme_seq)
insert_commercial('docs/wiki/zoho-marketing-plus.md', zoho_cd, zoho_db, zoho_seq)

# Mirror the 7 commercial wikis to inspire/TomoriBot/docs/en/wiki/
import shutil
os.makedirs('inspire/TomoriBot/docs/en/wiki', exist_ok=True)
mirrored = ['instantly-ai.md', 'clay-com.md', 'apollo-io.md', 'lemlist-com.md', 'heyreach-io.md', 'respond-io.md', 'sent-dm.md']
for m in mirrored:
    src = os.path.join('docs/wiki', m)
    dst = os.path.join('inspire/TomoriBot/docs/en/wiki', m)
    shutil.copy2(src, dst)
    print(f"Mirrored {m} to {dst}")
