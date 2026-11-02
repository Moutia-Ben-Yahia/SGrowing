import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface AdCampaignChannel {
  campaignId: string;
  campaignName: string;
  channel: string;
  dailyBudget: number;
  currentSpend: number;
  targetCpa: number;
  currentCpa: number;
  roas: number;
  status: 'Active' | 'Optimized' | 'Throttled' | 'AnomalyPaused';
}

export interface BudgetRebalanceRecommendation {
  sourceCampaignId: string;
  targetCampaignId: string;
  reallocatedAmount: number;
  expectedRoasLiftPercent: number;
  reasoning: string;
}

@Component({
  selector: 'app-ad-spend-autopilot-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="ad-spend-autopilot-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-700/60 pb-6">
        <div>
          <div class="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-violet-400">
            <span class="inline-block w-2 h-2 rounded-full bg-violet-400 animate-pulse"></span>
            Programmatic Advertising & ROAS • Horizon 52
          </div>
          <h1 class="text-3xl font-extrabold text-white tracking-tight mt-1">
            Programmatic Ad Spend Autopilot & Bid Shading
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Cross-channel budget re-allocation based on marginal ROAS, first-price bid shading & autonomous CPA anomaly killswitches.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="simulateBidShading()"
            data-testid="execute-bid-shading-btn"
            class="px-4 py-2 bg-violet-600 hover:bg-violet-500 text-white rounded-lg text-sm font-semibold transition-colors flex items-center gap-2 shadow-lg shadow-violet-600/20"
          >
            <span>📉</span> Run Bid Shading
          </button>
        </div>
      </div>

      <!-- KPI Overview Stats -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Total Managed Daily Budget</div>
          <div class="text-2xl font-bold text-white mt-1" data-testid="stat-daily-budget">
            \${{ totalDailyBudget() | number:'1.0-0' }}
          </div>
          <div class="text-violet-400 text-xs mt-1">Google • Meta • TikTok</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Bid Shading Savings</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1" data-testid="stat-shading-savings">
            \${{ bidShadingSavings() | number:'1.0-0' }}
          </div>
          <div class="text-slate-400 text-xs mt-1">16.4% Avg Shave on 1st-Price Win</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Blended ROAS</div>
          <div class="text-2xl font-bold text-violet-400 mt-1" data-testid="stat-blended-roas">{{ blendedRoas() }}x</div>
          <div class="text-emerald-400 text-xs mt-1">+14.2% Autopilot Optimization Lift</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Anomaly Killswitch</div>
          <div class="text-2xl font-bold text-rose-400 mt-1" data-testid="stat-anomaly-count">{{ anomalyCount() }} Paused</div>
          <div class="text-slate-400 text-xs mt-1">CPA Spikes Quarantined</div>
        </div>
      </div>

      <!-- Main Stage: Campaigns & Autopilot Rebalance Console -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <!-- Campaigns List (7 cols) -->
        <div class="lg:col-span-7 bg-slate-800/80 border border-slate-700/60 rounded-xl overflow-hidden">
          <div class="p-4 border-b border-slate-700/60 flex items-center justify-between">
            <h2 class="text-lg font-bold text-white">Cross-Channel Active Campaigns</h2>
            <span class="text-xs text-slate-400">{{ campaigns().length }} Campaigns Managed</span>
          </div>

          <div class="divide-y divide-slate-700/40">
            @for (camp of campaigns(); track camp.campaignId) {
              <div
                (click)="selectCampaign(camp)"
                [class.bg-slate-700/40]="selectedCampaign()?.campaignId === camp.campaignId"
                class="p-4 hover:bg-slate-700/30 transition-colors cursor-pointer flex flex-col md:flex-row md:items-center justify-between gap-3"
                [attr.data-testid]="'campaign-row-' + camp.campaignId"
              >
                <div>
                  <div class="flex items-center gap-2">
                    <span class="font-bold text-white text-sm">{{ camp.campaignName }}</span>
                    <span
                      [class]="getStatusBadgeClass(camp.status)"
                      class="px-2 py-0.5 rounded text-[10px] font-semibold border uppercase"
                      [attr.data-testid]="'status-badge-' + camp.campaignId"
                    >
                      {{ camp.status }}
                    </span>
                  </div>
                  <div class="text-xs text-slate-400 mt-1">
                    Channel: <span class="text-slate-200 font-medium">{{ camp.channel }}</span> • Daily Budget: <strong class="text-white">\${{ camp.dailyBudget | number:'1.0-0' }}</strong>
                  </div>
                  <div class="text-xs text-slate-400 mt-0.5">
                    Target CPA: \${{ camp.targetCpa | number:'1.2-2' }} • Current CPA:
                    <span [class]="camp.currentCpa > camp.targetCpa * 1.5 ? 'text-rose-400 font-bold' : 'text-emerald-400 font-medium'">
                      \${{ camp.currentCpa | number:'1.2-2' }}
                    </span>
                  </div>
                </div>

                <div class="text-right self-end md:self-auto">
                  <div class="text-xs text-slate-400">Attributed ROAS</div>
                  <div
                    [class]="camp.roas >= 4.0 ? 'text-emerald-400' : (camp.roas >= 2.5 ? 'text-violet-400' : 'text-rose-400')"
                    class="text-base font-extrabold"
                  >
                    {{ camp.roas }}x
                  </div>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Autopilot Optimization & Rebalance Recommendation (5 cols) -->
        <div class="lg:col-span-5 bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 space-y-4">
          <div class="flex items-center justify-between border-b border-slate-700/60 pb-3">
            <div>
              <h3 class="font-bold text-white text-base">Marginal ROAS Rebalance</h3>
              <p class="text-xs text-slate-400">Autonomous Budget Shift Engine</p>
            </div>
            <span class="text-xs px-2.5 py-1 rounded bg-violet-950 text-violet-300 border border-violet-800">
              Live Rebalancing
            </span>
          </div>

          @if (recommendation(); as rec) {
            <div class="p-3.5 bg-slate-900 rounded-xl border border-slate-800 space-y-3">
              <div class="flex items-center justify-between text-xs">
                <span class="text-slate-400">Shift Capital:</span>
                <span class="text-emerald-400 font-bold text-sm">\${{ rec.reallocatedAmount | number:'1.0-0' }} / day</span>
              </div>

              <div class="p-2.5 bg-slate-950 rounded-lg text-xs text-slate-300 border border-slate-800/80 leading-relaxed">
                {{ rec.reasoning }}
              </div>

              <div class="flex items-center justify-between text-xs pt-1">
                <div class="text-violet-400 font-semibold">+{{ rec.expectedRoasLiftPercent }}% Projected Lift</div>
                <button
                  (click)="executeRebalance(rec)"
                  data-testid="execute-rebalance-btn"
                  class="px-3.5 py-1.5 bg-violet-600 hover:bg-violet-500 text-white rounded-lg text-xs font-semibold shadow transition-colors"
                >
                  Apply Autonomous Shift
                </button>
              </div>
            </div>
          } @else {
            <div class="p-4 text-center text-xs text-slate-400 bg-slate-900 rounded-xl border border-slate-800">
              Budgets currently balanced at optimal Pareto efficiency.
            </div>
          }

          <!-- Bid Shading Simulator Console -->
          <div class="pt-3 border-t border-slate-700/60 space-y-2">
            <div class="text-xs font-bold text-slate-300">Bid Shading Telemetry Monitor:</div>
            <div class="p-3 bg-slate-900 rounded-xl border border-slate-800 space-y-1.5 text-xs">
              <div class="flex items-center justify-between text-slate-400">
                <span>Raw First-Price Bid:</span>
                <span class="text-white font-mono">\${{ simulatedRawBid.toFixed(2) }}</span>
              </div>
              <div class="flex items-center justify-between text-slate-400">
                <span>Autonomous Shaded Bid:</span>
                <span class="text-emerald-400 font-mono font-bold">\${{ simulatedShadedBid.toFixed(2) }} (-16%)</span>
              </div>
              <div class="flex items-center justify-between text-slate-400">
                <span>Predicted Win Rate:</span>
                <span class="text-violet-400 font-mono">84.5%</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  `
})
export class AdSpendAutopilotCockpitComponent {
  readonly campaigns = signal<AdCampaignChannel[]>([
    {
      campaignId: 'cmp_google_pmax',
      campaignName: 'Enterprise Tier 1 Google Search PMax',
      channel: 'GoogleSearch',
      dailyBudget: 3500,
      currentSpend: 2840,
      targetCpa: 45,
      currentCpa: 38.5,
      roas: 4.82,
      status: 'Optimized'
    },
    {
      campaignId: 'cmp_meta_retargeting',
      campaignName: 'Meta DPA Mid-Funnel Retargeting',
      channel: 'MetaFeed',
      dailyBudget: 2200,
      currentSpend: 1950,
      targetCpa: 55,
      currentCpa: 52.1,
      roas: 3.45,
      status: 'Active'
    },
    {
      campaignId: 'cmp_tiktok_topview',
      campaignName: 'TikTok Gen-Z Viral Creator Sparks',
      channel: 'TikTokVideo',
      dailyBudget: 1500,
      currentSpend: 1480,
      targetCpa: 30,
      currentCpa: 68.2,
      roas: 1.15,
      status: 'AnomalyPaused'
    }
  ]);

  readonly selectedCampaign = signal<AdCampaignChannel | null>(this.campaigns()[0]);
  readonly bidShadingSavings = signal(14820);

  simulatedRawBid = 4.50;
  simulatedShadedBid = 3.78;

  readonly recommendation = signal<BudgetRebalanceRecommendation | null>({
    sourceCampaignId: 'cmp_meta_retargeting',
    targetCampaignId: 'cmp_google_pmax',
    reallocatedAmount: 500,
    expectedRoasLiftPercent: 14.8,
    reasoning: 'Shift $500 daily budget from Meta Retargeting (ROAS 3.45x) to Google PMax (ROAS 4.82x).'
  });

  readonly totalDailyBudget = computed(() =>
    this.campaigns().reduce((acc, c) => acc + c.dailyBudget, 0)
  );

  readonly blendedRoas = computed(() => {
    const list = this.campaigns();
    if (!list.length) return '0.00';
    return (list.reduce((acc, c) => acc + c.roas, 0) / list.length).toFixed(2);
  });

  readonly anomalyCount = computed(() =>
    this.campaigns().filter(c => c.status === 'AnomalyPaused').length
  );

  selectCampaign(camp: AdCampaignChannel): void {
    this.selectedCampaign.set(camp);
  }

  executeRebalance(rec: BudgetRebalanceRecommendation): void {
    this.campaigns.update(list =>
      list.map(c => {
        if (c.campaignId === rec.sourceCampaignId) {
          return { ...c, dailyBudget: c.dailyBudget - rec.reallocatedAmount };
        }
        if (c.campaignId === rec.targetCampaignId) {
          return { ...c, dailyBudget: c.dailyBudget + rec.reallocatedAmount, status: 'Optimized' };
        }
        return c;
      })
    );
    this.recommendation.set(null);
  }

  simulateBidShading(): void {
    this.simulatedRawBid += 0.50;
    this.simulatedShadedBid = Math.round(this.simulatedRawBid * 0.84 * 100) / 100;
    this.bidShadingSavings.update(v => v + 1200);
  }

  getStatusBadgeClass(status: string): string {
    switch (status) {
      case 'Optimized':
        return 'bg-emerald-900/80 text-emerald-300 border-emerald-700';
      case 'Active':
        return 'bg-blue-900/80 text-blue-300 border-blue-700';
      case 'AnomalyPaused':
        return 'bg-rose-900/80 text-rose-300 border-rose-700';
      default:
        return 'bg-slate-700 text-slate-300 border-slate-600';
    }
  }
}
