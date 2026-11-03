import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface AttributionChannel {
  channelId: string;
  channelName: string;
  currentSpend: number;
  firstTouchSharePct: number;
  lastTouchSharePct: number;
  shapleySharePct: number;
  attributionSkewPct: number;
  recommendedSpendShift: number;
  projectedRoasLift: number;
}

export interface TouchpointJourney {
  journeyId: string;
  accountName: string;
  dealArr: number;
  touchpoints: string[];
  closedAt: string;
}

@Component({
  selector: 'app-shapley-attribution-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen" dir="ltr" data-testid="shapley-attribution-cockpit">
      <header class="mb-6 flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-4">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-2.5 py-1 text-xs font-semibold rounded bg-cyan-500/20 text-cyan-400 border border-cyan-500/30">Horizon 97</span>
            <span class="text-xs text-slate-400">Marketing Attribution & Game-Theoretic Media Mix</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white mt-1">Autonomous Multi-Touch Attribution Shapley Value & Ad Spend Autopilot</h1>
          <p class="text-sm text-slate-400">Cooperative game theory Shapley value distribution across 2^n buyer journey touchpoints and automated spend rebalancing.</p>
        </div>
      </header>

      <!-- KPI Summary -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-pipeline-arr">
          <div class="text-xs font-medium text-slate-400 uppercase">Attributed Closed ARR</div>
          <div class="text-2xl font-bold text-white mt-1">\${{ totalPipelineArr() | number:'1.0-0' }}</div>
          <div class="text-xs text-slate-400 mt-1">Across multi-touch buyer journeys</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-marketing-spend">
          <div class="text-xs font-medium text-slate-400 uppercase">Active Marketing Spend</div>
          <div class="text-2xl font-bold text-cyan-400 mt-1">\${{ totalSpend() | number:'1.0-0' }} / mo</div>
          <div class="text-xs text-slate-400 mt-1">Across 5 integrated ad channels</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-blended-roas">
          <div class="text-xs font-medium text-slate-400 uppercase">Blended Pipeline ROAS</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">{{ blendedRoas() | number:'1.2-2' }}x</div>
          <div class="text-xs text-slate-400 mt-1">Net pipeline ARR / ad spend</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-projected-uplift">
          <div class="text-xs font-medium text-slate-400 uppercase">Projected ARR Uplift</div>
          <div class="text-2xl font-bold text-amber-400 mt-1">+\${{ projectedUplift() | number:'1.0-0' }}</div>
          <div class="text-xs text-slate-400 mt-1">Via Shapley spend redistribution</div>
        </div>
      </section>

      <!-- Attribution Models Comparison Table -->
      <section class="bg-slate-800/70 border border-slate-700/60 rounded-xl p-5 mb-6">
        <div class="flex items-center justify-between mb-4">
          <div>
            <h2 class="text-lg font-semibold text-white">Shapley Value Fair Attribution vs Heuristic Models</h2>
            <p class="text-xs text-slate-400">Highlights over-credited and under-credited channels relative to cooperative marginal yield.</p>
          </div>
          <span class="text-xs text-slate-400">5 Channels Audited</span>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full text-left text-sm" data-testid="attribution-table">
            <thead class="text-xs uppercase bg-slate-800 text-slate-400 border-b border-slate-700">
              <tr>
                <th class="py-3 px-4">Channel</th>
                <th class="py-3 px-4">Monthly Spend</th>
                <th class="py-3 px-4">First-Touch</th>
                <th class="py-3 px-4">Last-Touch</th>
                <th class="py-3 px-4">Shapley Fair Value</th>
                <th class="py-3 px-4">Attribution Skew</th>
                <th class="py-3 px-4">Rebalance Shift</th>
                <th class="py-3 px-4 text-right">Action</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-750 text-slate-300">
              @for (c of channels(); track c.channelId) {
                <tr class="hover:bg-slate-800/50 transition-colors" [attr.data-testid]="'channel-row-' + c.channelId">
                  <td class="py-3 px-4">
                    <div class="font-medium text-white">{{ c.channelName }}</div>
                    <div class="text-xs text-slate-400 font-mono">{{ c.channelId }}</div>
                  </td>
                  <td class="py-3 px-4 font-semibold text-white">
                    \${{ c.currentSpend | number:'1.0-0' }}
                  </td>
                  <td class="py-3 px-4 text-xs font-mono text-slate-400">
                    {{ c.firstTouchSharePct | number:'1.1-1' }}%
                  </td>
                  <td class="py-3 px-4 text-xs font-mono text-slate-400">
                    {{ c.lastTouchSharePct | number:'1.1-1' }}%
                  </td>
                  <td class="py-3 px-4">
                    <span class="px-2 py-0.5 rounded text-xs font-bold bg-cyan-500/20 text-cyan-300 font-mono">
                      {{ c.shapleySharePct | number:'1.1-1' }}%
                    </span>
                  </td>
                  <td class="py-3 px-4">
                    <span class="px-2 py-0.5 rounded text-xs font-semibold"
                          [ngClass]="{
                            'bg-emerald-500/20 text-emerald-300': c.attributionSkewPct > 5,
                            'bg-rose-500/20 text-rose-300': c.attributionSkewPct < -5,
                            'bg-slate-700 text-slate-300': c.attributionSkewPct >= -5 && c.attributionSkewPct <= 5
                          }">
                      {{ c.attributionSkewPct > 0 ? '+' : '' }}{{ c.attributionSkewPct | number:'1.1-1' }}%
                    </span>
                  </td>
                  <td class="py-3 px-4">
                    <div class="font-mono text-xs font-semibold"
                         [ngClass]="c.recommendedSpendShift > 0 ? 'text-emerald-400' : (c.recommendedSpendShift < 0 ? 'text-rose-400' : 'text-slate-400')">
                      {{ c.recommendedSpendShift > 0 ? '+' : '' }}\${{ c.recommendedSpendShift | number:'1.0-0' }}
                    </div>
                  </td>
                  <td class="py-3 px-4 text-right">
                    <button class="px-3 py-1 text-xs font-semibold rounded bg-cyan-600 hover:bg-cyan-500 text-white transition-colors"
                            (click)="rebalance(c.channelId)"
                            [attr.data-testid]="'btn-rebalance-' + c.channelId">
                      Auto-Shift
                    </button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <!-- Action Toast -->
      @if (rebalanceLog()) {
        <div class="p-3 bg-cyan-950/60 border border-cyan-500/40 rounded-xl text-cyan-300 text-xs flex items-center justify-between"
             data-testid="rebalance-notice">
          <span>{{ rebalanceLog() }}</span>
          <button (click)="rebalanceLog.set(null)" class="text-slate-400 hover:text-white">&times;</button>
        </div>
      }
    </div>
  `
})
export class ShapleyAttributionCockpitComponent {
  readonly channels = signal<AttributionChannel[]>([
    {
      channelId: 'Webinars',
      channelName: 'Executive Interactive Webinars',
      currentSpend: 45000,
      firstTouchSharePct: 6.2,
      lastTouchSharePct: 8.5,
      shapleySharePct: 29.4,
      attributionSkewPct: 20.9,
      recommendedSpendShift: 35000,
      projectedRoasLift: 2.8
    },
    {
      channelId: 'SocialRetargeting',
      channelName: 'LinkedIn & Meta ABM Retargeting',
      currentSpend: 85000,
      firstTouchSharePct: 12.0,
      lastTouchSharePct: 41.5,
      shapleySharePct: 18.2,
      attributionSkewPct: -23.3,
      recommendedSpendShift: -30000,
      projectedRoasLift: 1.4
    },
    {
      channelId: 'ContentSeo',
      channelName: 'High-Intent Product SEO & Whitepapers',
      currentSpend: 30000,
      firstTouchSharePct: 38.0,
      lastTouchSharePct: 11.2,
      shapleySharePct: 22.1,
      attributionSkewPct: 10.9,
      recommendedSpendShift: 15000,
      projectedRoasLift: 2.1
    },
    {
      channelId: 'PaidSearch',
      channelName: 'Google Search (High Intent Brand & Non-Brand)',
      currentSpend: 60000,
      firstTouchSharePct: 32.5,
      lastTouchSharePct: 24.8,
      shapleySharePct: 16.5,
      attributionSkewPct: -8.3,
      recommendedSpendShift: -12000,
      projectedRoasLift: 1.1
    },
    {
      channelId: 'PartnerEcosystem',
      channelName: 'Co-Sell Cloud Marketplace Partners',
      currentSpend: 25000,
      firstTouchSharePct: 11.3,
      lastTouchSharePct: 14.0,
      shapleySharePct: 13.8,
      attributionSkewPct: -0.2,
      recommendedSpendShift: 5000,
      projectedRoasLift: 1.9
    }
  ]);

  readonly rebalanceLog = signal<string | null>(null);

  readonly totalPipelineArr = signal<number>(1785000);

  readonly totalSpend = computed(() =>
    this.channels().reduce((sum, c) => sum + c.currentSpend, 0)
  );

  readonly blendedRoas = computed(() => {
    const spend = this.totalSpend();
    return spend > 0 ? this.totalPipelineArr() / spend : 0;
  });

  readonly projectedUplift = computed(() =>
    Math.round(this.totalPipelineArr() * 0.185)
  );

  rebalance(channelId: string): void {
    const channel = this.channels().find(c => c.channelId === channelId);
    if (!channel) return;

    const shift = channel.recommendedSpendShift;
    const newSpend = Math.max(5000, channel.currentSpend + shift);

    this.channels.update(list =>
      list.map(c => c.channelId === channelId ? {
        ...c,
        currentSpend: newSpend,
        recommendedSpendShift: 0
      } : c)
    );

    this.rebalanceLog.set(`Autopilot: Reallocated \$${Math.abs(shift).toLocaleString()} on ${channel.channelName}. New monthly spend: \$${newSpend.toLocaleString()}.`);
  }
}
