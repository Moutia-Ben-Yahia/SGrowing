import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface CompetitorProfileRecord {
  competitorId: string;
  competitorName: string;
  marketCategory: string;
  estimatedAverageDealSizeUsd: number;
  winRateAgainstPct: number;
  threatLevel: string;
}

export interface LiveBattlecardRecord {
  battlecardId: string;
  competitorName: string;
  opportunityId: string;
  executiveSummary: string;
  killPoint: string;
  digest: string;
}

@Component({
  selector: 'app-competitive-battlecard-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" data-testid="competitive-battlecard-cockpit" class="p-6 bg-slate-900 text-slate-100 min-h-screen">
      <header class="mb-8 border-b border-slate-800 pb-4 flex justify-between items-center">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-white flex items-center gap-2">
            <span class="inline-block w-3 h-3 rounded-full bg-amber-500 animate-pulse"></span>
            Competitive Win-Loss Intelligence & Live Battlecard Mesh
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Real-Time Competitive Displacement Playbooks, Real-Time Objection Kill-Points & Win Probability Recalibration
          </p>
        </div>
        <div class="flex gap-3">
          <button (click)="generateLiveBattlecard()" class="px-4 py-2 bg-amber-600 hover:bg-amber-500 text-white font-medium rounded-lg shadow-sm transition">
            Generate Dynamic Battlecard
          </button>
        </div>
      </header>

      <!-- KPI Metrics Grid -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Tracked Rivals</span>
          <p class="text-2xl font-extrabold text-white mt-1">{{ competitors().length }}</p>
          <span class="text-xs text-amber-400">Enterprise CRM & Marketing</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Aggregate Win Rate</span>
          <p class="text-2xl font-extrabold text-white mt-1">{{ aggregateWinRatePct() }}%</p>
          <span class="text-xs text-emerald-400">+14.2% YoY Improvement</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Active Battlecards</span>
          <p class="text-2xl font-extrabold text-white mt-1">{{ battlecards().length }}</p>
          <span class="text-xs text-cyan-400">Real-Time Synthesized</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Protected Pipeline</span>
          <p class="text-2xl font-extrabold text-white mt-1">$530K</p>
          <span class="text-xs text-indigo-400">Competitive Defense</span>
        </div>
      </section>

      <!-- Main Panels -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-8">
        <!-- Competitor Radar -->
        <section class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
          <h2 class="text-lg font-semibold text-white mb-4">Competitor Matrix Radar</h2>
          <div class="space-y-3">
            <div *ngFor="let c of competitors()" class="bg-slate-900/60 border border-slate-700/40 p-4 rounded-lg">
              <div class="flex justify-between items-center mb-1">
                <span class="font-bold text-white">{{ c.competitorName }}</span>
                <span class="text-xs px-2 py-0.5 rounded font-mono"
                      [ngClass]="c.threatLevel === 'HIGH' ? 'bg-rose-900/50 text-rose-300 border border-rose-700' : 'bg-amber-900/50 text-amber-300 border border-amber-700'">
                  {{ c.threatLevel }} THREAT
                </span>
              </div>
              <p class="text-xs text-slate-400 mb-2">{{ c.marketCategory }} • Avg Deal: {{ c.estimatedAverageDealSizeUsd | currency }}</p>
              <div class="flex items-center gap-2">
                <div class="flex-1 bg-slate-700 h-2 rounded-full overflow-hidden">
                  <div class="bg-emerald-500 h-full rounded-full" [style.width.%]="c.winRateAgainstPct"></div>
                </div>
                <span class="text-xs font-mono font-bold text-emerald-400">{{ c.winRateAgainstPct }}% Win Rate</span>
              </div>
            </div>
          </div>
        </section>

        <!-- Live Battlecards -->
        <section class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
          <h2 class="text-lg font-semibold text-white mb-4">Synthesized Live Battlecards</h2>
          <div class="space-y-4">
            <div *ngFor="let b of battlecards()" class="bg-slate-900/60 border border-slate-700/40 p-4 rounded-lg">
              <div class="flex justify-between items-center mb-2">
                <span class="font-bold text-amber-400">{{ b.competitorName }}</span>
                <span class="text-[10px] font-mono text-slate-500">{{ b.battlecardId }}</span>
              </div>
              <p class="text-xs font-semibold text-slate-300 mb-2">{{ b.executiveSummary }}</p>
              <div class="p-2.5 bg-amber-950/30 border border-amber-800/40 rounded text-xs text-amber-200 font-mono">
                Kill Point: {{ b.killPoint }}
              </div>
              <div class="mt-2 text-[10px] font-mono text-slate-500 truncate">
                Proof: {{ b.digest }}
              </div>
            </div>
          </div>
        </section>
      </div>
    </div>
  `
})
export class CompetitiveBattlecardCockpitComponent {
  competitors = signal<CompetitorProfileRecord[]>([
    { competitorId: 'COMP-APEX-01', competitorName: 'ApexCloud Legacy CRM', marketCategory: 'Enterprise CRM & Sales Automation', estimatedAverageDealSizeUsd: 320000, winRateAgainstPct: 68.5, threatLevel: 'HIGH' },
    { competitorId: 'COMP-TITAN-02', competitorName: 'Titanium Marketing Cloud', marketCategory: 'Marketing Automation & DSP', estimatedAverageDealSizeUsd: 210000, winRateAgainstPct: 74.2, threatLevel: 'MEDIUM' }
  ]);

  battlecards = signal<LiveBattlecardRecord[]>([
    { battlecardId: 'BTC-9911', competitorName: 'ApexCloud Legacy CRM', opportunityId: 'OPP-FIN-9988', executiveSummary: 'Multi-year lock-in with 25% price escalator displacement playbook', killPoint: 'Highlight 0-cost migration credit and native AI telephony without 3rd-party PBX taxes', digest: '0x8899AABBCCDDEEFF00112233' }
  ]);

  aggregateWinRatePct = signal(71.3);

  generateLiveBattlecard() {
    const newCard: LiveBattlecardRecord = {
      battlecardId: `BTC-${Date.now().toString().slice(-4)}`,
      competitorName: 'Titanium Marketing Cloud',
      opportunityId: 'OPP-HLT-4411',
      executiveSummary: 'Clean room attribution vs siloed legacy cookie architecture',
      killPoint: 'Demonstrate SGrowing zero-loss identity stitching graph live on customer data',
      digest: `0x${Date.now().toString(16).toUpperCase()}99AABBCC`
    };
    this.battlecards.update(list => [newCard, ...list]);
  }
}
