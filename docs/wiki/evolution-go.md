# evolution-go (Evolution Go) - Architecture & Technical Wiki

> **High-Performance, Lightweight WhatsApp API Gateway Engineered in Go & Whatsmeow**  
> GitHub: [evolution-foundation/evolution-go](https://github.com/evolution-foundation/evolution-go) | Stack: Go 1.25+, Gin Framework, Whatsmeow, GORM (PostgreSQL / SQLite), RabbitMQ, NATS, MinIO / S3, Docker | License: Apache-2.0

---

## 1. Executive Summary & Value Proposition

**evolution-go** is the compiled, high-throughput WhatsApp messaging engine developed by the Evolution Foundation. Engineered as a high-performance alternative to Node.js/Baileys-based WhatsApp gateways (such as [evolution-api](evolution-api.md)), Evolution Go leverages the raw concurrency, low memory footprint, and goroutine parallelism of Go paired with the [whatsmeow](https://github.com/tulir/whatsmeow) library.

Evolution Go provides a multi-tenant RESTful API, embedded Swagger documentation, multi-instance session management, automated QR code generation, and multi-broker event streaming (Webhooks, WebSockets, RabbitMQ, and NATS) for enterprise high-volume WhatsApp communication.

### Core Problems Solved
- **Resource Exhaustion in Node.js WhatsApp Gateways**: Node.js/Puppeteer/Baileys stacks often consume hundreds of megabytes per connected session. Evolution Go drastically reduces memory consumption to tens of megabytes per WhatsApp instance, allowing hundreds of concurrent instances on modest hardware.
- **High-Velocity Message Dispatch**: Capable of processing thousands of inbound/outbound messages per second without event loop lag.
- **Enterprise Event Fanout**: Dispatches real-time WhatsApp events (message received, message status, presence updates) across multiple enterprise event brokers simultaneously: RabbitMQ (AMQP), NATS, WebSockets, and HTTP webhooks.
- **Decoupled Media Storage**: Automatically downloads, decrypts, and uploads incoming images, audio voice notes, videos, and documents to S3-compatible object storage (MinIO/AWS S3), preventing disk saturation.
- **Drop-in WhatsApp Provider**: Serves as the primary WhatsApp transport layer for CRM suites like [Evo CRM Community](evo-crm-community.md).

---

## 2. High-Level System Architecture

Evolution Go is structured as a modular Go service centered around the Gin HTTP framework, an internal instance manager managing `whatsmeow` client structs, and an event dispatcher publishing to queues and webhooks.

```mermaid
flowchart TD
    subgraph ClientAndIntegrations ["External Clients & Consumers"]
        EvoCRM["Evo CRM / External Apps"]
        AdminDashboard["Admin & Instance Manager UI"]
        WebSocketClients["Real-Time WebSocket Listeners"]
    end

    subgraph HttpIngress ["Gin HTTP Engine & Ingress Layer"]
        GinServer["Gin Web Framework (:8080)"]
        SwaggerDocs["Swagger / OpenAPI UI (/swagger/*)"]
        ApiKeyAuth["API Key Authentication Middleware"]
        InstanceRouter["Instance Route Dispatcher (/instance/:name/...)"]
    end

    subgraph InstanceManagerCore ["Session & Instance Manager (pkg/instance)"]
        InstanceManager["Multi-Instance Manager"]
        
        subgraph WhatsAppClients ["Active Whatsmeow Client Pool"]
            Client1["Instance: Support_BR (whatsmeow.Client)"]
            Client2["Instance: Sales_LATAM (whatsmeow.Client)"]
            ClientN["Instance: Alerts_Global (whatsmeow.Client)"]
        end
    end

    subgraph EventDispatcherHub ["Event Routing & Broker Subsystem (pkg/events)"]
        EventHub["Unified Event Bus"]
        WebhookDispatcher["HTTP Webhook Dispatcher"]
        AmqpPublisher["RabbitMQ AMQP Publisher"]
        NatsPublisher["NATS JetStream Publisher"]
        WsHub["WebSocket Broadcast Hub"]
    end

    subgraph PersistenceAndStorage ["Data & Media Subsystem"]
        RelationalDB[(PostgreSQL 15+ / SQLite WAL via GORM)]
        MinIO["MinIO / AWS S3 Media Store"]
    end

    EvoCRM -->|REST API Calls| GinServer
    AdminDashboard -->|Manage Instances & QR| GinServer
    WebSocketClients <-->|Real-Time WS Streams| WsHub

    GinServer --> SwaggerDocs
    GinServer --> ApiKeyAuth
    ApiKeyAuth --> InstanceRouter

    InstanceRouter --> InstanceManager
    InstanceManager --> WhatsAppClients

    WhatsAppClients <-->|Noise Protocol / WebSockets| WhatsAppServers[("WhatsApp Cellular Network")]

    WhatsAppClients -->|Inbound Messages & Receipts| EventHub
    EventHub --> WebhookDispatcher
    EventHub --> AmqpPublisher
    EventHub --> NatsPublisher
    EventHub --> WsHub

    WebhookDispatcher -->|Deliver to CRM| EvoCRM
    WhatsAppClients --> RelationalDB
    WhatsAppClients -->|Store Audio / Images| MinIO
```

---

### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class Instance {
        +String Name
        +String Status "connected, connecting, disconnected, qrcode"
        +String PhoneNumber
        +UUID Token
        +Client whatsmeowClient
        +Connect() Error
        +Disconnect() Void
        +GetQRCode() String
    }

    class MessageService {
        +SendTextMessage(instanceName, to, text) MessageResponse
        +SendMediaMessage(instanceName, to, mediaUrl, caption) MessageResponse
        +SendAudioMessage(instanceName, to, audioBuffer) MessageResponse
        +SendLocation(instanceName, to, lat, lng) MessageResponse
    }

    class EventDispatcher {
        +PublishEvent(eventName, payload) Void
        +RegisterWebhook(url, events) Void
        +PublishRabbitMQ(queueName, data) Error
        +PublishNATS(subject, data) Error
    }

    class MediaStorage {
        +UploadMedia(instanceName, mediaBytes, mimeType) String
        +GetMediaStream(mediaId) Stream
    }

    class GormDatabase {
        +SaveMessage(messageModel) Error
        +GetChatHistory(instanceName, jid, limit) List~Message~
    }

    Instance --> MessageService : executes_outbound
    Instance --> EventDispatcher : emits_inbound
    MessageService --> MediaStorage : fetches_attachments
    Instance --> GormDatabase : persists_state
```

---

## 3. Technology Stack & Key Dependencies

- **Language & Runtime**: Go 1.25+ (compiled binary, zero runtime dependency)
- **HTTP Web Framework**: `gin-gonic/gin` v1.10 (fast HTTP router, middleware chain, JSON binding)
- **API Documentation**: `swaggo/gin-swagger` v1.6 & `swaggo/swag` (OpenAPI specification auto-generation)
- **WhatsApp Core Protocol**: `go.mau.fi/whatsmeow` (Golang library implementing the WhatsApp Web multi-device protobuf and Noise encryption protocols)
- **ORM & Persistence**: `gorm.io/gorm` v1.25 with drivers for PostgreSQL (`gorm.io/driver/postgres`) and SQLite (`modernc.org/sqlite`)
- **Event Brokers**:
  - `rabbitmq/amqp091-go` v1.10 (AMQP 0-9-1 for enterprise queue consumers)
  - `nats-io/nats.go` v1.39 (ultra-fast cloud-native messaging)
  - `gorilla/websocket` v1.5 (bidirectional live socket streaming)
- **Object Storage**: `minio/minio-go/v7` v7.0.80 (S3-compatible bucket interactions)
- **Utilities**: `skip2/go-qrcode` (ASCII and PNG QR code rendering for browser terminal pairing)

---

## 4. Repository Layout & Key Modules

```
evolution-go/
├── cmd/
│   └── server/                        # Application entrypoint (main.go)
├── pkg/
│   ├── instance/                      # Instance lifecycle, registration, connection states
│   ├── whatsmeow/                     # Whatsmeow event listeners, pairing, protobuf wrappers
│   ├── sendMessage/                   # Outbound payloads (Text, Media, Audio, Buttons, Polls)
│   ├── message/                       # Message parsing, decryption, format conversions
│   ├── chat/                          # Chat history, mark-as-read, typing status indicators
│   ├── group/                         # Group creation, participant management, metadata
│   ├── community/                     # WhatsApp Community channel creation and management
│   ├── newsletter/                    # WhatsApp Newsletter / Broadcast channel APIs
│   ├── events/                        # Event publisher adapters (Webhook, AMQP, NATS, WS)
│   ├── storage/                       # MinIO / S3 upload adapters and asset cache
│   ├── middleware/                    # API key validation, rate-limiting, CORS, logging
│   └── routes/                        # Gin HTTP route declarations and Swagger annotations
├── manager/                           # Instance manager controller and background heartbeats
├── passkey-helper/                    # Passkey verification and authentication utilities
├── docs/                              # Generated Swagger docs (swagger.json, swagger.yaml)
├── Dockerfile                         # Multi-stage lightweight scratch/alpine container build
├── Makefile                           # Development, test, swagger-gen, and build automation
└── go.mod                             # Go module definition and dependencies
```

---

## 5. Core Features & Capabilities

### 5.1 Multi-Instance WhatsApp Isolation
Evolution Go is designed for multi-tenant SaaS environments:
- Each WhatsApp phone number operates as a named **Instance** (e.g., `company_support`, `marketing_alerts`).
- Every instance maintains its own cryptographic session keys, in-memory buffers, and isolated SQLite/PostgreSQL connection.
- Instances can be dynamically created, reconnected, or deleted at runtime via REST calls without restarting the Go process.

### 5.2 Multi-Broker Event Streaming
Incoming events (e.g., `MESSAGES_UPSERT`, `MESSAGES_UPDATE`, `CONNECTION_UPDATE`, `CALL_OFFER`) can be broadcast across multiple channels concurrently:
1. **HTTP Webhooks**: Instant HTTP POST deliveries to endpoint URLs with retry backoff.
2. **RabbitMQ**: Direct publishing to AMQP exchanges for worker-pool consumption.
3. **NATS**: Subject-based event distribution (`whatsapp.instance.events`).
4. **WebSockets**: Streamed to live dashboard UIs for sub-millisecond agent notifications.

### 5.3 Automated Media Transcoding & Cloud Offloading
When an audio voice note, high-resolution photo, or PDF contract is received:
- Evolution Go automatically requests the media decryption key from WhatsApp servers.
- It decrypts the stream in-memory, converts voice notes to compatible Opus/OGG formats, and streams the buffer directly to MinIO/S3.
- The webhook payload delivered to the CRM contains clean public URLs rather than bloated base64 data strings.

---

## 6. Data Models & Schema Architecture

```mermaid
erDiagram
    INSTANCES ||--o{ AUTH_SESSIONS : "holds"
    INSTANCES ||--o{ CONTACTS : "syncs"
    INSTANCES ||--o{ CHATS : "tracks"
    CHATS ||--o{ MESSAGES : "contains"
    INSTANCES ||--o{ WEBHOOKS : "notifies"

    INSTANCES {
        string name PK
        string status "connected, disconnected, qrcode"
        string phone_number
        string apikey UK
        timestamptz created_at
        timestamptz last_seen
    }

    AUTH_SESSIONS {
        string instance_name FK
        string jid PK
        text session_data
        timestamptz updated_at
    }

    CONTACTS {
        string id PK
        string instance_name FK
        string jid UK
        string push_name
        string profile_picture_url
    }

    CHATS {
        string id PK
        string instance_name FK
        string jid UK
        string name
        int unread_count
        timestamptz last_message_at
    }

    MESSAGES {
        string id PK
        string instance_name FK
        string chat_jid FK
        string sender_jid
        boolean from_me
        string message_type "text, image, audio, document"
        text content
        string media_url
        string status "pending, sent, delivered, read"
        timestamptz timestamp
    }

    WEBHOOKS {
        uuid id PK
        string instance_name FK
        string target_url
        boolean enabled
        json events
    }
```

---

## 7. Operational Workflows & Sequence Diagrams

### 7.1 Instance Provisioning & QR-Code Pairing Flow

```mermaid
sequenceDiagram
    autonumber
    actor Admin as System Admin / CRM
    participant API as Evolution Go (Gin HTTP)
    participant Mgr as Instance Manager
    participant Meow as Whatsmeow Core
    participant WA as WhatsApp Network

    Admin->>API: POST /instance/create (instanceName: "support_line", qrcode: true)
    API->>Mgr: Initialize instance context & DB state
    Mgr->>Meow: whatsmeow.NewClient(deviceStore)
    Meow->>WA: Connect via TLS & Noise Handshake
    WA-->>Meow: Pairing QR Code Data
    Meow-->>API: Emit QR Channel Event
    API-->>Admin: 201 Created (QRCode: base64/png string)

    Admin->>Admin: Scan QR code with physical mobile device
    WA->>Meow: Pair device confirmed & session tokens issued
    Meow->>Mgr: State change -> CONNECTED
    Mgr->>API: Broadcast event: CONNECTION_UPDATE (status: open)
    API-->>Admin: Webhook / WS Event (Instance Connected)
```

---

## 8. Strategic Value & Takeaways for MarketingStudio / SGrowing

### Reusable Architectural Patterns
1. **Lightweight Concurrency**: The Go + `whatsmeow` architecture proves that high-volume WhatsApp automation can be executed at a fraction of the RAM footprint required by Node.js or Chromium-based alternatives.
2. **Multi-Protocol Fanout**: The unified event dispatcher pattern (broadcasting events simultaneously to Webhooks, WebSockets, RabbitMQ, and NATS) is directly applicable to MarketingStudio’s webhook delivery architecture.
3. **Decoupled Media Ingestion**: Streaming binary payload decryption straight into S3/MinIO rather than serializing as base64 inside JSON requests keeps API payloads lean and prevents memory spikes.
