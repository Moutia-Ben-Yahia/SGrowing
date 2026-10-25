# asterisk - Architecture & Technical Wiki

> **Industry-Standard Open-Source PBX, Telephony Engine, Media Gateway & Programmable Voice Core**  
> GitHub: [asterisk/asterisk](https://github.com/asterisk/asterisk.git) | Stack: C (POSIX), PJSIP, WebRTC, RTP/SRTP, AudioSocket, Asterisk REST Interface (ARI), AMI, ODBC | License: GPL-2.0

---

## 1. Executive Summary & Value Proposition

**asterisk** is the world’s preeminent, open-source communications engine and private branch exchange (PBX) framework. Developed in highly optimized ANSI C, Asterisk powers enterprise telecommunications, VoIP gateways, call center automatic call distribution (ACD), interactive voice response (IVR) systems, and WebRTC media bridges globally. It abstracts the complexities of physical PSTN lines, digital telephony protocols, and IP multimedia sessions, enabling software developers to programmatically control audio, video, and signaling across heterogeneous networks.

In modern sales engineering and AI-driven contact centers, Asterisk acts as a resilient media plane. It converts web-based browser media streams (WebRTC DTLS-SRTP with Opus) into carrier-compatible SIP/RTP audio streams (G.711 µ-law/a-law, G.722, G.729), handles high-concurrency power dialing, records uncompressed multi-channel audio, and provides real-time streaming interfaces (ARI, AudioSocket) to attach external Large Language Models (LLMs) and Speech-to-Text (STT) speech pipelines.

### Core Problems Solved
- **Carrier VoIP Protocol Translation & Transcoding**: Seamlessly terminates SIP (Session Initiation Protocol), WebSockets (WSS), and raw RTP, performing real-time zero-copy audio codec transcoding between Opus, G.711, G.729, and linear PCM.
- **Programmable Channel Control (ARI & Stasis Bus)**: Provides external applications (Node.js, Go, Python) direct asynchronous control over channels, audio playbacks, DTMF tones, and media bridges via the WebSocket-driven Asterisk REST Interface (ARI).
- **Sub-Millisecond AI Voice Streaming**: Enables bidirectional, low-latency PCM audio streaming between live phone calls and AI speech models via `res_audiosocket` and ARI `externalMedia` endpoints.
- **Enterprise Contact Center Distribution**: Offers deterministic agent queue distribution algorithms (`app_queue`) with priority weighting, skill-based routing, round-robin, and wrap-up timers.
- **Synchronous High-Fidelity Call Recording**: Executes carrier-grade dual-channel audio capture (`app_mixmonitor`) directly in the media pipeline, recording the caller and agent on distinct stereo audio tracks for automated ML transcription.
- **Caller ID Authentication & Regulatory Compliance**: Native STIR/SHAKEN identity verification and signing (`res_pjsip_stir_shaken`) to validate caller ID authenticity and reduce robocall spam scores.

---

## 2. High-Level System Architecture

Asterisk employs an event-driven, multi-threaded C architecture where channel drivers terminate network protocols, dynamic translators process media frames, the Stasis message bus dispatches state events, and bridging engines mix or swap audio streams.

```mermaid
flowchart TD
    subgraph ClientSurfaces ["Telephony Ingress & Egress Endpoints"]
        BrowserRTC["Browser WebRTC Softphone\n(WSS / DTLS-SRTP / Opus)"]
        SIPTrunk["PSTN SIP Carrier Trunk\n(Twilio / Telnyx / Bandwidth)"]
        IPDeskPhone["Hardware SIP Handset\n(Grandstream / Yealink / Cisco)"]
    end

    subgraph AsteriskCoreEngine ["Asterisk C Telephony Core"]
        ChannelDrivers["Channel Drivers\n(chan_pjsip, chan_rtp)"]
        
        subgraph MediaPlane ["Media & Transcoding Plane"]
            RTPCore["RTP / SRTP Media Engine (res_rtp_asterisk)"]
            CodecMatrix["Codec Transcoding Matrix\n(codec_opus, codec_g711, codec_resample)"]
            JitterBuffer["Adaptive Jitter Buffer & PLC"]
            AudioSocket["AudioSocket Connector (res_audiosocket)"]
            MixMonitor["Dual-Channel Recorder (app_mixmonitor)"]
        end

        subgraph StasisCore ["Internal Messaging & Event Bus"]
            StasisBus["Stasis Message Bus (main/stasis.c)"]
            ChannelState["Channel & Bridge State Engine"]
        end

        subgraph BridgingPlane ["Bridging & Mixing Engine"]
            BridgeSimple["Simple 2-Party Bridge (bridge_simple)"]
            BridgeSoftmix["Conference Multi-Party Bridge (bridge_softmix)"]
            BridgeHolding["Holding / Music on Hold Bridge (bridge_holding)"]
        end

        subgraph ControlInterfaces ["Management & Programming Interfaces"]
            DialplanEngine["Dialplan PBX Engine (pbx_config / extensions.conf)"]
            ARISystem["Asterisk REST Interface (res_ari / res_http_websocket)"]
            AMISystem["Asterisk Manager Interface (manager.c :5038)"]
            FastAGI["Asterisk Gateway Interface (res_agi)"]
        end
    end

    subgraph ExternalEcosystem ["External Application & AI Cloud"]
        MarketingStudioApp["MarketingStudio Voice Gateway / Power Dialer"]
        SpeechAIService["Voice AI Pipeline / STT & TTS Engine\n(Deepgram / Cartesia / OpenAI)"]
        DatabaseARA[("Asterisk Realtime Architecture (ARA)\n(PostgreSQL / MySQL via ODBC)")]
        AudioStore[("S3 / NFS Audio Recordings Storage")]
    end

    BrowserRTC <-->|WSS & DTLS-SRTP| ChannelDrivers
    SIPTrunk <-->|UDP/TCP SIP & RTP| ChannelDrivers
    IPDeskPhone <-->|SIP & RTP| ChannelDrivers

    ChannelDrivers <--> RTPCore
    RTPCore <--> CodecMatrix
    CodecMatrix <--> JitterBuffer
    JitterBuffer <--> BridgingPlane
    
    RTPCore --> MixMonitor
    MixMonitor --> AudioStore

    RTPCore <--> AudioSocket
    AudioSocket <-->|TCP / Unix Socket PCM| SpeechAIService

    ChannelDrivers --> StasisBus
    StasisBus --> ChannelState
    ChannelState --> ARISystem
    ChannelState --> AMISystem

    DialplanEngine <--> ChannelDrivers
    ARISystem <-->|REST & WebSocket Events| MarketingStudioApp
    AMISystem <-->|TCP Action/Response| MarketingStudioApp
    FastAGI <--> MarketingStudioApp

    AsteriskCoreEngine <-->|Dynamic Config & CDR| DatabaseARA
```

### Architectural Narrative & Data Flow
1. **Signaling & Session Negotiation**: When an incoming call arrives or an outbound call is originated, `chan_pjsip` parses the SIP INVITE. For WebRTC clients, Asterisk negotiates ICE candidates, performs DTLS handshake for SRTP key exchange, and agrees on audio parameters (typically Opus 48kHz). For PSTN carriers, it negotiates standard G.711 µ-law/a-law or G.722.
2. **Media Transcoding & Jitter Buffering**: RTP packets pass into `res_rtp_asterisk`. If participants use mismatched codecs (e.g. WebRTC Opus to PSTN G.711), the audio frame passes through the `CodecMatrix`, converting audio frames into raw linear PCM (SLIN) and re-encoding them into the target codec with adaptive jitter buffering and packet loss concealment (PLC).
3. **Stasis Message Bus & Application Control**: Every state transition (channel creation, ringing, answer, DTMF reception, hangup) is emitted to the internal `StasisBus`. When a channel enters a Stasis application (e.g., `Stasis("marketingstudio_dialer")`), the dialplan yields complete control to ARI.
4. **External ARI Control & Real-time AI Voice**: The external application (MarketingStudio) receives a `StasisStart` event over an established WebSocket. MarketingStudio can place the channel into a media bridge (`POST /ari/bridges`), attach a snooping channel (`POST /ari/channels/{id}/snoop`), stream bidirectional audio to an AI engine via `res_audiosocket`, or play dynamic prompts.
5. **Call Detail Logging & Realtime Database**: All channel event transitions and call detail records are written out in real time through ODBC drivers (`cdr_adaptive_odbc`, `cel_odbc`) to PostgreSQL tables for billing, analytics, and CRM audit trails.

---

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

---

## 3. Technology Stack & Key Dependencies

| Component | Technology | Description & Version Baseline |
|---|---|---|
| **Core Language** | ANSI C (POSIX / C99) | High-performance multi-threaded POSIX execution engine |
| **SIP & Networking** | PJSIP Stack (`pjproject`) | RFC 3261 SIP protocol engine, SIP NAT traversal, transaction state |
| **Crypto & Encryption** | OpenSSL / libsrtp | TLS signaling encryption, DTLS-SRTP key exchange for WebRTC |
| **Audio Processing** | libopus, libspeex, libsndfile | Opus, G.711, Speex codecs, audio file format translation |
| **WebSockets & HTTP** | Asterisk HTTP Server (`res_http_websocket`) | Built-in HTTP/HTTPS server powering ARI REST and WebSocket streams |
| **Realtime Media Bridge**| `res_audiosocket` | Bidirectional 16-bit 8kHz/16kHz linear PCM streaming over TCP/Unix sockets |
| **Database & Configuration**| Asterisk Realtime Architecture (ARA) via unixODBC | Dynamic endpoint, trunk, and dialplan loading from PostgreSQL/MySQL |
| **Event Interfaces** | ARI (HTTP/WSS), AMI (TCP :5038), FastAGI | External programmatic orchestration interfaces |

---

## 4. Repository Layout & Key Modules

```
asterisk/
├── apps/                    # Dialplan applications
│   ├── app_dial.c           # Outbound dialing application
│   ├── app_queue.c          # Call center ACD queuing and agent distribution
│   ├── app_mixmonitor.c     # Real-time multi-channel call recording engine
│   ├── app_voicemail.c      # Voicemail boxes, MWI, and audio storage
│   └── app_confbridge.c     # High-capacity audio/video conference bridging
├── bridges/                 # Pluggable media bridging technologies
│   ├── bridge_simple.c      # Optimized two-channel direct audio bridge
│   ├── bridge_softmix.c     # Software audio mixer for N-way conferences
│   └── bridge_holding.c     # Holding bridge for music-on-hold and queue waiting
├── channels/                # Network channel drivers
│   ├── chan_pjsip.c         # PJSIP-based SIP and WebRTC channel driver
│   ├── chan_rtp.c           # Generic RTP channel implementation
│   └── chan_dahdi.c         # TDM / physical analog/ISDN telephony driver
├── configs/samples/         # Canonical configuration templates
│   ├── pjsip.conf.sample    # PJSIP endpoints, transports, auth, and AOR definitions
│   ├── extensions.conf.sample# PBX dialplan syntax and context routing rules
│   ├── ari.conf.sample      # ARI HTTP/WebSocket credentials and permissions
│   ├── manager.conf.sample  # AMI TCP connection settings and ACLs
│   └── res_odbc.conf.sample # ODBC database connection pool settings
├── main/                    # Asterisk daemon core
│   ├── asterisk.c           # Process entrypoint, signal handling, daemonization
│   ├── stasis.c             # Internal publish-subscribe event bus
│   ├── channel.c            # Channel lifecycle, frame allocation, audio routing
│   └── bridge.c             # Abstract bridging framework
├── res/                     # Core system resources and modules
│   ├── res_pjsip/           # Modular PJSIP components (registrar, outbound, NAT)
│   ├── res_ari/             # Asterisk REST Interface implementation
│   ├── res_audiosocket.c    # Bidirectional TCP streaming for external AI models
│   ├── res_rtp_asterisk.c   # Master RTP/RTCP engine with ICE and DTLS
│   └── res_config_odbc.c    # Asterisk Realtime Architecture (ARA) database driver
└── rest-api/api-docs/       # OpenAPI/Swagger JSON specifications for ARI endpoints
```

---

## 5. Core Features & Capabilities

### 5.1 Stasis Message Bus & Asterisk REST Interface (ARI)
The Asterisk REST Interface (ARI) shifts telephony development away from procedural dialplan scripting to modern, event-driven web development. By subscribing to a Stasis application over WebSockets (`/ari/events?app={app_name}`), external servers gain complete lifecycle control over channels, bridges, recordings, and audio playback. Channels are placed into the Stasis application by the dialplan, halting native execution while the external controller directs call flow.

### 5.2 AudioSocket & Real-Time Voice AI Integration
The `res_audiosocket` module provides an ultra-low-latency interface specifically suited for AI voice bots. Instead of piping audio through lossy intermediaries, AudioSocket establishes a raw TCP or Unix socket connection between Asterisk and an external AI voice runtime. The protocol transmits 16-bit signed linear PCM audio chunks with a simple 3-byte header (UUID, payload type, payload length), achieving round-trip latency under 20ms and enabling real-time voice activity detection (VAD), speech recognition, and instant conversational turn-taking.

### 5.3 WebRTC to PSTN Gateway with NAT Traversal
Asterisk acts as a robust media gateway between modern web browsers and carrier networks. In `chan_pjsip`, endpoints can be designated with `webrtc=yes`. Asterisk handles ICE candidate gathering, STUN/TURN negotiation, DTLS cryptographic handshakes, and RTCP feedback packets (NACK, PLI, FIR). It decodes WebRTC Opus audio and transcodes it to carrier PSTN formats on the fly with sub-millisecond overhead.

### 5.4 Automatic Call Distribution (ACD) & Queuing (`app_queue`)
`app_queue` provides carrier-grade inbound call distribution. It supports:
- Flexible queue strategies: `ringall`, `leastrecent`, `fewestcalls`, `random`, `rrmemory` (round-robin with memory), and `linear`.
- Dynamic and static queue member assignments (SIP extensions, external PSTN mobile numbers).
- Penalty routing (routing calls to tier-2 agents only when tier-1 agents are occupied).
- Custom announce frequencies, estimated wait times, and exit digit escapes.

### 5.5 High-Fidelity Dual-Channel Recording (`app_mixmonitor`)
`app_mixmonitor` taps directly into the channel's read and write audio streams. Unlike external recording proxies, it records both parties synchronously at the media engine level. It can output discrete stereo channels (left channel = caller, right channel = agent) in linear WAV format. This channel isolation is essential for automated speech recognition (ASR), preventing agent speech from corrupting the customer's audio transcript.

### 5.6 Asterisk Realtime Architecture (ARA)
Through `res_config_odbc`, Asterisk can bypass flat configuration files (`pjsip.conf`, `extensions.conf`) entirely. It dynamically reads and writes SIP endpoints, authentication credentials, registration contacts, and dialplans directly from relational databases like PostgreSQL. When a customer provisions a new phone number or SIP trunk in MarketingStudio, Asterisk recognizes the new endpoint instantaneously without requiring a reload or service restart.

---

## 6. Data Models & Schema Architecture

Under Asterisk Realtime Architecture (ARA) and call logging, Asterisk interacts with relational database tables. Below is the entity relationship diagram representing PJSIP realtime provisioning, Call Detail Records (CDR), and Channel Event Logging (CEL):

```mermaid
erDiagram
    PS_ENDPOINTS ||--o| PS_AUTH : authenticates_via
    PS_ENDPOINTS ||--o| PS_AORS : maps_to_contact
    PS_AORS ||--o{ PS_CONTACTS : registers
    PS_REGISTRATIONS ||--o| PS_AUTH : outbound_registration
    
    PS_ENDPOINTS ||--o{ CDR : generates
    CDR ||--o{ CEL : detailed_events

    PS_ENDPOINTS {
        varchar id PK
        varchar transport
        varchar aors
        varchar auth
        varchar context
        varchar disallow
        varchar allow
        varchar dtmf_mode
        varchar rtp_symmetric
        varchar force_rport
        varchar rewrite_contact
        varchar webrtc
    }

    PS_AUTH {
        varchar id PK
        varchar auth_type
        varchar password
        varchar realm
        varchar username
    }

    PS_AORS {
        varchar id PK
        varchar contact
        int default_expiration
        int max_contacts
        int remove_existing
        int qualify_frequency
    }

    PS_REGISTRATIONS {
        varchar id PK
        varchar transport
        varchar outbound_auth
        varchar client_uri
        varchar server_uri
        int expiration
    }

    CDR {
        bigint id PK
        varchar accountcode
        varchar src
        varchar dst
        varchar dcontext
        varchar clid
        varchar channel
        varchar dstchannel
        varchar lastapp
        timestamp start
        timestamp answer
        timestamp end
        int duration
        int billsec
        varchar disposition
        varchar uniqueid
    }

    CEL {
        bigint id PK
        varchar eventtype
        timestamp eventtime
        varchar cid_name
        varchar cid_num
        varchar exten
        varchar context
        varchar channame
        varchar appname
        varchar uniqueid
        varchar linkedid
        json extra
    }
```

### Typed Field Descriptions
- **`PS_ENDPOINTS.id`**: Unique SIP endpoint identifier (e.g. `agent_1001` or `carrier_telnyx`).
- **`PS_ENDPOINTS.webrtc`**: Boolean string (`yes`/`no`) instructing Asterisk to enable DTLS, ICE, and WSS signaling for this endpoint.
- **`CDR.billsec`**: The actual billable duration of the call in integer seconds (elapsed time between `answer` and `end`).
- **`CDR.disposition`**: Asterisk call outcome: `ANSWERED`, `NO ANSWER`, `BUSY`, or `FAILED`.
- **`CEL.uniqueid` / `CEL.linkedid`**: Globally unique channel identifiers. `linkedid` remains constant across call transfers, bridges, and conferences, allowing multi-leg call reconstruction.

---

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

---

## 7. API Architecture, Webhooks & Key Endpoints

Asterisk provides the Asterisk REST Interface (ARI) over HTTP/JSON and WebSockets, alongside the Asterisk Manager Interface (AMI) over raw TCP.

### Key ARI REST Endpoints

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/ari/channels` | Originates a new outbound channel to an endpoint |
| `POST` | `/ari/channels/{channelId}/snoop` | Snoops on an active call leg (whisper, listen, or AI bridge) |
| `POST` | `/ari/channels/externalMedia` | Creates an external RTP/AudioSocket channel to stream audio to an external server |
| `POST` | `/ari/bridges` | Creates a new multi-channel mixing or holding bridge |
| `POST` | `/ari/bridges/{bridgeId}/addChannel` | Adds one or more channels to an existing bridge |
| `POST` | `/ari/channels/{channelId}/record` | Starts recording audio on a live channel |
| `GET` | `/ari/events` | WebSocket connection for real-time bidirectional Stasis event streaming |

### Sample ARI Invocation: Originate Call to AI Bridge
```http
POST /ari/channels?endpoint=PJSIP/+14155550100@carrier_trunk&app=marketingstudio_dialer&callerId=MarketingStudio<+18005550199> HTTP/1.1
Host: pbx.marketingstudio.internal:8088
Authorization: Basic YXJpdXNlcjpwYXNzd29yZA==
Content-Type: application/json
```

### Sample ARI WebSocket Event: `StasisStart`
```json
{
  "type": "StasisStart",
  "timestamp": "2026-10-24T10:14:22.450Z",
  "application": "marketingstudio_dialer",
  "channel": {
    "id": "1786607298.14",
    "name": "PJSIP/carrier_trunk-0000000a",
    "state": "Up",
    "caller": {
      "name": "MarketingStudio",
      "number": "+18005550199"
    },
    "connected": {
      "name": "",
      "number": "+14155550100"
    },
    "dialplan": {
      "context": "outbound_campaigns",
      "exten": "s",
      "priority": 1
    }
  },
  "args": ["campaign_789", "lead_456"]
}
```

### Asterisk Manager Interface (AMI) Action: Originate Call
```text
Action: Originate
ActionID: ms_orig_1024
Channel: PJSIP/agent_101
Exten: 914155552671
Context: default
Priority: 1
CallerID: "MarketingStudio" <+18005550199>
Timeout: 30000
Async: true
```

---

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

---

## 8. Strategic Value & Takeaways for MarketingStudio

### Reusable Architectural Patterns
1. **AudioSocket / ExternalMedia for Direct Voice AI Pipelines**:
   - *Pattern*: Asterisk’s `res_audiosocket` and ARI `externalMedia` endpoints eliminate the need for third-party CPaaS middleware (e.g. Twilio Media Streams). Audio flows directly from the telephony engine to local or cloud speech processing nodes via raw 16-bit linear PCM over TCP.
   - *MarketingStudio Convergence*: Deploy an Asterisk media cluster running `res_audiosocket` to connect our self-hosted Voice AI agent directly to SIP carrier trunks, cutting telephony latency down to under 30ms.
2. **ARI Headless PBX Pattern**:
   - *Pattern*: Decoupling call control logic from legacy dialplan syntax (`extensions.conf`) allows full control over call state from our primary backend language (Node.js/TypeScript or Python) via REST and WebSockets.
   - *MarketingStudio Convergence*: Build a clean Asterisk ARI gateway service that exposes simple RPC primitives (`dial()`, `transfer()`, `whisper()`, `record()`) to the MarketingStudio power dialer engine.
3. **Asterisk Realtime Architecture (ARA) for Dynamic Multi-Tenant SIP Trunks**:
   - *Pattern*: Storing PJSIP endpoints, trunks, and authentication in PostgreSQL tables means new customer phone numbers, BYOC (Bring Your Own Carrier) SIP credentials, and agent extensions are activated instantaneously without restarting Asterisk.
   - *MarketingStudio Convergence*: Use ARA tables connected to MarketingStudio's database so user phone numbers and carrier settings can be provisioned in real time.
4. **Whisper Coaching & Silent Monitoring via Channel Snooping**:
   - *Pattern*: Asterisk's `/ari/channels/{channelId}/snoop` capability enables managers and AI supervisors to silently listen to live calls or whisper advice exclusively to the sales representative without the prospect hearing.
   - *MarketingStudio Convergence*: Implement a live sales coaching dashboard in MarketingStudio where AI sales managers whisper real-time battle-card tips directly into the softphone audio stream.
