import { Component, signal, computed, inject, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { TranslationService } from '../../core/services/translation.service';
import { LanguageSwitcherComponent } from '../../core/components/language-switcher.component';

export type PairingStatus = 'Disconnected' | 'PendingEntry' | 'Connected' | 'Syncing';

export interface CountryPreset {
  name: string;
  code: string;
  dialCode: string;
  flag: string;
  placeholder: string;
}

export interface SimulatedOrder {
  id: string;
  orderNumber: string;
  customerName: string;
  customerPhone: string;
  shippingAddress: string;
  totalAmount: number;
  currency: string;
  status: 'PendingConfirmation' | 'Confirmed' | 'AddressUpdated' | 'Cancelled' | 'TimedOut';
  confirmedVia?: string;
  notes?: string;
  sentMinutesAgo: number;
  requiresPhoneCall: boolean;
  reservationStatus: 'Active' | 'Released';
}

@Component({
  selector: 'app-whatsapp-pairing-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LanguageSwitcherComponent],
  template: `
    <div class="min-h-screen bg-slate-950 text-slate-100 flex flex-col font-sans" dir="ltr">
      <!-- Top Navigation Bar -->
      <header class="border-b border-slate-800 bg-slate-900/70 backdrop-blur px-6 py-4 flex items-center justify-between sticky top-0 z-50">
        <div class="flex items-center gap-4">
          <a routerLink="/" class="text-xl font-black bg-gradient-to-r from-emerald-400 via-teal-300 to-cyan-400 bg-clip-text text-transparent hover:opacity-90">
            SGrowing
          </a>
          <span class="text-slate-600">/</span>
          <div class="flex items-center gap-2">
            <span class="text-sm font-semibold text-slate-200">WhatsApp Multi-Device Gateway</span>
            <span class="text-xs px-2.5 py-0.5 rounded-full bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 font-medium">
              8-Digit Pairing Code & COD Bot
            </span>
          </div>
        </div>

        <div class="flex items-center gap-3">
          <div class="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-slate-800/80 border border-slate-700/60 text-xs">
            <span class="text-slate-400">Gateway Engine:</span>
            <span class="text-emerald-400 font-mono font-semibold">Baileys Noise_XX + TikTak PRO</span>
          </div>
          <app-language-switcher />
        </div>
      </header>

      <main class="flex-1 p-6 max-w-7xl w-full mx-auto space-y-6">
        <!-- Sub-header & Status Strip -->
        <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 bg-slate-900/60 border border-slate-800 rounded-2xl p-6 shadow-xl backdrop-blur">
          <div>
            <h1 class="text-2xl font-black text-white flex items-center gap-3">
              <span>WhatsApp 8-Digit Pairing Handshake</span>
              <!-- Live Connection Status Badge -->
              <span [class]="statusBadgeClasses()">
                <span class="w-2 h-2 rounded-full" [class]="statusDotClasses()"></span>
                {{ connectionStatus() }}
              </span>
            </h1>
            <p class="text-xs text-slate-400 mt-1 max-w-2xl">
              Zero-browser headless Multi-Device Noise handshake protocol. Connect any physical WhatsApp phone in 10 seconds via human-readable 8-digit OTP without scanning QR codes.
            </p>
          </div>

          <div class="flex items-center gap-3">
            @if (connectionStatus() === 'Connected') {
              <button
                (click)="disconnectInstance()"
                class="px-4 py-2 rounded-xl bg-rose-500/10 hover:bg-rose-500/20 text-rose-400 border border-rose-500/30 text-xs font-semibold transition flex items-center gap-2">
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1"/></svg>
                Disconnect Device
              </button>
            }
            <button
              (click)="triggerTimeoutSweep()"
              [disabled]="isSweeping()"
              class="px-4 py-2 rounded-xl bg-amber-500/10 hover:bg-amber-500/20 text-amber-400 border border-amber-500/30 text-xs font-semibold transition flex items-center gap-2">
              <svg class="w-4 h-4" [class.animate-spin]="isSweeping()" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"/></svg>
              Run 30m Fallback Sweep
            </button>
          </div>
        </div>

        <!-- Upper Grid: Pairing Request & Giant Pairing Code Card -->
        <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
          <!-- Left: Phone Input & Country Presets (5 cols) -->
          <div class="lg:col-span-5 bg-slate-900/80 border border-slate-800 rounded-2xl p-6 shadow-xl flex flex-col justify-between">
            <div>
              <div class="flex items-center justify-between mb-4">
                <h2 class="text-sm font-bold text-white uppercase tracking-wider flex items-center gap-2">
                  <span class="w-2 h-2 rounded-full bg-cyan-400"></span>
                  1. Link Phone Number
                </h2>
                <span class="text-xs text-slate-400 font-mono">E.164 Format</span>
              </div>

              <!-- Country Presets -->
              <div class="mb-4">
                <label class="block text-xs font-medium text-slate-400 mb-2">Quick Regional Presets</label>
                <div class="grid grid-cols-3 gap-2">
                  @for (c of countryPresets; track c.code) {
                    <button
                      type="button"
                      (click)="selectPreset(c)"
                      [class.border-cyan-500]="selectedCountry() === c.code"
                      [class.bg-cyan-500/10]="selectedCountry() === c.code"
                      class="px-2.5 py-2 rounded-xl border border-slate-800 bg-slate-950/60 hover:bg-slate-800/60 text-xs flex items-center justify-center gap-1.5 transition text-left">
                      <span class="text-base">{{ c.flag }}</span>
                      <span class="font-bold text-slate-200">{{ c.dialCode }}</span>
                    </button>
                  }
                </div>
              </div>

              <!-- Phone Number Input -->
              <div class="space-y-4">
                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1.5">WhatsApp Master Phone Number</label>
                  <div class="relative">
                    <span class="absolute inset-y-0 left-0 pl-3.5 flex items-center text-slate-500 text-sm font-mono">+</span>
                    <input
                      type="text"
                      [(ngModel)]="phoneNumberInput"
                      placeholder="21698123456"
                      class="w-full bg-slate-950 border border-slate-700/80 focus:border-cyan-400 focus:ring-1 focus:ring-cyan-400 rounded-xl pl-8 pr-4 py-2.5 text-sm font-mono text-white placeholder-slate-600 transition" />
                  </div>
                  <p class="text-[11px] text-slate-500 mt-1">Accepts international E.164 (e.g. +216 98 123 456, +20 10 1234 5678, +212 6 1234 5678)</p>
                </div>

                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1.5">Instance Alias (Multi-Tenant Identifier)</label>
                  <input
                    type="text"
                    [(ngModel)]="instanceNameInput"
                    placeholder="default"
                    class="w-full bg-slate-950 border border-slate-700/80 focus:border-cyan-400 focus:ring-1 focus:ring-cyan-400 rounded-xl px-4 py-2.5 text-sm font-mono text-white placeholder-slate-600 transition" />
                </div>
              </div>
            </div>

            <div class="pt-6 mt-6 border-t border-slate-800">
              <button
                (click)="requestPairingCode()"
                [disabled]="isLoadingPairingCode() || !phoneNumberInput.trim()"
                class="w-full py-3 px-4 rounded-xl bg-gradient-to-r from-emerald-500 to-teal-600 hover:from-emerald-400 hover:to-teal-500 text-white font-bold text-sm shadow-lg shadow-emerald-500/20 disabled:opacity-50 disabled:cursor-not-allowed transition flex items-center justify-center gap-2">
                @if (isLoadingPairingCode()) {
                  <svg class="w-4 h-4 animate-spin text-white" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z"/></svg>
                  <span>Generating Noise Keypair...</span>
                } @else {
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 18h.01M8 21h8a2 2 0 002-2V5a2 2 0 00-2-2H8a2 2 0 00-2 2v14a2 2 0 002 2z"/></svg>
                  <span>Request 8-Digit Pairing Code</span>
                }
              </button>
            </div>
          </div>

          <!-- Right: Giant High-Contrast Pairing Code Card (7 cols) -->
          <div class="lg:col-span-7 bg-slate-900/80 border border-slate-800 rounded-2xl p-6 shadow-xl flex flex-col justify-between relative overflow-hidden">
            <!-- Background Glow -->
            <div class="absolute -right-20 -top-20 w-64 h-64 bg-emerald-500/10 rounded-full blur-3xl pointer-events-none"></div>

            <div>
              <div class="flex items-center justify-between mb-4">
                <h2 class="text-sm font-bold text-white uppercase tracking-wider flex items-center gap-2">
                  <span class="w-2 h-2 rounded-full bg-emerald-400"></span>
                  2. Companion Device Pairing OTP
                </h2>
                <div class="flex items-center gap-2 text-xs font-mono text-slate-400">
                  <span class="w-2 h-2 rounded-full" [class.bg-emerald-400]="pairingCode()" [class.bg-slate-600]="!pairingCode()"></span>
                  TTL: <span class="text-emerald-400 font-bold">{{ formattedRemainingTime() }}</span>
                </div>
              </div>

              <!-- Giant Code Box -->
              <div class="bg-slate-950/90 border border-slate-700/80 rounded-2xl p-6 text-center shadow-inner relative group">
                <div class="text-xs uppercase font-mono tracking-widest text-slate-500 mb-2">WhatsApp Multi-Device Pairing Code</div>

                <div class="text-4xl md:text-5xl font-black font-mono tracking-widest text-emerald-400 drop-shadow-[0_0_15px_rgba(52,211,153,0.3)] select-all py-2">
                  {{ pairingCode() || '•••• - ••••' }}
                </div>

                <div class="mt-3 flex items-center justify-center gap-3">
                  <button
                    (click)="copyPairingCode()"
                    [disabled]="!pairingCode()"
                    class="px-4 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-xs font-semibold text-slate-200 border border-slate-700 transition flex items-center gap-1.5 disabled:opacity-40">
                    <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z"/></svg>
                    <span>{{ isCopied() ? 'Copied to Clipboard!' : 'Copy Code' }}</span>
                  </button>

                  <button
                    (click)="simulatePhoneConfirmation()"
                    [disabled]="!pairingCode() || connectionStatus() === 'Connected' || isSimulating()"
                    class="px-4 py-1.5 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-xs font-semibold text-white shadow transition flex items-center gap-1.5 disabled:opacity-40">
                    <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"/></svg>
                    <span>{{ isSimulating() ? 'Exchanging Noise Handshake...' : 'Simulate Phone Confirmation' }}</span>
                  </button>
                </div>

                <!-- 10-Minute Countdown Progress Bar -->
                <div class="w-full bg-slate-800 rounded-full h-1.5 mt-5 overflow-hidden">
                  <div
                    class="bg-gradient-to-r from-emerald-500 to-cyan-400 h-full transition-all duration-1000"
                    [style.width.%]="countdownPercent()"></div>
                </div>
              </div>

              <!-- Step by Step Instructions -->
              <div class="mt-6 space-y-2.5">
                <div class="text-xs font-bold text-slate-300 uppercase tracking-wider">How to Link on Your Mobile Phone:</div>
                <div class="grid grid-cols-1 md:grid-cols-2 gap-2 text-xs text-slate-400">
                  <div class="flex items-start gap-2 bg-slate-950/40 p-2.5 rounded-xl border border-slate-800/60">
                    <span class="w-5 h-5 rounded-full bg-emerald-500/20 text-emerald-400 font-bold flex items-center justify-center shrink-0 text-[11px]">1</span>
                    <span>Open <strong>WhatsApp</strong> on your primary smartphone.</span>
                  </div>
                  <div class="flex items-start gap-2 bg-slate-950/40 p-2.5 rounded-xl border border-slate-800/60">
                    <span class="w-5 h-5 rounded-full bg-emerald-500/20 text-emerald-400 font-bold flex items-center justify-center shrink-0 text-[11px]">2</span>
                    <span>Tap <strong>Linked Devices</strong> > <strong>Link a Device</strong>.</span>
                  </div>
                  <div class="flex items-start gap-2 bg-slate-950/40 p-2.5 rounded-xl border border-slate-800/60">
                    <span class="w-5 h-5 rounded-full bg-emerald-500/20 text-emerald-400 font-bold flex items-center justify-center shrink-0 text-[11px]">3</span>
                    <span>Select <strong>"Link with phone number instead"</strong> at the bottom.</span>
                  </div>
                  <div class="flex items-start gap-2 bg-slate-950/40 p-2.5 rounded-xl border border-slate-800/60">
                    <span class="w-5 h-5 rounded-full bg-emerald-500/20 text-emerald-400 font-bold flex items-center justify-center shrink-0 text-[11px]">4</span>
                    <span>Enter the 8-character code shown above. Connection takes ~3 seconds!</span>
                  </div>
                </div>
              </div>
            </div>

            <!-- Connected Session Details Footer -->
            @if (connectionStatus() === 'Connected') {
              <div class="mt-4 p-3 rounded-xl bg-emerald-500/10 border border-emerald-500/20 text-xs text-emerald-300 flex items-center justify-between">
                <div class="flex items-center gap-2">
                  <svg class="w-4 h-4 text-emerald-400 shrink-0" fill="currentColor" viewBox="0 0 20 20"><path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd"/></svg>
                  <span>Active Companion Session: <strong>Chrome/Linux (SGrowing-MultiDevice)</strong></span>
                </div>
                <span class="font-mono text-[11px] text-emerald-400">Noise_XX_25519_AESGCM</span>
              </div>
            }
          </div>
        </div>

        <!-- Lower Grid: Interactive COD Bot Simulator & Timeout Fallback Queue -->
        <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
          <!-- Left: Smartphone Simulator with Interactive COD Message (6 cols) -->
          <div class="lg:col-span-6 bg-slate-900/80 border border-slate-800 rounded-2xl p-6 shadow-xl flex flex-col items-center">
            <div class="w-full flex items-center justify-between mb-4">
              <h2 class="text-sm font-bold text-white uppercase tracking-wider flex items-center gap-2">
                <span class="w-2 h-2 rounded-full bg-teal-400"></span>
                Interactive COD Confirmation Bot Simulator
              </h2>
              <span class="text-xs px-2 py-0.5 rounded bg-teal-500/10 text-teal-400 border border-teal-500/20 font-mono">TikTak PRO Engine</span>
            </div>

            <!-- Mobile Phone Frame -->
            <div class="w-full max-w-sm rounded-[36px] p-3.5 bg-slate-950 border-4 border-slate-800 shadow-2xl relative">
              <!-- Notch/Speaker -->
              <div class="w-32 h-4 bg-slate-800 rounded-full mx-auto mb-3 flex items-center justify-center">
                <div class="w-12 h-1 bg-slate-900 rounded-full"></div>
                <div class="w-2 h-2 bg-slate-900 rounded-full ml-2"></div>
              </div>

              <!-- Phone Screen Content (WhatsApp Chat Layout) -->
              <div class="bg-[#0b141a] rounded-[24px] p-3 text-slate-100 flex flex-col h-[510px] overflow-hidden border border-slate-800/80 shadow-inner">
                <!-- WhatsApp Chat Header -->
                <div class="bg-[#202c33] p-2.5 rounded-xl flex items-center justify-between border-b border-[#2a3942] mb-3">
                  <div class="flex items-center gap-2.5">
                    <div class="w-8 h-8 rounded-full bg-emerald-700 flex items-center justify-center font-bold text-white text-xs">SG</div>
                    <div>
                      <div class="text-xs font-semibold text-slate-100 flex items-center gap-1">
                        <span>SGrowing Orders Bot</span>
                        <svg class="w-3 h-3 text-emerald-400" fill="currentColor" viewBox="0 0 20 20"><path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd"/></svg>
                      </div>
                      <div class="text-[10px] text-emerald-400">Official Business Account</div>
                    </div>
                  </div>
                  <span class="text-slate-400 text-xs">⋮</span>
                </div>

                <!-- Chat Messages Scroll Area -->
                <div class="flex-1 overflow-y-auto space-y-3 px-1 py-2">
                  <!-- Incoming WhatsApp Interactive COD Card -->
                  <div class="bg-[#202c33] rounded-2xl rounded-tl-sm p-3.5 shadow-md border border-[#2a3942]/60 max-w-[95%]">
                    <!-- Header -->
                    <div class="text-[11px] font-bold text-emerald-400 mb-1 flex items-center gap-1.5">
                      <span>📦</span>
                      <span>Cash-on-Delivery Order Confirmation</span>
                    </div>

                    <!-- Body -->
                    <div class="text-xs text-slate-200 space-y-1.5 leading-relaxed">
                      <p>Hello <strong>{{ activeSimulatedOrder().customerName }}</strong>!</p>
                      <p class="text-slate-300">
                        Thank you for your order <strong>#{{ activeSimulatedOrder().orderNumber }}</strong> on SGrowing Express.
                      </p>
                      <div class="bg-[#111b21] p-2 rounded-lg border border-slate-700/40 text-[11px] font-mono space-y-0.5">
                        <div class="flex justify-between text-slate-400">
                          <span>Amount to Pay (COD):</span>
                          <span class="text-emerald-400 font-bold">{{ activeSimulatedOrder().totalAmount }} {{ activeSimulatedOrder().currency }}</span>
                        </div>
                        <div class="flex justify-between text-slate-400">
                          <span>Address:</span>
                          <span class="text-slate-300 truncate max-w-[170px]">{{ activeSimulatedOrder().shippingAddress }}</span>
                        </div>
                      </div>
                      <p class="text-[11px] text-slate-300">Please choose one of the options below to validate or modify your shipment:</p>
                    </div>

                    <!-- Footer & Timestamp -->
                    <div class="flex items-center justify-between text-[10px] text-slate-500 mt-2 pt-2 border-t border-[#2a3942]">
                      <span>TikTak PRO Bot</span>
                      <span>14:32 ✓✓</span>
                    </div>

                    <!-- WhatsApp Interactive Action Buttons (The 3 Core Buttons) -->
                    <div class="mt-3 space-y-1.5 pt-2 border-t border-[#2a3942]/80">
                      <button
                        (click)="onSimulatedButtonClick('btn_confirm')"
                        class="w-full py-2 px-3 rounded-lg bg-[#00a884] hover:bg-[#008f70] active:scale-[0.98] text-white text-xs font-bold text-center transition flex items-center justify-center gap-1.5 shadow">
                        <span>✓</span>
                        <span>Confirm Order (Ship Now)</span>
                      </button>

                      <button
                        (click)="onSimulatedButtonClick('btn_edit_address')"
                        class="w-full py-2 px-3 rounded-lg bg-[#111b21] hover:bg-[#182229] active:scale-[0.98] text-cyan-300 border border-[#2a3942] text-xs font-semibold text-center transition flex items-center justify-center gap-1.5">
                        <span>✎</span>
                        <span>Update Address / Delivery Note</span>
                      </button>

                      <button
                        (click)="onSimulatedButtonClick('btn_cancel')"
                        class="w-full py-2 px-3 rounded-lg bg-[#111b21] hover:bg-rose-950/40 active:scale-[0.98] text-rose-400 border border-rose-900/40 text-xs font-semibold text-center transition flex items-center justify-center gap-1.5">
                        <span>✕</span>
                        <span>Cancel Order</span>
                      </button>
                    </div>
                  </div>

                  <!-- Inline Address Update Prompt (if toggled) -->
                  @if (showAddressEditModal()) {
                    <div class="bg-[#111b21] border border-cyan-500/40 rounded-xl p-3 space-y-2 animate-fadeIn">
                      <div class="text-[11px] font-bold text-cyan-400">Update Shipping Note:</div>
                      <input
                        type="text"
                        [(ngModel)]="addressUpdateText"
                        placeholder="e.g. Near Ennakl, deliver after 15h"
                        class="w-full bg-[#202c33] border border-slate-700 rounded-lg p-2 text-xs text-white" />
                      <div class="flex gap-2">
                        <button
                          (click)="submitAddressUpdate()"
                          class="flex-1 py-1 bg-cyan-600 hover:bg-cyan-500 text-white rounded text-xs font-semibold">Save</button>
                        <button
                          (click)="showAddressEditModal.set(false)"
                          class="px-2 py-1 bg-slate-800 text-slate-400 rounded text-xs">Cancel</button>
                      </div>
                    </div>
                  }

                  <!-- Simulator Live Feedback Banner -->
                  @if (simulatorFeedback()) {
                    <div [class]="simulatorFeedbackClasses()">
                      {{ simulatorFeedback() }}
                    </div>
                  }
                </div>
              </div>
            </div>
          </div>

          <!-- Right: Timeout Fallback Tracker & Escalation Queue (6 cols) -->
          <div class="lg:col-span-6 bg-slate-900/80 border border-slate-800 rounded-2xl p-6 shadow-xl flex flex-col justify-between">
            <div>
              <div class="flex items-center justify-between mb-4">
                <div>
                  <h2 class="text-sm font-bold text-white uppercase tracking-wider flex items-center gap-2">
                    <span class="w-2 h-2 rounded-full bg-amber-400"></span>
                    30-Minute Timeout Fallback Tracker
                  </h2>
                  <p class="text-xs text-slate-400 mt-0.5">Orders unanswered on WhatsApp are auto-escalated to human call center dialers</p>
                </div>
                <span class="text-xs px-2.5 py-1 rounded-full bg-amber-500/10 text-amber-400 border border-amber-500/20 font-bold">
                  {{ escalatedCount() }} Escalated
                </span>
              </div>

              <!-- Orders Queue Table -->
              <div class="space-y-3">
                @for (ord of simulatedOrders(); track ord.id) {
                  <div class="p-3.5 rounded-xl border border-slate-800 bg-slate-950/60 hover:border-slate-700/80 transition flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                    <div>
                      <div class="flex items-center gap-2">
                        <span class="font-mono font-bold text-white text-xs">#{{ ord.orderNumber }}</span>
                        <span class="text-xs text-slate-400">({{ ord.customerName }})</span>
                        <!-- Status Badge -->
                        @switch (ord.status) {
                          @case ('Confirmed') {
                            <span class="text-[10px] px-2 py-0.5 rounded-full bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 font-semibold">
                              ✓ Confirmed via WA
                            </span>
                          }
                          @case ('Cancelled') {
                            <span class="text-[10px] px-2 py-0.5 rounded-full bg-rose-500/10 text-rose-400 border border-rose-500/20 font-semibold">
                              ✕ Cancelled (Stock Released)
                            </span>
                          }
                          @case ('AddressUpdated') {
                            <span class="text-[10px] px-2 py-0.5 rounded-full bg-cyan-500/10 text-cyan-400 border border-cyan-500/20 font-semibold">
                              ✎ Note Updated
                            </span>
                          }
                          @default {
                            <span class="text-[10px] px-2 py-0.5 rounded-full bg-amber-500/10 text-amber-400 border border-amber-500/20 font-semibold">
                              Pending Confirmation
                            </span>
                          }
                        }
                      </div>

                      <div class="text-[11px] text-slate-400 mt-1 flex items-center gap-3">
                        <span>Total: <strong class="text-slate-200">{{ ord.totalAmount }} {{ ord.currency }}</strong></span>
                        <span>•</span>
                        <span>Sent: <strong class="text-slate-300">{{ ord.sentMinutesAgo }}m ago</strong></span>
                        <span>•</span>
                        <span>WMS Lock: <strong [class.text-emerald-400]="ord.reservationStatus === 'Active'" [class.text-slate-500]="ord.reservationStatus === 'Released'">{{ ord.reservationStatus }}</strong></span>
                      </div>

                      @if (ord.notes) {
                        <div class="text-[10px] text-slate-500 mt-1 italic">{{ ord.notes }}</div>
                      }
                    </div>

                    <!-- Action / Dial Button -->
                    <div class="flex items-center gap-2 shrink-0">
                      @if (ord.requiresPhoneCall) {
                        <span class="px-2.5 py-1 rounded-lg bg-rose-500/10 text-rose-400 border border-rose-500/30 text-[11px] font-bold flex items-center gap-1">
                          <svg class="w-3 h-3 animate-pulse" fill="currentColor" viewBox="0 0 20 20"><path d="M2 3a1 1 0 011-1h2.153a1 1 0 01.986.836l.74 4.435a1 1 0 01-.54 1.06l-1.548.773a11.037 11.037 0 006.105 6.105l.774-1.548a1 1 0 011.059-.54l4.435.74a1 1 0 01.836.986V17a1 1 0 01-1 1h-2C7.82 18 2 12.18 2 4V3z"/></svg>
                          Call Center Dialer
                        </span>
                      } @else if (ord.status === 'Confirmed') {
                        <span class="text-xs text-emerald-400 font-medium">Bypassed Call Center</span>
                      } @else {
                        <span class="text-[11px] text-slate-500">
                          {{ 30 - ord.sentMinutesAgo }}m until auto-escalation
                        </span>
                      }
                    </div>
                  </div>
                }
              </div>
            </div>

            <!-- Escalation Info Box -->
            <div class="mt-6 p-4 rounded-xl bg-slate-950/80 border border-slate-800 text-xs space-y-2">
              <div class="font-bold text-slate-300 flex items-center justify-between">
                <span>TikTak PRO Anti-Collision Strategy:</span>
                <span class="text-teal-400 font-mono">1-Click WhatsApp Bypass</span>
              </div>
              <p class="text-slate-400 leading-relaxed">
                When customer clicks <strong>"[ Confirm Order ]"</strong> on WhatsApp, the order transitions immediately to <code class="text-emerald-400">Confirmed</code>, automatically unlocking agents and eliminating expensive manual phone calls.
              </p>
            </div>
          </div>
        </div>
      </main>
    </div>
  `,
  styles: [`
    @keyframes fadeIn {
      from { opacity: 0; transform: translateY(-4px); }
      to { opacity: 1; transform: translateY(0); }
    }
    .animate-fadeIn {
      animation: fadeIn 0.2s ease-out forwards;
    }
  `]
})
export class WhatsAppPairingCockpitComponent implements OnInit, OnDestroy {
  private http = inject(HttpClient);

  // Regional Country Presets
  countryPresets: CountryPreset[] = [
    { name: 'Tunisia', code: 'TN', dialCode: '+216', flag: '🇹🇳', placeholder: '98 123 456' },
    { name: 'Egypt', code: 'EG', dialCode: '+20', flag: '🇪🇬', placeholder: '10 1234 5678' },
    { name: 'Morocco', code: 'MA', dialCode: '+212', flag: '🇲🇦', placeholder: '6 1234 5678' },
    { name: 'Saudi Arabia', code: 'SA', dialCode: '+966', flag: '🇸🇦', placeholder: '50 123 4567' },
    { name: 'UAE', code: 'AE', dialCode: '+971', flag: '🇦🇪', placeholder: '50 123 4567' },
    { name: 'Algeria', code: 'DZ', dialCode: '+213', flag: '🇩🇿', placeholder: '55 123 4567' }
  ];

  selectedCountry = signal<string>('TN');
  phoneNumberInput = '+21698123456';
  instanceNameInput = 'default';

  // Live Pairing & Status State
  pairingCode = signal<string>('K9W2-M7PQ');
  connectionStatus = signal<PairingStatus>('PendingEntry');
  remainingSeconds = signal<number>(540); // 9 minutes initial
  isCopied = signal<boolean>(false);
  isLoadingPairingCode = signal<boolean>(false);
  isSimulating = signal<boolean>(false);
  isSweeping = signal<boolean>(false);

  // COD Simulator State
  simulatorFeedback = signal<string | null>(null);
  showAddressEditModal = signal<boolean>(false);
  addressUpdateText = '';

  simulatedOrders = signal<SimulatedOrder[]>([
    {
      id: 'f1a9b2c3-4d5e-6f7a-8b9c-0d1e2f3a4b5c',
      orderNumber: 'ORD-7892',
      customerName: 'Yassine Triki',
      customerPhone: '+21698123456',
      shippingAddress: 'Avenue Habib Bourguiba, Tunis',
      totalAmount: 165.0,
      currency: 'TND',
      status: 'PendingConfirmation',
      sentMinutesAgo: 8,
      requiresPhoneCall: false,
      reservationStatus: 'Active'
    },
    {
      id: 'a2b3c4d5-e6f7-8a9b-0c1d-2e3f4a5b6c7d',
      orderNumber: 'ORD-7890',
      customerName: 'Kareem Mansour',
      customerPhone: '+201012345678',
      shippingAddress: 'Nasr City, Cairo',
      totalAmount: 920.0,
      currency: 'EGP',
      status: 'PendingConfirmation',
      sentMinutesAgo: 34,
      requiresPhoneCall: true,
      notes: '[Auto-Fallback]: WhatsApp confirmation timed out after 30m. Escalated to outbound call center dialer.',
      reservationStatus: 'Active'
    },
    {
      id: 'b3c4d5e6-f7a8-9b0c-1d2e-3f4a5b6c7d8e',
      orderNumber: 'ORD-7888',
      customerName: 'Nadia Amrani',
      customerPhone: '+212612345678',
      shippingAddress: 'Maarif, Casablanca',
      totalAmount: 480.0,
      currency: 'MAD',
      status: 'Confirmed',
      confirmedVia: 'WhatsAppInteractive',
      sentMinutesAgo: 14,
      requiresPhoneCall: false,
      notes: '[WhatsApp Bot]: Confirmed via interactive button.',
      reservationStatus: 'Active'
    }
  ]);

  activeSimulatedOrder = computed(() => this.simulatedOrders()[0]);

  escalatedCount = computed(() => {
    return this.simulatedOrders().filter(o => o.requiresPhoneCall).length;
  });

  countdownPercent = computed(() => {
    const total = 600; // 10 minutes
    const rem = this.remainingSeconds();
    return Math.max(0, Math.min(100, (rem / total) * 100));
  });

  formattedRemainingTime = computed(() => {
    const s = this.remainingSeconds();
    if (s <= 0) return 'Expired';
    const m = Math.floor(s / 60);
    const sec = s % 60;
    return `${m}:${sec.toString().padStart(2, '0')}`;
  });

  statusBadgeClasses = computed(() => {
    switch (this.connectionStatus()) {
      case 'Connected':
        return 'inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-bold bg-emerald-500/15 text-emerald-400 border border-emerald-500/30';
      case 'PendingEntry':
        return 'inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-bold bg-amber-500/15 text-amber-400 border border-amber-500/30 animate-pulse';
      case 'Syncing':
        return 'inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-bold bg-cyan-500/15 text-cyan-400 border border-cyan-500/30';
      default:
        return 'inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-bold bg-slate-800 text-slate-400 border border-slate-700';
    }
  });

  statusDotClasses = computed(() => {
    switch (this.connectionStatus()) {
      case 'Connected':
        return 'bg-emerald-400';
      case 'PendingEntry':
        return 'bg-amber-400 animate-ping';
      case 'Syncing':
        return 'bg-cyan-400';
      default:
        return 'bg-slate-500';
    }
  });

  simulatorFeedbackClasses = computed(() => {
    const fb = this.simulatorFeedback();
    if (!fb) return '';
    if (fb.includes('Confirmed')) {
      return 'p-2.5 rounded-xl bg-emerald-500/20 border border-emerald-500/40 text-emerald-300 text-xs font-semibold';
    }
    if (fb.includes('Cancelled')) {
      return 'p-2.5 rounded-xl bg-rose-500/20 border border-rose-500/40 text-rose-300 text-xs font-semibold';
    }
    return 'p-2.5 rounded-xl bg-cyan-500/20 border border-cyan-500/40 text-cyan-300 text-xs font-semibold';
  });

  private timerInterval: any = null;

  ngOnInit() {
    this.startCountdown();
  }

  ngOnDestroy() {
    if (this.timerInterval) {
      clearInterval(this.timerInterval);
    }
  }

  private startCountdown() {
    if (this.timerInterval) clearInterval(this.timerInterval);
    this.timerInterval = setInterval(() => {
      this.remainingSeconds.update(s => (s > 0 ? s - 1 : 0));
    }, 1000);
  }

  selectPreset(preset: CountryPreset) {
    this.selectedCountry.set(preset.code);
    this.phoneNumberInput = `${preset.dialCode}`;
  }

  requestPairingCode() {
    this.isLoadingPairingCode.set(true);

    const payload = {
      phoneNumber: this.phoneNumberInput.trim(),
      instanceName: this.instanceNameInput.trim() || 'default'
    };

    this.http.post<any>('/api/v1/channels/whatsapp/pairing/request', payload).subscribe({
      next: (res) => {
        this.pairingCode.set(res.pairingCode || res.formattedCode);
        this.connectionStatus.set('PendingEntry');
        this.remainingSeconds.set(res.expiresInSeconds || 600);
        this.isLoadingPairingCode.set(false);
        this.startCountdown();
      },
      error: () => {
        // Fallback simulation for offline/preview
        const chars = '23456789ABCDEFGHJKLMNPQRSTUVWXYZ';
        let code = '';
        for (let i = 0; i < 8; i++) {
          code += chars.charAt(Math.floor(Math.random() * chars.length));
        }
        const formatted = `${code.slice(0, 4)}-${code.slice(4)}`;
        this.pairingCode.set(formatted);
        this.connectionStatus.set('PendingEntry');
        this.remainingSeconds.set(600);
        this.isLoadingPairingCode.set(false);
        this.startCountdown();
      }
    });
  }

  copyPairingCode() {
    const code = this.pairingCode();
    if (!code) return;
    navigator.clipboard?.writeText(code).then(() => {
      this.isCopied.set(true);
      setTimeout(() => this.isCopied.set(false), 2500);
    });
  }

  simulatePhoneConfirmation() {
    this.isSimulating.set(true);
    const code = this.pairingCode();

    const payload = { pairingCode: code };

    this.http.post<any>('/api/v1/channels/whatsapp/pairing/verify', payload).subscribe({
      next: () => {
        this.connectionStatus.set('Connected');
        this.isSimulating.set(false);
      },
      error: () => {
        // Fallback simulated success
        setTimeout(() => {
          this.connectionStatus.set('Connected');
          this.isSimulating.set(false);
        }, 1200);
      }
    });
  }

  disconnectInstance() {
    this.connectionStatus.set('Disconnected');
    this.pairingCode.set('');
  }

  onSimulatedButtonClick(buttonId: string) {
    const currentOrder = this.activeSimulatedOrder();

    if (buttonId === 'btn_confirm') {
      this.http.post('/api/v1/channels/whatsapp/interactive/webhook', {
        orderId: currentOrder.id,
        buttonId: 'btn_confirm'
      }).subscribe({ next: () => {}, error: () => {} });

      this.updateOrderStatus(currentOrder.id, 'Confirmed', 'WhatsAppInteractive');
      this.simulatorFeedback.set('✓ Order confirmed! Removed from manual call center queue.');
    } else if (buttonId === 'btn_edit_address') {
      this.showAddressEditModal.set(true);
    } else if (buttonId === 'btn_cancel') {
      this.http.post('/api/v1/channels/whatsapp/interactive/webhook', {
        orderId: currentOrder.id,
        buttonId: 'btn_cancel'
      }).subscribe({ next: () => {}, error: () => {} });

      this.updateOrderStatus(currentOrder.id, 'Cancelled');
      this.simulatorFeedback.set('✕ Order cancelled by customer. Inventory reservation released.');
    }
  }

  submitAddressUpdate() {
    const currentOrder = this.activeSimulatedOrder();
    const note = this.addressUpdateText.trim();
    if (!note) return;

    this.http.post('/api/v1/channels/whatsapp/interactive/webhook', {
      orderId: currentOrder.id,
      buttonId: 'btn_edit_address',
      updatedText: note
    }).subscribe({ next: () => {}, error: () => {} });

    this.simulatedOrders.update(orders => {
      return orders.map(o => {
        if (o.id === currentOrder.id) {
          return {
            ...o,
            shippingAddress: `${o.shippingAddress} (Note: ${note})`,
            notes: `[Address Correction via WhatsApp]: ${note}`,
            status: 'AddressUpdated'
          };
        }
        return o;
      });
    });

    this.showAddressEditModal.set(false);
    this.addressUpdateText = '';
    this.simulatorFeedback.set(`✎ Delivery note saved: "${note}"`);
  }

  private updateOrderStatus(orderId: string, status: any, confirmedVia?: string) {
    this.simulatedOrders.update(orders => {
      return orders.map(o => {
        if (o.id === orderId) {
          return {
            ...o,
            status,
            confirmedVia,
            requiresPhoneCall: false,
            reservationStatus: status === 'Cancelled' ? 'Released' : o.reservationStatus
          };
        }
        return o;
      });
    });
  }

  triggerTimeoutSweep() {
    this.isSweeping.set(true);

    this.http.post('/api/v1/channels/whatsapp/interactive/timeout-sweep', { timeoutMinutes: 30 }).subscribe({
      next: () => {
        this.runLocalSweep();
        this.isSweeping.set(false);
      },
      error: () => {
        this.runLocalSweep();
        this.isSweeping.set(false);
      }
    });
  }

  private runLocalSweep() {
    this.simulatedOrders.update(orders => {
      return orders.map(o => {
        if (o.status === 'PendingConfirmation' && o.sentMinutesAgo >= 30) {
          return {
            ...o,
            requiresPhoneCall: true,
            notes: '[Auto-Fallback]: WhatsApp confirmation timed out after 30m. Escalated to outbound call center dialer.'
          };
        }
        return o;
      });
    });
  }
}
