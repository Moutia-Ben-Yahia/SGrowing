import { Component, signal, computed, inject, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';

export type CompanionSessionState =
  | 'Initial'
  | 'PairingCodeRequested'
  | 'PairingCodeGenerated'
  | 'HandshakeNoiseExchange'
  | 'Connected'
  | 'Syncing'
  | 'Disconnected';

export type MessageAckLevel = 'None' | 'AckServer' | 'AckDevice' | 'AckRead' | 'AckPlayed';
export type CompanionNetworkType = 'WiFi' | 'Cellular5G' | 'Cellular4G';

export interface CompanionDeviceTelemetry {
  batteryPercentage: number;
  isCharging: boolean;
  networkType: CompanionNetworkType;
  pingLatencyMs: number;
  lastHeartbeatUtc: string;
}

export interface CompanionSocketPacketLog {
  id: string;
  timestampUtc: string;
  direction: 'INBOUND' | 'OUTBOUND';
  frameType: string;
  summary: string;
  hexPayloadSnippet: string;
  payloadSizeBytes: number;
}

export interface CompanionSession {
  sessionId: string;
  tenantId: string;
  phoneNumber: string;
  deviceModel: string;
  state: CompanionSessionState;
  pairingCode?: string | null;
  pairingCodeExpiresAtUtc?: string | null;
  secondsRemaining: number;
  failedVerificationAttempts: number;
  isLockedOut: boolean;
  sessionValidationToken?: string | null;
  telemetry: CompanionDeviceTelemetry;
  createdAtUtc: string;
  lastActiveAtUtc: string;
  reconnectAttempts: number;
  disconnectReason?: string | null;
}

export interface MediaDecryptionResult {
  success: boolean;
  isChecksumValid: boolean;
  decryptedBase64?: string | null;
  computedSha256Checksum: string;
  expectedSha256Checksum: string;
  mimeType: string;
  decryptedSizeBytes: number;
  error?: string | null;
}

@Component({
  selector: 'app-whatsapp-companion-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="min-h-screen bg-slate-950 text-slate-100 flex flex-col font-sans" dir="ltr">
      <!-- Top Navigation Header -->
      <header class="border-b border-slate-800 bg-slate-900/80 backdrop-blur px-6 py-4 flex items-center justify-between sticky top-0 z-50">
        <div class="flex items-center gap-4">
          <a routerLink="/" class="text-xl font-black bg-gradient-to-r from-emerald-400 via-teal-300 to-cyan-400 bg-clip-text text-transparent hover:opacity-90">
            SGrowing
          </a>
          <span class="text-slate-600">/</span>
          <div class="flex items-center gap-2">
            <span class="text-sm font-semibold text-slate-200">WhatsApp Companion Studio</span>
            <span class="text-xs px-2.5 py-0.5 rounded-full bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 font-medium">
              Noise_XX Socket & Pairing Bridge
            </span>
          </div>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="loadSessions()"
            class="px-3 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-xs text-slate-300 font-medium transition flex items-center gap-2 border border-slate-700"
          >
            <svg class="w-3.5 h-3.5 text-slate-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
            Refresh Fleet
          </button>
          <div class="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-slate-800/80 border border-slate-700/60 text-xs">
            <span class="text-slate-400">Gateway Engine:</span>
            <span class="text-emerald-400 font-mono font-semibold">Baileys Noise_XX + WABinary Codec</span>
          </div>
        </div>
      </header>

      <main class="flex-1 p-6 max-w-7xl w-full mx-auto space-y-6">
        <!-- Banner & Live Overview Strip -->
        <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 bg-slate-900/60 border border-slate-800 rounded-2xl p-6 shadow-xl backdrop-blur">
          <div>
            <h1 class="text-2xl font-black text-white flex items-center gap-3">
              <span>High-Throughput WhatsApp Companion Socket</span>
              @if (activeSession()) {
                <span [class]="getStatusBadgeClass(activeSession()!.state)">
                  <span class="w-2 h-2 rounded-full" [class]="getStatusDotClass(activeSession()!.state)"></span>
                  {{ activeSession()!.state }}
                </span>
              }
            </h1>
            <p class="text-xs text-slate-400 mt-1 max-w-2xl">
              Zero-browser headless Multi-Device Noise_XX handshake protocol. Fast 8-character pairing codes with 180s TTL, AES-256-GCM binary payload offloading, and WABinary end-to-end receipt ACK tracking.
            </p>
          </div>

          <div class="flex items-center gap-3">
            <div class="flex items-center gap-3 bg-slate-950/70 border border-slate-800 rounded-xl px-4 py-2.5">
              <div class="text-center">
                <div class="text-xs text-slate-400">Fleet Devices</div>
                <div class="text-lg font-bold text-emerald-400 font-mono">{{ sessions().length }}</div>
              </div>
              <div class="h-8 w-px bg-slate-800"></div>
              <div class="text-center">
                <div class="text-xs text-slate-400">Active Lines</div>
                <div class="text-lg font-bold text-cyan-400 font-mono">{{ connectedCount() }}</div>
              </div>
              <div class="h-8 w-px bg-slate-800"></div>
              <div class="text-center">
                <div class="text-xs text-slate-400">Security</div>
                <div class="text-xs font-semibold text-amber-400 font-mono mt-1">Noise_XX</div>
              </div>
            </div>
          </div>
        </div>

        <!-- Upper Grid: Pairing Code Station + Active Session Telemetry Card -->
        <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
          <!-- 1. Interactive Pairing Code Station (7 Cols) -->
          <div class="lg:col-span-7 bg-slate-900/70 border border-slate-800 rounded-2xl p-6 shadow-lg flex flex-col justify-between">
            <div>
              <div class="flex items-center justify-between mb-4">
                <div class="flex items-center gap-2">
                  <div class="w-8 h-8 rounded-lg bg-emerald-500/10 border border-emerald-500/30 flex items-center justify-center text-emerald-400 font-bold text-sm">
                    1
                  </div>
                  <div>
                    <h2 class="text-base font-bold text-white">8-Digit Companion Pairing Station</h2>
                    <p class="text-xs text-slate-400">Generate human-readable 8-char OTP pairing code with 180s countdown</p>
                  </div>
                </div>
                <span class="text-xs font-mono px-2 py-0.5 rounded bg-slate-800 text-slate-300 border border-slate-700">
                  RFC Multi-Device
                </span>
              </div>

              <!-- Phone & Device Input -->
              <div class="grid grid-cols-1 sm:grid-cols-2 gap-3 mb-4">
                <div>
                  <label class="block text-xs font-medium text-slate-300 mb-1">Phone Number (E.164)</label>
                  <div class="relative">
                    <input
                      type="text"
                      [(ngModel)]="phoneNumber"
                      placeholder="+216 98 123 456"
                      class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3.5 py-2.5 text-sm text-white placeholder-slate-500 focus:outline-none focus:border-emerald-500 font-mono"
                    />
                  </div>
                </div>
                <div>
                  <label class="block text-xs font-medium text-slate-300 mb-1">Companion Device Model</label>
                  <input
                    type="text"
                    [(ngModel)]="deviceModel"
                    placeholder="Primary Support Desk (Chrome/Linux)"
                    class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3.5 py-2.5 text-sm text-white placeholder-slate-500 focus:outline-none focus:border-emerald-500"
                  />
                </div>
              </div>

              <!-- Quick Presets -->
              <div class="flex flex-wrap items-center gap-2 mb-5">
                <span class="text-xs text-slate-500">Presets:</span>
                <button
                  type="button"
                  (click)="setPresetPhone('+21671998801', 'Primary Support Desk Line')"
                  class="text-xs px-2.5 py-1 rounded-lg bg-slate-800/80 hover:bg-slate-700 text-slate-300 border border-slate-700 transition"
                >
                  Support Desk (+216 71 998 801)
                </button>
                <button
                  type="button"
                  (click)="setPresetPhone('+21671998802', 'VIP Account Manager Companion')"
                  class="text-xs px-2.5 py-1 rounded-lg bg-slate-800/80 hover:bg-slate-700 text-slate-300 border border-slate-700 transition"
                >
                  VIP Account (+216 71 998 802)
                </button>
                <button
                  type="button"
                  (click)="setPresetPhone('+21671998803', 'Automated Inbound Dispatcher')"
                  class="text-xs px-2.5 py-1 rounded-lg bg-slate-800/80 hover:bg-slate-700 text-slate-300 border border-slate-700 transition"
                >
                  Inbound Bot (+216 71 998 803)
                </button>
              </div>

              <!-- Generate Button -->
              <div class="flex items-center gap-3 mb-6">
                <button
                  type="button"
                  (click)="requestPairingCode()"
                  [disabled]="isGenerating()"
                  class="px-5 py-2.5 rounded-xl bg-gradient-to-r from-emerald-500 to-teal-500 hover:from-emerald-400 hover:to-teal-400 text-slate-950 font-bold text-sm shadow-lg shadow-emerald-500/20 transition disabled:opacity-50 flex items-center gap-2"
                >
                  @if (isGenerating()) {
                    <svg class="animate-spin w-4 h-4" fill="none" viewBox="0 0 24 24">
                      <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                      <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z"></path>
                    </svg>
                    Generating Code...
                  } @else {
                    <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
                    </svg>
                    Generate Pairing Code
                  }
                </button>
              </div>

              <!-- Large 8-Character Pairing Display with Circular Countdown -->
              @if (currentPairingCode()) {
                <div class="bg-slate-950/90 border border-emerald-500/30 rounded-2xl p-5 mb-5 relative overflow-hidden">
                  <div class="flex flex-col sm:flex-row items-center justify-between gap-4">
                    <div>
                      <div class="text-xs text-emerald-400 uppercase tracking-widest font-mono font-semibold mb-1">
                        Companion Handshake Code
                      </div>
                      <div class="text-3xl sm:text-4xl font-black font-mono text-white tracking-widest flex items-center gap-2">
                        <span>{{ currentPairingCode() }}</span>
                        <button
                          type="button"
                          (click)="copyPairingCode()"
                          class="p-1.5 rounded-lg hover:bg-slate-800 text-slate-400 hover:text-white transition"
                          title="Copy Code"
                        >
                          @if (copied()) {
                            <span class="text-xs text-emerald-400 font-sans font-medium px-2 py-0.5 rounded bg-emerald-500/20">Copied!</span>
                          } @else {
                            <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z" />
                            </svg>
                          }
                        </button>
                      </div>
                      <p class="text-xs text-slate-400 mt-2">
                        On your WhatsApp phone: <span class="text-slate-200 font-semibold">Settings &gt; Linked Devices &gt; Link with phone number instead</span> and enter this code.
                      </p>
                    </div>

                    <!-- Circular 180s Countdown Progress Indicator -->
                    <div class="flex flex-col items-center justify-center">
                      <div class="relative w-20 h-20 flex items-center justify-center">
                        <svg class="w-full h-full transform -rotate-90" viewBox="0 0 36 36">
                          <path
                            class="text-slate-800"
                            stroke-width="3"
                            stroke="currentColor"
                            fill="none"
                            d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831"
                          />
                          <path
                            [class]="countdownProgressColor()"
                            stroke-dasharray="100, 100"
                            [attr.stroke-dashoffset]="countdownOffset()"
                            stroke-linecap="round"
                            stroke-width="3.5"
                            stroke="currentColor"
                            fill="none"
                            d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831"
                          />
                        </svg>
                        <div class="absolute text-center">
                          <span class="text-base font-black font-mono text-white">{{ secondsLeft() }}</span>
                          <span class="block text-[9px] uppercase tracking-wider text-slate-400">SEC</span>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>

                <!-- Simulation Verification Box -->
                <div class="bg-slate-900 border border-slate-800 rounded-xl p-4 flex flex-col sm:flex-row items-center gap-3">
                  <div class="flex-1 w-full">
                    <label class="block text-xs font-medium text-slate-300 mb-1">Simulate Handshake Entry (8-Digit Code)</label>
                    <input
                      type="text"
                      [(ngModel)]="verifyInputCode"
                      placeholder="e.g. ABCD-EFGH"
                      class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-white placeholder-slate-500 font-mono uppercase"
                    />
                  </div>
                  <div class="flex items-center gap-2 w-full sm:w-auto pt-4 sm:pt-0">
                    <button
                      type="button"
                      (click)="autoFillCode()"
                      class="px-3 py-2 rounded-lg bg-slate-800 hover:bg-slate-700 text-xs text-slate-300 border border-slate-700 whitespace-nowrap"
                    >
                      Fill Code
                    </button>
                    <button
                      type="button"
                      (click)="verifyPairingCode()"
                      [disabled]="isVerifying()"
                      class="px-4 py-2 rounded-lg bg-cyan-500 hover:bg-cyan-400 text-slate-950 font-bold text-xs whitespace-nowrap transition disabled:opacity-50"
                    >
                      @if (isVerifying()) {
                        Verifying...
                      } @else {
                        Verify &amp; Connect
                      }
                    </button>
                  </div>
                </div>
              }
            </div>

            @if (operationMessage()) {
              <div
                class="mt-4 p-3 rounded-xl text-xs border"
                [class]="operationSuccess() ? 'bg-emerald-500/10 border-emerald-500/30 text-emerald-300' : 'bg-rose-500/10 border-rose-500/30 text-rose-300'"
              >
                {{ operationMessage() }}
              </div>
            }
          </div>

          <!-- 2. Live Companion Session Status Card (5 Cols) -->
          <div class="lg:col-span-5 bg-slate-900/70 border border-slate-800 rounded-2xl p-6 shadow-lg flex flex-col justify-between">
            <div>
              <div class="flex items-center justify-between mb-4">
                <div class="flex items-center gap-2">
                  <div class="w-8 h-8 rounded-lg bg-cyan-500/10 border border-cyan-500/30 flex items-center justify-center text-cyan-400 font-bold text-sm">
                    2
                  </div>
                  <div>
                    <h2 class="text-base font-bold text-white">Live Companion Status &amp; Telemetry</h2>
                    <p class="text-xs text-slate-400">Physical handset socket telemetry &amp; Noise state</p>
                  </div>
                </div>
                <button
                  (click)="refreshCurrentTelemetry()"
                  class="p-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-400 hover:text-white transition"
                  title="Ping Telemetry"
                >
                  <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z" />
                  </svg>
                </button>
              </div>

              @if (activeSession()) {
                <div class="space-y-4">
                  <!-- Device Badge & State -->
                  <div class="bg-slate-950/80 border border-slate-800 rounded-xl p-4">
                    <div class="flex items-center justify-between mb-2">
                      <span class="text-xs text-slate-400">Device Model</span>
                      <span [class]="getStatusBadgeClass(activeSession()!.state)">
                        {{ activeSession()!.state }}
                      </span>
                    </div>
                    <div class="text-sm font-bold text-white truncate">{{ activeSession()!.deviceModel }}</div>
                    <div class="text-xs text-emerald-400 font-mono mt-0.5">{{ activeSession()!.phoneNumber }}</div>
                  </div>

                  <!-- Telemetry Grid -->
                  <div class="grid grid-cols-2 gap-3">
                    <!-- Battery -->
                    <div class="bg-slate-950/80 border border-slate-800 rounded-xl p-3">
                      <div class="text-xs text-slate-400 mb-1 flex items-center justify-between">
                        <span>Handset Battery</span>
                        @if (activeSession()!.telemetry.isCharging) {
                          <span class="text-amber-400 text-[10px] font-semibold flex items-center">⚡ Charging</span>
                        }
                      </div>
                      <div class="flex items-center gap-2">
                        <div class="w-7 h-4 border border-slate-600 rounded-sm p-0.5 flex items-center">
                          <div
                            class="h-full rounded-2xs"
                            [class]="getBatteryColor(activeSession()!.telemetry.batteryPercentage)"
                            [style.width.%]="activeSession()!.telemetry.batteryPercentage"
                          ></div>
                        </div>
                        <span class="text-base font-black font-mono text-white">{{ activeSession()!.telemetry.batteryPercentage }}%</span>
                      </div>
                    </div>

                    <!-- Network & Latency -->
                    <div class="bg-slate-950/80 border border-slate-800 rounded-xl p-3">
                      <div class="text-xs text-slate-400 mb-1">Network &amp; Latency</div>
                      <div class="flex items-center justify-between">
                        <span class="text-xs font-semibold px-2 py-0.5 rounded bg-slate-800 text-slate-200 border border-slate-700">
                          {{ activeSession()!.telemetry.networkType }}
                        </span>
                        <div class="flex items-center gap-1.5">
                          <span class="w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
                          <span class="text-sm font-mono font-bold text-emerald-400">{{ activeSession()!.telemetry.pingLatencyMs }}ms</span>
                        </div>
                      </div>
                    </div>
                  </div>

                  <!-- Cryptographic Security Strip -->
                  <div class="bg-slate-950/80 border border-slate-800 rounded-xl p-3">
                    <div class="text-xs text-slate-400 mb-1 flex items-center justify-between">
                      <span>Noise_XX Validation Token</span>
                      <span class="text-[10px] text-cyan-400 font-mono">HMAC-SHA256</span>
                    </div>
                    @if (activeSession()!.sessionValidationToken) {
                      <div class="text-xs font-mono text-slate-300 truncate bg-slate-900 px-2.5 py-1.5 rounded border border-slate-800 select-all">
                        {{ activeSession()!.sessionValidationToken }}
                      </div>
                    } @else {
                      <div class="text-xs text-slate-500 italic">Handshake pending token signing...</div>
                    }
                  </div>
                </div>
              } @else {
                <div class="py-12 text-center text-slate-500 text-xs">
                  No active companion session selected. Generate a pairing code or select a device from the fleet table below.
                </div>
              }
            </div>

            @if (activeSession()) {
              <div class="pt-4 border-t border-slate-800/80 flex items-center justify-between gap-3">
                <button
                  type="button"
                  (click)="disconnectActiveSession()"
                  class="px-3.5 py-1.5 rounded-lg bg-rose-500/10 hover:bg-rose-500/20 text-rose-400 border border-rose-500/30 text-xs font-semibold transition flex items-center gap-1.5"
                >
                  <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                  </svg>
                  Terminate Socket
                </button>
                <span class="text-[11px] text-slate-500 font-mono">
                  Active: {{ activeSession()!.sessionId.substring(0, 8) }}...
                </span>
              </div>
            }
          </div>
        </div>

        <!-- Middle Grid: Socket Packet Inspector + Encrypted Media Decryptor Workbench -->
        <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
          <!-- 3. Socket Packet Inspector (Terminal Style) (6 Cols) -->
          <div class="lg:col-span-6 bg-slate-900/70 border border-slate-800 rounded-2xl p-6 shadow-lg flex flex-col h-[480px]">
            <div class="flex items-center justify-between mb-4 pb-2 border-b border-slate-800">
              <div class="flex items-center gap-2">
                <div class="w-8 h-8 rounded-lg bg-amber-500/10 border border-amber-500/30 flex items-center justify-center text-amber-400 font-bold text-sm">
                  3
                </div>
                <div>
                  <h2 class="text-base font-bold text-white">Socket Packet Inspector</h2>
                  <p class="text-xs text-slate-400">Live stream of Noise handshakes, WABinary frames &amp; ACKs</p>
                </div>
              </div>
              <div class="flex items-center gap-2">
                <span class="w-2 h-2 rounded-full bg-emerald-400 animate-ping"></span>
                <span class="text-xs text-emerald-400 font-mono">CONNECTED</span>
              </div>
            </div>

            <!-- Terminal Window -->
            <div class="flex-1 bg-slate-950 rounded-xl p-3.5 border border-slate-800 font-mono text-xs overflow-y-auto space-y-2">
              @if (packetLogs().length === 0) {
                <div class="text-slate-600 italic py-6 text-center">
                  Waiting for incoming socket frames or handshakes...
                </div>
              }
              @for (log of packetLogs(); track log.id) {
                <div class="p-2 rounded bg-slate-900/60 border border-slate-800/80 hover:border-slate-700 transition">
                  <div class="flex items-center justify-between mb-1">
                    <div class="flex items-center gap-1.5">
                      <span
                        class="text-[10px] px-1.5 py-0.2 rounded font-semibold uppercase"
                        [class]="log.direction === 'INBOUND' ? 'bg-cyan-500/20 text-cyan-400 border border-cyan-500/30' : 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30'"
                      >
                        {{ log.direction }}
                      </span>
                      <span class="text-[10px] text-amber-400 font-semibold font-mono">{{ log.frameType }}</span>
                    </div>
                    <span class="text-[10px] text-slate-500">{{ log.timestampUtc | date:'HH:mm:ss.SSS' }}</span>
                  </div>
                  <div class="text-slate-300 text-xs">{{ log.summary }}</div>
                  <div class="text-[10px] text-slate-500 font-mono mt-0.5 flex items-center gap-2">
                    <span class="text-slate-600">HEX:</span>
                    <span class="text-slate-400 truncate">{{ log.hexPayloadSnippet }}</span>
                    <span class="text-slate-600">({{ log.payloadSizeBytes }} B)</span>
                  </div>
                </div>
              }
            </div>
          </div>

          <!-- 4. Encrypted Media Decryptor Workbench (6 Cols) -->
          <div class="lg:col-span-6 bg-slate-900/70 border border-slate-800 rounded-2xl p-6 shadow-lg flex flex-col justify-between">
            <div>
              <div class="flex items-center justify-between mb-4 pb-2 border-b border-slate-800">
                <div class="flex items-center gap-2">
                  <div class="w-8 h-8 rounded-lg bg-indigo-500/10 border border-indigo-500/30 flex items-center justify-center text-indigo-400 font-bold text-sm">
                    4
                  </div>
                  <div>
                    <h2 class="text-base font-bold text-white">Encrypted Media Decryptor Workbench</h2>
                    <p class="text-xs text-slate-400">Stream-decrypt AES-256-GCM payloads &amp; verify SHA-256 hash</p>
                  </div>
                </div>
                <button
                  type="button"
                  (click)="loadTestVector()"
                  class="px-2.5 py-1 rounded bg-slate-800 hover:bg-slate-700 text-xs text-slate-300 border border-slate-700 font-medium"
                >
                  Load Test Vector
                </button>
              </div>

              <!-- Media Type Selector -->
              <div class="grid grid-cols-4 gap-2 mb-3">
                <button
                  type="button"
                  (click)="selectedMediaType.set('audio/ogg; codecs=opus')"
                  [class]="selectedMediaType() === 'audio/ogg; codecs=opus' ? 'bg-indigo-600 text-white' : 'bg-slate-800 text-slate-400 hover:bg-slate-700'"
                  class="py-1.5 px-2 rounded-lg text-xs font-semibold text-center transition"
                >
                  Voice Note
                </button>
                <button
                  type="button"
                  (click)="selectedMediaType.set('image/jpeg')"
                  [class]="selectedMediaType() === 'image/jpeg' ? 'bg-indigo-600 text-white' : 'bg-slate-800 text-slate-400 hover:bg-slate-700'"
                  class="py-1.5 px-2 rounded-lg text-xs font-semibold text-center transition"
                >
                  Image
                </button>
                <button
                  type="button"
                  (click)="selectedMediaType.set('video/mp4')"
                  [class]="selectedMediaType() === 'video/mp4' ? 'bg-indigo-600 text-white' : 'bg-slate-800 text-slate-400 hover:bg-slate-700'"
                  class="py-1.5 px-2 rounded-lg text-xs font-semibold text-center transition"
                >
                  Video
                </button>
                <button
                  type="button"
                  (click)="selectedMediaType.set('application/pdf')"
                  [class]="selectedMediaType() === 'application/pdf' ? 'bg-indigo-600 text-white' : 'bg-slate-800 text-slate-400 hover:bg-slate-700'"
                  class="py-1.5 px-2 rounded-lg text-xs font-semibold text-center transition"
                >
                  Document
                </button>
              </div>

              <!-- Vector Inputs -->
              <div class="space-y-2 mb-4">
                <div>
                  <label class="block text-[11px] font-mono text-slate-400">Ciphertext (Base64)</label>
                  <input
                    type="text"
                    [(ngModel)]="mediaCiphertext"
                    placeholder="Base64 AES-GCM Ciphertext"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-2.5 py-1.5 text-xs text-white font-mono"
                  />
                </div>
                <div class="grid grid-cols-2 gap-2">
                  <div>
                    <label class="block text-[11px] font-mono text-slate-400">IV / Nonce (Base64)</label>
                    <input
                      type="text"
                      [(ngModel)]="mediaIv"
                      placeholder="12-byte Nonce Base64"
                      class="w-full bg-slate-950 border border-slate-800 rounded-lg px-2.5 py-1.5 text-xs text-white font-mono"
                    />
                  </div>
                  <div>
                    <label class="block text-[11px] font-mono text-slate-400">Auth Tag (Base64)</label>
                    <input
                      type="text"
                      [(ngModel)]="mediaTag"
                      placeholder="16-byte GCM Tag Base64"
                      class="w-full bg-slate-950 border border-slate-800 rounded-lg px-2.5 py-1.5 text-xs text-white font-mono"
                    />
                  </div>
                </div>
                <div>
                  <label class="block text-[11px] font-mono text-slate-400">Expected SHA-256 Checksum (Hex)</label>
                  <input
                    type="text"
                    [(ngModel)]="mediaChecksum"
                    placeholder="64-character SHA-256 checksum"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-2.5 py-1.5 text-xs text-white font-mono"
                  />
                </div>
              </div>

              <!-- Decrypt Action Button -->
              <button
                type="button"
                (click)="decryptMedia()"
                [disabled]="isDecrypting()"
                class="w-full py-2.5 rounded-xl bg-gradient-to-r from-indigo-500 to-purple-600 hover:from-indigo-400 hover:to-purple-500 text-white font-bold text-xs shadow-lg shadow-indigo-500/20 transition disabled:opacity-50"
              >
                @if (isDecrypting()) {
                  Decrypting &amp; Validating Checksum...
                } @else {
                  Decrypt &amp; Verify SHA-256 Checksum
                }
              </button>

              <!-- Decryption Result Preview -->
              @if (decryptionResult()) {
                <div
                  class="mt-4 p-3.5 rounded-xl border"
                  [class]="decryptionResult()!.isChecksumValid ? 'bg-emerald-500/10 border-emerald-500/30' : 'bg-rose-500/10 border-rose-500/30'"
                >
                  <div class="flex items-center justify-between mb-1.5">
                    <span class="text-xs font-bold text-white">Decryption Result</span>
                    <span
                      class="text-[10px] font-mono font-bold px-2 py-0.5 rounded"
                      [class]="decryptionResult()!.isChecksumValid ? 'bg-emerald-500/20 text-emerald-400' : 'bg-rose-500/20 text-rose-400'"
                    >
                      {{ decryptionResult()!.isChecksumValid ? 'SHA-256 VERIFIED' : 'CHECKSUM MISMATCH' }}
                    </span>
                  </div>
                  <div class="text-[11px] text-slate-300 font-mono space-y-0.5">
                    <div>Size: <span class="text-white font-bold">{{ decryptionResult()!.decryptedSizeBytes }} bytes</span></div>
                    <div class="truncate">Computed: <span class="text-emerald-400">{{ decryptionResult()!.computedSha256Checksum }}</span></div>
                    <div class="truncate">Expected: <span class="text-slate-400">{{ decryptionResult()!.expectedSha256Checksum }}</span></div>
                  </div>
                </div>
              }
            </div>
          </div>
        </div>

        <!-- 5. Session Fleet Management Table -->
        <div class="bg-slate-900/70 border border-slate-800 rounded-2xl p-6 shadow-xl">
          <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 mb-5">
            <div>
              <h2 class="text-base font-bold text-white flex items-center gap-2">
                <span>Companion Device Fleet Table</span>
                <span class="text-xs px-2.5 py-0.5 rounded-full bg-cyan-500/10 text-cyan-400 border border-cyan-500/20 font-mono font-normal">
                  {{ sessions().length }} Devices Registered
                </span>
              </h2>
              <p class="text-xs text-slate-400">Multi-Device socket instances, real-time connectivity status &amp; ACK progression</p>
            </div>

            <!-- ACK Simulation Trigger for Active Session -->
            <div class="flex items-center gap-2">
              <span class="text-xs text-slate-400">Simulate ACK:</span>
              <button
                type="button"
                (click)="simulateAck('AckServer')"
                class="px-2.5 py-1 rounded bg-slate-800 hover:bg-slate-700 text-xs text-slate-300 border border-slate-700"
              >
                ✓ Server
              </button>
              <button
                type="button"
                (click)="simulateAck('AckDevice')"
                class="px-2.5 py-1 rounded bg-slate-800 hover:bg-slate-700 text-xs text-slate-300 border border-slate-700"
              >
                ✓✓ Device
              </button>
              <button
                type="button"
                (click)="simulateAck('AckRead')"
                class="px-2.5 py-1 rounded bg-cyan-500/20 text-cyan-400 border border-cyan-500/30 text-xs font-semibold"
              >
                ✓✓ Read
              </button>
            </div>
          </div>

          <!-- Table -->
          <div class="overflow-x-auto">
            <table class="w-full text-left text-xs">
              <thead class="bg-slate-950/60 text-slate-400 uppercase font-mono text-[10px] border-b border-slate-800">
                <tr>
                  <th class="py-3 px-4">Device &amp; Phone Line</th>
                  <th class="py-3 px-4">State</th>
                  <th class="py-3 px-4">Telemetry</th>
                  <th class="py-3 px-4">Latency</th>
                  <th class="py-3 px-4">Security</th>
                  <th class="py-3 px-4 text-right">Actions</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800/60">
                @for (item of sessions(); track item.sessionId) {
                  <tr
                    class="hover:bg-slate-800/30 transition cursor-pointer"
                    [class.bg-emerald-500-5]="activeSession()?.sessionId === item.sessionId"
                    (click)="selectSession(item)"
                  >
                    <!-- Device & Phone -->
                    <td class="py-3.5 px-4">
                      <div class="font-bold text-white text-sm">{{ item.deviceModel }}</div>
                      <div class="text-emerald-400 font-mono text-xs">{{ item.phoneNumber }}</div>
                    </td>

                    <!-- State -->
                    <td class="py-3.5 px-4">
                      <span [class]="getStatusBadgeClass(item.state)">
                        <span class="w-1.5 h-1.5 rounded-full" [class]="getStatusDotClass(item.state)"></span>
                        {{ item.state }}
                      </span>
                    </td>

                    <!-- Telemetry (Battery + Net) -->
                    <td class="py-3.5 px-4">
                      <div class="flex items-center gap-2">
                        <span class="font-mono text-slate-200 font-semibold">{{ item.telemetry.batteryPercentage }}%</span>
                        @if (item.telemetry.isCharging) {
                          <span class="text-amber-400 text-[10px]">⚡</span>
                        }
                        <span class="text-[10px] px-1.5 py-0.2 rounded bg-slate-800 text-slate-400">
                          {{ item.telemetry.networkType }}
                        </span>
                      </div>
                    </td>

                    <!-- Latency -->
                    <td class="py-3.5 px-4 font-mono font-bold text-emerald-400">
                      {{ item.telemetry.pingLatencyMs }}ms
                    </td>

                    <!-- Security -->
                    <td class="py-3.5 px-4">
                      @if (item.sessionValidationToken) {
                        <span class="text-[10px] px-2 py-0.5 rounded bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 font-mono">
                          AES-256-GCM
                        </span>
                      } @else {
                        <span class="text-[10px] px-2 py-0.5 rounded bg-slate-800 text-slate-400 border border-slate-700 font-mono">
                          Pending
                        </span>
                      }
                    </td>

                    <!-- Actions -->
                    <td class="py-3.5 px-4 text-right">
                      <div class="flex items-center justify-end gap-2" (click)="$event.stopPropagation()">
                        <button
                          type="button"
                          (click)="pingSession(item.sessionId)"
                          class="px-2.5 py-1 rounded bg-slate-800 hover:bg-slate-700 text-slate-300 text-xs border border-slate-700"
                          title="Heartbeat Ping"
                        >
                          Ping
                        </button>
                        <button
                          type="button"
                          (click)="disconnectSession(item.sessionId)"
                          class="px-2.5 py-1 rounded bg-rose-500/10 hover:bg-rose-500/20 text-rose-400 border border-rose-500/30 text-xs"
                          title="Disconnect Session"
                        >
                          Disconnect
                        </button>
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      </main>
    </div>
  `
})
export class WhatsAppCompanionStudioComponent implements OnInit, OnDestroy {
  private http = inject(HttpClient);

  // Signals
  phoneNumber = signal('+216 71 998 801');
  deviceModel = signal('Primary Support Desk Line (Ubuntu 24.04 / Chrome Multi-Device)');
  currentPairingCode = signal<string | null>(null);
  currentSessionId = signal<string | null>(null);
  secondsLeft = signal<number>(180);
  isGenerating = signal<boolean>(false);
  isVerifying = signal<boolean>(false);
  isDecrypting = signal<boolean>(false);
  copied = signal<boolean>(false);
  verifyInputCode = signal<string>('');
  operationMessage = signal<string | null>(null);
  operationSuccess = signal<boolean>(true);

  sessions = signal<CompanionSession[]>([]);
  activeSession = signal<CompanionSession | null>(null);
  packetLogs = signal<CompanionSocketPacketLog[]>([]);

  // Encrypted Media Workbench
  selectedMediaType = signal<string>('audio/ogg; codecs=opus');
  mediaCiphertext = signal<string>('');
  mediaIv = signal<string>('');
  mediaTag = signal<string>('');
  mediaChecksum = signal<string>('');
  decryptionResult = signal<MediaDecryptionResult | null>(null);

  // Computeds
  connectedCount = computed(() =>
    this.sessions().filter(s => s.state === 'Connected' || s.state === 'Syncing').length
  );

  countdownOffset = computed(() => {
    const total = 180;
    const current = this.secondsLeft();
    const progress = Math.max(0, Math.min(100, (current / total) * 100));
    return 100 - progress;
  });

  countdownProgressColor = computed(() => {
    const s = this.secondsLeft();
    if (s > 60) return 'text-emerald-400';
    if (s > 20) return 'text-amber-400';
    return 'text-rose-400';
  });

  private countdownTimer: any = null;
  private telemetryPoller: any = null;

  ngOnInit(): void {
    this.loadSessions();
    this.loadTestVector();

    // Start background telemetry poller
    this.telemetryPoller = setInterval(() => {
      if (this.activeSession() && this.activeSession()!.state === 'Connected') {
        this.refreshCurrentTelemetry();
      }
    }, 15000);
  }

  ngOnDestroy(): void {
    if (this.countdownTimer) clearInterval(this.countdownTimer);
    if (this.telemetryPoller) clearInterval(this.telemetryPoller);
  }

  setPresetPhone(phone: string, model: string): void {
    this.phoneNumber.set(phone);
    this.deviceModel.set(model);
  }

  loadSessions(): void {
    this.http.get<any>('/api/v1/channels/whatsapp/companion/sessions').subscribe({
      next: res => {
        if (res && res.sessions) {
          this.sessions.set(res.sessions);
          if (!this.activeSession() && res.sessions.length > 0) {
            this.selectSession(res.sessions[0]);
          }
        }
      },
      error: () => {
        // Fallback initial seeded state if backend mock
      }
    });
  }

  selectSession(session: CompanionSession): void {
    this.activeSession.set(session);
    this.currentSessionId.set(session.sessionId);
    this.loadSessionLogs(session.sessionId);
  }

  loadSessionLogs(sessionId: string): void {
    this.http.get<any>(`/api/v1/channels/whatsapp/companion/status/${sessionId}`).subscribe({
      next: res => {
        if (res && res.packetLogs) {
          this.packetLogs.set(res.packetLogs);
        }
      }
    });
  }

  requestPairingCode(): void {
    this.isGenerating.set(true);
    this.operationMessage.set(null);

    const body = {
      phoneNumber: this.phoneNumber(),
      deviceModel: this.deviceModel()
    };

    this.http.post<any>('/api/v1/channels/whatsapp/companion/pairing-code', body).subscribe({
      next: res => {
        this.isGenerating.set(false);
        this.currentPairingCode.set(res.pairingCode);
        this.currentSessionId.set(res.sessionId);
        this.verifyInputCode.set(res.pairingCode);
        this.secondsLeft.set(res.ttlSecondsRemaining || 180);
        this.operationSuccess.set(true);
        this.operationMessage.set(`Pairing code ${res.pairingCode} generated successfully. Valid for 180s.`);

        this.startCountdown();
        this.loadSessions();
      },
      error: err => {
        this.isGenerating.set(false);
        this.operationSuccess.set(false);
        this.operationMessage.set(err.error?.error || 'Failed to generate companion pairing code.');
      }
    });
  }

  startCountdown(): void {
    if (this.countdownTimer) clearInterval(this.countdownTimer);
    this.countdownTimer = setInterval(() => {
      const left = this.secondsLeft();
      if (left > 0) {
        this.secondsLeft.set(left - 1);
      } else {
        clearInterval(this.countdownTimer);
        this.currentPairingCode.set(null);
        this.operationSuccess.set(false);
        this.operationMessage.set('Pairing code expired (180s TTL). Please generate a new code.');
      }
    }, 1000);
  }

  autoFillCode(): void {
    if (this.currentPairingCode()) {
      this.verifyInputCode.set(this.currentPairingCode()!);
    }
  }

  copyPairingCode(): void {
    if (this.currentPairingCode()) {
      navigator.clipboard.writeText(this.currentPairingCode()!);
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 2000);
    }
  }

  verifyPairingCode(): void {
    const sessionId = this.currentSessionId();
    if (!sessionId) return;

    this.isVerifying.set(true);
    this.operationMessage.set(null);

    const body = {
      sessionId,
      pairingCode: this.verifyInputCode()
    };

    this.http.post<any>('/api/v1/channels/whatsapp/companion/verify-code', body).subscribe({
      next: res => {
        this.isVerifying.set(false);
        this.operationSuccess.set(true);
        this.operationMessage.set('Companion device connected via Noise_XX handshake!');
        this.currentPairingCode.set(null);
        if (this.countdownTimer) clearInterval(this.countdownTimer);

        this.loadSessions();
        this.loadSessionLogs(sessionId);
      },
      error: err => {
        this.isVerifying.set(false);
        this.operationSuccess.set(false);
        this.operationMessage.set(err.error?.message || 'Pairing verification failed.');
      }
    });
  }

  refreshCurrentTelemetry(): void {
    const active = this.activeSession();
    if (!active) return;

    this.http.get<any>(`/api/v1/channels/whatsapp/companion/telemetry/${active.sessionId}?refresh=true`).subscribe({
      next: telemetry => {
        const updated = { ...active, telemetry };
        this.activeSession.set(updated);
        this.loadSessionLogs(active.sessionId);
      }
    });
  }

  pingSession(sessionId: string): void {
    this.http.get<any>(`/api/v1/channels/whatsapp/companion/telemetry/${sessionId}?refresh=true`).subscribe({
      next: () => this.loadSessions()
    });
  }

  disconnectSession(sessionId: string): void {
    this.http.post<any>(`/api/v1/channels/whatsapp/companion/disconnect/${sessionId}`, {}).subscribe({
      next: () => this.loadSessions()
    });
  }

  disconnectActiveSession(): void {
    const active = this.activeSession();
    if (active) {
      this.disconnectSession(active.sessionId);
    }
  }

  simulateAck(ack: MessageAckLevel): void {
    const active = this.activeSession();
    if (!active) return;

    const body = {
      sessionId: active.sessionId,
      messageId: `MSG-${Date.now()}`,
      targetAck: ack,
      isVoiceNote: false
    };

    this.http.post<any>('/api/v1/channels/whatsapp/companion/simulate-ack', body).subscribe({
      next: () => {
        this.loadSessionLogs(active.sessionId);
      }
    });
  }

  loadTestVector(): void {
    const plaintext = 'SGrowing Encrypted Voice Note: Hello from WhatsApp Multi-Device Companion Bridge!';
    this.http.post<any>('/api/v1/channels/whatsapp/companion/encrypt-test-vector', {
      plaintext,
      mimeType: this.selectedMediaType()
    }).subscribe({
      next: res => {
        this.mediaCiphertext.set(res.ciphertextBase64);
        this.mediaIv.set(res.ivBase64);
        this.mediaTag.set(res.tagBase64);
        this.mediaChecksum.set(res.sha256Checksum);
        this.decryptionResult.set(null);
      }
    });
  }

  decryptMedia(): void {
    const active = this.activeSession();
    if (!active) return;

    this.isDecrypting.set(true);

    const body = {
      sessionId: active.sessionId,
      mimeType: this.selectedMediaType(),
      ciphertextBase64: this.mediaCiphertext(),
      ivBase64: this.mediaIv(),
      tagBase64: this.mediaTag(),
      expectedSha256Checksum: this.mediaChecksum()
    };

    this.http.post<any>('/api/v1/channels/whatsapp/companion/decrypt-media', body).subscribe({
      next: res => {
        this.isDecrypting.set(false);
        this.decryptionResult.set(res);
        this.loadSessionLogs(active.sessionId);
      },
      error: err => {
        this.isDecrypting.set(false);
        this.decryptionResult.set({
          success: false,
          isChecksumValid: false,
          computedSha256Checksum: 'ERROR',
          expectedSha256Checksum: this.mediaChecksum(),
          mimeType: this.selectedMediaType(),
          decryptedSizeBytes: 0,
          error: err.error?.error || 'Decryption failed'
        });
      }
    });
  }

  getStatusBadgeClass(state: CompanionSessionState): string {
    return state === 'Connected'
      ? 'px-2.5 py-0.5 rounded-full bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 text-xs font-semibold flex items-center gap-1.5'
      : state === 'Syncing'
      ? 'px-2.5 py-0.5 rounded-full bg-cyan-500/10 text-cyan-400 border border-cyan-500/20 text-xs font-semibold flex items-center gap-1.5'
      : state === 'PairingCodeGenerated'
      ? 'px-2.5 py-0.5 rounded-full bg-amber-500/10 text-amber-400 border border-amber-500/20 text-xs font-semibold flex items-center gap-1.5'
      : 'px-2.5 py-0.5 rounded-full bg-slate-800 text-slate-400 border border-slate-700 text-xs font-semibold flex items-center gap-1.5';
  }

  getStatusDotClass(state: CompanionSessionState): string {
    return state === 'Connected'
      ? 'bg-emerald-400 shadow-sm shadow-emerald-400'
      : state === 'Syncing'
      ? 'bg-cyan-400 animate-pulse'
      : state === 'PairingCodeGenerated'
      ? 'bg-amber-400 animate-ping'
      : 'bg-slate-500';
  }

  getBatteryColor(percent: number): string {
    if (percent > 50) return 'bg-emerald-400';
    if (percent > 20) return 'bg-amber-400';
    return 'bg-rose-500';
  }
}
