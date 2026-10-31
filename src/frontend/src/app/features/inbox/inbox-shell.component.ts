import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { SignalRService } from '../../core/services/signalr.service';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { AiDraftDto, ConversationDto, MessageDto, ContactDto } from '../../core/models/chat.models';
import { TranslationService } from '../../core/services/translation.service';
import { LanguageSwitcherComponent } from '../../core/components/language-switcher.component';

@Component({
  selector: 'app-inbox-shell',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LanguageSwitcherComponent],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <!-- 1. Left Nav & Channels Bar -->
      <aside class="w-64 border-r border-slate-800 bg-slate-900/80 flex flex-col justify-between p-4">
        <div>
          <div class="flex items-center gap-2 mb-6">
            <div class="h-8 w-8 rounded-lg bg-gradient-to-tr from-indigo-500 to-cyan-400 flex items-center justify-center font-bold text-white shadow-lg shadow-indigo-500/20">
              SG
            </div>
            <div>
              <h1 class="text-sm font-semibold tracking-wide text-white">SGrowing</h1>
              <p class="text-xs text-slate-400">Omnicommunication</p>
            </div>
          </div>

          <div class="space-y-1 mb-6">
            <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider px-2">Navigation</span>
            <div class="space-y-1 mb-4">
              <a
                routerLink="/projects/tasks"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-indigo-400 hover:bg-indigo-500/10 border border-indigo-500/20 transition">
                <span class="flex items-center gap-2">📋 Sprint Tasks & Onboarding</span>
              </a>
              <a
                routerLink="/integrations/webhook-mesh"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-rose-400 hover:bg-rose-500/10 border border-rose-500/20 transition">
                <span class="flex items-center gap-2">⚡ Webhook Mesh & Breakers</span>
              </a>
              <a
                routerLink="/sales/predictive-cadence"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-emerald-400 hover:bg-emerald-500/10 border border-emerald-500/20 transition">
                <span class="flex items-center gap-2">🎯 Predictive Sales Cadence</span>
              </a>
              <a
                routerLink="/commerce/inventory-replenishment"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-amber-400 hover:bg-amber-500/10 border border-amber-500/20 transition">
                <span class="flex items-center gap-2">📦 Inventory Replenishment</span>
              </a>
              <a
                routerLink="/tenancy/whitelabel-branding"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-cyan-400 hover:bg-cyan-500/10 border border-cyan-500/20 transition">
                <span class="flex items-center gap-2">🎨 Whitelabel Branding & SSL</span>
              </a>
              <a
                routerLink="/finance/cashflow-forecasting"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-emerald-400 hover:bg-emerald-500/10 border border-emerald-500/20 transition">
                <span class="flex items-center gap-2">💰 Cash Flow & Working Capital</span>
              </a>
              <a
                routerLink="/security/rate-limiting"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-fuchsia-400 hover:bg-fuchsia-500/10 border border-fuchsia-500/20 transition">
                <span class="flex items-center gap-2">🛡️ Rate Limiting & Token Bucket</span>
              </a>
              <a
                routerLink="/crm/churn-retention"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-pink-400 hover:bg-pink-500/10 border border-pink-500/20 transition">
                <span class="flex items-center gap-2">🔄 Churn Prediction & Retention</span>
              </a>
              <a
                routerLink="/security/compliance-audit-ledger"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-sky-400 hover:bg-sky-500/10 border border-sky-500/20 transition">
                <span class="flex items-center gap-2">⛓️ Merkle Audit Ledger & SOC 2</span>
              </a>
              <a
                routerLink="/security/idempotency-ledger"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-violet-400 hover:bg-violet-500/10 border border-violet-500/20 transition">
                <span class="flex items-center gap-2">⚡ Idempotency & Safe Replay</span>
              </a>
              <a
                routerLink="/feedback/board"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-amber-400 hover:bg-amber-500/10 border border-amber-500/20 transition">
                <span class="flex items-center gap-2">💡 Feedback & Feature Roadmap</span>
              </a>
              <a
                routerLink="/billing/tax-invoicing"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-teal-400 hover:bg-teal-500/10 border border-teal-500/20 transition">
                <span class="flex items-center gap-2">📑 Multi-Currency & ZATCA Tax</span>
              </a>
              <a
                routerLink="/knowledge/rag-autopilot"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-violet-400 hover:bg-violet-500/10 border border-violet-500/20 transition">
                <span class="flex items-center gap-2">🤖 Hybrid RAG & Ticket Autopilot</span>
              </a>
              <a
                routerLink="/documents/telemetry"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-rose-400 hover:bg-rose-500/10 border border-rose-500/20 transition">
                <span class="flex items-center gap-2">📊 Document Dwell & Buying Radar</span>
              </a>
              <a
                routerLink="/commerce/dynamic-pricing"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-indigo-400 hover:bg-indigo-500/10 border border-indigo-500/20 transition">
                <span class="flex items-center gap-2">🏷️ Dynamic Pricing & Bandit</span>
              </a>
              <a
                routerLink="/crm/lead-enrichment"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-emerald-400 hover:bg-emerald-500/10 border border-emerald-500/20 transition">
                <span class="flex items-center gap-2">🎯 Lead Radar & DNS Verification</span>
              </a>
              <a
                routerLink="/sla/monitor"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-amber-400 hover:bg-amber-500/10 border border-amber-500/20 transition">
                <span class="flex items-center gap-2">⏱️ Predictive SLA Breach Monitor</span>
              </a>
              <a
                routerLink="/forms/headless"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-amber-400 hover:bg-amber-500/10 border border-amber-500/20 transition">
                <span class="flex items-center gap-2">📝 Headless Forms & Turnstile</span>
              </a>
              <a
                routerLink="/channels/whatsapp-companion"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-emerald-400 hover:bg-emerald-500/10 border border-emerald-500/20 transition">
                <span class="flex items-center gap-2">📱 WhatsApp Companion & Pairing</span>
              </a>
              <a
                routerLink="/commerce/affiliates"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-indigo-400 hover:bg-indigo-500/10 border border-indigo-500/20 transition">
                <span class="flex items-center gap-2">🤝 B2B Affiliate & Commission Ledger</span>
              </a>
              <a
                routerLink="/conversations/agent-presence"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-rose-400 hover:bg-rose-500/10 border border-rose-500/20 transition">
                <span class="flex items-center gap-2">🛡️ Agent Presence & Collision Shield</span>
              </a>
              <a
                routerLink="/funnels/split-testing"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-fuchsia-400 hover:bg-fuchsia-500/10 border border-fuchsia-500/20 transition">
                <span class="flex items-center gap-2">🧪 Dirichlet Funnel Split-Testing</span>
              </a>
              <a
                routerLink="/voice/streaming-cockpit"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-cyan-400 hover:bg-cyan-500/10 border border-cyan-500/20 transition">
                <span class="flex items-center gap-2">⚡ Full-Duplex Voice AI Streaming</span>
              </a>
              <a
                routerLink="/voice/transcription"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-blue-400 hover:bg-blue-500/10 border border-blue-500/20 transition">
                <span class="flex items-center gap-2">🎙️ Voice Diarization & Sentiment</span>
              </a>
              <a
                routerLink="/courier/fleet-dispatch"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-cyan-400 hover:bg-cyan-500/10 border border-cyan-500/20 transition">
                <span class="flex items-center gap-2">🚚 Geodetic Fleet Dispatch & TSP</span>
              </a>
              <a
                routerLink="/security/continuous-compliance"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-emerald-400 hover:bg-emerald-500/10 border border-emerald-500/20 transition">
                <span class="flex items-center gap-2">🛡️ SOC 2 Continuous Compliance</span>
              </a>
              <a
                routerLink="/deliverability/custom-domains"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-teal-400 hover:bg-teal-500/10 border border-teal-500/20 transition">
                <span class="flex items-center gap-2">🌐 Custom Sending Domains & DKIM</span>
              </a>
              <a
                routerLink="/contracts/e-sign"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-violet-400 hover:bg-violet-500/10 border border-violet-500/20 transition">
                <span class="flex items-center gap-2">✍️ Multi-Signer Contract E-Sign</span>
              </a>
              <a
                routerLink="/channels/whatsapp-pacing"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-emerald-400 hover:bg-emerald-500/10 border border-emerald-500/20 transition">
                <span class="flex items-center gap-2">💬 WhatsApp Broadcast Pacing & Anti-Ban</span>
              </a>
              <a
                routerLink="/integrations/event-mesh"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-cyan-400 hover:bg-cyan-500/10 border border-cyan-500/20 transition">
                <span class="flex items-center gap-2">⚡ Distributed Event Mesh</span>
              </a>
              <a
                routerLink="/channels/whatsapp-catalog"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-emerald-400 hover:bg-emerald-500/10 border border-emerald-500/20 transition">
                <span class="flex items-center gap-2">🛒 WhatsApp Catalog & Cart</span>
              </a>
              <a
                routerLink="/channels/telegram-bot"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-sky-400 hover:bg-sky-500/10 border border-sky-500/20 transition">
                <span class="flex items-center gap-2">✈️ Telegram Marketing Bot & Mini-App</span>
              </a>
              <a
                routerLink="/attribution/capi-cockpit"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-amber-400 hover:bg-amber-500/10 border border-amber-500/20 transition">
                <span class="flex items-center gap-2">📡 Server-Side Ad Attribution CAPI</span>
              </a>
              <a
                routerLink="/scheduling/appointments"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-indigo-400 hover:bg-indigo-500/10 border border-indigo-500/20 transition">
                <span class="flex items-center gap-2">📅 Appointment Scheduling</span>
              </a>
              <a
                routerLink="/social/calendar"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-rose-400 hover:bg-rose-500/10 border border-rose-500/20 transition">
                <span class="flex items-center gap-2">📱 Social Media Content Calendar</span>
              </a>
              <a
                routerLink="/outbound/research"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-cyan-400 hover:bg-cyan-500/10 border border-cyan-500/20 transition">
                <span class="flex items-center gap-2">🌐 Deep AI Web Research</span>
              </a>
              <a
                routerLink="/commerce/callcenter"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-amber-400 hover:bg-amber-500/10 border border-amber-500/20 transition">
                <span class="flex items-center gap-2">📞 Call Center & COD Ledger</span>
              </a>
              <a
                routerLink="/commerce/warehouses"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-emerald-400 hover:bg-emerald-500/10 border border-emerald-500/20 transition">
                <span class="flex items-center gap-2">🏬 Multi-Warehouse Allocation</span>
              </a>
              <a
                routerLink="/organizations/margin"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-amber-400 hover:bg-amber-500/10 border border-amber-500/20 transition">
                <span class="flex items-center gap-2">🏢 Organization & Margin Tracker</span>
              </a>
              <a
                routerLink="/accounts/snapshots"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-sky-400 hover:bg-sky-500/10 border border-sky-500/20 transition">
                <span class="flex items-center gap-2">🏢 Agency Snapshots & Rebilling</span>
              </a>
              <a
                routerLink="/workforce/timesheets"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-emerald-400 hover:bg-emerald-500/10 border border-emerald-500/20 transition">
                <span class="flex items-center gap-2">⏱️ Timesheets & Labor Margin</span>
              </a>
              <a
                routerLink="/channels/whatsapp-gateway"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-cyan-400 hover:bg-cyan-500/10 border border-cyan-500/20 transition">
                <span class="flex items-center gap-2">💬 Native WhatsApp Gateway</span>
              </a>
              <a
                routerLink="/funnels/autonomous"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-violet-400 hover:bg-violet-500/10 border border-violet-500/20 transition">
                <span class="flex items-center gap-2">🚀 Autonomous Funnel Workshop</span>
              </a>
              <a
                routerLink="/ai/agent-fleet"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-indigo-400 hover:bg-indigo-500/10 border border-indigo-500/20 transition">
                <span class="flex items-center gap-2">🤖 Dual-Layer 38 Agent Fleet</span>
              </a>
              <a
                routerLink="/billing/invoices"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-teal-400 hover:bg-teal-500/10 border border-teal-500/20 transition">
                <span class="flex items-center gap-2">💳 Multi-Currency Invoices & VAT</span>
              </a>
              <a
                routerLink="/courier"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-amber-400 hover:bg-amber-500/10 border border-amber-500/20 transition">
                <span class="flex items-center gap-2">🛵 Courier Driver Companion</span>
              </a>
              <a
                routerLink="/checkout"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-emerald-400 hover:bg-emerald-500/10 border border-emerald-500/20 transition">
                <span class="flex items-center gap-2">🛒 1-Page COD Checkout</span>
              </a>
              <a
                routerLink="/creatives"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-purple-400 hover:bg-purple-500/10 border border-purple-500/20 transition">
                <span class="flex items-center gap-2">🎬 AI UGC & Competitor Radar</span>
              </a>
              <a
                routerLink="/digital-goods"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-purple-400 hover:bg-purple-500/10 border border-purple-500/20 transition">
                <span class="flex items-center gap-2">📦 Digital Goods & PIM</span>
              </a>
              <a
                routerLink="/forms"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-teal-400 hover:bg-teal-500/10 border border-teal-500/20 transition">
                <span class="flex items-center gap-2">📋 Dynamic Forms & PDF</span>
              </a>
              <a
                routerLink="/developer"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-cyan-400 hover:bg-cyan-500/10 border border-cyan-500/20 transition">
                <span class="flex items-center gap-2">🔌 Developer API & Webhooks</span>
              </a>
              <a
                routerLink="/calendar"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-indigo-400 hover:bg-indigo-500/10 border border-indigo-500/20 transition">
                <span class="flex items-center gap-2">📅 Appointment Calendar</span>
              </a>
              <a
                routerLink="/deliverability"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-emerald-400 hover:bg-emerald-500/10 border border-emerald-500/20 transition">
                <span class="flex items-center gap-2">🛡️ Email Deliverability</span>
              </a>
              <a
                routerLink="/telemetry"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-cyan-400 hover:bg-cyan-500/10 border border-cyan-500/20 transition">
                <span class="flex items-center gap-2">📡 Live Product Telemetry</span>
              </a>
              <a
                routerLink="/telemetry/analytics"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-emerald-400 hover:bg-emerald-500/10 border border-emerald-500/20 transition">
                <span class="flex items-center gap-2">📈 Cohort Retention & Analytics</span>
              </a>
              <a
                routerLink="/billing"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-amber-400 hover:bg-amber-500/10 border border-amber-500/20 transition">
                <span class="flex items-center gap-2">💳 SaaS Billing & Plans</span>
              </a>
              <a
                routerLink="/operations"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-emerald-400 hover:bg-emerald-500/10 border border-emerald-500/20 transition">
                <span class="flex items-center gap-2">⚡ COD Ops & Logistics Hub</span>
              </a>
              <a
                routerLink="/analytics"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-sky-400 hover:bg-sky-500/10 border border-sky-500/20 transition">
                <span class="flex items-center gap-2">📊 Analytics & Multi-Touch BI</span>
              </a>
              <a
                routerLink="/commerce"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-amber-400 hover:bg-amber-500/10 border border-amber-500/20 transition">
                <span class="flex items-center gap-2">🛒 Commerce & CPQ</span>
              </a>
              <a
                routerLink="/commerce/connectors"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-cyan-400 hover:bg-cyan-500/10 border border-cyan-500/20 transition">
                <span class="flex items-center gap-2">🔌 {{ translation.t('nav.connectors') }}</span>
              </a>
              <a
                routerLink="/funnels"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-orange-400 hover:bg-orange-500/10 border border-orange-500/20 transition">
                <span class="flex items-center gap-2">⚡ AI Funnel & Upsell Studio</span>
              </a>
              <a
                routerLink="/seo"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-lime-400 hover:bg-lime-500/10 border border-lime-500/20 transition">
                <span class="flex items-center gap-2">🔍 Technical SEO & SERP Hub</span>
              </a>
              <a
                routerLink="/outbound"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-amber-400 hover:bg-amber-500/10 border border-amber-500/20 transition">
                <span class="flex items-center gap-2">🚀 AI SDR Outbound</span>
              </a>
              <a
                routerLink="/social"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-fuchsia-400 hover:bg-fuchsia-500/10 border border-fuchsia-500/20 transition">
                <span class="flex items-center gap-2">📱 Social Media Studio</span>
              </a>
              <a
                routerLink="/voice"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-emerald-400 hover:bg-emerald-500/10 border border-emerald-500/20 transition">
                <span class="flex items-center gap-2">📞 Voice & Softphone</span>
              </a>
              <a
                routerLink="/accounts"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-blue-400 hover:bg-blue-500/10 border border-blue-500/20 transition">
                <span class="flex items-center gap-2">🏢 B2B Accounts CRM</span>
              </a>
              <a
                routerLink="/workflows"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-cyan-400 hover:bg-cyan-500/10 border border-cyan-500/20 transition">
                <span class="flex items-center gap-2">⚡ Node Workflows</span>
              </a>
              <a
                routerLink="/contracts"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-emerald-400 hover:bg-emerald-500/10 border border-emerald-500/20 transition">
                <span class="flex items-center gap-2">✍️ E-Signatures & Deals</span>
              </a>
              <a
                routerLink="/rag"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-purple-400 hover:bg-purple-500/10 border border-purple-500/20 transition">
                <span class="flex items-center gap-2">🧠 Vector RAG pgvector</span>
              </a>
              <a
                routerLink="/broadcasts"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-teal-400 hover:bg-teal-500/10 border border-teal-500/20 transition">
                <span class="flex items-center gap-2">📢 WA Broadcasts</span>
              </a>
              <a
                routerLink="/sla"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-rose-400 hover:bg-rose-500/10 border border-rose-500/20 transition">
                <span class="flex items-center gap-2">🛡️ SLA & Escalations</span>
              </a>
              <a
                routerLink="/journeys"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-purple-400 hover:bg-purple-500/10 border border-purple-500/20 transition">
                <span class="flex items-center gap-2">🎯 Customer Journeys</span>
              </a>
              <a
                routerLink="/lifecycle/journeys"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-fuchsia-400 hover:bg-fuchsia-500/10 border border-fuchsia-500/20 transition">
                <span class="flex items-center gap-2">⚡ Lifecycle DAG Studio</span>
              </a>
              <a
                routerLink="/feedback"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-yellow-400 hover:bg-yellow-500/10 border border-yellow-500/20 transition">
                <span class="flex items-center gap-2">⭐ Feedback & CSAT</span>
              </a>
              <a
                routerLink="/help"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-indigo-400 hover:bg-indigo-500/10 border border-indigo-500/20 transition">
                <span class="flex items-center gap-2">📚 Help Center & Bot</span>
              </a>
              <a
                routerLink="/chatbots"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-indigo-400 hover:bg-indigo-500/10 border border-indigo-500/20 transition">
                <span class="flex items-center gap-2">🤖 Chatbot Flow DAG & Triggers</span>
              </a>
              <a
                routerLink="/warmup"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-amber-400 hover:bg-amber-500/10 border border-amber-500/20 transition">
                <span class="flex items-center gap-2">🔥 P2P Warmup & Spam Rescue</span>
              </a>
              <a
                routerLink="/softphone"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-cyan-400 hover:bg-cyan-500/10 border border-cyan-500/20 transition">
                <span class="flex items-center gap-2">📞 WebRTC Softphone & Fleet</span>
              </a>
              <a
                routerLink="/cpq"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-emerald-400 hover:bg-emerald-500/10 border border-emerald-500/20 transition">
                <span class="flex items-center gap-2">💼 Enterprise CPQ & EAV</span>
              </a>
              <a
                routerLink="/sealing"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-violet-400 hover:bg-violet-500/10 border border-violet-500/20 transition">
                <span class="flex items-center gap-2">🛡️ Cryptographic PDF Sealing</span>
              </a>
              <a
                routerLink="/outbound/spintax"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-amber-400 hover:bg-amber-500/10 border border-amber-500/20 transition">
                <span class="flex items-center gap-2">🎯 AI SDR & Spintax Engine</span>
              </a>
              <a
                routerLink="/deliverability/warmup-swarm"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-emerald-400 hover:bg-emerald-500/10 border border-emerald-500/20 transition">
                <span class="flex items-center gap-2">🐝 Deliverability Warmup Swarm</span>
              </a>
              <a
                routerLink="/inbox/omnichannel"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-indigo-400 hover:bg-indigo-500/10 border border-indigo-500/20 transition">
                <span class="flex items-center gap-2">💬 Omnichannel MIME & Macros</span>
              </a>
              <a
                routerLink="/accounts/csv-import"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-blue-400 hover:bg-blue-500/10 border border-blue-500/20 transition">
                <span class="flex items-center gap-2">📥 High-Throughput CSV Import</span>
              </a>
              <a
                routerLink="/chatbots/simulator"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-fuchsia-400 hover:bg-fuchsia-500/10 border border-fuchsia-500/20 transition">
                <span class="flex items-center gap-2">🎮 Bot Flow Interactive Runner</span>
              </a>
              <a
                routerLink="/contracts/multi-party"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-rose-400 hover:bg-rose-500/10 border border-rose-500/20 transition">
                <span class="flex items-center gap-2">✍️ Multi-Party Sign & OTP</span>
              </a>
              <a
                routerLink="/courier/cod-fraud"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-teal-400 hover:bg-teal-500/10 border border-teal-500/20 transition">
                <span class="flex items-center gap-2">🚚 Multi-Carrier COD & Fraud</span>
              </a>
              <a
                routerLink="/agents/console"
                class="w-full flex items-center justify-between px-3 py-1.5 rounded-lg text-xs text-purple-400 hover:bg-purple-500/10 border border-purple-500/20 transition">
                <span class="flex items-center gap-2">🤖 Autonomous AI Agent Hub</span>
              </a>
            </div>

            <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider px-2">Inboxes</span>
            <button
              (click)="selectedChannelFilter.set('all')"
              [class.bg-slate-800]="selectedChannelFilter() === 'all'"
              class="w-full flex items-center justify-between px-3 py-2 rounded-lg text-sm text-slate-200 hover:bg-slate-800/60 transition">
              <span class="flex items-center gap-2">📥 All Conversations</span>
              <span class="bg-indigo-500/20 text-indigo-400 text-xs px-2 py-0.5 rounded-full font-medium">{{ conversations().length }}</span>
            </button>
            <button
              (click)="selectedChannelFilter.set('whatsapp')"
              [class.bg-slate-800]="selectedChannelFilter() === 'whatsapp'"
              class="w-full flex items-center justify-between px-3 py-2 rounded-lg text-sm text-slate-200 hover:bg-slate-800/60 transition">
              <span class="flex items-center gap-2">🟢 WhatsApp</span>
              <span class="text-xs text-slate-400">Meta + Evo</span>
            </button>
            <button
              (click)="selectedChannelFilter.set('email')"
              [class.bg-slate-800]="selectedChannelFilter() === 'email'"
              class="w-full flex items-center justify-between px-3 py-2 rounded-lg text-sm text-slate-200 hover:bg-slate-800/60 transition">
              <span class="flex items-center gap-2">✉️ Email</span>
            </button>
            <button
              (click)="selectedChannelFilter.set('webchat')"
              [class.bg-slate-800]="selectedChannelFilter() === 'webchat'"
              class="w-full flex items-center justify-between px-3 py-2 rounded-lg text-sm text-slate-200 hover:bg-slate-800/60 transition">
              <span class="flex items-center gap-2">💬 Web Chat</span>
            </button>
          </div>
        </div>

        <div class="border-t border-slate-800 pt-3 space-y-2">
          <!-- User Profile & Tenant Sandbox Card -->
          <div class="p-2.5 rounded-xl bg-slate-950/80 border border-slate-800/80">
            <div class="flex items-center justify-between mb-1">
              <span class="text-[11px] font-semibold text-slate-200 truncate flex items-center gap-1">
                🏢 {{ auth.currentTenant()?.name || 'SGrowing Labs' }}
              </span>
              <span class="px-1.5 py-0.5 rounded text-[9px] font-mono uppercase bg-indigo-500/20 text-indigo-300 border border-indigo-500/30">
                {{ auth.currentUser()?.role || 'Admin' }}
              </span>
            </div>
            <p class="text-[10px] text-slate-400 truncate mb-2">{{ auth.currentUser()?.email || 'admin@sgrowing.io' }}</p>
            <div class="flex items-center gap-1.5">
              <button
                (click)="onSeedDemo()"
                [disabled]="seeding()"
                class="flex-1 px-2 py-1 bg-indigo-600 hover:bg-indigo-500 text-white rounded text-[10px] font-medium transition disabled:opacity-50">
                {{ seeding() ? 'Seeding...' : '🌱 Seed Sandbox' }}
              </button>
              <button
                (click)="auth.logout()"
                class="px-2 py-1 bg-slate-800 hover:bg-rose-900/60 hover:text-rose-200 text-slate-300 rounded text-[10px] font-medium transition">
                🚪 Exit
              </button>
            </div>
            @if (seedMessage()) {
              <p class="text-[10px] text-emerald-400 mt-1.5 text-center">{{ seedMessage() }}</p>
            }
          </div>

          <div class="flex items-center justify-between text-xs text-slate-400">
            <span class="flex items-center gap-1.5">
              <span class="h-2 w-2 rounded-full" [class.bg-emerald-400]="signalR.isConnected()" [class.bg-amber-400]="!signalR.isConnected()"></span>
              {{ signalR.isConnected() ? 'Real-Time Connected' : 'Local Mock Mode' }}
            </span>
            <a href="http://localhost:5000/scalar/v1" target="_blank" class="bg-indigo-500/20 text-indigo-300 hover:text-white px-1.5 py-0.5 rounded text-[10px] flex items-center gap-1 transition">
              Scalar API ↗
            </a>
          </div>
        </div>
      </aside>

      <!-- 2. Middle: Conversation List (Column 2) -->
      <section class="w-80 border-r border-slate-800 bg-slate-900/50 flex flex-col">
        <div class="p-3 border-b border-slate-800">
          <input
            type="text"
            placeholder="Search conversations..."
            class="w-full bg-slate-800/80 border border-slate-700/60 rounded-md px-3 py-1.5 text-xs text-slate-200 placeholder-slate-400 focus:outline-none focus:border-indigo-500"
          />
        </div>

        <div class="flex-1 overflow-y-auto divide-y divide-slate-800/40">
          @for (conv of conversations(); track conv.id) {
            <div
              (click)="selectConversation(conv)"
              [class.bg-slate-800]="activeConversation()?.id === conv.id"
              class="p-3 cursor-pointer hover:bg-slate-800/50 transition">
              <div class="flex items-start justify-between mb-1">
                <span class="font-medium text-sm text-slate-100 flex items-center gap-1.5">
                  <span class="h-2 w-2 rounded-full bg-emerald-400"></span>
                  {{ conv.contact?.firstName }} {{ conv.contact?.lastName }}
                </span>
                <span class="text-[11px] text-slate-400">12m</span>
              </div>
              <p class="text-xs text-slate-400 truncate mb-1.5">
                {{ getLastMessagePreview(conv.id) }}
              </p>
              <div class="flex items-center gap-2">
                <span class="text-[10px] bg-slate-800 text-slate-300 px-1.5 py-0.5 rounded font-mono">
                  {{ getChannelIcon(conv.inboxId) }}
                </span>
                @if (conv.aiMode === 2) {
                  <span class="text-[10px] bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 px-1.5 py-0.5 rounded">
                    🤖 Autonomous Bot
                  </span>
                } @else if (conv.aiMode === 1) {
                  <span class="text-[10px] bg-indigo-500/10 text-indigo-400 border border-indigo-500/20 px-1.5 py-0.5 rounded">
                    ⚡ Copilot
                  </span>
                }
                @if (conv.priority === 4) {
                  <span class="text-[10px] bg-rose-500/20 text-rose-300 border border-rose-500/30 px-1.5 py-0.5 rounded font-semibold">
                    🚨 Urgent SLA
                  </span>
                }
              </div>
            </div>
          }
        </div>
      </section>

      <!-- 3. Message Thread & Composer (Column 3) -->
      <main class="flex-1 flex flex-col bg-slate-950">
        <!-- Thread Header -->
        <header class="h-14 border-b border-slate-800 px-6 flex items-center justify-between bg-slate-900/30">
          <div class="flex items-center gap-3">
            <h2 class="font-semibold text-sm text-slate-100">
              {{ activeConversation()?.contact?.firstName }} {{ activeConversation()?.contact?.lastName }}
            </h2>
            <span class="text-xs text-slate-400 font-mono">{{ activeConversation()?.contact?.phone || activeConversation()?.contact?.email }}</span>
            
            @if (activeConversation()?.priority === 4) {
              <span class="text-xs bg-rose-950 text-rose-300 border border-rose-800 px-2 py-0.5 rounded-full font-medium">
                SLA Breached - Auto Escalated
              </span>
            }
          </div>

          <div class="flex items-center gap-3">
            <!-- Connection State Pill & Offline Buffer Badge -->
            <div class="flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[11px] font-mono border"
                 [ngClass]="{
                   'bg-emerald-950/60 text-emerald-400 border-emerald-800/60': signalR.connectionState() === 'Connected',
                   'bg-amber-950/60 text-amber-300 border-amber-800/60 animate-pulse': signalR.connectionState() === 'Reconnecting',
                   'bg-rose-950/60 text-rose-300 border-rose-800/60': signalR.connectionState() === 'Disconnected'
                 }">
              <span class="w-2 h-2 rounded-full"
                    [ngClass]="{
                      'bg-emerald-400': signalR.connectionState() === 'Connected',
                      'bg-amber-400': signalR.connectionState() === 'Reconnecting',
                      'bg-rose-400': signalR.connectionState() === 'Disconnected'
                    }"></span>
              <span>{{ signalR.connectionState() }}</span>
              @if (signalR.offlineQueue().length > 0) {
                <span class="ml-1 px-1.5 py-0.2 bg-amber-500/20 text-amber-300 rounded text-[10px]">
                  {{ signalR.offlineQueue().length }} queued
                </span>
              }
            </div>

            <app-language-switcher></app-language-switcher>
            <button
              (click)="resolveCurrentConversation()"
              class="text-xs bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 px-3 py-1.5 rounded-md transition">
              ✓ Resolve
            </button>
          </div>
        </header>

        <!-- Messages Stream -->
        <div class="flex-1 p-6 overflow-y-auto space-y-4">
          @for (msg of activeMessages(); track msg.id) {
            <div
              class="flex flex-col max-w-lg"
              [class.ml-auto]="msg.senderType === 2 || msg.senderType === 3"
              [class.items-end]="msg.senderType === 2 || msg.senderType === 3">
              <div
                class="rounded-2xl px-4 py-2.5 text-sm shadow-sm"
                [class.bg-indigo-600]="msg.senderType === 2 && !msg.isOptimistic"
                [class.bg-indigo-600/75]="msg.senderType === 2 && msg.isOptimistic"
                [class.text-white]="msg.senderType === 2"
                [class.bg-slate-800]="msg.senderType === 1"
                [class.text-slate-100]="msg.senderType === 1"
                [class.bg-cyan-950]="msg.senderType === 3"
                [class.border]="msg.senderType === 3"
                [class.border-cyan-800]="msg.senderType === 3"
                [class.text-cyan-100]="msg.senderType === 3">
                {{ msg.content }}
              </div>
              <span class="text-[10px] text-slate-500 mt-1 px-1 flex items-center gap-1.5">
                {{ msg.senderType === 1 ? 'Customer' : msg.senderType === 2 ? 'Agent' : '🤖 AI Agent' }} • {{ msg.createdAtUtc | date:'shortTime' }}
                @if (msg.isOptimistic) {
                  <span class="text-amber-400 font-mono text-[9px] flex items-center gap-0.5">
                    <span>⏳</span> In Flight (Offline Queue)
                  </span>
                }
              </span>
            </div>
          }
        </div>

        <!-- Real-Time Collision Warning Banner (if another agent is locked/typing) -->
        @if (lockedByAgent()) {
          <div class="mx-6 mb-2 p-2.5 rounded-lg border border-amber-500/40 bg-amber-950/40 flex items-center justify-between text-xs text-amber-200">
            <span class="flex items-center gap-2">
              <span class="animate-pulse text-amber-400">⚠️</span>
              <strong>Collision Warning:</strong> Agent {{ lockedByAgent() }} is viewing or replying to this conversation.
            </span>
            <button (click)="lockedByAgent.set(null)" class="text-amber-400 hover:text-white underline text-[11px]">
              Dismiss lock
            </button>
          </div>
        }

        <!-- AI Copilot Suggestion Card (if pending draft exists) -->
        @if (currentAiDraft()) {
          <div class="mx-6 mb-3 p-3.5 rounded-xl border border-indigo-500/30 bg-indigo-950/30 flex items-center justify-between">
            <div class="flex items-start gap-3">
              <span class="text-lg">🤖</span>
              <div>
                <div class="flex items-center gap-2 mb-1">
                  <span class="text-xs font-semibold text-indigo-300">AI Suggested Response</span>
                  <span class="text-[10px] bg-indigo-500/20 text-indigo-400 px-1.5 py-0.2 rounded font-mono">
                    {{ (currentAiDraft()!.confidenceScore * 100) | number:'1.0-0' }}% confidence
                  </span>
                </div>
                <p class="text-xs text-slate-300 italic">"{{ currentAiDraft()!.suggestedReply }}"</p>
              </div>
            </div>
            <div class="flex items-center gap-2">
              <button
                (click)="applyAiDraft()"
                class="text-xs bg-indigo-600 hover:bg-indigo-500 text-white font-medium px-3 py-1.5 rounded-lg transition shadow-md shadow-indigo-600/20">
                1-Click Approve & Send
              </button>
            </div>
          </div>
        }

        <!-- Composer & Action Bar -->
        <footer class="p-4 border-t border-slate-800 bg-slate-900/40 space-y-2.5">
          <!-- Quick Action Bar -->
          <div class="flex items-center justify-between text-xs">
            <div class="flex items-center gap-2">
              <!-- Channel Switcher -->
              <span class="text-slate-400">Send via:</span>
              <div class="flex items-center bg-slate-950 p-0.5 rounded-lg border border-slate-800">
                <button 
                  (click)="activeComposerChannel.set('whatsapp')"
                  [class]="activeComposerChannel() === 'whatsapp' ? 'bg-emerald-600 text-white' : 'text-slate-400 hover:text-slate-200'"
                  class="px-2 py-0.5 rounded text-[11px] font-medium transition">
                  🟢 WhatsApp
                </button>
                <button 
                  (click)="activeComposerChannel.set('email')"
                  [class]="activeComposerChannel() === 'email' ? 'bg-indigo-600 text-white' : 'text-slate-400 hover:text-slate-200'"
                  class="px-2 py-0.5 rounded text-[11px] font-medium transition">
                  ✉️ Email
                </button>
              </div>
            </div>

            <div class="flex items-center gap-2">
              <button 
                (click)="insertBookingLink()"
                class="bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 px-2.5 py-1 rounded-lg text-xs transition flex items-center gap-1">
                <span>📅 Schedule Meeting</span>
              </button>
              <button 
                (click)="insertCheckoutLink()"
                class="bg-emerald-950 hover:bg-emerald-900 text-emerald-300 border border-emerald-800/80 px-2.5 py-1 rounded-lg text-xs transition flex items-center gap-1 font-medium">
                <span>💳 Checkout Link ($299)</span>
              </button>
            </div>
          </div>

          <!-- Input Box -->
          <div class="flex items-center gap-2">
            <input
              type="text"
              [(ngModel)]="composerText"
              (keyup.enter)="sendMessage()"
              placeholder="Type your message or click an action above..."
              class="flex-1 bg-slate-800/80 border border-slate-700/80 rounded-xl px-4 py-2.5 text-sm text-slate-100 placeholder-slate-400 focus:outline-none focus:border-indigo-500 transition"
            />
            <button
              (click)="sendMessage()"
              class="bg-indigo-600 hover:bg-indigo-500 text-white font-medium text-sm px-5 py-2.5 rounded-xl transition shadow-lg shadow-indigo-500/20">
              Send
            </button>
          </div>
        </footer>
      </main>

      <!-- 4. Right: Customer 360 & CRM Details (Column 4) -->
      <aside class="w-72 border-l border-slate-800 bg-slate-900/60 p-5 flex flex-col justify-between overflow-y-auto">
        <div>
          <h3 class="text-xs font-semibold text-slate-400 uppercase tracking-wider mb-4">Customer 360 CRM</h3>
          
          <div class="flex items-center gap-3 mb-6">
            <div class="h-12 w-12 rounded-full bg-slate-800 border border-slate-700 flex items-center justify-center font-bold text-slate-200">
              {{ activeConversation()?.contact?.firstName?.[0] || '?' }}
            </div>
            <div>
              <h4 class="font-semibold text-sm text-white">
                {{ activeConversation()?.contact?.firstName }} {{ activeConversation()?.contact?.lastName }}
              </h4>
              <span class="text-xs text-emerald-400 bg-emerald-500/10 px-2 py-0.5 rounded-full font-medium">
                Resolved 360 Identity
              </span>
            </div>
          </div>

          <div class="space-y-4 text-xs">
            <div>
              <span class="text-slate-400 block mb-1">Email (Resolved)</span>
              <span class="text-slate-200 font-mono">{{ activeConversation()?.contact?.email || 'None' }}</span>
            </div>
            <div>
              <span class="text-slate-400 block mb-1">Phone / WhatsApp</span>
              <span class="text-slate-200 font-mono">{{ activeConversation()?.contact?.phone || 'None' }}</span>
            </div>
            <div>
              <span class="text-slate-400 block mb-1">CRM Deal Pipeline</span>
              <div class="p-2 rounded bg-slate-950/80 border border-slate-800 text-amber-300 font-medium">
                Enterprise Deal: $2,400 (Won)
              </div>
            </div>
            <div>
              <span class="text-slate-400 block mb-1">Tags</span>
              <div class="flex flex-wrap gap-1 mt-1">
                @for (tag of activeConversation()?.contact?.tags || []; track tag) {
                  <span class="bg-slate-800 text-slate-300 px-2 py-0.5 rounded font-mono text-[10px]">{{ tag }}</span>
                }
              </div>
            </div>
            <div>
              <span class="text-slate-400 block mb-1">Dynamic Attributes (JSONB)</span>
              <div class="bg-slate-950/80 p-2.5 rounded-lg border border-slate-800 font-mono text-[11px] text-cyan-300">
                <pre class="whitespace-pre-wrap">{{ activeConversation()?.contact?.customFields | json }}</pre>
              </div>
            </div>
          </div>
        </div>

        <div class="border-t border-slate-800 pt-4 mt-6">
          <button class="w-full text-xs text-center text-slate-400 hover:text-slate-200 py-1 transition">
            View Full 360 Timeline →
          </button>
        </div>
      </aside>
    </div>
  `
})
export class InboxShellComponent implements OnInit {
  public signalR = inject(SignalRService);
  public auth = inject(AuthService);
  public translation = inject(TranslationService);
  private api = inject(ApiService);

  public selectedChannelFilter = signal<string>('all');
  public conversations = signal<ConversationDto[]>([]);
  public activeConversation = signal<ConversationDto | null>(null);
  public activeMessages = signal<MessageDto[]>([]);
  public currentAiDraft = signal<AiDraftDto | null>(null);
  public lockedByAgent = signal<string | null>(null);
  public activeComposerChannel = signal<'whatsapp' | 'email'>('whatsapp');
  public composerText = '';
  public seeding = signal<boolean>(false);
  public seedMessage = signal<string | null>(null);

  private currentTenantId = '00000000-0000-0000-0000-000000000001';

  ngOnInit(): void {
    // 1. Seed demo / loaded conversations
    this.seedDemoData();

    // 2. Connect to real-time SignalR hub
    this.signalR.startConnection();

    // 3. Subscribe to real-time inbound messages
    this.signalR.messageReceived$.subscribe((msg) => {
      if (this.activeConversation()?.id === msg.conversationId) {
        this.activeMessages.update(list => [...list, msg]);
      }
    });

    // 4. Subscribe to AI drafts generated by background copilot
    this.signalR.aiDraftGenerated$.subscribe((draft) => {
      if (this.activeConversation()?.id === draft.conversationId) {
        this.currentAiDraft.set(draft);
      }
    });

    // 5. Subscribe to conversation collision lock
    this.signalR.conversationLocked$.subscribe((lock) => {
      if (this.activeConversation()?.id === lock.conversationId) {
        this.lockedByAgent.set(lock.agentName);
      }
    });

    this.signalR.conversationUnlocked$.subscribe((unlock) => {
      if (this.activeConversation()?.id === unlock.conversationId) {
        this.lockedByAgent.set(null);
      }
    });

    // 6. Subscribe to flushed offline messages
    this.signalR.offlineMessageFlushed$.subscribe((flushed) => {
      this.activeMessages.update(list =>
        list.map(m => m.clientUuid === flushed.clientUuid ? { ...m, isOptimistic: false, deliveryStatus: 2 } : m)
      );
    });
  }

  private seedDemoData(): void {
    const contactA: ContactDto = {
      id: 'contact-01',
      tenantId: this.currentTenantId,
      firstName: 'Elena',
      lastName: 'Fisher',
      email: 'elena@travelmag.org',
      phone: '+15559876543',
      lifecycleStage: 3,
      customFields: { plan: 'Enterprise', lead_source: 'WhatsApp', satisfaction_score: 98 },
      tags: ['vip', 'whatsapp-verified', 'paying-customer'],
      createdAtUtc: new Date().toISOString()
    };

    const convA: ConversationDto = {
      id: 'conv-01',
      tenantId: this.currentTenantId,
      inboxId: 'inbox-wa',
      contactId: contactA.id,
      status: 1,
      priority: 2,
      aiMode: 2, // Autonomous Agent enabled
      lastMessageAtUtc: new Date().toISOString(),
      createdAtUtc: new Date().toISOString(),
      contact: contactA
    };

    this.conversations.set([convA]);
    this.selectConversation(convA);
  }

  public selectConversation(conv: ConversationDto): void {
    this.activeConversation.set(conv);
    this.activeMessages.set([
      {
        id: 'msg-01',
        conversationId: conv.id,
        senderType: 1, // Contact
        contentType: 1,
        content: 'Hi! Can you share pricing for the enterprise tier and how WhatsApp integrates with CRM?',
        deliveryStatus: 3,
        createdAtUtc: new Date(Date.now() - 1000 * 60 * 15).toISOString()
      },
      {
        id: 'msg-02',
        conversationId: conv.id,
        senderType: 3, // Bot
        contentType: 1,
        content: 'Our Enterprise tier starts at $299/mo with full WhatsApp multi-device integration, automated AI SDR, and assisted commerce.',
        deliveryStatus: 3,
        createdAtUtc: new Date(Date.now() - 1000 * 60 * 14).toISOString()
      }
    ]);

    // Set demo AI Draft
    this.currentAiDraft.set({
      id: 'draft-01',
      conversationId: conv.id,
      suggestedReply: 'Would you like to schedule a 15-minute onboarding demo or get an instant checkout link?',
      confidenceScore: 0.96,
      status: 'Drafted',
      createdAtUtc: new Date().toISOString()
    });
  }

  public sendMessage(): void {
    if (!this.composerText.trim() || !this.activeConversation()) return;

    const text = this.composerText;
    const convId = this.activeConversation()!.id;
    const clientUuid = 'cli-' + Date.now() + '-' + Math.random().toString(36).substring(2, 7);
    const isConnected = this.signalR.isConnected();

    const newMsg: MessageDto = {
      id: clientUuid,
      conversationId: convId,
      senderType: 2, // Agent
      contentType: 1,
      content: text,
      deliveryStatus: isConnected ? 2 : 1,
      createdAtUtc: new Date().toISOString(),
      isOptimistic: !isConnected,
      clientUuid: clientUuid
    };

    this.activeMessages.update(list => [...list, newMsg]);
    this.composerText = '';

    if (!isConnected) {
      this.signalR.enqueueOfflineMessage({
        clientUuid: clientUuid,
        conversationId: convId,
        content: text,
        queuedAt: new Date().toISOString()
      });
    } else {
      setTimeout(() => {
        this.activeMessages.update(list =>
          list.map(m => m.clientUuid === clientUuid ? { ...m, isOptimistic: false, deliveryStatus: 2 } : m)
        );
      }, 300);
    }
  }

  public insertBookingLink(): void {
    this.composerText = 'You can select a convenient slot for our meeting here: https://sgrowing.app/book/slot-enterprise-demo';
  }

  public insertCheckoutLink(): void {
    this.composerText = 'Here is your direct assisted payment link: https://sgrowing.app/pay/chk-987216 ($299.00 USD)';
  }

  public applyAiDraft(): void {
    if (!this.currentAiDraft()) return;

    const approvedMsg: MessageDto = {
      id: 'msg-' + Date.now(),
      conversationId: this.currentAiDraft()!.conversationId,
      senderType: 2, // Agent approved
      contentType: 1,
      content: this.currentAiDraft()!.suggestedReply,
      deliveryStatus: 2,
      createdAtUtc: new Date().toISOString()
    };

    this.activeMessages.update(list => [...list, approvedMsg]);
    this.currentAiDraft.set(null);
  }

  public resolveCurrentConversation(): void {
    if (!this.activeConversation()) return;
    this.activeConversation.update(c => c ? { ...c, status: 3 } : null);
  }

  public getLastMessagePreview(convId: string): string {
    return 'Our Enterprise tier starts at $299/mo with full WhatsApp...';
  }

  public getChannelIcon(inboxId: string): string {
    return inboxId.includes('wa') ? 'WA' : 'MAIL';
  }

  public onSeedDemo(): void {
    this.seeding.set(true);
    this.seedMessage.set(null);
    const tenantId = this.auth.currentTenant()?.id;
    this.api.seedDemoData(tenantId).subscribe({
      next: (res) => {
        this.seeding.set(false);
        this.seedMessage.set('✅ Demo sandbox seeded!');
        setTimeout(() => this.seedMessage.set(null), 4000);
      },
      error: () => {
        this.seeding.set(false);
        this.seedMessage.set('❌ Seeding failed');
        setTimeout(() => this.seedMessage.set(null), 4000);
      }
    });
  }
}
