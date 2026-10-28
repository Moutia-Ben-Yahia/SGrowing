import { Component, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslationService } from '../../core/services/translation.service';
import { LanguageSwitcherComponent } from '../../core/components/language-switcher.component';

export interface WhatsAppInstance {
  id: string;
  name: string;
  phoneNumber?: string;
  status: 'Connected' | 'Pairing' | 'Disconnected';
  memoryUsageMb: number;
  messagesSent: number;
  messagesReceived: number;
  pairingCode?: string;
  connectedSince?: string;
}

export interface DispatchedLog {
  id: string;
  recipient: string;
  content: string;
  status: string;
  latencyMs: number;
  broker: string;
  timestamp: string;
}

@Component({
  selector: 'app-whatsapp-gateway-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LanguageSwitcherComponent],
  template: `
    <div class="min-h-screen bg-slate-950 text-slate-100 flex flex-col font-sans" dir="ltr">
      <!-- Header -->
      <header class="border-b border-slate-800 bg-slate-900/60 backdrop-blur px-6 py-4 flex items-center justify-between sticky top-0 z-50">
        <div class="flex items-center gap-4">
          <a routerLink="/" class="text-xl font-black bg-gradient-to-r from-emerald-400 via-teal-300 to-cyan-400 bg-clip-text text-transparent hover:opacity-90">
            SGrowing
          </a>
          <span class="text-slate-600">/</span>
          <div class="flex items-center gap-2">
            <span class="text-sm font-semibold text-slate-200">Native WhatsApp Gateway</span>
            <span class="text-xs px-2.5 py-0.5 rounded-full bg-cyan-500/10 text-cyan-400 border border-cyan-500/20 font-medium">
              Evolution-Go & Baileys
            </span>
          </div>
        </div>

        <div class="flex items-center gap-3">
          <button (click)="openNewInstanceModal.set(true)" class="px-3.5 py-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-xs font-semibold transition flex items-center gap-2">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"/></svg>
            New Number Instance
          </button>
          <app-language-switcher />
        </div>
      </header>

      <main class="flex-1 p-6 max-w-7xl w-full mx-auto space-y-6">
        <!-- Gateway Stats -->
        <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
          <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-5">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Active Numbers</div>
            <div class="text-2xl font-bold text-emerald-400 mt-2">{{ connectedCount() }} / {{ instances().length }}</div>
            <div class="text-xs text-slate-500 mt-1">Direct WebSocket protocol</div>
          </div>

          <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-5">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">RAM Footprint</div>
            <div class="text-2xl font-bold text-cyan-400 mt-2">~{{ totalMemoryUsage() }} MB</div>
            <div class="text-xs text-cyan-500/80 mt-1">20-25MB per instance (No Chromium)</div>
          </div>

          <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-5">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Messages Dispatched</div>
            <div class="text-2xl font-bold text-white mt-2">{{ totalSent().toLocaleString() }}</div>
            <div class="text-xs text-emerald-400 mt-1">NATS + RabbitMQ worker pool</div>
          </div>

          <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-5">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Avg Gateway Latency</div>
            <div class="text-2xl font-bold text-teal-300 mt-2">14 ms</div>
            <div class="text-xs text-slate-500 mt-1">Microsecond broker fanout</div>
          </div>
        </div>

        <!-- Two Columns: Instance Pairing & Message Dispatch -->
        <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
          <!-- Left: Instances & Pairing Cockpit (7 cols) -->
          <div class="lg:col-span-7 space-y-4">
            <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-5 shadow-xl">
              <h2 class="text-base font-bold text-white mb-4 flex items-center justify-between">
                <span>WhatsApp Instances & Companion Pairing</span>
                <span class="text-xs text-slate-400 font-normal">Signal Protocol E2EE</span>
              </h2>

              <div class="space-y-3">
                @for (inst of instances(); track inst.id) {
                  <div class="border border-slate-800 rounded-xl p-4 bg-slate-950/50 flex flex-col md:flex-row items-start md:items-center justify-between gap-4">
                    <div>
                      <div class="flex items-center gap-2">
                        <span class="font-bold text-white text-sm">{{ inst.name }}</span>
                        <span [class]="getStatusBadge(inst.status)">{{ inst.status }}</span>
                      </div>
                      <div class="text-xs text-slate-400 mt-1 font-mono">
                        {{ inst.phoneNumber || 'Unlinked Phone Number' }} &bull; Memory: {{ inst.memoryUsageMb }} MB
                      </div>
                      <div class="text-xs text-slate-500 mt-1">
                        Sent: {{ inst.messagesSent }} &bull; Received: {{ inst.messagesReceived }}
                      </div>
                    </div>

                    <div class="flex items-center gap-2 w-full md:w-auto">
                      @if (inst.status === 'Connected') {
                        <button (click)="disconnectInstance(inst.id)" class="px-3 py-1.5 bg-rose-500/10 hover:bg-rose-500/20 text-rose-400 border border-rose-500/30 rounded-lg text-xs font-medium transition">
                          Disconnect
                        </button>
                      } @else {
                        <button (click)="selectedInstance.set(inst); showPairingModal.set(true)" class="px-3.5 py-1.5 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-xs font-semibold transition shadow">
                          Pair Number
                        </button>
                      }
                    </div>
                  </div>
                }
              </div>
            </div>

            <!-- Pairing Modal / Drawer -->
            @if (showPairingModal()) {
              <div class="border border-emerald-500/30 bg-slate-900 rounded-xl p-5 space-y-4 shadow-2xl">
                <div class="flex items-center justify-between border-b border-slate-800 pb-3">
                  <div>
                    <h3 class="text-sm font-bold text-white">Pair {{ selectedInstance()?.name }}</h3>
                    <p class="text-xs text-slate-400">Connect via 8-Digit Pairing Code or QR Code</p>
                  </div>
                  <button (click)="showPairingModal.set(false)" class="text-slate-400 hover:text-white">&times;</button>
                </div>

                <!-- Tabs -->
                <div class="flex gap-2 border-b border-slate-800 pb-2">
                  <button (click)="activePairTab.set('code')" [class.border-b-2]="activePairTab() === 'code'" [class.border-emerald-400]="activePairTab() === 'code'" [class.text-emerald-400]="activePairTab() === 'code'" class="px-3 py-1 text-xs font-semibold text-slate-400 hover:text-white transition">
                    8-Digit Pairing Code (Phone Link)
                  </button>
                  <button (click)="activePairTab.set('qr')" [class.border-b-2]="activePairTab() === 'qr'" [class.border-emerald-400]="activePairTab() === 'qr'" [class.text-emerald-400]="activePairTab() === 'qr'" class="px-3 py-1 text-xs font-semibold text-slate-400 hover:text-white transition">
                    QR Code Scan
                  </button>
                </div>

                @if (activePairTab() === 'code') {
                  <div class="space-y-3">
                    <div>
                      <label class="block text-xs font-medium text-slate-400 mb-1">Target WhatsApp Phone Number</label>
                      <div class="flex gap-2">
                        <input [(ngModel)]="pairingPhone" type="text" placeholder="+216 71 890 123" class="flex-1 bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-white font-mono focus:outline-none focus:border-emerald-500">
                        <button (click)="generatePairingCode()" class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-xs font-bold transition">
                          Generate 8-Digit Code
                        </button>
                      </div>
                    </div>

                    @if (generatedCode()) {
                      <div class="bg-slate-950 border border-emerald-500/40 rounded-xl p-4 text-center space-y-2">
                        <div class="text-xs font-medium text-slate-400 uppercase tracking-widest">Your 8-Digit Pairing Code</div>
                        <div class="text-3xl font-black font-mono tracking-widest text-emerald-400">{{ generatedCode() }}</div>
                        <div class="text-xs text-slate-400 max-w-sm mx-auto leading-relaxed">
                          Open WhatsApp on your phone &rarr; <strong>Settings</strong> &rarr; <strong>Linked Devices</strong> &rarr; <strong>Link with Phone Number instead</strong>, and enter this code.
                        </div>
                        <button (click)="confirmPairingSuccess()" class="mt-3 px-4 py-2 bg-emerald-500 text-slate-950 font-bold rounded-lg text-xs transition hover:bg-emerald-400 shadow">
                          Simulate Mobile App Confirmation
                        </button>
                      </div>
                    }
                  </div>
                } @else {
                  <div class="text-center p-6 bg-slate-950 rounded-xl border border-slate-800 space-y-3">
                    <div class="w-36 h-36 mx-auto bg-white p-2 rounded-lg flex items-center justify-center shadow-inner">
                      <!-- Simulated QR Matrix -->
                      <div class="w-full h-full bg-slate-900 rounded flex flex-col items-center justify-center text-emerald-400 text-xs font-mono">
                        [QR MATRIX]
                        <span class="text-[9px] text-slate-400 mt-1">Noise_XX E2EE</span>
                      </div>
                    </div>
                    <div class="text-xs text-slate-400">Point your phone camera at this QR code in WhatsApp Linked Devices</div>
                    <button (click)="confirmPairingSuccess()" class="px-4 py-1.5 bg-emerald-600 text-white font-semibold rounded text-xs transition">
                      Simulate QR Scanned
                    </button>
                  </div>
                }
              </div>
            }
          </div>

          <!-- Right: Test Dispatcher & Live Log (5 cols) -->
          <div class="lg:col-span-5 space-y-4">
            <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-5 shadow-xl space-y-3">
              <h2 class="text-base font-bold text-white flex items-center justify-between">
                <span>High-Speed Message Dispatcher</span>
                <span class="text-xs text-cyan-400 font-mono">Go Worker Fanout</span>
              </h2>

              <div>
                <label class="block text-xs font-medium text-slate-400 mb-1">Recipient Phone Number</label>
                <input [(ngModel)]="dispatchRecipient" type="text" class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-white font-mono focus:outline-none focus:border-cyan-500" placeholder="+216 98 123 456">
              </div>

              <div>
                <label class="block text-xs font-medium text-slate-400 mb-1">Message Content / Template</label>
                <textarea [(ngModel)]="dispatchBody" rows="3" class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:border-cyan-500" placeholder="Hello! Your order TRF-20261028-A01 is out for delivery."></textarea>
              </div>

              <button (click)="dispatchTestMessage()" class="w-full py-2.5 bg-gradient-to-r from-cyan-600 to-teal-600 hover:from-cyan-500 hover:to-teal-500 text-white rounded-lg text-xs font-bold transition shadow-lg shadow-cyan-950 flex items-center justify-center gap-2">
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z"/></svg>
                Dispatch via Go Gateway
              </button>

              <div class="pt-2 border-t border-slate-800 flex gap-2">
                <button (click)="openInteractiveModal.set(true)" class="w-full py-2 bg-slate-800 hover:bg-slate-700 text-cyan-300 rounded-lg text-xs font-semibold transition border border-cyan-500/20 flex items-center justify-center gap-1.5">
                  <span>📱</span> Compose 1-Tap Quick-Reply Buttons
                </button>
              </div>
            </div>

            <!-- Live Stream Dispatch Log -->
            <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-4 shadow-xl space-y-3">
              <div class="text-xs font-bold text-slate-400 uppercase tracking-wider flex items-center justify-between">
                <span>Live Event Fanout Log</span>
                <span class="w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
              </div>

              <div class="space-y-2 max-h-60 overflow-y-auto font-mono text-xs">
                @for (log of dispatchLogs(); track log.id) {
                  <div class="p-2.5 rounded bg-slate-950/70 border border-slate-800/80 space-y-1">
                    <div class="flex items-center justify-between text-slate-300">
                      <span class="text-cyan-400">{{ log.recipient }}</span>
                      <span class="text-emerald-400">{{ log.latencyMs }}ms</span>
                    </div>
                    <div class="text-slate-400 truncate text-[11px]">{{ log.content }}</div>
                    <div class="text-[10px] text-slate-500 flex items-center justify-between">
                      <span>{{ log.broker }}</span>
                      <span>{{ log.timestamp }}</span>
                    </div>
                  </div>
                }
              </div>
            </div>
          </div>
        </div>
      </main>

      <!-- Interactive Buttons Modal -->
      @if (openInteractiveModal()) {
        <div class="fixed inset-0 bg-black/75 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl max-w-lg w-full p-6 space-y-4 shadow-2xl">
            <div class="flex items-center justify-between pb-3 border-b border-slate-800">
              <h3 class="text-base font-bold text-white flex items-center gap-2">
                <span>📱</span> WhatsApp Interactive Buttons Composer
              </h3>
              <button (click)="openInteractiveModal.set(false)" class="text-slate-400 hover:text-white">✕</button>
            </div>

            <div class="space-y-3 text-xs">
              <div>
                <label class="block text-slate-400 mb-1">Recipient Phone Number</label>
                <input [(ngModel)]="interactiveRecipient" type="text" class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-white font-mono" placeholder="+216 98 123 456">
              </div>

              <div>
                <label class="block text-slate-400 mb-1">Message Body</label>
                <textarea [(ngModel)]="interactiveBody" rows="2" class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-white" placeholder="Hi! Your order is ready. Would you like tracking updates?"></textarea>
              </div>

              <div class="grid grid-cols-3 gap-2">
                <div>
                  <label class="block text-slate-400 mb-1">Button 1</label>
                  <input [(ngModel)]="button1" type="text" class="w-full bg-slate-950 border border-slate-700 rounded px-2.5 py-1.5 text-white" placeholder="Confirm">
                </div>
                <div>
                  <label class="block text-slate-400 mb-1">Button 2</label>
                  <input [(ngModel)]="button2" type="text" class="w-full bg-slate-950 border border-slate-700 rounded px-2.5 py-1.5 text-white" placeholder="Reschedule">
                </div>
                <div>
                  <label class="block text-slate-400 mb-1">Button 3</label>
                  <input [(ngModel)]="button3" type="text" class="w-full bg-slate-950 border border-slate-700 rounded px-2.5 py-1.5 text-white" placeholder="Support">
                </div>
              </div>

              <!-- Interactive Handset Preview -->
              <div class="p-4 bg-emerald-950/30 border border-emerald-500/20 rounded-xl space-y-2">
                <div class="text-[10px] text-emerald-400 font-bold uppercase tracking-wider">Mobile Handset Preview</div>
                <div class="p-3 bg-emerald-900/40 rounded-lg text-slate-100 text-xs">
                  {{ interactiveBody }}
                </div>
                <div class="grid grid-cols-3 gap-2 pt-1">
                  <div class="p-2 bg-slate-900 border border-emerald-500/30 text-emerald-300 text-center rounded font-medium text-[11px] shadow">
                    {{ button1 }}
                  </div>
                  <div class="p-2 bg-slate-900 border border-emerald-500/30 text-emerald-300 text-center rounded font-medium text-[11px] shadow">
                    {{ button2 }}
                  </div>
                  <div class="p-2 bg-slate-900 border border-emerald-500/30 text-emerald-300 text-center rounded font-medium text-[11px] shadow">
                    {{ button3 }}
                  </div>
                </div>
              </div>
            </div>

            <div class="flex justify-end gap-3 pt-3 border-t border-slate-800">
              <button (click)="openInteractiveModal.set(false)" class="px-4 py-2 bg-slate-800 text-slate-300 rounded-lg text-xs hover:bg-slate-700">Cancel</button>
              <button (click)="dispatchInteractiveTest()" class="px-4 py-2 bg-gradient-to-r from-emerald-600 to-teal-600 text-white rounded-lg text-xs font-bold hover:from-emerald-500 hover:to-teal-500">Dispatch Interactive</button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class WhatsAppGatewayCockpitComponent {
  private translationService = inject(TranslationService);

  openNewInstanceModal = signal<boolean>(false);
  showPairingModal = signal<boolean>(false);
  openInteractiveModal = signal<boolean>(false);
  selectedInstance = signal<WhatsAppInstance | null>(null);
  activePairTab = signal<'code' | 'qr'>('code');

  pairingPhone = '+216 71 890 123';
  generatedCode = signal<string | null>(null);

  dispatchRecipient = '+216 98 444 888';
  dispatchBody = 'Your wholesale stock transfer TRF-20261028-A01 is dispatched from Tunis Central Hub.';

  interactiveRecipient = '+216 98 444 888';
  interactiveBody = 'Your order is out for delivery with our courier driver. Please confirm your presence:';
  button1 = 'Confirm Delivery';
  button2 = 'Change Address';
  button3 = 'Contact Driver';

  instances = signal<WhatsAppInstance[]>([
    {
      id: 'inst-01',
      name: 'primary-sales-wa',
      phoneNumber: '+216 71 890 123',
      status: 'Connected',
      memoryUsageMb: 24,
      messagesSent: 1420,
      messagesReceived: 3180,
      connectedSince: '2026-10-14'
    },
    {
      id: 'inst-02',
      name: 'logistics-support-wa',
      phoneNumber: '+216 73 456 789',
      status: 'Connected',
      memoryUsageMb: 22,
      messagesSent: 890,
      messagesReceived: 1240,
      connectedSince: '2026-10-20'
    },
    {
      id: 'inst-03',
      name: 'outbound-campaigns-wa',
      status: 'Disconnected',
      memoryUsageMb: 0,
      messagesSent: 0,
      messagesReceived: 0
    }
  ]);

  dispatchLogs = signal<DispatchedLog[]>([
    {
      id: 'log-01',
      recipient: '+216 98 123 456',
      content: 'Your order was routed to Sousse Warehouse.',
      status: 'Delivered',
      latencyMs: 12,
      broker: 'NATS JetStream + WebSockets',
      timestamp: '10:04:15'
    },
    {
      id: 'log-02',
      recipient: '+216 97 789 012',
      content: 'True margin ledger update approved.',
      status: 'Read',
      latencyMs: 16,
      broker: 'RabbitMQ + WebSockets',
      timestamp: '10:03:52'
    }
  ]);

  connectedCount = computed(() => this.instances().filter(i => i.status === 'Connected').length);
  totalMemoryUsage = computed(() => this.instances().reduce((acc, i) => acc + i.memoryUsageMb, 0));
  totalSent = computed(() => this.instances().reduce((acc, i) => acc + i.messagesSent, 0));

  getStatusBadge(status: string): string {
    switch (status) {
      case 'Connected':
        return 'px-2 py-0.5 rounded text-xs font-semibold bg-emerald-500/10 text-emerald-400 border border-emerald-500/20';
      case 'Pairing':
        return 'px-2 py-0.5 rounded text-xs font-semibold bg-amber-500/10 text-amber-400 border border-amber-500/20';
      default:
        return 'px-2 py-0.5 rounded text-xs font-semibold bg-slate-800 text-slate-400 border border-slate-700';
    }
  }

  generatePairingCode() {
    const p1 = Math.floor(1000 + Math.random() * 9000);
    const p2 = Math.floor(1000 + Math.random() * 9000);
    this.generatedCode.set(`${p1}-${p2}`);
  }

  confirmPairingSuccess() {
    const current = this.selectedInstance();
    if (current) {
      this.instances.update(list =>
        list.map(i => i.id === current.id ? { ...i, status: 'Connected', phoneNumber: this.pairingPhone, memoryUsageMb: 24 } : i)
      );
    }
    this.showPairingModal.set(false);
    this.generatedCode.set(null);
  }

  disconnectInstance(id: string) {
    this.instances.update(list =>
      list.map(i => i.id === id ? { ...i, status: 'Disconnected', memoryUsageMb: 0 } : i)
    );
  }

  dispatchTestMessage() {
    const lat = Math.floor(8 + Math.random() * 15);
    const newLog: DispatchedLog = {
      id: 'log-' + Math.random().toString(36).substring(2, 6),
      recipient: this.dispatchRecipient,
      content: this.dispatchBody,
      status: 'Dispatched',
      latencyMs: lat,
      broker: 'NATS JetStream + RabbitMQ',
      timestamp: new Date().toTimeString().split(' ')[0]
    };

    this.dispatchLogs.update(logs => [newLog, ...logs]);
    this.instances.update(list => {
      if (list.length > 0 && list[0].status === 'Connected') {
        return [{ ...list[0], messagesSent: list[0].messagesSent + 1 }, ...list.slice(1)];
      }
      return list;
    });
  }

  dispatchInteractiveTest() {
    const lat = Math.floor(6 + Math.random() * 12);
    const content = `[Interactive Buttons: "${this.button1}", "${this.button2}", "${this.button3}"] ${this.interactiveBody}`;
    const newLog: DispatchedLog = {
      id: 'log-' + Math.random().toString(36).substring(2, 6),
      recipient: this.interactiveRecipient,
      content: content,
      status: 'Dispatched',
      latencyMs: lat,
      broker: 'Whatsmeow Binary Codec + RabbitMQ',
      timestamp: new Date().toTimeString().split(' ')[0]
    };

    this.dispatchLogs.update(logs => [newLog, ...logs]);
    this.instances.update(list => {
      if (list.length > 0 && list[0].status === 'Connected') {
        return [{ ...list[0], messagesSent: list[0].messagesSent + 1 }, ...list.slice(1)];
      }
      return list;
    });
    this.openInteractiveModal.set(false);
  }
}
