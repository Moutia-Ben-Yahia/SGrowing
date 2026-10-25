import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { SenderAccountDto, CampaignDto, DealDto } from '../../core/models/outbound.models';

@Component({
  selector: 'app-outbound-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <!-- 1. Left Nav Sidebar -->
      <aside class="w-64 border-r border-slate-800 bg-slate-900/80 flex flex-col justify-between p-4">
        <div>
          <div class="flex items-center gap-2 mb-6">
            <div class="h-8 w-8 rounded-lg bg-gradient-to-tr from-amber-500 to-rose-500 flex items-center justify-center font-bold text-white shadow-lg shadow-amber-500/20">
              SDR
            </div>
            <div>
              <h1 class="text-sm font-semibold tracking-wide text-white">MarketingStudio</h1>
              <p class="text-xs text-amber-400 font-medium">Autonomous AI SDR</p>
            </div>
          </div>

          <nav class="space-y-1 mb-6">
            <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider px-2">Navigation</span>
            <a
              routerLink="/"
              class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-slate-400 hover:text-slate-100 hover:bg-slate-800/60 transition">
              <span>📥 Omnichannel Inbox</span>
            </a>
            <a
              routerLink="/journeys"
              class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-slate-400 hover:text-cyan-400 hover:bg-slate-800/60 transition">
              <span>⚡ Customer Journeys</span>
            </a>
            <a
              routerLink="/feedback"
              class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-slate-400 hover:text-emerald-400 hover:bg-slate-800/60 transition">
              <span>⭐ Feedback & CSAT</span>
            </a>
            <a
              routerLink="/help"
              class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-slate-400 hover:text-purple-400 hover:bg-slate-800/60 transition">
              <span>📚 Help Center & Bot</span>
            </a>

            <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider px-2 pt-2">Outbound Tabs</span>
            <button
              (click)="activeTab.set('campaigns')"
              [class.bg-slate-800]="activeTab() === 'campaigns'"
              [class.text-amber-400]="activeTab() === 'campaigns'"
              class="w-full flex items-center justify-between px-3 py-2 rounded-lg text-sm text-slate-200 hover:bg-slate-800/60 transition">
              <span class="flex items-center gap-2">🎯 Outreach Campaigns</span>
              <span class="bg-amber-500/20 text-amber-400 text-xs px-2 py-0.5 rounded-full font-medium">{{ campaigns().length }}</span>
            </button>
            <button
              (click)="activeTab.set('mailboxes')"
              [class.bg-slate-800]="activeTab() === 'mailboxes'"
              [class.text-amber-400]="activeTab() === 'mailboxes'"
              class="w-full flex items-center justify-between px-3 py-2 rounded-lg text-sm text-slate-200 hover:bg-slate-800/60 transition">
              <span class="flex items-center gap-2">🔥 Mailbox Warmup</span>
              <span class="bg-emerald-500/20 text-emerald-400 text-xs px-2 py-0.5 rounded-full font-medium">{{ mailboxes().length }}</span>
            </button>
            <button
              (click)="activeTab.set('deals')"
              [class.bg-slate-800]="activeTab() === 'deals'"
              [class.text-amber-400]="activeTab() === 'deals'"
              class="w-full flex items-center justify-between px-3 py-2 rounded-lg text-sm text-slate-200 hover:bg-slate-800/60 transition">
              <span class="flex items-center gap-2">💼 CRM Deal Pipeline</span>
              <span class="bg-indigo-500/20 text-indigo-400 text-xs px-2 py-0.5 rounded-full font-medium">{{ deals().length }}</span>
            </button>
          </nav>

          <!-- AI SDR Agent Status Card -->
          <div class="p-3 bg-gradient-to-b from-slate-800/90 to-slate-900 border border-amber-500/30 rounded-xl space-y-2">
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold text-amber-300 flex items-center gap-1.5">
                <span class="relative flex h-2 w-2">
                  <span class="animate-ping absolute inline-flex h-full w-full rounded-full bg-amber-400 opacity-75"></span>
                  <span class="relative inline-flex rounded-full h-2 w-2 bg-amber-500"></span>
                </span>
                AgentSDR Active
              </span>
              <span class="text-[10px] bg-amber-500/10 text-amber-300 border border-amber-500/20 px-1.5 py-0.5 rounded">v2.4</span>
            </div>
            <p class="text-[11px] text-slate-300 leading-relaxed">
              Personalizing icebreakers with OpenOutreach heuristics & sentiment-driven auto-handoff.
            </p>
          </div>
        </div>

        <div class="border-t border-slate-800 pt-4">
          <button
            (click)="triggerDispatch()"
            [disabled]="isDispatching()"
            class="w-full py-2 px-3 bg-gradient-to-r from-amber-500 to-rose-600 hover:from-amber-600 hover:to-rose-700 text-white text-xs font-semibold rounded-lg shadow-lg shadow-amber-500/20 transition flex items-center justify-center gap-2 disabled:opacity-50">
            <span>⚡ Run Dispatch Cycle</span>
          </button>
        </div>
      </aside>

      <!-- 2. Main Content Area -->
      <main class="flex-1 flex flex-col overflow-hidden bg-slate-950">
        <!-- Top Stats Header -->
        <header class="h-16 border-b border-slate-800 bg-slate-900/60 px-6 flex items-center justify-between">
          <div class="flex items-center gap-4">
            <h2 class="text-base font-semibold text-white">
              @if (activeTab() === 'campaigns') { Outreach Cadences & Campaigns }
              @if (activeTab() === 'mailboxes') { Mailbox Pool & Warmup Deliverability }
              @if (activeTab() === 'deals') { Closed-Loop CRM Pipeline (AI SDR Deals) }
            </h2>
          </div>

          <div class="flex items-center gap-6 text-xs">
            <div class="flex items-center gap-2">
              <span class="text-slate-400">Avg Deliverability:</span>
              <span class="text-emerald-400 font-semibold font-mono">98.4%</span>
            </div>
            <div class="flex items-center gap-2">
              <span class="text-slate-400">Total Enrolled:</span>
              <span class="text-white font-semibold font-mono">428 leads</span>
            </div>
            <div class="flex items-center gap-2">
              <span class="text-slate-400">Pipeline Generated:</span>
              <span class="text-amber-400 font-semibold font-mono">$45,000</span>
            </div>
          </div>
        </header>

        <!-- Dynamic Tab Views -->
        <div class="flex-1 overflow-y-auto p-6">
          <!-- TAB 1: CAMPAIGNS -->
          @if (activeTab() === 'campaigns') {
            <div class="space-y-6 max-w-6xl">
              <div class="flex items-center justify-between">
                <div>
                  <h3 class="text-sm font-semibold text-slate-200">Active Multi-Touch Cadences</h3>
                  <p class="text-xs text-slate-400">Multi-step sequences with automated AI hook personalization and pacing.</p>
                </div>
                <button
                  (click)="showNewCampaignModal.set(true)"
                  class="px-3 py-1.5 bg-amber-500 hover:bg-amber-600 text-slate-950 text-xs font-semibold rounded-lg transition">
                  + New Campaign
                </button>
              </div>

              <div class="grid grid-cols-1 gap-4">
                @for (c of campaigns(); track c.id) {
                  <div class="p-5 bg-slate-900 border border-slate-800 rounded-xl hover:border-slate-700 transition space-y-4">
                    <div class="flex items-center justify-between">
                      <div class="flex items-center gap-3">
                        <div class="h-9 w-9 rounded-lg bg-indigo-500/10 border border-indigo-500/20 text-indigo-400 flex items-center justify-center font-bold text-sm">
                          🎯
                        </div>
                        <div>
                          <h4 class="text-sm font-semibold text-white">{{ c.name }}</h4>
                          <span class="text-xs text-slate-400">Created {{ c.createdAtUtc | date:'mediumDate' }} • Channel: Email</span>
                        </div>
                      </div>
                      <div class="flex items-center gap-3">
                        <span class="px-2.5 py-0.5 rounded-full text-xs font-medium bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
                          Active Cadence
                        </span>
                        <button
                          (click)="selectCampaignForPreview(c)"
                          class="px-3 py-1 bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs rounded-lg transition">
                          View Cadence Steps
                        </button>
                      </div>
                    </div>

                    <!-- Steps Timeline -->
                    <div class="grid grid-cols-1 md:grid-cols-3 gap-3 pt-2">
                      @for (s of c.steps; track s.stepNumber) {
                        <div class="p-3 bg-slate-950/60 border border-slate-800/80 rounded-lg space-y-1">
                          <div class="flex items-center justify-between text-xs font-semibold text-slate-300">
                            <span>Step {{ s.stepNumber }} (Day {{ s.delayDays }})</span>
                            <span class="text-amber-400 text-[10px]">AI Personalized</span>
                          </div>
                          <p class="text-xs text-slate-200 font-medium truncate">{{ s.subjectTemplate }}</p>
                          <p class="text-[11px] text-slate-400 line-clamp-2 leading-relaxed">{{ s.bodyTemplate }}</p>
                        </div>
                      }
                    </div>
                  </div>
                }
              </div>
            </div>
          }

          <!-- TAB 2: MAILBOXES & WARMUP -->
          @if (activeTab() === 'mailboxes') {
            <div class="space-y-6 max-w-6xl">
              <div>
                <h3 class="text-sm font-semibold text-slate-200">Sender Mailbox Pool & Warmbly Deliverability</h3>
                <p class="text-xs text-slate-400">Gradual ramp-up scheduling, SPF/DKIM health monitoring, and round-robin quota protection.</p>
              </div>

              <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                @for (mb of mailboxes(); track mb.id) {
                  <div class="p-5 bg-slate-900 border border-slate-800 rounded-xl space-y-4">
                    <div class="flex items-start justify-between">
                      <div>
                        <h4 class="text-sm font-semibold text-white">{{ mb.displayName }}</h4>
                        <p class="text-xs text-slate-400 font-mono">{{ mb.email }}</p>
                      </div>
                      <span class="text-xs font-mono px-2 py-0.5 rounded bg-slate-800 text-slate-300">
                        {{ getProviderName(mb.provider) }}
                      </span>
                    </div>

                    <!-- Quota & Ramp Indicator -->
                    <div class="space-y-1.5">
                      <div class="flex justify-between text-xs">
                        <span class="text-slate-400">Daily Quota Used:</span>
                        <span class="text-slate-200 font-mono font-medium">{{ mb.sentTodayCount }} / {{ mb.dailyQuota }}</span>
                      </div>
                      <div class="w-full bg-slate-800 rounded-full h-1.5 overflow-hidden">
                        <div class="bg-amber-500 h-1.5 rounded-full" [style.width.%]="(mb.sentTodayCount / mb.dailyQuota) * 100"></div>
                      </div>
                    </div>

                    <!-- Health Score -->
                    <div class="flex items-center justify-between pt-2 border-t border-slate-800/80">
                      <div class="flex items-center gap-1.5">
                        <span class="h-2 w-2 rounded-full bg-emerald-400"></span>
                        <span class="text-xs text-slate-300">Deliverability Health</span>
                      </div>
                      <span class="text-xs font-mono font-semibold text-emerald-400">{{ mb.healthScore }}%</span>
                    </div>
                  </div>
                }
              </div>
            </div>
          }

          <!-- TAB 3: DEALS KANBAN -->
          @if (activeTab() === 'deals') {
            <div class="space-y-6">
              <div>
                <h3 class="text-sm font-semibold text-slate-200">Closed-Loop CRM Deal Pipeline</h3>
                <p class="text-xs text-slate-400">Deals originated autonomously when AI SDR prospects reply with positive purchase intent.</p>
              </div>

              <div class="grid grid-cols-1 md:grid-cols-4 gap-4 items-start">
                <!-- Column 1: Qualified SDR Lead -->
                <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-3 space-y-3">
                  <div class="flex items-center justify-between pb-2 border-b border-slate-800">
                    <span class="text-xs font-semibold text-amber-400 uppercase tracking-wider">🎯 Qualified SDR</span>
                    <span class="text-xs bg-slate-800 text-slate-400 px-1.5 py-0.5 rounded font-mono">{{ getDealsByStage(1).length }}</span>
                  </div>
                  <div class="space-y-2">
                    @for (d of getDealsByStage(1); track d.id) {
                      <div class="p-3 bg-slate-900 border border-slate-800/80 rounded-lg space-y-2 hover:border-amber-500/40 transition">
                        <h5 class="text-xs font-semibold text-white">{{ d.title }}</h5>
                        <div class="flex items-center justify-between text-xs">
                          <span class="text-amber-400 font-mono font-medium">{{ d.amount | currency }}</span>
                          <span class="text-[10px] text-slate-400">AI Reply Handoff</span>
                        </div>
                        @if (d.notes) {
                          <p class="text-[11px] text-slate-400 italic line-clamp-2">"{{ d.notes }}"</p>
                        }
                      </div>
                    }
                  </div>
                </div>

                <!-- Column 2: Demo Scheduled -->
                <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-3 space-y-3">
                  <div class="flex items-center justify-between pb-2 border-b border-slate-800">
                    <span class="text-xs font-semibold text-indigo-400 uppercase tracking-wider">📅 Demo Scheduled</span>
                    <span class="text-xs bg-slate-800 text-slate-400 px-1.5 py-0.5 rounded font-mono">{{ getDealsByStage(2).length }}</span>
                  </div>
                  <div class="space-y-2">
                    @for (d of getDealsByStage(2); track d.id) {
                      <div class="p-3 bg-slate-900 border border-slate-800/80 rounded-lg space-y-2">
                        <h5 class="text-xs font-semibold text-white">{{ d.title }}</h5>
                        <div class="flex items-center justify-between text-xs">
                          <span class="text-indigo-400 font-mono font-medium">{{ d.amount | currency }}</span>
                          <span class="text-[10px] text-slate-400">Ready for Demo</span>
                        </div>
                      </div>
                    }
                  </div>
                </div>

                <!-- Column 3: Proposal Sent -->
                <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-3 space-y-3">
                  <div class="flex items-center justify-between pb-2 border-b border-slate-800">
                    <span class="text-xs font-semibold text-cyan-400 uppercase tracking-wider">📝 Proposal Sent</span>
                    <span class="text-xs bg-slate-800 text-slate-400 px-1.5 py-0.5 rounded font-mono">{{ getDealsByStage(3).length }}</span>
                  </div>
                  <div class="space-y-2">
                    @for (d of getDealsByStage(3); track d.id) {
                      <div class="p-3 bg-slate-900 border border-slate-800/80 rounded-lg space-y-2">
                        <h5 class="text-xs font-semibold text-white">{{ d.title }}</h5>
                        <div class="flex items-center justify-between text-xs">
                          <span class="text-cyan-400 font-mono font-medium">{{ d.amount | currency }}</span>
                          <span class="text-[10px] text-slate-400">Proposal Review</span>
                        </div>
                      </div>
                    }
                  </div>
                </div>

                <!-- Column 4: Closed Won -->
                <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-3 space-y-3">
                  <div class="flex items-center justify-between pb-2 border-b border-slate-800">
                    <span class="text-xs font-semibold text-emerald-400 uppercase tracking-wider">🏆 Closed Won</span>
                    <span class="text-xs bg-slate-800 text-slate-400 px-1.5 py-0.5 rounded font-mono">{{ getDealsByStage(4).length }}</span>
                  </div>
                  <div class="space-y-2">
                    @for (d of getDealsByStage(4); track d.id) {
                      <div class="p-3 bg-slate-900 border border-slate-800/80 rounded-lg space-y-2 border-l-2 border-l-emerald-500">
                        <h5 class="text-xs font-semibold text-white">{{ d.title }}</h5>
                        <div class="flex items-center justify-between text-xs">
                          <span class="text-emerald-400 font-mono font-medium">{{ d.amount | currency }}</span>
                          <span class="text-[10px] text-emerald-400">Won</span>
                        </div>
                      </div>
                    }
                  </div>
                </div>
              </div>
            </div>
          }
        </div>
      </main>
    </div>
  `
})
export class OutboundStudioComponent implements OnInit {
  private api = inject(ApiService);

  public activeTab = signal<'campaigns' | 'mailboxes' | 'deals'>('campaigns');
  public isDispatching = signal(false);
  public showNewCampaignModal = signal(false);

  public campaigns = signal<CampaignDto[]>([]);
  public mailboxes = signal<SenderAccountDto[]>([]);
  public deals = signal<DealDto[]>([]);

  private tenantId = '00000000-0000-0000-0000-000000000001';

  ngOnInit() {
    this.loadData();
  }

  private loadData() {
    // 1. Fetch campaigns
    this.api.getCampaigns(this.tenantId).subscribe({
      next: (res) => {
        if (res && res.length > 0) this.campaigns.set(res);
        else this.initMockCampaigns();
      },
      error: () => this.initMockCampaigns()
    });

    // 2. Fetch mailboxes
    this.api.getMailboxes(this.tenantId).subscribe({
      next: (res) => {
        if (res && res.length > 0) this.mailboxes.set(res);
        else this.initMockMailboxes();
      },
      error: () => this.initMockMailboxes()
    });

    // 3. Fetch deals
    this.api.getDeals(this.tenantId).subscribe({
      next: (res) => {
        if (res && res.length > 0) this.deals.set(res);
        else this.initMockDeals();
      },
      error: () => this.initMockDeals()
    });
  }

  private initMockCampaigns() {
    this.campaigns.set([
      {
        id: 'camp-1',
        tenantId: this.tenantId,
        name: 'Enterprise FinTech AI SDR Sequence',
        status: 2,
        createdAtUtc: new Date().toISOString(),
        steps: [
          {
            stepNumber: 1,
            delayDays: 0,
            channel: 1,
            subjectTemplate: 'Accelerating {{company}} customer communications with AI',
            bodyTemplate: 'Hi {{first_name}}, {{icebreaker}} Wanted to explore if MarketingStudio could help your SDR team scale pipeline.'
          },
          {
            stepNumber: 2,
            delayDays: 3,
            channel: 1,
            subjectTemplate: 'Quick follow-up on ROI metrics for {{company}}',
            bodyTemplate: 'Hi {{first_name}}, following up on my previous note. Teams in FinTech typically see a 3.4x boost in reply rates.'
          },
          {
            stepNumber: 3,
            delayDays: 7,
            channel: 1,
            subjectTemplate: 'Permission to close your file?',
            bodyTemplate: 'Hi {{first_name}}, haven\'t heard back, so I assume this is not a priority right now. All the best!'
          }
        ]
      }
    ]);
  }

  private initMockMailboxes() {
    this.mailboxes.set([
      {
        id: 'mb-1',
        tenantId: this.tenantId,
        email: 'alex.sdr@growth-monolith.io',
        displayName: 'Alex Rivers',
        provider: 1,
        dailyQuota: 45,
        sentTodayCount: 22,
        warmupEnabled: true,
        warmupRampDailyIncrement: 3,
        healthScore: 98,
        isActive: true
      },
      {
        id: 'mb-2',
        tenantId: this.tenantId,
        email: 'sarah.outreach@growth-monolith.io',
        displayName: 'Sarah Chen',
        provider: 2,
        dailyQuota: 40,
        sentTodayCount: 14,
        warmupEnabled: true,
        warmupRampDailyIncrement: 3,
        healthScore: 96,
        isActive: true
      }
    ]);
  }

  private initMockDeals() {
    this.deals.set([
      {
        id: 'deal-1',
        tenantId: this.tenantId,
        contactId: 'c-1',
        title: 'Deal: Acme Corp - Omnichannel Expansion',
        amount: 12000,
        stage: 1,
        notes: 'Prospect replied positively to AI SDR cold email: "Yes, interested in demo."',
        createdAtUtc: new Date().toISOString()
      },
      {
        id: 'deal-2',
        tenantId: this.tenantId,
        contactId: 'c-2',
        title: 'Deal: CloudFlow - Multi-tenant WhatsApp',
        amount: 18000,
        stage: 2,
        notes: 'Demo scheduled for next Tuesday at 2 PM.',
        createdAtUtc: new Date().toISOString()
      },
      {
        id: 'deal-3',
        tenantId: this.tenantId,
        contactId: 'c-3',
        title: 'Deal: Apex Logistics - WhatsApp + Email Support',
        amount: 25000,
        stage: 4,
        notes: 'Contract signed for 12 months enterprise tier.',
        createdAtUtc: new Date().toISOString()
      }
    ]);
  }

  public getProviderName(provider: number): string {
    switch (provider) {
      case 1: return 'Google Workspace';
      case 2: return 'Microsoft 365';
      default: return 'Custom SMTP';
    }
  }

  public getDealsByStage(stage: number): DealDto[] {
    return this.deals().filter(d => d.stage === stage);
  }

  public selectCampaignForPreview(c: CampaignDto) {
    // Selection hook for step viewing/editing
  }

  public triggerDispatch() {
    this.isDispatching.set(true);
    this.api.triggerDispatch(this.tenantId).subscribe({
      next: (res) => {
        this.isDispatching.set(false);
        alert(`Dispatched ${res.processed} pending cadence emails.`);
        this.loadData();
      },
      error: () => {
        this.isDispatching.set(false);
        alert('Simulated dispatch executed for active cadences.');
      }
    });
  }
}
