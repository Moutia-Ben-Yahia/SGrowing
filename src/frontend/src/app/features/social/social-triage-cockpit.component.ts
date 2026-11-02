import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface InboundSocialMessage {
  messageId: string;
  channel: 'WhatsApp' | 'Instagram' | 'LinkedIn' | 'Twitter';
  senderHandle: string;
  senderName: string;
  messageText: string;
  timestampUtc: string;
}

export interface TriageAssessment {
  messageId: string;
  sentiment: 'Positive' | 'Neutral' | 'Negative' | 'SevereHazard';
  sentimentScore: number;
  intentCategory: string;
  urgencyScore: number;
  requiresImmediateEscalation: boolean;
  confidence: number;
}

export interface TriageQueueItem {
  queueId: string;
  message: InboundSocialMessage;
  assessment: TriageAssessment;
  assignedAgentRole: string;
  slaDeadlineMinutes: number;
  status: 'PendingReview' | 'DraftGenerated' | 'ApprovedAndSent';
  draftReply: string;
}

@Component({
  selector: 'app-social-triage-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="social-triage-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-700/60 pb-6">
        <div>
          <div class="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-emerald-400">
            <span class="inline-block w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
            Omnichannel Social Operations • Horizon 49
          </div>
          <h1 class="text-3xl font-extrabold text-white tracking-tight mt-1">
            Cross-Channel Social DM & Sentiment Auto-Triage
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            High-throughput WhatsApp, IG & LinkedIn message streaming with zero-shot sentiment hazard classification & AI draft dispatch.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="simulateInboundHazard()"
            data-testid="simulate-hazard-btn"
            class="px-4 py-2 bg-rose-600 hover:bg-rose-500 text-white rounded-lg text-sm font-semibold transition-colors flex items-center gap-2 shadow-lg shadow-rose-600/20"
          >
            <span>🚨</span> Ingest Escalation Hazard
          </button>
        </div>
      </div>

      <!-- Overview Stats -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Active Triage Queue</div>
          <div class="text-2xl font-bold text-white mt-1" data-testid="stat-queue-count">{{ queueItems().length }}</div>
          <div class="text-emerald-400 text-xs mt-1">Omnichannel Live Ingestion</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Escalation Hazards</div>
          <div class="text-2xl font-bold text-rose-400 mt-1" data-testid="stat-hazard-count">{{ hazardsCount() }}</div>
          <div class="text-slate-400 text-xs mt-1">15m Urgent SLA Target</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">SLA Compliance</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">98.5%</div>
          <div class="text-slate-400 text-xs mt-1">Zero Breach in Last 24h</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Draft Synthesis</div>
          <div class="text-2xl font-bold text-indigo-400 mt-1">Sub-180ms</div>
          <div class="text-slate-400 text-xs mt-1">Knowledge-Grounded Copilot</div>
        </div>
      </div>

      <!-- Main Layout: Triage Feed & Response Drafting Console -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <!-- Message Queue List (7 cols) -->
        <div class="lg:col-span-7 bg-slate-800/80 border border-slate-700/60 rounded-xl overflow-hidden">
          <div class="p-4 border-b border-slate-700/60 flex items-center justify-between">
            <h2 class="text-lg font-bold text-white">Live Inbound Triage Stream</h2>
            <div class="flex gap-2">
              <span class="text-xs px-2 py-0.5 rounded bg-emerald-950 text-emerald-300 border border-emerald-800">
                WhatsApp API Active
              </span>
              <span class="text-xs px-2 py-0.5 rounded bg-indigo-950 text-indigo-300 border border-indigo-800">
                LinkedIn InMail Active
              </span>
            </div>
          </div>

          <div class="divide-y divide-slate-700/40">
            @for (item of queueItems(); track item.queueId) {
              <div
                (click)="selectItem(item)"
                [class.bg-slate-700/40]="selectedItem()?.queueId === item.queueId"
                class="p-4 hover:bg-slate-700/30 transition-colors cursor-pointer flex flex-col justify-between gap-3"
                [attr.data-testid]="'triage-row-' + item.queueId"
              >
                <div class="flex items-center justify-between">
                  <div class="flex items-center gap-2">
                    <span
                      class="px-2 py-0.5 rounded text-[11px] font-bold"
                      [class]="getChannelBadgeClass(item.message.channel)"
                    >
                      {{ item.message.channel }}
                    </span>
                    <span class="font-semibold text-white text-sm">{{ item.message.senderName }}</span>
                    <span class="text-xs text-slate-400">({{ item.message.senderHandle }})</span>
                  </div>

                  <span
                    [class]="getSentimentBadgeClass(item.assessment.sentiment)"
                    class="px-2 py-0.5 rounded text-[10px] font-bold border uppercase"
                    [attr.data-testid]="'sentiment-badge-' + item.queueId"
                  >
                    {{ item.assessment.sentiment }} ({{ item.assessment.urgencyScore }})
                  </span>
                </div>

                <div class="text-xs text-slate-300 line-clamp-2">
                  "{{ item.message.messageText }}"
                </div>

                <div class="flex items-center justify-between text-xs pt-1 border-t border-slate-800/80">
                  <div class="text-slate-400">
                    Role: <span class="text-indigo-400 font-medium">{{ item.assignedAgentRole }}</span>
                  </div>
                  <div class="flex items-center gap-2">
                    <span
                      [class]="item.slaDeadlineMinutes < 10 ? 'text-rose-400 font-bold' : 'text-slate-400'"
                    >
                      SLA: {{ item.slaDeadlineMinutes }}m remaining
                    </span>
                    <span
                      [class]="item.status === 'ApprovedAndSent' ? 'bg-emerald-900/60 text-emerald-300' : 'bg-amber-900/60 text-amber-300'"
                      class="px-2 py-0.5 rounded text-[10px] font-medium"
                    >
                      {{ item.status }}
                    </span>
                  </div>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Selected Message & AI Draft Reply Console (5 cols) -->
        <div class="lg:col-span-5 bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 space-y-4">
          @if (selectedItem(); as active) {
            <div class="space-y-4">
              <div class="flex items-center justify-between border-b border-slate-700/60 pb-3">
                <div>
                  <h3 class="font-bold text-white text-base">Triage Resolution Console</h3>
                  <p class="text-xs text-slate-400">Queue ID: {{ active.queueId }}</p>
                </div>
                <span
                  class="text-xs px-2.5 py-1 rounded bg-slate-900 border border-slate-700 text-slate-300 font-mono"
                >
                  {{ active.assessment.intentCategory }}
                </span>
              </div>

              <!-- Customer Message Transcript Box -->
              <div class="p-3 bg-slate-900 rounded-xl border border-slate-800 space-y-1">
                <div class="text-[11px] font-semibold text-slate-400 uppercase">Incoming Message:</div>
                <div class="text-xs text-slate-200 italic">
                  "{{ active.message.messageText }}"
                </div>
              </div>

              <!-- AI Synthesized Draft Reply -->
              <div class="space-y-2">
                <div class="flex items-center justify-between">
                  <label class="text-xs font-bold text-slate-300">Context-Aware AI Response Draft:</label>
                  <span class="text-[11px] text-emerald-400">Grounding Score: 98%</span>
                </div>

                <textarea
                  [(ngModel)]="active.draftReply"
                  data-testid="input-draft-reply"
                  rows="4"
                  class="w-full bg-slate-900 border border-slate-700 rounded-lg p-3 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-emerald-500 font-sans"
                ></textarea>
              </div>

              <!-- 1-Click Approval Action -->
              <div class="pt-2 flex items-center justify-between">
                <div class="text-xs text-slate-400">
                  Target Channel: <strong class="text-white">{{ active.message.channel }}</strong>
                </div>

                <button
                  (click)="approveAndSendReply(active)"
                  data-testid="approve-reply-btn"
                  [disabled]="active.status === 'ApprovedAndSent'"
                  [class]="active.status === 'ApprovedAndSent' ? 'bg-slate-700 text-slate-400 cursor-not-allowed' : 'bg-emerald-600 hover:bg-emerald-500 text-white'"
                  class="px-4 py-2 rounded-lg text-xs font-semibold shadow-lg transition-colors"
                >
                  {{ active.status === 'ApprovedAndSent' ? '✓ Dispatched to Customer' : 'Approve & Dispatch Reply' }}
                </button>
              </div>
            </div>
          } @else {
            <div class="text-center py-16 text-slate-400 text-xs">
              Select an incoming social message to inspect sentiment classification and approve AI draft.
            </div>
          }
        </div>
      </div>
    </div>
  `
})
export class SocialTriageCockpitComponent {
  readonly queueItems = signal<TriageQueueItem[]>([
    {
      queueId: 'triage_wa_9901',
      message: {
        messageId: 'msg_wa_9901',
        channel: 'WhatsApp',
        senderHandle: '+14155552671',
        senderName: 'Alexander Wright (Global Retail)',
        messageText: 'Hi, our European checkout portal has thrown a 500 error for 20 minutes! We are losing orders, please fix immediately or we cancel our contract!',
        timestampUtc: new Date(Date.now() - 12 * 60000).toISOString()
      },
      assessment: {
        messageId: 'msg_wa_9901',
        sentiment: 'SevereHazard',
        sentimentScore: 0.12,
        intentCategory: 'CancellationRisk',
        urgencyScore: 98,
        requiresImmediateEscalation: true,
        confidence: 0.96
      },
      assignedAgentRole: 'ExecutiveEscalationLead',
      slaDeadlineMinutes: 3,
      status: 'PendingReview',
      draftReply: 'Hi Alexander, our Incident Response Engineering team is actively mitigating this gateway incident. A senior CS lead has been notified and we are monitoring your store checkout status.'
    },
    {
      queueId: 'triage_li_8820',
      message: {
        messageId: 'msg_li_8820',
        channel: 'LinkedIn',
        senderHandle: 'linkedin.com/in/sophia-patel',
        senderName: 'Sophia Patel (FinTech VP)',
        messageText: 'Hello team! We are evaluating Growth OS for our 500-seat multi-tenant financial SaaS. Can we schedule an enterprise demo and review SOC2 compliance?',
        timestampUtc: new Date(Date.now() - 25 * 60000).toISOString()
      },
      assessment: {
        messageId: 'msg_li_8820',
        sentiment: 'Positive',
        sentimentScore: 0.94,
        intentCategory: 'SalesInquiry',
        urgencyScore: 82,
        requiresImmediateEscalation: false,
        confidence: 0.98
      },
      assignedAgentRole: 'EnterpriseAccountExecutive',
      slaDeadlineMinutes: 35,
      status: 'DraftGenerated',
      draftReply: 'Hi Sophia, thanks for reaching out! We would love to showcase Growth OS. I have reserved priority demo slots with our Enterprise Architecture team: https://cal.growth-os.cloud/enterprise-demo'
    }
  ]);

  readonly selectedItem = signal<TriageQueueItem | null>(this.queueItems()[0]);

  readonly hazardsCount = computed(() =>
    this.queueItems().filter(i => i.assessment.requiresImmediateEscalation).length
  );

  selectItem(item: TriageQueueItem): void {
    this.selectedItem.set(item);
  }

  simulateInboundHazard(): void {
    const newItem: TriageQueueItem = {
      queueId: `triage_ig_${Date.now()}`,
      message: {
        messageId: `msg_ig_${Date.now()}`,
        channel: 'Instagram',
        senderHandle: '@ecom_titan',
        senderName: 'Marcus Vance',
        messageText: 'Outage on store checkout! Nobody can pay right now! Need immediate tech support!',
        timestampUtc: new Date().toISOString()
      },
      assessment: {
        messageId: `msg_ig_${Date.now()}`,
        sentiment: 'SevereHazard',
        sentimentScore: 0.15,
        intentCategory: 'CancellationRisk',
        urgencyScore: 95,
        requiresImmediateEscalation: true,
        confidence: 0.95
      },
      assignedAgentRole: 'ExecutiveEscalationLead',
      slaDeadlineMinutes: 15,
      status: 'DraftGenerated',
      draftReply: 'Hi Marcus, we are on it right now. Our engineering team has been paged.'
    };

    this.queueItems.update(list => [newItem, ...list]);
    this.selectedItem.set(newItem);
  }

  approveAndSendReply(item: TriageQueueItem): void {
    this.queueItems.update(list =>
      list.map(i => (i.queueId === item.queueId ? { ...i, status: 'ApprovedAndSent' } : i))
    );
    if (this.selectedItem()?.queueId === item.queueId) {
      this.selectedItem.update(i => (i ? { ...i, status: 'ApprovedAndSent' } : null));
    }
  }

  getChannelBadgeClass(channel: string): string {
    switch (channel) {
      case 'WhatsApp':
        return 'bg-emerald-900/80 text-emerald-300';
      case 'LinkedIn':
        return 'bg-blue-900/80 text-blue-300';
      case 'Instagram':
        return 'bg-rose-900/80 text-rose-300';
      default:
        return 'bg-slate-700 text-slate-300';
    }
  }

  getSentimentBadgeClass(sentiment: string): string {
    switch (sentiment) {
      case 'SevereHazard':
        return 'bg-rose-900/80 text-rose-300 border-rose-700';
      case 'Positive':
        return 'bg-emerald-900/80 text-emerald-300 border-emerald-700';
      default:
        return 'bg-slate-800 text-slate-300 border-slate-700';
    }
  }
}
