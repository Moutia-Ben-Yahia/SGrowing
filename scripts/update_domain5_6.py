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
        sec_next_match = re.search(r'\n## [78]\.\s+', content)
        if sec_next_match:
            idx = sec_next_match.start()
            content = content[:idx] + "\n" + db_schema.strip() + "\n\n---\n" + content[idx:]

    # 3. Insert Sequence Diagram into Section 7 / before Strategic Value
    if 'sequenceDiagram' not in content:
        sec_strat_match = re.search(r'\n## [78]\.\s+Strategic Value', content)
        if sec_strat_match:
            idx = sec_strat_match.start()
            content = content[:idx] + "\n" + seq_diagram.strip() + "\n\n---\n" + content[idx:]

    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(content)
    print(f"Updated {filepath} successfully!")

# ==========================================
# DOMAIN 5: EMAIL INFRASTRUCTURE
# ==========================================

# 1. listmonk
listmonk_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class CampaignManager {
        +DBPool db
        +startCampaign(campaignId) Promise~Void~
        +pauseCampaign(campaignId) Promise~Void~
        +getCampaignStats(campaignId) CampaignStats
    }

    class SubscriberManager {
        +createSubscriber(subData) Subscriber
        +importSubscribers(csvReader) ImportReport
        +optOut(uuid) Void
    }

    class SMTPSenderPool {
        +List~SMTPSender~ workers
        +dispatchEmail(msg) SendResult
        +testConnection() Boolean
    }

    class BounceProcessor {
        +processBounceWebhook(payload) BounceRecord
        +deactivateHardBounces() Int
    }

    class Campaign {
        +Int id
        +String name
        +String subject
        +String body
        +String status
        +List~Int~ listIds
    }

    class Subscriber {
        +Int id
        +String uuid
        +String email
        +String name
        +JSON attribs
        +String status
    }

    CampaignManager --> Campaign : controls
    CampaignManager --> SMTPSenderPool : dispatches_via
    CampaignManager --> BounceProcessor : handles_bounces
    SubscriberManager --> Subscriber : manages
    Campaign "many" --> "many" Subscriber : targets
```
"""

listmonk_db = """
### 6.1 Comprehensive Database Schema Specification

Listmonk runs on PostgreSQL utilizing JSONB attributes, raw SQL concurrency primitives, and unlogged analytics tables for high throughput.

#### Table: `campaigns`
Email marketing newsletters and sequence campaigns.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `SERIAL` | `PRIMARY KEY` | - | Unique campaign ID |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Administrative title |
| `subject` | `VARCHAR(255)` | `NOT NULL` | - | Email subject line |
| `body` | `TEXT` | `NOT NULL` | - | HTML/Markdown email content |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'draft'` | State (`draft`, `scheduled`, `running`, `paused`, `finished`) |
| `sent` | `INTEGER` | `NOT NULL` | `0` | Total dispatched emails counter |
| `to_send` | `INTEGER` | `NOT NULL` | `0` | Target audience recipient count |
| `created_at` | `TIMESTAMPTZ` | `NOT NULL` | `NOW()` | Creation timestamp |

*Indexes:* `idx_campaigns_status` (`(status)`).

---

#### Table: `subscribers`
Recipient audience directory with extensible custom JSON attributes.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `SERIAL` | `PRIMARY KEY` | - | Subscriber ID |
| `uuid` | `UUID` | `NOT NULL, UNIQUE` | `gen_random_uuid()` | Public tracking token |
| `email` | `VARCHAR(255)` | `NOT NULL, UNIQUE` | - | Subscriber email address |
| `name` | `VARCHAR(255)` | `NOT NULL` | `''` | Full name |
| `attribs` | `JSONB` | `NOT NULL` | `'{}'` | Custom segmentation variables |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'enabled'` | Subscription state (`enabled`, `unsubscribed`, `blocklisted`) |
| `created_at` | `TIMESTAMPTZ` | `NOT NULL` | `NOW()` | Registration timestamp |

*Indexes:* `idx_subscribers_email` (`UNIQUE (email)`), `idx_subscribers_attribs` (`USING gin (attribs)`).
"""

listmonk_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

The following sequence illustrates Listmonk's multi-threaded Go worker pipeline querying subscribers, rendering Go HTML templates, and distributing via SMTP.

```mermaid
sequenceDiagram
    autonumber
    actor Operator as Marketing Operator
    participant UI as Listmonk Web Admin
    participant Mgr as CampaignManager (Go)
    participant DB as PostgreSQL Database
    participant Pool as SMTPSenderPool
    actor Recipient as Subscriber Mailbox

    Operator->>UI: Click "Start Campaign"
    UI->>Mgr: POST /api/campaigns/{id}/status (status='running')
    Mgr->>DB: Query eligible subscribers WHERE status='enabled' AND list_id IN (...)
    DB-->>Mgr: Stream batch of 5,000 subscribers
    
    loop Concurrent Worker Distribution (Goroutines)
        Mgr->>Pool: Submit message task to worker channel
        Pool->>Pool: Render template tags ({{ .Subscriber.Attribs.FirstName }})
        Pool->>Recipient: Transmit email over TLS SMTP
        Recipient-->>Pool: 250 OK (Queued for Delivery)
        Pool->>DB: Increment campaign sent counter
    end
    
    Recipient->>UI: Clicks 1x1 tracking pixel / link
    UI->>DB: INSERT into campaign_views (campaign_id, subscriber_id)
    Mgr-->>Operator: Campaign marked finished (100% delivered)
```
"""

# 2. postal
postal_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class PostalServer {
        +BigInt id
        +String name
        +String token
        +List~Message~ messages
        +sendEmail(params) SendResult
    }

    class SmtpSenderEngine {
        +resolveMxRecords(domain) List~MxRecord~
        +deliverMessage(msg, mx) DeliveryResult
    }

    class Message {
        +BigInt id
        +BigInt serverId
        +String to
        +String from
        +String status
    }

    PostalServer --> SmtpSenderEngine : routes_messages
    PostalServer "1" *-- "many" Message : logs
```
"""

postal_db = """
### 6.1 Comprehensive Database Schema Specification

Postal runs on MySQL with server-scoped email message routing and audit logs.

#### Table: `messages`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `BIGINT` | `PRIMARY KEY AUTO_INCREMENT` | - | Message ID |
| `server_id` | `INT` | `NOT NULL` | - | Virtual mail server ID |
| `scope` | `VARCHAR(20)` | `NOT NULL` | `'outbound'` | Direction (`inbound`, `outbound`) |
| `rcpt_to` | `VARCHAR(255)` | `NOT NULL` | - | Recipient email |
| `mail_from` | `VARCHAR(255)` | `NOT NULL` | - | Sender email |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'Pending'` | Delivery status |

*Indexes:* `idx_messages_server` (`(server_id)`), `idx_messages_status` (`(status)`).
"""

postal_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor App as External Application
    participant API as Postal HTTP / SMTP API
    participant Rabbit as RabbitMQ Message Queue
    participant Sender as Postal Delivery Worker
    actor MX as Recipient MX Server

    App->>API: Send Email Request
    API->>Rabbit: Enqueue message payload
    Rabbit->>Sender: Worker dequeues message
    Sender->>Sender: Query DNS MX records for recipient domain
    Sender->>MX: Connect SMTP over STARTTLS & send mail
    MX-->>Sender: 250 OK
    Sender->>API: Trigger webhook event `MessageDelivered`
    API-->>App: Delivery webhook confirmed
```
"""

# 3. useSend
usesend_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class EmailService {
        +sendEmail(options) SendResult
        +verifyDomainDns(domainId) DnsStatus
    }

    class TemplateRenderer {
        +renderReactEmail(component, props) String
    }

    EmailService --> TemplateRenderer : compiles_html
```
"""

usesend_db = """
### 6.1 Comprehensive Database Schema Specification

UseSend persists domains, API keys, and outbound email logs in PostgreSQL.

#### Table: `emails`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Email transaction ID |
| `from` | `VARCHAR(255)` | `NOT NULL` | - | Verified sender address |
| `to` | `VARCHAR(255)` | `NOT NULL` | - | Destination address |
| `subject` | `VARCHAR(255)` | `NOT NULL` | - | Email subject |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'queued'` | State (`queued`, `sent`, `delivered`) |

*Indexes:* `idx_usesend_to` (`(to)`).
"""

usesend_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor Dev as Developer App
    participant API as useSend API
    participant SES as AWS SES Gateway
    actor User as Inbox

    Dev->>API: POST /api/emails (to, from, react_template)
    API->>API: Render React Email component to HTML
    API->>SES: Relay via AWS SES API
    SES->>User: Transmit email
    SES-->>API: SES Message ID
    API-->>Dev: 200 OK (Email ID)
```
"""

# 4. warmbly
warmbly_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class WarmupScheduler {
        +runDailySchedule() Void
        +pairMailboxes(inboxA, inboxB) WarmupPair
    }

    class PeerNetworkRouter {
        +sendWarmupEmail(sender, recipient) Void
        +inspectSpamAndRecover(recipient, messageId) Void
    }

    WarmupScheduler --> PeerNetworkRouter : executes
```
"""

warmbly_db = """
### 6.1 Comprehensive Database Schema Specification

Warmbly stores connected sender inboxes and peer warmup interactions.

#### Table: `inboxes`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Mailbox ID |
| `email` | `VARCHAR(255)` | `NOT NULL, UNIQUE` | - | Email account |
| `health_score` | `FLOAT` | `NOT NULL` | `100.0` | Reputation score (0-100) |
| `daily_target` | `INTEGER` | `NOT NULL` | `40` | Target warmup volume |

*Indexes:* `idx_warmbly_email` (`UNIQUE (email)`).
"""

warmbly_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    participant Cron as Warmbly Scheduler
    participant NodeA as Sender Inbox (Mailbox A)
    participant NodeB as Peer Inbox (Mailbox B)

    Cron->>NodeA: Dispatch AI warmup email to NodeB
    NodeA->>NodeB: Send email via IMAP/SMTP
    NodeB->>NodeB: Poll inbox / spam folder
    NodeB->>NodeB: Move from Spam to Inbox & Star email
    NodeB->>NodeA: Send automated conversational reply
    Cron->>Cron: Increment health score
```
"""

# 5. BillionMail
billionmail_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class MailingEngine {
        +processBatch(batchId) Void
        +rotateIpAddress() String
    }

    class BatchDistributor {
        +chunkRecipients(list, chunkSize) List~Batch~
    }

    MailingEngine --> BatchDistributor : divides_work
```
"""

billionmail_db = """
### 6.1 Comprehensive Database Schema Specification

BillionMail manages high-volume queues and multi-IP pools in relational tables.

#### Table: `batches`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Batch ID |
| `total_count` | `INTEGER` | `NOT NULL` | `0` | Total emails in batch |
| `sent_count` | `INTEGER` | `NOT NULL` | `0` | Sent counter |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'processing'` | Batch state |
"""

billionmail_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor Admin as Operator
    participant Engine as BillionMail Engine
    participant IPPool as IP Rotator Pool
    actor MX as Destination Mail Server

    Admin->>Engine: Enqueue 1,000,000 email blast
    Engine->>Engine: Divide into 50k worker chunks
    loop For Each Chunk
        Engine->>IPPool: Lease next clean outbound IP
        IPPool-->>Engine: IP 192.0.2.45
        Engine->>MX: Concurrent SMTP dispatch
        MX-->>Engine: 250 OK
    end
```
"""

# ==========================================
# DOMAIN 6: SOCIAL MEDIA MANAGEMENT
# ==========================================

# 6. mixpost
mixpost_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class Post {
        +BigInt id
        +Text content
        +String status
        +Timestamp scheduled_at
        +publishNow() Void
    }

    class Account {
        +BigInt id
        +String name
        +String provider
        +JSON credentials
    }

    class SchedulePostJob {
        +BigInt postId
        +handle() Void
    }

    class TwitterProvider {
        +publishPost(account, content, media) PostReceipt
    }

    class LinkedInProvider {
        +publishPost(account, content, media) PostReceipt
    }

    SchedulePostJob --> Post : executes
    SchedulePostJob --> TwitterProvider : publishes_to
    SchedulePostJob --> LinkedInProvider : publishes_to
    Post "many" --> "many" Account : targets
```
"""

mixpost_db = """
### 6.1 Comprehensive Database Schema Specification

Mixpost utilizes MySQL or PostgreSQL with Laravel Horizon queuing.

#### Table: `mixpost_posts`
Scheduled and published social media posts.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `BIGINT UNSIGNED`| `PRIMARY KEY AUTO_INCREMENT` | - | Post identifier |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'DRAFT'` | State (`DRAFT`, `SCHEDULED`, `PUBLISHING`, `PUBLISHED`, `FAILED`) |
| `scheduled_at` | `TIMESTAMP` | `NULLABLE` | `NULL` | Scheduled publication time |
| `published_at` | `TIMESTAMP` | `NULLABLE` | `NULL` | Actual publication time |
| `created_at` | `TIMESTAMP` | `NOT NULL` | `NOW()` | Creation timestamp |

*Indexes:* `idx_mixpost_status_sched` (`(status, scheduled_at)`).

---

#### Table: `mixpost_accounts`
Social media account credentials and OAuth tokens.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `BIGINT UNSIGNED`| `PRIMARY KEY AUTO_INCREMENT` | - | Account identifier |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Profile handle / account name |
| `provider` | `VARCHAR(50)` | `NOT NULL` | - | Platform (`twitter`, `facebook`, `linkedin`, `instagram`) |
| `data` | `JSON` | `NOT NULL` | - | Encrypted OAuth access & refresh tokens |

*Indexes:* `idx_mixpost_provider` (`(provider)`).
"""

mixpost_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor Creator as Content Creator
    participant UI as Mixpost Dashboard
    participant DB as Database
    participant Horizon as Laravel Horizon / Redis
    participant Twitter as Twitter API v2
    participant LinkedIn as LinkedIn API

    Creator->>UI: Create Post & Select Channels (Twitter & LinkedIn)
    UI->>DB: INSERT into mixpost_posts (status='SCHEDULED', scheduled_at=15:00)
    UI->>Horizon: Schedule SchedulePostJob for 15:00
    
    Note over Horizon: Scheduled Time Reached (15:00)
    Horizon->>Twitter: POST /2/tweets (content, media_ids)
    Twitter-->>Horizon: 201 Created (Tweet ID)
    Horizon->>LinkedIn: POST /v2/ugcPosts (shareCommentary)
    LinkedIn-->>Horizon: 201 Created (URN)
    
    Horizon->>DB: UPDATE mixpost_posts SET status='PUBLISHED', published_at=NOW()
    Horizon-->>Creator: Push notification: Published across 2 channels
```
"""

# 7. postiz-app
postiz_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class PostService {
        +createPost(data) Post
        +schedulePost(postId, date) Void
    }

    class SocialIntegrationService {
        +publishToProvider(provider, content) PublishResult
    }

    class Post {
        +String id
        +String content
        +DateTime scheduledFor
        +String status
    }

    PostService --> SocialIntegrationService : publishes
    PostService --> Post : manages
```
"""

postiz_db = """
### 6.1 Comprehensive Database Schema Specification

Postiz persists social posts and media files using Prisma on PostgreSQL.

#### Table: `Post`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `TEXT` | `PRIMARY KEY` | `cuid()` | Unique post ID |
| `content` | `TEXT` | `NOT NULL` | - | Post copy |
| `scheduledFor` | `TIMESTAMP(3)` | `NULLABLE` | `NULL` | Scheduled timestamp |
| `status` | `TEXT` | `NOT NULL` | `'DRAFT'` | State (`DRAFT`, `SCHEDULED`, `PUBLISHED`) |

*Indexes:* `Post_scheduledFor_idx` (`(scheduledFor)`).
"""

postiz_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor User as Social Manager
    participant App as Postiz Web App
    participant Queue as BullMQ Worker
    participant Social as Social Network API

    User->>App: Schedule post for tomorrow 10am
    App->>Queue: Add delayed job (delay: 24h)
    Queue->>Social: Dispatches post payload at trigger time
    Social-->>Queue: Success response
    Queue->>App: Update post status = PUBLISHED
```
"""

# 8. openpost
openpost_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class PostManager {
        +schedule(post) Void
        +publish(post) Void
    }

    class ChannelConnector {
        +sendUpdate(channel, text) Result
    }

    PostManager --> ChannelConnector : delegates
```
"""

openpost_db = """
### 6.1 Comprehensive Database Schema Specification

OpenPost stores multi-platform social media posts in indexed tables.

#### Table: `posts`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Post ID |
| `content` | `TEXT` | `NOT NULL` | - | Content body |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'pending'` | Status |
"""

openpost_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor User as User
    participant Mgr as PostManager
    participant Conn as ChannelConnector
    participant Platform as Platform API

    User->>Mgr: Submit post
    Mgr->>Conn: sendUpdate(channel, content)
    Conn->>Platform: Publish API request
    Platform-->>Conn: 200 OK
```
"""

# 9. trypost
trypost_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class ComposerService {
        +draftPost(text) PostDraft
        +attachMedia(file) MediaItem
    }

    class QueueManager {
        +enqueue(postDraft, timestamp) Void
    }

    ComposerService --> QueueManager : schedules
```
"""

trypost_db = """
### 6.1 Comprehensive Database Schema Specification

Trypost manages post drafts and scheduling intervals.

#### Table: `post_drafts`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Draft ID |
| `body` | `TEXT` | `NOT NULL` | - | Draft text |
| `state` | `VARCHAR(50)` | `NOT NULL` | `'draft'` | Draft state |
"""

trypost_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor User as Marketer
    participant Svc as ComposerService
    participant Queue as QueueManager

    User->>Svc: Draft post
    Svc->>Queue: Enqueue for release
    Queue-->>User: Scheduled confirmed
```
"""

# 10. postbase
postbase_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class ContentPipeline {
        +normalizePost(rawContent) NormalizedPost
        +broadcast(post, channels) Void
    }

    class AccountBridge {
        +authenticate(provider) Token
    }

    ContentPipeline --> AccountBridge : uses_auth
```
"""

postbase_db = """
### 6.1 Comprehensive Database Schema Specification

Postbase stores normalized content and social connection tokens.

#### Table: `content_items`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Item ID |
| `content` | `TEXT` | `NOT NULL` | - | Social text |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'draft'` | Item status |
"""

postbase_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor Creator as Creator
    participant Pipeline as ContentPipeline
    participant Social as Social Network

    Creator->>Pipeline: Submit content
    Pipeline->>Social: Distribute to connected feeds
    Social-->>Pipeline: Published
```
"""

# Execute insertions
insert_sections('docs/wiki/listmonk.md', listmonk_cd, listmonk_db, listmonk_seq)
insert_sections('docs/wiki/postal.md', postal_cd, postal_db, postal_seq)
insert_sections('docs/wiki/useSend.md', usesend_cd, usesend_db, usesend_seq)
insert_sections('docs/wiki/warmbly.md', warmbly_cd, warmbly_db, warmbly_seq)
insert_sections('docs/wiki/BillionMail.md', billionmail_cd, billionmail_db, billionmail_seq)
insert_sections('docs/wiki/mixpost.md', mixpost_cd, mixpost_db, mixpost_seq)
insert_sections('docs/wiki/postiz-app.md', postiz_cd, postiz_db, postiz_seq)
insert_sections('docs/wiki/openpost.md', openpost_cd, openpost_db, openpost_seq)
insert_sections('docs/wiki/trypost.md', trypost_cd, trypost_db, trypost_seq)
insert_sections('docs/wiki/postbase.md', postbase_cd, postbase_db, postbase_seq)
