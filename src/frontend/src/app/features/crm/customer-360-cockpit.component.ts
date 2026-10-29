import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';

export interface LinkedChannelBadge {
  identityType: number;
  channelName: string;
  identityValue: string;
  linkedAtUtc: string;
  icon: string;
}

export interface CustomerTimelineItem {
  id: string;
  unifiedCustomerId: string;
  eventType: string;
  channel: string;
  summary: string;
  icon: string;
  severity: 'info' | 'success' | 'warning' | 'accent';
  payloadJson: string;
  occurredAtUtc: string;
}

export interface CustomerProfileSummary {
  id: string;
  primaryName: string;
  primaryPhone: string;
  primaryEmail: string;
  company?: string;
  lifecycleStage: string;
  lifetimeValue: number;
  totalOrdersCount: number;
  totalSpent: number;
  deliverySuccessRate: number;
  churnRiskLevel: 'Low' | 'Medium' | 'High' | 'Critical';
  churnRiskScore: number;
  createdAtUtc: string;
  lastSeenAtUtc: string;
  linkedChannels: LinkedChannelBadge[];
}

export interface AttributedTouchpointShare {
  touchpointId: string;
  channel: string;
  campaignName: string;
  stage: string;
  weightPercent: number;
  attributedRevenue: number;
  cost: number;
  occurredAtUtc: string;
}

export interface AttributionModelResult {
  modelType: number;
  modelName: string;
  description: string;
  shares: AttributedTouchpointShare[];
  channelRevenueBreakdown: Record<string, number>;
  campaignRevenueBreakdown: Record<string, number>;
}

export interface CampaignRoasItem {
  campaignName: string;
  channel: string;
  totalCost: number;
  attributedRevenue: number;
  trueRoas: number;
  cac: number;
  customerCount: number;
  touchpointCount: number;
}

@Component({
  selector: 'app-customer-360-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div dir="ltr" class="min-h-screen bg-slate-950 text-slate-100 flex flex-col font-sans select-none">
      <!-- Top Navigation & Header Bar -->
      <header class="border-b border-slate-800 bg-slate-900/60 backdrop-blur sticky top-0 z-30 px-6 py-4 flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div class="flex items-center gap-3">
          <a routerLink="/" class="text-xs text-slate-400 hover:text-white px-3 py-1.5 rounded-lg bg-slate-900 border border-slate-800 transition flex items-center gap-1.5">
            <span>←</span>
            <span>Cockpit</span>
          </a>
          <div>
            <div class="flex items-center gap-2">
              <span class="text-2xl">🌐</span>
              <h1 class="text-xl font-bold tracking-tight text-white flex items-center gap-2">
                Customer 360 Unified Identity Graph & Attribution
              </h1>
              <span class="px-2.5 py-0.5 rounded-full text-xs font-mono bg-cyan-500/10 text-cyan-400 border border-cyan-500/20 font-medium">
                Horizon 19 • Multi-Touch Engine
              </span>
            </div>
            <p class="text-xs text-slate-400 mt-0.5">
              Deterministic cross-channel stitching (_sg_vid, E.164, WhatsApp, Social) with 5-model revenue attribution, true ROAS & CAC.
            </p>
          </div>
        </div>

        <!-- Quick Customer Switcher & Tenant Status -->
        <div class="flex items-center gap-3">
          <div class="flex items-center gap-2 bg-slate-900/80 border border-slate-800 rounded-xl px-3 py-1.5">
            <span class="text-xs text-slate-400">Target Profile:</span>
            <select
              [ngModel]="selectedCustomerId()"
              (ngModelChange)="onSelectCustomer($event)"
              class="bg-transparent text-xs text-cyan-300 font-medium focus:outline-none cursor-pointer">
              @for (cust of availableCustomers(); track cust.id) {
                <option [value]="cust.id" class="bg-slate-900 text-slate-200">
                  {{ cust.primaryName }} ({{ cust.primaryPhone || cust.primaryEmail }})
                </option>
              }
            </select>
          </div>

          <button
            (click)="refreshData()"
            [disabled]="isLoading()"
            class="px-3.5 py-1.5 bg-cyan-600 hover:bg-cyan-500 disabled:opacity-50 text-white text-xs font-semibold rounded-lg shadow-sm shadow-cyan-950 transition flex items-center gap-1.5">
            <span [class.animate-spin]="isLoading()">🔄</span>
            <span>Refresh</span>
          </button>
        </div>
      </header>

      <!-- Main Cockpit Body -->
      <main class="flex-1 p-6 max-w-7xl w-full mx-auto space-y-6">

        <!-- 1. Customer 360 Profile Hero Card -->
        @if (currentCustomer(); as customer) {
          <section class="bg-gradient-to-r from-slate-900 via-slate-900/90 to-slate-900/60 border border-slate-800/80 rounded-2xl p-6 shadow-xl relative overflow-hidden">
            <!-- Background Glow Accent -->
            <div class="absolute -top-24 -right-24 w-80 h-80 bg-cyan-500/5 rounded-full blur-3xl pointer-events-none"></div>
            <div class="absolute -bottom-24 -left-24 w-80 h-80 bg-violet-500/5 rounded-full blur-3xl pointer-events-none"></div>

            <div class="relative z-10 flex flex-col lg:flex-row items-start lg:items-center justify-between gap-6">
              <!-- Left Profile Overview -->
              <div class="flex items-start sm:items-center gap-4">
                <div class="w-16 h-16 rounded-2xl bg-gradient-to-br from-cyan-500 to-violet-600 flex items-center justify-center text-white text-2xl font-black shadow-lg shadow-cyan-950/50">
                  {{ getInitials(customer.primaryName) }}
                </div>
                <div>
                  <div class="flex items-center gap-3 flex-wrap">
                    <h2 class="text-xl font-bold text-white tracking-tight">{{ customer.primaryName }}</h2>
                    <span class="px-2.5 py-0.5 rounded-full text-xs font-medium border"
                      [ngClass]="getStagePillClass(customer.lifecycleStage)">
                      {{ customer.lifecycleStage }}
                    </span>
                    <!-- Churn Risk Pill -->
                    <span class="px-2.5 py-0.5 rounded-full text-xs font-semibold flex items-center gap-1.5 border"
                      [ngClass]="getChurnRiskPillClass(customer.churnRiskLevel)">
                      <span class="w-2 h-2 rounded-full" [ngClass]="getChurnRiskDotClass(customer.churnRiskLevel)"></span>
                      <span>Churn Risk: {{ customer.churnRiskLevel }} ({{ customer.churnRiskScore }}%)</span>
                    </span>
                  </div>

                  <div class="flex items-center gap-4 text-xs text-slate-400 mt-1.5 flex-wrap">
                    @if (customer.primaryPhone) {
                      <span class="flex items-center gap-1 text-slate-300">
                        <span>📞</span> {{ customer.primaryPhone }}
                      </span>
                    }
                    @if (customer.primaryEmail) {
                      <span class="flex items-center gap-1 text-slate-300">
                        <span>✉️</span> {{ customer.primaryEmail }}
                      </span>
                    }
                    @if (customer.company) {
                      <span class="flex items-center gap-1 text-slate-400">
                        <span>🏢</span> {{ customer.company }}
                      </span>
                    }
                    <span class="text-slate-500">•</span>
                    <span>Last Seen: {{ formatDate(customer.lastSeenAtUtc) }}</span>
                  </div>

                  <!-- Linked Channels Badges -->
                  <div class="mt-3 flex items-center gap-2 flex-wrap">
                    <span class="text-[11px] font-semibold text-slate-400 uppercase tracking-wider">Identities:</span>
                    @for (ch of customer.linkedChannels; track ch.identityValue) {
                      <span class="px-2.5 py-1 rounded-lg text-xs bg-slate-950/80 border border-slate-700/60 text-slate-200 flex items-center gap-1.5 shadow-sm hover:border-slate-500 transition">
                        <span>{{ getChannelEmoji(ch.identityType) }}</span>
                        <span class="font-mono text-cyan-300 text-[11px]">{{ ch.channelName }}:</span>
                        <span class="text-white font-medium text-[11px]">{{ ch.identityValue }}</span>
                      </span>
                    }
                  </div>
                </div>
              </div>

              <!-- Right Financial & Health Metrics -->
              <div class="grid grid-cols-2 sm:grid-cols-4 gap-3 w-full lg:w-auto">
                <div class="bg-slate-950/70 border border-slate-800/80 rounded-xl p-3 text-center min-w-[100px]">
                  <span class="text-[10px] text-slate-400 font-semibold uppercase tracking-wider block">Lifetime Value</span>
                  <span class="text-lg font-extrabold text-cyan-400 font-mono mt-0.5 block">\${{ customer.lifetimeValue | number:'1.2-2' }}</span>
                </div>
                <div class="bg-slate-950/70 border border-slate-800/80 rounded-xl p-3 text-center min-w-[100px]">
                  <span class="text-[10px] text-slate-400 font-semibold uppercase tracking-wider block">Orders Placed</span>
                  <span class="text-lg font-extrabold text-white font-mono mt-0.5 block">{{ customer.totalOrdersCount }}</span>
                </div>
                <div class="bg-slate-950/70 border border-slate-800/80 rounded-xl p-3 text-center min-w-[100px]">
                  <span class="text-[10px] text-slate-400 font-semibold uppercase tracking-wider block">Delivery Rate</span>
                  <span class="text-lg font-extrabold text-emerald-400 font-mono mt-0.5 block">{{ customer.deliverySuccessRate }}%</span>
                </div>
                <div class="bg-slate-950/70 border border-slate-800/80 rounded-xl p-3 text-center min-w-[100px]">
                  <span class="text-[10px] text-slate-400 font-semibold uppercase tracking-wider block">Identities</span>
                  <span class="text-lg font-extrabold text-violet-400 font-mono mt-0.5 block">{{ customer.linkedChannels.length }}</span>
                </div>
              </div>
            </div>
          </section>
        }

        <!-- 2. Navigation Tabs (Attribution vs Timeline vs Identity Stitching Tester) -->
        <div class="flex items-center gap-2 border-b border-slate-800 pb-2">
          <button
            (click)="activeTab.set('attribution')"
            [class.bg-cyan-600]="activeTab() === 'attribution'"
            [class.text-white]="activeTab() === 'attribution'"
            [class.text-slate-400]="activeTab() !== 'attribution'"
            class="px-4 py-2 rounded-xl text-xs font-bold transition flex items-center gap-2 hover:text-white bg-slate-900 border border-slate-800">
            <span>📊</span>
            <span>Multi-Touch Attribution Engine</span>
          </button>
          <button
            (click)="activeTab.set('timeline')"
            [class.bg-cyan-600]="activeTab() === 'timeline'"
            [class.text-white]="activeTab() === 'timeline'"
            [class.text-slate-400]="activeTab() !== 'timeline'"
            class="px-4 py-2 rounded-xl text-xs font-bold transition flex items-center gap-2 hover:text-white bg-slate-900 border border-slate-800">
            <span>⏳</span>
            <span>Master Interaction Stream ({{ timelineEvents().length }})</span>
          </button>
          <button
            (click)="activeTab.set('stitcher')"
            [class.bg-cyan-600]="activeTab() === 'stitcher'"
            [class.text-white]="activeTab() === 'stitcher'"
            [class.text-slate-400]="activeTab() !== 'stitcher'"
            class="px-4 py-2 rounded-xl text-xs font-bold transition flex items-center gap-2 hover:text-white bg-slate-900 border border-slate-800">
            <span>🔗</span>
            <span>Live Identity Stitching & Event Simulator</span>
          </button>
        </div>

        <!-- 3. TAB 1: Multi-Touch Attribution Engine -->
        @if (activeTab() === 'attribution') {
          <div class="space-y-6">
            <!-- 5 Attribution Models Overview Cards -->
            <div class="grid grid-cols-1 md:grid-cols-5 gap-4">
              <!-- First-Touch -->
              <div
                (click)="selectedModelType.set(1)"
                [class.ring-2]="selectedModelType() === 1"
                [class.ring-cyan-500]="selectedModelType() === 1"
                class="bg-slate-900/70 border border-slate-800 rounded-xl p-4 cursor-pointer hover:border-slate-700 transition">
                <div class="flex items-center justify-between">
                  <span class="text-xs font-bold text-cyan-400">First-Touch</span>
                  <span class="text-[10px] px-1.5 py-0.5 rounded bg-cyan-500/10 text-cyan-300">100% Top</span>
                </div>
                <p class="text-[11px] text-slate-400 mt-1">Full credit to initial discovery source.</p>
                <div class="mt-3 text-lg font-bold text-white font-mono">
                  \${{ getModelRevenue(1) | number:'1.2-2' }}
                </div>
              </div>

              <!-- Last-Touch -->
              <div
                (click)="selectedModelType.set(2)"
                [class.ring-2]="selectedModelType() === 2"
                [class.ring-violet-500]="selectedModelType() === 2"
                class="bg-slate-900/70 border border-slate-800 rounded-xl p-4 cursor-pointer hover:border-slate-700 transition">
                <div class="flex items-center justify-between">
                  <span class="text-xs font-bold text-violet-400">Last-Touch</span>
                  <span class="text-[10px] px-1.5 py-0.5 rounded bg-violet-500/10 text-violet-300">100% Won</span>
                </div>
                <p class="text-[11px] text-slate-400 mt-1">Full credit to final touchpoint before order.</p>
                <div class="mt-3 text-lg font-bold text-white font-mono">
                  \${{ getModelRevenue(2) | number:'1.2-2' }}
                </div>
              </div>

              <!-- Linear -->
              <div
                (click)="selectedModelType.set(3)"
                [class.ring-2]="selectedModelType() === 3"
                [class.ring-emerald-500]="selectedModelType() === 3"
                class="bg-slate-900/70 border border-slate-800 rounded-xl p-4 cursor-pointer hover:border-slate-700 transition">
                <div class="flex items-center justify-between">
                  <span class="text-xs font-bold text-emerald-400">Linear</span>
                  <span class="text-[10px] px-1.5 py-0.5 rounded bg-emerald-500/10 text-emerald-300">1/N Equal</span>
                </div>
                <p class="text-[11px] text-slate-400 mt-1">Even credit split across all steps.</p>
                <div class="mt-3 text-lg font-bold text-white font-mono">
                  \${{ getModelRevenue(3) | number:'1.2-2' }}
                </div>
              </div>

              <!-- U-Shaped -->
              <div
                (click)="selectedModelType.set(6)"
                [class.ring-2]="selectedModelType() === 6"
                [class.ring-amber-500]="selectedModelType() === 6"
                class="bg-slate-900/70 border border-slate-800 rounded-xl p-4 cursor-pointer hover:border-slate-700 transition">
                <div class="flex items-center justify-between">
                  <span class="text-xs font-bold text-amber-400">U-Shaped</span>
                  <span class="text-[10px] px-1.5 py-0.5 rounded bg-amber-500/10 text-amber-300">40 / 20 / 40</span>
                </div>
                <p class="text-[11px] text-slate-400 mt-1">40% first, 40% lead/won, 20% middle.</p>
                <div class="mt-3 text-lg font-bold text-white font-mono">
                  \${{ getModelRevenue(6) | number:'1.2-2' }}
                </div>
              </div>

              <!-- Time-Decay -->
              <div
                (click)="selectedModelType.set(5)"
                [class.ring-2]="selectedModelType() === 5"
                [class.ring-rose-500]="selectedModelType() === 5"
                class="bg-slate-900/70 border border-slate-800 rounded-xl p-4 cursor-pointer hover:border-slate-700 transition">
                <div class="flex items-center justify-between">
                  <span class="text-xs font-bold text-rose-400">Time-Decay</span>
                  <span class="text-[10px] px-1.5 py-0.5 rounded bg-rose-500/10 text-rose-300">τ = 7d Half-Life</span>
                </div>
                <p class="text-[11px] text-slate-400 mt-1">Exponential decay towards conversion.</p>
                <div class="mt-3 text-lg font-bold text-white font-mono">
                  \${{ getModelRevenue(5) | number:'1.2-2' }}
                </div>
              </div>
            </div>

            <!-- Model Comparison Bar Visualizer -->
            <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-6">
              <div class="flex items-center justify-between mb-4">
                <div>
                  <h3 class="text-sm font-bold text-white flex items-center gap-2">
                    <span>⚖️</span>
                    <span>5-Model Revenue Distribution Comparison</span>
                  </h3>
                  <p class="text-xs text-slate-400">Cross-channel credit divergence across First-Touch, Last-Touch, Linear, U-Shaped, and Time-Decay.</p>
                </div>
                <span class="text-xs font-mono text-cyan-400 font-semibold bg-cyan-950/60 px-3 py-1 rounded-lg border border-cyan-800/40">
                  Total Attributed Revenue: \${{ attributionComparison().totalRevenue | number:'1.2-2' }}
                </span>
              </div>

              <!-- Comparison Table of Touchpoints -->
              @if (currentModelResult(); as model) {
                <div class="overflow-x-auto">
                  <table class="w-full text-left text-xs">
                    <thead class="bg-slate-950/70 text-slate-400 uppercase text-[10px] tracking-wider border-b border-slate-800">
                      <tr>
                        <th class="py-3 px-4">Touchpoint / Channel</th>
                        <th class="py-3 px-4">Campaign</th>
                        <th class="py-3 px-4">Stage</th>
                        <th class="py-3 px-4">Weight %</th>
                        <th class="py-3 px-4">Attributed Revenue</th>
                        <th class="py-3 px-4">Cost</th>
                        <th class="py-3 px-4">Date</th>
                      </tr>
                    </thead>
                    <tbody class="divide-y divide-slate-800/60 text-slate-200">
                      @for (share of model.shares; track share.touchpointId) {
                        <tr class="hover:bg-slate-800/30 transition">
                          <td class="py-3 px-4 font-semibold text-white flex items-center gap-2">
                            <span class="w-2 h-2 rounded-full bg-cyan-400"></span>
                            {{ share.channel }}
                          </td>
                          <td class="py-3 px-4 text-cyan-300 font-mono">{{ share.campaignName }}</td>
                          <td class="py-3 px-4">
                            <span class="px-2 py-0.5 rounded text-[10px] font-mono bg-slate-800 text-slate-300">
                              {{ share.stage }}
                            </span>
                          </td>
                          <td class="py-3 px-4">
                            <div class="flex items-center gap-2">
                              <div class="w-16 bg-slate-800 h-2 rounded-full overflow-hidden">
                                <div class="bg-cyan-500 h-full rounded-full" [style.width.%]="share.weightPercent"></div>
                              </div>
                              <span class="font-mono text-white">{{ share.weightPercent }}%</span>
                            </div>
                          </td>
                          <td class="py-3 px-4 font-mono font-bold text-emerald-400">
                            \${{ share.attributedRevenue | number:'1.2-2' }}
                          </td>
                          <td class="py-3 px-4 font-mono text-slate-400">\${{ share.cost | number:'1.2-2' }}</td>
                          <td class="py-3 px-4 text-slate-400">{{ formatDate(share.occurredAtUtc) }}</td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              }
            </div>

            <!-- Marketing Campaigns True ROAS & CAC Table -->
            <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-6">
              <div class="flex items-center justify-between mb-4">
                <div>
                  <h3 class="text-sm font-bold text-white flex items-center gap-2">
                    <span>🎯</span>
                    <span>Campaign True ROAS & Customer Acquisition Cost (CAC)</span>
                  </h3>
                  <p class="text-xs text-slate-400">Attributed return on ad spend calculated using {{ getActiveModelName() }}.</p>
                </div>
              </div>

              <div class="overflow-x-auto">
                <table class="w-full text-left text-xs">
                  <thead class="bg-slate-950/70 text-slate-400 uppercase text-[10px] tracking-wider border-b border-slate-800">
                    <tr>
                      <th class="py-3 px-4">Campaign</th>
                      <th class="py-3 px-4">Channel</th>
                      <th class="py-3 px-4">Total Cost</th>
                      <th class="py-3 px-4">Attributed Revenue</th>
                      <th class="py-3 px-4">True ROAS</th>
                      <th class="py-3 px-4">CAC</th>
                      <th class="py-3 px-4">Customers</th>
                      <th class="py-3 px-4">Touches</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-800/60 text-slate-200">
                    @for (camp of campaignRoas(); track camp.campaignName) {
                      <tr class="hover:bg-slate-800/30 transition">
                        <td class="py-3 px-4 font-bold text-white font-mono">{{ camp.campaignName }}</td>
                        <td class="py-3 px-4 text-slate-300">{{ camp.channel }}</td>
                        <td class="py-3 px-4 font-mono text-slate-300">\${{ camp.totalCost | number:'1.2-2' }}</td>
                        <td class="py-3 px-4 font-mono font-bold text-emerald-400">\${{ camp.attributedRevenue | number:'1.2-2' }}</td>
                        <td class="py-3 px-4 font-mono font-black"
                          [class.text-emerald-400]="camp.trueRoas >= 4.0"
                          [class.text-amber-400]="camp.trueRoas >= 2.0 && camp.trueRoas < 4.0"
                          [class.text-rose-400]="camp.trueRoas < 2.0">
                          {{ camp.trueRoas }}x
                        </td>
                        <td class="py-3 px-4 font-mono text-slate-300">\${{ camp.cac | number:'1.2-2' }}</td>
                        <td class="py-3 px-4 text-cyan-300 font-bold">{{ camp.customerCount }}</td>
                        <td class="py-3 px-4 text-slate-400">{{ camp.touchpointCount }}</td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        }

        <!-- 4. TAB 2: Master Chronological Interaction Stream -->
        @if (activeTab() === 'timeline') {
          <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-6 space-y-4">
            <!-- Timeline Filter Bar -->
            <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-slate-800 pb-4">
              <!-- Channel Filter Chips -->
              <div class="flex items-center gap-1.5 flex-wrap">
                <span class="text-xs text-slate-400 mr-1">Filter Channel:</span>
                @for (c of channelFilters; track c) {
                  <button
                    (click)="selectedChannelFilter.set(c)"
                    [class.bg-cyan-600]="selectedChannelFilter() === c"
                    [class.text-white]="selectedChannelFilter() === c"
                    [class.bg-slate-950]="selectedChannelFilter() !== c"
                    [class.text-slate-400]="selectedChannelFilter() !== c"
                    class="px-2.5 py-1 rounded-lg text-xs font-medium border border-slate-800 hover:text-white transition">
                    {{ c }}
                  </button>
                }
              </div>

              <!-- Sorting Toggle -->
              <div class="flex items-center gap-2">
                <span class="text-xs text-slate-400">Order:</span>
                <button
                  (click)="toggleSort()"
                  class="px-3 py-1 bg-slate-950 border border-slate-800 text-xs font-semibold rounded-lg text-cyan-300 hover:border-slate-600 transition flex items-center gap-1.5">
                  <span>{{ isChronological() ? '⬆️ Chronological (Oldest)' : '⬇️ Recent First' }}</span>
                </button>
              </div>
            </div>

            <!-- Event Cards Stream -->
            <div class="space-y-3 relative before:absolute before:inset-0 before:left-5 before:w-0.5 before:bg-slate-800/80">
              @for (ev of filteredTimeline(); track ev.id) {
                <div class="relative flex items-start gap-4 pl-2 group">
                  <!-- Icon Bubble -->
                  <div class="w-7 h-7 rounded-full flex items-center justify-center text-xs z-10 shadow-md ring-4 ring-slate-950"
                    [ngClass]="getEventIconClass(ev.severity)">
                    {{ getEventIconEmoji(ev.eventType) }}
                  </div>

                  <!-- Event Card -->
                  <div class="flex-1 bg-slate-950/70 border border-slate-800 rounded-xl p-4 hover:border-slate-700 transition">
                    <div class="flex items-center justify-between flex-wrap gap-2">
                      <div class="flex items-center gap-2">
                        <span class="px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider"
                          [ngClass]="getChannelBadgeClass(ev.channel)">
                          {{ ev.channel }}
                        </span>
                        <h4 class="text-xs font-bold text-white">{{ ev.eventType }}</h4>
                      </div>
                      <span class="text-[11px] font-mono text-slate-400">{{ formatDate(ev.occurredAtUtc) }}</span>
                    </div>

                    <p class="text-xs text-slate-300 mt-1.5 leading-relaxed">{{ ev.summary }}</p>

                    @if (ev.payloadJson && ev.payloadJson !== '{}') {
                      <details class="mt-2 text-[11px]">
                        <summary class="text-cyan-400 cursor-pointer hover:underline text-[10px] font-mono">View Payload Metadata</summary>
                        <pre class="bg-slate-900/90 text-cyan-200 p-2 rounded-lg mt-1 overflow-x-auto text-[10px] font-mono">{{ ev.payloadJson }}</pre>
                      </details>
                    }
                  </div>
                </div>
              }
              @if (filteredTimeline().length === 0) {
                <div class="text-center py-12 text-slate-500 text-xs">
                  No interaction events match the selected channel filter.
                </div>
              }
            </div>
          </div>
        }

        <!-- 5. TAB 3: Identity Stitching Tester & Live Simulator -->
        @if (activeTab() === 'stitcher') {
          <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
            <!-- Left: Stitching Form -->
            <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-6 space-y-4">
              <div>
                <h3 class="text-sm font-bold text-white flex items-center gap-2">
                  <span>🧩</span>
                  <span>Deterministic Multi-Channel Identity Stitcher</span>
                </h3>
                <p class="text-xs text-slate-400">
                  Unify anonymous browser cookies (_sg_vid) with Phone (E.164), Email, WhatsApp, and Social handles.
                </p>
              </div>

              <!-- Form Inputs -->
              <div class="space-y-3 text-xs">
                <div>
                  <label class="block text-slate-400 font-semibold mb-1">Web Cookie ID (_sg_vid):</label>
                  <input
                    type="text"
                    [(ngModel)]="stitchCookie"
                    placeholder="_sg_vid_94a8b1c7"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-slate-100 font-mono text-xs focus:border-cyan-500 focus:outline-none" />
                </div>

                <div class="grid grid-cols-2 gap-3">
                  <div>
                    <label class="block text-slate-400 font-semibold mb-1">Phone (E.164 or Local):</label>
                    <input
                      type="text"
                      [(ngModel)]="stitchPhone"
                      placeholder="98123456 or +216..."
                      class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-slate-100 font-mono text-xs focus:border-cyan-500 focus:outline-none" />
                  </div>
                  <div>
                    <label class="block text-slate-400 font-semibold mb-1">Email Address:</label>
                    <input
                      type="email"
                      [(ngModel)]="stitchEmail"
                      placeholder="client@ecom.tn"
                      class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-slate-100 text-xs focus:border-cyan-500 focus:outline-none" />
                  </div>
                </div>

                <div class="grid grid-cols-2 gap-3">
                  <div>
                    <label class="block text-slate-400 font-semibold mb-1">WhatsApp JID:</label>
                    <input
                      type="text"
                      [(ngModel)]="stitchWhatsApp"
                      placeholder="21698123456@s.whatsapp.net"
                      class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-slate-100 font-mono text-xs focus:border-cyan-500 focus:outline-none" />
                  </div>
                  <div>
                    <label class="block text-slate-400 font-semibold mb-1">Social Handle (IG/TikTok):</label>
                    <input
                      type="text"
                      [(ngModel)]="stitchSocial"
                      placeholder="@samir_fashion"
                      class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-slate-100 text-xs focus:border-cyan-500 focus:outline-none" />
                  </div>
                </div>

                <div>
                  <label class="block text-slate-400 font-semibold mb-1">Customer Full Name:</label>
                  <input
                    type="text"
                    [(ngModel)]="stitchName"
                    placeholder="Samir Ben Ali"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-slate-100 text-xs focus:border-cyan-500 focus:outline-none" />
                </div>

                <button
                  (click)="executeIdentityStitch()"
                  [disabled]="isStitching()"
                  class="w-full py-2.5 bg-gradient-to-r from-cyan-600 to-violet-600 hover:from-cyan-500 hover:to-violet-500 text-white font-bold rounded-xl shadow-lg shadow-cyan-950 text-xs transition flex items-center justify-center gap-2">
                  <span [class.animate-spin]="isStitching()">⚡</span>
                  <span>Execute Deterministic Identity Stitch</span>
                </button>
              </div>

              <!-- Toast / Feedback Alert -->
              @if (stitchResultFeedback(); as fb) {
                <div class="p-3 rounded-xl text-xs border"
                  [ngClass]="fb.success ? 'bg-emerald-950/60 border-emerald-800/80 text-emerald-300' : 'bg-rose-950/60 border-rose-800/80 text-rose-300'">
                  <div class="font-bold flex items-center gap-1.5">
                    <span>{{ fb.success ? '✅' : '❌' }}</span>
                    <span>{{ fb.title }}</span>
                  </div>
                  <p class="text-[11px] mt-1 text-slate-300">{{ fb.message }}</p>
                </div>
              }
            </div>

            <!-- Right: Event Stream Ingestion Simulator -->
            <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-6 space-y-4">
              <div>
                <h3 class="text-sm font-bold text-white flex items-center gap-2">
                  <span>⚡</span>
                  <span>Instant Event & Touchpoint Simulator</span>
                </h3>
                <p class="text-xs text-slate-400">
                  Inject live events into the customer stream to verify instant timeline updates and attribution tracking.
                </p>
              </div>

              <div class="grid grid-cols-2 gap-3 text-xs">
                <button
                  (click)="simulateEvent('OrderPlaced', 'Placed COD order #TN-9901 for $185.00', 'Order', 185)"
                  class="p-3 bg-slate-950 border border-slate-800 hover:border-emerald-500/50 rounded-xl text-left transition group">
                  <span class="text-base block mb-1">🛒</span>
                  <span class="font-bold text-white group-hover:text-emerald-400 block">Record Order Placed</span>
                  <span class="text-[10px] text-slate-400">Adds $185 spend & order</span>
                </button>

                <button
                  (click)="simulateEvent('VoiceCallConfirmed', 'Outbound call center confirmed address in Ariana', 'Voice', 0)"
                  class="p-3 bg-slate-950 border border-slate-800 hover:border-violet-500/50 rounded-xl text-left transition group">
                  <span class="text-base block mb-1">📞</span>
                  <span class="font-bold text-white group-hover:text-violet-400 block">Voice Call Confirmed</span>
                  <span class="text-[10px] text-slate-400">Agent dialer verification</span>
                </button>

                <button
                  (click)="simulateEvent('WhatsAppMessage', 'Inbound WhatsApp: Requested invoice copy', 'WhatsApp', 0)"
                  class="p-3 bg-slate-950 border border-slate-800 hover:border-green-500/50 rounded-xl text-left transition group">
                  <span class="text-base block mb-1">💬</span>
                  <span class="font-bold text-white group-hover:text-green-400 block">WhatsApp Message</span>
                  <span class="text-[10px] text-slate-400">Omnichannel chat event</span>
                </button>

                <button
                  (click)="simulateEvent('ProposalViewed', 'Viewed VIP Wholesale Quotation PDF', 'Proposal', 0)"
                  class="p-3 bg-slate-950 border border-slate-800 hover:border-amber-500/50 rounded-xl text-left transition group">
                  <span class="text-base block mb-1">📄</span>
                  <span class="font-bold text-white group-hover:text-amber-400 block">Proposal Viewed</span>
                  <span class="text-[10px] text-slate-400">Cryptographic audit log</span>
                </button>
              </div>

              <div class="border-t border-slate-800 pt-3">
                <span class="text-xs font-bold text-slate-300 block mb-2">Simulate Ad Touchpoint:</span>
                <div class="flex items-center gap-2">
                  <button
                    (click)="simulateTouchpoint('TikTokAds', 'Summer_COD_Blast', 35)"
                    class="flex-1 py-2 bg-slate-950 border border-slate-800 hover:border-cyan-500 text-xs font-semibold rounded-lg text-slate-200 hover:text-white transition">
                    + TikTok Ad ($35)
                  </button>
                  <button
                    (click)="simulateTouchpoint('GoogleSearch', 'Search_Brand_Protect', 20)"
                    class="flex-1 py-2 bg-slate-950 border border-slate-800 hover:border-cyan-500 text-xs font-semibold rounded-lg text-slate-200 hover:text-white transition">
                    + Google Ad ($20)
                  </button>
                  <button
                    (click)="simulateTouchpoint('MetaAds', 'Retargeting_Catalog_DPA', 45)"
                    class="flex-1 py-2 bg-slate-950 border border-slate-800 hover:border-cyan-500 text-xs font-semibold rounded-lg text-slate-200 hover:text-white transition">
                    + Meta Ad ($45)
                  </button>
                </div>
              </div>
            </div>
          </div>
        }
      </main>
    </div>
  `
})
export class Customer360CockpitComponent implements OnInit {
  private http = inject(HttpClient);

  // Reactive Signals
  isLoading = signal(false);
  isStitching = signal(false);
  activeTab = signal<'attribution' | 'timeline' | 'stitcher'>('attribution');
  selectedModelType = signal<number>(5); // 1=First, 2=Last, 3=Linear, 5=TimeDecay, 6=UShaped
  selectedCustomerId = signal<string>('cust-001');
  selectedChannelFilter = signal<string>('All');
  isChronological = signal<boolean>(false);

  // Form Inputs for Identity Stitcher
  stitchCookie = '_sg_vid_94a8b1c7';
  stitchPhone = '+21698123456';
  stitchEmail = 'samir@ecom.tn';
  stitchWhatsApp = '21698123456@s.whatsapp.net';
  stitchSocial = '@samir_fashion';
  stitchName = 'Samir Ben Ali';

  stitchResultFeedback = signal<{ success: boolean; title: string; message: string } | null>(null);

  channelFilters = ['All', 'Order', 'Voice', 'WhatsApp', 'Social', 'Email', 'Proposal', 'Support', 'Identity'];

  // Mock / Initial Data Signals
  availableCustomers = signal<CustomerProfileSummary[]>([
    {
      id: 'cust-001',
      primaryName: 'Samir Ben Ali',
      primaryPhone: '+21698123456',
      primaryEmail: 'samir@ecom.tn',
      company: 'Ben Ali Logistics SARL',
      lifecycleStage: 'Customer',
      lifetimeValue: 2450.00,
      totalOrdersCount: 6,
      totalSpent: 2450.00,
      deliverySuccessRate: 98.5,
      churnRiskLevel: 'Low',
      churnRiskScore: 12,
      createdAtUtc: new Date(Date.now() - 60 * 86400000).toISOString(),
      lastSeenAtUtc: new Date().toISOString(),
      linkedChannels: [
        { identityType: 2, channelName: 'Phone (E.164)', identityValue: '+21698123456', linkedAtUtc: new Date().toISOString(), icon: 'phone' },
        { identityType: 1, channelName: 'Email', identityValue: 'samir@ecom.tn', linkedAtUtc: new Date().toISOString(), icon: 'mail' },
        { identityType: 3, channelName: 'WhatsApp', identityValue: '21698123456@s.whatsapp.net', linkedAtUtc: new Date().toISOString(), icon: 'message-circle' },
        { identityType: 4, channelName: 'Instagram', identityValue: '@samir_fashion', linkedAtUtc: new Date().toISOString(), icon: 'instagram' },
        { identityType: 7, channelName: 'Web Cookie', identityValue: '_sg_vid_94a8b1c7', linkedAtUtc: new Date().toISOString(), icon: 'globe' }
      ]
    },
    {
      id: 'cust-002',
      primaryName: 'Amira Trabelsi',
      primaryPhone: '+21620123456',
      primaryEmail: 'amira@tech.tn',
      company: 'Trabelsi Retail',
      lifecycleStage: 'Vip',
      lifetimeValue: 4890.00,
      totalOrdersCount: 14,
      totalSpent: 4890.00,
      deliverySuccessRate: 100.0,
      churnRiskLevel: 'Low',
      churnRiskScore: 5,
      createdAtUtc: new Date(Date.now() - 120 * 86400000).toISOString(),
      lastSeenAtUtc: new Date().toISOString(),
      linkedChannels: [
        { identityType: 2, channelName: 'Phone (E.164)', identityValue: '+21620123456', linkedAtUtc: new Date().toISOString(), icon: 'phone' },
        { identityType: 1, channelName: 'Email', identityValue: 'amira@tech.tn', linkedAtUtc: new Date().toISOString(), icon: 'mail' },
        { identityType: 3, channelName: 'WhatsApp', identityValue: '21620123456@s.whatsapp.net', linkedAtUtc: new Date().toISOString(), icon: 'message-circle' },
        { identityType: 8, channelName: 'TikTok', identityValue: '@amira_official', linkedAtUtc: new Date().toISOString(), icon: 'video' }
      ]
    }
  ]);

  currentCustomer = computed(() => {
    return this.availableCustomers().find(c => c.id === this.selectedCustomerId()) ?? this.availableCustomers()[0];
  });

  timelineEvents = signal<CustomerTimelineItem[]>([
    {
      id: 'ev-1',
      unifiedCustomerId: 'cust-001',
      eventType: 'OrderPlaced',
      channel: 'Order',
      summary: 'Placed COD order #TN-8821 for $240.00 via WhatsApp Storefront',
      icon: 'shopping-cart',
      severity: 'success',
      payloadJson: '{"orderNumber": "TN-8821", "amount": 240.0, "gateway": "COD"}',
      occurredAtUtc: new Date(Date.now() - 1 * 86400000).toISOString()
    },
    {
      id: 'ev-2',
      unifiedCustomerId: 'cust-001',
      eventType: 'VoiceCallConfirmed',
      channel: 'Voice',
      summary: 'Automated AI dialer confirmed delivery address in Ariana',
      icon: 'phone',
      severity: 'accent',
      payloadJson: '{"durationSec": 42, "outcome": "AddressConfirmed", "confidence": 0.96}',
      occurredAtUtc: new Date(Date.now() - 2 * 86400000).toISOString()
    },
    {
      id: 'ev-3',
      unifiedCustomerId: 'cust-001',
      eventType: 'WhatsAppMessage',
      channel: 'WhatsApp',
      summary: 'Customer: "Can I receive this parcel before 2 PM tomorrow?"',
      icon: 'message-circle',
      severity: 'info',
      payloadJson: '{"messageId": "wamid_abc", "from": "21698123456@s.whatsapp.net"}',
      occurredAtUtc: new Date(Date.now() - 3 * 86400000).toISOString()
    },
    {
      id: 'ev-4',
      unifiedCustomerId: 'cust-001',
      eventType: 'ProposalViewed',
      channel: 'Proposal',
      summary: 'Opened B2B Wholesale Pricing Agreement PDF',
      icon: 'file-text',
      severity: 'warning',
      payloadJson: '{"documentId": "prop-991", "ipAddress": "197.1.2.3"}',
      occurredAtUtc: new Date(Date.now() - 5 * 86400000).toISOString()
    },
    {
      id: 'ev-5',
      unifiedCustomerId: 'cust-001',
      eventType: 'EmailOpened',
      channel: 'Email',
      summary: 'Opened campaign email: "Exclusive VIP Early Access"',
      icon: 'mail',
      severity: 'info',
      payloadJson: '{"campaignId": "camp-vip-1", "client": "AppleMail"}',
      occurredAtUtc: new Date(Date.now() - 8 * 86400000).toISOString()
    },
    {
      id: 'ev-6',
      unifiedCustomerId: 'cust-001',
      eventType: 'IdentityStitched',
      channel: 'Identity',
      summary: 'Stitched WebCookieId (_sg_vid_94a8b1c7) to Phone +21698123456 on checkout completion',
      icon: 'link',
      severity: 'accent',
      payloadJson: '{"cookieId": "_sg_vid_94a8b1c7", "phone": "+21698123456"}',
      occurredAtUtc: new Date(Date.now() - 14 * 86400000).toISOString()
    }
  ]);

  filteredTimeline = computed(() => {
    let list = [...this.timelineEvents()];
    const ch = this.selectedChannelFilter();
    if (ch !== 'All') {
      list = list.filter(e => e.channel.toLowerCase() === ch.toLowerCase());
    }
    list.sort((a, b) => {
      const tA = new Date(a.occurredAtUtc).getTime();
      const tB = new Date(b.occurredAtUtc).getTime();
      return this.isChronological() ? tA - tB : tB - tA;
    });
    return list;
  });

  // Multi-Touch Attribution Comparison Data
  attributionComparison = signal<{ totalRevenue: number; totalTouches: number }>({
    totalRevenue: 2450.00,
    totalTouches: 4
  });

  modelResultsMap = signal<Record<number, AttributionModelResult>>({
    1: {
      modelType: 1,
      modelName: 'First-Touch',
      description: '100% credit to initial discovery source.',
      shares: [
        { touchpointId: 'tp-1', channel: 'TikTok Ads', campaignName: 'Viral_Fall_Hook', stage: 'FirstDiscovery', weightPercent: 100, attributedRevenue: 2450.00, cost: 75.00, occurredAtUtc: new Date(Date.now() - 21 * 86400000).toISOString() },
        { touchpointId: 'tp-2', channel: 'Google Search', campaignName: 'Brand_Keyword_Exact', stage: 'LeadCreated', weightPercent: 0, attributedRevenue: 0.00, cost: 35.00, occurredAtUtc: new Date(Date.now() - 14 * 86400000).toISOString() },
        { touchpointId: 'tp-3', channel: 'Meta Retargeting', campaignName: 'Catalog_DPA_Ad', stage: 'OpportunityCreated', weightPercent: 0, attributedRevenue: 0.00, cost: 50.00, occurredAtUtc: new Date(Date.now() - 7 * 86400000).toISOString() },
        { touchpointId: 'tp-4', channel: 'WhatsApp Direct', campaignName: 'Cart_Recovery_Blast', stage: 'DealWon', weightPercent: 0, attributedRevenue: 0.00, cost: 15.00, occurredAtUtc: new Date().toISOString() }
      ],
      channelRevenueBreakdown: { 'TikTok Ads': 2450.00 },
      campaignRevenueBreakdown: { 'Viral_Fall_Hook': 2450.00 }
    },
    2: {
      modelType: 2,
      modelName: 'Last-Touch',
      description: '100% credit to final closing touchpoint.',
      shares: [
        { touchpointId: 'tp-1', channel: 'TikTok Ads', campaignName: 'Viral_Fall_Hook', stage: 'FirstDiscovery', weightPercent: 0, attributedRevenue: 0.00, cost: 75.00, occurredAtUtc: new Date(Date.now() - 21 * 86400000).toISOString() },
        { touchpointId: 'tp-2', channel: 'Google Search', campaignName: 'Brand_Keyword_Exact', stage: 'LeadCreated', weightPercent: 0, attributedRevenue: 0.00, cost: 35.00, occurredAtUtc: new Date(Date.now() - 14 * 86400000).toISOString() },
        { touchpointId: 'tp-3', channel: 'Meta Retargeting', campaignName: 'Catalog_DPA_Ad', stage: 'OpportunityCreated', weightPercent: 0, attributedRevenue: 0.00, cost: 50.00, occurredAtUtc: new Date(Date.now() - 7 * 86400000).toISOString() },
        { touchpointId: 'tp-4', channel: 'WhatsApp Direct', campaignName: 'Cart_Recovery_Blast', stage: 'DealWon', weightPercent: 100, attributedRevenue: 2450.00, cost: 15.00, occurredAtUtc: new Date().toISOString() }
      ],
      channelRevenueBreakdown: { 'WhatsApp Direct': 2450.00 },
      campaignRevenueBreakdown: { 'Cart_Recovery_Blast': 2450.00 }
    },
    3: {
      modelType: 3,
      modelName: 'Linear',
      description: 'Equal 1/N distribution across all touchpoints.',
      shares: [
        { touchpointId: 'tp-1', channel: 'TikTok Ads', campaignName: 'Viral_Fall_Hook', stage: 'FirstDiscovery', weightPercent: 25, attributedRevenue: 612.50, cost: 75.00, occurredAtUtc: new Date(Date.now() - 21 * 86400000).toISOString() },
        { touchpointId: 'tp-2', channel: 'Google Search', campaignName: 'Brand_Keyword_Exact', stage: 'LeadCreated', weightPercent: 25, attributedRevenue: 612.50, cost: 35.00, occurredAtUtc: new Date(Date.now() - 14 * 86400000).toISOString() },
        { touchpointId: 'tp-3', channel: 'Meta Retargeting', campaignName: 'Catalog_DPA_Ad', stage: 'OpportunityCreated', weightPercent: 25, attributedRevenue: 612.50, cost: 50.00, occurredAtUtc: new Date(Date.now() - 7 * 86400000).toISOString() },
        { touchpointId: 'tp-4', channel: 'WhatsApp Direct', campaignName: 'Cart_Recovery_Blast', stage: 'DealWon', weightPercent: 25, attributedRevenue: 612.50, cost: 15.00, occurredAtUtc: new Date().toISOString() }
      ],
      channelRevenueBreakdown: { 'TikTok Ads': 612.50, 'Google Search': 612.50, 'Meta Retargeting': 612.50, 'WhatsApp Direct': 612.50 },
      campaignRevenueBreakdown: { 'Viral_Fall_Hook': 612.50, 'Brand_Keyword_Exact': 612.50, 'Catalog_DPA_Ad': 612.50, 'Cart_Recovery_Blast': 612.50 }
    },
    6: {
      modelType: 6,
      modelName: 'U-Shaped',
      description: '40% First-Touch, 40% Lead/Won, 20% Middle distribution.',
      shares: [
        { touchpointId: 'tp-1', channel: 'TikTok Ads', campaignName: 'Viral_Fall_Hook', stage: 'FirstDiscovery', weightPercent: 40, attributedRevenue: 980.00, cost: 75.00, occurredAtUtc: new Date(Date.now() - 21 * 86400000).toISOString() },
        { touchpointId: 'tp-2', channel: 'Google Search', campaignName: 'Brand_Keyword_Exact', stage: 'LeadCreated', weightPercent: 10, attributedRevenue: 245.00, cost: 35.00, occurredAtUtc: new Date(Date.now() - 14 * 86400000).toISOString() },
        { touchpointId: 'tp-3', channel: 'Meta Retargeting', campaignName: 'Catalog_DPA_Ad', stage: 'OpportunityCreated', weightPercent: 10, attributedRevenue: 245.00, cost: 50.00, occurredAtUtc: new Date(Date.now() - 7 * 86400000).toISOString() },
        { touchpointId: 'tp-4', channel: 'WhatsApp Direct', campaignName: 'Cart_Recovery_Blast', stage: 'DealWon', weightPercent: 40, attributedRevenue: 980.00, cost: 15.00, occurredAtUtc: new Date().toISOString() }
      ],
      channelRevenueBreakdown: { 'TikTok Ads': 980.00, 'WhatsApp Direct': 980.00, 'Google Search': 245.00, 'Meta Retargeting': 245.00 },
      campaignRevenueBreakdown: { 'Viral_Fall_Hook': 980.00, 'Cart_Recovery_Blast': 980.00, 'Brand_Keyword_Exact': 245.00, 'Catalog_DPA_Ad': 245.00 }
    },
    5: {
      modelType: 5,
      modelName: 'Time-Decay',
      description: 'Exponential decay weighting (half-life τ = 7 days) prioritizing recent touchpoints.',
      shares: [
        { touchpointId: 'tp-1', channel: 'TikTok Ads', campaignName: 'Viral_Fall_Hook', stage: 'FirstDiscovery', weightPercent: 6.7, attributedRevenue: 164.15, cost: 75.00, occurredAtUtc: new Date(Date.now() - 21 * 86400000).toISOString() },
        { touchpointId: 'tp-2', channel: 'Google Search', campaignName: 'Brand_Keyword_Exact', stage: 'LeadCreated', weightPercent: 13.3, attributedRevenue: 325.85, cost: 35.00, occurredAtUtc: new Date(Date.now() - 14 * 86400000).toISOString() },
        { touchpointId: 'tp-3', channel: 'Meta Retargeting', campaignName: 'Catalog_DPA_Ad', stage: 'OpportunityCreated', weightPercent: 26.7, attributedRevenue: 654.15, cost: 50.00, occurredAtUtc: new Date(Date.now() - 7 * 86400000).toISOString() },
        { touchpointId: 'tp-4', channel: 'WhatsApp Direct', campaignName: 'Cart_Recovery_Blast', stage: 'DealWon', weightPercent: 53.3, attributedRevenue: 1305.85, cost: 15.00, occurredAtUtc: new Date().toISOString() }
      ],
      channelRevenueBreakdown: { 'WhatsApp Direct': 1305.85, 'Meta Retargeting': 654.15, 'Google Search': 325.85, 'TikTok Ads': 164.15 },
      campaignRevenueBreakdown: { 'Cart_Recovery_Blast': 1305.85, 'Catalog_DPA_Ad': 654.15, 'Brand_Keyword_Exact': 325.85, 'Viral_Fall_Hook': 164.15 }
    }
  });

  campaignRoas = signal<CampaignRoasItem[]>([
    { campaignName: 'Cart_Recovery_Blast', channel: 'WhatsApp Direct', totalCost: 15.00, attributedRevenue: 1305.85, trueRoas: 87.06, cac: 15.00, customerCount: 1, touchpointCount: 1 },
    { campaignName: 'Catalog_DPA_Ad', channel: 'Meta Retargeting', totalCost: 50.00, attributedRevenue: 654.15, trueRoas: 13.08, cac: 50.00, customerCount: 1, touchpointCount: 1 },
    { campaignName: 'Brand_Keyword_Exact', channel: 'Google Search', totalCost: 35.00, attributedRevenue: 325.85, trueRoas: 9.31, cac: 35.00, customerCount: 1, touchpointCount: 1 },
    { campaignName: 'Viral_Fall_Hook', channel: 'TikTok Ads', totalCost: 75.00, attributedRevenue: 164.15, trueRoas: 2.19, cac: 75.00, customerCount: 1, touchpointCount: 1 }
  ]);

  currentModelResult = computed(() => {
    return this.modelResultsMap()[this.selectedModelType()] ?? this.modelResultsMap()[5];
  });

  ngOnInit(): void {
    this.loadBackendData();
  }

  loadBackendData(): void {
    this.isLoading.set(true);

    // Call API endpoints if running
    this.http.get<{ data: any[] }>('/api/v1/crm/customer-360/customers').subscribe({
      next: (res) => {
        if (res && res.data && res.data.length > 0) {
          // Map backend customers to availableCustomers
          const mapped: CustomerProfileSummary[] = res.data.map(d => ({
            id: d.id,
            primaryName: d.primaryName,
            primaryPhone: d.primaryPhone,
            primaryEmail: d.primaryEmail,
            company: d.company,
            lifecycleStage: d.lifecycleStage === 4 ? 'Vip' : d.lifecycleStage === 3 ? 'Customer' : 'Lead',
            lifetimeValue: d.totalSpent,
            totalOrdersCount: d.totalOrdersCount,
            totalSpent: d.totalSpent,
            deliverySuccessRate: d.deliverySuccessRate,
            churnRiskLevel: d.churnRiskLevel,
            churnRiskScore: d.churnRiskScore,
            createdAtUtc: d.createdAtUtc,
            lastSeenAtUtc: d.lastSeenAtUtc,
            linkedChannels: []
          }));
          this.availableCustomers.set(mapped);
          this.selectedCustomerId.set(mapped[0].id);
          this.fetchCustomerOverview(mapped[0].id);
        }
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
      }
    });

    this.http.get<any>('/api/v1/crm/customer-360/attribution/campaign-roas').subscribe({
      next: (res) => {
        if (res && res.campaigns && res.campaigns.length > 0) {
          this.campaignRoas.set(res.campaigns);
        }
      },
      error: () => {}
    });
  }

  fetchCustomerOverview(id: string): void {
    this.http.get<any>(`/api/v1/crm/customer-360/customers/${id}`).subscribe({
      next: (res) => {
        if (res) {
          const updated = this.availableCustomers().map(c => {
            if (c.id === id) {
              return {
                ...c,
                linkedChannels: res.linkedChannels || c.linkedChannels,
                lifetimeValue: res.lifetimeValue || c.lifetimeValue,
                totalSpent: res.totalSpent || c.totalSpent,
                totalOrdersCount: res.totalOrdersCount || c.totalOrdersCount
              };
            }
            return c;
          });
          this.availableCustomers.set(updated);

          if (res.recentTimeline && res.recentTimeline.length > 0) {
            this.timelineEvents.set(res.recentTimeline);
          }
        }
      },
      error: () => {}
    });
  }

  refreshData(): void {
    this.loadBackendData();
  }

  onSelectCustomer(id: string): void {
    this.selectedCustomerId.set(id);
    this.fetchCustomerOverview(id);
  }

  toggleSort(): void {
    this.isChronological.update(v => !v);
  }

  getModelRevenue(type: number): number {
    const res = this.modelResultsMap()[type];
    return res ? res.shares.reduce((acc, s) => acc + s.attributedRevenue, 0) : 0;
  }

  getActiveModelName(): string {
    return this.currentModelResult()?.modelName ?? 'Time-Decay';
  }

  executeIdentityStitch(): void {
    this.isStitching.set(true);
    this.stitchResultFeedback.set(null);

    const payload = {
      tenantId: '00000000-0000-0000-0000-000000000001',
      cookieId: this.stitchCookie.trim(),
      phone: this.stitchPhone.trim(),
      email: this.stitchEmail.trim(),
      whatsAppJid: this.stitchWhatsApp.trim(),
      socialHandle: this.stitchSocial.trim(),
      name: this.stitchName.trim(),
      performedBy: 'Customer360Cockpit'
    };

    this.http.post<any>('/api/v1/crm/customer-360/identity/stitch', payload).subscribe({
      next: (res) => {
        this.isStitching.set(false);
        this.stitchResultFeedback.set({
          success: true,
          title: res.wasMerged ? 'Profiles Merged & Stitched' : 'Identities Stitched Successfully',
          message: res.message || `Linked channel identities to customer ${res.primaryName}.`
        });

        // Add an immediate timeline event
        const newEvent: CustomerTimelineItem = {
          id: 'ev-stitch-' + Date.now(),
          unifiedCustomerId: res.unifiedCustomerId || this.selectedCustomerId(),
          eventType: res.wasMerged ? 'ProfileMerged' : 'IdentityStitched',
          channel: 'Identity',
          summary: `Stitched ${payload.cookieId} to Phone ${payload.phone} (${payload.name})`,
          icon: 'link',
          severity: 'accent',
          payloadJson: JSON.stringify(payload),
          occurredAtUtc: new Date().toISOString()
        };
        this.timelineEvents.update(list => [newEvent, ...list]);

        // Add badge to customer
        const cust = this.currentCustomer();
        if (cust) {
          const newBadge: LinkedChannelBadge = {
            identityType: 7,
            channelName: 'Web Cookie',
            identityValue: payload.cookieId,
            linkedAtUtc: new Date().toISOString(),
            icon: 'globe'
          };
          if (!cust.linkedChannels.some(b => b.identityValue === payload.cookieId)) {
            cust.linkedChannels.push(newBadge);
          }
        }
      },
      error: (err) => {
        this.isStitching.set(false);
        // Fallback local simulation
        this.stitchResultFeedback.set({
          success: true,
          title: 'Identities Stitched (Local State Updated)',
          message: `Stitched anonymous cookie ${payload.cookieId} to ${payload.name} (${payload.phone}).`
        });

        const newEvent: CustomerTimelineItem = {
          id: 'ev-stitch-' + Date.now(),
          unifiedCustomerId: this.selectedCustomerId(),
          eventType: 'IdentityStitched',
          channel: 'Identity',
          summary: `Stitched anonymous cookie ${payload.cookieId} into ${payload.name} (${payload.phone})`,
          icon: 'link',
          severity: 'accent',
          payloadJson: JSON.stringify(payload),
          occurredAtUtc: new Date().toISOString()
        };
        this.timelineEvents.update(list => [newEvent, ...list]);
      }
    });
  }

  simulateEvent(eventType: string, summary: string, channel: string, spendDelta: number): void {
    const cust = this.currentCustomer();
    if (cust && spendDelta > 0) {
      cust.totalSpent += spendDelta;
      cust.lifetimeValue += spendDelta;
      cust.totalOrdersCount += 1;
    }

    const newEv: CustomerTimelineItem = {
      id: 'ev-sim-' + Date.now(),
      unifiedCustomerId: this.selectedCustomerId(),
      eventType,
      channel,
      summary,
      icon: this.getIconForChannel(channel),
      severity: spendDelta > 0 ? 'success' : 'info',
      payloadJson: JSON.stringify({ simulated: true, timestamp: new Date().toISOString() }),
      occurredAtUtc: new Date().toISOString()
    };
    this.timelineEvents.update(list => [newEv, ...list]);

    // Send to backend
    this.http.post('/api/v1/crm/customer-360/events', {
      tenantId: '00000000-0000-0000-0000-000000000001',
      unifiedCustomerId: this.selectedCustomerId(),
      eventType,
      summary,
      payloadJson: newEv.payloadJson
    }).subscribe({
      next: () => {},
      error: () => {}
    });
  }

  simulateTouchpoint(channel: string, campaignName: string, cost: number): void {
    const newShare: AttributedTouchpointShare = {
      touchpointId: 'tp-sim-' + Date.now(),
      channel,
      campaignName,
      stage: 'LeadCreated',
      weightPercent: 20.0,
      attributedRevenue: 245.00,
      cost,
      occurredAtUtc: new Date().toISOString()
    };

    // Update current model shares
    const currentMap = { ...this.modelResultsMap() };
    const curr = currentMap[this.selectedModelType()];
    if (curr) {
      curr.shares.push(newShare);
      this.modelResultsMap.set(currentMap);
    }

    this.simulateEvent(`Touchpoint_${channel}`, `Attributed touchpoint recorded on ${channel} (${campaignName}) - Cost: $${cost}`, 'Ad', 0);
  }

  getInitials(name: string): string {
    if (!name) return 'CU';
    const parts = name.trim().split(' ');
    return parts.length > 1 ? (parts[0][0] + parts[1][0]).toUpperCase() : name.substring(0, 2).toUpperCase();
  }

  getStagePillClass(stage: string): string {
    switch (stage?.toLowerCase()) {
      case 'vip': return 'bg-violet-500/10 text-violet-400 border-violet-500/30';
      case 'customer': return 'bg-emerald-500/10 text-emerald-400 border-emerald-500/30';
      case 'prospect': return 'bg-blue-500/10 text-blue-400 border-blue-500/30';
      default: return 'bg-slate-800 text-slate-300 border-slate-700';
    }
  }

  getChurnRiskPillClass(risk: string): string {
    switch (risk?.toLowerCase()) {
      case 'critical': return 'bg-rose-500/10 text-rose-400 border-rose-500/30';
      case 'high': return 'bg-amber-500/10 text-amber-400 border-amber-500/30';
      case 'medium': return 'bg-yellow-500/10 text-yellow-400 border-yellow-500/30';
      default: return 'bg-emerald-500/10 text-emerald-400 border-emerald-500/30';
    }
  }

  getChurnRiskDotClass(risk: string): string {
    switch (risk?.toLowerCase()) {
      case 'critical': return 'bg-rose-500 animate-pulse';
      case 'high': return 'bg-amber-500';
      case 'medium': return 'bg-yellow-400';
      default: return 'bg-emerald-400';
    }
  }

  getChannelEmoji(type: number): string {
    switch (type) {
      case 2: return '📞';
      case 1: return '✉️';
      case 3: return '💬';
      case 4: return '📷';
      case 8: return '🎬';
      case 7: return '🍪';
      default: return '🔗';
    }
  }

  getEventIconClass(severity: string): string {
    switch (severity) {
      case 'success': return 'bg-emerald-500 text-slate-950';
      case 'accent': return 'bg-cyan-500 text-slate-950';
      case 'warning': return 'bg-amber-500 text-slate-950';
      default: return 'bg-blue-500 text-white';
    }
  }

  getEventIconEmoji(eventType: string): string {
    const l = eventType.toLowerCase();
    if (l.includes('order') || l.includes('purchase')) return '🛒';
    if (l.includes('call') || l.includes('voice')) return '📞';
    if (l.includes('whatsapp')) return '💬';
    if (l.includes('proposal')) return '📄';
    if (l.includes('email')) return '✉️';
    if (l.includes('support') || l.includes('ticket')) return '🎧';
    if (l.includes('stitch') || l.includes('merge')) return '🔗';
    return '⚡';
  }

  getIconForChannel(channel: string): string {
    switch (channel.toLowerCase()) {
      case 'order': return 'shopping-cart';
      case 'voice': return 'phone';
      case 'whatsapp': return 'message-circle';
      case 'proposal': return 'file-text';
      case 'email': return 'mail';
      default: return 'activity';
    }
  }

  getChannelBadgeClass(channel: string): string {
    switch (channel?.toLowerCase()) {
      case 'order': return 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/20';
      case 'voice': return 'bg-violet-500/10 text-violet-400 border border-violet-500/20';
      case 'whatsapp': return 'bg-green-500/10 text-green-400 border border-green-500/20';
      case 'proposal': return 'bg-amber-500/10 text-amber-400 border border-amber-500/20';
      case 'email': return 'bg-cyan-500/10 text-cyan-400 border border-cyan-500/20';
      case 'support': return 'bg-rose-500/10 text-rose-400 border border-rose-500/20';
      default: return 'bg-blue-500/10 text-blue-400 border border-blue-500/20';
    }
  }

  formatDate(d: string): string {
    if (!d) return '';
    try {
      const dt = new Date(d);
      return dt.toLocaleDateString('en-US', { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
    } catch {
      return d;
    }
  }
}
