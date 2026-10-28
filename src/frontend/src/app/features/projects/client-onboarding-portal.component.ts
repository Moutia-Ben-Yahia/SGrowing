import { Component, signal, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';

export interface ClientPortalMilestone {
  id: string;
  title: string;
  description: string;
  status: 'Backlog' | 'Todo' | 'InProgress' | 'InReview' | 'Done';
  priority: 'Urgent' | 'High' | 'Medium' | 'Low';
  subtasksCompleted: number;
  subtasksTotal: number;
  progressPercent: number;
  dueDate: string;
  latestClientVote?: 'Approved' | 'ChangeRequested' | 'InReview';
}

export interface ClientPortalFeedbackEntry {
  id: string;
  clientName: string;
  milestoneTitle: string;
  statusVote: 'Approved' | 'ChangeRequested';
  notes: string;
  timestamp: string;
}

@Component({
  selector: 'app-client-onboarding-portal',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="min-h-screen w-screen bg-slate-950 text-slate-100 font-sans flex flex-col" dir="ltr">
      <!-- Executive Brand Header -->
      <header class="h-16 border-b border-slate-800 bg-slate-900/90 backdrop-blur-md px-8 flex items-center justify-between shrink-0 sticky top-0 z-30" data-testid="client-portal-header">
        <div class="flex items-center gap-3">
          <div class="w-9 h-9 rounded-xl bg-gradient-to-tr from-indigo-600 to-emerald-400 flex items-center justify-center font-bold text-white shadow-md text-sm">
            SG
          </div>
          <div>
            <div class="text-sm font-bold text-white flex items-center gap-2">
              <span>SGrowing Client Success Portal</span>
              <span class="px-2 py-0.5 rounded-full bg-emerald-500/20 text-emerald-400 text-[10px] font-mono border border-emerald-500/30">
                Live Onboarding
              </span>
            </div>
            <div class="text-[11px] text-slate-400 font-mono">
              Secure Tokenized Stakeholder Access
            </div>
          </div>
        </div>

        <div class="flex items-center gap-4">
          <div class="text-right">
            <div class="text-xs font-semibold text-slate-200">{{ clientOrgName() }}</div>
            <div class="text-[10px] text-slate-400 font-mono">Token Expiry: {{ tokenExpiry() }}</div>
          </div>
          <div class="w-8 h-8 rounded-full bg-slate-800 border border-slate-700 flex items-center justify-center text-xs font-bold text-indigo-400">
            CL
          </div>
        </div>
      </header>

      <main class="flex-1 max-w-6xl w-full mx-auto px-6 py-8 flex flex-col gap-8">
        <!-- Hero Summary Banner -->
        <div class="relative overflow-hidden rounded-2xl bg-gradient-to-br from-indigo-950/60 via-slate-900 to-slate-900 border border-indigo-500/30 p-6 md:p-8 shadow-xl">
          <div class="relative z-10 flex flex-col md:flex-row md:items-center justify-between gap-6">
            <div class="max-w-xl">
              <span class="text-xs font-semibold uppercase tracking-wider text-indigo-400 font-mono">
                Client Implementation Roadmap
              </span>
              <h1 class="text-2xl md:text-3xl font-extrabold text-white mt-1 mb-2">
                {{ projectName() }}
              </h1>
              <p class="text-xs md:text-sm text-slate-300 leading-relaxed">
                {{ projectDescription() }}
              </p>
            </div>

            <!-- Progress Meter -->
            <div class="bg-slate-950/80 border border-slate-800 rounded-xl p-5 flex items-center gap-6 shrink-0">
              <div class="relative w-20 h-20 flex items-center justify-center">
                <svg class="w-20 h-20 transform -rotate-90" viewBox="0 0 36 36">
                  <path
                    class="text-slate-800"
                    stroke-width="3"
                    stroke="currentColor"
                    fill="none"
                    d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831" />
                  <path
                    class="text-emerald-400 transition-all duration-1000 ease-out"
                    stroke-width="3"
                    [attr.stroke-dasharray]="overallProgress() + ', 100'"
                    stroke-linecap="round"
                    stroke="currentColor"
                    fill="none"
                    d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831" />
                </svg>
                <div class="absolute text-sm font-bold text-white font-mono">
                  {{ overallProgress() }}%
                </div>
              </div>

              <div>
                <div class="text-xs font-semibold text-slate-200">Implementation Velocity</div>
                <div class="text-[11px] text-slate-400 mt-0.5">
                  <span class="text-emerald-400 font-bold font-mono">{{ completedMilestonesCount() }}</span> of
                  <span class="font-mono">{{ milestones().length }}</span> milestones completed
                </div>
                <div class="mt-2 inline-flex items-center gap-1.5 px-2 py-0.5 rounded bg-indigo-500/10 border border-indigo-500/20 text-[10px] text-indigo-300 font-mono">
                  🚀 Target Go-Live: {{ targetGoLive() }}
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Success Toast -->
        @if (feedbackSuccessMessage()) {
          <div class="p-4 rounded-xl bg-emerald-500/20 border border-emerald-500/40 text-emerald-200 text-xs flex items-center justify-between shadow-lg" data-testid="client-feedback-success">
            <div class="flex items-center gap-2">
              <span class="text-base">🎉</span>
              <span>{{ feedbackSuccessMessage() }}</span>
            </div>
            <button (click)="feedbackSuccessMessage.set('')" class="text-emerald-400 hover:text-white">✕</button>
          </div>
        }

        <!-- Two Column Grid: Milestones vs Feedback -->
        <div class="grid grid-cols-1 lg:grid-cols-12 gap-8 items-start">
          <!-- Left Column: Milestone Checklist -->
          <div class="lg:col-span-7 flex flex-col gap-4">
            <div class="flex items-center justify-between pb-2 border-b border-slate-800">
              <h2 class="text-sm font-bold text-white flex items-center gap-2">
                <span>🎯</span> Onboarding Sprints & Deliverables
              </h2>
              <span class="text-xs text-slate-400 font-mono">{{ milestones().length }} Key Stages</span>
            </div>

            <div class="flex flex-col gap-3">
              @for (m of milestones(); track m.id) {
                <div
                  class="bg-slate-900/70 border border-slate-800 hover:border-slate-700 transition rounded-xl p-5 shadow-sm"
                  data-testid="milestone-card">
                  <div class="flex items-start justify-between gap-4 mb-2">
                    <div>
                      <div class="flex items-center gap-2">
                        <span class="text-sm font-bold text-white">{{ m.title }}</span>
                        @if (m.latestClientVote === 'Approved') {
                          <span class="px-2 py-0.5 rounded bg-emerald-500/20 text-emerald-300 text-[10px] font-semibold border border-emerald-500/30">
                            ✓ Client Approved
                          </span>
                        } @else if (m.latestClientVote === 'ChangeRequested') {
                          <span class="px-2 py-0.5 rounded bg-amber-500/20 text-amber-300 text-[10px] font-semibold border border-amber-500/30">
                            ⚠️ Changes Requested
                          </span>
                        }
                      </div>
                      <p class="text-xs text-slate-300 mt-1 leading-relaxed">{{ m.description }}</p>
                    </div>

                    <span
                      class="px-2.5 py-1 rounded text-[11px] font-semibold font-mono uppercase shrink-0"
                      [class.bg-emerald-500-20]="m.status === 'Done'"
                      [class.text-emerald-300]="m.status === 'Done'"
                      [class.bg-indigo-500-20]="m.status === 'InProgress'"
                      [class.text-indigo-300]="m.status === 'InProgress'"
                      [class.bg-amber-500-20]="m.status === 'InReview'"
                      [class.text-amber-300]="m.status === 'InReview'"
                      [class.bg-slate-800]="m.status === 'Todo' || m.status === 'Backlog'"
                      [class.text-slate-400]="m.status === 'Todo' || m.status === 'Backlog'">
                      {{ m.status }}
                    </span>
                  </div>

                  <!-- Subtask Progress -->
                  <div class="mt-4 pt-3 border-t border-slate-800/80 flex items-center justify-between text-xs text-slate-400">
                    <div class="flex items-center gap-2 w-1/2">
                      <div class="flex-1 bg-slate-950 rounded-full h-1.5 overflow-hidden">
                        <div
                          class="bg-indigo-500 h-1.5 rounded-full transition-all"
                          [style.width.%]="m.progressPercent"></div>
                      </div>
                      <span class="font-mono text-[11px] text-slate-300">{{ m.subtasksCompleted }}/{{ m.subtasksTotal }}</span>
                    </div>

                    <div class="flex items-center gap-3">
                      <span class="text-[11px] font-mono text-slate-400">Due: {{ m.dueDate }}</span>
                      <button
                        (click)="selectMilestoneForReview(m)"
                        class="text-xs text-indigo-400 hover:text-indigo-300 font-semibold transition">
                        Review / Signoff →
                      </button>
                    </div>
                  </div>
                </div>
              }
            </div>
          </div>

          <!-- Right Column: Client Feedback & Approval Action -->
          <div class="lg:col-span-5 flex flex-col gap-6">
            <!-- Review Form -->
            <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-6 shadow-md" data-testid="client-feedback-form">
              <div class="flex items-center gap-2 pb-3 mb-4 border-b border-slate-800">
                <span class="text-base">✍️</span>
                <h3 class="text-sm font-bold text-white">Client Milestone Review & Signoff</h3>
              </div>

              <form (ngSubmit)="submitFeedback()" class="flex flex-col gap-4">
                <div>
                  <label class="block text-[11px] font-medium text-slate-300 mb-1">Target Milestone</label>
                  <select
                    [(ngModel)]="selectedMilestoneId"
                    name="selectedMilestone"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-indigo-500">
                    @for (m of milestones(); track m.id) {
                      <option [value]="m.id">{{ m.title }} ({{ m.status }})</option>
                    }
                  </select>
                </div>

                <div class="grid grid-cols-2 gap-3">
                  <div>
                    <label class="block text-[11px] font-medium text-slate-300 mb-1">Your Full Name</label>
                    <input
                      type="text"
                      data-testid="client-name-input"
                      [(ngModel)]="clientFeedbackName"
                      name="clientName"
                      placeholder="e.g. Sarah Jenkins"
                      required
                      class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500" />
                  </div>
                  <div>
                    <label class="block text-[11px] font-medium text-slate-300 mb-1">Work Email</label>
                    <input
                      type="email"
                      data-testid="client-email-input"
                      [(ngModel)]="clientFeedbackEmail"
                      name="clientEmail"
                      placeholder="sarah@client.com"
                      required
                      class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500 font-mono" />
                  </div>
                </div>

                <div>
                  <label class="block text-[11px] font-medium text-slate-300 mb-1">Signoff Decision</label>
                  <select
                    data-testid="client-status-vote"
                    [(ngModel)]="clientStatusVote"
                    name="statusVote"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-indigo-500">
                    <option value="Approved">✅ Approve Milestone & Authorize Progression</option>
                    <option value="ChangeRequested">⚠️ Request Adjustments / Scope Clarification</option>
                  </select>
                </div>

                <div>
                  <label class="block text-[11px] font-medium text-slate-300 mb-1">Feedback Notes / Approval Comments</label>
                  <textarea
                    rows="3"
                    data-testid="client-feedback-notes"
                    [(ngModel)]="clientFeedbackNotes"
                    name="feedbackNotes"
                    placeholder="Provide acceptance notes, verified test observations, or required adjustment details..."
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg p-3 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500"></textarea>
                </div>

                <button
                  type="submit"
                  data-testid="submit-client-feedback-btn"
                  [disabled]="isSubmittingFeedback() || !clientFeedbackName || !clientFeedbackEmail"
                  class="w-full mt-2 py-2.5 px-4 rounded-lg bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 text-white text-xs font-bold transition shadow-md flex items-center justify-center gap-2">
                  @if (isSubmittingFeedback()) {
                    <span class="animate-spin text-xs">⚡</span>
                    <span>Recording Feedback...</span>
                  } @else {
                    <span>🚀 Submit Formal Milestone Signoff</span>
                  }
                </button>
              </form>
            </div>

            <!-- Activity Audit Feed -->
            <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-5 shadow-sm">
              <h4 class="text-xs font-bold text-slate-300 uppercase tracking-wider font-mono mb-3">
                Recent Signoffs & Verification Stream
              </h4>
              <div class="flex flex-col gap-3">
                @for (entry of feedbackLog(); track entry.id) {
                  <div class="p-3 bg-slate-950/70 border border-slate-800/80 rounded-lg text-xs">
                    <div class="flex items-center justify-between mb-1">
                      <span class="font-semibold text-slate-200">{{ entry.clientName }}</span>
                      <span
                        class="px-2 py-0.5 rounded text-[10px] font-semibold"
                        [class.bg-emerald-500-20]="entry.statusVote === 'Approved'"
                        [class.text-emerald-300]="entry.statusVote === 'Approved'"
                        [class.bg-amber-500-20]="entry.statusVote === 'ChangeRequested'"
                        [class.text-amber-300]="entry.statusVote === 'ChangeRequested'">
                        {{ entry.statusVote === 'Approved' ? '✓ Approved' : '⚠️ Changes' }}
                      </span>
                    </div>
                    <div class="text-[11px] text-indigo-400 font-medium">{{ entry.milestoneTitle }}</div>
                    <p class="text-[11px] text-slate-400 mt-1 italic">"{{ entry.notes }}"</p>
                    <div class="text-[10px] text-slate-500 font-mono mt-1 text-right">{{ entry.timestamp }}</div>
                  </div>
                }
              </div>
            </div>
          </div>
        </div>
      </main>
    </div>
  `,
  styles: [`
    .bg-emerald-500-20 { background-color: rgba(16, 185, 129, 0.2); }
    .bg-indigo-500-20 { background-color: rgba(99, 102, 241, 0.2); }
    .bg-amber-500-20 { background-color: rgba(245, 158, 11, 0.2); }
  `]
})
export class ClientOnboardingPortalComponent implements OnInit {
  private route = inject(ActivatedRoute);

  token = signal<string>('');
  projectName = signal<string>('Enterprise ERP Data Migration & Production Cutover');
  projectDescription = signal<string>('Automated implementation workflow converted from sealed CRM contract. Comprehensive data cleansing, multi-channel bridge setup, and team training.');
  clientOrgName = signal<string>('Hyper-Logistics S.A. Stakeholders');
  tokenExpiry = signal<string>('Valid until Nov 27, 2026');
  targetGoLive = signal<string>('Nov 15, 2026');

  milestones = signal<ClientPortalMilestone[]>([
    {
      id: 'm-1',
      title: 'Phase 1: Kickoff & Technical Architecture Scoping',
      description: 'Initial stakeholder alignment, SLA baseline signoff, and tenant security boundary setup.',
      status: 'Done',
      priority: 'Urgent',
      subtasksCompleted: 4,
      subtasksTotal: 4,
      progressPercent: 100,
      dueDate: 'Oct 28, 2026',
      latestClientVote: 'Approved'
    },
    {
      id: 'm-2',
      title: 'Phase 2: Environment Provisioning & Domain Setup',
      description: 'Database schema migrations, Redis cluster cache setup, and SSL DNS validation.',
      status: 'Done',
      priority: 'High',
      subtasksCompleted: 4,
      subtasksTotal: 4,
      progressPercent: 100,
      dueDate: 'Nov 02, 2026',
      latestClientVote: 'Approved'
    },
    {
      id: 'm-3',
      title: 'Phase 3: Catalog Ingestion & ERP Data Pipeline',
      description: 'Historical order import, SKU mapping, and inventory reconciliation testing.',
      status: 'InReview',
      priority: 'High',
      subtasksCompleted: 4,
      subtasksTotal: 5,
      progressPercent: 80,
      dueDate: 'Nov 08, 2026'
    },
    {
      id: 'm-4',
      title: 'Phase 4: Multi-Channel Communication & WhatsApp Cutover',
      description: 'Evolution API webhook connectivity, verified phone templates, and agent inbox training.',
      status: 'InProgress',
      priority: 'Medium',
      subtasksCompleted: 2,
      subtasksTotal: 6,
      progressPercent: 33,
      dueDate: 'Nov 12, 2026'
    },
    {
      id: 'm-5',
      title: 'Phase 5: Production Go-Live & Executive Signoff',
      description: 'Final security verification, live transactions smoke test, and SLA warranty start.',
      status: 'Todo',
      priority: 'Urgent',
      subtasksCompleted: 0,
      subtasksTotal: 3,
      progressPercent: 0,
      dueDate: 'Nov 15, 2026'
    }
  ]);

  feedbackLog = signal<ClientPortalFeedbackEntry[]>([
    {
      id: 'fb-1',
      clientName: 'Alexandre Meyer (CTO)',
      milestoneTitle: 'Phase 1: Kickoff & Technical Architecture Scoping',
      statusVote: 'Approved',
      notes: 'Security model and multi-tenant isolation reviewed and approved by InfoSec committee.',
      timestamp: 'Oct 26, 2026 16:30'
    },
    {
      id: 'fb-2',
      clientName: 'Kamel Riahi (VP Ops)',
      milestoneTitle: 'Phase 2: Environment Provisioning & Domain Setup',
      statusVote: 'Approved',
      notes: 'DNS propagation verified across our regional branches in Tunis and Sfax.',
      timestamp: 'Oct 27, 2026 11:15'
    }
  ]);

  selectedMilestoneId = 'm-3';
  clientFeedbackName = 'Sarah Jenkins';
  clientFeedbackEmail = 's.jenkins@hyper-logistics.tn';
  clientStatusVote: 'Approved' | 'ChangeRequested' = 'Approved';
  clientFeedbackNotes = 'Inventory SKU mapping verified against SAP export. Ready to proceed to final live tests.';
  isSubmittingFeedback = signal<boolean>(false);
  feedbackSuccessMessage = signal<string>('');

  overallProgress = signal<number>(62);
  completedMilestonesCount = signal<number>(2);

  ngOnInit(): void {
    const routeToken = this.route.snapshot.paramMap.get('token');
    if (routeToken) {
      this.token.set(routeToken);
    }
    this.recomputeProgress();
  }

  selectMilestoneForReview(m: ClientPortalMilestone): void {
    this.selectedMilestoneId = m.id;
  }

  submitFeedback(): void {
    if (!this.clientFeedbackName || !this.clientFeedbackEmail) return;

    this.isSubmittingFeedback.set(true);

    setTimeout(() => {
      const selected = this.milestones().find(m => m.id === this.selectedMilestoneId);
      const title = selected?.title ?? 'Milestone';

      const entry: ClientPortalFeedbackEntry = {
        id: `fb-${Date.now()}`,
        clientName: this.clientFeedbackName,
        milestoneTitle: title,
        statusVote: this.clientStatusVote,
        notes: this.clientFeedbackNotes,
        timestamp: 'Just now'
      };

      this.feedbackLog.update(log => [entry, ...log]);

      // Update milestone vote & status
      this.milestones.update(list => list.map(m => {
        if (m.id === this.selectedMilestoneId) {
          const newStatus = this.clientStatusVote === 'Approved' ? 'Done' : 'InProgress';
          const subtasks = this.clientStatusVote === 'Approved' ? m.subtasksTotal : m.subtasksCompleted;
          return {
            ...m,
            latestClientVote: this.clientStatusVote,
            status: newStatus,
            subtasksCompleted: subtasks,
            progressPercent: Math.round((subtasks / m.subtasksTotal) * 100)
          };
        }
        return m;
      }));

      this.recomputeProgress();
      this.isSubmittingFeedback.set(false);
      this.feedbackSuccessMessage.set(
        `Formal milestone feedback successfully recorded for '${title}' (${this.clientStatusVote}). SGrowing operations team notified.`
      );
    }, 300);
  }

  private recomputeProgress(): void {
    const list = this.milestones();
    const completed = list.filter(m => m.status === 'Done').length;
    this.completedMilestonesCount.set(completed);
    const pct = list.length > 0 ? Math.round((completed / list.length) * 100) : 0;
    this.overallProgress.set(pct);
  }
}
