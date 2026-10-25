# Architecture Design Spec: Enterprise Omnicommunication Pillars (Phases A–E)
**Date:** 2026-10-25  
**Project:** SGrowing (.NET 10 + Angular 19 + SignalR)

---

## 1. System Overview

This specification expands **SGrowing** from core multichannel messaging into a complete enterprise omnicommunication suite spanning Marketing, Sales, Customer Support, and Autonomous Replies:

```
+-------------------------------------------------------------------------------------------------------------+
|                                        SGROWING ENTERPRISE PLATFORM                                         |
+-------------------------------------------------------------------------------------------------------------+
|  Pillar A: Autonomous AI Support Agent      |  Pillar B: Cross-Channel 360 Identity & Composer Switching    |
|  - Multi-turn autonomous replies (24/7)    |  - Unified Contact Profile across Phone, Email, & WA IDs    |
|  - Tool calling: Order, Booking, KB, Escalate|  - Switch reply channel in composer (WA <-> Email <-> Web)   |
+---------------------------------------------+---------------------------------------------------------------+
|  Pillar C: Meta WhatsApp Templates & Broadcasts | Pillar D: In-Chat Booking (Cal.com) & Assisted Checkout   |
|  - Meta-approved templates (Marketing/Utility)|  - Interactive meeting scheduling links in WhatsApp/Email   |
|  - Throttled audience broadcast dispatcher  |  - In-chat instant payment links & automatic deal closing    |
+---------------------------------------------+---------------------------------------------------------------+
|  Pillar E: SLA Monitoring, Business Hours & Collision Prevention                                            |
|  - First Response Time (FRT) & Resolution tracking with breach escalation                                   |
|  - Real-time SignalR typing presence & conversation lock to eliminate duplicate replies                      |
+-------------------------------------------------------------------------------------------------------------+
```

---

## 2. Pillar A: Autonomous Multi-Turn AI Support Agent (`AutonomousAiAgentService`)

### Domain Logic
- When `Conversation.AiMode == AiMode.Autonomous`:
  - An incoming customer message directly invokes `AutonomousAiAgentService.HandleIncomingMessageAsync`.
  - The agent inspects conversation context and evaluates available **Tools**:
    1. `KnowledgeBaseTool`: Searches documentation for instant answers.
    2. `OrderLookupTool`: Checks customer purchase history and delivery tracking.
    3. `BookingTool`: Checks available meeting slots for sales/support demos.
    4. `EscalateToHumanTool`: Transitions `AiMode` to `Copilot`, sets priority to `High`, and notifies human agents via SignalR.
  - Automatically synthesizes response, saves message as `MessageSenderType.Bot`, and broadcasts via `OmniHub`.

---

## 3. Pillar B: Cross-Channel 360 Identity & Channel-Switching Composer

### Domain Logic
- A single `Contact` can engage across multiple inboxes (e.g. initial touchpoint on Web Chat, followed up by WhatsApp, confirmed via Email).
- `IdentityResolverService`:
  - Matches inbound identifier (Phone, Email, WA ID) against existing contacts.
  - Pulls complete timeline across all channels into a unified view.
- In reply composer: Agents can specify `TargetChannelOverride` to send a reply via Email or WhatsApp regardless of how the conversation originated.

---

## 4. Pillar C: WhatsApp Template Management & Broadcasts

### Domain Entities
- `WhatsAppTemplate`:
  - `Id`, `TenantId`, `Name`, `Category` (`MARKETING`, `UTILITY`, `AUTHENTICATION`), `Language`, `Status` (`APPROVED`, `PENDING`), `BodyText`, `ButtonsJson`.
- `BroadcastCampaign`:
  - `Id`, `TenantId`, `Name`, `Channel`, `TemplateId`, `TargetTag`, `TotalRecipients`, `SentCount`, `Status` (`Draft`, `Scheduled`, `Running`, `Completed`).
- `BroadcastDispatcherService`:
  - Evaluates audience, enforces 24h Meta compliance rules, throttles dispatches, and writes to durable `Outbox`.

---

## 5. Pillar D: In-Chat Appointment Booking & Assisted Checkout

### Domain Entities
- `AppointmentSlot`: `Id`, `TenantId`, `Title`, `DurationMinutes`, `HostAgentName`, `IsActive`.
- `AppointmentBooking`: `Id`, `TenantId`, `SlotId`, `ContactId`, `ScheduledTimeUtc`, `Status`.
- `CheckoutLink`: `Id`, `TenantId`, `ContactId`, `ConversationId`, `Title`, `Amount`, `Currency`, `Status`, `PaymentUrl`.
- Integrated directly into the message stream with clickable action buttons.

---

## 6. Pillar E: SLA Escalation & Real-Time Collision Prevention

### Domain Logic
- `SlaPolicy`: `Id`, `TenantId`, `Name`, `Priority`, `TargetFirstResponseMinutes`, `TargetResolutionMinutes`.
- `SlaMonitoringService`: Continuously monitors open conversations. If `LastMessageAtUtc` exceeds policy without an agent response, marks `IsSlaBreached = true` and raises an urgent alert.
- Real-Time SignalR Collision Prevention:
  - `OnUserTyping(conversationId, agentName)`
  - `OnConversationLocked(conversationId, agentName)`
  - `OnConversationUnlocked(conversationId)`

---

## 7. Verification & Automated Testing Plan
- Unit tests for every new service (`AutonomousAiAgentTests`, `IdentityResolverTests`, `WhatsAppTemplateTests`, `BookingAndCheckoutTests`, `SlaMonitoringTests`).
- Master Integration E2E test exercising the combined flow.
- Angular 19 compilation with updated UI studios.
