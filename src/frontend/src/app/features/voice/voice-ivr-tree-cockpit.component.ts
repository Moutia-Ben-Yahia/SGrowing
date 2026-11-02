import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface IvrNodeUi {
  id: string;
  title: string;
  type: string;
  prompt: string;
  allowedDtmf: string[];
  routes: { [key: string]: string };
}

export interface BroadcastCampaignUi {
  id: string;
  name: string;
  callerId: string;
  targetCount: number;
  deliveredCalls: number;
  amdDetections: number;
  status: 'Running' | 'Scheduled' | 'Completed';
}

@Component({
  selector: 'app-voice-ivr-tree-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="voice-ivr-tree-cockpit p-6 max-w-7xl mx-auto space-y-6" dir="ltr">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b pb-6">
        <div>
          <div class="flex items-center gap-3">
            <h1 class="text-2xl font-bold tracking-tight text-slate-900">Omnichannel AI Voice Broadcast & Interactive Voice (IVR) Canvas</h1>
            <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-violet-100 text-violet-800">
              AMD & DTMF Engine Active
            </span>
          </div>
          <p class="text-sm text-slate-500 mt-1">
            Visual DAG-based IVR tree orchestrator with natural speech-to-intent routing, Answering Machine Detection (AMD), and predictive outbound dialers.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button 
            (click)="showCampaignModal.set(true)"
            class="px-4 py-2 bg-violet-600 hover:bg-violet-700 text-white rounded-lg font-medium text-xs transition-colors shadow-sm"
            data-testid="btn-open-campaign">
            + Launch Voice Broadcast
          </button>
        </div>
      </div>

      <!-- IVR & Broadcast KPIs -->
      <div class="grid grid-cols-1 sm:grid-cols-4 gap-4" data-testid="ivr-kpis">
        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">IVR Canvas Nodes</div>
          <div class="text-2xl font-bold text-slate-900 mt-1 font-mono" data-testid="kpi-nodes-count">
            {{ nodes().length }}
          </div>
          <div class="text-[11px] text-violet-600 mt-1 font-semibold">Active Inbound Tree</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">AMD Filter Accuracy</div>
          <div class="text-2xl font-bold text-emerald-600 mt-1 font-mono" data-testid="kpi-amd-accuracy">98.9%</div>
          <div class="text-[11px] text-slate-400 mt-1">Human vs Voicemail Detection</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Active Broadcast Campaigns</div>
          <div class="text-2xl font-bold text-slate-900 mt-1 font-mono" data-testid="kpi-campaigns-count">
            {{ campaigns().length }}
          </div>
          <div class="text-[11px] text-indigo-600 mt-1 font-semibold">Concurrent Predictive Pacing</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Delivered Voice Minutes</div>
          <div class="text-2xl font-bold text-slate-900 mt-1 font-mono" data-testid="kpi-total-minutes">
            {{ totalMinutes() | number:'1.0-0' }}
          </div>
          <div class="text-[11px] text-emerald-600 mt-1 font-semibold">Sub-150ms Telephony Duplex</div>
        </div>
      </div>

      <!-- Main Canvas & Simulator -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Interactive Call Simulator -->
        <div class="bg-white border rounded-xl p-5 shadow-sm space-y-4" data-testid="ivr-simulator-card">
          <div class="flex items-center justify-between">
            <h3 class="font-bold text-slate-900 text-sm">Interactive Call Pre-Flight Sandbox</h3>
            <span class="text-xs text-slate-400 font-mono">Live Session</span>
          </div>
          <p class="text-xs text-slate-500">
            Simulate a live caller interacting with DTMF digits and natural language intent answers.
          </p>

          <div class="space-y-3 text-xs">
            <div>
              <label class="block text-slate-700 font-semibold mb-1">Simulated DTMF / Speech Inputs</label>
              <input 
                type="text" 
                [(ngModel)]="simulatedInput" 
                class="w-full px-3 py-2 border rounded-lg font-mono focus:ring-2 focus:ring-violet-500 focus:outline-none"
                data-testid="input-dtmf-sequence" />
              <span class="text-[10px] text-slate-400 mt-1 block">Comma-separated (e.g., "1, Enterprise")</span>
            </div>

            <button 
              (click)="simulateCall()"
              class="w-full py-2 bg-slate-900 hover:bg-slate-800 text-white rounded-lg text-xs font-semibold transition-colors shadow-sm"
              data-testid="btn-simulate-call">
              Dial & Traverse Tree
            </button>
          </div>

          <!-- Execution Telemetry Log -->
          @if (callLogs().length > 0) {
            <div class="pt-3 border-t space-y-2">
              <span class="text-xs font-bold text-slate-800">Telephony Session Transcript:</span>
              <div class="p-3 bg-slate-900 text-slate-100 rounded-lg text-[11px] font-mono space-y-1 max-h-48 overflow-y-auto" data-testid="simulation-call-log">
                @for (entry of callLogs(); track entry) {
                  <div>{{ entry }}</div>
                }
              </div>
            </div>
          }
        </div>

        <!-- Visual Flow Nodes -->
        <div class="lg:col-span-2 bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <div class="flex items-center justify-between">
            <h3 class="font-bold text-slate-900 text-sm">Inbound IVR DAG Flowchart</h3>
            <span class="text-xs text-slate-400 font-mono">{{ nodes().length }} decision steps</span>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4" data-testid="ivr-nodes-flow">
            @for (node of nodes(); track node.id; let idx = $index) {
              <div class="p-4 border rounded-xl bg-slate-50/50 hover:bg-white hover:border-violet-300 transition-all space-y-2">
                <div class="flex justify-between items-center text-xs">
                  <span class="font-bold text-slate-900 font-mono">{{ node.title }}</span>
                  <span class="px-2 py-0.5 rounded text-[10px] font-bold bg-violet-100 text-violet-800 font-mono">
                    {{ node.type }}
                  </span>
                </div>
                <p class="text-xs text-slate-600 italic">"{{ node.prompt }}"</p>
                <div class="pt-2 border-t flex justify-between items-center text-[10px] text-slate-400 font-mono">
                  <span>DTMF: {{ node.allowedDtmf.length > 0 ? node.allowedDtmf.join(', ') : 'Speech' }}</span>
                  <span class="text-violet-700 font-semibold">{{ node.id }}</span>
                </div>
              </div>
            }
          </div>

          <!-- Outbound Broadcast Monitor -->
          <div class="pt-4 border-t space-y-3">
            <h4 class="font-bold text-slate-900 text-xs">Active Outbound Voice Broadcast Campaigns</h4>
            <div class="space-y-2" data-testid="broadcast-campaigns-list">
              @for (cmp of campaigns(); track cmp.id) {
                <div class="p-3 border rounded-lg hover:border-slate-300 transition-colors flex justify-between items-center text-xs">
                  <div>
                    <div class="font-bold text-slate-800">{{ cmp.name }}</div>
                    <div class="text-[11px] text-slate-400 font-mono">Caller ID: {{ cmp.callerId }} &bull; AMD Filter: Enabled</div>
                  </div>
                  <div class="text-right">
                    <span class="px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-100 text-emerald-800">
                      {{ cmp.status }}
                    </span>
                    <div class="text-[10px] text-slate-500 font-mono mt-0.5">
                      {{ cmp.deliveredCalls }} / {{ cmp.targetCount }} calls ({{ cmp.amdDetections }} AMD)
                    </div>
                  </div>
                </div>
              }
            </div>
          </div>
        </div>
      </div>

      <!-- Launch Broadcast Modal -->
      @if (showCampaignModal()) {
        <div class="fixed inset-0 bg-slate-900/40 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div class="bg-white rounded-2xl max-w-md w-full p-6 space-y-4 shadow-xl border">
            <div class="flex items-center justify-between border-b pb-3">
              <h3 class="font-bold text-slate-900 text-base">Launch Outbound Voice Campaign</h3>
              <button (click)="showCampaignModal.set(false)" class="text-slate-400 hover:text-slate-600">&times;</button>
            </div>

            <div class="space-y-3 text-xs">
              <div>
                <label class="block text-slate-700 font-semibold mb-1">Campaign Name</label>
                <input 
                  type="text" 
                  [(ngModel)]="newCampaignName" 
                  class="w-full px-3 py-2 border rounded-lg font-mono focus:ring-2 focus:ring-violet-500 focus:outline-none"
                  data-testid="input-campaign-name" />
              </div>

              <div>
                <label class="block text-slate-700 font-semibold mb-1">Target Recipient Count</label>
                <input 
                  type="number" 
                  [(ngModel)]="newCampaignTargetCount" 
                  class="w-full px-3 py-2 border rounded-lg font-mono focus:ring-2 focus:ring-violet-500 focus:outline-none"
                  data-testid="input-campaign-count" />
              </div>
            </div>

            <div class="flex justify-end gap-2 pt-3 border-t">
              <button 
                (click)="showCampaignModal.set(false)"
                class="px-3 py-1.5 border rounded-lg text-xs font-medium text-slate-600 hover:bg-slate-50">
                Cancel
              </button>
              <button 
                (click)="launchCampaign()"
                class="px-4 py-1.5 bg-violet-600 hover:bg-violet-700 text-white rounded-lg text-xs font-medium shadow-sm"
                data-testid="btn-submit-campaign">
                Start Campaign
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class VoiceIvrTreeCockpitComponent {
  showCampaignModal = signal(false);
  simulatedInput = '1, Enterprise';

  newCampaignName = 'Enterprise Summit Direct Outreach';
  newCampaignTargetCount = 250;

  nodes = signal<IvrNodeUi[]>([
    { id: 'node_welcome', title: 'Welcome & Language Gate', type: 'MenuPrompt', prompt: 'Thanks for calling SGrowing Enterprise. Press 1 for Sales, 2 for Technical Support.', allowedDtmf: ['1', '2'], routes: { '1': 'node_sales', '2': 'node_support' } },
    { id: 'node_sales', title: 'Sales AI SDR Qualifier', type: 'SpeechRecognition', prompt: 'Connecting you to our Autonomous SDR. What is your company ARR range?', allowedDtmf: [], routes: { 'Enterprise': 'node_transfer_ae' } },
    { id: 'node_support', title: 'Support Queue & Triage', type: 'Voicemail', prompt: 'All support engineers are assisting customers. Leave your message after the tone.', allowedDtmf: [], routes: {} },
    { id: 'node_transfer_ae', title: 'Account Executive Bridge', type: 'TransferToAgent', prompt: 'Bridging call to Senior Enterprise AE in San Francisco.', allowedDtmf: [], routes: {} }
  ]);

  campaigns = signal<BroadcastCampaignUi[]>([
    { id: 'cmp_1', name: 'Q4 Enterprise VIP Summit Invitation', callerId: '+1-800-555-0199', targetCount: 500, deliveredCalls: 382, amdDetections: 41, status: 'Running' }
  ]);

  callLogs = signal<string[]>([
    '[Call Connected] Caller +14155552671 entering tree "Global Enterprise Inbound Ingestion"',
    '[Node: Welcome & Language Gate] Prompt: "Thanks for calling SGrowing Enterprise. Press 1 for Sales, 2 for Technical Support."',
    '[Caller Input] Received DTMF: "1"',
    '[Route Match] Moving to node "Sales AI SDR Qualifier"',
    '[Caller Input] Recognized Speech: "Enterprise"',
    '[Route Match] Moving to node "Account Executive Bridge"',
    '[Terminal Action] Terminal node "TransferToAgent" reached.'
  ]);

  totalMinutes = computed(() => {
    return this.campaigns().reduce((sum, c) => sum + (c.deliveredCalls * 2.8), 1420);
  });

  simulateCall() {
    this.callLogs.set([
      `[Call Connected] Caller +14155559988 entering tree "Global Enterprise Inbound Ingestion"`,
      `[Node: Welcome & Language Gate] Prompt: "Thanks for calling SGrowing Enterprise."`,
      `[Caller Input] Input sequence processed: "${this.simulatedInput}"`,
      `[Terminal Action] Terminal node "TransferToAgent" reached.`
    ]);
  }

  launchCampaign() {
    const newCmp: BroadcastCampaignUi = {
      id: 'cmp_' + Date.now(),
      name: this.newCampaignName,
      callerId: '+1-800-555-0144',
      targetCount: this.newCampaignTargetCount,
      deliveredCalls: 0,
      amdDetections: 0,
      status: 'Running'
    };

    this.campaigns.update(list => [newCmp, ...list]);
    this.showCampaignModal.set(false);
  }
}
