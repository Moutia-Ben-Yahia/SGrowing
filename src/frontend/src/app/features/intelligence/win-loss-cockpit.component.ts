import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface DealPostMortem {
  dealId: string;
  accountName: string;
  contractValueUsd: number;
  outcome: 'ClosedWon' | 'ClosedLost' | 'Stalled';
  primaryFactor: string;
  competitorName: string;
  sentimentScore: number;
  coachingRecommendation: string;
  closedAt: string;
}

@Component({
  selector: 'app-win-loss-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen" dir="ltr" data-testid="win-loss-cockpit">
      <header class="mb-6 flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-4">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-2.5 py-1 text-xs font-semibold rounded bg-indigo-500/20 text-indigo-400 border border-indigo-500/30">Horizon 76</span>
            <span class="text-xs text-slate-400">Enterprise Win-Loss Intelligence</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white mt-1">Autonomous Deal Post-Mortem Intelligence Mesh</h1>
          <p class="text-sm text-slate-400">Continuous post-close analysis, loss autopsy, competitor displacement, and pipeline coaching.</p>
        </div>
        <div class="flex items-center gap-3">
          <button (click)="runAutonomousPostMortem()" data-testid="run-autopsy-btn" class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded font-medium text-sm transition-colors shadow">
            Run Autonomous Autopsy
          </button>
        </div>
      </header>

      <!-- KPI Summary Cards -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-win-rate">
          <div class="text-xs font-medium text-slate-400 uppercase">Win Rate</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">{{ winRate() }}%</div>
          <div class="text-xs text-slate-400 mt-1">{{ wonCount() }} Won / {{ deals().length }} Analyzed</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-won-pipeline">
          <div class="text-xs font-medium text-slate-400 uppercase">Won Pipeline (USD)</div>
          <div class="text-2xl font-bold text-white mt-1">\${{ totalWonValue() | number }}</div>
          <div class="text-xs text-emerald-400 mt-1">Avg Deal: \${{ avgWonDeal() | number:'1.0-0' }}</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-lost-pipeline">
          <div class="text-xs font-medium text-slate-400 uppercase">Lost Pipeline (USD)</div>
          <div class="text-2xl font-bold text-rose-400 mt-1">\${{ totalLostValue() | number }}</div>
          <div class="text-xs text-slate-400 mt-1">{{ lostCount() }} Deals Closed Lost</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-sentiment">
          <div class="text-xs font-medium text-slate-400 uppercase">Avg Buyer Sentiment</div>
          <div class="text-2xl font-bold text-amber-400 mt-1">{{ avgSentiment() }} / 10</div>
          <div class="text-xs text-slate-400 mt-1">Extracted from call transcripts</div>
        </div>
      </section>

      <!-- Filter bar -->
      <div class="flex items-center gap-3 mb-4">
        <label class="text-xs text-slate-400">Filter Outcome:</label>
        <button (click)="filterOutcome.set('All')" [class.bg-indigo-600]="filterOutcome() === 'All'" [class.bg-slate-800]="filterOutcome() !== 'All'" data-testid="filter-all-btn" class="px-3 py-1 text-xs rounded text-slate-200">All ({{ deals().length }})</button>
        <button (click)="filterOutcome.set('ClosedWon')" [class.bg-emerald-600]="filterOutcome() === 'ClosedWon'" [class.bg-slate-800]="filterOutcome() !== 'ClosedWon'" data-testid="filter-won-btn" class="px-3 py-1 text-xs rounded text-slate-200">Closed Won ({{ wonCount() }})</button>
        <button (click)="filterOutcome.set('ClosedLost')" [class.bg-rose-600]="filterOutcome() === 'ClosedLost'" [class.bg-slate-800]="filterOutcome() !== 'ClosedLost'" data-testid="filter-lost-btn" class="px-3 py-1 text-xs rounded text-slate-200">Closed Lost ({{ lostCount() }})</button>
      </div>

      <!-- Main Layout -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Deal List -->
        <div class="lg:col-span-2 bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
          <h2 class="text-base font-semibold text-white mb-3">Enterprise Deal Post-Mortem Records</h2>
          <div class="overflow-x-auto">
            <table class="w-full text-left text-sm" data-testid="win-loss-table">
              <thead class="text-xs text-slate-400 uppercase border-b border-slate-700/60 bg-slate-800/40">
                <tr>
                  <th class="p-3">Account</th>
                  <th class="p-3">ACV</th>
                  <th class="p-3">Outcome</th>
                  <th class="p-3">Primary Factor</th>
                  <th class="p-3">Competitor</th>
                  <th class="p-3">Action</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-700/40">
                @for (deal of filteredDeals(); track deal.dealId) {
                  <tr [class.bg-slate-700/30]="selectedDeal()?.dealId === deal.dealId" (click)="selectDeal(deal)" class="hover:bg-slate-700/20 cursor-pointer" [attr.data-testid]="'deal-row-' + deal.dealId">
                    <td class="p-3 font-medium text-white">{{ deal.accountName }}</td>
                    <td class="p-3 text-slate-300">\${{ deal.contractValueUsd | number }}</td>
                    <td class="p-3">
                      <span [ngClass]="{
                        'bg-emerald-500/20 text-emerald-400 border-emerald-500/30': deal.outcome === 'ClosedWon',
                        'bg-rose-500/20 text-rose-400 border-rose-500/30': deal.outcome === 'ClosedLost',
                        'bg-amber-500/20 text-amber-400 border-amber-500/30': deal.outcome === 'Stalled'
                      }" class="px-2 py-0.5 text-xs rounded border font-medium">
                        {{ deal.outcome }}
                      </span>
                    </td>
                    <td class="p-3 text-xs text-slate-300">{{ deal.primaryFactor }}</td>
                    <td class="p-3 text-xs text-slate-400">{{ deal.competitorName }}</td>
                    <td class="p-3">
                      <button (click)="selectDeal(deal); $event.stopPropagation();" class="text-xs text-indigo-400 hover:text-indigo-300">View Dossier</button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>

        <!-- Deal Dossier Panel -->
        <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5" data-testid="deal-dossier-panel">
          <h2 class="text-base font-semibold text-white mb-2">Deal Dossier & Coaching Blueprint</h2>
          @if (selectedDeal(); as deal) {
            <div class="space-y-4">
              <div class="p-3 rounded-lg bg-slate-900/60 border border-slate-700/40">
                <div class="text-xs text-slate-400 uppercase">Selected Opportunity</div>
                <div class="text-lg font-bold text-white mt-0.5">{{ deal.accountName }}</div>
                <div class="text-xs text-slate-400">Deal ID: {{ deal.dealId }} | Closed: {{ deal.closedAt | date:'mediumDate' }}</div>
              </div>

              <div>
                <span class="text-xs font-semibold text-slate-300">Autopsy Findings:</span>
                <p class="text-xs text-slate-400 mt-1 leading-relaxed">{{ deal.primaryFactor }}</p>
              </div>

              <div class="p-3 rounded-lg bg-indigo-950/40 border border-indigo-700/30">
                <span class="text-xs font-semibold text-indigo-300">Autonomous Coaching Recommendation:</span>
                <p class="text-xs text-slate-200 mt-1 leading-relaxed" data-testid="coaching-recommendation-text">{{ deal.coachingRecommendation }}</p>
              </div>

              <div class="grid grid-cols-2 gap-2 text-xs">
                <div class="bg-slate-900/40 p-2.5 rounded border border-slate-800">
                  <span class="text-slate-400 block">Buyer Sentiment</span>
                  <span class="text-sm font-semibold text-amber-400">{{ deal.sentimentScore }} / 10.0</span>
                </div>
                <div class="bg-slate-900/40 p-2.5 rounded border border-slate-800">
                  <span class="text-slate-400 block">Incumbent / Contender</span>
                  <span class="text-sm font-semibold text-slate-200">{{ deal.competitorName }}</span>
                </div>
              </div>
            </div>
          } @else {
            <div class="p-8 text-center text-slate-500 text-sm">Select an opportunity to view its post-mortem autopsy and AI coaching blueprint.</div>
          }
        </div>
      </div>
    </div>
  `
})
export class WinLossCockpitComponent {
  deals = signal<DealPostMortem[]>([
    {
      dealId: 'deal-wl-101',
      accountName: 'Acme Global Logistics',
      contractValueUsd: 145000,
      outcome: 'ClosedWon',
      primaryFactor: 'Executive alignment and rapid security compliance audit readiness',
      competitorName: 'Salesforce Field Service',
      sentimentScore: 9.2,
      coachingRecommendation: 'Replicate multi-threaded security posture playbook in upcoming Fortune 500 logistics pipeline.',
      closedAt: '2026-10-24T14:30:00Z'
    },
    {
      dealId: 'deal-wl-102',
      accountName: 'Helios Healthcare Cloud',
      contractValueUsd: 220000,
      outcome: 'ClosedLost',
      primaryFactor: 'Lacked native HL7 FHIR bi-directional connector on day one of RFP',
      competitorName: 'Epic Systems Integration Suite',
      sentimentScore: 5.4,
      coachingRecommendation: 'Prioritize healthcare FHIR gateway integration in Sprint 18 to close the competitive product gap.',
      closedAt: '2026-10-28T09:15:00Z'
    },
    {
      dealId: 'deal-wl-103',
      accountName: 'Nordic Fintech AB',
      contractValueUsd: 85000,
      outcome: 'ClosedWon',
      primaryFactor: 'Superior API gateway semantic latency and zero-trust credential architecture',
      competitorName: 'MuleSoft AnyPoint',
      sentimentScore: 8.8,
      coachingRecommendation: 'Leverage micro-benchmark latency case study in European fintech outreach.',
      closedAt: '2026-11-01T16:00:00Z'
    }
  ]);

  filterOutcome = signal<'All' | 'ClosedWon' | 'ClosedLost'>('All');
  selectedDeal = signal<DealPostMortem | null>(this.deals()[0]);

  wonCount = computed(() => this.deals().filter(d => d.outcome === 'ClosedWon').length);
  lostCount = computed(() => this.deals().filter(d => d.outcome === 'ClosedLost').length);

  winRate = computed(() => {
    const total = this.deals().length;
    if (total === 0) return 0;
    return Math.round((this.wonCount() / total) * 100);
  });

  totalWonValue = computed(() => this.deals()
    .filter(d => d.outcome === 'ClosedWon')
    .reduce((sum, d) => sum + d.contractValueUsd, 0));

  totalLostValue = computed(() => this.deals()
    .filter(d => d.outcome === 'ClosedLost')
    .reduce((sum, d) => sum + d.contractValueUsd, 0));

  avgWonDeal = computed(() => {
    const count = this.wonCount();
    return count > 0 ? this.totalWonValue() / count : 0;
  });

  avgSentiment = computed(() => {
    const total = this.deals().length;
    if (total === 0) return 0;
    const sum = this.deals().reduce((acc, d) => acc + d.sentimentScore, 0);
    return Math.round((sum / total) * 10) / 10;
  });

  filteredDeals = computed(() => {
    const filter = this.filterOutcome();
    if (filter === 'All') return this.deals();
    return this.deals().filter(d => d.outcome === filter);
  });

  selectDeal(deal: DealPostMortem) {
    this.selectedDeal.set(deal);
  }

  runAutonomousPostMortem() {
    const newDeal: DealPostMortem = {
      dealId: `deal-wl-${Date.now().toString().slice(-4)}`,
      accountName: 'Vertex BioPharma Labs',
      contractValueUsd: 310000,
      outcome: 'ClosedWon',
      primaryFactor: 'Autonomous real-time transcription agent assist eliminated buyer onboarding friction',
      competitorName: 'Veeva Systems Vault',
      sentimentScore: 9.6,
      coachingRecommendation: 'Standardize live AI whisper coaching on all multi-million ACV clinical trials negotiations.',
      closedAt: new Date().toISOString()
    };

    this.deals.update(prev => [newDeal, ...prev]);
    this.selectedDeal.set(newDeal);
  }
}
