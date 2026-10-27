import { Component, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { WarmupDialogueThread, SeedPlacementReportDto } from '../../core/models/pillars.models';

@Component({
  selector: 'app-warmup-swarm-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Topbar -->
        <header class="h-14 border-b border-slate-800 bg-slate-900/60 px-6 flex items-center justify-between shrink-0">
          <div class="flex items-center gap-3">
            <a routerLink="/deliverability" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← Deliverability Studio
            </a>
            <span class="text-slate-700">|</span>
            <span class="text-base font-semibold text-white flex items-center gap-2">
              🔥 Warmup Swarm & ESP Seed Placement Radar
            </span>
            <span class="bg-amber-500/20 text-amber-400 text-[10px] font-mono px-2 py-0.5 rounded-full border border-amber-500/30">
              Multi-Turn Threads • Seed Analyzer
            </span>
          </div>

          <div class="flex items-center gap-3">
            <button
              (click)="activeTab.set('threads')"
              [class]="activeTab() === 'threads' ? 'bg-amber-600 text-white' : 'bg-slate-800 text-slate-300 hover:bg-slate-700'"
              class="px-3 py-1.5 rounded-lg text-xs font-semibold transition">
              Conversational Threads
            </button>
            <button
              (click)="activeTab.set('placement')"
              [class]="activeTab() === 'placement' ? 'bg-amber-600 text-white' : 'bg-slate-800 text-slate-300 hover:bg-slate-700'"
              class="px-3 py-1.5 rounded-lg text-xs font-semibold transition">
              ESP Placement Radar
            </button>
          </div>
        </header>

        <!-- Body -->
        <div class="p-6 space-y-6 flex-1">
          <!-- TAB 1: THREADS -->
          @if (activeTab() === 'threads') {
            <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
              <!-- Controls -->
              <div class="p-5 rounded-xl bg-slate-900/60 border border-slate-800 space-y-4">
                <h3 class="text-sm font-semibold text-white flex items-center gap-2">
                  <span>💬</span> Generate Dialogue Thread
                </h3>

                <div>
                  <label class="text-[11px] text-slate-400 block mb-1">Sender Mailbox</label>
                  <input type="email" [(ngModel)]="threadSender" class="w-full bg-slate-950 border border-slate-800 rounded-lg p-2 text-xs text-slate-200" />
                </div>
                <div>
                  <label class="text-[11px] text-slate-400 block mb-1">Swarm Peer Mailbox</label>
                  <input type="email" [(ngModel)]="threadPeer" class="w-full bg-slate-950 border border-slate-800 rounded-lg p-2 text-xs text-slate-200" />
                </div>
                <div>
                  <label class="text-[11px] text-slate-400 block mb-1">Contextual Dialogue Topic</label>
                  <select [(ngModel)]="threadTopic" class="w-full bg-slate-950 border border-slate-800 rounded-lg p-2 text-xs text-slate-200">
                    <option [value]="0">Product API Limits Inquiry</option>
                    <option [value]="1">Partnership & Co-Marketing</option>
                    <option [value]="2">Meeting Follow-Up</option>
                    <option [value]="4">Invoice & Billing Clarification</option>
                  </select>
                </div>
                <div>
                  <label class="text-[11px] text-slate-400 block mb-1">Turn Depth</label>
                  <input type="number" [(ngModel)]="turnCount" min="2" max="6" class="w-full bg-slate-950 border border-slate-800 rounded-lg p-2 text-xs text-slate-200" />
                </div>

                <button
                  (click)="generateThread()"
                  [disabled]="isGeneratingThread()"
                  class="w-full py-2 bg-amber-600 hover:bg-amber-500 disabled:opacity-50 text-white rounded-lg text-xs font-semibold transition">
                  {{ isGeneratingThread() ? 'Generating...' : 'Simulate Thread Exchange' }}
                </button>
              </div>

              <!-- Thread Preview -->
              <div class="lg:col-span-2 p-5 rounded-xl bg-slate-900/60 border border-slate-800 space-y-4">
                <div class="flex items-center justify-between">
                  <h3 class="text-sm font-semibold text-white">Synthesized Conversation Transcript</h3>
                  @if (dialogueThread()) {
                    <span class="text-xs text-slate-400 font-mono">
                      Subject: {{ dialogueThread()?.threadSubject }}
                    </span>
                  }
                </div>

                @if (dialogueThread()) {
                  <div class="space-y-3">
                    @for (turn of dialogueThread()?.turns; track turn.turnIndex) {
                      <div class="p-4 rounded-xl bg-slate-950 border border-slate-800 space-y-2">
                        <div class="flex items-center justify-between text-xs">
                          <span class="font-semibold text-amber-400">Turn #{{ turn.turnIndex }}: {{ turn.senderEmail }} → {{ turn.recipientEmail }}</span>
                          <div class="flex gap-1.5">
                            @if (turn.markAsImportant) {
                              <span class="px-2 py-0.5 rounded text-[10px] bg-amber-500/10 text-amber-300 border border-amber-500/20">Mark Important</span>
                            }
                            @if (turn.starMessage) {
                              <span class="px-2 py-0.5 rounded text-[10px] bg-yellow-500/10 text-yellow-300 border border-yellow-500/20">★ Starred</span>
                            }
                            @if (turn.rescueFromSpam) {
                              <span class="px-2 py-0.5 rounded text-[10px] bg-rose-500/10 text-rose-300 border border-rose-500/20">Spam Rescued</span>
                            }
                          </div>
                        </div>

                        <div class="text-[11px] text-slate-500 font-mono truncate">
                          Message-ID: {{ turn.messageId }}
                          @if (turn.inReplyTo) {
                            <span class="block">In-Reply-To: {{ turn.inReplyTo }}</span>
                          }
                        </div>

                        <p class="text-xs text-slate-200 whitespace-pre-line leading-relaxed font-sans pt-1">
                          {{ turn.body }}
                        </p>
                      </div>
                    }
                  </div>
                } @else {
                  <div class="h-64 flex flex-col items-center justify-center text-slate-500 text-xs gap-2">
                    <span>💡</span> Click Simulate to generate multi-turn contextual dialogues with authentic RFC headers.
                  </div>
                }
              </div>
            </div>
          }

          <!-- TAB 2: PLACEMENT RADAR -->
          @if (activeTab() === 'placement') {
            <div class="space-y-6">
              <div class="p-5 rounded-xl bg-slate-900/60 border border-slate-800 flex items-center justify-between">
                <div>
                  <h3 class="text-sm font-semibold text-white">Automated Seed Placement Test</h3>
                  <p class="text-xs text-slate-400 mt-1">Dispatches to 20 seed accounts across Google Workspace, Microsoft 365, Yahoo, Zoho, and Proton.</p>
                </div>
                <button
                  (click)="runPlacementTest()"
                  [disabled]="isRunningPlacement()"
                  class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 text-white rounded-lg text-xs font-semibold transition">
                  {{ isRunningPlacement() ? 'Testing Seeds...' : 'Execute Placement Test' }}
                </button>
              </div>

              @if (placementReport()) {
                <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
                  <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800">
                    <span class="text-xs text-slate-400 block uppercase">Overall Inbox Rate</span>
                    <span class="text-3xl font-bold font-mono text-emerald-400 mt-2 block">{{ placementReport()?.overallInboxPlacementPercentage }}%</span>
                  </div>
                  <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800">
                    <span class="text-xs text-slate-400 block uppercase">Spam Folder Rate</span>
                    <span class="text-3xl font-bold font-mono text-rose-400 mt-2 block">{{ placementReport()?.overallSpamPercentage }}%</span>
                  </div>
                  <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800">
                    <span class="text-xs text-slate-400 block uppercase">Total Seeds Audited</span>
                    <span class="text-3xl font-bold font-mono text-white mt-2 block">{{ placementReport()?.totalSeedsTested }}</span>
                  </div>
                </div>

                <!-- ESP Breakdown Grid -->
                <div class="p-5 rounded-xl bg-slate-900/60 border border-slate-800 space-y-4">
                  <h4 class="text-xs font-semibold uppercase tracking-wider text-slate-400">ESP Provider Inboxing Breakdown</h4>
                  <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-3">
                    @for (esp of placementReport()?.espBreakdowns; track esp.esp) {
                      <div class="p-3.5 rounded-lg bg-slate-950 border border-slate-800 space-y-2">
                        <div class="text-xs font-bold text-white">{{ getEspName(esp.esp) }}</div>
                        <div class="text-xl font-bold font-mono text-emerald-400">{{ esp.inboxPlacementPercentage }}%</div>
                        <div class="text-[11px] text-slate-400 space-y-0.5">
                          <div>Inbox: {{ esp.inboxCount }} / {{ esp.totalSeeds }}</div>
                          <div>Spam: {{ esp.spamCount }}</div>
                          <div>Promo: {{ esp.promotionsCount }}</div>
                        </div>
                      </div>
                    }
                  </div>
                </div>

                <!-- Inboxing Recommendations -->
                <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800 space-y-2">
                  <span class="text-xs font-semibold text-indigo-400 uppercase tracking-wider">Automated Recommendations:</span>
                  <ul class="list-disc list-inside text-xs text-slate-300 space-y-1">
                    @for (rec of placementReport()?.inboxingRecommendations; track rec) {
                      <li>{{ rec }}</li>
                    }
                  </ul>
                </div>
              }
            </div>
          }
        </div>
      </main>
    </div>
  `
})
export class WarmupSwarmCockpitComponent {
  private api = inject(ApiService);
  private auth = inject(AuthService);

  public activeTab = signal<'threads' | 'placement'>('threads');

  // Threads
  public threadSender = 'outreach@growth-corp.com';
  public threadPeer = 'warmup-partner@peer-swarm.org';
  public threadTopic = 0;
  public turnCount = 4;
  public isGeneratingThread = signal(false);
  public dialogueThread = signal<WarmupDialogueThread | null>(null);

  // Placement
  public isRunningPlacement = signal(false);
  public placementReport = signal<SeedPlacementReportDto | null>(null);

  public generateThread() {
    this.isGeneratingThread.set(true);
    this.api.generateWarmupThread({
      senderEmail: this.threadSender,
      peerEmail: this.threadPeer,
      topic: Number(this.threadTopic),
      turnsCount: Number(this.turnCount)
    }).subscribe({
      next: res => {
        this.dialogueThread.set(res);
        this.isGeneratingThread.set(false);
      },
      error: () => this.isGeneratingThread.set(false)
    });
  }

  public runPlacementTest() {
    this.isRunningPlacement.set(true);
    const tenantId = this.auth.currentUser()?.tenantId || '00000000-0000-0000-0000-000000000000';
    this.api.runPlacementTest({
      tenantId,
      senderEmail: this.threadSender,
      subject: 'Follow-up regarding scheduled pipeline review',
      bodyHtml: '<p>Hi team, checking in on the agreed timelines for our enterprise deployment.</p>'
    }).subscribe({
      next: res => {
        this.placementReport.set(res);
        this.isRunningPlacement.set(false);
      },
      error: () => this.isRunningPlacement.set(false)
    });
  }

  public getEspName(esp: string | number): string {
    const val = String(esp);
    if (val === '0' || val.includes('Google')) return 'Google Workspace';
    if (val === '1' || val.includes('Microsoft')) return 'Microsoft 365';
    if (val === '2' || val.includes('Yahoo')) return 'Yahoo / AOL';
    if (val === '3' || val.includes('Zoho')) return 'Zoho Mail';
    return 'Proton / Apple';
  }
}
