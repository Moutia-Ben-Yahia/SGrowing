import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export type IvrNodeType =
  | 'PlayAudioPrompt'
  | 'CollectDtmfDigits'
  | 'SpeechRecognition'
  | 'RouteToQueue'
  | 'VoicemailDrop'
  | 'Hangup';

export interface IvrNodeItem {
  id: string;
  name: string;
  type: IvrNodeType;
  promptText: string;
  allowedDigits?: string[];
  targetQueue?: string;
  transitions?: Record<string, string>; // digit -> target node name
}

@Component({
  selector: 'app-voice-ivr-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6 text-slate-100" dir="ltr">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-5">
        <div>
          <div class="flex items-center gap-3">
            <span class="text-3xl">📞</span>
            <div>
              <h1 class="text-2xl font-bold tracking-tight text-white">
                Voice IVR Flow Studio & Call Router
              </h1>
              <p class="text-xs text-slate-400 mt-1">
                Visual node tree, DTMF keypad dispatch, and operating hours schedule manager.
              </p>
            </div>
          </div>
        </div>

        <div class="flex items-center gap-2">
          <span class="px-2.5 py-1 rounded-full text-xs font-semibold bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">
            Flow Status: Active
          </span>
        </div>
      </div>

      <!-- Main Layout: Node Graph + Simulator -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Node Tree / Graph (2 Cols) -->
        <div class="lg:col-span-2 space-y-4">
          <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-5">
            <div class="flex items-center justify-between mb-4 pb-2 border-b border-slate-800">
              <h2 class="text-sm font-semibold text-white flex items-center gap-2">
                <span>🔀</span> Inbound Call Navigation Tree
              </h2>
              <span class="text-xs text-slate-400">Default Flow: Enterprise Customer Intake</span>
            </div>

            <div class="space-y-3">
              @for (node of nodes(); track node.id) {
                <div
                  class="p-4 rounded-xl border transition"
                  [ngClass]="activeSimNode()?.id === node.id ? 'bg-indigo-950/40 border-indigo-500/80 shadow-lg shadow-indigo-500/10' : 'bg-slate-950/60 border-slate-800'">
                  <div class="flex items-center justify-between">
                    <div class="flex items-center gap-2">
                      <span class="text-xs font-bold text-white">{{ node.name }}</span>
                      <span
                        class="px-2 py-0.5 rounded text-[10px] font-semibold border"
                        [ngClass]="getNodeBadgeClass(node.type)">
                        {{ node.type }}
                      </span>
                    </div>

                    @if (node.targetQueue) {
                      <span class="text-xs text-cyan-400 font-mono">Queue: {{ node.targetQueue }}</span>
                    }
                  </div>

                  <p class="text-xs text-slate-300 mt-2 italic bg-slate-900/80 p-2.5 rounded border border-slate-800/80">
                    "{{ node.promptText }}"
                  </p>

                  @if (node.transitions) {
                    <div class="mt-3 flex flex-wrap gap-2 text-xs">
                      @for (item of getTransitionsList(node.transitions); track item.digit) {
                        <span class="px-2 py-0.5 rounded bg-slate-800 text-slate-300 border border-slate-700 font-mono text-[11px]">
                          Key [{{ item.digit }}] → {{ item.target }}
                        </span>
                      }
                    </div>
                  }
                </div>
              }
            </div>
          </div>
        </div>

        <!-- Live Dialpad & Caller Simulator (1 Col) -->
        <div class="space-y-4">
          <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-5 space-y-4">
            <div class="flex items-center justify-between pb-2 border-b border-slate-800">
              <h2 class="text-sm font-semibold text-white flex items-center gap-2">
                <span>📱</span> Live Caller Simulator
              </h2>
              <button
                (click)="restartSimulation()"
                class="text-[11px] text-cyan-400 hover:text-cyan-300 transition">
                Reset Call
              </button>
            </div>

            <!-- Current Prompt Audio Box -->
            <div class="p-3 rounded-lg bg-slate-950 border border-indigo-500/30 text-xs space-y-1">
              <div class="text-[10px] uppercase font-bold text-indigo-400 flex items-center gap-1">
                <span>🔊</span> Prompt Playing
              </div>
              <p class="text-white font-medium">
                {{ activeSimNode()?.promptText ?? 'Call ended.' }}
              </p>
            </div>

            <!-- DTMF Dialpad -->
            <div class="grid grid-cols-3 gap-2">
              @for (key of dialpadKeys; track key) {
                <button
                  (click)="pressKey(key)"
                  [disabled]="isTerminalNode()"
                  class="py-3 rounded-lg bg-slate-950 hover:bg-slate-800 disabled:opacity-30 border border-slate-800 hover:border-slate-700 text-sm font-bold text-white transition active:scale-95 shadow">
                  {{ key }}
                </button>
              }
            </div>

            <!-- Call Transition Log -->
            <div class="space-y-2 pt-2 border-t border-slate-800">
              <div class="text-xs font-semibold text-slate-400">Call Trajectory:</div>
              <div class="max-h-40 overflow-y-auto space-y-1 text-[11px] font-mono">
                @for (log of callLog(); track $index) {
                  <div class="text-slate-400">{{ log }}</div>
                }
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  `
})
export class VoiceIvrStudioComponent {
  dialpadKeys = ['1', '2', '3', '4', '5', '6', '7', '8', '9', '*', '0', '#'];

  nodes = signal<IvrNodeItem[]>([
    {
      id: 'node-start',
      name: 'MainMenu',
      type: 'CollectDtmfDigits',
      promptText: 'Welcome to SGrowing. Press 1 for Sales, 2 for Technical Support, or 3 for Billing.',
      allowedDigits: ['1', '2', '3'],
      transitions: {
        '1': 'SalesRoute',
        '2': 'SupportRoute',
        '3': 'BillingRoute'
      }
    },
    {
      id: 'node-sales',
      name: 'SalesRoute',
      type: 'RouteToQueue',
      promptText: 'Connecting you with an enterprise growth advisor.',
      targetQueue: 'SalesEnterprise'
    },
    {
      id: 'node-support',
      name: 'SupportRoute',
      type: 'RouteToQueue',
      promptText: 'Transferring to Tier 1 Technical Support queue.',
      targetQueue: 'TechSupportTier1'
    },
    {
      id: 'node-billing',
      name: 'BillingRoute',
      type: 'RouteToQueue',
      promptText: 'Hold on while we connect you to Billing & Invoicing.',
      targetQueue: 'FinanceBilling'
    },
    {
      id: 'node-hangup',
      name: 'HangupNode',
      type: 'Hangup',
      promptText: 'Thank you for contacting SGrowing. Have a great day. Goodbye.'
    }
  ]);

  activeSimNode = signal<IvrNodeItem | null>(this.nodes()[0]);
  callLog = signal<string[]>(['[00:00] Call connected -> MainMenu']);

  isTerminalNode = computed(() => {
    const node = this.activeSimNode();
    return !node || node.type === 'Hangup' || node.type === 'RouteToQueue';
  });

  getNodeBadgeClass(type: IvrNodeType): string {
    switch (type) {
      case 'CollectDtmfDigits':
        return 'bg-indigo-500/20 text-indigo-300 border-indigo-500/30';
      case 'RouteToQueue':
        return 'bg-emerald-500/20 text-emerald-300 border-emerald-500/30';
      case 'VoicemailDrop':
        return 'bg-amber-500/20 text-amber-300 border-amber-500/30';
      case 'Hangup':
        return 'bg-rose-500/20 text-rose-300 border-rose-500/30';
      default:
        return 'bg-blue-500/20 text-blue-300 border-blue-500/30';
    }
  }

  getTransitionsList(transitions: Record<string, string>) {
    return Object.entries(transitions).map(([digit, target]) => ({ digit, target }));
  }

  pressKey(digit: string) {
    const current = this.activeSimNode();
    if (!current || !current.transitions) return;

    const targetName = current.transitions[digit];
    if (targetName) {
      const nextNode = this.nodes().find(n => n.name === targetName);
      if (nextNode) {
        this.activeSimNode.set(nextNode);
        this.callLog.update(logs => [...logs, `[Key ${digit}] -> ${nextNode.name}`]);
        return;
      }
    }

    this.callLog.update(logs => [...logs, `[Key ${digit}] Invalid selection for ${current.name}`]);
  }

  restartSimulation() {
    this.activeSimNode.set(this.nodes()[0]);
    this.callLog.set(['[00:00] Call reconnected -> MainMenu']);
  }
}
