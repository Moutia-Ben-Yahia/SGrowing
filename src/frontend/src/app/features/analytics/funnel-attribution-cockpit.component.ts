import { Component, signal, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

export type AttributionModel = 'TimeDecay' | 'FirstTouch' | 'LastTouch' | 'Linear';

export interface ChannelAttributionUi {
  channel: string;
  touchpointsCount: number;
  attributedWeight: number; // 0 to 1
  attributedRevenue: number;
  spendCost: number;
  cac: number;
  roas: number;
}

export interface DealTouchpointJourneyUi {
  id: string;
  channel: string;
  campaignName: string;
  stage: string;
  occurredAtUtc: string;
  cost: number;
  weight: number;
}

@Component({
  selector: 'app-funnel-attribution-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Topbar -->
        <header class="h-14 border-b border-slate-800 bg-slate-900/70 backdrop-blur-md px-6 flex items-center justify-between shrink-0 sticky top-0 z-20">
          <div class="flex items-center gap-3">
            <a routerLink="/analytics" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← Analytics Studio
            </a>
            <span class="text-slate-700">|</span>
            <span class="text-base font-bold text-white flex items-center gap-2">
              📊 Multi-Touch Attribution & Funnel Analytics Cockpit
            </span>
            <span class="bg-indigo-500/20 text-indigo-300 text-[10px] font-mono px-2 py-0.5 rounded-full border border-indigo-500/30">
              Time-Decay • CAC/ROAS • Partitioned BigData
            </span>
          </div>

          <div class="flex items-center gap-3">
            <!-- Partitioning Engine Badge -->
            <div class="hidden md:flex items-center gap-1.5 px-3 py-1 rounded-lg bg-slate-900 border border-slate-800 text-[11px] font-mono text-emerald-400">
              <span class="h-2 w-2 rounded-full bg-emerald-400 animate-pulse"></span>
              PostgreSQL Partitioning Active
            </div>
            <a routerLink="/telemetry" class="text-xs text-slate-400 hover:text-slate-200 px-3 py-1.5 rounded-lg bg-slate-800/80 border border-slate-700">
              Telemetry Studio
            </a>
          </div>
        </header>

        <div class="p-6 max-w-7xl mx-auto w-full space-y-6 flex-1">
          <!-- Model Selector Bar -->
          <div class="bg-slate-900/70 border border-slate-800 rounded-2xl p-5 shadow-xl flex flex-col sm:flex-row sm:items-center justify-between gap-4">
            <div>
              <h2 class="text-xs font-bold uppercase tracking-wider text-slate-300 font-mono">
                Attribution Model Framework
              </h2>
              <p class="text-xs text-slate-400 mt-0.5">
                Switch weighting algorithm across deal revenue and customer journey touchpoints.
              </p>
            </div>

            <!-- 4 Model Buttons -->
            <div class="flex flex-wrap items-center gap-2">
              @for (m of models; track m.id) {
                <button
                  (click)="selectedModel.set(m.id)"
                  class="px-3.5 py-1.5 rounded-xl text-xs font-semibold transition border"
                  [ngClass]="selectedModel() === m.id
                    ? 'bg-indigo-600 text-white border-indigo-500 shadow-md shadow-indigo-600/25'
                    : 'bg-slate-950 text-slate-400 border-slate-800 hover:border-slate-700 hover:text-slate-200'">
                  {{ m.label }}
                </button>
              }
            </div>
          </div>

          <!-- Executive KPIs Cards -->
          <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <div class="p-5 rounded-2xl border border-slate-800 bg-slate-900/60 shadow-lg space-y-2">
              <span class="text-xs text-slate-400 block font-mono">Attributed Closed Revenue</span>
              <div class="flex items-baseline justify-between">
                <span class="text-2xl font-bold text-white">\${{ kpiTotalRevenue().toLocaleString() }}</span>
                <span class="text-xs text-emerald-400 font-mono font-semibold">+24.5% MoM</span>
              </div>
              <p class="text-[11px] text-slate-500">Across 64 closed won enterprise deals</p>
            </div>

            <div class="p-5 rounded-2xl border border-slate-800 bg-slate-900/60 shadow-lg space-y-2">
              <span class="text-xs text-slate-400 block font-mono">Blended CAC (Acquisition Cost)</span>
              <div class="flex items-baseline justify-between">
                <span class="text-2xl font-bold text-indigo-400">\${{ kpiBlendedCac() }}</span>
                <span class="text-xs text-emerald-400 font-mono font-semibold">-14.2%</span>
              </div>
              <p class="text-[11px] text-slate-500">Per acquired paying merchant</p>
            </div>

            <div class="p-5 rounded-2xl border border-slate-800 bg-slate-900/60 shadow-lg space-y-2">
              <span class="text-xs text-slate-400 block font-mono">Blended ROAS (Ad / Outbound Spend)</span>
              <div class="flex items-baseline justify-between">
                <span class="text-2xl font-bold text-emerald-400">{{ kpiBlendedRoas() }}x</span>
                <span class="text-xs text-emerald-400 font-mono font-semibold">Tier-1 Return</span>
              </div>
              <p class="text-[11px] text-slate-500">Revenue per marketing dollar spent</p>
            </div>

            <div class="p-5 rounded-2xl border border-slate-800 bg-slate-900/60 shadow-lg space-y-2">
              <span class="text-xs text-slate-400 block font-mono">Telemetry Buffer & Partitions</span>
              <div class="flex items-baseline justify-between">
                <span class="text-2xl font-bold text-white">4.2M</span>
                <span class="text-xs text-indigo-400 font-mono">100% Flush</span>
              </div>
              <p class="text-[11px] text-slate-500">Events partitioned in monthly ranges</p>
            </div>
          </div>

          <!-- Channel Attribution Breakdown & Bar Charts -->
          <div class="bg-slate-900/70 border border-slate-800 rounded-2xl p-6 shadow-xl space-y-5">
            <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-2">
              <div>
                <h3 class="text-sm font-bold text-white uppercase tracking-wider font-mono">
                  Channel Attribution Breakdown ({{ currentModelLabel() }})
                </h3>
                <p class="text-xs text-slate-400">
                  Revenue attribution weighted by {{ currentModelDescription() }}.
                </p>
              </div>
              <span class="text-xs font-mono text-slate-400">Total Spend: \$29,400</span>
            </div>

            <!-- Table of channels -->
            <div class="border border-slate-800 rounded-xl overflow-x-auto bg-slate-950/40">
              <table class="w-full text-left text-xs">
                <thead class="bg-slate-950 text-slate-400 font-mono text-[11px] border-b border-slate-800">
                  <tr>
                    <th class="p-3">Marketing Channel</th>
                    <th class="p-3">Touchpoints</th>
                    <th class="p-3">Attributed Weight</th>
                    <th class="p-3">Attributed Revenue</th>
                    <th class="p-3">Channel Spend</th>
                    <th class="p-3">Channel CAC</th>
                    <th class="p-3 text-right">ROAS</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800/60 font-mono text-[11px]">
                  @for (ch of activeChannelData(); track ch.channel) {
                    <tr class="hover:bg-slate-800/30">
                      <td class="p-3 font-bold text-white flex items-center gap-2 font-sans">
                        <span class="h-2 w-2 rounded-full bg-indigo-400"></span>
                        {{ ch.channel }}
                      </td>
                      <td class="p-3 text-slate-300">{{ ch.touchpointsCount }}</td>
                      <td class="p-3">
                        <div class="flex items-center gap-2">
                          <div class="w-16 h-2 bg-slate-800 rounded-full overflow-hidden">
                            <div class="h-full bg-indigo-500" [style.width.%]="ch.attributedWeight * 100"></div>
                          </div>
                          <span class="text-slate-300">{{ (ch.attributedWeight * 100).toFixed(1) }}%</span>
                        </div>
                      </td>
                      <td class="p-3 text-emerald-400 font-bold">\${{ ch.attributedRevenue.toLocaleString() }}</td>
                      <td class="p-3 text-slate-400">\${{ ch.spendCost.toLocaleString() }}</td>
                      <td class="p-3 text-slate-300">\${{ ch.cac }}</td>
                      <td class="p-3 text-right">
                        <span class="px-2 py-0.5 rounded font-bold"
                          [ngClass]="ch.roas >= 4.0 ? 'bg-emerald-950 text-emerald-400 border border-emerald-500/30' : 'bg-slate-800 text-slate-300'">
                          {{ ch.roas.toFixed(1) }}x
                        </span>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>

          <!-- Full Funnel Dropoff Flow -->
          <div class="bg-slate-900/70 border border-slate-800 rounded-2xl p-6 shadow-xl space-y-5">
            <div>
              <h3 class="text-sm font-bold text-white uppercase tracking-wider font-mono">
                Enterprise Revenue Funnel Conversion Dropoff
              </h3>
              <p class="text-xs text-slate-400">
                End-to-end prospect progression from anonymous clickstream visit to closed-won revenue contract.
              </p>
            </div>

            <div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-5 gap-3">
              @for (stage of funnelStages; track stage.name) {
                <div class="p-4 rounded-xl border border-slate-800 bg-slate-950/60 relative flex flex-col justify-between space-y-3">
                  <div>
                    <span class="text-[10px] font-mono uppercase text-slate-400">{{ stage.stageCode }}</span>
                    <h4 class="text-xs font-bold text-white mt-1">{{ stage.name }}</h4>
                    <div class="text-xl font-bold text-indigo-400 font-mono mt-2">{{ stage.count.toLocaleString() }}</div>
                  </div>
                  <div class="pt-2 border-t border-slate-800/80 text-[11px] font-mono flex items-center justify-between text-slate-400">
                    <span>Conv. Rate:</span>
                    <strong class="text-emerald-400">{{ stage.conversionRate }}</strong>
                  </div>
                </div>
              }
            </div>
          </div>

          <!-- Deal Journey Inspector -->
          <div class="bg-slate-900/70 border border-slate-800 rounded-2xl p-6 shadow-xl space-y-4">
            <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
              <div>
                <h3 class="text-sm font-bold text-white uppercase tracking-wider font-mono">
                  Inspect Deal Journey: "Maghreb Retail Enterprise Contract (\$18,000)"
                </h3>
                <p class="text-xs text-slate-400">
                  Touchpoint sequence leading to won deal with dynamically recalculated weights under {{ selectedModel() }}.
                </p>
              </div>
            </div>

            <div class="border border-slate-800 rounded-xl overflow-x-auto bg-slate-950/40">
              <table class="w-full text-left text-xs">
                <thead class="bg-slate-950 text-slate-400 font-mono text-[11px] border-b border-slate-800">
                  <tr>
                    <th class="p-3">Touchpoint</th>
                    <th class="p-3">Channel</th>
                    <th class="p-3">Campaign</th>
                    <th class="p-3">Funnel Stage</th>
                    <th class="p-3">Occurred At</th>
                    <th class="p-3">Cost</th>
                    <th class="p-3 text-right">Attributed Weight</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800/60 font-mono text-[11px]">
                  @for (tp of dealJourney(); track tp.id; let idx = $index) {
                    <tr class="hover:bg-slate-800/30">
                      <td class="p-3 font-bold text-slate-300">#{{ idx + 1 }}</td>
                      <td class="p-3 font-semibold text-white">{{ tp.channel }}</td>
                      <td class="p-3 text-indigo-300">{{ tp.campaignName }}</td>
                      <td class="p-3">
                        <span class="px-2 py-0.5 rounded bg-slate-800 text-slate-300 text-[10px]">
                          {{ tp.stage }}
                        </span>
                      </td>
                      <td class="p-3 text-slate-400">{{ tp.occurredAtUtc }}</td>
                      <td class="p-3 text-slate-400">\${{ tp.cost.toFixed(2) }}</td>
                      <td class="p-3 text-right">
                        <span class="text-emerald-400 font-bold">
                          {{ (calculateWeightForModel(idx, dealJourney().length) * 100).toFixed(1) }}%
                        </span>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </main>
    </div>
  `
})
export class FunnelAttributionCockpitComponent implements OnInit {
  selectedModel = signal<AttributionModel>('TimeDecay');

  models = [
    { id: 'TimeDecay' as AttributionModel, label: '⏱️ Time-Decay (7-day Half-Life)' },
    { id: 'FirstTouch' as AttributionModel, label: '🎯 First-Touch (Acquisition)' },
    { id: 'LastTouch' as AttributionModel, label: '🏁 Last-Touch (Conversion)' },
    { id: 'Linear' as AttributionModel, label: '⚖️ Linear (Equal Credit)' }
  ];

  funnelStages = [
    { stageCode: 'STAGE 1', name: 'Web & Telemetry Visits', count: 18500, conversionRate: '100%' },
    { stageCode: 'STAGE 2', name: 'Qualified Inbound Leads', count: 2840, conversionRate: '15.3%' },
    { stageCode: 'STAGE 3', name: 'Product Demo Scheduled', count: 520, conversionRate: '18.3%' },
    { stageCode: 'STAGE 4', name: 'Enterprise Deals In Flight', count: 180, conversionRate: '34.6%' },
    { stageCode: 'STAGE 5', name: 'Closed-Won Customers', count: 64, conversionRate: '35.5%' }
  ];

  kpiTotalRevenue = computed(() => {
    switch (this.selectedModel()) {
      case 'TimeDecay': return 142500;
      case 'FirstTouch': return 138000;
      case 'LastTouch': return 146000;
      case 'Linear': return 141200;
    }
  });

  kpiBlendedCac = computed(() => {
    switch (this.selectedModel()) {
      case 'TimeDecay': return 320;
      case 'FirstTouch': return 345;
      case 'LastTouch': return 310;
      case 'Linear': return 328;
    }
  });

  kpiBlendedRoas = computed(() => {
    switch (this.selectedModel()) {
      case 'TimeDecay': return 4.8;
      case 'FirstTouch': return 4.7;
      case 'LastTouch': return 5.0;
      case 'Linear': return 4.8;
    }
  });

  currentModelLabel = computed(() => {
    const m = this.models.find(x => x.id === this.selectedModel());
    return m ? m.label : 'Time-Decay';
  });

  currentModelDescription = computed(() => {
    switch (this.selectedModel()) {
      case 'TimeDecay': return 'half-life exponential decay (touchpoints closer to deal close receive higher credit)';
      case 'FirstTouch': return '100% credit granted to the initial discovery touchpoint';
      case 'LastTouch': return '100% credit granted to the final touchpoint prior to contract signing';
      case 'Linear': return 'equal distribution across every registered touchpoint in the journey';
    }
  });

  activeChannelData = computed<ChannelAttributionUi[]>(() => {
    const model = this.selectedModel();
    if (model === 'FirstTouch') {
      return [
        { channel: 'Cold Outreach (Spintax)', touchpointsCount: 420, attributedWeight: 0.45, attributedRevenue: 62100, spendCost: 8200, cac: 285, roas: 7.5 },
        { channel: 'Organic SEO & Telemetry', touchpointsCount: 310, attributedWeight: 0.30, attributedRevenue: 41400, spendCost: 3500, cac: 182, roas: 11.8 },
        { channel: 'LinkedIn Sales Navigator', touchpointsCount: 180, attributedWeight: 0.15, attributedRevenue: 20700, spendCost: 6500, cac: 680, roas: 3.1 },
        { channel: 'COD Courier Referrals', touchpointsCount: 95, attributedWeight: 0.10, attributedRevenue: 13800, spendCost: 2200, cac: 340, roas: 6.2 }
      ];
    } else if (model === 'LastTouch') {
      return [
        { channel: 'Cold Outreach (Spintax)', touchpointsCount: 420, attributedWeight: 0.20, attributedRevenue: 29200, spendCost: 8200, cac: 640, roas: 3.5 },
        { channel: 'Organic SEO & Telemetry', touchpointsCount: 310, attributedWeight: 0.15, attributedRevenue: 21900, spendCost: 3500, cac: 360, roas: 6.2 },
        { channel: 'LinkedIn Sales Navigator', touchpointsCount: 180, attributedWeight: 0.40, attributedRevenue: 58400, spendCost: 6500, cac: 250, roas: 8.9 },
        { channel: 'COD Courier Referrals', touchpointsCount: 95, attributedWeight: 0.25, attributedRevenue: 36500, spendCost: 2200, cac: 135, roas: 16.5 }
      ];
    } else if (model === 'Linear') {
      return [
        { channel: 'Cold Outreach (Spintax)', touchpointsCount: 420, attributedWeight: 0.25, attributedRevenue: 35300, spendCost: 8200, cac: 512, roas: 4.3 },
        { channel: 'Organic SEO & Telemetry', touchpointsCount: 310, attributedWeight: 0.25, attributedRevenue: 35300, spendCost: 3500, cac: 218, roas: 10.0 },
        { channel: 'LinkedIn Sales Navigator', touchpointsCount: 180, attributedWeight: 0.25, attributedRevenue: 35300, spendCost: 6500, cac: 406, roas: 5.4 },
        { channel: 'COD Courier Referrals', touchpointsCount: 95, attributedWeight: 0.25, attributedRevenue: 35300, spendCost: 2200, cac: 137, roas: 16.0 }
      ];
    } else {
      // Time-Decay (default)
      return [
        { channel: 'Cold Outreach (Spintax)', touchpointsCount: 420, attributedWeight: 0.32, attributedRevenue: 45600, spendCost: 8200, cac: 400, roas: 5.5 },
        { channel: 'LinkedIn Sales Navigator', touchpointsCount: 180, attributedWeight: 0.34, attributedRevenue: 48450, spendCost: 6500, cac: 298, roas: 7.4 },
        { channel: 'COD Courier Referrals', touchpointsCount: 95, attributedWeight: 0.21, attributedRevenue: 29925, spendCost: 2200, cac: 162, roas: 13.6 },
        { channel: 'Organic SEO & Telemetry', touchpointsCount: 310, attributedWeight: 0.13, attributedRevenue: 18525, spendCost: 3500, cac: 420, roas: 5.2 }
      ];
    }
  });

  dealJourney = signal<DealTouchpointJourneyUi[]>([
    {
      id: 'tp-1',
      channel: 'Organic SEO & Telemetry',
      campaignName: 'B2B Maghreb Logistics Whitepaper',
      stage: 'Discovery',
      occurredAtUtc: '2026-10-01 09:15:00',
      cost: 15.00,
      weight: 0.10
    },
    {
      id: 'tp-2',
      channel: 'Cold Outreach (Spintax)',
      campaignName: 'Q4 Retail Blitz Spintax Run',
      stage: 'Engagement',
      occurredAtUtc: '2026-10-08 14:20:00',
      cost: 2.50,
      weight: 0.22
    },
    {
      id: 'tp-3',
      channel: 'LinkedIn Sales Navigator',
      campaignName: 'Direct InMail with VP Commercial',
      stage: 'Evaluation',
      occurredAtUtc: '2026-10-18 11:00:00',
      cost: 45.00,
      weight: 0.31
    },
    {
      id: 'tp-4',
      channel: 'COD Courier Referrals',
      campaignName: 'Yalidine Logistics Integration Demo',
      stage: 'Closing',
      occurredAtUtc: '2026-10-24 16:45:00',
      cost: 10.00,
      weight: 0.37
    }
  ]);

  calculateWeightForModel(index: number, total: number): number {
    const model = this.selectedModel();
    if (model === 'FirstTouch') {
      return index === 0 ? 1.0 : 0.0;
    }
    if (model === 'LastTouch') {
      return index === total - 1 ? 1.0 : 0.0;
    }
    if (model === 'Linear') {
      return total > 0 ? 1.0 / total : 0;
    }
    // Time decay simulated
    if (index === 0) return 0.10;
    if (index === 1) return 0.22;
    if (index === 2) return 0.31;
    return 0.37;
  }

  ngOnInit() {}
}
