import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface CampaignPacing {
  campaignId: string;
  campaignName: string;
  platform: string;
  dailyBudgetUsd: number;
  spendToDateUsd: number;
  burnRatePercentage: number;
  currentBidMultiplier: number;
  mode: 'EvenThroughoutDay' | 'AcceleratedHighIntent' | 'ThrottledOverSpend';
  targetAccountImpressions: number;
}

@Component({
  selector: 'app-bid-pacing-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen" dir="ltr" data-testid="bid-pacing-cockpit">
      <header class="mb-6 flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-4">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-2.5 py-1 text-xs font-semibold rounded bg-sky-500/20 text-sky-400 border border-sky-500/30">Horizon 92</span>
            <span class="text-xs text-slate-400">ABM Programmatic DSP Bidding</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white mt-1">Autonomous B2B ABM Retargeting & Bid-Pacing Autopilot</h1>
          <p class="text-sm text-slate-400">Intraday budget burn rate smoothing, intent-triggered bid surges, and algorithmic over-spend prevention.</p>
        </div>
      </header>

      <!-- KPI Summary -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-daily-budget">
          <div class="text-xs font-medium text-slate-400 uppercase">Total Daily Budget</div>
          <div class="text-2xl font-bold text-white mt-1">\${{ totalDailyBudget() | number:'1.0-0' }}</div>
          <div class="text-xs text-slate-400 mt-1">Allocated across ABM DSP channels</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-spend-to-date">
          <div class="text-xs font-medium text-slate-400 uppercase">Spend to Date</div>
          <div class="text-2xl font-bold text-amber-400 mt-1">\${{ totalSpendToDate() | number:'1.0-0' }}</div>
          <div class="text-xs text-slate-400 mt-1">Intraday burn across LinkedIn & DSPs</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-pacing-efficiency">
          <div class="text-xs font-medium text-slate-400 uppercase">Pacing Efficiency</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">98.4%</div>
          <div class="text-xs text-slate-400 mt-1">Variance against target linear pace</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-throttled-campaigns">
          <div class="text-xs font-medium text-slate-400 uppercase">Throttled Campaigns</div>
          <div class="text-2xl font-bold text-rose-400 mt-1">{{ throttledCount() }}</div>
          <div class="text-xs text-slate-400 mt-1">Protected from early budget exhaust</div>
        </div>
      </section>

      <!-- Campaigns Grid -->
      <section class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div class="lg:col-span-2 space-y-4">
          <h2 class="text-lg font-semibold text-white">Campaign Bid Pacing Matrix</h2>
          <div class="space-y-3">
            @for (camp of campaigns(); track camp.campaignId) {
              <div
                class="bg-slate-800/60 border border-slate-700 p-4 rounded-xl hover:border-slate-600 transition-all cursor-pointer"
                [class.ring-2]="selectedCampaign()?.campaignId === camp.campaignId"
                [class.ring-sky-500]="selectedCampaign()?.campaignId === camp.campaignId"
                (click)="selectCampaign(camp)"
                data-testid="bid-pacing-card"
              >
                <div class="flex items-center justify-between">
                  <div>
                    <h3 class="font-semibold text-white text-base">{{ camp.campaignName }}</h3>
                    <p class="text-xs text-slate-400 mt-0.5">{{ camp.platform }} &bull; Target Impressions: {{ camp.targetAccountImpressions | number }}</p>
                  </div>
                  <div class="text-right">
                    <span class="text-xs px-2.5 py-1 rounded font-medium"
                      [class.bg-emerald-500/20]="camp.mode === 'EvenThroughoutDay'"
                      [class.text-emerald-400]="camp.mode === 'EvenThroughoutDay'"
                      [class.bg-sky-500/20]="camp.mode === 'AcceleratedHighIntent'"
                      [class.text-sky-400]="camp.mode === 'AcceleratedHighIntent'"
                      [class.bg-rose-500/20]="camp.mode === 'ThrottledOverSpend'"
                      [class.text-rose-400]="camp.mode === 'ThrottledOverSpend'"
                    >
                      {{ camp.mode }}
                    </span>
                    <div class="text-xs text-slate-300 mt-1">Multiplier: {{ camp.currentBidMultiplier | number:'1.2-2' }}x</div>
                  </div>
                </div>

                <div class="grid grid-cols-2 gap-2 mt-3 pt-3 border-t border-slate-700/60 text-xs">
                  <div>
                    <span class="text-slate-400">Budget:</span>
                    <span class="font-semibold text-white ml-1">\${{ camp.dailyBudgetUsd | number:'1.0-0' }}</span>
                  </div>
                  <div>
                    <span class="text-slate-400">Burn Rate:</span>
                    <span class="font-semibold text-amber-300 ml-1">{{ camp.burnRatePercentage }}%</span>
                  </div>
                </div>

                <div class="mt-4 flex items-center justify-between pt-2">
                  <span class="text-xs text-slate-400">Spend: \${{ camp.spendToDateUsd | number:'1.0-0' }} / \${{ camp.dailyBudgetUsd | number:'1.0-0' }}</span>
                  <button
                    (click)="boostHighIntent(camp.campaignId); $event.stopPropagation()"
                    data-testid="btn-boost-bids"
                    class="px-3 py-1 bg-sky-600 hover:bg-sky-500 text-white text-xs font-semibold rounded shadow transition-colors"
                  >
                    Boost High Intent
                  </button>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Detail Sidebar -->
        <div class="bg-slate-800/60 border border-slate-700 p-5 rounded-xl flex flex-col justify-between">
          <div>
            <h2 class="text-lg font-semibold text-white mb-3">Pacing Telemetry Detail</h2>
            @if (selectedCampaign(); as sel) {
              <div class="space-y-4">
                <div class="p-3 bg-slate-900/60 rounded-lg border border-slate-700/60">
                  <div class="text-xs text-slate-400 uppercase">Selected Campaign</div>
                  <div class="text-base font-bold text-white mt-0.5">{{ sel.campaignName }}</div>
                  <div class="text-xs text-sky-400 mt-1">Pacing Mode: {{ sel.mode }}</div>
                </div>

                <div class="space-y-2 text-sm">
                  <div class="flex justify-between py-1 border-b border-slate-700/50">
                    <span class="text-slate-400">Daily Cap:</span>
                    <span class="font-mono text-white">\${{ sel.dailyBudgetUsd | number:'1.0-0' }}</span>
                  </div>
                  <div class="flex justify-between py-1 border-b border-slate-700/50">
                    <span class="text-slate-400">Spend So Far:</span>
                    <span class="font-mono text-amber-300">\${{ sel.spendToDateUsd | number:'1.0-0' }}</span>
                  </div>
                  <div class="flex justify-between py-1 border-b border-slate-700/50">
                    <span class="text-slate-400">Active Bid Multiplier:</span>
                    <span class="font-mono text-emerald-400">{{ sel.currentBidMultiplier | number:'1.2-2' }}x</span>
                  </div>
                </div>

                <button
                  (click)="boostHighIntent(sel.campaignId)"
                  class="w-full mt-4 py-2 bg-gradient-to-r from-sky-600 to-indigo-600 hover:from-sky-500 hover:to-indigo-500 text-white font-semibold rounded text-sm shadow transition-all"
                >
                  Apply 1.35x Surge Bid Multiplier
                </button>
              </div>
            } @else {
              <p class="text-xs text-slate-400">Select a campaign to inspect pacing telemetry.</p>
            }
          </div>

          <div class="mt-6 p-3 bg-slate-900/80 rounded border border-slate-800 text-xs text-slate-400">
            <span class="text-sky-400 font-semibold">Real-Time DSP Integration:</span> Syncs bid adjustments directly via LinkedIn Marketing API and Google Ads DSP.
          </div>
        </div>
      </section>
    </div>
  `
})
export class BidPacingCockpitComponent {
  campaigns = signal<CampaignPacing[]>([
    {
      campaignId: 'camp_abm_enterprise_01',
      campaignName: 'Fortune 500 ABM Intent Surge',
      platform: 'LinkedIn Marketing',
      dailyBudgetUsd: 2500,
      spendToDateUsd: 1450,
      burnRatePercentage: 58.0,
      currentBidMultiplier: 1.25,
      mode: 'AcceleratedHighIntent',
      targetAccountImpressions: 18400
    },
    {
      campaignId: 'camp_google_high_cpc_02',
      campaignName: 'B2B Cloud Migration Intent',
      platform: 'Google Ads DSP',
      dailyBudgetUsd: 3500,
      spendToDateUsd: 3200,
      burnRatePercentage: 91.4,
      currentBidMultiplier: 0.85,
      mode: 'ThrottledOverSpend',
      targetAccountImpressions: 24100
    },
    {
      campaignId: 'camp_meta_lookalike_03',
      campaignName: 'FinTech SaaS Buying Committee',
      platform: 'Meta Ads',
      dailyBudgetUsd: 1800,
      spendToDateUsd: 900,
      burnRatePercentage: 50.0,
      currentBidMultiplier: 1.0,
      mode: 'EvenThroughoutDay',
      targetAccountImpressions: 31200
    }
  ]);

  selectedCampaign = signal<CampaignPacing | null>(this.campaigns()[0]);

  totalDailyBudget = computed(() =>
    this.campaigns().reduce((sum, c) => sum + c.dailyBudgetUsd, 0)
  );

  totalSpendToDate = computed(() =>
    this.campaigns().reduce((sum, c) => sum + c.spendToDateUsd, 0)
  );

  throttledCount = computed(() =>
    this.campaigns().filter(c => c.mode === 'ThrottledOverSpend').length
  );

  selectCampaign(campaign: CampaignPacing): void {
    this.selectedCampaign.set(campaign);
  }

  boostHighIntent(campaignId: string): void {
    this.campaigns.update(list =>
      list.map(c => {
        if (c.campaignId === campaignId) {
          return {
            ...c,
            currentBidMultiplier: 1.35,
            mode: 'AcceleratedHighIntent'
          };
        }
        return c;
      })
    );

    if (this.selectedCampaign()?.campaignId === campaignId) {
      this.selectedCampaign.update(curr => {
        if (!curr) return null;
        return {
          ...curr,
          currentBidMultiplier: 1.35,
          mode: 'AcceleratedHighIntent'
        };
      });
    }
  }
}
