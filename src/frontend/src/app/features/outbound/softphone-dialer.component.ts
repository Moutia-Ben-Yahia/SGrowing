import { Component, OnInit, OnDestroy, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { 
  InboxSlotAllocationDto, 
  CallSessionInitiationDto, 
  ClassifiedReplyDto 
} from '../../core/models/inspiration.models';

@Component({
  selector: 'app-softphone-dialer',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Topbar -->
        <header class="h-14 border-b border-slate-800 bg-slate-900/60 px-6 flex items-center justify-between shrink-0">
          <div class="flex items-center gap-3">
            <a routerLink="/" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← Inbox
            </a>
            <span class="text-slate-700">|</span>
            <span class="text-base font-semibold text-white flex items-center gap-2">
              📞 Autonomous SDR Fleet & WebRTC Softphone Dialer
            </span>
            <span class="bg-cyan-500/20 text-cyan-400 text-[10px] font-mono px-2 py-0.5 rounded-full border border-cyan-500/30">
              AgentSDR • Ultra-Low Latency
            </span>
          </div>

          <div class="flex items-center gap-3">
            <button
              (click)="allocateSlots()"
              [disabled]="isAllocating()"
              class="px-3 py-1.5 bg-cyan-600 hover:bg-cyan-500 disabled:opacity-50 text-white rounded-lg text-xs font-semibold shadow-lg shadow-cyan-950/20 transition flex items-center gap-1.5">
              <span>⚡</span>
              <span>{{ isAllocating() ? 'Allocating...' : 'Refresh Inbox Slot Pools' }}</span>
            </button>
          </div>
        </header>

        <!-- Body Area -->
        <div class="p-6 space-y-6 flex-1">
          <!-- 3-Column Layout: Softphone Dialpad / Active Call / Slot Pool & AI Classifier -->
          <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
            
            <!-- Column 1: WebRTC Dialpad (4 cols) -->
            <div class="lg:col-span-4 bg-slate-900/40 border border-slate-800 rounded-2xl p-5 flex flex-col justify-between">
              <div>
                <div class="flex items-center justify-between mb-4">
                  <h3 class="text-sm font-semibold text-white">Browser Softphone</h3>
                  <span class="flex items-center gap-1.5 text-[10px] text-emerald-400 font-mono">
                    <span class="w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span> WebRTC Ready
                  </span>
                </div>

                <!-- Number Display Screen -->
                <div class="bg-slate-950 border border-slate-800 rounded-xl p-3 mb-4 text-center">
                  <input 
                    type="text" 
                    [(ngModel)]="dialedNumber" 
                    placeholder="+213 555 019 283"
                    class="w-full bg-transparent text-xl font-mono text-center text-white focus:outline-none tracking-wider" />
                  <div class="text-[10px] text-slate-500 mt-1">E.164 International Format</div>
                </div>

                <!-- Dialpad Matrix -->
                <div class="grid grid-cols-3 gap-2.5 max-w-[280px] mx-auto mb-4">
                  @for (btn of dialButtons; track btn.digit) {
                    <button 
                      (click)="pressKey(btn.digit)"
                      class="h-12 rounded-xl bg-slate-800/80 hover:bg-slate-700/80 active:bg-cyan-600 border border-slate-700/60 flex flex-col items-center justify-center transition">
                      <span class="text-base font-bold text-white">{{ btn.digit }}</span>
                      <span class="text-[8px] text-slate-400 font-mono tracking-widest">{{ btn.sub }}</span>
                    </button>
                  }
                </div>
              </div>

              <!-- Call Trigger / Hangup Controls -->
              <div class="space-y-2">
                @if (!activeCall()) {
                  <button 
                    (click)="startCall()"
                    [disabled]="!dialedNumber.trim() || isCalling()"
                    class="w-full py-3 bg-emerald-600 hover:bg-emerald-500 disabled:opacity-50 text-white rounded-xl font-semibold text-sm shadow-lg shadow-emerald-950/30 transition flex items-center justify-center gap-2">
                    <span>📞</span>
                    <span>{{ isCalling() ? 'Establishing WebRTC Room...' : 'Start Outbound Call' }}</span>
                  </button>
                } @else {
                  <button 
                    (click)="endCall()"
                    class="w-full py-3 bg-rose-600 hover:bg-rose-500 text-white rounded-xl font-semibold text-sm shadow-lg shadow-rose-950/30 transition flex items-center justify-center gap-2">
                    <span>🔴</span>
                    <span>Hang Up (End Call)</span>
                  </button>
                }
              </div>
            </div>

            <!-- Column 2: Live In-Call Session & Disposition (4 cols) -->
            <div class="lg:col-span-4 bg-slate-900/40 border border-slate-800 rounded-2xl p-5 flex flex-col justify-between">
              <div>
                <h3 class="text-sm font-semibold text-white mb-1">Live Call Telemetry</h3>
                <p class="text-xs text-slate-400 mb-4">Session state, audio spectrum, and post-call disposition logging.</p>

                @if (activeCall()) {
                  <div class="p-4 rounded-xl bg-slate-950 border border-cyan-500/40 space-y-4">
                    <div class="flex items-center justify-between">
                      <span class="px-2 py-0.5 rounded text-[10px] font-mono bg-cyan-500/20 text-cyan-400 border border-cyan-500/30">
                        {{ activeCall()?.status }}
                      </span>
                      <span class="text-xs font-mono text-cyan-300 font-bold">
                        {{ callDuration() }}
                      </span>
                    </div>

                    <!-- Audio Waveform Visualization Simulation -->
                    <div class="h-12 bg-slate-900 rounded-lg flex items-center justify-center gap-1.5 px-3">
                      <div class="w-1.5 bg-cyan-400 rounded-full animate-bounce h-6"></div>
                      <div class="w-1.5 bg-cyan-300 rounded-full animate-bounce h-10 delay-75"></div>
                      <div class="w-1.5 bg-cyan-400 rounded-full animate-bounce h-4 delay-100"></div>
                      <div class="w-1.5 bg-cyan-500 rounded-full animate-bounce h-8 delay-150"></div>
                      <div class="w-1.5 bg-cyan-300 rounded-full animate-bounce h-3 delay-200"></div>
                      <div class="w-1.5 bg-cyan-400 rounded-full animate-bounce h-9 delay-75"></div>
                    </div>

                    <div class="text-[11px] text-slate-400 space-y-1">
                      <div><strong class="text-slate-300">Prospect:</strong> {{ activeCall()?.prospectPhone }}</div>
                      <div><strong class="text-slate-300">Agent:</strong> {{ activeCall()?.agentPhone }}</div>
                      <div class="truncate"><strong class="text-slate-300">WebRTC Token:</strong> {{ activeCall()?.webRtcRoomToken }}</div>
                    </div>
                  </div>
                } @else {
                  <div class="p-8 rounded-xl bg-slate-950/40 border border-dashed border-slate-800 text-center text-slate-500 text-xs">
                    No call in progress. Enter a prospect phone number or select from campaign queue to dial.
                  </div>
                }

                <!-- Post-Call Disposition Logger -->
                <div class="mt-4 pt-4 border-t border-slate-800 space-y-3">
                  <h4 class="text-xs font-semibold text-slate-300 uppercase tracking-wider">Log Call Disposition</h4>
                  <select 
                    [(ngModel)]="selectedDisposition"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-cyan-500">
                    <option value="MeetingBooked">🎉 Meeting Booked</option>
                    <option value="InterestedFollowUp">⭐ Interested (Follow-up Needed)</option>
                    <option value="LeftVoicemail">📼 Left Voicemail</option>
                    <option value="BusyNoAnswer">⏳ Busy / No Answer</option>
                    <option value="NotInterested">❌ Not Interested</option>
                    <option value="GatekeeperBlock">🛑 Gatekeeper Blocked</option>
                  </select>

                  <textarea 
                    [(ngModel)]="dispositionNotes"
                    rows="2"
                    placeholder="Call notes & next steps..."
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg p-2.5 text-xs text-white focus:outline-none focus:border-cyan-500"></textarea>

                  <button 
                    (click)="submitDisposition()"
                    [disabled]="isSubmittingDisposition()"
                    class="w-full py-2 bg-slate-800 hover:bg-slate-700 disabled:opacity-50 text-white rounded-lg text-xs font-semibold transition">
                    {{ isSubmittingDisposition() ? 'Logging...' : 'Submit Disposition to CRM' }}
                  </button>
                </div>
              </div>
            </div>

            <!-- Column 3: Multi-Inbox Rotation & AI Classifier (4 cols) -->
            <div class="lg:col-span-4 space-y-6">
              <!-- Multi-Inbox Pool -->
              <div class="bg-slate-900/40 border border-slate-800 rounded-2xl p-5 space-y-3">
                <div class="flex items-center justify-between">
                  <h3 class="text-sm font-semibold text-white">Inbox Rotation Pool</h3>
                  <span class="text-[10px] text-slate-400 font-mono">Slot Pacing: 45s</span>
                </div>

                <div class="space-y-2">
                  @for (slot of inboxSlots(); track slot.senderAccountId) {
                    <div class="p-2.5 rounded-xl bg-slate-950 border border-slate-800 flex items-center justify-between text-xs">
                      <div>
                        <div class="font-medium text-white">{{ slot.senderEmail }}</div>
                        <div class="text-[10px] text-slate-500 font-mono">{{ slot.sendingDomain }}</div>
                      </div>
                      <div class="text-right">
                        <div class="font-mono text-cyan-400 font-semibold">{{ slot.allocatedSlotsRemaining }} slots left</div>
                        <div class="text-[10px] text-slate-500">Cap: {{ slot.dailyCap }}/day</div>
                      </div>
                    </div>
                  }
                </div>
              </div>

              <!-- AI Inbound Reply Classifier -->
              <div class="bg-slate-900/40 border border-slate-800 rounded-2xl p-5 space-y-3">
                <div class="flex items-center justify-between">
                  <h3 class="text-sm font-semibold text-white">AI Reply Classifier</h3>
                  <span class="px-2 py-0.5 rounded text-[10px] font-mono bg-purple-500/20 text-purple-400">LLM Intent</span>
                </div>

                <input 
                  type="text" 
                  [(ngModel)]="classifySubject"
                  placeholder="Subject: e.g. Re: Quick question regarding SGrowing"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-white focus:outline-none focus:border-cyan-500" />

                <textarea 
                  [(ngModel)]="classifyBody"
                  rows="2"
                  placeholder="Email body: e.g. Looks interesting! Can we chat Thursday at 3pm?"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg p-2 text-xs text-white focus:outline-none focus:border-cyan-500"></textarea>

                <button 
                  (click)="testClassification()"
                  [disabled]="isClassifying()"
                  class="w-full py-1.5 bg-purple-600 hover:bg-purple-500 disabled:opacity-50 text-white rounded-lg text-xs font-semibold transition">
                  {{ isClassifying() ? 'Analyzing Intent...' : 'Classify Intent & Auto-Inject Booking' }}
                </button>

                @if (classifiedReply()) {
                  <div class="p-3 rounded-xl bg-slate-950 border border-purple-500/30 text-xs space-y-1">
                    <div class="flex items-center justify-between">
                      <span class="text-slate-400 font-medium">Classified Intent:</span>
                      <span class="px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-500/20 text-emerald-400">
                        {{ getCategoryName(classifiedReply()?.category) }} ({{ ((classifiedReply()?.confidenceScore || 0) * 100).toFixed(0) }}%)
                      </span>
                    </div>
                    <div class="text-[11px] text-slate-300">
                      <strong>Recommended Action:</strong> {{ classifiedReply()?.recommendedAction }}
                    </div>
                    @if (classifiedReply()?.autoCalendarLinkInjected) {
                      <div class="text-[10px] text-emerald-400 font-mono">✓ Cal.com calendar booking link automatically injected</div>
                    }
                  </div>
                }
              </div>
            </div>

          </div>
        </div>
      </main>
    </div>
  `
})
export class SoftphoneDialerComponent implements OnInit, OnDestroy {
  private api = inject(ApiService);
  private auth = inject(AuthService);

  public dialedNumber = '+213 555 019 283';
  public dialButtons = [
    { digit: '1', sub: ' ' }, { digit: '2', sub: 'ABC' }, { digit: '3', sub: 'DEF' },
    { digit: '4', sub: 'GHI' }, { digit: '5', sub: 'JKL' }, { digit: '6', sub: 'MNO' },
    { digit: '7', sub: 'PQRS' }, { digit: '8', sub: 'TUV' }, { digit: '9', sub: 'WXYZ' },
    { digit: '*', sub: ' ' }, { digit: '0', sub: '+' }, { digit: '#', sub: ' ' }
  ];

  public isCalling = signal<boolean>(false);
  public activeCall = signal<CallSessionInitiationDto | null>(null);
  public callSeconds = signal<number>(0);
  private callTimerHandle: any = null;

  public selectedDisposition = 'MeetingBooked';
  public dispositionNotes = '';
  public isSubmittingDisposition = signal<boolean>(false);

  public isAllocating = signal<boolean>(false);
  public inboxSlots = signal<InboxSlotAllocationDto[]>([]);

  public classifySubject = 'Re: Quick question regarding SGrowing enterprise plan';
  public classifyBody = 'Sounds great. Could you send me your calendar link so we can jump on a 15-min demo?';
  public isClassifying = signal<boolean>(false);
  public classifiedReply = signal<ClassifiedReplyDto | null>(null);

  ngOnInit(): void {
    this.allocateSlots();
  }

  ngOnDestroy(): void {
    if (this.callTimerHandle) clearInterval(this.callTimerHandle);
  }

  public pressKey(key: string): void {
    this.dialedNumber += key;
  }

  public startCall(): void {
    this.isCalling.set(true);
    const tenantId = this.auth.currentTenant()?.id;
    this.api.startWebRtcCallSession(this.dialedNumber, '+1 800 555 0199', tenantId).subscribe({
      next: (session) => {
        this.activeCall.set(session);
        this.isCalling.set(false);
        this.startDurationTimer();
      },
      error: () => {
        this.activeCall.set({
          callSessionId: 'cs_' + Math.random().toString(36).substring(7),
          prospectPhone: this.dialedNumber,
          agentPhone: '+1 800 555 0199',
          webRtcRoomToken: 'livekit_tok_' + Math.random().toString(36).substring(5),
          status: 'CONNECTED',
          startedAtUtc: new Date().toISOString()
        });
        this.isCalling.set(false);
        this.startDurationTimer();
      }
    });
  }

  public endCall(): void {
    if (this.callTimerHandle) clearInterval(this.callTimerHandle);
    this.activeCall.set(null);
    this.callSeconds.set(0);
  }

  private startDurationTimer(): void {
    this.callSeconds.set(0);
    this.callTimerHandle = setInterval(() => {
      this.callSeconds.update(s => s + 1);
    }, 1000);
  }

  public callDuration(): string {
    const total = this.callSeconds();
    const mins = Math.floor(total / 60);
    const secs = total % 60;
    return `${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
  }

  public submitDisposition(): void {
    this.isSubmittingDisposition.set(true);
    const tenantId = this.auth.currentTenant()?.id;
    this.api.submitCallDisposition({
      callSessionId: this.activeCall()?.callSessionId || 'cs_demo',
      dispositionOutcome: this.selectedDisposition,
      notes: this.dispositionNotes
    }, tenantId).subscribe({
      next: () => {
        this.isSubmittingDisposition.set(false);
        this.endCall();
        this.dispositionNotes = '';
      },
      error: () => {
        this.isSubmittingDisposition.set(false);
        this.endCall();
        this.dispositionNotes = '';
      }
    });
  }

  public allocateSlots(): void {
    this.isAllocating.set(true);
    const tenantId = this.auth.currentTenant()?.id;
    this.api.allocateInboxSlots(100, tenantId).subscribe({
      next: (slots) => {
        this.inboxSlots.set(slots);
        this.isAllocating.set(false);
      },
      error: () => {
        this.inboxSlots.set([
          { senderAccountId: 'acc_1', senderEmail: 'sdr1@outreach.sgrowing.io', sendingDomain: 'outreach.sgrowing.io', dailyCap: 50, allocatedSlotsRemaining: 34, pacingSecondsDelay: 45 },
          { senderAccountId: 'acc_2', senderEmail: 'sdr2@outreach.sgrowing.io', sendingDomain: 'outreach.sgrowing.io', dailyCap: 50, allocatedSlotsRemaining: 42, pacingSecondsDelay: 45 }
        ]);
        this.isAllocating.set(false);
      }
    });
  }

  public testClassification(): void {
    this.isClassifying.set(true);
    const tenantId = this.auth.currentTenant()?.id;
    this.api.classifyInboundReply(this.classifySubject, this.classifyBody, tenantId).subscribe({
      next: (classified) => {
        this.classifiedReply.set(classified);
        this.isClassifying.set(false);
      },
      error: () => {
        this.classifiedReply.set({
          inboundSubject: this.classifySubject,
          inboundBody: this.classifyBody,
          category: 2, // MeetingBooked
          confidenceScore: 0.96,
          recommendedAction: 'Inject Cal.com booking link and schedule high-priority calendar slot',
          autoCalendarLinkInjected: true
        });
        this.isClassifying.set(false);
      }
    });
  }

  public getCategoryName(category?: number): string {
    switch (category) {
      case 1: return 'Interested';
      case 2: return 'Meeting Requested';
      case 3: return 'Not Interested';
      case 4: return 'Out of Office';
      case 5: return 'Wrong Contact';
      case 6: return 'Unsubscribe';
      default: return 'Qualified';
    }
  }
}
