import os
import re

def insert_domain3(filepath, class_diagram, db_schema, seq_diagram):
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
# 1. chatwoot
# ==========================================
chatwoot_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class Account {
        +BigInt id
        +String name
        +String locale
        +List~Inbox~ inboxes
        +List~User~ users
    }

    class Inbox {
        +BigInt id
        +BigInt accountId
        +String channelType
        +BigInt channelId
        +String name
        +List~Conversation~ conversations
    }

    class Conversation {
        +BigInt id
        +BigInt accountId
        +BigInt inboxId
        +BigInt contactId
        +Int status
        +BigInt assigneeId
        +assignAgent(userId) Void
        +toggleStatus() Void
    }

    class Message {
        +BigInt id
        +BigInt conversationId
        +BigInt accountId
        +Int messageType
        +Text content
        +JSON attachments
    }

    class Contact {
        +BigInt id
        +BigInt accountId
        +String name
        +String email
        +String phoneNumber
    }

    class MessagesCreateService {
        +Conversation conversation
        +User sender
        +perform(params) Message
    }

    class ActionCableBroadcaster {
        +broadcastToAccount(accountId, event, payload) Void
        +broadcastToConversation(convId, event, payload) Void
    }

    Account "1" *-- "many" Inbox : provisions
    Inbox "1" *-- "many" Conversation : routes
    Conversation "1" *-- "many" Message : contains
    Contact "1" *-- "many" Conversation : initiates
    MessagesCreateService --> Message : creates
    MessagesCreateService --> ActionCableBroadcaster : triggers
```
"""

chatwoot_db = """
### 6.1 Comprehensive Database Schema Specification

Chatwoot uses PostgreSQL with multi-tenant partitioning by `account_id`, Redis-backed Sidekiq queues, and ActionCable WebSockets.

#### Table: `accounts`
Root multi-tenant workspace entity.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `BIGINT` | `PRIMARY KEY AUTO_INCREMENT` | - | Unique workspace account identifier |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Commercial company/team name |
| `locale` | `VARCHAR(10)` | `NOT NULL` | `'en'` | Default interface language |
| `created_at` | `TIMESTAMP` | `NOT NULL` | `NOW()` | Registration timestamp |

*Indexes:* Primary key on `id`.

---

#### Table: `inboxes`
Communication channel endpoints (Web Widget, WhatsApp, Email, Facebook, API).

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `BIGINT` | `PRIMARY KEY AUTO_INCREMENT` | - | Inbox endpoint ID |
| `account_id` | `BIGINT` | `NOT NULL, FK -> accounts(id)` | - | Tenant scope key |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Inbox label |
| `channel_type` | `VARCHAR(255)` | `NOT NULL` | - | Channel class name (e.g. `Channel::WebWidget`) |
| `channel_id` | `BIGINT` | `NOT NULL` | - | Polymorphic ID pointing to specific channel config |

*Indexes:* `idx_inboxes_account` (`(account_id)`).

---

#### Table: `conversations`
Unified conversation threads aggregating messages between contact and agents.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `BIGINT` | `PRIMARY KEY AUTO_INCREMENT` | - | Unique conversation thread ID |
| `account_id` | `BIGINT` | `NOT NULL, FK -> accounts(id)` | - | Workspace isolation key |
| `inbox_id` | `BIGINT` | `NOT NULL, FK -> inboxes(id)` | - | Originating channel inbox |
| `contact_id` | `BIGINT` | `NOT NULL, FK -> contacts(id)` | - | Interacting customer entity |
| `status` | `INTEGER` | `NOT NULL` | `0` | Status enum (0=open, 1=resolved, 2=pending) |
| `assignee_id` | `BIGINT` | `NULLABLE, FK -> users(id)` | `NULL` | Assigned support agent |
| `created_at` | `TIMESTAMP` | `NOT NULL` | `NOW()` | Thread start timestamp |

*Indexes:* `idx_conv_account_status` (`(account_id, status)`), `idx_conv_contact` (`(contact_id)`).
"""

chatwoot_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

The following sequence details how an inbound visitor message is parsed, saved, and pushed to agents in real time via ActionCable WebSockets.

```mermaid
sequenceDiagram
    autonumber
    actor Visitor as Web Widget Visitor
    participant WidgetAPI as Chatwoot Public API
    participant MsgSvc as Messages::CreateService
    participant DB as PostgreSQL Database
    participant Sidekiq as Redis / Sidekiq Workers
    participant Cable as ActionCable Server
    actor Agent as Support Agent Dashboard

    Visitor->>WidgetAPI: POST /public/api/v1/inboxes/{token}/messages (content)
    WidgetAPI->>MsgSvc: perform(account, contact, conversation, content)
    MsgSvc->>DB: INSERT into messages (conversation_id, content, message_type=incoming)
    DB-->>MsgSvc: Message created (id: 501)
    
    MsgSvc->>Sidekiq: Enqueue WebhookJob & NotificationJob
    MsgSvc->>Cable: broadcastToAccount(account_id, 'message.created', msg_json)
    Cable-->>Agent: ActionCable WebSocket frame pushed to live UI
    
    Agent->>WidgetAPI: POST /api/v1/accounts/1/conversations/10/messages (Agent Reply)
    WidgetAPI->>DB: INSERT into messages (message_type=outgoing)
    WidgetAPI->>Cable: Push outgoing frame to Visitor Web Widget
    Cable-->>Visitor: Live reply bubble displayed
```
"""

# ==========================================
# 2. zammad
# ==========================================
zammad_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class Ticket {
        +BigInt id
        +String number
        +String title
        +BigInt groupId
        +BigInt customerId
        +BigInt ownerId
        +Int stateId
        +Int priorityId
        +close() Void
        +assign(userId) Void
    }

    class TicketArticle {
        +BigInt id
        +BigInt ticketId
        +BigInt senderId
        +String from
        +String to
        +Text body
        +String contentType
    }

    class User {
        +BigInt id
        +String email
        +String firstname
        +String lastname
        +List~Role~ roles
    }

    class Group {
        +BigInt id
        +String name
        +Boolean active
    }

    class ChannelEmailParser {
        +fetchUnread() List~RawEmail~
        +parseEmailToArticle(raw) TicketArticle
    }

    Ticket "1" *-- "many" TicketArticle : contains
    Group "1" *-- "many" Ticket : groups
    User "1" *-- "many" Ticket : requests_or_owns
    ChannelEmailParser ..> Ticket : creates_from_email
```
"""

zammad_db = """
### 6.1 Comprehensive Database Schema Specification

Zammad runs on PostgreSQL or MySQL with Elasticsearch full-text search indexing and relational ticket auditing.

#### Table: `tickets`
Central customer support tickets tracked by unique alphanumeric numbers.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `BIGINT` | `PRIMARY KEY AUTO_INCREMENT` | - | Unique internal ticket ID |
| `number` | `VARCHAR(255)` | `NOT NULL, UNIQUE` | - | Customer-facing ticket number (e.g. `20261001`) |
| `title` | `VARCHAR(255)` | `NOT NULL` | - | Ticket subject title |
| `group_id` | `BIGINT` | `NOT NULL, FK -> groups(id)` | - | Department / queue group |
| `customer_id` | `BIGINT` | `NOT NULL, FK -> users(id)` | - | Submitting user profile |
| `owner_id` | `BIGINT` | `NULLABLE, FK -> users(id)` | `NULL` | Assigned agent |
| `state_id` | `INTEGER` | `NOT NULL` | `1` | Ticket state (1=new, 2=open, 3=pending, 4=closed) |
| `priority_id` | `INTEGER` | `NOT NULL` | `2` | Priority (1=low, 2=normal, 3=high) |
| `created_at` | `TIMESTAMP` | `NOT NULL` | `NOW()` | Creation timestamp |

*Indexes:* `idx_tickets_number` (`UNIQUE (number)`), `idx_tickets_customer` (`(customer_id)`).

---

#### Table: `ticket_articles`
Threaded conversational communications within a ticket.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `BIGINT` | `PRIMARY KEY AUTO_INCREMENT` | - | Unique article ID |
| `ticket_id` | `BIGINT` | `NOT NULL, FK -> tickets(id)` | - | Parent ticket |
| `type_id` | `INTEGER` | `NOT NULL` | `1` | Type (1=email, 2=phone, 3=note, 4=web) |
| `from` | `VARCHAR(255)` | `NOT NULL` | - | Sender email / identity |
| `to` | `VARCHAR(255)` | `NULLABLE` | `NULL` | Recipient email |
| `body` | `TEXT` | `NOT NULL` | - | Article message body |
| `created_at` | `TIMESTAMP` | `NOT NULL` | `NOW()` | Timestamp |

*Indexes:* `idx_articles_ticket` (`(ticket_id)`).
"""

zammad_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

The following sequence details how Zammad fetches incoming support emails, indexes them in Elasticsearch, and triggers routing workflows.

```mermaid
sequenceDiagram
    autonumber
    actor Customer as Email Sender
    participant IMAP as Mailbox Server (IMAP)
    participant Parser as Channel::EmailParser
    participant DB as PostgreSQL Database
    participant ES as Elasticsearch Engine
    participant Trigger as Trigger Engine
    actor Agent as Support Specialist

    Customer->>IMAP: Dispatches support email (subject: "System Down")
    Parser->>IMAP: Fetch new unread RFC822 messages
    IMAP-->>Parser: Return raw email mime data
    
    Parser->>DB: Find or create User by email
    DB-->>Parser: Return customer_id
    
    Parser->>DB: INSERT into tickets (title, customer_id, group_id, state_id=new)
    DB-->>Parser: Ticket created (number: 99401)
    
    Parser->>DB: INSERT into ticket_articles (ticket_id, body, from)
    Parser->>ES: Index ticket & article content for full-text search
    
    Parser->>Trigger: Evaluate trigger conditions ("Subject contains Down")
    Trigger->>DB: UPDATE tickets SET priority_id = 3 (High Priority)
    Trigger-->>Agent: Send urgent desktop notification & email alert
```
"""

# ==========================================
# 3. freescout
# ==========================================
freescout_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class Mailbox {
        +BigInt id
        +String name
        +String email
        +List~Conversation~ conversations
    }

    class Conversation {
        +BigInt id
        +BigInt mailboxId
        +BigInt customerId
        +String subject
        +Int status
        +BigInt userId
    }

    class Thread {
        +BigInt id
        +BigInt conversationId
        +Int type
        +Text body
        +BigInt createdBy
    }

    class Customer {
        +BigInt id
        +String firstName
        +String lastName
        +List~String~ emails
    }

    class FetchEmailsJob {
        +BigInt mailboxId
        +fetchImapMessages() Void
    }

    Mailbox "1" *-- "many" Conversation : organizes
    Conversation "1" *-- "many" Thread : contains
    Customer "1" *-- "many" Conversation : initiates
    FetchEmailsJob ..> Mailbox : pulls_from
```
"""

freescout_db = """
### 6.1 Comprehensive Database Schema Specification

FreeScout utilizes Laravel Eloquent on MySQL with strict mailbox scoping.

#### Table: `mailboxes`
Team mailboxes mapped to specific shared inboxes (e.g. `support@company.com`).

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `INT UNSIGNED` | `PRIMARY KEY AUTO_INCREMENT` | - | Mailbox identifier |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Display title of mailbox |
| `email` | `VARCHAR(255)` | `NOT NULL, UNIQUE` | - | Incoming email address |
| `created_at` | `TIMESTAMP` | `NOT NULL` | `NOW()` | Creation timestamp |

*Indexes:* `idx_mailboxes_email` (`UNIQUE (email)`).

---

#### Table: `conversations`
Support conversations grouped under specific mailboxes.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `INT UNSIGNED` | `PRIMARY KEY AUTO_INCREMENT` | - | Unique conversation ID |
| `mailbox_id` | `INT UNSIGNED` | `NOT NULL, FK -> mailboxes(id)`| - | Parent mailbox |
| `customer_id`| `INT UNSIGNED` | `NOT NULL, FK -> customers(id)`| - | Customer contact |
| `subject` | `VARCHAR(255)` | `NOT NULL` | - | Email subject thread |
| `status` | `TINYINT` | `NOT NULL` | `1` | 1=Active, 2=Pending, 3=Closed |
| `user_id` | `INT UNSIGNED` | `NULLABLE, FK -> users(id)` | `NULL` | Assigned agent |

*Indexes:* `idx_conv_mailbox_status` (`(mailbox_id, status)`).
"""

freescout_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

The following sequence details how FreeScout executes scheduled email retrieval and converts threads into assigned support conversations.

```mermaid
sequenceDiagram
    autonumber
    participant Cron as Laravel Scheduler
    participant Job as FetchEmailsJob
    participant MailServer as IMAP Server
    participant DB as MySQL Database
    actor Agent as FreeScout Agent

    Cron->>Job: Dispatch FetchEmailsJob(mailbox_id=1)
    Job->>MailServer: Connect IMAP & fetch unseen messages
    MailServer-->>Job: Return message headers & body
    
    Job->>DB: Check if customer exists by From email
    DB-->>Job: Customer record (id: 44)
    
    Job->>DB: INSERT into conversations (mailbox_id, customer_id, subject, status=1)
    DB-->>Job: Conversation created (id: 108)
    
    Job->>DB: INSERT into threads (conversation_id, type=customer, body=...)
    DB-->>Job: Thread logged
    
    Job-->>Agent: Notify assigned mailbox agents via browser push
```
"""

# ==========================================
# 4. helpdesk
# ==========================================
helpdesk_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class HelpdeskTicketService {
        +createTicket(data) Ticket
        +assignTicket(ticketId, agentId) Void
        +resolveTicket(ticketId) Void
    }

    class SlaPolicyEngine {
        +evaluateBreach(ticketId) Boolean
        +calculateTimeRemaining(ticketId) Int
    }

    class Ticket {
        +UUID id
        +String subject
        +String status
        +String priority
        +UUID customerId
        +UUID agentId
    }

    class TicketComment {
        +UUID id
        +UUID ticketId
        +String author
        +Text content
    }

    HelpdeskTicketService --> Ticket : manages
    HelpdeskTicketService --> SlaPolicyEngine : enforces_sla
    Ticket "1" *-- "many" TicketComment : contains
```
"""

helpdesk_db = """
### 6.1 Comprehensive Database Schema Specification

Generic relational helpdesk persistence schema tracking tickets, SLAs, and customer comments.

#### Table: `tickets`
Support requests submitted by clients.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Unique ticket identifier |
| `subject` | `VARCHAR(255)` | `NOT NULL` | - | Ticket title |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'open'` | State (`open`, `in_progress`, `resolved`) |
| `priority` | `VARCHAR(50)` | `NOT NULL` | `'normal'` | Priority (`low`, `normal`, `high`) |
| `customer_id` | `UUID` | `NOT NULL` | - | Requesting user |
| `agent_id` | `UUID` | `NULLABLE` | `NULL` | Assigned agent |

*Indexes:* `idx_hd_status` (`(status)`), `idx_hd_customer` (`(customer_id)`).
"""

helpdesk_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor User as Submitting Customer
    participant API as Helpdesk REST API
    participant Svc as HelpdeskTicketService
    participant SLA as SlaPolicyEngine
    participant DB as PostgreSQL Database

    User->>API: POST /api/tickets (subject, description)
    API->>Svc: createTicket(data)
    Svc->>DB: INSERT into tickets (subject, status='open')
    DB-->>Svc: Ticket created (id: tk_99)
    Svc->>SLA: calculateTimeRemaining(tk_99)
    SLA-->>Svc: SLA deadline: 4 hours
    Svc-->>API: 201 Created
    API-->>User: Ticket confirmation with SLA guarantee
```
"""

# ==========================================
# 5. libredesk
# ==========================================
libredesk_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class TicketEngine {
        +submitTicket(req) LibreTicket
        +replyTicket(id, body) Message
    }

    class LibreTicket {
        +UUID id
        +String title
        +String category
        +String status
    }

    class Message {
        +UUID id
        +UUID ticketId
        +String sender
        +Text body
    }

    TicketEngine --> LibreTicket : creates
    LibreTicket "1" *-- "many" Message : logs
```
"""

libredesk_db = """
### 6.1 Comprehensive Database Schema Specification

Lightweight ticket storage utilizing relational tables for categories and tickets.

#### Table: `tickets`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Ticket ID |
| `title` | `VARCHAR(255)` | `NOT NULL` | - | Summary title |
| `category` | `VARCHAR(100)` | `NOT NULL` | `'general'` | Category tag |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'open'` | Status |

*Indexes:* `idx_libre_status` (`(status)`).
"""

libredesk_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor User as User
    participant App as LibreDesk App
    participant Engine as TicketEngine
    participant DB as Database

    User->>App: Submits ticket
    App->>Engine: submitTicket(data)
    Engine->>DB: INSERT into tickets
    DB-->>Engine: Confirmed
    Engine-->>App: Ticket created
```
"""

# ==========================================
# 6. chaskiq
# ==========================================
chaskiq_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class App {
        +BigInt id
        +String name
        +String key
        +List~Conversation~ conversations
    }

    class Conversation {
        +BigInt id
        +BigInt appId
        +BigInt appUserId
        +Int state
    }

    class Message {
        +BigInt id
        +BigInt conversationId
        +Text message
    }

    class AppUser {
        +BigInt id
        +BigInt appId
        +String email
    }

    App "1" *-- "many" Conversation : hosts
    Conversation "1" *-- "many" Message : logs
    AppUser "1" *-- "many" Conversation : starts
```
"""

chaskiq_db = """
### 6.1 Comprehensive Database Schema Specification

Chaskiq persists conversational data using PostgreSQL and Ruby on Rails.

#### Table: `conversations`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `BIGINT` | `PRIMARY KEY AUTO_INCREMENT` | - | Conversation ID |
| `app_id` | `BIGINT` | `NOT NULL, FK -> apps(id)` | - | Parent app key |
| `app_user_id`| `BIGINT` | `NOT NULL, FK -> app_users(id)`| - | Participating user |
| `state` | `INTEGER` | `NOT NULL` | `0` | Active state |

*Indexes:* `idx_chaskiq_app` (`(app_id)`).
"""

chaskiq_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor Customer as App User
    participant Widget as Chaskiq Web Widget
    participant Backend as Rails API
    participant DB as PostgreSQL Database

    Customer->>Widget: Send message
    Widget->>Backend: POST /api/conversations
    Backend->>DB: INSERT into messages
    DB-->>Backend: Created
    Backend-->>Widget: Render response bubble
```
"""

# ==========================================
# 7. brightbean-chat
# ==========================================
brightbean_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class ChatSessionManager {
        +createSession(visitorId) ChatSession
        +routeToAgent(sessionId) Void
    }

    class VisitorTracker {
        +recordPageview(visitorId, url) Void
    }

    class ChatSession {
        +UUID id
        +UUID visitorId
        +UUID agentId
        +String status
    }

    ChatSessionManager --> ChatSession : manages
```
"""

brightbean_db = """
### 6.1 Comprehensive Database Schema Specification

Brightbean persists live visitor sessions and agent chat histories.

#### Table: `chat_sessions`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Session ID |
| `visitor_id` | `UUID` | `NOT NULL` | - | Visitor tracking ID |
| `agent_id` | `UUID` | `NULLABLE` | `NULL` | Assigned agent |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'active'` | Session state |

*Indexes:* `idx_bb_status` (`(status)`).
"""

brightbean_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor Visitor as Website Visitor
    participant App as Brightbean Chat
    participant Manager as ChatSessionManager
    participant DB as Database

    Visitor->>App: Clicks "Chat With Us"
    App->>Manager: createSession(visitorId)
    Manager->>DB: INSERT into chat_sessions
    DB-->>Manager: Session initialized
    Manager-->>App: Session open
```
"""

# Execute insertions for Domain 3
insert_domain3('docs/wiki/chatwoot.md', chatwoot_cd, chatwoot_db, chatwoot_seq)
insert_domain3('docs/wiki/zammad.md', zammad_cd, zammad_db, zammad_seq)
insert_domain3('docs/wiki/freescout.md', freescout_cd, freescout_db, freescout_seq)
insert_domain3('docs/wiki/helpdesk.md', helpdesk_cd, helpdesk_db, helpdesk_seq)
insert_domain3('docs/wiki/libredesk.md', libredesk_cd, libredesk_db, libredesk_seq)
insert_domain3('docs/wiki/chaskiq.md', chaskiq_cd, chaskiq_db, chaskiq_seq)
insert_domain3('docs/wiki/brightbean-chat.md', brightbean_cd, brightbean_db, brightbean_seq)
