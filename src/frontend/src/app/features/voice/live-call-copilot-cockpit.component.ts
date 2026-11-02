import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface UtteranceUi {
  id: string;
  speaker: 'Agent' | 'Prospect' | 'System';
  text: string;
  sentiment: number;
  time: string;
}

export interface BattlecardUi {
  id: string;
  category: string;
  keyword: string;
  talkingPoints: string;
  proofPoint: string;
}

@Component({
  selector: 'app-live-call-copilot-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="live-call-copilot-cockpit p-6 max-w-7xl mx-auto space-y-6" dir="ltr">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b pb-6">
        <div>
          <div class="flex items-center gap-3">
            <h1 class="text-2xl font-bold tracking-tight text-slate-900">Real-Time Voice Call Diarization & In-Call Copilot</h1>
            <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-100 text-emerald-800 animate-pulse">
              ● Live Dual-Channel Stream
            </span>
          </div>
          <p class="text-sm text-slate-500 mt-1">
            Real-time speech-to-text separation, in-flight sentiment trajectory telemetry, and instant battlecard objection triggers.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button 
            (click)="endAndSummarizeCall()"
            class="px-4 py-2 bg-rose-600 hover:bg-rose-700 text-white rounded-lg font-medium text-xs transition-colors shadow-sm"
            data-testid="btn-end-call">
            End Call & Auto-Summarize
          </button>
        </div>
      </div>

      <!-- Live Stream KPIs -->
      <div class="grid grid-cols-1 sm:grid-cols-4 gap-4" data-testid="copilot-kpis">
        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Active Call Prospect</div>
          <div class="text-lg font-bold text-slate-900 mt-1 truncate" data-testid="kpi-prospect-company">{{ prospectCompany() }}</div>
          <div class="text-[11px] text-slate-400 mt-1 font-mono">+1-415-555-0199</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">In-Flight Sentiment Trajectory</div>
          <div class="text-2xl font-bold mt-1 font-mono" 
            [ngClass]="averageSentiment() >= 0 ? 'text-emerald-600' : 'text-amber-600'"
            data-testid="kpi-sentiment">
            {{ averageSentiment() >= 0 ? '+' : '' }}{{ averageSentiment() }}
          </div>
          <div class="text-[11px] text-slate-400 mt-1">Lexical & Prosodic Scoring</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Stream Latency</div>
          <div class="text-2xl font-bold text-indigo-600 mt-1 font-mono">&lt; 140ms</div>
          <div class="text-[11px] text-slate-400 mt-1">Duplex WebRTC Opus 48kHz</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Surfaced Battlecards</div>
          <div class="text-2xl font-bold text-slate-900 mt-1 font-mono" data-testid="kpi-battlecards-count">{{ activeBattlecards().length }}</div>
          <div class="text-[11px] text-emerald-600 mt-1 font-semibold">Sub-250ms RAG Lookups</div>
        </div>
      </div>

      <!-- Main Diarized Split View -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Live Diarized Utterances Stream -->
        <div class="lg:col-span-2 bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <div class="flex items-center justify-between">
            <h3 class="font-bold text-slate-900 text-sm">Dual-Channel Diarized Audio Transcript</h3>
            <span class="text-xs text-slate-400 font-mono">{{ utterances().length }} utterances</span>
          </div>

          <div class="space-y-3 max-h-[420px] overflow-y-auto pr-2" data-testid="transcript-feed">
            @for (utt of utterances(); track utt.id) {
              <div class="p-3 rounded-lg text-xs space-y-1"
                [ngClass]="utt.speaker === 'Agent' ? 'bg-indigo-50/70 border border-indigo-100 ml-8' : 'bg-slate-50 border border-slate-200 mr-8'">
                <div class="flex justify-between items-center text-[11px]">
                  <span class="font-bold" [ngClass]="utt.speaker === 'Agent' ? 'text-indigo-700' : 'text-slate-800'">
                    {{ utt.speaker === 'Agent' ? 'Rep (Sarah Connor)' : 'Prospect (David)' }}
                  </span>
                  <div class="flex items-center gap-2">
                    <span class="font-mono text-slate-400">{{ utt.time }}</span>
                    <span class="px-1.5 py-0.2 rounded text-[10px] font-mono font-bold"
                      [ngClass]="utt.sentiment >= 0 ? 'bg-emerald-100 text-emerald-800' : 'bg-rose-100 text-rose-800'">
                      {{ utt.sentiment >= 0 ? '+' : '' }}{{ utt.sentiment }}
                    </span>
                  </div>
                </div>
                <p class="text-slate-700 leading-relaxed">{{ utt.text }}</p>
              </div>
            }
          </div>

          <!-- Live Utterance Simulation Input -->
          <div class="pt-3 border-t space-y-2">
            <label class="block text-xs font-semibold text-slate-700">Simulate Live Prospect Utterance</label>
            <div class="flex gap-2">
              <input 
                type="text" 
                [(ngModel)]="simulatedUtterance" 
                placeholder="e.g. Migrating from HubSpot seems expensive..."
                class="w-full px-3 py-2 border rounded-lg text-xs focus:ring-2 focus:ring-emerald-500 focus:outline-none"
                data-testid="input-simulate-utterance" />
              <button 
                (click)="sendUtterance()"
                class="px-4 py-2 bg-slate-900 hover:bg-slate-800 text-white rounded-lg text-xs font-medium whitespace-nowrap shadow-sm"
                data-testid="btn-send-utterance">
                Stream Utterance
              </button>
            </div>
          </div>
        </div>

        <!-- In-Flight Battlecards & AI Cue Cards -->
        <div class="bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <div class="flex items-center justify-between">
            <h3 class="font-bold text-slate-900 text-sm">Live Objection Battlecards</h3>
            <span class="text-xs text-emerald-600 font-semibold">Auto-Surfacing</span>
          </div>
          <p class="text-xs text-slate-500">
            Real-time RAG recommendations triggered by detected keywords and customer hesitation signals.
          </p>

          <div class="space-y-3" data-testid="battlecards-list">
            @for (card of activeBattlecards(); track card.id) {
              <div class="p-3.5 bg-amber-50/60 border border-amber-200 rounded-xl space-y-2">
                <div class="flex justify-between items-center text-xs">
                  <span class="font-bold text-amber-900">{{ card.category }}</span>
                  <span class="px-1.5 py-0.5 rounded text-[10px] font-mono font-bold bg-amber-100 text-amber-800">
                    Keyword: {{ card.keyword }}
                  </span>
                </div>
                <p class="text-xs text-slate-700 leading-normal">{{ card.talkingPoints }}</p>
                <div class="pt-1.5 border-t border-amber-200/60 text-[11px] text-amber-800 font-medium">
                  Proof Point: {{ card.proofPoint }}
                </div>
              </div>
            }
          </div>

          @if (callSummary()) {
            <div class="p-4 bg-emerald-50 border border-emerald-200 rounded-xl space-y-2" data-testid="call-summary-card">
              <div class="font-bold text-emerald-900 text-xs">Post-Call AI Summary & CRM Action Items</div>
              <p class="text-xs text-slate-700">{{ callSummary()?.summary }}</p>
              <ul class="text-[11px] text-emerald-800 list-disc list-inside space-y-0.5 font-medium">
                @for (item of callSummary()?.actionItems; track item) {
                  <li>{{ item }}</li>
                }
              </ul>
            </div>
          }
        </div>
      </div>
    </div>
  `
})
export class LiveCallCopilotCockpitComponent {
  prospectCompany = signal('Nexus Innovations Inc');

  utterances = signal<UtteranceUi[]>([
    { id: 'u1', speaker: 'Agent', text: 'Hello David, thanks for joining! How are your outbound campaigns pacing this quarter?', sentiment: 0.45, time: '14:02:10' },
    { id: 'u2', speaker: 'Prospect', text: "We're struggling with response rates and our SDR budget is tight this quarter.", sentiment: -0.35, time: '14:02:18' },
    { id: 'u3', speaker: 'Agent', text: "I understand completely. That's why we built autonomous multi-channel warmup and voice dialers.", sentiment: 0.60, time: '14:02:26' }
  ]);

  activeBattlecards = signal<BattlecardUi[]>([
    {
      id: 'bc_pricing',
      category: 'Budget Caution',
      keyword: 'budget',
      talkingPoints: 'Highlight that SGrowing replaces 5 separate SaaS tools, yielding 340% median ROI within 90 days.',
      proofPoint: 'Median cost reduction of $48,000/yr per sales team'
    }
  ]);

  averageSentiment = computed(() => {
    const list = this.utterances();
    if (!list.length) return 0;
    const avg = list.reduce((sum, u) => sum + u.sentiment, 0) / list.length;
    return Math.round(avg * 100) / 100;
  });

  simulatedUtterance = "We're considering HubSpot, but the legacy plugins are frustrating.";
  callSummary = signal<{ summary: string; actionItems: string[] } | null>(null);

  sendUtterance() {
    const text = this.simulatedUtterance;
    const isNegative = text.includes('expensive') || text.includes('frustrating') || text.includes('struggling');
    const sentiment = isNegative ? -0.50 : 0.40;

    const newUtt: UtteranceUi = {
      id: 'u_' + Date.now(),
      speaker: 'Prospect',
      text,
      sentiment,
      time: '14:03:00'
    };

    this.utterances.update(list => [...list, newUtt]);

    if (text.toLowerCase().includes('hubspot')) {
      const competitorCard: BattlecardUi = {
        id: 'bc_hubspot',
        category: 'Legacy CRM Sprawl',
        keyword: 'hubspot',
        talkingPoints: "Emphasize native WebRTC voice SDRs and sub-150ms real-time audio versus HubSpot's third-party plugin delays.",
        proofPoint: 'Zero-latency two-way sync vs 15-minute polling delays'
      };
      if (!this.activeBattlecards().some(c => c.id === 'bc_hubspot')) {
        this.activeBattlecards.update(cards => [...cards, competitorCard]);
      }
    }
  }

  endAndSummarizeCall() {
    this.callSummary.set({
      summary: 'Call with Nexus Innovations concluded. Prospect showed high interest in replacing fragmented CRM dialers with SGrowing native voice SDRs.',
      actionItems: [
        'Dispatch formal CPQ proposal with 90-day ROI guarantee.',
        'Schedule technical architecture call with enterprise team.',
        'Logged disposition: Qualified Opportunity.'
      ]
    });
  }
}
