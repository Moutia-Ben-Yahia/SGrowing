import { Component, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslationService } from '../../core/services/translation.service';
import { LanguageSwitcherComponent } from '../../core/components/language-switcher.component';

export interface SnapshotItem {
  id: string;
  name: string;
  category: string;
  version: string;
  description: string;
  createdAt: string;
}

export interface WaterfallStep {
  provider: string;
  status: 'Waiting' | 'Querying' | 'Matched' | 'Cascaded';
  confidence: number;
  latencyMs: number;
}

@Component({
  selector: 'app-agency-snapshot-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LanguageSwitcherComponent],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans" dir="ltr">
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Top Navigation Header -->
        <header class="h-14 border-b border-slate-800 bg-slate-900/80 backdrop-blur-md px-6 flex items-center justify-between shrink-0 sticky top-0 z-20">
          <div class="flex items-center gap-3">
            <a routerLink="/accounts" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← {{ translation.t('nav.accounts') }}
            </a>
            <span class="text-slate-700">|</span>
            <span class="text-base font-bold text-white flex items-center gap-2">
              📦 Agency Snapshot Engine & Usage Rebilling
            </span>
            <span class="bg-indigo-500/20 text-indigo-300 text-[10px] font-mono px-2 py-0.5 rounded-full border border-indigo-500/30">
              GoHighLevel & Clay Architecture
            </span>
          </div>

          <div class="flex items-center gap-3">
            <app-language-switcher></app-language-switcher>
            <button
              (click)="openCreateSnapshotModal.set(true)"
              class="px-3 py-1.5 rounded-lg bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold flex items-center gap-1.5 transition shadow-sm">
              + Create Master Snapshot
            </button>
          </div>
        </header>

        <!-- Notification Toast -->
        @if (toastMessage()) {
          <div class="mx-6 mt-4 px-4 py-2 bg-emerald-500/20 border border-emerald-500/40 text-emerald-300 text-xs rounded-lg flex items-center justify-between">
            <span>{{ toastMessage() }}</span>
            <button (click)="toastMessage.set('')" class="text-emerald-400 hover:text-white">✕</button>
          </div>
        }

        <div class="p-6 space-y-6">
          <!-- 1. Agency Blueprint Snapshots Table -->
          <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-5 shadow-sm space-y-4">
            <div class="flex items-center justify-between pb-3 border-b border-slate-800/80">
              <div class="flex items-center gap-2">
                <span class="text-base">🚀</span>
                <div>
                  <h3 class="text-sm font-bold text-white">Agency Turnkey Blueprint Snapshots</h3>
                  <p class="text-xs text-slate-400">1-click complete client deployment (funnels, automations, custom fields & pipelines)</p>
                </div>
              </div>
              <span class="text-xs text-slate-400 font-mono">{{ snapshots().length }} Snapshots ready</span>
            </div>

            <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
              @for (snap of snapshots(); track snap.id) {
                <div class="bg-slate-950/70 border border-slate-800/80 rounded-xl p-4 flex flex-col justify-between hover:border-indigo-500/40 transition">
                  <div>
                    <div class="flex items-center justify-between mb-2">
                      <span class="text-[10px] font-mono uppercase px-2 py-0.5 rounded bg-indigo-500/20 text-indigo-300 border border-indigo-500/30">
                        {{ snap.category }}
                      </span>
                      <span class="text-xs font-mono text-slate-500">v{{ snap.version }}</span>
                    </div>
                    <h4 class="text-sm font-bold text-white mb-1">{{ snap.name }}</h4>
                    <p class="text-xs text-slate-400 line-clamp-2 mb-4">{{ snap.description }}</p>
                  </div>
                  <div class="pt-3 border-t border-slate-800/60 flex items-center justify-between">
                    <span class="text-[11px] text-slate-500">{{ snap.createdAt }}</span>
                    <button
                      (click)="deploySnapshot(snap)"
                      class="px-2.5 py-1 bg-indigo-600/30 hover:bg-indigo-600/50 text-indigo-300 border border-indigo-500/40 rounded-lg text-xs font-medium transition">
                      ⚡ 1-Click Deploy
                    </button>
                  </div>
                </div>
              }
            </div>
          </div>

          <!-- 2. Two-Column Grid: Rebilling Markup & Waterfall Enrichment -->
          <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
            <!-- Rebilling Calculator (GoHighLevel SaaS Mode) -->
            <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-5 shadow-sm space-y-4">
              <div class="flex items-center justify-between pb-3 border-b border-slate-800/80">
                <div class="flex items-center gap-2">
                  <span class="text-base">💳</span>
                  <div>
                    <h3 class="text-sm font-bold text-white">Wholesale-to-Retail Rebilling Margins</h3>
                    <p class="text-xs text-slate-400">Configure agency profit multipliers on telephony, SMS & AI tokens</p>
                  </div>
                </div>
                <span class="text-xs text-emerald-400 font-semibold">Active Margin: +{{ calculateOverallMargin() }}%</span>
              </div>

              <div class="space-y-4 text-xs font-sans">
                <div>
                  <div class="flex justify-between text-slate-300 mb-1">
                    <span>SMS Multiplier (Wholesale: &dollar;0.01 / msg)</span>
                    <span class="font-mono text-emerald-400 font-bold">{{ smsMultiplier() }}x (&dollar;{{ (0.01 * smsMultiplier()).toFixed(3) }})</span>
                  </div>
                  <input type="range" min="1.0" max="3.0" step="0.05" [(ngModel)]="smsMultiplier" class="w-full accent-indigo-500" />
                </div>

                <div>
                  <div class="flex justify-between text-slate-300 mb-1">
                    <span>Voice Telephony Multiplier (Wholesale: &dollar;0.025 / min)</span>
                    <span class="font-mono text-emerald-400 font-bold">{{ voiceMultiplier() }}x (&dollar;{{ (0.025 * voiceMultiplier()).toFixed(3) }})</span>
                  </div>
                  <input type="range" min="1.0" max="3.0" step="0.05" [(ngModel)]="voiceMultiplier" class="w-full accent-indigo-500" />
                </div>

                <div>
                  <div class="flex justify-between text-slate-300 mb-1">
                    <span>AI Model Tokens Multiplier (Wholesale: &dollar;0.002 / 1k tokens)</span>
                    <span class="font-mono text-emerald-400 font-bold">{{ aiMultiplier() }}x (&dollar;{{ (0.002 * aiMultiplier()).toFixed(4) }})</span>
                  </div>
                  <input type="range" min="1.0" max="3.0" step="0.05" [(ngModel)]="aiMultiplier" class="w-full accent-indigo-500" />
                </div>

                <div class="bg-slate-950 p-3.5 rounded-lg border border-slate-800 flex items-center justify-between text-xs">
                  <div>
                    <div class="text-slate-400">Estimated Monthly Rebilled Volume</div>
                    <div class="text-white font-bold text-sm mt-0.5 font-mono">&dollar;4,850.00 Gross Retail</div>
                  </div>
                  <div class="text-right">
                    <div class="text-slate-400">Net Agency Monthly Margin</div>
                    <div class="text-emerald-400 font-bold text-sm mt-0.5 font-mono">+&dollar;{{ calculateNetProfit() }}</div>
                  </div>
                </div>
              </div>
            </div>

            <!-- Waterfall Prospect Enrichment Cascade (Clay.com) -->
            <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-5 shadow-sm space-y-4">
              <div class="flex items-center justify-between pb-3 border-b border-slate-800/80">
                <div class="flex items-center gap-2">
                  <span class="text-base">💧</span>
                  <div>
                    <h3 class="text-sm font-bold text-white">Waterfall Lead Enrichment Cascade</h3>
                    <p class="text-xs text-slate-400">Sequential multi-provider cascade with 85%+ verified hit rate</p>
                  </div>
                </div>
                <button
                  (click)="simulateWaterfallEnrichment()"
                  class="px-2.5 py-1 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-xs font-semibold transition">
                  ⚡ Test Cascade
                </button>
              </div>

              <div class="space-y-3">
                <div class="flex items-center gap-2">
                  <input
                    type="text"
                    [(ngModel)]="prospectQueryInput"
                    placeholder="e.g. Yassine Ben Salah (moncef@biat.tn)"
                    class="flex-1 bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-white focus:outline-none focus:border-indigo-500 font-mono" />
                </div>

                <div class="space-y-2">
                  @for (step of waterfallSteps(); track step.provider) {
                    <div class="bg-slate-950/70 border border-slate-800/80 rounded-lg p-2.5 flex items-center justify-between text-xs font-mono">
                      <div class="flex items-center gap-2">
                        <span class="w-5 h-5 rounded-full flex items-center justify-center text-[10px] font-bold"
                          [ngClass]="step.status === 'Matched' ? 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30' : (step.status === 'Cascaded' ? 'bg-slate-800 text-slate-500' : 'bg-indigo-500/20 text-indigo-300')">
                          {{ step.status === 'Matched' ? '✓' : (step.status === 'Cascaded' ? '✕' : '•') }}
                        </span>
                        <span class="text-white font-medium">{{ step.provider }}</span>
                      </div>
                      <div class="flex items-center gap-3">
                        <span class="text-slate-500">{{ step.latencyMs }}ms</span>
                        <span class="px-2 py-0.5 rounded text-[10px]"
                          [ngClass]="step.status === 'Matched' ? 'bg-emerald-500/20 text-emerald-300 font-bold' : (step.status === 'Cascaded' ? 'text-slate-500' : 'text-indigo-300')">
                          {{ step.status }}
                        </span>
                      </div>
                    </div>
                  }
                </div>

                @if (enrichedOutput()) {
                  <div class="bg-emerald-950/20 border border-emerald-500/40 rounded-lg p-3 text-xs font-mono text-emerald-300 space-y-1">
                    <div class="font-bold text-white flex items-center gap-1">✓ Verified Contact Discovered:</div>
                    <div>Email: <span class="text-white">{{ enrichedOutput()!.email }}</span></div>
                    <div>Phone: <span class="text-white">{{ enrichedOutput()!.phone }}</span></div>
                    <div>Confidence: <span class="text-white font-bold">{{ enrichedOutput()!.confidence }}%</span> ({{ enrichedOutput()!.provider }})</div>
                  </div>
                }
              </div>
            </div>
          </div>
        </div>
      </main>
    </div>
  `
})
export class AgencySnapshotCockpitComponent {
  translation = inject(TranslationService);

  toastMessage = signal<string>('');
  openCreateSnapshotModal = signal<boolean>(false);

  smsMultiplier = signal<number>(1.40);
  voiceMultiplier = signal<number>(1.25);
  aiMultiplier = signal<number>(1.50);

  prospectQueryInput = 'Moncef Marzouki (biat.com.tn)';

  snapshots = signal<SnapshotItem[]>([
    {
      id: 'snap-1',
      name: 'Tunisia High-Conversion COD Funnel & Call Center',
      category: 'COD E-Commerce',
      version: '2.1.0',
      description: 'Pre-configured 1-step checkout, WinSMS confirmation workflows, 24 Gouvernorats cascade, and anti-collision call center setup.',
      createdAt: '2 days ago'
    },
    {
      id: 'snap-2',
      name: 'B2B Enterprise Cold Outbound & Waterfall Enrichment',
      category: 'SDR Outbound',
      version: '1.4.0',
      description: 'Multi-inbox rotation, Clay-style waterfall cascades (Hunter/Apollo), and stop-on-reply CRM handoff automation.',
      createdAt: 'Last week'
    },
    {
      id: 'snap-3',
      name: 'Omnichannel Customer Support & Post-Resolution CSAT',
      category: 'Customer Support',
      version: '1.0.0',
      description: 'Collision warning banners, automated WhatsApp 1-click CSAT survey upon ticket resolution, and SLA escalation policies.',
      createdAt: 'Yesterday'
    }
  ]);

  waterfallSteps = signal<WaterfallStep[]>([
    { provider: 'Hunter.io (Step 1)', status: 'Cascaded', confidence: 45, latencyMs: 140 },
    { provider: 'Apollo.io (Step 2)', status: 'Matched', confidence: 94, latencyMs: 185 },
    { provider: 'Dropcontact (Step 3)', status: 'Waiting', confidence: 0, latencyMs: 0 },
    { provider: 'Prospeo (Step 4)', status: 'Waiting', confidence: 0, latencyMs: 0 }
  ]);

  enrichedOutput = signal<{ email: string; phone: string; confidence: number; provider: string } | null>({
    email: 'moncef.marzouki@biat.com.tn',
    phone: '+216 71 889 120',
    confidence: 94,
    provider: 'Apollo.io'
  });

  calculateOverallMargin(): number {
    const avg = ((this.smsMultiplier() + this.voiceMultiplier() + this.aiMultiplier()) / 3 - 1.0) * 100;
    return Math.round(avg);
  }

  calculateNetProfit(): string {
    const gross = 4850.0;
    const marginRatio = (this.calculateOverallMargin() / 100);
    const profit = gross * (marginRatio / (1 + marginRatio));
    return profit.toFixed(2);
  }

  deploySnapshot(snapshot: SnapshotItem): void {
    this.toastMessage.set(`Successfully deployed '${snapshot.name}' into target client sub-account with custom token resolution!`);
  }

  simulateWaterfallEnrichment(): void {
    this.waterfallSteps.set([
      { provider: 'Hunter.io (Step 1)', status: 'Cascaded', confidence: 42, latencyMs: 135 },
      { provider: 'Apollo.io (Step 2)', status: 'Matched', confidence: 96, latencyMs: 190 },
      { provider: 'Dropcontact (Step 3)', status: 'Waiting', confidence: 0, latencyMs: 0 },
      { provider: 'Prospeo (Step 4)', status: 'Waiting', confidence: 0, latencyMs: 0 }
    ]);
    this.enrichedOutput.set({
      email: `${this.prospectQueryInput.split(' ')[0].toLowerCase()}@biat.com.tn`,
      phone: '+216 71 340 500',
      confidence: 96,
      provider: 'Apollo.io'
    });
    this.toastMessage.set('Waterfall cascade completed: match achieved at Step 2 (Apollo.io) with 96% confidence.');
  }
}
