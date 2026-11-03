import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface BattlecardView {
  competitor: string;
  differentiators: string[];
  proofMetric: string;
}

export interface TranscriptSnippet {
  id: string;
  speaker: string;
  text: string;
  timeSec: number;
}

@Component({
  selector: 'app-agent-assist-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="agent-assist-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-700/60 pb-6">
        <div>
          <div class="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-orange-400">
            <span class="inline-block w-2 h-2 rounded-full bg-orange-400 animate-pulse"></span>
            Real-Time Voice AI & Agent Coaching • Horizon 73
          </div>
          <h1 class="text-3xl font-extrabold text-white tracking-tight mt-1">
            Conversational Agent Assist & In-Flight Whisper Coaching
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Sub-100ms transcript parsing, semantic competitor battlecards & live talk-to-listen pacing guardrails.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="triggerSalesforceBattlecard()"
            data-testid="trigger-salesforce-btn"
            class="px-4 py-2 bg-orange-600 hover:bg-orange-500 text-white rounded-lg text-sm font-semibold transition-colors flex items-center gap-2 shadow-lg shadow-orange-600/20"
          >
            <span>⚔️</span> Competitor: Salesforce Mentioned
          </button>
        </div>
      </div>

      <!-- KPI Overview Stats -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Talk-to-Listen Ratio</div>
          <div class="text-2xl font-bold text-white mt-1" data-testid="stat-talk-ratio">
            {{ repTalkPercentage() }}% Rep / {{ 100 - repTalkPercentage() }}% Prospect
          </div>
          <div class="text-emerald-400 text-xs mt-1">Optimal Discovery Range (&lt;55%)</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Active Battlecards</div>
          <div class="text-2xl font-bold text-orange-400 mt-1" data-testid="stat-battlecards-count">
            {{ battlecards().length }} Cards
          </div>
          <div class="text-slate-400 text-xs mt-1">Semantic Match Ingested</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Whisper Guidance Latency</div>
          <div class="text-2xl font-bold text-cyan-400 mt-1" data-testid="stat-stream-latency">
            88 ms
          </div>
          <div class="text-emerald-400 text-xs mt-1">Sub-100ms Edge Inference</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Pacing Guardrail Status</div>
          <div class="text-2xl font-bold mt-1" [class]="pacingAlert() ? 'text-rose-400' : 'text-emerald-400'" data-testid="stat-pacing">
            {{ pacingAlert() ? 'Warning: Rep Over-Talking' : 'Optimal Pacing' }}
          </div>
          <div class="text-slate-400 text-xs mt-1">Active Call: Vanguard Logistics</div>
        </div>
      </div>

      <!-- Main Stage: Live Transcript vs Battlecards & Coaching -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <!-- Live Transcripts (7 cols) -->
        <div class="lg:col-span-7 bg-slate-800/80 border border-slate-700/60 rounded-xl overflow-hidden">
          <div class="p-4 border-b border-slate-700/60 flex items-center justify-between">
            <h2 class="text-lg font-bold text-white">Live Call Audio Transcript Waterfall</h2>
            <span class="text-xs text-emerald-400 font-mono">● LIVE RECORDING</span>
          </div>

          <div class="divide-y divide-slate-700/40 p-4 space-y-3">
            @for (snippet of transcripts(); track snippet.id) {
              <div class="p-3 rounded-lg border space-y-1" [class]="snippet.speaker === 'Customer' ? 'bg-slate-900 border-slate-800' : 'bg-orange-950/20 border-orange-800/40'">
                <div class="flex items-center justify-between text-xs">
                  <span class="font-bold" [class]="snippet.speaker === 'Customer' ? 'text-cyan-400' : 'text-orange-400'">
                    {{ snippet.speaker }}
                  </span>
                  <span class="text-slate-500 font-mono">{{ snippet.timeSec }}s</span>
                </div>
                <p class="text-xs text-slate-200 leading-relaxed">{{ snippet.text }}</p>
              </div>
            }
          </div>
        </div>

        <!-- Battlecards & In-Flight Whispers (5 cols) -->
        <div class="lg:col-span-5 space-y-6">
          <!-- Active Battlecards -->
          <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 space-y-4">
            <div class="flex items-center justify-between border-b border-slate-700/60 pb-3">
              <h3 class="text-base font-bold text-white">Competitor Differentiators</h3>
              <span class="text-xs text-orange-400 font-mono">{{ battlecards().length }} Active</span>
            </div>

            <div class="space-y-3">
              @for (card of battlecards(); track card.competitor) {
                <div class="p-4 bg-slate-900 rounded-xl border border-orange-900/40 space-y-2" [attr.data-testid]="'battlecard-' + card.competitor.toLowerCase()">
                  <div class="flex items-center justify-between">
                    <span class="font-bold text-white text-sm">vs {{ card.competitor }}</span>
                    <span class="text-[10px] px-2 py-0.5 rounded bg-emerald-950 text-emerald-300 border border-emerald-800 font-semibold">
                      {{ card.proofMetric }}
                    </span>
                  </div>
                  <ul class="text-xs text-slate-300 space-y-1 list-disc list-inside">
                    @for (bullet of card.differentiators; track bullet) {
                      <li class="leading-relaxed">{{ bullet }}</li>
                    }
                  </ul>
                </div>
              }
            </div>
          </div>

          <!-- Live Whisper Suggestions -->
          <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 space-y-3">
            <h3 class="text-base font-bold text-white">In-Flight Whisper Coaching</h3>
            <div class="p-3 bg-slate-900 rounded-lg border border-slate-800 text-xs text-emerald-300 space-y-1">
              <div class="font-bold text-white">Discovery Prompt:</div>
              <p class="text-slate-300">
                "Ask: 'What specific pipeline visibility gaps prompted your team to look beyond your current setup?'"
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  `
})
export class AgentAssistCockpitComponent {
  readonly repTalkPercentage = signal(40.9);
  readonly pacingAlert = signal(false);

  readonly transcripts = signal<TranscriptSnippet[]>([
    { id: 'chk_1', speaker: 'Customer', text: 'We are currently using HubSpot and evaluating Salesforce for enterprise scale.', timeSec: 12.0 },
    { id: 'chk_2', speaker: 'Representative', text: 'Understood. Many high-growth teams hit scalability hurdles when trying to integrate external agents.', timeSec: 28.0 }
  ]);

  readonly battlecards = signal<BattlecardView[]>([
    {
      competitor: 'HubSpot',
      differentiators: [
        'HubSpot lacks native multi-region zero-trust disaster recovery.',
        'SGrowing provides verifiable SHA-256 Merkle audit vaults.'
      ],
      proofMetric: '3.4x faster lead response'
    }
  ]);

  triggerSalesforceBattlecard(): void {
    const sfCard: BattlecardView = {
      competitor: 'Salesforce',
      differentiators: [
        'Salesforce requires 6-9 months implementation; SGrowing deploys in 48 hours.',
        'Zero per-seat licensing penalties; unlimited autonomous agent swarms included.'
      ],
      proofMetric: '72% lower TCO'
    };

    this.battlecards.update(list => [...list, sfCard]);
  }
}
