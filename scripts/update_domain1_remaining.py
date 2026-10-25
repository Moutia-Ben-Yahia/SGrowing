import os
import re

def insert_sections(filepath, class_diagram, db_schema, seq_diagram):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    # 1. Insert Class Diagram into Section 2
    if 'classDiagram' not in content:
        # Locate end of Section 2's flowchart or section 3 start
        sec3_match = re.search(r'\n## 3\.\s+Technology Stack', content)
        if sec3_match:
            idx = sec3_match.start()
            content = content[:idx] + "\n" + class_diagram.strip() + "\n\n---\n" + content[idx:]
        else:
            print(f"Warning: Section 3 not found in {filepath}")

    # 2. Insert Detailed DB Schema into Section 6
    if 'Comprehensive Database Schema Specification' not in content:
        sec7_match = re.search(r'\n## 7\.\s+API Architecture', content)
        if sec7_match:
            idx = sec7_match.start()
            content = content[:idx] + "\n" + db_schema.strip() + "\n\n---\n" + content[idx:]
        else:
            print(f"Warning: Section 7 not found in {filepath}")

    # 3. Insert Sequence Diagram into Section 7
    if 'sequenceDiagram' not in content:
        sec8_match = re.search(r'\n## 8\.\s+Strategic Value', content)
        if sec8_match:
            idx = sec8_match.start()
            content = content[:idx] + "\n" + seq_diagram.strip() + "\n\n---\n" + content[idx:]
        else:
            print(f"Warning: Section 8 not found in {filepath}")

    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(content)
    print(f"Updated {filepath} successfully!")

# ==========================================================
# 1. OpenOutreach
# ==========================================================
openoutreach_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class SiteConfig {
        +Int id
        +String product_docs
        +String campaign_target
        +String ai_model
        +String llm_api_key
        +String apollo_api_key
        +String bettercontact_api_key
        +String operator_name
        +String operator_email
        +export() Dict~String, String~
    }

    class LeadFinderAgent {
        +SiteConfig config
        +discoverTargetAccounts(query) List~Account~
        +extractProspectRoles(domain, titles) List~Prospect~
        +enrichContactDetails(prospect) EnrichedLead
    }

    class OutreachSenderAgent {
        +SiteConfig config
        +String smtpHost
        +Int dailyMailboxLimit
        +verifyMailboxCapacity() Boolean
        +generatePersonalizedDraft(lead) EmailDraft
        +dispatchColdEmail(draft) SendReceipt
    }

    class ApolloProvider {
        +String apiKey
        +searchPeople(domain, titles) List~ApolloPerson~
        +enrichEmail(personId) String
    }

    class BetterContactProvider {
        +String apiKey
        +waterfallEnrichment(email, name, company) WaterfallResult
    }

    class Lead {
        +UUID id
        +String email
        +String firstName
        +String lastName
        +String company
        +String linkedinUrl
        +String status
        +markSent() Void
        +markReplied() Void
    }

    class Campaign {
        +UUID id
        +String name
        +String status
        +List~Step~ steps
        +addLead(leadId) Void
    }

    class OutreachLog {
        +UUID id
        +UUID leadId
        +String action
        +String provider
        +Timestamp timestamp
        +String payload
    }

    LeadFinderAgent --> SiteConfig : reads
    OutreachSenderAgent --> SiteConfig : reads
    LeadFinderAgent --> ApolloProvider : queries
    LeadFinderAgent --> BetterContactProvider : waterfall
    Campaign "1" *-- "many" Lead : enrolls
    OutreachSenderAgent ..> Lead : sends_to
    OutreachSenderAgent --> OutreachLog : audits
```
"""

openoutreach_db = """
### 6.1 Comprehensive Database Schema Specification

The underlying persistence architecture utilizes Django ORM with SQLite or PostgreSQL backends, strictly separating operator configurations from transactional outreach logs.

#### Table: `config_siteconfig`
Persists operator inputs collected through the interactive CLI wizard and exports them as environment variables for child processes (`openoutfind` and `openoutsend`).

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `INTEGER` | `PRIMARY KEY AUTOINCREMENT` | - | Primary configuration row identifier |
| `product_docs` | `TEXT` | `NOT NULL` | `''` | Value proposition, product markdown, and persona docs |
| `campaign_target` | `TEXT` | `NOT NULL` | `''` | Target ICP criteria (e.g. VP Sales, B2B SaaS, US/EU) |
| `ai_model` | `VARCHAR(100)` | `NOT NULL` | `'gpt-4o'` | LLM identifier for copywriting and scoring |
| `llm_api_key` | `VARCHAR(255)` | `NOT NULL` | `''` | API credential for OpenAI, Anthropic, or OpenRouter |
| `apollo_api_key` | `VARCHAR(255)` | `NOT NULL` | `''` | Apollo.io credential for prospect discovery |
| `bettercontact_api_key`| `VARCHAR(255)`| `NOT NULL` | `''` | Waterfall email enrichment credential |
| `operator_name` | `VARCHAR(255)` | `NOT NULL` | `''` | Human operator name appearing in signatures |
| `operator_email` | `VARCHAR(255)` | `NOT NULL` | `''` | Primary administrative contact and sender identity |
| `operator_country_code`| `VARCHAR(10)` | `NOT NULL` | `''` | Jurisdiction ISO code for CAN-SPAM / GDPR compliance |

*Indexes:* Primary key index on `id`.

---

#### Table: `leads_lead`
Stores prospect contacts discovered by `openoutfind` alongside enrichment metadata.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `uuid4` | Unique prospect contact identifier |
| `email` | `VARCHAR(255)` | `NOT NULL, UNIQUE` | - | Primary verified business email address |
| `first_name` | `VARCHAR(100)` | `NOT NULL` | `''` | Prospect given name |
| `last_name` | `VARCHAR(100)` | `NOT NULL` | `''` | Prospect family name |
| `company_name` | `VARCHAR(255)` | `NOT NULL` | `''` | Employer organization name |
| `job_title` | `VARCHAR(255)` | `NOT NULL` | `''` | Professional title (e.g. Head of Growth) |
| `linkedin_url` | `VARCHAR(500)` | `NULLABLE` | `NULL` | Public LinkedIn profile URL |
| `enrichment_score`| `FLOAT` | `NOT NULL` | `0.0` | ICP fit and email confidence score (0.0 - 1.0) |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'discovered'`| Pipeline state (`discovered`, `verified`, `sent`, `replied`) |
| `created_at` | `TIMESTAMPTZ` | `NOT NULL` | `NOW()` | Discovery timestamp |

*Indexes:* `idx_lead_email` (`UNIQUE btree (email)`), `idx_lead_status` (`btree (status)`).

---

#### Table: `outreach_log`
Append-only operational audit log tracking every email transmission, bounce, or reply event.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `uuid4` | Unique audit event identifier |
| `lead_id` | `UUID` | `NOT NULL, FK -> leads_lead(id)` | - | Target prospect identifier |
| `action` | `VARCHAR(50)` | `NOT NULL` | - | Action type (`email_sent`, `open`, `reply`, `bounce`) |
| `subject` | `VARCHAR(500)` | `NOT NULL` | `''` | Subject line dispatched |
| `smtp_status` | `INTEGER` | `NOT NULL` | `250` | SMTP response code received from provider |
| `error_message` | `TEXT` | `NULLABLE` | `NULL` | Error details if dispatch failed |
| `created_at` | `TIMESTAMPTZ` | `NOT NULL` | `NOW()` | Event timestamp |

*Indexes:* `idx_outreach_lead` (`btree (lead_id)`), `idx_outreach_action_created` (`btree (action, created_at)`).
"""

openoutreach_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

The following sequence illustrates the complete OpenOutreach workflow from CLI configuration, Apollo/BetterContact waterfall enrichment, LLM copy generation, and SMTP dispatch.

```mermaid
sequenceDiagram
    autonumber
    actor Operator as Sales Operator
    participant Wizard as CLI Wizard (wizard.py)
    participant DB as SQLite / PostgreSQL
    participant Finder as openoutfind Agent
    participant Apollo as Apollo.io API
    participant BetterContact as BetterContact Waterfall
    participant LLM as OpenAI / Anthropic
    participant Sender as openoutsend Agent
    participant SMTP as SMTP Relay Server

    Operator->>Wizard: Run `python -m openoutreach.wizard`
    Wizard->>Operator: Prompt for ICP, Product Docs & API Keys
    Operator-->>Wizard: Submits credentials & target criteria
    Wizard->>DB: INSERT / UPDATE config_siteconfig
    DB-->>Wizard: Config persisted
    
    Operator->>Finder: Execute lead generation task
    Finder->>DB: Read config_siteconfig -> export env vars
    Finder->>Apollo: Query prospects matching ICP criteria
    Apollo-->>Finder: Candidate lead profiles (name, company, domain)
    
    loop For Each Candidate Lead
        Finder->>BetterContact: Waterfall email verification
        BetterContact-->>Finder: Verified business email (98% confidence)
        Finder->>DB: INSERT into leads_lead (status='verified')
    end

    Operator->>Sender: Execute outreach send run
    Sender->>DB: Read verified leads WHERE status='verified'
    
    loop For Each Eligible Lead
        Sender->>Sender: Verify daily mailbox sending limit
        Sender->>LLM: Generate personalized email copy(lead, product_docs)
        LLM-->>Sender: Return personalized subject & body
        Sender->>SMTP: Send message via authenticated SMTP
        SMTP-->>Sender: 250 OK (Message Accepted)
        Sender->>DB: UPDATE leads_lead SET status='sent'
        Sender->>DB: INSERT into outreach_log (lead_id, action='email_sent')
    end
    Sender-->>Operator: Send run complete summary
```
"""

# ==========================================================
# 2. linkedin-outreach-mcp
# ==========================================================
linkedin_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class HeyLeadClient {
        +String baseUrl
        +String apiKey
        +sendConnectionRequest(profileId, note) Promise~Result~
        +sendDirectMessage(chatId, message) Promise~Result~
        +getConversationHistory(chatId) List~Message~
        +checkAccountHealth() HealthReport
    }

    class OutreachScheduler {
        +SQLiteConnection db
        +planDailyActions(date) List~Action~
        +enforceSlotReservations() Boolean
        +dispatchNextAction() Promise~Void~
    }

    class SignalClassifier {
        +String model
        +detectSignals(postActivity) List~Signal~
        +scoreIntent(events) Float
        +classifyJobChanges(profile) JobChangeSignal
    }

    class DecisionEngine {
        +PromptRegistry registry
        +evaluateDecision(contact, signals) AgentDecision
        +generateMessageVariant(contact, prompt) String
        +recordDecisionOutcome(decisionId, outcome) Void
    }

    class RateLimiter {
        +Int maxDailyInvites
        +Int maxDailyMessages
        +canExecute(actionType) Boolean
        +recordExecution(actionType) Void
    }

    class Contact {
        +String id
        +String linkedinUrn
        +String firstName
        +String lastName
        +String company
        +String headline
        +String status
    }

    class Outreach {
        +String id
        +String contactId
        +String campaignId
        +String actionType
        +String status
        +Timestamp scheduledFor
    }

    class AgentDecision {
        +String id
        +String contactId
        +String selectedAction
        +String reasoning
        +Float confidenceScore
    }

    OutreachScheduler --> HeyLeadClient : delegates dispatch
    OutreachScheduler --> RateLimiter : checks limits
    DecisionEngine --> SignalClassifier : inspects signals
    DecisionEngine ..> AgentDecision : outputs
    OutreachScheduler --> Outreach : updates
    Outreach "1" *-- "1" Contact : targets
    AgentDecision --> Outreach : authorizes
```
"""

linkedin_db = """
### 6.1 Comprehensive Database Schema Specification

The storage layer is built on SQLite with PRAGMA versioning (version 16) and transactional retry backoffs handling concurrent access across MCP tool calls.

#### Table: `contacts`
Stores LinkedIn prospect profiles enriched with targeting signals and fit evaluations.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `TEXT` | `PRIMARY KEY` | `uuid4` | Unique internal contact record ID |
| `linkedin_urn` | `TEXT` | `NOT NULL, UNIQUE` | - | Unique LinkedIn Member URN |
| `first_name` | `TEXT` | `NOT NULL` | `''` | Prospect given name |
| `last_name` | `TEXT` | `NOT NULL` | `''` | Prospect family name |
| `headline` | `TEXT` | `NULLABLE` | `NULL` | Public headline / current professional summary |
| `company` | `TEXT` | `NULLABLE` | `NULL` | Current employer name |
| `profile_json` | `TEXT` | `NULLABLE` | `NULL` | Raw JSON dump from profile scraper |
| `why_json` | `TEXT` | `NULLABLE` | `NULL` | AI scoring rationale explaining ICP alignment |
| `status` | `TEXT` | `NOT NULL` | `'new'` | Lifecycle state (`new`, `connecting`, `connected`, `messaged`) |
| `created_at` | `INTEGER` | `NOT NULL` | `unixepoch` | Record creation timestamp |

*Indexes:* `idx_contacts_urn` (`UNIQUE (linkedin_urn)`), `idx_contacts_status` (`(status)`).

---

#### Table: `outreaches`
Execution ledger for queued, active, and completed LinkedIn outreach actions.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `TEXT` | `PRIMARY KEY` | `uuid4` | Outreach interaction identifier |
| `contact_id` | `TEXT` | `NOT NULL, FK -> contacts(id)` | - | Recipient contact reference |
| `campaign_id` | `TEXT` | `NOT NULL` | - | Associated outreach campaign |
| `chat_id` | `TEXT` | `NULLABLE` | `NULL` | LinkedIn conversation thread ID |
| `decision_id` | `TEXT` | `NULLABLE` | `NULL` | AI decision record authorizing this step |
| `action_type` | `TEXT` | `NOT NULL` | `'invite'` | Action (`invite`, `message`, `inmail`, `view_profile`) |
| `status` | `TEXT` | `NOT NULL` | `'scheduled'` | Execution status (`scheduled`, `sent`, `failed`, `replied`) |
| `scheduled_for` | `INTEGER` | `NOT NULL` | - | Unix timestamp for scheduled dispatch |
| `sent_at` | `INTEGER` | `NULLABLE` | `NULL` | Unix timestamp of completed execution |

*Indexes:* `idx_outreaches_contact` (`(contact_id)`), `idx_outreaches_sched` (`(status, scheduled_for)`).

---

#### Table: `signals`
Stores real-time buying signals detected from prospect LinkedIn behavior.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `TEXT` | `PRIMARY KEY` | `uuid4` | Signal event identifier |
| `contact_id` | `TEXT` | `NOT NULL, FK -> contacts(id)` | - | Contact triggering the signal |
| `signal_type` | `TEXT` | `NOT NULL` | - | Type (`job_change`, `post_engagement`, `company_funding`) |
| `source_url` | `TEXT` | `NULLABLE` | `NULL` | Originating post or announcement URL |
| `score` | `REAL` | `NOT NULL` | `1.0` | Algorithmic signal strength weight |
| `detected_at` | `INTEGER` | `NOT NULL` | `unixepoch` | Detection timestamp |
| `status` | `TEXT` | `NOT NULL` | `'new'` | Processing status (`new`, `processed`, `ignored`) |

*Indexes:* `idx_signals_status_detected` (`(status, detected_at)`).
"""

linkedin_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

The following sequence details how the MCP client invokes LinkedIn outreach tools, evaluates intent signals, runs AI decision reasoning, and controls rate limits.

```mermaid
sequenceDiagram
    autonumber
    actor Client as AI Agent / Claude Client
    participant MCP as HeyLead MCP Server
    participant DB as SQLite Database
    participant Classifier as Signal Classifier
    participant Decision as Decision Engine
    participant RateLimiter as Rate Limiter
    participant HeyLead as HeyLead / LinkedIn Bridge

    Client->>MCP: Call tool: `plan_daily_outreach(campaign_id)`
    MCP->>DB: Query candidate contacts & detected signals
    DB-->>MCP: Return active signals (job change, recent post)
    
    loop For Each Contact
        MCP->>Classifier: Evaluate signal relevance & score intent
        Classifier-->>MCP: Signal weight = 0.85 (High Intent)
        MCP->>Decision: Generate outreach message variant & action type
        Decision-->>MCP: Action: send_invite with custom note
        MCP->>RateLimiter: Check daily invite budget
        alt Budget Available
            RateLimiter-->>MCP: Approved (18/25 used today)
            MCP->>DB: INSERT into outreaches (status='scheduled')
            MCP->>DB: INSERT into agent_decisions
        else Daily Limit Exceeded
            RateLimiter-->>MCP: Rejected (25/25 limit reached)
            MCP->>DB: Postpone outreach to next calendar slot
        end
    end

    Client->>MCP: Call tool: `execute_pending_outreach()`
    MCP->>DB: SELECT next scheduled outreach
    MCP->>HeyLead: Dispatch connection request with personalized note
    HeyLead-->>MCP: 200 OK (Action Queued)
    MCP->>DB: UPDATE outreaches SET status='sent', sent_at=NOW()
    MCP->>RateLimiter: Increment executed counter
    MCP-->>Client: Report execution success
```
"""

# ==========================================================
# 3. Open-Cold-Dialer
# ==========================================================
ocd_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class DialerService {
        +DrizzleDB db
        +TwilioClient twilio
        +startCampaignDialing(campaignId) Promise~Void~
        +pauseCampaignDialing(campaignId) Promise~Void~
        +handleCallConnected(callSid, leadId) Promise~Void~
        +handleAmdResult(callSid, result) Promise~Void~
    }

    class AgentSession {
        +String agentId
        +String socketId
        +String currentCallSid
        +Boolean isAvailable
        +connectSoftphone(webrtcToken) Void
        +transferCall(targetNumber) Promise~Void~
        +hangup() Promise~Void~
    }

    class CallQueueManager {
        +getEligibleLeads(campaignId, batchSize) List~Lead~
        +lockLeadForDialing(leadId) Boolean
        +releaseLead(leadId) Void
    }

    class TwilioVoiceAdapter {
        +String accountSid
        +String authToken
        +initiateOutboundCall(to, from, webhookUrl) CallInstance
        +generateTwimlResponse(dialInstruction) String
        +enableAnsweringMachineDetection(callSid) Void
    }

    class DispositionsHandler {
        +logDisposition(callLogId, disposition, notes) Promise~Void~
        +scheduleFollowUp(leadId, followUpAt) Promise~Void~
    }

    class Campaign {
        +String id
        +String name
        +String type
        +String status
        +String settings
    }

    class Lead {
        +String id
        +String campaignId
        +String phoneNumber
        +String firstName
        +String status
    }

    class CallLog {
        +String id
        +String campaignId
        +String leadId
        +String agentId
        +Int duration
        +String disposition
        +String recordingUrl
    }

    DialerService --> TwilioVoiceAdapter : initiates telephony
    DialerService --> CallQueueManager : fetches prospects
    DialerService --> AgentSession : bridges active human
    DispositionsHandler --> CallLog : writes outcomes
    Campaign "1" *-- "many" Lead : targets
    CallLog "many" --> "1" Lead : references
```
"""

ocd_db = """
### 6.1 Comprehensive Database Schema Specification

The persistence layer uses Drizzle ORM configured with SQLite or PostgreSQL to handle high-frequency call signaling and disposition tracking.

#### Table: `profiles`
User accounts, roles, and softphone agent configurations.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `TEXT` | `PRIMARY KEY` | - | Unique user account ID |
| `email` | `TEXT` | `NOT NULL, UNIQUE` | - | Operator email address |
| `full_name` | `TEXT` | `NULLABLE` | `NULL` | Agent display name |
| `role` | `TEXT` | `NOT NULL` | `'agent'` | System role (`admin`, `agent`, `manager`) |
| `created_at` | `TEXT` | `NOT NULL` | `ISO8601` | Account creation timestamp |
| `updated_at` | `TEXT` | `NOT NULL` | `ISO8601` | Account modification timestamp |

*Indexes:* `idx_profiles_email` (`UNIQUE (email)`).

---

#### Table: `campaigns`
Dialing campaign configurations, pacing parameters, and caller ID assignments.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `TEXT` | `PRIMARY KEY` | - | Unique campaign ID |
| `name` | `TEXT` | `NOT NULL` | - | Campaign display label |
| `type` | `TEXT` | `NOT NULL` | `'outbound'` | Dialing mode (`outbound`, `inbound`, `blended`) |
| `status` | `TEXT` | `NOT NULL` | `'active'` | State (`active`, `paused`, `completed`) |
| `settings` | `TEXT` | `NULLABLE` | `NULL` | JSON string defining AMD thresholds and retry logic |
| `created_by` | `TEXT` | `NULLABLE` | `NULL` | Creator user ID |
| `created_at` | `TEXT` | `NOT NULL` | `ISO8601` | Creation timestamp |

*Indexes:* `idx_campaigns_status` (`(status)`).

---

#### Table: `call_logs`
Records every outbound call attempt, duration, AMD outcome, and agent disposition.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `TEXT` | `PRIMARY KEY` | - | Call record ID |
| `campaign_id` | `TEXT` | `NOT NULL` | - | Associated campaign |
| `lead_id` | `TEXT` | `NOT NULL` | - | Target lead called |
| `agent_id` | `TEXT` | `NULLABLE` | `NULL` | Softphone agent assigned |
| `call_sid` | `TEXT` | `NOT NULL` | - | Twilio / Telnyx Call SID |
| `duration` | `INTEGER` | `NOT NULL` | `0` | Call duration in seconds |
| `status` | `TEXT` | `NOT NULL` | `'initiated'` | Telephony status (`initiated`, `ringing`, `in-progress`, `completed`) |
| `amd_status` | `TEXT` | `NULLABLE` | `NULL` | AMD result (`human`, `machine_start`, `nonesuch`) |
| `disposition` | `TEXT` | `NULLABLE` | `NULL` | Agent disposition (`interested`, `callback`, `dnc`, `no_answer`) |
| `recording_url`| `TEXT` | `NULLABLE` | `NULL` | S3 / Twilio audio recording URL |
| `created_at` | `TEXT` | `NOT NULL` | `ISO8601` | Call initiation timestamp |

*Indexes:* `idx_call_logs_lead` (`(lead_id)`), `idx_call_logs_sid` (`(call_sid)`).
"""

ocd_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

The following sequence shows the end-to-end power dialing loop with Answering Machine Detection (AMD) and WebRTC softphone bridging.

```mermaid
sequenceDiagram
    autonumber
    actor Agent as Sales Rep (Softphone)
    participant Dialer as Dialer Service
    participant DB as Drizzle Database
    participant Twilio as Twilio Voice Gateway
    actor Prospect as Lead Phone

    Agent->>Dialer: Open softphone WebRTC & set status='ready'
    Dialer->>DB: Query eligible leads for active campaign
    DB-->>Dialer: Return next lead (phone: +15551234567)
    
    Dialer->>Twilio: POST /Calls (To: Lead, MachineDetection: 'Enable')
    Twilio-->>Dialer: Call SID initiated (status='ringing')
    Twilio->>Prospect: Cellular PSTN Ringing...
    
    Prospect-->>Twilio: Answers call ("Hello?")
    Twilio->>Twilio: AMD Audio Analysis (Voice vs Voicemail Beep)
    
    alt Human Detected
        Twilio-->>Dialer: Webhook: AMD Result = 'human'
        Dialer->>Twilio: Return TwiML <Dial><Client>Agent_ID</Client></Dial>
        Twilio->>Agent: Bridge WebRTC audio to Agent softphone
        Note over Agent, Prospect: Live Sales Conversation Occurs
        Agent-->>Dialer: Hang up call
        Dialer->>Twilio: Terminate PSTN Leg
        Agent->>Dialer: Submit Disposition ("Interested - Send Proposal")
        Dialer->>DB: UPDATE call_logs (duration, disposition, recording_url)
        Dialer->>DB: UPDATE leads SET status='contacted'
    else Voicemail / Answering Machine Detected
        Twilio-->>Dialer: Webhook: AMD Result = 'machine_start'
        Dialer->>Twilio: Play automated voicemail drop or Hangup
        Dialer->>DB: UPDATE call_logs (disposition='voicemail')
        Dialer->>Dialer: Fetch next lead immediately
    end
```
"""

# ==========================================================
# 4. ringee-app
# ==========================================================
ringee_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class VoiceAgentService {
        +PrismaClient prisma
        +TelnyxClient telnyx
        +handleIncomingCall(callPayload) Promise~TwiML~
        +dispatchToVoiceAgent(agentId, callSid) Promise~Void~
        +streamAudioToTranscription(audioStream) WebSocket
    }

    class SipDeviceManager {
        +registerSipDevice(orgId, sipCredentials) SipDevice
        +routeCallToSipDevice(callId, deviceId) Promise~Void~
        +checkDeviceRegistrationStatus(sipUsername) Boolean
    }

    class CallSessionCoordinator {
        +UUID callId
        +String telnyxCallControlId
        +String callerNumber
        +String recipientNumber
        +bridgeToAgent(agentWebRtcToken) Void
        +hangup() Promise~Void~
        +recordSegment(speaker, text, duration) Void
    }

    class TelnyxClient {
        +String apiKey
        +answerCall(controlId) Promise~Void~
        +forkAudioStream(controlId, streamUrl) Promise~Void~
        +speakText(controlId, text, voice) Promise~Void~
    }

    class TranscriptionPipeline {
        +DeepgramClient deepgram
        +processPcmAudio(pcmChunk) Void
        +onTranscriptionSegment(segment) SegmentEvent
    }

    class User {
        +String id
        +String email
        +String role
    }

    class Organization {
        +String id
        +String name
        +List~User~ members
    }

    class Call {
        +String id
        +String organizationId
        +String from
        +String to
        +Int duration
        +String status
    }

    class CallTranscriptionSegment {
        +String id
        +String callId
        +String speaker
        +String text
        +Float startTime
        +Float endTime
    }

    VoiceAgentService --> TelnyxClient : telephony control
    VoiceAgentService --> TranscriptionPipeline : audio streaming
    CallSessionCoordinator --> SipDeviceManager : routes endpoints
    Call "1" *-- "many" CallTranscriptionSegment : logs
    Organization "1" *-- "many" Call : owns
```
"""

ringee_db = """
### 6.1 Comprehensive Database Schema Specification

The database architecture is built with PostgreSQL and defined through Prisma ORM, providing real-time multi-tenant telephony and billing management.

#### Table: `Organization`
Tenant workspace entity managing enterprise phone numbers, users, and routing policies.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `TEXT` | `PRIMARY KEY` | `cuid()` | Unique organization identifier |
| `name` | `TEXT` | `NOT NULL` | - | Business name of the tenant |
| `createdAt` | `TIMESTAMP(3)` | `NOT NULL` | `CURRENT_TIMESTAMP` | Workspace creation timestamp |
| `updatedAt` | `TIMESTAMP(3)` | `NOT NULL` | - | Modification timestamp |

*Indexes:* Primary key on `id`.

---

#### Table: `Call`
Core telephony log recording metadata, status, provider IDs, and duration for all inbound/outbound calls.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `TEXT` | `PRIMARY KEY` | `cuid()` | Internal call session identifier |
| `organizationId` | `TEXT` | `NOT NULL, FK -> Organization(id)` | - | Tenant scope key |
| `telnyxCallControlId`| `TEXT` | `NULLABLE, UNIQUE` | `NULL` | Telnyx Call Control API ID |
| `from` | `TEXT` | `NOT NULL` | - | Caller E.164 phone number |
| `to` | `TEXT` | `NOT NULL` | - | Destination E.164 phone number |
| `status` | `TEXT` | `NOT NULL` | `'ringing'` | Call state (`ringing`, `answered`, `completed`, `busy`) |
| `duration` | `INTEGER` | `NOT NULL` | `0` | Connected duration in seconds |
| `recordingUrl` | `TEXT` | `NULLABLE` | `NULL` | Public/presigned S3 recording URL |
| `createdAt` | `TIMESTAMP(3)` | `NOT NULL` | `CURRENT_TIMESTAMP` | Call initiation timestamp |

*Indexes:* `Call_organizationId_idx` (`(organizationId)`), `Call_telnyxCallControlId_key` (`UNIQUE (telnyxCallControlId)`).

---

#### Table: `CallTranscriptionSegment`
Fine-grained real-time transcription chunks captured from conversational audio streams.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `TEXT` | `PRIMARY KEY` | `cuid()` | Segment identifier |
| `callId` | `TEXT` | `NOT NULL, FK -> Call(id) ON DELETE CASCADE` | - | Associated call session |
| `speaker` | `TEXT` | `NOT NULL` | `'caller'` | Speaker tag (`caller`, `agent`, `ai`) |
| `text` | `TEXT` | `NOT NULL` | - | Transcribed utterance content |
| `startTime` | `DOUBLE PRECISION`| `NOT NULL` | `0.0` | Offset start time in seconds |
| `endTime` | `DOUBLE PRECISION`| `NOT NULL` | `0.0` | Offset end time in seconds |
| `confidence` | `DOUBLE PRECISION`| `NOT NULL` | `1.0` | STT engine confidence score |

*Indexes:* `CallTranscriptionSegment_callId_idx` (`(callId)`).
"""

ringee_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

The following sequence diagram details inbound SIP call routing, Telnyx Call Control API interaction, WebRTC audio forking, and real-time AI transcription.

```mermaid
sequenceDiagram
    autonumber
    actor Caller as PSTN Caller
    participant Telnyx as Telnyx Telephony Cloud
    participant Ringee as Ringee NestJS Backend
    participant DB as PostgreSQL (Prisma)
    participant STT as Deepgram Transcription Engine
    actor Agent as Softphone Agent

    Caller->>Telnyx: Dials Organization Number (+18005550199)
    Telnyx->>Ringee: Webhook: `call.initiated` (call_control_id)
    Ringee->>DB: INSERT into Call (status='ringing', from, to)
    Ringee->>Telnyx: Command: `answer_call`
    Telnyx-->>Caller: Call answered (audio line open)
    
    Ringee->>Telnyx: Command: `fork_media` (WebSocket to Ringee)
    Telnyx->>Ringee: Stream bidirectional PCM audio chunks
    Ringee->>STT: Pipe audio chunks via WebSocket
    
    Ringee->>Agent: Ring registered WebRTC softphone
    Agent-->>Ringee: Accept call
    Ringee->>Telnyx: Bridge audio leg to Agent SIP/WebRTC
    
    loop Real-Time Transcription Loop
        STT-->>Ringee: Utterance detected ("I want pricing info")
        Ringee->>DB: INSERT into CallTranscriptionSegment (speaker='caller', text=...)
        Ringee->>Agent: Push live transcription chunk to UI via WebSocket
    end

    Caller->>Telnyx: Hangs up call
    Telnyx->>Ringee: Webhook: `call.hangup` (duration: 142s)
    Ringee->>DB: UPDATE Call SET status='completed', duration=142
    Ringee-->>Agent: Close softphone session & display summary
```
"""

# ==========================================================
# 5. asterisk
# ==========================================================
asterisk_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class ast_channel {
        +String name
        +String uniqueid
        +Int state
        +ast_endpoint endpoint
        +ast_hangup() Int
        +ast_answer() Int
        +ast_write(frame) Int
        +ast_read() ast_frame
    }

    class ast_endpoint {
        +String id
        +String transport
        +String context
        +ast_endpoint_state state
    }

    class ast_bridge {
        +String uniqueid
        +String type
        +ast_bridge_add_channel(chan) Int
        +ast_bridge_remove_channel(chan) Int
        +ast_bridge_merge(other_bridge) Int
    }

    class stasis_app {
        +String app_name
        +stasis_app_register() Void
        +stasis_app_send_event(event) Void
    }

    class ari_client {
        +String host
        +Int port
        +channels_create(endpoint) ast_channel
        +bridges_add_channel(bridge_id, channel_id) Void
        +play_media(channel_id, media_uri) Void
    }

    class cdr_engine {
        +write_cdr(record) Int
        +flush_records() Void
    }

    class cel_engine {
        +log_event(event_type, channel) Void
    }

    ast_channel --> ast_endpoint : associates
    ast_bridge "1" o-- "many" ast_channel : connects
    stasis_app --> ast_channel : manages
    ari_client ..> stasis_app : controls_via_websocket
    ast_channel --> cdr_engine : emits_metrics
    ast_channel --> cel_engine : emits_events
```
"""

asterisk_db = """
### 6.1 Comprehensive Database Schema Specification

Asterisk RealTime Architecture (ARA) connects Asterisk dynamically to relational database tables (PostgreSQL / MySQL) via ODBC for realtime endpoint routing, credentials, and call auditing.

#### Table: `ps_endpoints`
Defines PJSIP SIP endpoints, codecs, contexts, and AOR (Address of Record) bindings.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `VARCHAR(40)` | `PRIMARY KEY` | - | Unique SIP endpoint identifier |
| `transport` | `VARCHAR(40)` | `NULLABLE` | `NULL` | Transport binding (`transport-udp`, `transport-tls`) |
| `aors` | `VARCHAR(200)` | `NULLABLE` | `NULL` | Associated Address of Record references |
| `auth` | `VARCHAR(40)` | `NULLABLE` | `NULL` | Authentication configuration reference |
| `context` | `VARCHAR(40)` | `NOT NULL` | `'default'` | Dialplan context for inbound routing |
| `disallow` | `VARCHAR(200)` | `NOT NULL` | `'all'` | Codecs disallowed for negotiation |
| `allow` | `VARCHAR(200)` | `NOT NULL` | `'ulaw,alaw,opus'` | Permitted audio/video codecs |
| `direct_media` | `VARCHAR(10)` | `NOT NULL` | `'no'` | Enable direct peer-to-peer RTP bypass |

*Indexes:* Primary key on `id`.

---

#### Table: `cdr`
Call Detail Records generated upon channel destruction for billing and compliance auditing.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `accountcode` | `VARCHAR(80)` | `NOT NULL` | `''` | Billing account assignment code |
| `src` | `VARCHAR(80)` | `NOT NULL` | `''` | Caller ID number |
| `dst` | `VARCHAR(80)` | `NOT NULL` | `''` | Destination extension or dialed number |
| `dcontext` | `VARCHAR(80)` | `NOT NULL` | `''` | Dialplan context |
| `channel` | `VARCHAR(80)` | `NOT NULL` | `''` | Channel name (e.g. `PJSIP/trunk-0000001a`) |
| `dstchannel` | `VARCHAR(80)` | `NOT NULL` | `''` | Destination bridged channel name |
| `start` | `TIMESTAMP` | `NOT NULL` | `NOW()` | Call attempt timestamp |
| `answer` | `TIMESTAMP` | `NULLABLE` | `NULL` | Timestamp call was answered |
| `end` | `TIMESTAMP` | `NOT NULL` | `NOW()` | Call termination timestamp |
| `duration` | `INTEGER` | `NOT NULL` | `0` | Total duration from attempt to hangup (seconds) |
| `billsec` | `INTEGER` | `NOT NULL` | `0` | Total billable conversational seconds |
| `disposition` | `VARCHAR(45)` | `NOT NULL` | `'NO ANSWER'` | Call outcome (`ANSWERED`, `BUSY`, `FAILED`) |
| `uniqueid` | `VARCHAR(150)` | `PRIMARY KEY` | - | Asterisk unique channel identifier |

*Indexes:* `cdr_uniqueid_idx` (`PRIMARY KEY (uniqueid)`), `cdr_src_idx` (`(src)`), `cdr_start_idx` (`(start)`).
"""

asterisk_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

The following sequence illustrates a complete SIP call lifecycle routed through chan_pjsip, dialplan evaluation, and external ARI (Asterisk REST Interface) control.

```mermaid
sequenceDiagram
    autonumber
    actor SIPPhone as SIP Softphone / Carrier
    participant PJSIP as chan_pjsip Driver
    participant Core as Asterisk PBX Core
    participant Dialplan as extensions.conf
    participant ARI as External ARI Controller
    participant ARA as PostgreSQL (ARA ODBC)

    SIPPhone->>PJSIP: SIP INVITE (sip:1001@pbx.local)
    PJSIP->>ARA: Query ps_endpoints & ps_auths
    ARA-->>PJSIP: Return valid endpoint & credentials
    PJSIP-->>SIPPhone: 401 Unauthorized (Auth Challenge)
    SIPPhone->>PJSIP: SIP INVITE with MD5/SHA256 Auth
    PJSIP->>Core: Channel created (`PJSIP/1001-000001`)
    
    Core->>Dialplan: Evaluate context `default`, extension `1001`
    Dialplan->>Core: Execute `Stasis("voice_ai_app")`
    Core->>ARI: WebSocket Event: `StasisStart` (channel_id, caller_id)
    
    ARI->>Core: REST POST `/bridges` (type: 'mixing')
    Core-->>ARI: Bridge created (`bridge_id_abc`)
    ARI->>Core: REST POST `/bridges/{id}/addChannel` (channel_id)
    ARI->>Core: REST POST `/channels/{id}/play` (media: 'sound:welcome')
    Core->>PJSIP: RTP stream audio to SIPPhone
    
    SIPPhone->>PJSIP: SIP BYE (Hang up)
    PJSIP->>Core: Channel destruction initiated
    Core->>ARI: WebSocket Event: `StasisEnd`
    Core->>ARA: INSERT into cdr (src, dst, duration, billsec, disposition='ANSWERED')
    Core-->>SIPPhone: SIP 200 OK
```
"""

# ==========================================================
# 6. VoicEra
# ==========================================================
voicera_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class TelephonyRouter {
        +PlivoProvider plivo
        +ViProvider vi
        +routeOutboundCall(campaign, contact) Promise~CallResult~
        +handleInboundWebhook(provider, payload) Response
    }

    class PlivoProvider {
        +String authId
        +String authToken
        +makeCall(from, to, answerUrl) CallResponse
        +hangupCall(callUuid) Void
    }

    class VoiceSession {
        +String callUuid
        +String contactId
        +WebSocket clientSocket
        +handleAudioFrame(frame) Void
        +sendTtsChunk(audioBytes) Void
        +terminateSession() Void
    }

    class LLMDialogueManager {
        +String systemPrompt
        +List~Message~ history
        +processUserUtterance(text) AsyncIterator~Token~
        +detectFunctionCall(text) ToolCall
    }

    class STTPipeline {
        +String deepgramApiKey
        +transcribeStream(pcmStream) AsyncIterator~Transcript~
    }

    class TTSPipeline {
        +String elevenlabsApiKey
        +synthesizeStream(textStream) AsyncIterator~AudioChunk~
    }

    class Organisation {
        +UUID id
        +String name
        +String slug
    }

    class Campaign {
        +UUID id
        +UUID organisationId
        +String name
        +String voiceTemplate
        +String status
    }

    class CallRecord {
        +UUID id
        +UUID campaignId
        +String contactPhone
        +Int duration
        +String disposition
        +String transcriptText
    }

    TelephonyRouter --> PlivoProvider : invokes
    VoiceSession --> STTPipeline : decodes voice
    VoiceSession --> LLMDialogueManager : generates reply
    VoiceSession --> TTSPipeline : encodes audio
    Campaign "1" *-- "many" CallRecord : tracks
    Organisation "1" *-- "many" Campaign : owns
```
"""

voicera_db = """
### 6.1 Comprehensive Database Schema Specification

VoicEra utilizes PostgreSQL managed via SQLAlchemy and Alembic, orchestrating multi-tenant telephony credentials, agent dialogue policies, and call recordings.

#### Table: `organisations`
Root workspace entity managing enterprise permissions and telephony quotas.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `uuid_generate_v4()` | Unique organisation ID |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Organisation display name |
| `slug` | `VARCHAR(100)` | `NOT NULL, UNIQUE` | - | URL slug identifier |
| `created_at` | `TIMESTAMPTZ` | `NOT NULL` | `NOW()` | Creation timestamp |

*Indexes:* `idx_org_slug` (`UNIQUE (slug)`).

---

#### Table: `campaigns`
Outbound voice calling campaigns linking lead lists to AI conversational prompts.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `uuid_generate_v4()` | Campaign identifier |
| `organisation_id` | `UUID` | `NOT NULL, FK -> organisations(id)` | - | Tenant scope key |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Campaign title |
| `system_prompt` | `TEXT` | `NOT NULL` | - | System instructions provided to LLM |
| `voice_provider` | `VARCHAR(50)` | `NOT NULL` | `'elevenlabs'` | Synthesizer provider (`elevenlabs`, `cartesia`) |
| `voice_id` | `VARCHAR(100)` | `NOT NULL` | - | Specific voice persona identifier |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'draft'` | State (`draft`, `running`, `paused`, `completed`) |
| `created_at` | `TIMESTAMPTZ` | `NOT NULL` | `NOW()` | Campaign creation timestamp |

*Indexes:* `idx_campaigns_org` (`(organisation_id)`), `idx_campaigns_status` (`(status)`).

---

#### Table: `call_records`
Detailed logs of every voice call session including transcription, metrics, and outcomes.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `uuid_generate_v4()` | Call log record ID |
| `campaign_id` | `UUID` | `NOT NULL, FK -> campaigns(id)` | - | Associated campaign |
| `call_uuid` | `VARCHAR(100)` | `NOT NULL, UNIQUE` | - | Plivo / VI Telephony Call UUID |
| `phone_number` | `VARCHAR(50)` | `NOT NULL` | - | Destination prospect phone number |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'initiated'` | Telephony status (`initiated`, `completed`, `failed`) |
| `duration` | `INTEGER` | `NOT NULL` | `0` | Conversational duration in seconds |
| `transcript` | `TEXT` | `NULLABLE` | `NULL` | Complete dialogue transcript |
| `disposition` | `VARCHAR(50)` | `NULLABLE` | `NULL` | AI-classified disposition (`qualified`, `not_interested`, `callback`) |
| `recording_url` | `TEXT` | `NULLABLE` | `NULL` | S3 audio recording URL |
| `created_at` | `TIMESTAMPTZ` | `NOT NULL` | `NOW()` | Call start timestamp |

*Indexes:* `idx_call_records_campaign` (`(campaign_id)`), `idx_call_records_uuid` (`UNIQUE (call_uuid)`).
"""

voicera_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

The following sequence diagram details the full bidirectional streaming lifecycle between Plivo, FastAPI WebSockets, Deepgram STT, OpenAI LLM, and ElevenLabs TTS.

```mermaid
sequenceDiagram
    autonumber
    actor Prospect as Prospect (Mobile Phone)
    participant Plivo as Plivo Telephony Cloud
    participant VoicEra as VoicEra FastAPI Server
    participant STT as Deepgram STT (WebSocket)
    participant LLM as OpenAI / Anthropic
    participant TTS as ElevenLabs TTS
    participant DB as PostgreSQL Database

    VoicEra->>Plivo: POST /Call (To: Prospect, AnswerUrl: Webhook)
    Plivo->>Prospect: Cellular PSTN Ringing...
    Prospect-->>Plivo: Answers Call
    Plivo->>VoicEra: Webhook: `answer_url`
    VoicEra-->>Plivo: Return XML `<Stream keepCallAlive="true">wss://api.voicera/media</Stream>`
    
    Plivo->>VoicEra: Open WebSocket & Stream raw PCM audio
    VoicEra->>STT: Forward incoming PCM chunks
    
    loop Conversational Turn Loop
        STT-->>VoicEra: Final Transcript ("Yes, can you tell me more?")
        VoicEra->>LLM: Stream dialogue context + user utterance
        LLM-->>VoicEra: Stream generated tokens ("Certainly! We help...")
        VoicEra->>TTS: Stream token chunks for fast TTFT synthesis
        TTS-->>VoicEra: Synthesized PCM audio chunks
        VoicEra->>Plivo: Stream PCM frames over WebSocket
        Plivo->>Prospect: Plays synthesized voice to caller
    end

    Prospect->>Plivo: Hangs up call
    Plivo->>VoicEra: Webhook: `call.hangup` (duration: 88s)
    VoicEra->>LLM: Classify dialogue outcome & summary
    LLM-->>VoicEra: Disposition: "qualified"
    VoicEra->>DB: INSERT into call_records (duration=88, disposition='qualified', transcript=...)
```
"""

# ==========================================================
# 7. dograh
# ==========================================================
dograh_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class WorkflowEngine {
        +DBSession db
        +executeWorkflow(workflowId, triggerContext) Promise~Void~
        +evaluateNode(node, sessionContext) NodeResult
        +transitionToNextNode(currentNodeId, condition) Void
    }

    class PipecatPipeline {
        +DailyTransport transport
        +VADProcessor vad
        +STTProcessor stt
        +LLMProcessor llm
        +TTSProcessor tts
        +TTSCache cache
        +startStream() Void
        +stopStream() Void
    }

    class TelephonyTrunkManager {
        +configureTrunk(orgId, trunkConfig) TelephonyTrunk
        +allocatePhoneNumber(orgId, country) TelephonyPhoneNumber
        +releaseNumber(phoneId) Void
    }

    class VoiceSessionManager {
        +String sessionId
        +String roomUrl
        +createDailyRoom() String
        +joinSession(participant) Void
        +recordSessionMetrics() SessionMetrics
    }

    class ModelConnectionService {
        +verifyCredentials(provider, apiKey) Boolean
        +getModelClient(providerName) BaseLLMClient
    }

    class UserModel {
        +UUID id
        +String email
        +String role
    }

    class OrganizationModel {
        +UUID id
        +String name
        +List~UserModel~ users
    }

    class WorkflowDefinitionModel {
        +UUID id
        +UUID organization_id
        +String name
        +JSON schema_json
    }

    class WorkflowModel {
        +UUID id
        +UUID definition_id
        +String status
        +JSON execution_log
    }

    WorkflowEngine --> PipecatPipeline : spawns
    WorkflowEngine --> VoiceSessionManager : coordinates
    PipecatPipeline --> TelephonyTrunkManager : binds audio trunk
    WorkflowEngine --> WorkflowModel : logs execution
    WorkflowDefinitionModel "1" *-- "many" WorkflowModel : instantiates
```
"""

dograh_db = """
### 6.1 Comprehensive Database Schema Specification

Dograh uses PostgreSQL managed via SQLAlchemy to persist multi-tenant agent configurations, dynamic workflow graphs, telephony trunks, and cached audio files.

#### Table: `organizations`
Multi-tenant workspace boundary governing phone trunks and model credentials.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Unique organisation identifier |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Display name of the business entity |
| `created_at` | `TIMESTAMPTZ` | `NOT NULL` | `NOW()` | Registration timestamp |

*Indexes:* Primary key on `id`.

---

#### Table: `telephony_trunks`
Configures SIP trunks, Twilio interconnects, or Daily.co SIP credentials.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Unique trunk identifier |
| `organization_id` | `UUID` | `NOT NULL, FK -> organizations(id)` | - | Associated organization |
| `provider` | `VARCHAR(50)` | `NOT NULL` | `'twilio'` | Telephony carrier (`twilio`, `telnyx`, `daily`) |
| `sip_endpoint` | `VARCHAR(255)` | `NOT NULL` | - | Carrier SIP gateway URL |
| `auth_username` | `VARCHAR(100)` | `NULLABLE` | `NULL` | SIP authentication username |
| `auth_password` | `VARCHAR(255)` | `NULLABLE` | `NULL` | Encrypted SIP authentication password |

*Indexes:* `idx_trunks_org` (`(organization_id)`).

---

#### Table: `workflow_definitions`
Visual workflow graphs containing state nodes, condition edges, and AI prompts.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Unique workflow definition ID |
| `organization_id` | `UUID` | `NOT NULL, FK -> organizations(id)` | - | Tenant scope key |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Human-readable workflow title |
| `schema_json` | `JSONB` | `NOT NULL` | `'{}'` | Full workflow graph (nodes, edges, prompt configs) |
| `created_at` | `TIMESTAMPTZ` | `NOT NULL` | `NOW()` | Creation timestamp |

*Indexes:* `idx_workflow_def_org` (`(organization_id)`).

---

#### Table: `workflows`
Runtime execution instances tracking conversational sessions and node traversals.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Workflow execution instance ID |
| `definition_id` | `UUID` | `NOT NULL, FK -> workflow_definitions(id)` | - | Parent definition |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'running'` | Lifecycle state (`running`, `completed`, `failed`) |
| `current_node_id`| `VARCHAR(100)` | `NULLABLE` | `NULL` | Active node in execution graph |
| `execution_log` | `JSONB` | `NOT NULL` | `'[]'` | Chronological event and message log |
| `started_at` | `TIMESTAMPTZ` | `NOT NULL` | `NOW()` | Start timestamp |
| `ended_at` | `TIMESTAMPTZ` | `NULLABLE` | `NULL` | Termination timestamp |

*Indexes:* `idx_workflows_def` (`(definition_id)`), `idx_workflows_status` (`(status)`).
"""

dograh_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

The following sequence illustrates the event-driven workflow engine dispatching Pipecat real-time audio pipelines across Twilio SIP and Daily.co WebRTC rooms.

```mermaid
sequenceDiagram
    autonumber
    actor Caller as Inbound / Outbound Prospect
    participant Trunk as Telephony Trunk (Twilio/Daily)
    participant Engine as Dograh WorkflowEngine
    participant Pipecat as Pipecat Audio Pipeline
    participant Deepgram as Deepgram STT
    participant Cartesia as Cartesia TTS (with Cache)
    participant DB as PostgreSQL Database

    Caller->>Trunk: Call dialed / connected
    Trunk->>Engine: Trigger Voice Session (caller_number, workflow_id)
    Engine->>DB: Query workflow_definitions (nodes, prompts, schema)
    DB-->>Engine: Return workflow graph
    
    Engine->>Pipecat: Instantiate Pipecat Pipeline (VAD, STT, LLM, TTS)
    Engine->>Trunk: Connect audio stream to Pipecat Transport
    
    loop Interactive Node Execution
        Trunk->>Pipecat: Inbound audio chunks
        Pipecat->>Deepgram: Transcribe speech stream
        Deepgram-->>Pipecat: Transcribed text
        Pipecat->>Engine: Evaluate active graph node conditions
        Engine-->>Pipecat: Select LLM response prompt
        Pipecat->>Cartesia: Synthesize speech (check TTS Cache)
        Cartesia-->>Pipecat: Output audio PCM frames
        Pipecat->>Trunk: Stream speech back to Caller
    end

    Caller->>Trunk: Call terminated
    Trunk->>Engine: Session ended event
    Engine->>DB: UPDATE workflows SET status='completed', execution_log=...
    Engine-->>DB: Record session metrics & call duration
```
"""

# Execute insertions
insert_sections('docs/wiki/OpenOutreach.md', openoutreach_cd, openoutreach_db, openoutreach_seq)
insert_sections('docs/wiki/linkedin-outreach-mcp.md', linkedin_cd, linkedin_db, linkedin_seq)
insert_sections('docs/wiki/Open-Cold-Dialer.md', ocd_cd, ocd_db, ocd_seq)
insert_sections('docs/wiki/ringee-app.md', ringee_cd, ringee_db, ringee_seq)
insert_sections('docs/wiki/asterisk.md', asterisk_cd, asterisk_db, asterisk_seq)
insert_sections('docs/wiki/VoicEra.md', voicera_cd, voicera_db, voicera_seq)
insert_sections('docs/wiki/dograh.md', dograh_cd, dograh_db, dograh_seq)
