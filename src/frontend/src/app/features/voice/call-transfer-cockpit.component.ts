import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface LiveVoiceCall {
  callId: string;
  callerNumber: string;
  customerName: string;
  callerSentimentScore: number;
  durationSeconds: number;
  intentSummary: string;
  state: 'AiHandling' | 'ThreeWayBridge' | 'TransferCompleted';
  targetSpecialist?: string;
}

@Component({
  selector: 'app-call-transfer-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen" dir="ltr" data-testid="call-transfer-cockpit">
      <header class="mb-6 flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-4">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-2.5 py-1 text-xs font-semibold rounded bg-sky-500/20 text-sky-400 border border-sky-500/30">Horizon 88</span>
            <span class="text-xs text-slate-400">Omnichannel IVR & Live Voice Transfer Mesh</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white mt-1">Autonomous Voice IVR & Warm Call Transfer Mesh</h1>
          <p class="text-sm text-slate-400">Sub-second warm transfer bridging, full context memory propagation, and sentiment de-escalation handoffs.</p>
        </div>
      </header>

      <!-- KPI Summary -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-live-calls">
          <div class="text-xs font-medium text-slate-400 uppercase">Active Voice Sessions</div>
          <div class="text-2xl font-bold text-sky-400 mt-1">{{ calls().length }} Concurrent</div>
          <div class="text-xs text-slate-400 mt-1">Real-time WebRTC audio streams</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-deflection-rate">
          <div class="text-xs font-medium text-slate-400 uppercase">AI Voice Deflection Rate</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">{{ deflectionRate() | number:'1.1-1' }}%</div>
          <div class="text-xs text-slate-400 mt-1">Resolved without human escalation</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-bridged-transfers">
          <div class="text-xs font-medium text-slate-400 uppercase">Warm Transfers Bridged</div>
          <div class="text-2xl font-bold text-amber-400 mt-1">{{ warmTransfersCount() }}</div>
          <div class="text-xs text-slate-400 mt-1">0% lost context on handoff</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-average-sentiment">
          <div class="text-xs font-medium text-slate-400 uppercase">Avg Sentiment Index</div>
          <div class="text-2xl font-bold text-purple-400 mt-1">{{ averageSentiment() | number:'1.2-2' }} / 1.0</div>
          <div class="text-xs text-slate-400 mt-1">Sentiment uplift on human bridge</div>
        </div>
      </section>

      <!-- Calls Grid -->
      <section class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div class="lg:col-span-2 space-y-4">
          <h2 class="text-lg font-semibold text-white">Live Call Queue & Bridge Matrix</h2>
          <div class="space-y-3">
            @for (call of calls(); track call.callId) {
              <div
                class="bg-slate-800/60 border border-slate-700 p-4 rounded-xl hover:border-slate-600 transition-all cursor-pointer"
                [class.ring-2]="selectedCall()?.callId === call.callId"
                [class.ring-sky-500]="selectedCall()?.callId === call.callId"
                (click)="selectCall(call)"
                data-testid="live-call-card"
              >
                <div class="flex items-center justify-between">
                  <div>
                    <div class="flex items-center gap-2">
                      <span class="font-semibold text-white text-base">{{ call.customerName }}</span>
                      <span class="text-xs text-slate-400">{{ call.callerNumber }}</span>
                    </div>
                    <p class="text-xs text-slate-300 mt-1 italic">"{{ call.intentSummary }}"</p>
                  </div>
                  <div class="text-right">
                    <span class="text-xs font-bold" [class.text-rose-400]="call.callerSentimentScore < 0.4" [class.text-emerald-400]="call.callerSentimentScore >= 0.7">
                      Sentiment: {{ call.callerSentimentScore * 100 | number:'1.0-0' }}%
                    </span>
                    <div class="text-xs text-slate-400 mt-0.5">Duration: {{ call.durationSeconds }}s</div>
                  </div>
                </div>

                <div class="mt-4 flex items-center justify-between pt-2 border-t border-slate-700/60">
                  <div class="text-xs">
                    <span class="text-slate-400">Status:</span>
                    <span class="ml-1 font-semibold" [class.text-sky-400]="call.state === 'AiHandling'" [class.text-amber-400]="call.state === 'ThreeWayBridge'" [class.text-emerald-400]="call.state === 'TransferCompleted'">
                      {{ call.state }}
                    </span>
                    @if (call.targetSpecialist) {
                      <span class="text-slate-400 ml-2">Assigned to: <strong class="text-slate-200">{{ call.targetSpecialist }}</strong></span>
                    }
                  </div>

                  @if (call.state === 'TransferCompleted') {
                    <span class="text-xs px-2.5 py-1 rounded-full bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 font-medium" data-testid="call-transferred-indicator">
                      Handoff Complete
                    </span>
                  } @else if (call.state === 'ThreeWayBridge') {
                    <button
                      (click)="completeTransfer(call.callId); $event.stopPropagation()"
                      class="px-3 py-1 bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-semibold rounded shadow transition-colors"
                    >
                      Complete Handoff
                    </button>
                  } @else {
                    <button
                      (click)="initiateWarmTransfer(call.callId, 'Tier 2 Escalations Lead'); $event.stopPropagation()"
                      data-testid="btn-initiate-transfer"
                      class="px-3 py-1 bg-sky-600 hover:bg-sky-500 text-white text-xs font-semibold rounded shadow transition-colors"
                    >
                      Initiate Warm Transfer
                    </button>
                  }
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Detail Sidebar -->
        <div class="bg-slate-800/60 border border-slate-700 p-5 rounded-xl flex flex-col justify-between">
          <div>
            <h2 class="text-lg font-semibold text-white mb-3">Live Session Audio & Context</h2>
            @if (selectedCall(); as sel) {
              <div class="space-y-4">
                <div class="p-3 bg-slate-900/60 rounded-lg border border-slate-700/60">
                  <div class="text-xs text-slate-400 uppercase">Caller Identity</div>
                  <div class="text-base font-bold text-white mt-0.5">{{ sel.customerName }}</div>
                  <div class="text-xs text-sky-400 mt-1">WebRTC Stream ID: {{ sel.callId }}</div>
                </div>

                <div class="space-y-2 text-sm">
                  <div class="flex justify-between py-1 border-b border-slate-700/50">
                    <span class="text-slate-400">Caller Sentiment:</span>
                    <span class="font-mono text-emerald-400">{{ sel.callerSentimentScore * 100 | number:'1.0-0' }}%</span>
                  </div>
                  <div class="flex justify-between py-1 border-b border-slate-700/50">
                    <span class="text-slate-400">Detected Intent:</span>
                    <span class="text-xs font-medium text-slate-200">{{ sel.intentSummary }}</span>
                  </div>
                  <div class="flex justify-between py-1 border-b border-slate-700/50">
                    <span class="text-slate-400">Context Memory Injection:</span>
                    <span class="font-mono text-emerald-400">Active (Full Summary Transferred)</span>
                  </div>
                </div>

                @if (sel.state === 'AiHandling') {
                  <button
                    (click)="initiateWarmTransfer(sel.callId, 'Senior Support Engineer')"
                    class="w-full mt-4 py-2 bg-gradient-to-r from-sky-600 to-blue-600 hover:from-sky-500 hover:to-blue-500 text-white font-semibold rounded text-sm shadow transition-all"
                  >
                    Bridge to Human Specialist
                  </button>
                }
              </div>
            } @else {
              <p class="text-xs text-slate-400">Select a call to inspect WebRTC telemetry.</p>
            }
          </div>

          <div class="mt-6 p-3 bg-slate-900/80 rounded border border-slate-800 text-xs text-slate-400">
            <span class="text-sky-400 font-semibold">Zero-Latency Bridge:</span> Both parties stay on the line during warm intro so caller never repeats themselves.
          </div>
        </div>
      </section>
    </div>
  `
})
export class CallTransferCockpitComponent {
  calls = signal<LiveVoiceCall[]>([
    {
      callId: 'call_voice_01',
      callerNumber: '+1 (555) 349-2911',
      customerName: 'Marcus Vance (Datacorp)',
      callerSentimentScore: 0.35,
      durationSeconds: 142,
      intentSummary: 'Billing discrepancy on invoice #9011 - requesting manager intervention',
      state: 'AiHandling'
    },
    {
      callId: 'call_voice_02',
      callerNumber: '+1 (555) 782-4410',
      customerName: 'Elena Rostova (Apex Dynamics)',
      callerSentimentScore: 0.78,
      durationSeconds: 85,
      intentSummary: 'API webhook latency optimization guidance',
      state: 'AiHandling'
    },
    {
      callId: 'call_voice_03',
      callerNumber: '+1 (555) 912-3329',
      customerName: 'David Kim (Nexus Logistics)',
      callerSentimentScore: 0.85,
      durationSeconds: 210,
      intentSummary: 'Enterprise contract renewal inquiry',
      state: 'AiHandling'
    }
  ]);

  selectedCall = signal<LiveVoiceCall | null>(this.calls()[0]);

  deflectionRate = computed(() => {
    // Percentage resolved by AI without human transfer
    const total = this.calls().length;
    if (!total) return 100;
    const aiHandled = this.calls().filter(c => c.state === 'AiHandling').length;
    return (aiHandled / total) * 100;
  });

  warmTransfersCount = computed(() =>
    this.calls().filter(c => c.state === 'ThreeWayBridge' || c.state === 'TransferCompleted').length
  );

  averageSentiment = computed(() => {
    const list = this.calls();
    if (!list.length) return 0;
    return list.reduce((sum, c) => sum + c.callerSentimentScore, 0) / list.length;
  });

  selectCall(call: LiveVoiceCall): void {
    this.selectedCall.set(call);
  }

  initiateWarmTransfer(callId: string, specialist: string): void {
    this.calls.update(list =>
      list.map(c => c.callId === callId ? { ...c, state: 'ThreeWayBridge', targetSpecialist: specialist } : c)
    );
    if (this.selectedCall()?.callId === callId) {
      this.selectedCall.update(curr => curr ? { ...curr, state: 'ThreeWayBridge', targetSpecialist: specialist } : null);
    }
  }

  completeTransfer(callId: string): void {
    this.calls.update(list =>
      list.map(c => c.callId === callId ? { ...c, state: 'TransferCompleted', callerSentimentScore: Math.min(1.0, c.callerSentimentScore + 0.4) } : c)
    );
    if (this.selectedCall()?.callId === callId) {
      this.selectedCall.update(curr => curr ? { ...curr, state: 'TransferCompleted', callerSentimentScore: Math.min(1.0, curr.callerSentimentScore + 0.4) } : null);
    }
  }
}
