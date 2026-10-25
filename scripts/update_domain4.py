import os
import re

def insert_domain4(filepath, class_diagram, db_schema, seq_diagram):
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

# 1. evolution-api
evolution_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class WhatsAppInstanceService {
        +PrismaClient prisma
        +createInstance(instanceName) Instance
        +connectSession(instanceName) QrCodeResult
        +getInstanceSocket(instanceName) WASocket
        +deleteInstance(instanceName) Void
    }

    class BaileysSocketManager {
        +Map instances
        +initWASocket(instance) WASocket
        +handleConnectionUpdate(update) Void
        +handleCredsUpdate(creds) Void
    }

    class MessageSenderService {
        +sendTextMessage(instanceName, number, text) SendResult
        +sendMediaMessage(instanceName, number, mediaUrl) SendResult
        +sendButtonMessage(instanceName, number, buttons) SendResult
    }

    class WebhookDispatcherService {
        +dispatchWebhook(event, instanceName, data) Promise~Void~
    }

    class Instance {
        +String id
        +String name
        +String connectionStatus
        +String ownerJid
        +JSON token
    }

    class Message {
        +String id
        +String keyId
        +String instanceId
        +String remoteJid
        +String messageType
        +JSON content
    }

    WhatsAppInstanceService --> BaileysSocketManager : orchestrates
    MessageSenderService --> BaileysSocketManager : sends_via_socket
    BaileysSocketManager --> WebhookDispatcherService : notifies
    Instance "1" *-- "many" Message : logs
```
"""

evolution_db = """
### 6.1 Comprehensive Database Schema Specification

Evolution API uses PostgreSQL with Prisma ORM to manage instance configurations, credentials, and message ledgers.

#### Table: `Instance`
Stores provisioned multi-device WhatsApp sessions and connection state.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `TEXT` | `PRIMARY KEY` | `cuid()` | Unique instance ID |
| `name` | `TEXT` | `NOT NULL, UNIQUE` | - | Unique session name (e.g. `support_line_1`) |
| `connectionStatus`| `TEXT` | `NOT NULL` | `'close'` | Connection state (`open`, `close`, `connecting`) |
| `ownerJid` | `TEXT` | `NULLABLE` | `NULL` | WhatsApp JID of the connected smartphone |
| `token` | `TEXT` | `NULLABLE` | `NULL` | API authorization bearer token |
| `createdAt` | `TIMESTAMP(3)` | `NOT NULL` | `CURRENT_TIMESTAMP` | Creation timestamp |

*Indexes:* `Instance_name_key` (`UNIQUE (name)`).

---

#### Table: `Message`
Historical record of inbound and outbound WhatsApp chat messages.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `TEXT` | `PRIMARY KEY` | `cuid()` | Internal message ID |
| `keyId` | `TEXT` | `NOT NULL` | - | WhatsApp protocol message ID |
| `instanceId` | `TEXT` | `NOT NULL, FK -> Instance(id)` | - | Associated session instance |
| `remoteJid` | `TEXT` | `NOT NULL` | - | Destination / Sender JID (e.g. `5511999999999@s.whatsapp.net`) |
| `fromMe` | `BOOLEAN` | `NOT NULL` | `false` | True if sent outbound |
| `messageType` | `TEXT` | `NOT NULL` | `'conversation'` | Type (`conversation`, `imageMessage`, `documentMessage`) |
| `createdAt` | `TIMESTAMP(3)` | `NOT NULL` | `CURRENT_TIMESTAMP` | Ingestion timestamp |

*Indexes:* `Message_instanceId_idx` (`(instanceId)`), `Message_remoteJid_idx` (`(remoteJid)`).
"""

evolution_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

The following sequence details the full lifecycle from instance provisioning to QR code pairing and automated message dispatch.

```mermaid
sequenceDiagram
    autonumber
    actor Client as API Consumer / CRM
    participant API as Evolution Express API
    participant Svc as WhatsAppInstanceService
    participant Baileys as Baileys WASocket Manager
    participant DB as PostgreSQL Database
    actor WA as WhatsApp Mobile App

    Client->>API: POST /instance/create (name: "sales-agent-1")
    API->>Svc: createInstance("sales-agent-1")
    Svc->>DB: INSERT into Instance (name, connectionStatus='close')
    DB-->>Svc: Created
    
    Client->>API: GET /instance/connect/sales-agent-1
    API->>Baileys: initWASocket("sales-agent-1")
    Baileys-->>API: Emit QR Code Data URI
    API-->>Client: Return QR Code image
    
    WA->>Client: User scans QR code with phone camera
    Baileys->>Baileys: Exchange Noise Protocol encryption keys
    Baileys->>DB: UPDATE Instance SET connectionStatus='open', ownerJid=...
    
    Client->>API: POST /message/sendText/sales-agent-1 (number, text)
    API->>Baileys: socket.sendMessage(jid, {text})
    Baileys->>WA: Dispatches WhatsApp Protobuf packet
    WA-->>Baileys: Message delivered acknowledgement
    Baileys->>DB: INSERT into Message (keyId, remoteJid, fromMe=true)
    API-->>Client: 200 OK (Message Sent)
```
"""

# 2. wppconnect
wppconnect_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class WppSession {
        +String sessionName
        +Page puppeteerPage
        +startSession() Promise~Boolean~
        +getQrCode() String
        +sendText(to, message) Promise~Result~
        +close() Promise~Void~
    }

    class PuppeteerPageController {
        +launchBrowser() Browser
        +navigateToWhatsAppWeb(page) Void
        +injectWppJs(page) Void
    }

    class MessageListener {
        +onMessage(handler) Void
        +onAck(handler) Void
    }

    WppSession --> PuppeteerPageController : manages_browser
    WppSession --> MessageListener : binds_events
```
"""

wppconnect_db = """
### 6.1 Comprehensive Database Schema Specification

WPPConnect utilizes an in-memory session registry and file-based/Redis multi-device token storage.

#### Table: `session_tokens`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `session_name` | `VARCHAR(100)` | `PRIMARY KEY` | - | Session identifier |
| `token_data` | `TEXT` | `NOT NULL` | - | Encrypted base64 authentication keys |
| `updated_at` | `TIMESTAMP` | `NOT NULL` | `NOW()` | Token sync timestamp |

*Indexes:* Primary key on `session_name`.
"""

wppconnect_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor User as Client
    participant WPP as WPPConnect Server
    participant Chrome as Headless Chromium
    actor Phone as WhatsApp Mobile

    User->>WPP: createSession("bot-session")
    WPP->>Chrome: Launch page & open web.whatsapp.com
    Chrome-->>WPP: QR Code detected
    WPP-->>User: Return QR Code
    Phone->>Chrome: User scans QR
    Chrome-->>WPP: Authenticated
    User->>WPP: sendText("+15551234567", "Hello!")
    WPP->>Chrome: Execute WPP.chat.sendTextMessage(...)
    Chrome-->>User: Message dispatched
```
"""

# 3. OpenWA
openwa_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class OpenWAClient {
        +create(config) Client
        +sendText(to, text) Promise~Id~
        +onAnyMessage(callback) Void
    }

    class HostDevice {
        +String pushname
        +String wid
        +Boolean connected
    }

    OpenWAClient --> HostDevice : queries
```
"""

openwa_db = """
### 6.1 Comprehensive Database Schema Specification

OpenWA stores local session tokens and client state via SQLite or persistent file directories.

#### Table: `openwa_sessions`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `VARCHAR(100)` | `PRIMARY KEY` | - | Session name |
| `auth_state` | `TEXT` | `NOT NULL` | - | Serialized auth tokens |
"""

openwa_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    participant App as Node.js App
    participant Client as OpenWA Client
    participant WA as WhatsApp Web

    App->>Client: create({sessionId: "main"})
    Client->>WA: Connect session
    WA-->>Client: Session Ready
    App->>Client: sendText(phone, "Hello")
    Client->>WA: Dispatched
```
"""

# 4. textbee
textbee_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class GatewayServer {
        +enqueueSms(deviceId, recipient, body) SmsJob
        +registerDevice(deviceToken) Device
    }

    class DeviceManager {
        +Map activeWebSockets
        +pushJobToDevice(deviceId, job) Boolean
    }

    class SmsJob {
        +UUID id
        +String recipient
        +String body
        +String status
    }

    GatewayServer --> DeviceManager : delegates
    GatewayServer --> SmsJob : creates
```
"""

textbee_db = """
### 6.1 Comprehensive Database Schema Specification

Textbee persists devices and SMS dispatch queues within PostgreSQL.

#### Table: `devices`
Android smartphones operating as physical cellular SMS gateways.

| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Device identifier |
| `name` | `VARCHAR(100)` | `NOT NULL` | - | Device model name |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'offline'` | Connection state |
| `api_key` | `VARCHAR(255)` | `NOT NULL, UNIQUE` | - | Authentication token |

*Indexes:* `idx_devices_key` (`UNIQUE (api_key)`).

---

#### Table: `sms_queue`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | SMS Job ID |
| `device_id` | `UUID` | `NOT NULL, FK -> devices(id)` | - | Target phone |
| `recipient` | `VARCHAR(50)` | `NOT NULL` | - | Destination number |
| `message` | `TEXT` | `NOT NULL` | - | SMS content |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'pending'` | State (`pending`, `sent`, `failed`) |

*Indexes:* `idx_sms_status` (`(status)`).
"""

textbee_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor WebApp as Web Application
    participant Server as Textbee Gateway
    participant Android as Android Phone App
    actor Recipient as SMS Recipient

    WebApp->>Server: POST /api/v1/sms (recipient, message)
    Server->>Server: Enqueue into sms_queue
    Server->>Android: Push WebSocket event: `dispatch_sms`
    Android->>Recipient: Send SMS via native Android SIM card
    Recipient-->>Android: GSM Delivery Receipt
    Android->>Server: HTTP callback: `status=delivered`
    Server-->>WebApp: Delivery webhook emitted
```
"""

# 5. playsms
playsms_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class SmsGatewayManager {
        +routeSms(smsId) Void
        +checkGatewayStatus() Boolean
    }

    class UserCreditManager {
        +deductCredit(userId, amount) Boolean
        +checkBalance(userId) Float
    }

    class playsms_tblSMSOutgoing {
        +Int id
        +Int uid
        +String p_dst
        +String p_msg
        +Int p_status
    }

    SmsGatewayManager --> UserCreditManager : validates_balance
    SmsGatewayManager --> playsms_tblSMSOutgoing : updates
```
"""

playsms_db = """
### 6.1 Comprehensive Database Schema Specification

PlaySMS stores users, credits, and outgoing SMS messages in MySQL.

#### Table: `playsms_tblSMSOutgoing`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `smslog_id` | `INT` | `PRIMARY KEY AUTO_INCREMENT` | - | Log ID |
| `uid` | `INT` | `NOT NULL` | `0` | User account ID |
| `p_dst` | `VARCHAR(100)` | `NOT NULL` | `''` | Destination phone |
| `p_msg` | `TEXT` | `NOT NULL` | - | Message body |
| `p_status` | `INT` | `NOT NULL` | `0` | 0=Pending, 1=Sent, 2=Failed |

*Indexes:* `idx_outgoing_status` (`(p_status)`).
"""

playsms_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor User as PlaySMS User
    participant Web as PlaySMS PHP Web
    participant Daemon as playsmsd Daemon
    participant Gateway as GSM / Kannel Gateway

    User->>Web: Submit SMS message
    Web->>Web: Deduct credit from user balance
    Web->>Web: INSERT into playsms_tblSMSOutgoing (p_status=0)
    Daemon->>Daemon: Poll table for p_status=0
    Daemon->>Gateway: Relay to Kannel / Gammu
    Gateway-->>Daemon: Status: Sent
    Daemon->>Web: UPDATE p_status = 1
```
"""

# 6. ntfy
ntfy_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class NtfyServer {
        +publishMessage(topic, message) Void
        +subscribeTopic(topic, subscriber) Stream
    }

    class Topic {
        +String name
        +List~Subscriber~ subscribers
        +broadcast(msg) Void
    }

    class Message {
        +String id
        +String topic
        +String title
        +String message
        +Int priority
        +Timestamp time
    }

    NtfyServer "1" *-- "many" Topic : manages
    Topic "1" *-- "many" Message : buffers
```
"""

ntfy_db = """
### 6.1 Comprehensive Database Schema Specification

Ntfy caches recent push notification payloads in SQLite or memory.

#### Table: `messages`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `TEXT` | `PRIMARY KEY` | - | Message ID |
| `time` | `INTEGER` | `NOT NULL` | - | Epoch timestamp |
| `topic` | `TEXT` | `NOT NULL` | - | Topic string |
| `message` | `TEXT` | `NOT NULL` | - | Content text |
| `title` | `TEXT` | `NULLABLE` | `NULL` | Optional notification title |
| `priority` | `INTEGER` | `NOT NULL` | `3` | Priority (1-5) |

*Indexes:* `idx_messages_topic_time` (`(topic, time)`).
"""

ntfy_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor Publisher as Alert Script / Server
    participant Ntfy as Ntfy HTTP Server
    participant DB as SQLite Cache
    actor Subscriber as Phone / Desktop Client

    Subscriber->>Ntfy: GET /mytopic/json (SSE / WebSocket)
    Publisher->>Ntfy: POST /mytopic (Title: "Alert", Body: "Disk 95% full")
    Ntfy->>DB: INSERT into messages
    Ntfy->>Subscriber: Push JSON event over open stream
    Subscriber-->>Subscriber: Display native push notification
```
"""

# 7. jasmin
jasmin_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class SMPPClientManager {
        +bindTransceiver(smscConfig) Void
        +submitSm(pdu) SubmitSmResp
    }

    class Router {
        +selectRoute(user, destination) SmscRoute
    }

    class BillingInterceptor {
        +checkBalance(user, cost) Boolean
        +charge(user, cost) Void
    }

    SMPPClientManager --> Router : routes
    SMPPClientManager --> BillingInterceptor : charges
```
"""

jasmin_db = """
### 6.1 Comprehensive Database Schema Specification

Jasmin SMS Gateway stores routing configurations and balances in Redis and logging stores.

#### Table: `smsc_routes`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `route_id` | `VARCHAR(50)` | `PRIMARY KEY` | - | Route ID |
| `smsc_connector`| `VARCHAR(50)` | `NOT NULL` | - | Bound upstream SMPC |
| `rate` | `DECIMAL(8, 4)` | `NOT NULL` | `0.0100` | Cost per SMS segment |
"""

jasmin_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor App as Client Application
    participant HTTP as Jasmin HTTP / SMPP Interface
    participant Router as Jasmin Router
    participant Redis as Redis Cache
    participant SMSC as Upstream Telco SMSC

    App->>HTTP: POST /send (to, content, username, password)
    HTTP->>Redis: Verify balance & quota
    HTTP->>Router: Select cheapest/optimal SMSC connector
    Router->>SMSC: SMPP `submit_sm` PDU
    SMSC-->>Router: `submit_sm_resp` (Message ID)
    HTTP-->>App: 200 OK (SMS ID)
```
"""

# Execute insertions for Domain 4
insert_domain4('docs/wiki/evolution-api.md', evolution_cd, evolution_db, evolution_seq)
insert_domain4('docs/wiki/wppconnect.md', wppconnect_cd, wppconnect_db, wppconnect_seq)
insert_domain4('docs/wiki/OpenWA.md', openwa_cd, openwa_db, openwa_seq)
insert_domain4('docs/wiki/textbee.md', textbee_cd, textbee_db, textbee_seq)
insert_domain4('docs/wiki/playsms.md', playsms_cd, playsms_db, playsms_seq)
insert_domain4('docs/wiki/ntfy.md', ntfy_cd, ntfy_db, ntfy_seq)
insert_domain4('docs/wiki/jasmin.md', jasmin_cd, jasmin_db, jasmin_seq)
