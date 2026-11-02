import { Component, inject, signal, computed, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslationService } from '../../core/services/translation.service';

export type ConversationChannel = 'WhatsApp' | 'Email' | 'Sms' | 'WebChat';
export type TicketPriority = 'Low' | 'Medium' | 'High' | 'Urgent';
export type TicketStatus = 'Open' | 'PendingCustomer' | 'Resolved' | 'Escalated';

export interface MessageItem {
  id: string;
  channel: ConversationChannel;
  sender: string;
  isCustomer: boolean;
  content: string;
  time: string;
}

export interface ThreadItem {
  id: string;
  customerName: string;
  customerEmail: string;
  customerPhone: string;
  channel: ConversationChannel;
  status: TicketStatus;
  priority: TicketPriority;
  sentiment: 'Positive' | 'Neutral' | 'Frustrated';
  slaMinutesRemaining: number;
  messages: MessageItem[];
}

@Component({
  selector: 'app-unified-support-inbox',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b border-slate-700 pb-5">
        <div>
          <div class="flex items-center gap-3">
            <span class="p-2 bg-blue-600/20 text-blue-400 rounded-lg text-xl">💬</span>
            <div>
              <h1 class="text-2xl font-bold text-white tracking-tight">Unified Omnichannel Customer Inbox & AI Deflection</h1>
              <p class="text-sm text-slate-400">Consolidated WhatsApp, Email, SMS & Chat streams with semantic auto-deflection</p>
            </div>
          </div>
        </div>
        <div class="flex items-center gap-3">
          <span class="inline-flex items-center px-3 py-1 rounded-full text-xs font-semibold bg-emerald-500/20 text-emerald-300 border border-emerald-500/30">
            ● AI Deflection Engine Active
          </span>
        </div>
      </div>

      <!-- 3-Pane Omnichannel Layout -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6 h-[640px]">
        <!-- Pane 1: Conversation Thread List (4 cols) -->
        <div class="lg:col-span-4 bg-slate-900 border border-slate-800 rounded-2xl p-4 shadow-xl flex flex-col overflow-hidden">
          <div class="pb-3 border-b border-slate-800 flex justify-between items-center">
            <h2 class="text-sm font-semibold text-white">Active Inquiries ({{ threads().length }})</h2>
            <div class="flex gap-1 text-[11px]">
              <span class="px-2 py-0.5 rounded bg-rose-500/20 text-rose-300 font-bold">
                {{ urgentCount() }} Urgent
              </span>
            </div>
          </div>

          <div class="overflow-y-auto space-y-2 pt-3 flex-1">
            @for (thread of threads(); track thread.id) {
              <div (click)="selectThread(thread.id)"
                   class="p-3 rounded-xl border transition cursor-pointer space-y-2"
                   [ngClass]="selectedThreadId() === thread.id ? 'bg-indigo-950/60 border-indigo-500' : 'bg-slate-950/60 border-slate-800/80 hover:bg-slate-800/50'">
                <div class="flex items-center justify-between">
                  <span class="font-semibold text-xs text-white">{{ thread.customerName }}</span>
                  <span class="px-1.5 py-0.5 rounded text-[10px] font-mono font-bold flex items-center gap-1"
                        [ngClass]="getChannelBadgeClass(thread.channel)">
                    {{ getChannelIcon(thread.channel) }} {{ thread.channel }}
                  </span>
                </div>
                <p class="text-[11px] text-slate-400 truncate">{{ thread.messages[thread.messages.length - 1].content }}</p>
                <div class="flex justify-between items-center text-[10px] text-slate-500 pt-1">
                  <span [ngClass]="thread.sentiment === 'Frustrated' ? 'text-rose-400 font-semibold' : 'text-slate-400'">
                    Mood: {{ thread.sentiment }}
                  </span>
                  <span class="font-mono" [ngClass]="thread.slaMinutesRemaining < 30 ? 'text-rose-400 font-bold' : 'text-slate-400'">
                    SLA: {{ thread.slaMinutesRemaining }}m
                  </span>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Pane 2: Message Stream (5 cols) -->
        <div class="lg:col-span-5 bg-slate-900 border border-slate-800 rounded-2xl p-4 shadow-xl flex flex-col justify-between overflow-hidden">
          @if (activeThread()) {
            <!-- Thread Header -->
            <div class="pb-3 border-b border-slate-800 flex items-center justify-between">
              <div>
                <h3 class="text-sm font-semibold text-white">{{ activeThread()!.customerName }}</h3>
                <p class="text-[11px] text-slate-400">{{ activeThread()!.customerEmail }} • {{ activeThread()!.customerPhone }}</p>
              </div>
              <span class="px-2 py-0.5 rounded text-[10px] font-bold"
                    [ngClass]="activeThread()!.status === 'Escalated' ? 'bg-rose-500/20 text-rose-300' : 'bg-emerald-500/20 text-emerald-300'">
                {{ activeThread()!.status }}
              </span>
            </div>

            <!-- Messages Timeline -->
            <div class="overflow-y-auto py-3 space-y-3 flex-1">
              @for (msg of activeThread()!.messages; track msg.id) {
                <div class="flex flex-col" [ngClass]="msg.isCustomer ? 'items-start' : 'items-end'">
                  <div class="max-w-[85%] rounded-2xl p-3 text-xs leading-relaxed space-y-1 shadow"
                       [ngClass]="msg.isCustomer ? 'bg-slate-800 text-slate-100 rounded-tl-sm' : 'bg-indigo-600 text-white rounded-tr-sm'">
                    <div class="flex items-center justify-between text-[10px] opacity-75 gap-2">
                      <span>{{ msg.sender }} ({{ msg.channel }})</span>
                      <span>{{ msg.time }}</span>
                    </div>
                    <p>{{ msg.content }}</p>
                  </div>
                </div>
              }
            </div>

            <!-- Reply Box -->
            <div class="pt-3 border-t border-slate-800 space-y-2">
              <div class="flex gap-2">
                <input type="text" [ngModel]="replyMessage()" (ngModelChange)="replyMessage.set($event)"
                       placeholder="Type your omnichannel reply..."
                       (keydown.enter)="sendAgentReply()"
                       class="flex-1 bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-xs text-slate-200 focus:outline-none focus:border-indigo-500">
                <button (click)="sendAgentReply()"
                        class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold rounded-xl transition">
                  Send
                </button>
              </div>
            </div>
          }
        </div>

        <!-- Pane 3: AI Copilot & Knowledge Deflection (3 cols) -->
        <div class="lg:col-span-3 bg-slate-900 border border-slate-800 rounded-2xl p-4 shadow-xl space-y-4 flex flex-col justify-between">
          <div class="space-y-3">
            <h3 class="text-sm font-semibold text-white flex items-center gap-1.5">
              <span>🤖</span> AI Deflection Copilot
            </h3>

            <!-- AI Suggested Response Card -->
            <div class="p-3 bg-slate-950 rounded-xl border border-indigo-900/40 space-y-2">
              <div class="flex items-center justify-between text-[10px]">
                <span class="text-indigo-400 font-bold">Suggested Draft</span>
                <span class="text-emerald-400 font-mono font-semibold">96% match</span>
              </div>
              <p class="text-xs text-slate-300 leading-relaxed italic bg-indigo-950/20 p-2 rounded border border-indigo-500/20">
                "{{ aiSuggestedDraft() }}"
              </p>
              <button (click)="applyAiDraft()"
                      class="w-full py-1.5 bg-indigo-600 hover:bg-indigo-500 text-white text-[11px] font-semibold rounded-lg shadow transition">
                Insert Suggestion ⚡
              </button>
            </div>

            <!-- SLA Guardrails -->
            <div class="p-3 bg-slate-950 rounded-xl border border-slate-800 space-y-1 text-xs">
              <span class="text-slate-400">Response SLA Status</span>
              <p class="text-slate-200 font-mono font-bold">{{ activeThread()?.slaMinutesRemaining }} minutes remaining</p>
              <span class="text-[10px] text-amber-400">Target: &lt; 15m first response</span>
            </div>
          </div>

          <!-- Escalation Action -->
          <div class="pt-3 border-t border-slate-800">
            <button (click)="escalateActiveThread()"
                    class="w-full py-2 bg-rose-600/20 hover:bg-rose-600/30 text-rose-300 border border-rose-500/30 text-xs font-semibold rounded-xl transition">
              Escalate to Senior Lead 🚨
            </button>
          </div>
        </div>
      </div>
    </div>
  `
})
export class UnifiedSupportInboxComponent {
  translation: TranslationService;
  private cdr?: ChangeDetectorRef;

  selectedThreadId = signal<string>('thr_001');
  replyMessage = signal<string>('');
  aiSuggestedDraft = signal<string>('Hello! You can easily connect your Meta CAPI token by navigating to Marketing > Ad CAPI ROAS and clicking Connect Token.');

  threads = signal<ThreadItem[]>([
    {
      id: 'thr_001',
      customerName: 'Marcus Vance',
      customerEmail: 'm.vance@techcorp.io',
      customerPhone: '+1-555-0192',
      channel: 'WhatsApp',
      status: 'Open',
      priority: 'High',
      sentiment: 'Frustrated',
      slaMinutesRemaining: 18,
      messages: [
        { id: 'm1', channel: 'WhatsApp', sender: 'Marcus Vance', isCustomer: true, content: 'Hey, our Meta CAPI sync seems to be failing on purchase events since morning.', time: '10:14' },
        { id: 'm2', channel: 'WhatsApp', sender: 'Marcus Vance', isCustomer: true, content: 'Can someone verify if our access token expired?', time: '10:15' }
      ]
    },
    {
      id: 'thr_002',
      customerName: 'Elena Rostova',
      customerEmail: 'elena@novasoft.de',
      customerPhone: '+49-170-555021',
      channel: 'Email',
      status: 'Open',
      priority: 'Urgent',
      sentiment: 'Neutral',
      slaMinutesRemaining: 8,
      messages: [
        { id: 'm3', channel: 'Email', sender: 'Elena Rostova', isCustomer: true, content: 'Requesting invoice breakdown for last month’s metered API overage charges.', time: '09:42' }
      ]
    }
  ]);

  activeThread = computed(() =>
    this.threads().find(t => t.id === this.selectedThreadId())
  );

  urgentCount = computed(() =>
    this.threads().filter(t => t.priority === 'Urgent' || t.slaMinutesRemaining < 15).length
  );

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
    try {
      this.cdr = inject(ChangeDetectorRef);
    } catch {
      // Direct unit test instantiation
    }
  }

  selectThread(id: string): void {
    this.selectedThreadId.set(id);
  }

  getChannelIcon(channel: ConversationChannel): string {
    switch (channel) {
      case 'WhatsApp': return '🟢';
      case 'Email': return '✉️';
      case 'Sms': return '📱';
      case 'WebChat': return '💬';
    }
  }

  getChannelBadgeClass(channel: ConversationChannel): string {
    switch (channel) {
      case 'WhatsApp': return 'bg-emerald-500/20 text-emerald-300';
      case 'Email': return 'bg-blue-500/20 text-blue-300';
      case 'Sms': return 'bg-amber-500/20 text-amber-300';
      case 'WebChat': return 'bg-indigo-500/20 text-indigo-300';
    }
  }

  applyAiDraft(): void {
    this.replyMessage.set(this.aiSuggestedDraft());
    this.cdr?.markForCheck();
  }

  sendAgentReply(): void {
    const content = this.replyMessage().trim();
    if (!content || !this.activeThread()) return;

    const newMsg: MessageItem = {
      id: 'm_' + Math.random().toString(36).substring(2, 8),
      channel: this.activeThread()!.channel,
      sender: 'Agent Support (You)',
      isCustomer: false,
      content,
      time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
    };

    this.threads.update(list =>
      list.map(t => t.id === this.selectedThreadId()
        ? { ...t, messages: [...t.messages, newMsg], status: 'PendingCustomer' }
        : t
      )
    );

    this.replyMessage.set('');
    this.cdr?.markForCheck();
  }

  escalateActiveThread(): void {
    if (!this.activeThread()) return;

    this.threads.update(list =>
      list.map(t => t.id === this.selectedThreadId()
        ? { ...t, status: 'Escalated', priority: 'Urgent' }
        : t
      )
    );
  }
}
