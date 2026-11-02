import { Component, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslationService } from '../../core/services/translation.service';
import { LanguageSwitcherComponent } from '../../core/components/language-switcher.component';

export interface IdentityNode {
  id: string;
  type: 'AnonymousCookie' | 'DeviceId' | 'Email' | 'PhoneNumber' | 'CustomerId';
  value: string;
  confidence: number;
  isKnown: boolean;
  linkedAt: string;
}

export interface BrowsingSession {
  sessionId: string;
  anonymousId: string;
  startedAt: string;
  pageViews: string[];
  utmSource: string;
  utmCampaign: string;
}

export interface MergeAuditLog {
  id: string;
  sourceProfileId: string;
  targetProfileId: string;
  triggerIdentifier: string;
  confidenceScore: number;
  timestamp: string;
  status: string;
}

@Component({
  selector: 'app-identity-graph-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LanguageSwitcherComponent],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans" dir="ltr">
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Top Header Navigation -->
        <header class="h-14 border-b border-slate-800 bg-slate-900/80 backdrop-blur-md px-6 flex items-center justify-between shrink-0 sticky top-0 z-20">
          <div class="flex items-center gap-3">
            <a routerLink="/crm" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← CRM
            </a>
            <span class="text-slate-700">|</span>
            <span class="text-base font-bold text-white flex items-center gap-2">
              🕸️ Customer 360 Identity Graph & Visitor Session Stitcher Cockpit
            </span>
            <span class="bg-cyan-500/20 text-cyan-300 text-[10px] font-mono px-2 py-0.5 rounded-full border border-cyan-500/30">
              Domain B3 • customer_data.identities • Graph Resolution
            </span>
          </div>

          <div class="flex items-center gap-3">
            <app-language-switcher></app-language-switcher>
            <button
              (click)="resetDemoGraph()"
              class="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg text-xs font-semibold transition border border-slate-700">
              🔄 Reset Graph
            </button>
          </div>
        </header>

        <!-- KPI Metrics Ribbon -->
        <section class="grid grid-cols-1 md:grid-cols-4 gap-4 p-6 border-b border-slate-800/80 bg-slate-900/40">
          <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Resolved Profiles</div>
            <div class="text-2xl font-bold text-cyan-400 mt-1">{{ totalProfiles() }}</div>
            <div class="text-[11px] text-slate-500 mt-1">Multi-device unified entities</div>
          </div>

          <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Stitched Sessions</div>
            <div class="text-2xl font-bold text-emerald-400 mt-1">{{ totalSessions() }}</div>
            <div class="text-[11px] text-slate-500 mt-1">Anonymous-to-known merged</div>
          </div>

          <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Deterministic Match Rate</div>
            <div class="text-2xl font-bold text-indigo-400 mt-1">94.8%</div>
            <div class="text-[11px] text-slate-500 mt-1">Email, Phone & Customer ID</div>
          </div>

          <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Active Graph Nodes</div>
            <div class="text-2xl font-bold text-amber-400 mt-1">{{ identityNodes().length }}</div>
            <div class="text-[11px] text-slate-500 mt-1">In current cluster view</div>
          </div>
        </section>

        <!-- Main Workbench Layout -->
        <div class="flex-1 p-6 grid grid-cols-1 lg:grid-cols-3 gap-6">
          <!-- Left Column: Visual Graph Nodes & Stitching Form -->
          <div class="lg:col-span-2 space-y-6">
            <!-- Stitching Action Card -->
            <div class="p-5 rounded-2xl bg-slate-900/90 border border-slate-800 shadow-xl">
              <h3 class="text-sm font-semibold text-white flex items-center gap-2 mb-3">
                <span>🔗</span> Stitch Anonymous Visitor to Known Profile
              </h3>
              <p class="text-xs text-slate-400 mb-4">
                Simulate a web visitor who browsed anonymously, then submitted a lead form, made a purchase, or authenticated.
              </p>

              <div class="grid grid-cols-1 md:grid-cols-3 gap-3 mb-4">
                <div>
                  <label class="block text-[11px] text-slate-400 mb-1">Anonymous Cookie ID</label>
                  <input
                    type="text"
                    [(ngModel)]="newAnonId"
                    placeholder="cookie_anon_999"
                    class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-slate-200 focus:outline-none focus:border-cyan-500" />
                </div>

                <div>
                  <label class="block text-[11px] text-slate-400 mb-1">Identifier Type</label>
                  <select
                    [(ngModel)]="newIdType"
                    class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-slate-200 focus:outline-none focus:border-cyan-500">
                    <option value="Email">Verified Email (1.00 Conf)</option>
                    <option value="PhoneNumber">E.164 Phone (0.95 Conf)</option>
                    <option value="CustomerId">CRM Customer ID (1.00 Conf)</option>
                    <option value="DeviceId">Device Fingerprint (0.85 Conf)</option>
                  </select>
                </div>

                <div>
                  <label class="block text-[11px] text-slate-400 mb-1">Identifier Value</label>
                  <input
                    type="text"
                    [(ngModel)]="newIdValue"
                    placeholder="user@enterprise.com"
                    class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-slate-200 focus:outline-none focus:border-cyan-500" />
                </div>
              </div>

              <div class="flex items-center justify-between">
                <span class="text-xs text-slate-500">
                  Target Canonical Profile: <span class="font-mono text-cyan-400">{{ activeProfileId() }}</span>
                </span>
                <button
                  (click)="stitchIdentity()"
                  data-testid="stitch-identity-btn"
                  class="px-4 py-2 bg-cyan-600 hover:bg-cyan-500 text-white rounded-lg text-xs font-semibold transition flex items-center gap-1.5 shadow-md shadow-cyan-900/30">
                  <span>⚡</span> Execute Identity Stitch
                </button>
              </div>
            </div>

            <!-- Identity Graph Visual Nodes -->
            <div class="p-5 rounded-2xl bg-slate-900/90 border border-slate-800 shadow-xl">
              <div class="flex items-center justify-between mb-4">
                <h3 class="text-sm font-semibold text-white flex items-center gap-2">
                  <span>🌐</span> Canonical Profile Identity Cluster Graph
                </h3>
                <span class="text-xs text-slate-400">Profile ID: <strong class="text-white">{{ activeProfileId() }}</strong></span>
              </div>

              <div class="grid grid-cols-1 md:grid-cols-2 gap-3">
                @for (node of identityNodes(); track node.id) {
                  <div
                    class="p-3.5 rounded-xl border transition flex items-center justify-between"
                    [class.bg-cyan-950/20]="node.isKnown"
                    [class.border-cyan-800/50]="node.isKnown"
                    [class.bg-slate-950]="!node.isKnown"
                    [class.border-slate-800]="!node.isKnown">
                    <div class="flex items-center gap-3">
                      <div
                        class="w-8 h-8 rounded-lg flex items-center justify-center text-sm font-bold"
                        [class.bg-cyan-600/30]="node.isKnown"
                        [class.text-cyan-300]="node.isKnown"
                        [class.bg-slate-800]="!node.isKnown"
                        [class.text-slate-400]="!node.isKnown">
                        {{ node.type === 'Email' ? '✉️' : (node.type === 'PhoneNumber' ? '📱' : (node.type === 'CustomerId' ? '🆔' : '🍪')) }}
                      </div>
                      <div>
                        <div class="text-xs font-semibold text-slate-200">{{ node.value }}</div>
                        <div class="text-[10px] text-slate-500 font-mono">{{ node.type }} • Linked: {{ node.linkedAt }}</div>
                      </div>
                    </div>

                    <div class="text-right">
                      <span
                        class="text-[10px] font-mono px-2 py-0.5 rounded-full border"
                        [class.bg-emerald-500/20]="node.confidence >= 0.95"
                        [class.text-emerald-300]="node.confidence >= 0.95"
                        [class.border-emerald-500/30]="node.confidence >= 0.95"
                        [class.bg-amber-500/20]="node.confidence < 0.95"
                        [class.text-amber-300]="node.confidence < 0.95"
                        [class.border-amber-500/30]="node.confidence < 0.95">
                        {{ (node.confidence * 100).toFixed(0) }}% Conf
                      </span>
                    </div>
                  </div>
                }
              </div>
            </div>

            <!-- Merged Session Timeline -->
            <div class="p-5 rounded-2xl bg-slate-900/90 border border-slate-800 shadow-xl">
              <h3 class="text-sm font-semibold text-white flex items-center gap-2 mb-3">
                <span>⏱️</span> Multi-Device Stitched Browsing Sessions
              </h3>
              <div class="space-y-3">
                @for (sess of sessions(); track sess.sessionId) {
                  <div class="p-3 rounded-xl bg-slate-950 border border-slate-800 flex items-start justify-between">
                    <div>
                      <div class="text-xs font-semibold text-slate-300 flex items-center gap-2">
                        <span>{{ sess.sessionId }}</span>
                        <span class="text-[10px] text-slate-500 font-mono">Cookie: {{ sess.anonymousId }}</span>
                      </div>
                      <div class="text-[11px] text-slate-400 mt-1">
                        Pageviews:
                        <span class="text-cyan-400 font-mono">{{ sess.pageViews.join(' → ') }}</span>
                      </div>
                    </div>
                    <div class="text-right text-[10px] text-slate-500 font-mono">
                      <div>{{ sess.utmSource }} / {{ sess.utmCampaign }}</div>
                      <div>{{ sess.startedAt }}</div>
                    </div>
                  </div>
                }
              </div>
            </div>
          </div>

          <!-- Right Column: Profile Merge Audit Logs & Traits -->
          <div class="space-y-6">
            <!-- Unified Traits -->
            <div class="p-5 rounded-2xl bg-slate-900/90 border border-slate-800 shadow-xl">
              <h3 class="text-sm font-semibold text-white flex items-center gap-2 mb-3">
                <span>🏷️</span> Unified Profile Traits
              </h3>
              <div class="space-y-2 text-xs">
                @for (trait of traitsList(); track trait.key) {
                  <div class="flex items-center justify-between p-2 rounded-lg bg-slate-950 border border-slate-800/80">
                    <span class="text-slate-400 font-mono text-[11px]">{{ trait.key }}</span>
                    <span class="text-slate-200 font-semibold">{{ trait.value }}</span>
                  </div>
                }
              </div>
            </div>

            <!-- Audit Trail -->
            <div class="p-5 rounded-2xl bg-slate-900/90 border border-slate-800 shadow-xl">
              <h3 class="text-sm font-semibold text-white flex items-center gap-2 mb-3">
                <span>📜</span> Profile Merge Audit Trail
              </h3>
              <div class="space-y-3">
                @for (log of auditLogs(); track log.id) {
                  <div class="p-3 rounded-xl bg-slate-950 border border-slate-800">
                    <div class="text-[11px] font-semibold text-cyan-400">{{ log.triggerIdentifier }}</div>
                    <div class="text-[10px] text-slate-400 mt-0.5">
                      Merged <span class="font-mono text-slate-300">{{ log.sourceProfileId }}</span> into <span class="font-mono text-slate-300">{{ log.targetProfileId }}</span>
                    </div>
                    <div class="flex items-center justify-between text-[10px] text-slate-500 mt-1 font-mono">
                      <span>{{ log.status }}</span>
                      <span>{{ log.timestamp }}</span>
                    </div>
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
export class IdentityGraphCockpitComponent {
  public readonly translation: TranslationService;

  constructor(translationService?: TranslationService) {
    if (translationService) {
      this.translation = translationService;
    } else {
      try {
        this.translation = inject(TranslationService);
      } catch {
        this.translation = new TranslationService();
      }
    }
  }

  public activeProfileId = signal<string>('prof_c0a80199_canonical');
  public totalProfiles = signal<number>(1420);
  public totalSessions = signal<number>(3890);

  public newAnonId = 'cookie_anon_4812';
  public newIdType = 'Email';
  public newIdValue = 'sarah.growth@enterprise.com';

  public identityNodes = signal<IdentityNode[]>([
    {
      id: 'node-1',
      type: 'Email',
      value: 'sarah.growth@enterprise.com',
      confidence: 1.0,
      isKnown: true,
      linkedAt: '2026-11-01 14:22 UTC'
    },
    {
      id: 'node-2',
      type: 'PhoneNumber',
      value: '+1 (555) 492-1920',
      confidence: 0.95,
      isKnown: true,
      linkedAt: '2026-11-01 14:25 UTC'
    },
    {
      id: 'node-3',
      type: 'CustomerId',
      value: 'CUST-ENT-9021',
      confidence: 1.0,
      isKnown: true,
      linkedAt: '2026-11-02 08:10 UTC'
    },
    {
      id: 'node-4',
      type: 'AnonymousCookie',
      value: 'cookie_anon_laptop_88',
      confidence: 0.75,
      isKnown: false,
      linkedAt: '2026-10-30 09:15 UTC'
    }
  ]);

  public sessions = signal<BrowsingSession[]>([
    {
      sessionId: 'sess_101',
      anonymousId: 'cookie_anon_laptop_88',
      startedAt: '2026-10-30 09:15 UTC',
      pageViews: ['/solutions', '/pricing', '/demo-request'],
      utmSource: 'google_ads',
      utmCampaign: 'enterprise_q4'
    },
    {
      sessionId: 'sess_102',
      anonymousId: 'cookie_anon_mobile_99',
      startedAt: '2026-11-01 14:20 UTC',
      pageViews: ['/blog/growth-os-architecture', '/checkout'],
      utmSource: 'linkedin',
      utmCampaign: 'b2b_growth'
    }
  ]);

  public unifiedTraits = signal<Record<string, string>>({
    'Company': 'Megacorp Global Ltd',
    'Role': 'VP Revenue Operations',
    'LeadScore': '96',
    'LifecycleStage': 'Opportunity',
    'PreferredChannel': 'WhatsApp',
    'Country': 'United States'
  });

  public traitsList = computed(() => {
    return Object.entries(this.unifiedTraits()).map(([key, value]) => ({ key, value }));
  });

  public auditLogs = signal<MergeAuditLog[]>([
    {
      id: 'log-1',
      sourceProfileId: 'prof_anon_9823',
      targetProfileId: 'prof_c0a80199_canonical',
      triggerIdentifier: 'Email: sarah.growth@enterprise.com',
      confidenceScore: 1.0,
      timestamp: '2026-11-01 14:22:10 UTC',
      status: 'Merged 2 Sessions & 4 Traits'
    },
    {
      id: 'log-2',
      sourceProfileId: 'prof_mobile_7712',
      targetProfileId: 'prof_c0a80199_canonical',
      triggerIdentifier: 'Phone: +1 (555) 492-1920',
      confidenceScore: 0.95,
      timestamp: '2026-11-01 14:25:30 UTC',
      status: 'Linked Device & SMS Channel'
    }
  ]);

  public stitchIdentity(): void {
    if (!this.newIdValue || !this.newAnonId) return;

    const conf = this.newIdType === 'Email' || this.newIdType === 'CustomerId' ? 1.0 : (this.newIdType === 'PhoneNumber' ? 0.95 : 0.85);

    // Add new identity node
    const newNode: IdentityNode = {
      id: `node-${Date.now()}`,
      type: this.newIdType as any,
      value: this.newIdValue,
      confidence: conf,
      isKnown: true,
      linkedAt: 'Just now'
    };

    const newAnonNode: IdentityNode = {
      id: `node-anon-${Date.now()}`,
      type: 'AnonymousCookie',
      value: this.newAnonId,
      confidence: 0.75,
      isKnown: false,
      linkedAt: 'Just now'
    };

    this.identityNodes.update(nodes => [newNode, newAnonNode, ...nodes]);
    this.totalProfiles.update(c => c + 1);
    this.totalSessions.update(c => c + 1);

    // Add session
    this.sessions.update(s => [
      {
        sessionId: `sess_${Date.now().toString().slice(-4)}`,
        anonymousId: this.newAnonId,
        startedAt: 'Just now',
        pageViews: ['/landing-page', '/lead-capture', '/thank-you'],
        utmSource: 'direct',
        utmCampaign: 'organic_search'
      },
      ...s
    ]);

    // Record audit log
    this.auditLogs.update(logs => [
      {
        id: `log-${Date.now()}`,
        sourceProfileId: `prof_${this.newAnonId.slice(-6)}`,
        targetProfileId: this.activeProfileId(),
        triggerIdentifier: `${this.newIdType}: ${this.newIdValue}`,
        confidenceScore: conf,
        timestamp: 'Just now',
        status: `Stitched ${this.newAnonId} to canonical profile`
      },
      ...logs
    ]);
  }

  public resetDemoGraph(): void {
    this.activeProfileId.set('prof_c0a80199_canonical');
    this.totalProfiles.set(1420);
    this.totalSessions.set(3890);
  }
}
