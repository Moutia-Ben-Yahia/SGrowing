import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SignalRService } from '../../core/services/signalr.service';
import { ApiService } from '../../core/services/api.service';
import { AiDraftDto, ConversationDto, MessageDto, ContactDto } from '../../core/models/chat.models';

@Component({
  selector: 'app-inbox-shell',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <!-- 1. Left Nav & Channels Bar -->
      <aside class="w-64 border-r border-slate-800 bg-slate-900/80 flex flex-col justify-between p-4">
        <div>
          <div class="flex items-center gap-2 mb-6">
            <div class="h-8 w-8 rounded-lg bg-gradient-to-tr from-indigo-500 to-cyan-400 flex items-center justify-center font-bold text-white shadow-lg shadow-indigo-500/20">
              MS
            </div>
            <div>
              <h1 class="text-sm font-semibold tracking-wide text-white">MarketingStudio</h1>
              <p class="text-xs text-slate-400">Omnicommunication</p>
            </div>
          </div>

          <div class="space-y-1 mb-6">
            <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider px-2">Inboxes</span>
            <button
              (click)="selectedChannelFilter.set('all')"
              [class.bg-slate-800]="selectedChannelFilter() === 'all'"
              class="w-full flex items-center justify-between px-3 py-2 rounded-lg text-sm text-slate-200 hover:bg-slate-800/60 transition">
              <span class="flex items-center gap-2">📥 All Conversations</span>
              <span class="bg-indigo-500/20 text-indigo-400 text-xs px-2 py-0.5 rounded-full font-medium">{{ conversations().length }}</span>
            </button>
            <button
              (click)="selectedChannelFilter.set('whatsapp')"
              [class.bg-slate-800]="selectedChannelFilter() === 'whatsapp'"
              class="w-full flex items-center justify-between px-3 py-2 rounded-lg text-sm text-slate-200 hover:bg-slate-800/60 transition">
              <span class="flex items-center gap-2">🟢 WhatsApp</span>
              <span class="text-xs text-slate-400">Meta + Evo</span>
            </button>
            <button
              (click)="selectedChannelFilter.set('email')"
              [class.bg-slate-800]="selectedChannelFilter() === 'email'"
              class="w-full flex items-center justify-between px-3 py-2 rounded-lg text-sm text-slate-200 hover:bg-slate-800/60 transition">
              <span class="flex items-center gap-2">✉️ Email</span>
            </button>
            <button
              (click)="selectedChannelFilter.set('webchat')"
              [class.bg-slate-800]="selectedChannelFilter() === 'webchat'"
              class="w-full flex items-center justify-between px-3 py-2 rounded-lg text-sm text-slate-200 hover:bg-slate-800/60 transition">
              <span class="flex items-center gap-2">💬 Web Chat</span>
            </button>
          </div>
        </div>

        <div class="border-t border-slate-800 pt-4">
          <div class="flex items-center justify-between text-xs text-slate-400">
            <span class="flex items-center gap-1.5">
              <span class="h-2 w-2 rounded-full" [class.bg-emerald-400]="signalR.isConnected()" [class.bg-amber-400]="!signalR.isConnected()"></span>
              {{ signalR.isConnected() ? 'Real-Time Connected' : 'Local Mock Mode' }}
            </span>
            <span class="bg-slate-800 text-slate-300 px-1.5 py-0.5 rounded text-[10px]">.NET 10</span>
          </div>
        </div>
      </aside>

      <!-- 2. Middle: Conversation List (Column 2) -->
      <section class="w-80 border-r border-slate-800 bg-slate-900/50 flex flex-col">
        <div class="p-3 border-b border-slate-800">
          <input
            type="text"
            placeholder="Search conversations..."
            class="w-full bg-slate-800/80 border border-slate-700/60 rounded-md px-3 py-1.5 text-xs text-slate-200 placeholder-slate-400 focus:outline-none focus:border-indigo-500"
          />
        </div>

        <div class="flex-1 overflow-y-auto divide-y divide-slate-800/40">
          @for (conv of conversations(); track conv.id) {
            <div
              (click)="selectConversation(conv)"
              [class.bg-slate-800]="activeConversation()?.id === conv.id"
              class="p-3 cursor-pointer hover:bg-slate-800/50 transition">
              <div class="flex items-start justify-between mb-1">
                <span class="font-medium text-sm text-slate-100 flex items-center gap-1.5">
                  <span class="h-2 w-2 rounded-full bg-emerald-400"></span>
                  {{ conv.contact?.firstName }} {{ conv.contact?.lastName }}
                </span>
                <span class="text-[11px] text-slate-400">12m</span>
              </div>
              <p class="text-xs text-slate-400 truncate mb-1.5">
                {{ getLastMessagePreview(conv.id) }}
              </p>
              <div class="flex items-center gap-2">
                <span class="text-[10px] bg-slate-800 text-slate-300 px-1.5 py-0.5 rounded font-mono">
                  {{ getChannelIcon(conv.inboxId) }}
                </span>
                @if (conv.aiMode === 1) {
                  <span class="text-[10px] bg-indigo-500/10 text-indigo-400 border border-indigo-500/20 px-1.5 py-0.5 rounded">
                    ⚡ Copilot
                  </span>
                }
              </div>
            </div>
          }
        </div>
      </section>

      <!-- 3. Message Thread & Composer (Column 3) -->
      <main class="flex-1 flex flex-col bg-slate-950">
        <!-- Thread Header -->
        <header class="h-14 border-b border-slate-800 px-6 flex items-center justify-between bg-slate-900/30">
          <div class="flex items-center gap-3">
            <h2 class="font-semibold text-sm text-slate-100">
              {{ activeConversation()?.contact?.firstName }} {{ activeConversation()?.contact?.lastName }}
            </h2>
            <span class="text-xs text-slate-400 font-mono">{{ activeConversation()?.contact?.phone || activeConversation()?.contact?.email }}</span>
          </div>

          <div class="flex items-center gap-2">
            <button
              (click)="resolveCurrentConversation()"
              class="text-xs bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 px-3 py-1.5 rounded-md transition">
              ✓ Resolve
            </button>
          </div>
        </header>

        <!-- Messages Stream -->
        <div class="flex-1 p-6 overflow-y-auto space-y-4">
          @for (msg of activeMessages(); track msg.id) {
            <div
              class="flex flex-col max-w-lg"
              [class.ml-auto]="msg.senderType === 2 || msg.senderType === 3"
              [class.items-end]="msg.senderType === 2 || msg.senderType === 3">
              <div
                class="rounded-2xl px-4 py-2.5 text-sm shadow-sm"
                [class.bg-indigo-600]="msg.senderType === 2"
                [class.text-white]="msg.senderType === 2"
                [class.bg-slate-800]="msg.senderType === 1"
                [class.text-slate-100]="msg.senderType === 1"
                [class.bg-cyan-950]="msg.senderType === 3"
                [class.border]="msg.senderType === 3"
                [class.border-cyan-800]="msg.senderType === 3"
                [class.text-cyan-100]="msg.senderType === 3">
                {{ msg.content }}
              </div>
              <span class="text-[10px] text-slate-500 mt-1 px-1">
                {{ msg.senderType === 1 ? 'Customer' : msg.senderType === 2 ? 'Agent' : '🤖 AI Copilot' }} • {{ msg.createdAtUtc | date:'shortTime' }}
              </span>
            </div>
          }
        </div>

        <!-- AI Copilot Suggestion Card (if pending draft exists) -->
        @if (currentAiDraft()) {
          <div class="mx-6 mb-3 p-3.5 rounded-xl border border-indigo-500/30 bg-indigo-950/30 flex items-center justify-between">
            <div class="flex items-start gap-3">
              <span class="text-lg">🤖</span>
              <div>
                <div class="flex items-center gap-2 mb-1">
                  <span class="text-xs font-semibold text-indigo-300">AI Suggested Response</span>
                  <span class="text-[10px] bg-indigo-500/20 text-indigo-400 px-1.5 py-0.2 rounded font-mono">
                    {{ (currentAiDraft()!.confidenceScore * 100) | number:'1.0-0' }}% confidence
                  </span>
                </div>
                <p class="text-xs text-slate-300 italic">"{{ currentAiDraft()!.suggestedReply }}"</p>
              </div>
            </div>
            <div class="flex items-center gap-2">
              <button
                (click)="applyAiDraft()"
                class="text-xs bg-indigo-600 hover:bg-indigo-500 text-white font-medium px-3 py-1.5 rounded-lg transition shadow-md shadow-indigo-600/20">
                1-Click Approve & Send
              </button>
            </div>
          </div>
        }

        <!-- Composer -->
        <footer class="p-4 border-t border-slate-800 bg-slate-900/40">
          <div class="flex items-center gap-2">
            <input
              type="text"
              [(ngModel)]="composerText"
              (keyup.enter)="sendMessage()"
              placeholder="Type your message or click an AI prompt..."
              class="flex-1 bg-slate-800/80 border border-slate-700/80 rounded-xl px-4 py-2.5 text-sm text-slate-100 placeholder-slate-400 focus:outline-none focus:border-indigo-500 transition"
            />
            <button
              (click)="sendMessage()"
              class="bg-indigo-600 hover:bg-indigo-500 text-white font-medium text-sm px-5 py-2.5 rounded-xl transition shadow-lg shadow-indigo-500/20">
              Send
            </button>
          </div>
        </footer>
      </main>

      <!-- 4. Right: Customer 360 & CRM Details (Column 4) -->
      <aside class="w-72 border-l border-slate-800 bg-slate-900/60 p-5 flex flex-col justify-between overflow-y-auto">
        <div>
          <h3 class="text-xs font-semibold text-slate-400 uppercase tracking-wider mb-4">Customer 360 CRM</h3>
          
          <div class="flex items-center gap-3 mb-6">
            <div class="h-12 w-12 rounded-full bg-slate-800 border border-slate-700 flex items-center justify-center font-bold text-slate-200">
              {{ activeConversation()?.contact?.firstName?.[0] || '?' }}
            </div>
            <div>
              <h4 class="font-semibold text-sm text-white">
                {{ activeConversation()?.contact?.firstName }} {{ activeConversation()?.contact?.lastName }}
              </h4>
              <span class="text-xs text-emerald-400 bg-emerald-500/10 px-2 py-0.5 rounded-full font-medium">
                Active Customer
              </span>
            </div>
          </div>

          <div class="space-y-4 text-xs">
            <div>
              <span class="text-slate-400 block mb-1">Email</span>
              <span class="text-slate-200 font-mono">{{ activeConversation()?.contact?.email || 'None' }}</span>
            </div>
            <div>
              <span class="text-slate-400 block mb-1">Phone / WhatsApp</span>
              <span class="text-slate-200 font-mono">{{ activeConversation()?.contact?.phone || 'None' }}</span>
            </div>
            <div>
              <span class="text-slate-400 block mb-1">Tags</span>
              <div class="flex flex-wrap gap-1 mt-1">
                @for (tag of activeConversation()?.contact?.tags || []; track tag) {
                  <span class="bg-slate-800 text-slate-300 px-2 py-0.5 rounded font-mono text-[10px]">{{ tag }}</span>
                }
              </div>
            </div>
            <div>
              <span class="text-slate-400 block mb-1">Dynamic Attributes (JSONB)</span>
              <div class="bg-slate-950/80 p-2.5 rounded-lg border border-slate-800 font-mono text-[11px] text-cyan-300">
                <pre class="whitespace-pre-wrap">{{ activeConversation()?.contact?.customFields | json }}</pre>
              </div>
            </div>
          </div>
        </div>

        <div class="border-t border-slate-800 pt-4 mt-6">
          <button class="w-full text-xs text-center text-slate-400 hover:text-slate-200 py-1 transition">
            View Full CRM Profile →
          </button>
        </div>
      </aside>
    </div>
  `
})
export class InboxShellComponent implements OnInit {
  public signalR = inject(SignalRService);
  private api = inject(ApiService);

  public selectedChannelFilter = signal<string>('all');
  public conversations = signal<ConversationDto[]>([]);
  public activeConversation = signal<ConversationDto | null>(null);
  public activeMessages = signal<MessageDto[]>([]);
  public currentAiDraft = signal<AiDraftDto | null>(null);
  public composerText = '';

  private currentTenantId = '00000000-0000-0000-0000-000000000001';

  ngOnInit(): void {
    // 1. Seed demo / loaded conversations
    this.seedDemoData();

    // 2. Connect to real-time SignalR hub
    this.signalR.startConnection();

    // 3. Subscribe to real-time inbound messages
    this.signalR.messageReceived$.subscribe((msg) => {
      if (this.activeConversation()?.id === msg.conversationId) {
        this.activeMessages.update(list => [...list, msg]);
      }
    });

    // 4. Subscribe to AI drafts generated by background copilot
    this.signalR.aiDraftGenerated$.subscribe((draft) => {
      if (this.activeConversation()?.id === draft.conversationId) {
        this.currentAiDraft.set(draft);
      }
    });
  }

  private seedDemoData(): void {
    const contactA: ContactDto = {
      id: 'contact-01',
      tenantId: this.currentTenantId,
      firstName: 'Elena',
      lastName: 'Fisher',
      email: 'elena@travelmag.org',
      phone: '+15559876543',
      lifecycleStage: 3,
      customFields: { plan: 'Pro', lead_source: 'WhatsApp', satisfaction_score: 98 },
      tags: ['vip', 'whatsapp-verified'],
      createdAtUtc: new Date().toISOString()
    };

    const convA: ConversationDto = {
      id: 'conv-01',
      tenantId: this.currentTenantId,
      inboxId: 'inbox-wa',
      contactId: contactA.id,
      status: 1,
      priority: 2,
      aiMode: 1,
      lastMessageAtUtc: new Date().toISOString(),
      createdAtUtc: new Date().toISOString(),
      contact: contactA
    };

    this.conversations.set([convA]);
    this.selectConversation(convA);
  }

  public selectConversation(conv: ConversationDto): void {
    this.activeConversation.set(conv);
    this.activeMessages.set([
      {
        id: 'msg-01',
        conversationId: conv.id,
        senderType: 1, // Contact
        contentType: 1,
        content: 'Hi! Can you share pricing for the enterprise tier and how WhatsApp integrates with CRM?',
        deliveryStatus: 3,
        createdAtUtc: new Date(Date.now() - 1000 * 60 * 15).toISOString()
      }
    ]);

    // Set demo AI Draft
    this.currentAiDraft.set({
      id: 'draft-01',
      conversationId: conv.id,
      suggestedReply: 'Our Enterprise tier starts at $299/mo and features unlimited WhatsApp channels via Evolution-API and Meta Cloud API, full CRM 360, and autonomous AI agents.',
      confidenceScore: 0.94,
      status: 'Drafted',
      createdAtUtc: new Date().toISOString()
    });
  }

  public sendMessage(): void {
    if (!this.composerText.trim() || !this.activeConversation()) return;

    const newMsg: MessageDto = {
      id: 'msg-' + Date.now(),
      conversationId: this.activeConversation()!.id,
      senderType: 2, // Agent
      contentType: 1,
      content: this.composerText,
      deliveryStatus: 2,
      createdAtUtc: new Date().toISOString()
    };

    this.activeMessages.update(list => [...list, newMsg]);
    this.composerText = '';
  }

  public applyAiDraft(): void {
    if (!this.currentAiDraft()) return;

    const approvedMsg: MessageDto = {
      id: 'msg-' + Date.now(),
      conversationId: this.currentAiDraft()!.conversationId,
      senderType: 2, // Agent approved
      contentType: 1,
      content: this.currentAiDraft()!.suggestedReply,
      deliveryStatus: 2,
      createdAtUtc: new Date().toISOString()
    };

    this.activeMessages.update(list => [...list, approvedMsg]);
    this.currentAiDraft.set(null);
  }

  public resolveCurrentConversation(): void {
    if (!this.activeConversation()) return;
    this.activeConversation.update(c => c ? { ...c, status: 3 } : null);
  }

  public getLastMessagePreview(convId: string): string {
    return 'Hi! Can you share pricing for the enterprise tier...';
  }

  public getChannelIcon(inboxId: string): string {
    return inboxId.includes('wa') ? 'WA' : 'MAIL';
  }
}
