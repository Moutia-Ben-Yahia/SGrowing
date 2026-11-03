import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface WhisperSessionItem {
  callSessionId: string;
  repName: string;
  buyerOrganization: string;
  targetDealAcv: number;
  currentCallPhase: string;
  sentimentScore: number;
  activeWhisperPrompt: string;
}

export interface GuardrailItem {
  concessionType: string;
  maximumAllowedDiscountPct: number;
  minimumAcvThreshold: number;
}

@Component({
  selector: 'app-agent-whisper-cockpit',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="agent-whisper-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 bg-slate-900 border border-slate-800 p-6 rounded-2xl shadow-xl">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-3 py-1 text-xs font-semibold rounded-full bg-cyan-500/20 text-cyan-400 border border-cyan-500/30">
              Horizon 113 • Domain E17 & F14
            </span>
            <span class="text-xs text-slate-400">Sub-200ms Live Telephony Whisper &amp; Negotiation Guardrails</span>
          </div>
          <h1 class="text-2xl font-bold text-white mt-2">Conversational AI Agent Whisper &amp; Deal Co-Pilot</h1>
          <p class="text-slate-400 text-sm mt-1">
            Real-time speech stream parsing, dynamic concession boundary evaluation, and margin-protected HUD whispers.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="simulateSpeechAnalysis()"
            data-testid="simulate-speech-btn"
            class="px-4 py-2 bg-cyan-600 hover:bg-cyan-500 text-white text-sm font-medium rounded-xl transition shadow-lg shadow-cyan-600/30 flex items-center gap-2">
            Analyze Live Utterance
          </button>
          <button
            (click)="applyAutonomousConcession()"
            data-testid="apply-concession-btn"
            class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white text-sm font-medium rounded-xl transition shadow-lg shadow-indigo-600/30 flex items-center gap-2">
            Disburse Deal Concession
          </button>
        </div>
      </div>

      <!-- Metrics -->
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Live Monitored Calls</span>
          <p class="text-2xl font-bold text-white mt-1" data-testid="metric-active-calls">{{ activeCalls() }}</p>
          <span class="text-xs text-cyan-400 mt-1 block">Sub-200ms Audio Stream</span>
        </div>

        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Total Whispers Dispatched</span>
          <p class="text-2xl font-bold text-white mt-1" data-testid="metric-total-whispers">{{ totalWhispers() | number }}</p>
          <span class="text-xs text-slate-400 mt-1 block">Real-Time In-Ear / HUD</span>
        </div>

        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Concession Adherence Rate</span>
          <p class="text-2xl font-bold text-emerald-400 mt-1" data-testid="metric-adherence">{{ adherenceRate() }}%</p>
          <span class="text-xs text-emerald-300 mt-1 block">Margin Protected &gt; 82%</span>
        </div>

        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Deal Win Rate Lift</span>
          <p class="text-2xl font-bold text-indigo-400 mt-1" data-testid="metric-win-lift">+{{ winRateLift() }}%</p>
          <span class="text-xs text-indigo-300 mt-1 block">AI-Coached Reps vs Control</span>
        </div>
      </div>

      <!-- Active Sessions Table -->
      <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-6">
        <h2 class="text-lg font-bold text-white mb-4">Live Calls &amp; Active Real-Time Whispers</h2>
        <div class="overflow-x-auto">
          <table class="w-full text-left text-sm text-slate-300">
            <thead class="text-xs uppercase bg-slate-800/60 text-slate-400 border-b border-slate-700">
              <tr>
                <th class="py-3 px-4">Call ID</th>
                <th class="py-3 px-4">Sales Rep</th>
                <th class="py-3 px-4">Buyer Organization</th>
                <th class="py-3 px-4 text-right">Target ACV</th>
                <th class="py-3 px-4">Phase</th>
                <th class="py-3 px-4">Real-Time Whisper HUD Prompt</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-800">
              @for (sess of sessions(); track sess.callSessionId) {
                <tr class="hover:bg-slate-800/30 transition">
                  <td class="py-3 px-4 font-mono text-xs text-slate-400">{{ sess.callSessionId }}</td>
                  <td class="py-3 px-4 font-semibold text-white">{{ sess.repName }}</td>
                  <td class="py-3 px-4 text-slate-300">{{ sess.buyerOrganization }}</td>
                  <td class="py-3 px-4 text-right font-bold text-emerald-400">\${{ sess.targetDealAcv | number }}</td>
                  <td class="py-3 px-4">
                    <span class="px-2 py-0.5 text-xs rounded bg-slate-800 text-cyan-300 border border-slate-700">
                      {{ sess.currentCallPhase }}
                    </span>
                  </td>
                  <td class="py-3 px-4 text-xs font-medium text-amber-300">{{ sess.activeWhisperPrompt }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `
})
export class AgentWhisperCockpitComponent {
  activeCalls = signal<number>(3);
  totalWhispers = signal<number>(1840);
  adherenceRate = signal<number>(94.2);
  winRateLift = signal<number>(28.5);

  sessions = signal<WhisperSessionItem[]>([
    { callSessionId: 'CALL-8841', repName: 'Sarah Jenkins', buyerOrganization: 'Apex Financial Corp', targetDealAcv: 185000, currentCallPhase: 'PriceObjection', sentimentScore: 0.35, activeWhisperPrompt: 'Offer Net 60 instead of 15% discount; protects 88% gross margin' },
    { callSessionId: 'CALL-8842', repName: 'Michael Chang', buyerOrganization: 'BioHealth Dynamics', targetDealAcv: 95000, currentCallPhase: 'ContractNegotiation', sentimentScore: 0.65, activeWhisperPrompt: 'Leverage SOC2 Type II cert & enterprise SLA guarantees against competitor' },
    { callSessionId: 'CALL-8843', repName: 'Elena Rostova', buyerOrganization: 'Nordic Retail Holdings', targetDealAcv: 240000, currentCallPhase: 'Discovery', sentimentScore: 0.50, activeWhisperPrompt: 'Ask about upcoming Q4 inventory peak to anchor early rollout urgency' }
  ]);

  simulateSpeechAnalysis(): void {
    this.totalWhispers.update(v => v + 1);
  }

  applyAutonomousConcession(): void {
    this.sessions.update(list =>
      list.map(s => s.callSessionId === 'CALL-8841' ? { ...s, activeWhisperPrompt: 'Concession Approved: 12% discount applied with 24-month contract commitment' } : s)
    );
  }
}
