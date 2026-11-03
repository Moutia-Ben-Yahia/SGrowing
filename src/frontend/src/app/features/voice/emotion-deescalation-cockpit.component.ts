import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface LiveCallAcousticTelemetry {
  callId: string;
  agentId: string;
  customerPhone: string;
  pitchVarianceHz: number;
  speechVelocityWordsPerMin: number;
  frustrationScore: number;
  detectedEmotion: string;
  recommendedResponseStrategy: string;
}

export interface SupervisorEscalationTicket {
  escalationId: string;
  callId: string;
  customerPhone: string;
  peakFrustrationScore: number;
  triggerReason: string;
  suggestedConcessionOffer: string;
  status: string;
}

@Component({
  selector: 'app-emotion-deescalation-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" data-testid="emotion-deescalation-cockpit" class="p-6 bg-slate-900 text-slate-100 min-h-screen">
      <header class="mb-8 border-b border-slate-800 pb-4 flex justify-between items-center">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-white flex items-center gap-2">
            <span class="inline-block w-3 h-3 rounded-full bg-rose-500 animate-pulse"></span>
            AI Voice Call Emotion De-escalation & Empathy Routing
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Real-Time Acoustic Sentiment Analysis, LEAP Empathy Framework & Warm Supervisor Handoff
          </p>
        </div>
        <div class="flex gap-3">
          <button (click)="triggerWarmHandoff()" class="px-4 py-2 bg-rose-600 hover:bg-rose-500 text-white font-medium rounded-lg shadow-sm transition">
            Trigger Emergency Supervisor Handoff
          </button>
        </div>
      </header>

      <!-- KPI Metrics Grid -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Monitored Calls</span>
          <p class="text-2xl font-extrabold text-white mt-1">{{ calls().length }}</p>
          <span class="text-xs text-rose-400">Sub-150ms DSP Streaming</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">De-escalation Rate</span>
          <p class="text-2xl font-extrabold text-emerald-400 mt-1">{{ deescalationRatePct() }}%</p>
          <span class="text-xs text-slate-400">Calmed within 3 turns</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Avg Acoustic Stress</span>
          <p class="text-2xl font-extrabold text-amber-400 mt-1">0.56</p>
          <span class="text-xs text-slate-400">Scale: 0.0 (Calm) - 1.0 (Rage)</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Supervisor Queue</span>
          <p class="text-2xl font-extrabold text-indigo-400 mt-1">{{ escalations().length }}</p>
          <span class="text-xs text-slate-400">Warm Handoffs Ready</span>
        </div>
      </section>

      <!-- Main Panels -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <!-- Live Acoustic Streams -->
        <section class="bg-slate-800/40 border border-slate-700/50 rounded-xl p-5 space-y-4">
          <h2 class="text-lg font-semibold text-white">Live Acoustic Emotion Telemetry</h2>
          <div *ngFor="let call of calls()" class="p-4 bg-slate-900/60 border border-slate-700/40 rounded-xl">
            <div class="flex justify-between items-start mb-2">
              <div>
                <span class="font-mono text-xs text-indigo-300 font-semibold">{{ call.callId }}</span>
                <span class="text-slate-400 text-xs ml-2">({{ call.customerPhone }})</span>
              </div>
              <span class="px-2 py-0.5 text-xs rounded font-semibold"
                    [ngClass]="call.frustrationScore > 0.6 ? 'bg-rose-500/20 text-rose-300 border border-rose-500/40' : 'bg-emerald-500/20 text-emerald-300'">
                {{ call.detectedEmotion }}
              </span>
            </div>
            <div class="grid grid-cols-3 gap-2 my-2 text-xs">
              <div class="bg-slate-800/50 p-2 rounded">
                <span class="text-slate-400">Pitch Variance:</span>
                <p class="font-mono font-bold text-slate-200">{{ call.pitchVarianceHz }} Hz</p>
              </div>
              <div class="bg-slate-800/50 p-2 rounded">
                <span class="text-slate-400">Speech Rate:</span>
                <p class="font-mono font-bold text-slate-200">{{ call.speechVelocityWordsPerMin }} WPM</p>
              </div>
              <div class="bg-slate-800/50 p-2 rounded">
                <span class="text-slate-400">Frustration:</span>
                <p class="font-mono font-bold" [ngClass]="call.frustrationScore > 0.6 ? 'text-rose-400' : 'text-emerald-400'">
                  {{ (call.frustrationScore * 100).toFixed(0) }}%
                </p>
              </div>
            </div>
            <div class="mt-3 p-2 bg-indigo-950/40 border border-indigo-500/30 rounded text-xs text-indigo-200">
              <span class="font-semibold text-indigo-400">AI Empathy Guidance: </span>
              {{ call.recommendedResponseStrategy }}
            </div>
          </div>
        </section>

        <!-- Supervisor Escalation Desk -->
        <section class="bg-slate-800/40 border border-slate-700/50 rounded-xl p-5">
          <h2 class="text-lg font-semibold text-white mb-4">Supervisor Handoff Queue</h2>
          <div class="space-y-4">
            <div *ngFor="let esc of escalations()" class="p-4 bg-slate-900/60 border border-rose-500/30 rounded-xl">
              <div class="flex justify-between items-center mb-2">
                <span class="font-mono text-xs text-rose-400 font-bold">{{ esc.escalationId }}</span>
                <span class="px-2 py-0.5 text-xs bg-rose-500/20 text-rose-300 rounded font-semibold">{{ esc.status }}</span>
              </div>
              <p class="text-sm font-medium text-slate-200 mb-1">{{ esc.triggerReason }}</p>
              <p class="text-xs text-slate-400 mb-3">Call Ref: {{ esc.callId }} | Phone: {{ esc.customerPhone }}</p>
              <div class="p-2.5 bg-slate-950/80 rounded border border-slate-800">
                <span class="text-xs font-semibold text-emerald-400">Suggested Autonomous Concession:</span>
                <p class="text-xs text-slate-300 mt-0.5">{{ esc.suggestedConcessionOffer }}</p>
              </div>
            </div>
          </div>
        </section>
      </div>
    </div>
  `
})
export class EmotionDeescalationCockpitComponent {
  calls = signal<LiveCallAcousticTelemetry[]>([
    {
      callId: 'CALL-VOC-8801',
      agentId: 'AGT-EMMA-01',
      customerPhone: '+1-415-555-0199',
      pitchVarianceHz: 145.2,
      speechVelocityWordsPerMin: 210.5,
      frustrationScore: 0.88,
      detectedEmotion: 'HIGH_FRUSTRATION',
      recommendedResponseStrategy: 'LEAP Empathy Pivot: Acknowledge delayed onboarding before addressing pricing'
    },
    {
      callId: 'CALL-VOC-8802',
      agentId: 'AGT-LIAM-02',
      customerPhone: '+44-20-7946-0912',
      pitchVarianceHz: 82.0,
      speechVelocityWordsPerMin: 145.0,
      frustrationScore: 0.24,
      detectedEmotion: 'CALM_COLLABORATIVE',
      recommendedResponseStrategy: 'Maintain positive validation and proceed to contract signature'
    }
  ]);

  escalations = signal<SupervisorEscalationTicket[]>([
    {
      escalationId: 'ESC-SUP-101',
      callId: 'CALL-VOC-8801',
      customerPhone: '+1-415-555-0199',
      peakFrustrationScore: 0.92,
      triggerReason: 'Contract Renewal Dispute & Integration Blocker',
      suggestedConcessionOffer: 'Authorize 1-month SLA credit + Dedicated Solution Architect',
      status: 'PENDING_SUPERVISOR_ACCEPT'
    }
  ]);

  deescalationRatePct = signal(91.4);

  triggerWarmHandoff() {
    const newEsc: SupervisorEscalationTicket = {
      escalationId: `ESC-SUP-${Date.now().toString().slice(-4)}`,
      callId: 'CALL-VOC-8802',
      customerPhone: '+44-20-7946-0912',
      peakFrustrationScore: 0.85,
      triggerReason: 'Executive Escalation on Data Residency',
      suggestedConcessionOffer: 'Offer Free Sovereign EU Cloud Dedicated Pod Migration',
      status: 'SUPERVISOR_ACCEPTED'
    };
    this.escalations.update(list => [newEsc, ...list]);
  }
}
