import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface VoiceCallUI {
  id: string;
  caller: string;
  contactName: string;
  direction: 'Inbound' | 'Outbound';
  status: 'Completed' | 'InProgress' | 'Missed';
  durationSeconds: number;
  timeAgo: string;
  transcriptSnippet: string;
}

@Component({
  selector: 'app-voice-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="flex h-screen bg-slate-950 text-slate-100 font-sans overflow-hidden">
      <!-- Main Content -->
      <div class="flex-1 flex flex-col h-full overflow-hidden">
        <!-- Header -->
        <header class="h-16 border-b border-slate-800 bg-slate-900/50 backdrop-blur px-8 flex items-center justify-between">
          <div class="flex items-center gap-3">
            <div class="w-8 h-8 rounded-lg bg-emerald-600/20 text-emerald-400 flex items-center justify-center font-bold text-sm border border-emerald-500/30">
              📞
            </div>
            <div>
              <h1 class="text-lg font-semibold text-white tracking-tight">Telephony & Real-Time AI Voice Studio</h1>
              <p class="text-xs text-slate-400">SIP/WebRTC softphone, AI autonomous voice agent & call intelligence</p>
            </div>
          </div>
          <div class="flex items-center gap-3">
            <span class="text-xs text-emerald-400 bg-emerald-500/10 px-3 py-1 rounded-full border border-emerald-500/20 font-medium flex items-center gap-1.5">
              <span class="w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
              SIP Gateway Online (+1 800-SGROWING)
            </span>
          </div>
        </header>

        <!-- Body -->
        <div class="flex-1 overflow-y-auto p-8 space-y-8">
          
          <div class="grid grid-cols-1 lg:grid-cols-12 gap-8">
            <!-- 1. Softphone Keypad (4 cols) -->
            <div class="lg:col-span-4 bg-slate-900 border border-slate-800 rounded-xl p-6 shadow-xl space-y-5">
              <h2 class="text-base font-semibold text-white flex items-center justify-between">
                <span>WebRTC Softphone</span>
                <span class="text-xs text-slate-400 font-mono">Dialer</span>
              </h2>

              <!-- Dial Display -->
              <div class="bg-slate-950 border border-slate-800 rounded-xl p-3 text-center">
                <input 
                  [(ngModel)]="dialNumber" 
                  placeholder="+1 (555) 000-0000" 
                  class="w-full bg-transparent text-center text-xl font-mono text-white focus:outline-none placeholder-slate-600 tracking-wider" />
              </div>

              <!-- Keypad Grid -->
              <div class="grid grid-cols-3 gap-3">
                @for (k of keypad(); track k.digit) {
                  <button 
                    (click)="pressKey(k.digit)"
                    class="bg-slate-950/80 hover:bg-slate-800 border border-slate-800 hover:border-slate-700 py-3 rounded-xl transition text-center shadow-sm">
                    <div class="text-lg font-bold text-white leading-none">{{ k.digit }}</div>
                    <div class="text-[10px] text-slate-500 tracking-widest mt-1">{{ k.letters }}</div>
                  </button>
                }
              </div>

              <!-- Call Action Buttons -->
              <div class="flex items-center gap-3 pt-2">
                @if (!isCallActive()) {
                  <button 
                    (click)="startCall()" 
                    [disabled]="!dialNumber"
                    class="flex-1 bg-emerald-600 hover:bg-emerald-500 disabled:opacity-40 text-white font-semibold py-3 rounded-xl text-sm transition shadow-lg shadow-emerald-900/30 flex items-center justify-center gap-2">
                    <span>📞</span>
                    <span>Start Call</span>
                  </button>
                } @else {
                  <button 
                    (click)="endCall()" 
                    class="flex-1 bg-rose-600 hover:bg-rose-500 text-white font-semibold py-3 rounded-xl text-sm transition shadow-lg shadow-rose-900/30 flex items-center justify-center gap-2">
                    <span>🛑</span>
                    <span>End Call ({{ activeCallSeconds() }}s)</span>
                  </button>
                }
              </div>
            </div>

            <!-- 2. Live AI Voice Agent Turn & Transcript (8 cols) -->
            <div class="lg:col-span-8 bg-slate-900 border border-slate-800 rounded-xl p-6 shadow-xl flex flex-col justify-between">
              <div>
                <div class="flex items-center justify-between pb-4 border-b border-slate-800">
                  <div class="flex items-center gap-2">
                    <span class="w-3 h-3 rounded-full" [class.bg-emerald-400]="isCallActive()" [class.bg-slate-600]="!isCallActive()"></span>
                    <h2 class="text-base font-semibold text-white">Live Call Stream & AI Speech Engine</h2>
                  </div>
                  <span class="text-xs text-indigo-400 bg-indigo-950 px-2.5 py-0.5 rounded border border-indigo-800">
                    Real-time STT $\rightarrow$ Agent $\rightarrow$ TTS (&lt;450ms)
                  </span>
                </div>

                <!-- Transcript stream -->
                <div class="mt-4 space-y-3 min-h-[220px]">
                  @if (!isCallActive()) {
                    <div class="text-center py-12 text-slate-500 text-sm">
                      No active call session. Dial a number or click "Simulate Inbound Call" below.
                    </div>
                  } @else {
                    <div class="space-y-3">
                      <div class="flex items-start gap-3">
                        <span class="text-xs font-bold text-slate-400 font-mono">Caller:</span>
                        <div class="bg-slate-950 p-2.5 rounded-lg border border-slate-800 text-xs text-slate-200">
                          "Hello, I'm calling about renewing our enterprise subscription and checking if our order went through."
                        </div>
                      </div>

                      <div class="flex items-start gap-3">
                        <span class="text-xs font-bold text-emerald-400 font-mono">AI Voice:</span>
                        <div class="bg-emerald-950/40 p-2.5 rounded-lg border border-emerald-800/60 text-xs text-emerald-200">
                          "Hello! Thank you for calling SGrowing. I've located your account for Elena Fisher and verified your order is active. Would you like me to send an assisted checkout link to your WhatsApp?"
                        </div>
                      </div>

                      <div class="flex items-start gap-3">
                        <span class="text-xs font-bold text-slate-400 font-mono">Caller:</span>
                        <div class="bg-slate-950 p-2.5 rounded-lg border border-slate-800 text-xs text-slate-200">
                          "Yes please, that would be wonderful!"
                        </div>
                      </div>
                    </div>
                  }
                </div>
              </div>

              <!-- Quick Simulator Trigger -->
              <div class="pt-4 border-t border-slate-800 flex items-center justify-between">
                <span class="text-xs text-slate-400">Testing Tools:</span>
                <button 
                  (click)="simulateInboundCall()"
                  class="bg-indigo-600 hover:bg-indigo-500 text-white text-xs px-3 py-1.5 rounded-lg transition">
                  Simulate Inbound VIP Call
                </button>
              </div>
            </div>
          </div>

          <!-- 3. Call Logs Table -->
          <div class="bg-slate-900 border border-slate-800 rounded-xl p-6 shadow-xl space-y-4">
            <h2 class="text-base font-semibold text-white">Recent Voice Sessions & Recordings</h2>
            
            <div class="overflow-x-auto">
              <table class="w-full text-left text-xs text-slate-300">
                <thead class="text-[11px] text-slate-400 uppercase tracking-wider bg-slate-950/60 border-b border-slate-800">
                  <tr>
                    <th class="py-3 px-4">Contact</th>
                    <th class="py-3 px-4">Phone</th>
                    <th class="py-3 px-4">Direction</th>
                    <th class="py-3 px-4">Duration</th>
                    <th class="py-3 px-4">Status</th>
                    <th class="py-3 px-4">AI Transcript Snippet</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800/60">
                  @for (call of callLogs(); track call.id) {
                    <tr class="hover:bg-slate-800/40 transition">
                      <td class="py-3 px-4 font-medium text-white">{{ call.contactName }}</td>
                      <td class="py-3 px-4 font-mono text-slate-400">{{ call.caller }}</td>
                      <td class="py-3 px-4">
                        <span class="px-2 py-0.5 rounded text-[10px] font-semibold"
                              [class.bg-cyan-950]="call.direction === 'Inbound'"
                              [class.text-cyan-400]="call.direction === 'Inbound'"
                              [class.bg-purple-950]="call.direction === 'Outbound'"
                              [class.text-purple-400]="call.direction === 'Outbound'">
                          {{ call.direction }}
                        </span>
                      </td>
                      <td class="py-3 px-4 font-mono">{{ call.durationSeconds }}s</td>
                      <td class="py-3 px-4 text-emerald-400 font-medium">✓ {{ call.status }}</td>
                      <td class="py-3 px-4 text-slate-400 truncate max-w-xs">{{ call.transcriptSnippet }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>

        </div>
      </div>
    </div>
  `
})
export class VoiceStudioComponent implements OnInit {
  public dialNumber = '+1 (555) 303-9999';
  public isCallActive = signal<boolean>(false);
  public activeCallSeconds = signal<number>(0);
  private timerId: any;

  public keypad = signal<{ digit: string; letters: string }[]>([
    { digit: '1', letters: ' ' },
    { digit: '2', letters: 'ABC' },
    { digit: '3', letters: 'DEF' },
    { digit: '4', letters: 'GHI' },
    { digit: '5', letters: 'JKL' },
    { digit: '6', letters: 'MNO' },
    { digit: '7', letters: 'PQRS' },
    { digit: '8', letters: 'TUV' },
    { digit: '9', letters: 'WXYZ' },
    { digit: '*', letters: ' ' },
    { digit: '0', letters: '+' },
    { digit: '#', letters: ' ' },
  ]);

  public callLogs = signal<VoiceCallUI[]>([
    { id: '1', caller: '+15553039999', contactName: 'Diana Prince', direction: 'Inbound', status: 'Completed', durationSeconds: 84, timeAgo: '10m ago', transcriptSnippet: 'Verified renewal order and confirmed assisted payment link.' },
    { id: '2', caller: '+15559876543', contactName: 'Elena Fisher', direction: 'Outbound', status: 'Completed', durationSeconds: 142, timeAgo: '2h ago', transcriptSnippet: 'Sales qualification call for Enterprise plan with demo scheduled.' },
  ]);

  ngOnInit(): void {}

  public pressKey(digit: string): void {
    this.dialNumber += digit;
  }

  public startCall(): void {
    this.isCallActive.set(true);
    this.activeCallSeconds.set(0);
    this.timerId = setInterval(() => {
      this.activeCallSeconds.update(s => s + 1);
    }, 1000);
  }

  public endCall(): void {
    this.isCallActive.set(false);
    if (this.timerId) clearInterval(this.timerId);
  }

  public simulateInboundCall(): void {
    this.dialNumber = '+1 (555) 303-9999';
    this.startCall();
  }
}
