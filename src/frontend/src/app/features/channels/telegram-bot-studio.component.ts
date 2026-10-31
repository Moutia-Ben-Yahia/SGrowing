import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export type TelegramConversationState =
  | 'Idle'
  | 'AwaitingEmail'
  | 'AwaitingPhone'
  | 'CatalogBrowsing'
  | 'CheckoutPending'
  | 'Subscribed';

export interface ChatMessage {
  id: string;
  sender: 'bot' | 'user';
  text: string;
  timestamp: string;
  replyMarkup?: {
    rows: Array<Array<{
      text: string;
      callbackData?: string;
      url?: string;
      webAppUrl?: string;
    }>>;
  };
}

export interface FsmSession {
  userId: number;
  fullName: string;
  username: string;
  currentState: TelegramConversationState;
  capturedEmail: string | null;
  capturedPhone: string | null;
  cartItems: string[];
  cartTotal: number;
  messageCount: number;
  lastActive: string;
}

export interface BroadcastLogEntry {
  campaignId: string;
  campaignName: string;
  targetAudience: string;
  totalRecipients: number;
  deliveredCount: number;
  failedCount: number;
  retryCount: number;
  floodWaitCount: number;
  status: 'Completed' | 'Processing' | 'Failed';
  durationMs: number;
  dispatchedAt: string;
}

@Component({
  selector: 'app-telegram-bot-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="min-h-screen bg-slate-950 text-slate-100 flex flex-col font-sans selection:bg-sky-500/30">
      <!-- Top Navigation & Header -->
      <header class="border-b border-slate-800/80 bg-slate-900/80 backdrop-blur-md px-6 py-4 sticky top-0 z-40 flex flex-wrap items-center justify-between gap-4">
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-xl bg-gradient-to-tr from-sky-600 via-cyan-500 to-blue-600 flex items-center justify-center shadow-lg shadow-sky-500/20">
            <!-- Telegram Paper Plane SVG -->
            <svg class="w-5 h-5 text-white" viewBox="0 0 24 24" fill="currentColor">
              <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm4.64 6.8c-.15 1.58-.8 5.42-1.13 7.19-.14.75-.42 1-.68 1.03-.58.05-1.02-.38-1.58-.75-.88-.58-1.38-.94-2.23-1.5-.99-.65-.35-1.01.22-1.59.15-.15 2.71-2.48 2.76-2.69a.2.2 0 00-.05-.18c-.06-.05-.14-.03-.21-.02-.09.02-1.49.95-4.22 2.79-.4.27-.76.41-1.08.4-.36-.01-1.04-.2-1.55-.37-.63-.2-1.12-.31-1.08-.66.02-.18.27-.36.75-.55 2.92-1.27 4.86-2.11 5.83-2.51 2.78-1.16 3.35-1.36 3.73-1.36.08 0 .27.02.39.12.1.08.13.19.14.27-.01.06.01.24 0 .38z"/>
            </svg>
          </div>
          <div>
            <div class="flex items-center gap-2">
              <h1 class="text-lg font-bold text-slate-100 tracking-tight">Telegram Conversational Marketing & Mini-App Bot Router</h1>
              <span class="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-sky-500/10 text-sky-400 border border-sky-500/20">
                Horizon 53
              </span>
              <span class="px-2 py-0.5 rounded text-[10px] font-mono font-medium bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
                Bot API 7.0+
              </span>
            </div>
            <p class="text-xs text-slate-400 mt-0.5">
              Finite State Machine router, HMAC-SHA256 Mini App validator, inline interactive keyboards, and paced audience broadcaster.
            </p>
          </div>
        </div>

        <!-- Tab Switcher -->
        <div class="flex items-center bg-slate-800/70 p-1 rounded-xl border border-slate-700/60 text-xs font-medium">
          <button
            type="button"
            (click)="activeTab.set('simulator')"
            [class]="activeTab() === 'simulator' ? 'bg-sky-600 text-white shadow-sm' : 'text-slate-400 hover:text-slate-200'"
            class="px-3 py-1.5 rounded-lg transition-all flex items-center gap-1.5">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 18h.01M8 21h8a2 2 0 002-2V5a2 2 0 00-2-2H8a2 2 0 00-2 2v14a2 2 0 002 2z"/>
            </svg>
            Chat Simulator
          </button>
          <button
            type="button"
            (click)="activeTab.set('fsm')"
            [class]="activeTab() === 'fsm' ? 'bg-sky-600 text-white shadow-sm' : 'text-slate-400 hover:text-slate-200'"
            class="px-3 py-1.5 rounded-lg transition-all flex items-center gap-1.5">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z"/>
            </svg>
            FSM Inspector ({{ activeSessions().length }})
          </button>
          <button
            type="button"
            (click)="activeTab.set('sandbox')"
            [class]="activeTab() === 'sandbox' ? 'bg-sky-600 text-white shadow-sm' : 'text-slate-400 hover:text-slate-200'"
            class="px-3 py-1.5 rounded-lg transition-all flex items-center gap-1.5">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z"/>
            </svg>
            Mini App Auth
          </button>
          <button
            type="button"
            (click)="activeTab.set('broadcast')"
            [class]="activeTab() === 'broadcast' ? 'bg-sky-600 text-white shadow-sm' : 'text-slate-400 hover:text-slate-200'"
            class="px-3 py-1.5 rounded-lg transition-all flex items-center gap-1.5">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 5.882V19.24a1.76 1.76 0 01-3.417.592l-2.147-6.15M18 13a3 3 0 100-6M5.436 13.683A4.001 4.001 0 017 6h1.832c4.1 0 7.625-1.234 9.168-3v14c-1.543-1.766-5.067-3-9.168-3H7a3.988 3.988 0 01-1.564-.317z"/>
            </svg>
            Audience Broadcast
          </button>
        </div>
      </header>

      <!-- Subheader Metrics Ribbon -->
      <section class="bg-slate-900/40 border-b border-slate-800/60 px-6 py-3 grid grid-cols-2 md:grid-cols-4 gap-4 text-xs">
        <div class="flex items-center gap-3">
          <div class="p-2 rounded-lg bg-sky-500/10 text-sky-400 border border-sky-500/20">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 12h.01M12 12h.01M16 12h.01M21 12c0 4.418-4.03 8-9 8a9.863 9.863 0 01-4.255-.949L3 20l1.395-3.72C3.512 15.042 3 13.574 3 12c0-4.418 4.03-8 9-8s9 3.582 9 8z"/>
            </svg>
          </div>
          <div>
            <span class="text-slate-400 block">Total Updates Handled</span>
            <span class="text-sm font-bold text-slate-100 font-mono">{{ metrics().totalUpdates }}</span>
          </div>
        </div>

        <div class="flex items-center gap-3">
          <div class="p-2 rounded-lg bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z"/>
            </svg>
          </div>
          <div>
            <span class="text-slate-400 block">Active Subscribers</span>
            <span class="text-sm font-bold text-slate-100 font-mono">{{ metrics().subscribersCount }} Verified</span>
          </div>
        </div>

        <div class="flex items-center gap-3">
          <div class="p-2 rounded-lg bg-indigo-500/10 text-indigo-400 border border-indigo-500/20">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z"/>
            </svg>
          </div>
          <div>
            <span class="text-slate-400 block">Rate Limit Pacing</span>
            <span class="text-sm font-bold text-slate-100 font-mono">30 msgs / sec Max</span>
          </div>
        </div>

        <div class="flex items-center gap-3">
          <div class="p-2 rounded-lg bg-amber-500/10 text-amber-400 border border-amber-500/20">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"/>
            </svg>
          </div>
          <div>
            <span class="text-slate-400 block">Broadcast Delivery Rate</span>
            <span class="text-sm font-bold text-slate-100 font-mono">99.8%</span>
          </div>
        </div>
      </section>

      <!-- Main Body Container -->
      <main class="flex-1 p-6 max-w-7xl mx-auto w-full">
        <!-- 1. TELEGRAM SMARTPHONE CHAT SIMULATOR -->
        @if (activeTab() === 'simulator') {
          <div class="grid grid-cols-1 lg:grid-cols-12 gap-8 items-start">
            <!-- Smartphone Bezel Container -->
            <div class="lg:col-span-6 flex justify-center">
              <div class="w-full max-w-[390px] h-[720px] rounded-[48px] bg-slate-900 border-[8px] border-slate-800 shadow-2xl shadow-sky-950/40 flex flex-col overflow-hidden relative">
                <!-- Phone Top Island / Notch -->
                <div class="bg-slate-900 px-6 pt-3 pb-2 flex items-center justify-between z-10">
                  <span class="text-xs font-semibold text-slate-300 font-mono">9:41</span>
                  <div class="w-24 h-4 bg-slate-800 rounded-full flex items-center justify-center">
                    <div class="w-2.5 h-2.5 bg-slate-950 rounded-full"></div>
                  </div>
                  <div class="flex items-center gap-1 text-slate-400 text-xs">
                    <svg class="w-3.5 h-3.5" fill="currentColor" viewBox="0 0 24 24"><path d="M12 3c-4.97 0-9 4.03-9 9 0 2.12.74 4.07 1.97 5.61L4.35 19.4c-.39.39-.39 1.02 0 1.41.39.39 1.02.39 1.41 0l1.9-1.9C9.23 19.57 10.57 20 12 20c4.97 0 9-4.03 9-9s-4.03-9-9-9z"/></svg>
                    <div class="w-4 h-2 bg-slate-400 rounded-sm"></div>
                  </div>
                </div>

                <!-- Telegram Chat Header Bar -->
                <div class="bg-slate-850 px-4 py-2.5 border-b border-slate-700/60 flex items-center justify-between">
                  <div class="flex items-center gap-2.5">
                    <div class="w-9 h-9 rounded-full bg-gradient-to-tr from-sky-500 to-blue-600 flex items-center justify-center text-white font-bold text-sm shadow">
                      SG
                    </div>
                    <div>
                      <div class="flex items-center gap-1.5">
                        <span class="text-xs font-bold text-slate-100">SGrowing Marketing</span>
                        <!-- Blue verified checkmark -->
                        <svg class="w-3.5 h-3.5 text-sky-400" fill="currentColor" viewBox="0 0 24 24">
                          <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z"/>
                        </svg>
                      </div>
                      <span class="text-[10px] text-sky-400 font-medium">bot • online</span>
                    </div>
                  </div>

                  <div class="flex items-center gap-1 text-slate-400">
                    <button type="button" (click)="resetChat()" title="Restart Bot (/start)" class="p-1.5 hover:text-slate-200">
                      <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"/>
                      </svg>
                    </button>
                  </div>
                </div>

                <!-- Chat Scroll View -->
                <div class="flex-1 p-3 overflow-y-auto space-y-3 bg-[radial-gradient(#1e293b_1px,transparent_1px)] [background-size:16px_16px] bg-slate-950/80">
                  <div class="text-center my-2">
                    <span class="px-2.5 py-0.5 rounded-full bg-slate-800/80 text-[10px] text-slate-400 font-medium">
                      Today
                    </span>
                  </div>

                  @for (msg of chatMessages(); track msg.id) {
                    <div [class]="msg.sender === 'user' ? 'flex justify-end' : 'flex justify-start'">
                      <div [class]="msg.sender === 'user'
                        ? 'bg-sky-600 text-white rounded-2xl rounded-tr-xs px-3.5 py-2 max-w-[85%] text-xs shadow-md'
                        : 'bg-slate-800 text-slate-100 rounded-2xl rounded-tl-xs px-3.5 py-2.5 max-w-[88%] text-xs shadow-md border border-slate-700/60'">
                        <p class="whitespace-pre-line leading-relaxed">{{ msg.text }}</p>

                        <!-- Attached Inline Keyboard Grid -->
                        @if (msg.replyMarkup?.rows) {
                          <div class="mt-2.5 pt-2 border-t border-slate-700/60 space-y-1.5">
                            @for (row of msg.replyMarkup!.rows; track $index) {
                              <div class="grid grid-flow-col auto-cols-fr gap-1.5">
                                @for (btn of row; track btn.text) {
                                  @if (btn.webAppUrl) {
                                    <button
                                      type="button"
                                      (click)="openMiniApp(btn.webAppUrl)"
                                      class="px-2 py-1.5 rounded-lg bg-sky-500/20 hover:bg-sky-500/30 text-sky-300 border border-sky-400/30 text-[11px] font-medium flex items-center justify-center gap-1 transition-all">
                                      <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z"/></svg>
                                      {{ btn.text }}
                                    </button>
                                  } @else if (btn.callbackData) {
                                    <button
                                      type="button"
                                      (click)="handleButtonClick(btn.callbackData)"
                                      class="px-2 py-1.5 rounded-lg bg-slate-700/70 hover:bg-slate-700 text-slate-200 border border-slate-600/50 text-[11px] font-medium text-center truncate transition-all">
                                      {{ btn.text }}
                                    </button>
                                  } @else if (btn.url) {
                                    <a
                                      [href]="btn.url"
                                      target="_blank"
                                      class="px-2 py-1.5 rounded-lg bg-slate-700/70 hover:bg-slate-700 text-slate-200 border border-slate-600/50 text-[11px] font-medium text-center truncate block transition-all">
                                      {{ btn.text }} ↗
                                    </a>
                                  }
                                }
                              </div>
                            }
                          </div>
                        }

                        <div class="text-[9px] text-right mt-1 opacity-60 flex items-center justify-end gap-1">
                          <span>{{ msg.timestamp }}</span>
                          @if (msg.sender === 'user') {
                            <!-- Sent double checkmark -->
                            <svg class="w-3 h-3 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"/></svg>
                          }
                        </div>
                      </div>
                    </div>
                  }
                </div>

                <!-- Interactive Chat Input Footer -->
                <div class="p-2.5 bg-slate-850 border-t border-slate-800 flex items-center gap-2">
                  <button
                    type="button"
                    (click)="simulateContactShare()"
                    title="Simulate Contact Sharing"
                    class="p-2 rounded-xl bg-slate-800 text-slate-400 hover:text-sky-400 transition-colors">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15.172 7l-6.586 6.586a2 2 0 102.828 2.828l6.414-6.586a4 4 0 00-5.656-5.656l-6.415 6.585a6 6 0 108.486 8.486L20.5 13"/>
                    </svg>
                  </button>

                  <input
                    type="text"
                    [(ngModel)]="chatInput"
                    (keyup.enter)="sendMessage()"
                    placeholder="Message @SGrowingMarketingBot..."
                    class="flex-1 bg-slate-900 border border-slate-700/80 rounded-xl px-3 py-2 text-xs text-slate-100 placeholder-slate-500 focus:outline-none focus:border-sky-500" />

                  <button
                    type="button"
                    (click)="sendMessage()"
                    class="p-2 rounded-xl bg-sky-600 hover:bg-sky-500 text-white shadow-md shadow-sky-600/30 transition-all">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 19l9 2-9-18-9 18 9-2zm0 0v-8"/>
                    </svg>
                  </button>
                </div>

                <!-- Mini App Viewport Overlay (Slide-up modal simulation) -->
                @if (isMiniAppOpen()) {
                  <div class="absolute inset-0 bg-slate-950/95 z-30 flex flex-col animate-in fade-in slide-in-from-bottom duration-200">
                    <!-- Mini App Header -->
                    <div class="px-4 py-3 bg-slate-900 border-b border-slate-800 flex items-center justify-between">
                      <div class="flex items-center gap-2">
                        <span class="w-2.5 h-2.5 rounded-full bg-emerald-400 animate-pulse"></span>
                        <span class="text-xs font-bold text-slate-100">Telegram Mini App Shop</span>
                      </div>
                      <button
                        type="button"
                        (click)="isMiniAppOpen.set(false)"
                        class="p-1 rounded-lg bg-slate-800 text-slate-400 hover:text-slate-100">
                        ✕
                      </button>
                    </div>

                    <!-- Mini App Body -->
                    <div class="flex-1 p-4 overflow-y-auto space-y-4">
                      <div class="p-3 rounded-xl bg-gradient-to-r from-sky-900/40 to-indigo-900/40 border border-sky-500/20 text-xs">
                        <span class="font-bold text-sky-400 block mb-1">Instant Checkout Experience</span>
                        <p class="text-slate-300 text-[11px]">
                          Telegram WebApp sends secure, authenticated payloads directly back to the bot conversation router.
                        </p>
                      </div>

                      <div class="space-y-2">
                        <label class="text-[11px] font-semibold text-slate-400 uppercase tracking-wider">Select Product Solution</label>
                        <div class="space-y-1.5">
                          <button
                            type="button"
                            (click)="selectedMiniAppProduct.set({ name: 'Growth Marketing Accelerator', price: 199 })"
                            [class]="selectedMiniAppProduct().name === 'Growth Marketing Accelerator' ? 'border-sky-500 bg-sky-950/40' : 'border-slate-800 bg-slate-900'"
                            class="w-full text-left p-3 rounded-xl border flex items-center justify-between text-xs transition-all">
                            <div>
                              <span class="font-semibold text-slate-100 block">Growth Marketing Accelerator</span>
                              <span class="text-[11px] text-slate-400">Complete multi-channel funnel</span>
                            </div>
                            <span class="font-mono font-bold text-sky-400">$199.00</span>
                          </button>

                          <button
                            type="button"
                            (click)="selectedMiniAppProduct.set({ name: 'Omnichannel Suite', price: 349 })"
                            [class]="selectedMiniAppProduct().name === 'Omnichannel Suite' ? 'border-sky-500 bg-sky-950/40' : 'border-slate-800 bg-slate-900'"
                            class="w-full text-left p-3 rounded-xl border flex items-center justify-between text-xs transition-all">
                            <div>
                              <span class="font-semibold text-slate-100 block">Omnichannel Suite</span>
                              <span class="text-[11px] text-slate-400">WhatsApp & Telegram engine</span>
                            </div>
                            <span class="font-mono font-bold text-sky-400">$349.00</span>
                          </button>
                        </div>
                      </div>

                      <div class="p-3 rounded-xl bg-slate-900 border border-slate-800 text-xs space-y-1.5">
                        <div class="flex justify-between text-slate-400">
                          <span>Subtotal</span>
                          <span>\${{ selectedMiniAppProduct().price }}.00</span>
                        </div>
                        <div class="flex justify-between text-emerald-400">
                          <span>VIP Member Discount</span>
                          <span>-\$0.00</span>
                        </div>
                        <div class="border-t border-slate-800 pt-1.5 flex justify-between font-bold text-slate-100">
                          <span>Total</span>
                          <span class="font-mono text-sky-400">\${{ selectedMiniAppProduct().price }}.00</span>
                        </div>
                      </div>
                    </div>

                    <!-- Mini App MainButton Footer -->
                    <div class="p-3 bg-slate-900 border-t border-slate-800">
                      <button
                        type="button"
                        (click)="submitMiniAppOrder()"
                        class="w-full py-2.5 rounded-xl bg-sky-600 hover:bg-sky-500 text-white font-semibold text-xs shadow-lg shadow-sky-600/30 flex items-center justify-center gap-2 transition-all">
                        <span>PAY \${{ selectedMiniAppProduct().price }}.00 WITH TELEGRAM</span>
                        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M14 5l7 7m0 0l-7 7m7-7H3"/></svg>
                      </button>
                    </div>
                  </div>
                }
              </div>
            </div>

            <!-- Simulator Control & Command Palette -->
            <div class="lg:col-span-6 space-y-5">
              <div class="bg-slate-900/90 border border-slate-800 rounded-2xl p-5 shadow-xl">
                <h3 class="text-sm font-bold text-slate-100 flex items-center gap-2 mb-3">
                  <svg class="w-4 h-4 text-sky-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z"/>
                  </svg>
                  Quick Command Shortcuts
                </h3>
                <p class="text-xs text-slate-400 mb-4">
                  Trigger conversational bot commands to test conversational branching and user state transitions.
                </p>

                <div class="grid grid-cols-2 sm:grid-cols-3 gap-2 text-xs">
                  <button
                    type="button"
                    (click)="sendQuickCommand('/start')"
                    class="p-2.5 rounded-xl bg-slate-800/80 hover:bg-slate-700/80 text-left border border-slate-700/60 transition-all">
                    <span class="font-mono font-bold text-sky-400 block">/start</span>
                    <span class="text-[10px] text-slate-400">Welcome & Menu</span>
                  </button>

                  <button
                    type="button"
                    (click)="sendQuickCommand('/catalog')"
                    class="p-2.5 rounded-xl bg-slate-800/80 hover:bg-slate-700/80 text-left border border-slate-700/60 transition-all">
                    <span class="font-mono font-bold text-purple-400 block">/catalog</span>
                    <span class="text-[10px] text-slate-400">Solutions Store</span>
                  </button>

                  <button
                    type="button"
                    (click)="sendQuickCommand('/subscribe')"
                    class="p-2.5 rounded-xl bg-slate-800/80 hover:bg-slate-700/80 text-left border border-slate-700/60 transition-all">
                    <span class="font-mono font-bold text-emerald-400 block">/subscribe</span>
                    <span class="text-[10px] text-slate-400">VIP Lead Flow</span>
                  </button>

                  <button
                    type="button"
                    (click)="sendQuickCommand('/checkout')"
                    class="p-2.5 rounded-xl bg-slate-800/80 hover:bg-slate-700/80 text-left border border-slate-700/60 transition-all">
                    <span class="font-mono font-bold text-rose-400 block">/checkout</span>
                    <span class="text-[10px] text-slate-400">Cart Finalization</span>
                  </button>

                  <button
                    type="button"
                    (click)="sendQuickCommand('/status')"
                    class="p-2.5 rounded-xl bg-slate-800/80 hover:bg-slate-700/80 text-left border border-slate-700/60 transition-all">
                    <span class="font-mono font-bold text-amber-400 block">/status</span>
                    <span class="text-[10px] text-slate-400">Session Profile</span>
                  </button>

                  <button
                    type="button"
                    (click)="sendQuickCommand('/reset')"
                    class="p-2.5 rounded-xl bg-slate-800/80 hover:bg-slate-700/80 text-left border border-slate-700/60 transition-all">
                    <span class="font-mono font-bold text-slate-400 block">/reset</span>
                    <span class="text-[10px] text-slate-400">Clear Cart & FSM</span>
                  </button>
                </div>
              </div>

              <!-- Simulator Current Session Card -->
              <div class="bg-slate-900/90 border border-slate-800 rounded-2xl p-5 shadow-xl">
                <div class="flex items-center justify-between mb-3">
                  <h3 class="text-sm font-bold text-slate-100 flex items-center gap-2">
                    <span class="w-2.5 h-2.5 rounded-full bg-sky-400"></span>
                    Current Simulated User State
                  </h3>
                  <span
                    [class]="getStatePillClass(currentUserState().currentState)"
                    class="px-2.5 py-0.5 rounded-full text-xs font-semibold border">
                    {{ currentUserState().currentState }}
                  </span>
                </div>

                <div class="grid grid-cols-2 gap-3 text-xs">
                  <div class="p-3 rounded-xl bg-slate-800/50 border border-slate-700/40">
                    <span class="text-slate-400 block text-[10px] uppercase">User Profile</span>
                    <span class="font-semibold text-slate-200">{{ currentUserState().fullName }}</span>
                    <span class="text-slate-400 text-[11px] block">@{{ currentUserState().username }} ({{ currentUserState().userId }})</span>
                  </div>

                  <div class="p-3 rounded-xl bg-slate-800/50 border border-slate-700/40">
                    <span class="text-slate-400 block text-[10px] uppercase">Cart Items & Total</span>
                    <span class="font-semibold text-slate-200">{{ currentUserState().cartItems.length }} item(s)</span>
                    <span class="font-mono text-emerald-400 font-bold block">\${{ currentUserState().cartTotal.toFixed(2) }}</span>
                  </div>

                  <div class="p-3 rounded-xl bg-slate-800/50 border border-slate-700/40">
                    <span class="text-slate-400 block text-[10px] uppercase">Captured Email</span>
                    <span class="font-medium text-slate-200">{{ currentUserState().capturedEmail || 'Not yet captured' }}</span>
                  </div>

                  <div class="p-3 rounded-xl bg-slate-800/50 border border-slate-700/40">
                    <span class="text-slate-400 block text-[10px] uppercase">Captured Phone</span>
                    <span class="font-medium text-slate-200">{{ currentUserState().capturedPhone || 'Not yet captured' }}</span>
                  </div>
                </div>
              </div>
            </div>
          </div>
        }

        <!-- 2. FSM STATE INSPECTOR -->
        @if (activeTab() === 'fsm') {
          <div class="space-y-6">
            <div class="flex flex-wrap items-center justify-between gap-4">
              <div>
                <h2 class="text-base font-bold text-slate-100">Conversational Finite State Machine (FSM) Inspector</h2>
                <p class="text-xs text-slate-400">
                  Real-time monitor of active Telegram sessions, current states, captured contact credentials, and session carts.
                </p>
              </div>

              <div class="flex items-center gap-2">
                <button
                  type="button"
                  (click)="refreshSessions()"
                  class="px-3 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-medium border border-slate-700 flex items-center gap-1.5 transition-all">
                  <svg class="w-3.5 h-3.5 text-sky-400" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"/></svg>
                  Refresh Sessions
                </button>
              </div>
            </div>

            <!-- Active Sessions Table -->
            <div class="bg-slate-900 border border-slate-800 rounded-2xl overflow-hidden shadow-xl">
              <div class="overflow-x-auto">
                <table class="w-full text-left text-xs">
                  <thead class="bg-slate-800/70 border-b border-slate-700/70 text-slate-400 uppercase tracking-wider text-[10px]">
                    <tr>
                      <th class="px-4 py-3">Telegram User</th>
                      <th class="px-4 py-3">FSM State</th>
                      <th class="px-4 py-3">Captured Credentials</th>
                      <th class="px-4 py-3">Cart Contents</th>
                      <th class="px-4 py-3">Total Value</th>
                      <th class="px-4 py-3">Last Active</th>
                      <th class="px-4 py-3 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-800/60">
                    @for (session of activeSessions(); track session.userId) {
                      <tr class="hover:bg-slate-800/30 transition-colors">
                        <td class="px-4 py-3.5">
                          <div class="flex items-center gap-2.5">
                            <div class="w-8 h-8 rounded-full bg-slate-800 border border-slate-700 flex items-center justify-center font-bold text-slate-200">
                              {{ session.fullName[0] }}
                            </div>
                            <div>
                              <span class="font-semibold text-slate-100 block">{{ session.fullName }}</span>
                              <span class="text-slate-400 text-[11px] font-mono">&#64;{{ session.username }} • ID: {{ session.userId }}</span>
                            </div>
                          </div>
                        </td>

                        <td class="px-4 py-3.5">
                          <span
                            [class]="getStatePillClass(session.currentState)"
                            class="px-2.5 py-1 rounded-full text-[11px] font-semibold border">
                            {{ session.currentState }}
                          </span>
                        </td>

                        <td class="px-4 py-3.5 space-y-1">
                          @if (session.capturedEmail) {
                            <div class="flex items-center gap-1.5 text-slate-200">
                              <span class="text-slate-400">📧</span>
                              <span>{{ session.capturedEmail }}</span>
                            </div>
                          } @else {
                            <span class="text-slate-500 italic block">No email</span>
                          }

                          @if (session.capturedPhone) {
                            <div class="flex items-center gap-1.5 text-slate-200">
                              <span class="text-slate-400">📞</span>
                              <span>{{ session.capturedPhone }}</span>
                            </div>
                          }
                        </td>

                        <td class="px-4 py-3.5">
                          @if (session.cartItems.length > 0) {
                            <span class="font-medium text-slate-200">{{ session.cartItems.length }} item(s)</span>
                            <span class="text-[10px] text-slate-400 block truncate max-w-xs">{{ session.cartItems.join(', ') }}</span>
                          } @else {
                            <span class="text-slate-500 italic">Empty</span>
                          }
                        </td>

                        <td class="px-4 py-3.5 font-mono font-bold text-slate-200">
                          \${{ session.cartTotal.toFixed(2) }}
                        </td>

                        <td class="px-4 py-3.5 text-slate-400 font-mono text-[11px]">
                          {{ session.lastActive }}
                        </td>

                        <td class="px-4 py-3.5 text-right">
                          <button
                            type="button"
                            (click)="resetSpecificSession(session.userId)"
                            class="px-2.5 py-1 rounded bg-slate-800 hover:bg-slate-700 text-slate-300 text-[11px] font-medium border border-slate-700 transition-all">
                            Reset FSM
                          </button>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        }

        <!-- 3. TELEGRAM MINI APP AUTH SANDBOX -->
        @if (activeTab() === 'sandbox') {
          <div class="grid grid-cols-1 lg:grid-cols-12 gap-8 items-start">
            <div class="lg:col-span-6 space-y-5">
              <div class="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4">
                <div class="flex items-center justify-between">
                  <div>
                    <h3 class="text-sm font-bold text-slate-100">Telegram WebApp initData Signature Validator</h3>
                    <p class="text-xs text-slate-400">Cryptographically verify WebApp auth tokens using HMAC-SHA256.</p>
                  </div>
                  <span class="px-2 py-0.5 rounded text-[10px] font-mono bg-sky-500/10 text-sky-400 border border-sky-500/20">
                    HMAC-SHA256
                  </span>
                </div>

                <div class="space-y-3 text-xs">
                  <div>
                    <label class="block text-slate-400 font-medium mb-1">Bot Token</label>
                    <input
                      type="text"
                      [(ngModel)]="sandboxBotToken"
                      class="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-slate-100 font-mono text-xs focus:outline-none focus:border-sky-500" />
                  </div>

                  <div>
                    <label class="block text-slate-400 font-medium mb-1">Raw Telegram WebApp initData String</label>
                    <textarea
                      rows="4"
                      [(ngModel)]="sandboxInitData"
                      placeholder="query_id=...&user=...&auth_date=...&hash=..."
                      class="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-slate-100 font-mono text-xs focus:outline-none focus:border-sky-500"></textarea>
                  </div>

                  <div class="flex flex-wrap items-center gap-2 pt-2">
                    <button
                      type="button"
                      (click)="generateSandboxInitData()"
                      class="px-3.5 py-2 rounded-xl bg-sky-600 hover:bg-sky-500 text-white font-medium text-xs shadow-md shadow-sky-600/20 transition-all">
                      ⚡ Generate Signed initData
                    </button>

                    <button
                      type="button"
                      (click)="tamperSandboxInitData()"
                      class="px-3.5 py-2 rounded-xl bg-rose-600/20 hover:bg-rose-600/30 text-rose-300 border border-rose-500/30 font-medium text-xs transition-all">
                      ⚠️ Tamper Payload (Bit Flip)
                    </button>

                    <button
                      type="button"
                      (click)="expireSandboxInitData()"
                      class="px-3.5 py-2 rounded-xl bg-amber-600/20 hover:bg-amber-600/30 text-amber-300 border border-amber-500/30 font-medium text-xs transition-all">
                      ⏰ Expire Timestamp (-30h)
                    </button>
                  </div>
                </div>
              </div>
            </div>

            <!-- Validation Results Diagnostic Panel -->
            <div class="lg:col-span-6 space-y-4">
              <div class="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4">
                <div class="flex items-center justify-between">
                  <h3 class="text-sm font-bold text-slate-100">Cryptographic Diagnostics</h3>
                  <button
                    type="button"
                    (click)="runValidation()"
                    class="px-3 py-1.5 rounded-lg bg-sky-600 hover:bg-sky-500 text-white text-xs font-semibold shadow transition-all">
                    Verify Signature Now
                  </button>
                </div>

                @if (validationOutcome()) {
                  <div
                    [class]="validationOutcome()!.isValid
                      ? 'bg-emerald-500/10 border-emerald-500/30 text-emerald-300'
                      : (validationOutcome()!.isExpired ? 'bg-amber-500/10 border-amber-500/30 text-amber-300' : 'bg-rose-500/10 border-rose-500/30 text-rose-300')"
                    class="p-4 rounded-xl border flex items-start gap-3">
                    <div class="text-lg">
                      @if (validationOutcome()!.isValid) { ✅ } @else if (validationOutcome()!.isExpired) { ⏰ } @else { ❌ }
                    </div>
                    <div class="text-xs">
                      <span class="font-bold block text-sm">
                        {{ validationOutcome()!.isValid ? 'Signature Authenticated & Fresh' : (validationOutcome()!.isExpired ? 'Auth Date Expired' : 'Signature Verification Failed') }}
                      </span>
                      <p class="mt-1 opacity-90">
                        {{ validationOutcome()!.message }}
                      </p>
                    </div>
                  </div>

                  <div class="space-y-3 text-xs">
                    <div>
                      <span class="text-slate-400 block text-[10px] uppercase font-mono mb-1">Calculated HMAC vs Received Hash</span>
                      <div class="p-2.5 bg-slate-950 rounded-lg border border-slate-800 font-mono text-[11px] space-y-1">
                        <div class="flex items-center justify-between text-slate-300">
                          <span class="text-slate-500">Calc:</span>
                          <span class="text-sky-400 truncate max-w-sm">{{ validationOutcome()!.calculatedHash }}</span>
                        </div>
                        <div class="flex items-center justify-between text-slate-300">
                          <span class="text-slate-500">Recv:</span>
                          <span [class]="validationOutcome()!.hashesMatch ? 'text-emerald-400' : 'text-rose-400'" class="truncate max-w-sm">
                            {{ validationOutcome()!.receivedHash }}
                          </span>
                        </div>
                      </div>
                    </div>

                    <div>
                      <span class="text-slate-400 block text-[10px] uppercase font-mono mb-1">Data Check String (Alphabetically Sorted)</span>
                      <pre class="p-2.5 bg-slate-950 rounded-lg border border-slate-800 font-mono text-[10px] text-slate-300 whitespace-pre-wrap">{{ validationOutcome()!.dataCheckString }}</pre>
                    </div>

                    <div>
                      <span class="text-slate-400 block text-[10px] uppercase font-mono mb-1">Decoded User Payload</span>
                      <pre class="p-2.5 bg-slate-950 rounded-lg border border-slate-800 font-mono text-[10px] text-slate-300 whitespace-pre-wrap">{{ validationOutcome()!.userJson }}</pre>
                    </div>
                  </div>
                }
              </div>
            </div>
          </div>
        }

        <!-- 4. AUDIENCE BROADCAST CONSOLE -->
        @if (activeTab() === 'broadcast') {
          <div class="grid grid-cols-1 lg:grid-cols-12 gap-8 items-start">
            <div class="lg:col-span-6 space-y-5">
              <div class="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4">
                <div class="flex items-center justify-between">
                  <div>
                    <h3 class="text-sm font-bold text-slate-100">Broadcast Campaign Composer</h3>
                    <p class="text-xs text-slate-400">Queue messages to subscriber cohorts with anti-ban rate limiting.</p>
                  </div>
                  <span class="px-2 py-0.5 rounded text-[10px] font-mono bg-indigo-500/10 text-indigo-400 border border-indigo-500/20">
                    Max 30 msgs/sec
                  </span>
                </div>

                <div class="space-y-3 text-xs">
                  <div>
                    <label class="block text-slate-400 font-medium mb-1">Campaign Name</label>
                    <input
                      type="text"
                      [(ngModel)]="broadcastForm.campaignName"
                      placeholder="e.g. Q4 Flash Sale VIP Teaser"
                      class="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-slate-100 text-xs focus:outline-none focus:border-sky-500" />
                  </div>

                  <div>
                    <label class="block text-slate-400 font-medium mb-1">Target Audience Segment</label>
                    <select
                      [(ngModel)]="broadcastForm.targetAudience"
                      class="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-slate-100 text-xs focus:outline-none focus:border-sky-500">
                      <option value="AllSubscribers">All Verified Subscribers ({{ metrics().subscribersCount }} recipients)</option>
                      <option value="VipOnly">VIP Members Only</option>
                      <option value="CatalogBrowsers">Active Catalog Browsers</option>
                      <option value="ActiveLast7Days">Subscribed in Last 7 Days</option>
                    </select>
                  </div>

                  <div>
                    <label class="block text-slate-400 font-medium mb-1">Broadcast Message Body</label>
                    <textarea
                      rows="4"
                      [(ngModel)]="broadcastForm.messageText"
                      placeholder="Write broadcast message..."
                      class="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-slate-100 text-xs focus:outline-none focus:border-sky-500"></textarea>
                  </div>

                  <div class="p-3 bg-slate-950 rounded-xl border border-slate-800 space-y-2">
                    <div class="flex items-center justify-between">
                      <span class="text-slate-300 font-medium">Pacing Rate: {{ broadcastForm.rateLimit }} msgs/sec</span>
                      <span class="text-[10px] text-slate-500 font-mono">Telegram Cap: 30</span>
                    </div>
                    <input
                      type="range"
                      min="1"
                      max="30"
                      [(ngModel)]="broadcastForm.rateLimit"
                      class="w-full accent-sky-500" />
                  </div>

                  <div class="flex items-center gap-2 pt-1">
                    <input
                      type="checkbox"
                      id="floodWaitCheck"
                      [(ngModel)]="broadcastForm.simulateFloodWait"
                      class="rounded bg-slate-800 border-slate-700 text-sky-500 focus:ring-0" />
                    <label for="floodWaitCheck" class="text-slate-300 text-xs">
                      Simulate Telegram HTTP 429 Flood-Wait & Exponential Backoff
                    </label>
                  </div>

                  <button
                    type="button"
                    (click)="dispatchBroadcast()"
                    [disabled]="isBroadcasting()"
                    class="w-full py-2.5 rounded-xl bg-sky-600 hover:bg-sky-500 disabled:bg-slate-800 disabled:text-slate-500 text-white font-semibold text-xs shadow-md shadow-sky-600/30 flex items-center justify-center gap-2 transition-all">
                    @if (isBroadcasting()) {
                      <svg class="animate-spin w-4 h-4 text-white" fill="none" viewBox="0 0 24 24">
                        <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                        <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z"></path>
                      </svg>
                      <span>Pacing & Dispatching Broadcast...</span>
                    } @else {
                      <span>Dispatch Broadcast Campaign</span>
                    }
                  </button>
                </div>
              </div>
            </div>

            <!-- Broadcast Delivery History -->
            <div class="lg:col-span-6 space-y-4">
              <div class="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl">
                <h3 class="text-sm font-bold text-slate-100 mb-3">Broadcast Log & Resilience History</h3>
                <div class="space-y-3">
                  @for (log of broadcastLogs(); track log.campaignId) {
                    <div class="p-3 rounded-xl bg-slate-950 border border-slate-800 text-xs space-y-2">
                      <div class="flex items-center justify-between">
                        <span class="font-bold text-slate-200">{{ log.campaignName }}</span>
                        <span class="px-2 py-0.5 rounded-full text-[10px] font-semibold bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
                          {{ log.status }}
                        </span>
                      </div>
                      <div class="grid grid-cols-3 gap-2 text-[11px] text-slate-400 font-mono">
                        <div>Delivered: <span class="text-slate-100 font-bold">{{ log.deliveredCount }}/{{ log.totalRecipients }}</span></div>
                        <div>Duration: <span class="text-slate-100 font-bold">{{ log.durationMs }}ms</span></div>
                        <div>Flood-Wait Retries: <span [class]="log.retryCount > 0 ? 'text-amber-400 font-bold' : 'text-slate-400'">{{ log.retryCount }}</span></div>
                      </div>
                      <div class="text-[10px] text-slate-500 flex justify-between items-center">
                        <span>{{ log.campaignId }}</span>
                        <span>{{ log.dispatchedAt }}</span>
                      </div>
                    </div>
                  }
                </div>
              </div>
            </div>
          </div>
        }
      </main>
    </div>
  `
})
export class TelegramBotStudioComponent {
  // Navigation
  activeTab = signal<'simulator' | 'fsm' | 'sandbox' | 'broadcast'>('simulator');

  // Metrics
  metrics = signal({
    totalUpdates: 142,
    subscribersCount: 5,
    rateLimit: 30,
    broadcastDeliveryRate: 99.8
  });

  // Simulator State
  chatInput = '';
  isMiniAppOpen = signal(false);
  selectedMiniAppProduct = signal({ name: 'Growth Marketing Accelerator', price: 199 });

  currentUserState = signal<FsmSession>({
    userId: 90001,
    fullName: 'Alex Rivero',
    username: 'alex_growth',
    currentState: 'Idle',
    capturedEmail: null,
    capturedPhone: null,
    cartItems: [],
    cartTotal: 0,
    messageCount: 3,
    lastActive: 'Just now'
  });

  chatMessages = signal<ChatMessage[]>([
    {
      id: 'm1',
      sender: 'bot',
      text: '👋 Welcome to SGrowing Growth Engine!\n\nI am your conversational AI marketing & Mini-App bot assistant. Browse our omnichannel catalog, subscribe for VIP growth vouchers, or launch our interactive Telegram Mini App!',
      timestamp: '09:41 AM',
      replyMarkup: {
        rows: [
          [
            { text: '🛍️ Browse Catalog', callbackData: 'action:browse_catalog' },
            { text: '⭐ VIP Offers', callbackData: 'action:subscribe_flow' }
          ],
          [
            { text: '🚀 Open Mini App Store', webAppUrl: 'https://miniapp.sgrowing.io/shop' },
            { text: 'ℹ️ Help & FAQ', callbackData: 'action:help' }
          ]
        ]
      }
    }
  ]);

  // FSM Sessions
  activeSessions = signal<FsmSession[]>([
    {
      userId: 108274912,
      fullName: 'Elena Rostova',
      username: 'elena_marketing',
      currentState: 'Subscribed',
      capturedEmail: 'elena@growthscale.io',
      capturedPhone: '+15550192831',
      cartItems: ['Omnichannel Suite', 'Growth Accelerator'],
      cartTotal: 548.00,
      messageCount: 12,
      lastActive: '25m ago'
    },
    {
      userId: 293847102,
      fullName: 'Marcus Vance',
      username: 'mvance_ops',
      currentState: 'CatalogBrowsing',
      capturedEmail: 'marcus@vancetech.com',
      capturedPhone: null,
      cartItems: ['AI Customer Voice Agent'],
      cartTotal: 499.00,
      messageCount: 5,
      lastActive: '3h ago'
    },
    {
      userId: 384729104,
      fullName: 'Sofia Chen',
      username: 'sofia_chen_ai',
      currentState: 'CheckoutPending',
      capturedEmail: 'sofia@chenenterprises.com',
      capturedPhone: null,
      cartItems: ['Growth Accelerator', 'AI Customer Voice Agent'],
      cartTotal: 698.00,
      messageCount: 7,
      lastActive: '5m ago'
    },
    {
      userId: 492817305,
      fullName: 'David Miller',
      username: 'dmiller_dev',
      currentState: 'Idle',
      capturedEmail: null,
      capturedPhone: null,
      cartItems: [],
      cartTotal: 0.00,
      messageCount: 2,
      lastActive: '1d ago'
    },
    {
      userId: 583920194,
      fullName: 'Aisha Patel',
      username: 'aisha_p',
      currentState: 'Subscribed',
      capturedEmail: 'aisha@patelgrowth.org',
      capturedPhone: '+15550172938',
      cartItems: ['Omnichannel Suite'],
      cartTotal: 349.00,
      messageCount: 9,
      lastActive: '1h ago'
    }
  ]);

  // Mini App Sandbox State
  sandboxBotToken = '7123456789:AAHTestToken_SGrowingEnterpriseMarketingBot';
  sandboxInitData = '';
  validationOutcome = signal<{
    isValid: boolean;
    isExpired: boolean;
    hashesMatch: boolean;
    calculatedHash: string;
    receivedHash: string;
    dataCheckString: string;
    userJson: string;
    message: string;
  } | null>(null);

  // Broadcast State
  isBroadcasting = signal(false);
  broadcastForm = {
    campaignName: 'Q4 VIP Omnichannel Flash Deal',
    targetAudience: 'AllSubscribers',
    messageText: '⚡ Exclusive VIP Offer: Accelerate your Q4 growth with 25% off all SGrowing AI modules. Tap below to claim!',
    rateLimit: 30,
    simulateFloodWait: true
  };

  broadcastLogs = signal<BroadcastLogEntry[]>([
    {
      campaignId: 'BCST-20261031-89FA12',
      campaignName: 'Q4 Omnichannel Launch VIP Teaser',
      targetAudience: 'AllSubscribers',
      totalRecipients: 5,
      deliveredCount: 5,
      failedCount: 0,
      retryCount: 0,
      floodWaitCount: 0,
      status: 'Completed',
      durationMs: 145,
      dispatchedAt: '2 hours ago'
    }
  ]);

  constructor() {
    this.generateSandboxInitData();
  }

  // --- Simulator Actions ---

  sendMessage() {
    const text = this.chatInput.trim();
    if (!text) return;

    this.chatInput = '';
    const now = this.formatTime();

    // Append user message
    this.chatMessages.update(msgs => [
      ...msgs,
      { id: 'u_' + Date.now(), sender: 'user', text, timestamp: now }
    ]);

    // Update metrics
    this.metrics.update(m => ({ ...m, totalUpdates: m.totalUpdates + 1 }));

    // Process conversational FSM
    this.processFsmInput(text);
  }

  sendQuickCommand(cmd: string) {
    this.chatInput = cmd;
    this.sendMessage();
  }

  simulateContactShare() {
    const now = this.formatTime();
    this.chatMessages.update(msgs => [
      ...msgs,
      { id: 'u_' + Date.now(), sender: 'user', text: '📱 Contact Shared: +1 555 019 4821 (Alex Rivero)', timestamp: now }
    ]);

    this.currentUserState.update(s => ({
      ...s,
      capturedPhone: '+1 555 019 4821',
      currentState: 'Subscribed'
    }));

    setTimeout(() => {
      this.chatMessages.update(msgs => [
        ...msgs,
        {
          id: 'b_' + Date.now(),
          sender: 'bot',
          text: '🎉 Phone verified! You are now subscribed as an SGrowing VIP member with exclusive 20% discounts.',
          timestamp: this.formatTime(),
          replyMarkup: {
            rows: [
              [
                { text: '🚀 Open Mini App Store', webAppUrl: 'https://miniapp.sgrowing.io/shop' },
                { text: '📦 View Catalog', callbackData: 'action:browse_catalog' }
              ],
              [
                { text: '👤 My Account', callbackData: 'action:status' }
              ]
            ]
          }
        }
      ]);
    }, 400);
  }

  handleButtonClick(callbackData: string) {
    const now = this.formatTime();
    this.metrics.update(m => ({ ...m, totalUpdates: m.totalUpdates + 1 }));

    if (callbackData === 'action:browse_catalog') {
      this.currentUserState.update(s => ({ ...s, currentState: 'CatalogBrowsing' }));
      this.chatMessages.update(msgs => [
        ...msgs,
        {
          id: 'b_' + Date.now(),
          sender: 'bot',
          text: '🛍️ SGrowing Product Solutions Catalog:\n\n1. Growth Marketing Accelerator ($199.00)\n2. Omnichannel Suite ($349.00)\n3. AI Customer Voice Agent ($499.00)',
          timestamp: now,
          replyMarkup: {
            rows: [
              [
                { text: '➕ Accelerator ($199)', callbackData: 'cart:add:acc' },
                { text: '➕ Suite ($349)', callbackData: 'cart:add:suite' }
              ],
              [
                { text: '➕ Voice Agent ($499)', callbackData: 'cart:add:voice' },
                { text: '🛒 Checkout', callbackData: 'action:checkout' }
              ]
            ]
          }
        }
      ]);
    } else if (callbackData.startsWith('cart:add:')) {
      const prod = callbackData.includes('suite')
        ? { name: 'Omnichannel Suite', price: 349 }
        : (callbackData.includes('voice') ? { name: 'AI Voice Agent', price: 499 } : { name: 'Growth Accelerator', price: 199 });

      this.currentUserState.update(s => ({
        ...s,
        cartItems: [...s.cartItems, prod.name],
        cartTotal: s.cartTotal + prod.price
      }));

      this.chatMessages.update(msgs => [
        ...msgs,
        {
          id: 'b_' + Date.now(),
          sender: 'bot',
          text: `🛍️ Added ${prod.name} ($${prod.price}.00) to your cart!\n\nCurrent Cart Total: $${this.currentUserState().cartTotal.toFixed(2)}`,
          timestamp: now,
          replyMarkup: {
            rows: [
              [
                { text: '🛒 Proceed to Checkout', callbackData: 'action:checkout' },
                { text: '🛍️ Add More', callbackData: 'action:browse_catalog' }
              ]
            ]
          }
        }
      ]);
    } else if (callbackData === 'action:checkout') {
      this.currentUserState.update(s => ({ ...s, currentState: 'CheckoutPending' }));
      this.chatMessages.update(msgs => [
        ...msgs,
        {
          id: 'b_' + Date.now(),
          sender: 'bot',
          text: `🛒 Checkout for ${this.currentUserState().fullName}:\n\nTotal Due: $${this.currentUserState().cartTotal.toFixed(2)}\n\nPlease reply with your business email to finalize your order, or click below to launch the Mini App checkout:`,
          timestamp: now,
          replyMarkup: {
            rows: [
              [
                { text: '💳 Pay in Mini App', webAppUrl: 'https://miniapp.sgrowing.io/shop' }
              ]
            ]
          }
        }
      ]);
    } else if (callbackData === 'action:subscribe_flow') {
      this.currentUserState.update(s => ({ ...s, currentState: 'AwaitingEmail' }));
      this.chatMessages.update(msgs => [
        ...msgs,
        {
          id: 'b_' + Date.now(),
          sender: 'bot',
          text: '⭐ VIP Member Onboarding\n\nPlease enter your business email address to unlock exclusive vouchers and market playbooks:',
          timestamp: now
        }
      ]);
    } else if (callbackData === 'action:status') {
      const s = this.currentUserState();
      this.chatMessages.update(msgs => [
        ...msgs,
        {
          id: 'b_' + Date.now(),
          sender: 'bot',
          text: `👤 Account Profile:\n• State: ${s.currentState}\n• Email: ${s.capturedEmail || 'Not set'}\n• Phone: ${s.capturedPhone || 'Not set'}\n• Cart: ${s.cartItems.length} items ($${s.cartTotal.toFixed(2)})`,
          timestamp: now,
          replyMarkup: {
            rows: [
              [
                { text: '🛍️ Browse Catalog', callbackData: 'action:browse_catalog' }
              ]
            ]
          }
        }
      ]);
    } else if (callbackData === 'action:help') {
      this.chatMessages.update(msgs => [
        ...msgs,
        {
          id: 'b_' + Date.now(),
          sender: 'bot',
          text: 'ℹ️ Commands:\n/start - Main menu\n/catalog - Store\n/subscribe - VIP Club\n/checkout - Finalize cart\n/reset - Reset session',
          timestamp: now
        }
      ]);
    }
  }

  processFsmInput(input: string) {
    const now = this.formatTime();
    const state = this.currentUserState().currentState;

    if (input.startsWith('/start')) {
      this.currentUserState.update(s => ({ ...s, currentState: 'Idle' }));
      setTimeout(() => {
        this.chatMessages.update(msgs => [
          ...msgs,
          {
            id: 'b_' + Date.now(),
            sender: 'bot',
            text: '👋 Main Menu: How can I help your business grow today?',
            timestamp: now,
            replyMarkup: {
              rows: [
                [
                  { text: '🛍️ Browse Catalog', callbackData: 'action:browse_catalog' },
                  { text: '⭐ VIP Offers', callbackData: 'action:subscribe_flow' }
                ],
                [
                  { text: '🚀 Open Mini App', webAppUrl: 'https://miniapp.sgrowing.io/shop' }
                ]
              ]
            }
          }
        ]);
      }, 300);
      return;
    }

    if (input.startsWith('/reset')) {
      this.currentUserState.update(s => ({
        ...s,
        currentState: 'Idle',
        cartItems: [],
        cartTotal: 0,
        capturedEmail: null,
        capturedPhone: null
      }));
      setTimeout(() => {
        this.chatMessages.update(msgs => [
          ...msgs,
          { id: 'b_' + Date.now(), sender: 'bot', text: '🔄 Session reset to default Idle state.', timestamp: now }
        ]);
      }, 300);
      return;
    }

    if (state === 'AwaitingEmail' || state === 'CheckoutPending') {
      if (input.includes('@') && input.includes('.')) {
        this.currentUserState.update(s => ({
          ...s,
          capturedEmail: input,
          currentState: 'AwaitingPhone'
        }));
        setTimeout(() => {
          this.chatMessages.update(msgs => [
            ...msgs,
            {
              id: 'b_' + Date.now(),
              sender: 'bot',
              text: `✅ Email captured: ${input}\n\nNow please reply with your mobile phone number for instant delivery alerts:`,
              timestamp: now
            }
          ]);
        }, 300);
      } else {
        setTimeout(() => {
          this.chatMessages.update(msgs => [
            ...msgs,
            {
              id: 'b_' + Date.now(),
              sender: 'bot',
              text: '⚠️ Please enter a valid business email (e.g. alex@example.com):',
              timestamp: now
            }
          ]);
        }, 300);
      }
      return;
    }

    if (state === 'AwaitingPhone') {
      const digits = input.replace(/\D/g, '');
      if (digits.length >= 7) {
        this.currentUserState.update(s => ({
          ...s,
          capturedPhone: input,
          currentState: 'Subscribed'
        }));
        setTimeout(() => {
          this.chatMessages.update(msgs => [
            ...msgs,
            {
              id: 'b_' + Date.now(),
              sender: 'bot',
              text: `🎉 You are now officially subscribed as an SGrowing VIP member!`,
              timestamp: now,
              replyMarkup: {
                rows: [
                  [
                    { text: '🛍️ Shop Catalog', callbackData: 'action:browse_catalog' },
                    { text: '🚀 Open Mini App', webAppUrl: 'https://miniapp.sgrowing.io/shop' }
                  ]
                ]
              }
            }
          ]);
        }, 300);
      } else {
        setTimeout(() => {
          this.chatMessages.update(msgs => [
            ...msgs,
            {
              id: 'b_' + Date.now(),
              sender: 'bot',
              text: '⚠️ Please provide a valid phone number with country code (e.g. +1 555 019 2831):',
              timestamp: now
            }
          ]);
        }, 300);
      }
      return;
    }

    // Default fallback
    setTimeout(() => {
      this.chatMessages.update(msgs => [
        ...msgs,
        {
          id: 'b_' + Date.now(),
          sender: 'bot',
          text: '💡 Type /catalog to view solutions, /subscribe for VIP offers, or /start for menu.',
          timestamp: now
        }
      ]);
    }, 300);
  }

  openMiniApp(url?: string) {
    this.isMiniAppOpen.set(true);
  }

  submitMiniAppOrder() {
    this.isMiniAppOpen.set(false);
    const prod = this.selectedMiniAppProduct();
    const orderId = 'ORD-' + Math.random().toString(36).substring(2, 8).toUpperCase();
    const now = this.formatTime();

    this.currentUserState.update(s => ({
      ...s,
      currentState: 'Subscribed',
      cartItems: [prod.name],
      cartTotal: prod.price
    }));

    this.chatMessages.update(msgs => [
      ...msgs,
      {
        id: 'b_' + Date.now(),
        sender: 'bot',
        text: `🎉 Telegram Mini App Order Received!\n\nOrder Reference: #${orderId}\nTotal Charged: $${prod.price}.00\nItem: ${prod.name}\n\nYour receipt and access tokens are confirmed!`,
        timestamp: now,
        replyMarkup: {
          rows: [
            [
              { text: '👤 View Profile', callbackData: 'action:status' },
              { text: '🛍️ Shop More', callbackData: 'action:browse_catalog' }
            ]
          ]
        }
      }
    ]);
  }

  resetChat() {
    this.chatMessages.set([
      {
        id: 'm1',
        sender: 'bot',
        text: '👋 Welcome to SGrowing Growth Engine!\n\nI am your conversational AI marketing & Mini-App bot assistant. Browse our omnichannel catalog, subscribe for VIP growth vouchers, or launch our interactive Telegram Mini App!',
        timestamp: '09:41 AM',
        replyMarkup: {
          rows: [
            [
              { text: '🛍️ Browse Catalog', callbackData: 'action:browse_catalog' },
              { text: '⭐ VIP Offers', callbackData: 'action:subscribe_flow' }
            ],
            [
              { text: '🚀 Open Mini App Store', webAppUrl: 'https://miniapp.sgrowing.io/shop' },
              { text: 'ℹ️ Help & FAQ', callbackData: 'action:help' }
            ]
          ]
        }
      }
    ]);
    this.currentUserState.update(s => ({
      ...s,
      currentState: 'Idle',
      cartItems: [],
      cartTotal: 0
    }));
  }

  // --- FSM Actions ---

  refreshSessions() {
    // Simulates live session reload
    this.metrics.update(m => ({ ...m, totalUpdates: m.totalUpdates + 2 }));
  }

  resetSpecificSession(userId: number) {
    this.activeSessions.update(list =>
      list.map(s => s.userId === userId ? { ...s, currentState: 'Idle' as TelegramConversationState, cartItems: [], cartTotal: 0 } : s)
    );
  }

  getStatePillClass(state: TelegramConversationState): string {
    switch (state) {
      case 'Idle':
        return 'bg-slate-700/40 text-slate-300 border-slate-600/50';
      case 'CatalogBrowsing':
        return 'bg-purple-500/10 text-purple-400 border-purple-500/30';
      case 'AwaitingEmail':
        return 'bg-amber-500/10 text-amber-400 border-amber-500/30';
      case 'AwaitingPhone':
        return 'bg-blue-500/10 text-blue-400 border-blue-500/30';
      case 'CheckoutPending':
        return 'bg-rose-500/10 text-rose-400 border-rose-500/30';
      case 'Subscribed':
        return 'bg-emerald-500/10 text-emerald-400 border-emerald-500/30';
      default:
        return 'bg-slate-700/40 text-slate-300 border-slate-600/50';
    }
  }

  // --- Mini App HMAC Sandbox ---

  generateSandboxInitData() {
    const authDate = Math.floor(Date.now() / 1000);
    const userJson = JSON.stringify({
      id: 108274912,
      first_name: 'Elena',
      last_name: 'Rostova',
      username: 'elena_marketing',
      language_code: 'en'
    });

    const checkString = `auth_date=${authDate}\nquery_id=AAHdF6IQAAAAAN0XohDhrOrc\nuser=${userJson}`;
    // Simulate HMAC hex for UI representation
    const simulatedHash = '7d89e5a1b32f4c90e123456789abcdef0123456789abcdef0123456789abcdef';

    this.sandboxInitData = `auth_date=${authDate}&query_id=AAHdF6IQAAAAAN0XohDhrOrc&user=${encodeURIComponent(userJson)}&hash=${simulatedHash}`;
    this.runValidation();
  }

  tamperSandboxInitData() {
    if (!this.sandboxInitData) return;
    this.sandboxInitData = this.sandboxInitData.replace('108274912', '999999999');
    this.runValidation();
  }

  expireSandboxInitData() {
    if (!this.sandboxInitData) return;
    const oldDate = Math.floor(Date.now() / 1000) - 30 * 3600;
    this.sandboxInitData = this.sandboxInitData.replace(/auth_date=\d+/, `auth_date=${oldDate}`);
    this.runValidation();
  }

  runValidation() {
    if (!this.sandboxInitData.includes('hash=')) {
      this.validationOutcome.set({
        isValid: false,
        isExpired: false,
        hashesMatch: false,
        calculatedHash: 'none',
        receivedHash: 'missing',
        dataCheckString: '',
        userJson: '',
        message: 'Missing required hash parameter in initData.'
      });
      return;
    }

    const isTampered = this.sandboxInitData.includes('999999999');
    const authDateMatch = this.sandboxInitData.match(/auth_date=(\d+)/);
    const authDate = authDateMatch ? parseInt(authDateMatch[1], 10) : 0;
    const isExpired = (Math.floor(Date.now() / 1000) - authDate) > 24 * 3600;

    const userMatch = this.sandboxInitData.match(/user=([^&]+)/);
    const userJson = userMatch ? decodeURIComponent(userMatch[1]) : '';

    const expectedHash = '7d89e5a1b32f4c90e123456789abcdef0123456789abcdef0123456789abcdef';
    const currentHash = isTampered ? 'a9f1b2c3d4e5f60718293a4b5c6d7e8f90123456789abcdef0123456789abcdef' : expectedHash;

    const isValid = !isTampered && !isExpired;

    this.validationOutcome.set({
      isValid,
      isExpired,
      hashesMatch: !isTampered,
      calculatedHash: expectedHash,
      receivedHash: currentHash,
      dataCheckString: `auth_date=${authDate}\nquery_id=AAHdF6IQAAAAAN0XohDhrOrc\nuser=${userJson}`,
      userJson: userJson ? JSON.stringify(JSON.parse(userJson), null, 2) : '',
      message: isValid
        ? 'Secret key = HMAC_SHA256("WebAppData", botToken). Calculated HMAC matches received hash. Auth date is within 24h.'
        : (isExpired ? 'Auth timestamp exceeds max allowed age (24h).' : 'Calculated HMAC mismatch: Payload was altered after signing.')
    });
  }

  // --- Broadcast Dispatcher ---

  dispatchBroadcast() {
    this.isBroadcasting.set(true);

    setTimeout(() => {
      this.isBroadcasting.set(false);

      const newLog: BroadcastLogEntry = {
        campaignId: 'BCST-' + Date.now().toString(36).toUpperCase(),
        campaignName: this.broadcastForm.campaignName,
        targetAudience: this.broadcastForm.targetAudience,
        totalRecipients: 5,
        deliveredCount: 5,
        failedCount: 0,
        retryCount: this.broadcastForm.simulateFloodWait ? 1 : 0,
        floodWaitCount: this.broadcastForm.simulateFloodWait ? 1 : 0,
        status: 'Completed',
        durationMs: 165,
        dispatchedAt: 'Just now'
      };

      this.broadcastLogs.update(logs => [newLog, ...logs]);
      this.metrics.update(m => ({
        ...m,
        totalUpdates: m.totalUpdates + 5
      }));
    }, 1200);
  }

  private formatTime(): string {
    const d = new Date();
    return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }
}
