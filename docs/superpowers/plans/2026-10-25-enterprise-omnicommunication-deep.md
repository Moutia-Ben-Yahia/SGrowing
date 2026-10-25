# Implementation Plan: Enterprise Omnicommunication Pillars (Phases A–E)
**Date:** 2026-10-25  
**Spec Reference:** `docs/superpowers/specs/2026-10-25-enterprise-omnicommunication-deep-design.md`

---

## Phases and Tasks

- **Phase A: Autonomous Multi-Turn AI Support Agent with Tool Calling**
  - Task A.1: Tool interfaces & implementations (`IAgentTool`, `KnowledgeBaseTool`, `OrderLookupTool`, `BookingTool`, `EscalateToHumanTool`)
  - Task A.2: `AutonomousAiAgentService` multi-turn reasoning loop
  - Task A.3: `AutonomousAiAgentTests` xUnit verification
- **Phase B: Cross-Channel 360 Identity & Composer Switching**
  - Task B.1: `IdentityResolverService` (multi-identifier contact matching & cross-channel timeline aggregation)
  - Task B.2: Channel override support in message sending slice
  - Task B.3: `IdentityResolverTests` xUnit verification
- **Phase C: Meta WhatsApp Template Manager & Audience Broadcast Engine**
  - Task C.1: Entities (`WhatsAppTemplate`, `BroadcastCampaign`) in `SGrowing.Core` & DbSets
  - Task C.2: `BroadcastDispatcherService` with throttling & Outbox integration
  - Task C.3: Broadcast Minimal API endpoints & `WhatsAppBroadcastTests`
- **Phase D: In-Chat Calendar Booking (`Cal.com`) & Assisted Checkout Links**
  - Task D.1: Entities (`AppointmentSlot`, `AppointmentBooking`, `CheckoutLink`) in `SGrowing.Core` & DbSets
  - Task D.2: `AppointmentBookingService` and `CheckoutLinkService`
  - Task D.3: Booking & Checkout Minimal API endpoints & `BookingAndCheckoutTests`
- **Phase E: SLA Escalation Engine, Business Hours & Real-Time Collision Prevention**
  - Task E.1: `SlaPolicy` entity & `SlaMonitoringService` breach evaluator
  - Task E.2: SignalR `OmniHub` typing presence & conversation lock methods
  - Task E.3: `SlaAndCollisionTests` xUnit verification
- **Phase F: Angular 19 UI Evolution & Studio Expansion**
  - Task F.1: Update `inbox-shell.component.ts` (live typing presence, conversation lock banner, channel selector in composer, action buttons)
  - Task F.2: `broadcast-studio.component.ts` (WhatsApp Templates editor & audience campaign dispatcher)
  - Task F.3: `sla-studio.component.ts` (SLA policies & breach scoreboard)
  - Task F.4: Routes & Angular compiler verification (`npm run build`)
- **Phase G: Master Enterprise Omnicommunication E2E Test & Final Audit**
  - Task G.1: Master E2E test covering autonomous bot deflection, channel switching, booking, checkout link, SLA monitor, and broadcast dispatch.
  - Task G.2: Full test suite verification (`dotnet test SGrowing.slnx`)
