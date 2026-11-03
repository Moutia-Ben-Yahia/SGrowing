import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface ActiveCallSessionSnapshotRecord {
  sessionId: string;
  callerPhoneNumber: string;
  calleePhoneNumber: string;
  customerName: string;
  lastAcousticUtterance: string;
  dialogGoalTopic: string;
  currentContractPhase: string;
  turnCounter: number;
}

export interface CallResumptionEventRecord {
  resumptionId: string;
  originalSessionId: string;
  reconnectSessionId: string;
  disconnectReason: string;
  reconnectLatencySeconds: number;
  generatedAcousticGreeting: string;
  resumptionStatus: string;
}

@Component({
  selector: 'app-call-state-resumption-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" data-testid="call-state-resumption-cockpit" class="p-6 bg-slate-900 text-slate-100 min-h-screen">
      <header class="mb-8 border-b border-slate-800 pb-4 flex justify-between items-center">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-white flex items-center gap-2">
            <span class="inline-block w-3 h-3 rounded-full bg-emerald-500 animate-pulse"></span>
            Call Disconnect Auto-Callback & State Resumption Mesh
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Zero-Loss Conversational AST Recovery, Sub-5s Automated PSTN/WebRTC Reconnect & Contextual Re-Greeting
          </p>
        </div>
        <div class="flex gap-3">
          <button (click)="simulateAutoCallback()" class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white font-medium rounded-lg shadow-sm transition">
            Simulate Drop & Auto-Callback
          </button>
        </div>
      </header>

      <!-- KPI Metrics Grid -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Monitored Calls</span>
          <p class="text-2xl font-extrabold text-white mt-1">{{ snapshots().length }}</p>
          <span class="text-xs text-emerald-400">Continuous Heartbeat AST</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Callback Success</span>
          <p class="text-2xl font-extrabold text-white mt-1">{{ autoCallbackSuccessRatePct() }}%</p>
          <span class="text-xs text-cyan-400">PSTN & WebRTC Resumed</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Reconnect Latency</span>
          <p class="text-2xl font-extrabold text-white mt-1">2.4s</p>
          <span class="text-xs text-indigo-400">Sub-5s SLA Target</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Context Retention</span>
          <p class="text-2xl font-extrabold text-white mt-1">100.0%</p>
          <span class="text-xs text-amber-400">Zero Lost Customer Data</span>
        </div>
      </section>

      <!-- Main Panels -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-8">
        <!-- Active Session Snapshots -->
        <section class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
          <h2 class="text-lg font-semibold text-white mb-4">Live Monitored Call Sessions</h2>
          <div class="space-y-4">
            <div *ngFor="let s of snapshots()" class="bg-slate-900/60 border border-slate-700/40 p-4 rounded-lg">
              <div class="flex justify-between items-center mb-1">
                <span class="font-bold text-white">{{ s.customerName }}</span>
                <span class="text-[10px] font-mono bg-slate-800 text-slate-400 px-2 py-0.5 rounded">
                  Turn {{ s.turnCounter }}
                </span>
              </div>
              <p class="text-xs text-slate-400 mb-1">{{ s.callerPhoneNumber }} → {{ s.calleePhoneNumber }}</p>
              <p class="text-xs font-semibold text-emerald-400 mb-2">Phase: {{ s.currentContractPhase }}</p>
              <div class="bg-slate-950 p-2.5 rounded text-xs text-slate-300 italic border border-slate-800">
                "{{ s.lastAcousticUtterance }}"
              </div>
            </div>
          </div>
        </section>

        <!-- Resumption Events -->
        <section class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
          <h2 class="text-lg font-semibold text-white mb-4">Auto-Callback Resumption Events</h2>
          <div class="space-y-4">
            <div *ngFor="let r of resumptions()" class="bg-slate-900/60 border border-slate-700/40 p-4 rounded-lg">
              <div class="flex justify-between items-center mb-1">
                <span class="font-bold text-emerald-400">{{ r.resumptionId }}</span>
                <span class="text-xs font-mono px-2 py-0.5 rounded bg-emerald-950 border border-emerald-800 text-emerald-300">
                  {{ r.reconnectLatencySeconds }}s Reconnect
                </span>
              </div>
              <p class="text-xs text-rose-400 font-mono mb-2">Dropped: {{ r.disconnectReason }}</p>
              <div class="p-2.5 bg-emerald-950/30 border border-emerald-800/40 rounded text-xs text-emerald-200">
                Prompt: {{ r.generatedAcousticGreeting }}
              </div>
            </div>
          </div>
        </section>
      </div>
    </div>
  `
})
export class CallStateResumptionCockpitComponent {
  snapshots = signal<ActiveCallSessionSnapshotRecord[]>([
    { sessionId: 'CALL-SES-991', callerPhoneNumber: '+1-415-555-0199', calleePhoneNumber: '+1-800-555-0100', customerName: 'Marcus Brody (VP Technology)', lastAcousticUtterance: 'We were wondering how your SOC2 compliance handles multi-region data sovereignty...', dialogGoalTopic: 'Security Architecture', currentContractPhase: 'COMMERCIAL_PROPOSAL_REVIEW', turnCounter: 14 },
    { sessionId: 'CALL-SES-992', callerPhoneNumber: '+44-20-7946-0922', calleePhoneNumber: '+44-800-014-9900', customerName: 'Helena Berg (Director of FinOps)', lastAcousticUtterance: 'Can we lock in the multi-currency hedging rates before next quarter?', dialogGoalTopic: 'Treasury & Payment Terms', currentContractPhase: 'PAYMENT_TERMS_DISCOUNT', turnCounter: 9 }
  ]);

  resumptions = signal<CallResumptionEventRecord[]>([
    { resumptionId: 'RES-EVT-1001', originalSessionId: 'CALL-SES-988', reconnectSessionId: 'CALL-SES-988-REC', disconnectReason: 'CELLULAR_TOWER_HANDOFF_DROP', reconnectLatencySeconds: 2.8, generatedAcousticGreeting: 'Hi Marcus, apologize for the brief signal drop! We were discussing the SOC2 data sovereignty boundaries.', resumptionStatus: 'RESUMED_IN_PROGRESS' }
  ]);

  autoCallbackSuccessRatePct = signal(96.8);

  simulateAutoCallback() {
    const newResumption: CallResumptionEventRecord = {
      resumptionId: `RES-${Date.now().toString().slice(-4)}`,
      originalSessionId: 'CALL-SES-992',
      reconnectSessionId: 'CALL-SES-992-REC',
      disconnectReason: 'SIP_BYE_UNEXPECTED_CARRIER_RESET',
      reconnectLatencySeconds: 2.1,
      generatedAcousticGreeting: 'Hi Helena, apologies for the carrier disconnect! We were just discussing locking in your hedging rates.',
      resumptionStatus: 'AUTO_CALLED_BACK'
    };
    this.resumptions.update(list => [newResumption, ...list]);
  }
}
