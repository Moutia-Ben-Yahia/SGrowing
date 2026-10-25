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

# 1. openfunnels
openfunnels_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class FunnelManager {
        +createFunnel(name) Funnel
        +addStep(funnelId, step) FunnelStep
        +trackConversion(stepId, visitorId) Void
    }

    class PageRenderer {
        +renderStepPage(stepId) HtmlString
    }

    class SplitTestEngine {
        +selectVariant(stepId) PageVariant
    }

    FunnelManager --> PageRenderer : serves
    FunnelManager --> SplitTestEngine : optimizes
```
"""

openfunnels_db = """
### 6.1 Comprehensive Database Schema Specification

#### Table: `funnels`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Funnel ID |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Funnel title |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'active'` | State |
"""

openfunnels_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor Visitor as Visitor
    participant App as OpenFunnels
    participant Split as SplitTestEngine
    participant DB as Database

    Visitor->>App: Visit /optin-page
    App->>Split: selectVariant(stepId)
    Split-->>App: Variant B
    App-->>Visitor: Render Variant B
    Visitor->>App: Submits Opt-in Form
    App->>DB: Log conversion & redirect to Step 2
```
"""

# 2. spree
spree_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class SpreeOrder {
        +BigInt id
        +String number
        +Decimal total
        +String state
        +List~SpreeLineItem~ lineItems
        +next() Boolean
    }

    class SpreeLineItem {
        +BigInt id
        +BigInt orderId
        +BigInt variantId
        +Int quantity
        +Decimal price
    }

    class SpreeVariant {
        +BigInt id
        +BigInt productId
        +String sku
        +Decimal price
    }

    SpreeOrder "1" *-- "many" SpreeLineItem : contains
    SpreeLineItem "many" --> "1" SpreeVariant : references
```
"""

spree_db = """
### 6.1 Comprehensive Database Schema Specification

Spree Commerce uses PostgreSQL / MySQL with active state machines for e-commerce checkouts.

#### Table: `spree_orders`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `BIGINT` | `PRIMARY KEY AUTO_INCREMENT` | - | Order ID |
| `number` | `VARCHAR(32)` | `NOT NULL, UNIQUE` | - | Order reference number (e.g. `R123456789`) |
| `item_total` | `DECIMAL(10, 2)`| `NOT NULL` | `0.00` | Subtotal amount |
| `total` | `DECIMAL(10, 2)`| `NOT NULL` | `0.00` | Grand total |
| `state` | `VARCHAR(255)` | `NOT NULL` | `'cart'` | Checkout state (`cart`, `address`, `delivery`, `payment`, `complete`) |

*Indexes:* `idx_spree_orders_number` (`UNIQUE (number)`), `idx_spree_orders_state` (`(state)`).
"""

spree_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor Shopper as Buyer
    participant Cart as Spree Cart API
    participant Checkout as Spree Order StateMachine
    participant Gateway as Stripe Payment Gateway
    participant DB as PostgreSQL Database

    Shopper->>Cart: POST /api/v2/storefront/cart/add_item (variant_id, qty)
    Cart->>DB: INSERT into spree_line_items
    Shopper->>Checkout: Advance order state (cart -> address -> payment)
    Shopper->>Checkout: Submit Stripe Token
    Checkout->>Gateway: Charge credit card ($149.00)
    Gateway-->>Checkout: Charge succeeded
    Checkout->>DB: UPDATE spree_orders SET state='complete', completed_at=NOW()
    Checkout-->>Shopper: Order Confirmation R123456789
```
"""

# 3. cal.diy
caldiy_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class BookingManager {
        +createBooking(eventTypeId, timeslot, guest) Booking
        +cancelBooking(bookingId) Void
    }

    class AvailabilityCalculator {
        +getFreeBusy(userId, dateRange) List~TimeSlot~
    }

    BookingManager --> AvailabilityCalculator : checks_slots
```
"""

caldiy_db = """
### 6.1 Comprehensive Database Schema Specification

#### Table: `bookings`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Booking ID |
| `user_id` | `UUID` | `NOT NULL` | - | Host user |
| `start_time` | `TIMESTAMPTZ` | `NOT NULL` | - | Start time |
| `end_time` | `TIMESTAMPTZ` | `NOT NULL` | - | End time |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'accepted'` | Booking status |
"""

caldiy_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor Guest as Meeting Guest
    participant Cal as Cal.diy Web
    participant Calc as AvailabilityCalculator
    participant Google as Google Calendar

    Guest->>Cal: View /host/30min booking page
    Cal->>Calc: getFreeBusy(host_id)
    Calc->>Google: Fetch Google Calendar busy slots
    Calc-->>Cal: Return open 30-minute intervals
    Guest->>Cal: Select 2:00 PM slot & submits details
    Cal->>Google: Insert Google Meet calendar event
    Cal-->>Guest: Booking confirmed with video call link
```
"""

# 4. calnode
calnode_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class CalNodeService {
        +reserveSlot(hostId, time) Slot
    }
```
"""

calnode_db = """
### 6.1 Comprehensive Database Schema Specification

#### Table: `slots`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Slot ID |
| `time` | `TIMESTAMPTZ` | `NOT NULL` | - | Reserved time |
"""

calnode_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    participant Client as Client
    participant Service as CalNodeService
    participant DB as Storage

    Client->>Service: reserveSlot(time)
    Service->>DB: Lock slot
    Service-->>Client: Reserved
```
"""

# 5. autonnel
autonnel_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class AutonnelEngine {
        +triggerFunnel(funnelId, leadId) Void
    }
```
"""

autonnel_db = """
### 6.1 Comprehensive Database Schema Specification

#### Table: `autonnel_campaigns`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Funnel ID |
| `title` | `VARCHAR(255)` | `NOT NULL` | - | Funnel title |
"""

autonnel_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    participant Trigger as Lead Trigger
    participant Engine as AutonnelEngine
    participant Step as Step Handler

    Trigger->>Engine: Lead enrolled
    Engine->>Step: Execute step 1 action
```
"""

# 6. Ghost
ghost_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class PostModel {
        +String id
        +String title
        +String slug
        +Text html
        +String status
        +publish() Void
    }

    class MemberModel {
        +String id
        +String email
        +String name
        +String status
    }

    class EmailService {
        +sendNewsletter(postId, memberList) Void
    }

    PostModel --> EmailService : triggers_newsletter
```
"""

ghost_db = """
### 6.1 Comprehensive Database Schema Specification

Ghost stores editorial publications and paid newsletter subscribers in MySQL / SQLite.

#### Table: `posts`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `VARCHAR(24)` | `PRIMARY KEY` | - | Post ID |
| `title` | `VARCHAR(2000)` | `NOT NULL` | - | Article title |
| `slug` | `VARCHAR(191)` | `NOT NULL, UNIQUE` | - | URL slug |
| `html` | `LONGTEXT` | `NULLABLE` | `NULL` | Rendered post HTML |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'draft'` | State (`draft`, `published`, `scheduled`) |

*Indexes:* `idx_posts_slug` (`UNIQUE (slug)`), `idx_posts_status` (`(status)`).

---

#### Table: `members`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `VARCHAR(24)` | `PRIMARY KEY` | - | Member ID |
| `email` | `VARCHAR(191)` | `NOT NULL, UNIQUE` | - | Subscriber email |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'free'` | Subscription tier (`free`, `paid`, `comped`) |

*Indexes:* `idx_members_email` (`UNIQUE (email)`).
"""

ghost_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor Author as Publisher
    participant Admin as Ghost Admin API
    participant DB as MySQL Database
    participant Mailer as Ghost Email Service (Mailgun)
    actor Member as Subscriber

    Author->>Admin: Click "Publish and Email Newsletter"
    Admin->>DB: UPDATE posts SET status='published', published_at=NOW()
    Admin->>Mailer: Dispatch newsletter batch to active members
    Mailer->>Member: Deliver responsive HTML email
    Mailer-->>Admin: Delivery metrics logged
```
"""

# 7. countly-server
countly_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class EventIngestor {
        +ingestBatch(appKey, events) Void
    }

    class SessionTracker {
        +startSession(appKey, deviceId) Void
        +extendSession(appKey, deviceId, duration) Void
    }

    class DrilldownEngine {
        +queryMetrics(appKey, metricName, dateRange) JSON
    }

    EventIngestor --> DrilldownEngine : feeds_data
```
"""

countly_db = """
### 6.1 Comprehensive Database Schema Specification

Countly stores analytics events and session documents in MongoDB collections.

#### Collection: `events`
| Field | Type | Description |
| :--- | :--- | :--- |
| `_id` | `ObjectId` | Document identifier |
| `app_id` | `ObjectId` | Application key |
| `key` | `String` | Event name (e.g. `purchase`, `screen_view`) |
| `count` | `Integer` | Occurrence count |
| `segmentation` | `Object` | Custom JSON properties map |
| `timestamp` | `Date` | Client event timestamp |
"""

countly_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor MobileApp as iOS / Android App
    participant Ingest as Countly API Server
    participant Mongo as MongoDB
    actor Dashboard as Marketing Analyst

    MobileApp->>Ingest: POST /i?app_key=...&events=[{key: 'signup', count: 1}]
    Ingest->>Mongo: Bulk update & aggregate into time-bucketed documents
    Ingest-->>MobileApp: 200 OK ({result: 'Success'})
    Dashboard->>Ingest: GET /o/analytics/drilldown
    Ingest->>Mongo: Query aggregated event collection
    Ingest-->>Dashboard: Return real-time visual charts
```
"""

# Execute insertions
insert_sections('docs/wiki/openfunnels.md', openfunnels_cd, openfunnels_db, openfunnels_seq)
insert_sections('docs/wiki/spree.md', spree_cd, spree_db, spree_seq)
insert_sections('docs/wiki/cal.diy.md', caldiy_cd, caldiy_db, caldiy_seq)
insert_sections('docs/wiki/calnode.md', calnode_cd, calnode_db, calnode_seq)
insert_sections('docs/wiki/autonnel.md', autonnel_cd, autonnel_db, autonnel_seq)
insert_sections('docs/wiki/Ghost.md', ghost_cd, ghost_db, ghost_seq)
insert_sections('docs/wiki/countly-server.md', countly_cd, countly_db, countly_seq)
