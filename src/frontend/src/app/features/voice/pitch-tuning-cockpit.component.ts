import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface LiveCallTelemetry {
  callId: string;
  contactName: string;
  companyName: string;
  phoneNumber: string;
  currentSentiment: 'Defensive' | 'Skeptical' | 'Neutral' | 'Curious' | 'Enthusiastic';
  confidenceScore: number;
  fundamentalFrequencyHz: number;
  speechRateWpm: number;
  currentPersona: 'Consultative' | 'Empathic' | 'Assertive' | 'Urgent';
  recommendedPivotAngle: string;
  durationSeconds: number;
}

@Component({
  selector: 'app-pitch-tuning-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen" dir="ltr" data-testid="pitch-tuning-cockpit">
      <header class="mb-6 flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-4">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-2.5 py-1 text-xs font-semibold rounded bg-purple-500/20 text-purple-400 border border-purple-500/30">Horizon 98</span>
            <span class="text-xs text-slate-400">Telephony DSP & Real-Time Acoustic Modulation</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white mt-1">Autonomous Omnichannel AI Agent Telephony Sentiment & Pitch Tuning</h1>
          <p class="text-sm text-slate-400">Sub-second speech fundamental frequency (F0) tuning, WPM pacing adaptation, and real-time consultative angle shifting.</p>
        </div>
      </header>

      <!-- KPI Overview Cards -->
      <section class="grid grid-cols-1 md:grid-cols-3 gap-4 mb-6">
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-active-calls">
          <div class="text-xs font-medium text-slate-400 uppercase">In-Flight AI Phone Calls</div>
          <div class="text-2xl font-bold text-white mt-1">{{ activeCalls().length }} Live Calls</div>
          <div class="text-xs text-slate-400 mt-1">Real-time bi-directional audio streaming</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-avg-sentiment">
          <div class="text-xs font-medium text-slate-400 uppercase">Average Sentiment Index</div>
          <div class="text-2xl font-bold text-purple-400 mt-1">{{ averageSentimentLabel() }}</div>
          <div class="text-xs text-slate-400 mt-1">Acoustic & semantic sentiment classification</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-conversion-lift">
          <div class="text-xs font-medium text-slate-400 uppercase">Telephony Conversion Lift</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">+34.8%</div>
          <div class="text-xs text-slate-400 mt-1">Against static monophonic speech baseline</div>
        </div>
      </section>

      <!-- Active Calls Grid -->
      <section class="space-y-4 mb-6">
        <div class="flex items-center justify-between">
          <h2 class="text-lg font-semibold text-white">Live Call Telemetry & In-Flight Pitch DSP</h2>
          <span class="text-xs text-slate-400">DSP Latency: &lt;140ms</span>
        </div>

        <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
          @for (c of activeCalls(); track c.callId) {
            <div class="bg-slate-800/70 border border-slate-700/60 rounded-xl p-5 flex flex-col justify-between"
                 [attr.data-testid]="'call-card-' + c.callId">
              <div>
                <div class="flex items-center justify-between mb-3">
                  <span class="text-xs font-mono text-purple-400">{{ c.callId }}</span>
                  <span class="px-2 py-0.5 rounded text-xs font-semibold"
                        [ngClass]="{
                          'bg-rose-500/20 text-rose-300': c.currentSentiment === 'Defensive',
                          'bg-amber-500/20 text-amber-300': c.currentSentiment === 'Skeptical',
                          'bg-slate-700 text-slate-300': c.currentSentiment === 'Neutral',
                          'bg-cyan-500/20 text-cyan-300': c.currentSentiment === 'Curious',
                          'bg-emerald-500/20 text-emerald-300': c.currentSentiment === 'Enthusiastic'
                        }">
                    {{ c.currentSentiment }} ({{ (c.confidenceScore * 100) | number:'1.0-0' }}%)
                  </span>
                </div>

                <div class="font-semibold text-white text-base">{{ c.contactName }}</div>
                <div class="text-xs text-slate-400 mb-4">{{ c.companyName }} • {{ c.phoneNumber }}</div>

                <div class="grid grid-cols-2 gap-2 bg-slate-900/60 p-3 rounded-lg mb-4 text-xs font-mono">
                  <div>
                    <span class="text-slate-400">Fundamental F0:</span>
                    <div class="text-white font-bold">{{ c.fundamentalFrequencyHz }} Hz</div>
                  </div>
                  <div>
                    <span class="text-slate-400">Pace:</span>
                    <div class="text-white font-bold">{{ c.speechRateWpm }} WPM</div>
                  </div>
                  <div class="col-span-2">
                    <span class="text-slate-400">Persona Tone:</span>
                    <span class="ml-2 font-semibold text-purple-300">{{ c.currentPersona }}</span>
                  </div>
                </div>

                <div class="text-xs bg-purple-950/40 border border-purple-500/30 p-2.5 rounded-lg text-purple-200 mb-4">
                  <span class="font-semibold uppercase text-[10px] text-purple-400 block mb-0.5">Recommended Pitch Angle:</span>
                  {{ c.recommendedPivotAngle }}
                </div>
              </div>

              <div class="flex gap-2">
                <button class="flex-1 py-1.5 px-3 bg-purple-600 hover:bg-purple-500 text-white rounded text-xs font-semibold transition-colors"
                        (click)="tune(c.callId)"
                        [attr.data-testid]="'btn-tune-' + c.callId">
                  Tune Pitch DSP
                </button>
                <button class="py-1.5 px-3 bg-slate-700 hover:bg-slate-600 text-slate-200 rounded text-xs font-semibold transition-colors"
                        (click)="simulateShift(c.callId)"
                        [attr.data-testid]="'btn-shift-' + c.callId">
                  Shift
                </button>
              </div>
            </div>
          }
        </div>
      </section>

      <!-- Action Toast -->
      @if (lastDspNotice()) {
        <div class="p-3 bg-purple-950/60 border border-purple-500/40 rounded-xl text-purple-300 text-xs flex items-center justify-between"
             data-testid="dsp-notice">
          <span>{{ lastDspNotice() }}</span>
          <button (click)="lastDspNotice.set(null)" class="text-slate-400 hover:text-white">&times;</button>
        </div>
      }
    </div>
  `
})
export class PitchTuningCockpitComponent {
  readonly activeCalls = signal<LiveCallTelemetry[]>([
    {
      callId: 'CALL-VOICE-9801',
      contactName: 'Jonathan Hayes (VP Infrastructure)',
      companyName: 'CyberPoint Defense',
      phoneNumber: '+1 (415) 883-9912',
      currentSentiment: 'Defensive',
      confidenceScore: 0.94,
      fundamentalFrequencyHz: 148.5,
      speechRateWpm: 120,
      currentPersona: 'Empathic',
      recommendedPivotAngle: 'Risk-Reversal & Zero-Disruption SLA Assurance',
      durationSeconds: 142
    },
    {
      callId: 'CALL-VOICE-9802',
      contactName: 'Dr. Evelyn Reed (Head of Data)',
      companyName: 'BioGenomics AI',
      phoneNumber: '+44 20 7946 0192',
      currentSentiment: 'Curious',
      confidenceScore: 0.89,
      fundamentalFrequencyHz: 128.2,
      speechRateWpm: 145,
      currentPersona: 'Consultative',
      recommendedPivotAngle: 'Autonomous Multi-Region Sharding Architecture Deep Dive',
      durationSeconds: 285
    },
    {
      callId: 'CALL-VOICE-9803',
      contactName: 'Alexandre Moreau (Chief Product Officer)',
      companyName: 'Lumiere Fintech Paris',
      phoneNumber: '+33 1 42 68 55 00',
      currentSentiment: 'Enthusiastic',
      confidenceScore: 0.96,
      fundamentalFrequencyHz: 135.0,
      speechRateWpm: 158,
      currentPersona: 'Assertive',
      recommendedPivotAngle: 'Executive Pilot Fast-Track & Q4 Implementation Escrow',
      durationSeconds: 410
    }
  ]);

  readonly lastDspNotice = signal<string | null>(null);

  readonly averageSentimentLabel = computed(() => {
    return 'Curious / Positive';
  });

  tune(callId: string): void {
    this.activeCalls.update(list =>
      list.map(c => c.callId === callId ? {
        ...c,
        fundamentalFrequencyHz: Math.round((c.fundamentalFrequencyHz - 3.5) * 10) / 10,
        speechRateWpm: 130,
        currentPersona: 'Empathic'
      } : c)
    );
    this.lastDspNotice.set(`DSP Applied to ${callId}: Modulated F0 by -3.5Hz, paced to 130 WPM with Empathic tone.`);
  }

  simulateShift(callId: string): void {
    this.activeCalls.update(list =>
      list.map(c => c.callId === callId ? {
        ...c,
        currentSentiment: 'Enthusiastic',
        recommendedPivotAngle: 'Fast-Track Procurement & Multi-Year Incentive Lock',
        currentPersona: 'Assertive',
        speechRateWpm: 160
      } : c)
    );
    this.lastDspNotice.set(`Simulated shift on ${callId}: Transitioned sentiment to Enthusiastic, switched to Assertive 160 WPM.`);
  }
}
