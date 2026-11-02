import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface WhatsAppFlowScreen {
  screenId: string;
  title: string;
  layoutType: string;
  terminalAction?: string;
}

export interface WhatsAppFlowDefinition {
  flowId: string;
  flowName: string;
  category: string;
  initialScreenId: string;
  screens: WhatsAppFlowScreen[];
  status: string;
  version: string;
}

@Component({
  selector: 'app-flow-interactive-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="flow-interactive-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-700/60 pb-6">
        <div>
          <div class="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-green-400">
            <span class="inline-block w-2 h-2 rounded-full bg-green-400 animate-pulse"></span>
            WhatsApp Conversational Flows • Horizon 54
          </div>
          <h1 class="text-3xl font-extrabold text-white tracking-tight mt-1">
            WhatsApp Interactive Flows & Screen DAG Engine
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Native WhatsApp Flows v3.0 screens, dynamic server data exchange, form state management & funnel drop-off analytics.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="resetDemoSession()"
            data-testid="reset-session-btn"
            class="px-4 py-2 bg-slate-700 hover:bg-slate-600 text-slate-200 rounded-lg text-sm font-semibold transition-colors flex items-center gap-2"
          >
            ↺ Restart Flow Session
          </button>
        </div>
      </div>

      <!-- KPI Overview Stats -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Published Flows</div>
          <div class="text-2xl font-bold text-white mt-1" data-testid="stat-published-flows">{{ flows().length }}</div>
          <div class="text-green-400 text-xs mt-1">WhatsApp Cloud API v3.0</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Funnel Completion Rate</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1" data-testid="stat-completion-rate">76.4%</div>
          <div class="text-slate-400 text-xs mt-1">4.2x Higher than Web Forms</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Server Exchange Latency</div>
          <div class="text-2xl font-bold text-green-400 mt-1">140 ms</div>
          <div class="text-slate-400 text-xs mt-1">Encrypted Payload Roundtrip</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Highest Drop-Off</div>
          <div class="text-2xl font-bold text-amber-400 mt-1">Screen 2 (Goal)</div>
          <div class="text-slate-400 text-xs mt-1">12.5% Drop-off Rate</div>
        </div>
      </div>

      <!-- Main Stage: Flow Screens DAG & Mobile Emulator Simulator -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <!-- Flow DAG Screen Architecture (7 cols) -->
        <div class="lg:col-span-7 bg-slate-800/80 border border-slate-700/60 rounded-xl overflow-hidden">
          <div class="p-4 border-b border-slate-700/60 flex items-center justify-between">
            <h2 class="text-lg font-bold text-white">Flow Screen DAG Architecture</h2>
            <span class="text-xs text-slate-400">{{ selectedFlow()?.version }} Schema</span>
          </div>

          <div class="p-5 space-y-4">
            <div class="space-y-3">
              @for (screen of selectedFlow()?.screens; track screen.screenId; let idx = $index) {
                <div
                  [class.border-green-500]="activeScreenId() === screen.screenId"
                  [class.bg-slate-700/40]="activeScreenId() === screen.screenId"
                  class="p-4 bg-slate-900/60 border border-slate-800 rounded-xl flex items-center justify-between transition-all"
                  [attr.data-testid]="'flow-screen-' + screen.screenId"
                >
                  <div class="flex items-center gap-3">
                    <span class="w-6 h-6 rounded-full bg-slate-800 border border-slate-700 text-xs flex items-center justify-center font-bold text-green-400">
                      {{ idx + 1 }}
                    </span>
                    <div>
                      <div class="font-bold text-sm text-white">{{ screen.title }}</div>
                      <div class="text-xs text-slate-400 font-mono mt-0.5">{{ screen.screenId }} • {{ screen.layoutType }}</div>
                    </div>
                  </div>

                  <span
                    [class]="activeScreenId() === screen.screenId ? 'bg-green-900/80 text-green-300 border-green-700' : 'bg-slate-800 text-slate-400 border-slate-700'"
                    class="px-2.5 py-1 rounded text-xs font-semibold border"
                  >
                    {{ activeScreenId() === screen.screenId ? 'Active in Emulator' : (screen.terminalAction ? 'Terminal' : 'Intermediate') }}
                  </span>
                </div>
              }
            </div>
          </div>
        </div>

        <!-- Mobile Phone Emulator Simulator (5 cols) -->
        <div class="lg:col-span-5 bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 flex flex-col items-center">
          <div class="w-full border-b border-slate-700/60 pb-3 mb-4 flex items-center justify-between">
            <h3 class="font-bold text-white text-base">WhatsApp Mobile Emulator</h3>
            <span class="text-[11px] text-green-400 font-mono bg-green-950 px-2 py-0.5 rounded border border-green-800">
              Interactive Preview
            </span>
          </div>

          <!-- Phone Chassis -->
          <div
            class="w-full max-w-[320px] bg-slate-950 border-4 border-slate-800 rounded-[32px] p-4 shadow-2xl space-y-4"
            data-testid="whatsapp-flow-emulator"
          >
            <!-- WhatsApp Top Bar -->
            <div class="flex items-center justify-between border-b border-slate-800 pb-2">
              <div class="flex items-center gap-2">
                <div class="w-6 h-6 rounded-full bg-green-600 flex items-center justify-center text-[10px] text-white font-bold">
                  WA
                </div>
                <span class="text-xs font-bold text-white">Growth OS Bot</span>
              </div>
              <span class="text-[10px] text-slate-400">12:45</span>
            </div>

            <!-- Screen 1: Lead Info -->
            @if (activeScreenId() === 'SCREEN_LEAD_INFO') {
              <div class="space-y-3 text-xs">
                <div class="font-bold text-sm text-white">Enterprise Consultation</div>
                <p class="text-[11px] text-slate-400">Enter company details to reserve briefing.</p>

                <div>
                  <label class="block text-slate-300 text-[11px] mb-1">Company Name</label>
                  <input
                    type="text"
                    [(ngModel)]="companyInput"
                    placeholder="e.g. Acme Corp"
                    data-testid="input-flow-company"
                    class="w-full bg-slate-900 border border-slate-800 rounded p-2 text-white text-xs focus:outline-none focus:border-green-500"
                  />
                </div>

                <div>
                  <label class="block text-slate-300 text-[11px] mb-1">Team Size</label>
                  <select
                    [(ngModel)]="teamSizeInput"
                    class="w-full bg-slate-900 border border-slate-800 rounded p-2 text-white text-xs focus:outline-none"
                  >
                    <option value="1-50">1-50 employees</option>
                    <option value="51-200">51-200 employees</option>
                    <option value="201-1000">201-1000 employees</option>
                  </select>
                </div>

                <button
                  (click)="goToNextScreen()"
                  data-testid="flow-next-screen-btn"
                  class="w-full py-2 bg-green-600 hover:bg-green-500 text-white font-semibold rounded-lg text-xs shadow transition-colors"
                >
                  Continue →
                </button>
              </div>
            }

            <!-- Screen 2: Objective -->
            @if (activeScreenId() === 'SCREEN_USE_CASE') {
              <div class="space-y-3 text-xs">
                <div class="font-bold text-sm text-white">Select Primary Objective</div>
                <p class="text-[11px] text-slate-400">Select the main capability you want to evaluate.</p>

                <div class="space-y-2">
                  <label class="flex items-center gap-2 p-2 bg-slate-900 rounded border border-slate-800 cursor-pointer">
                    <input type="radio" name="goal" [(ngModel)]="selectedGoal" value="Autonomous Voice SDR" />
                    <span class="text-white text-xs">Autonomous Voice SDR</span>
                  </label>
                  <label class="flex items-center gap-2 p-2 bg-slate-900 rounded border border-slate-800 cursor-pointer">
                    <input type="radio" name="goal" [(ngModel)]="selectedGoal" value="High-Throughput WhatsApp Engine" />
                    <span class="text-white text-xs">High-Throughput WhatsApp Engine</span>
                  </label>
                </div>

                <button
                  (click)="submitFlow()"
                  data-testid="flow-submit-btn"
                  class="w-full py-2 bg-green-600 hover:bg-green-500 text-white font-semibold rounded-lg text-xs shadow transition-colors"
                >
                  Confirm & Book Consultation
                </button>
              </div>
            }

            <!-- Screen 3: Terminal Confirmation -->
            @if (activeScreenId() === 'SUCCESS_SCREEN') {
              <div class="text-center py-6 space-y-3" data-testid="flow-success-card">
                <div class="w-10 h-10 rounded-full bg-green-900/60 border border-green-700 text-green-400 flex items-center justify-center mx-auto text-lg font-bold">
                  ✓
                </div>
                <div class="font-bold text-white text-sm">Consultation Confirmed!</div>
                <p class="text-[11px] text-slate-400">
                  Meeting reserved for <strong class="text-white">{{ companyInput }}</strong>. Calendar invite dispatched via WhatsApp.
                </p>
              </div>
            }
          </div>
        </div>
      </div>
    </div>
  `
})
export class FlowInteractiveCockpitComponent {
  readonly flows = signal<WhatsAppFlowDefinition[]>([
    {
      flowId: 'flow_vip_booking_v3',
      flowName: 'VIP Enterprise Demo & Consultation Flow',
      category: 'LEAD_GENERATION',
      initialScreenId: 'SCREEN_LEAD_INFO',
      status: 'Published',
      version: '3.0',
      screens: [
        {
          screenId: 'SCREEN_LEAD_INFO',
          title: 'Enterprise Consultation Booking',
          layoutType: 'FormLayout'
        },
        {
          screenId: 'SCREEN_USE_CASE',
          title: 'Select Primary Growth Objective',
          layoutType: 'SingleChoiceList',
          terminalAction: 'SUBMIT_FLOW'
        }
      ]
    }
  ]);

  readonly selectedFlow = signal<WhatsAppFlowDefinition | null>(this.flows()[0]);
  readonly activeScreenId = signal<string>('SCREEN_LEAD_INFO');

  companyInput = 'Stellar Enterprises';
  teamSizeInput = '201-1000';
  selectedGoal = 'Autonomous Voice SDR';

  goToNextScreen(): void {
    this.activeScreenId.set('SCREEN_USE_CASE');
  }

  submitFlow(): void {
    this.activeScreenId.set('SUCCESS_SCREEN');
  }

  resetDemoSession(): void {
    this.activeScreenId.set('SCREEN_LEAD_INFO');
  }
}
