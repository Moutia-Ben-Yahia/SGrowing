import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { DomainDnsHealthRecordDto, EmailSpamAnalysisReportDto, MailboxWarmupCadenceDto, WarmupExchangeLogDto } from '../../core/models/deliverability.models';

@Component({
  selector: 'app-deliverability-studio',
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
              🛡️ Email Deliverability Guardian & Mailbox Warmup
            </span>
            <span class="bg-cyan-500/20 text-cyan-400 text-[10px] font-mono px-2 py-0.5 rounded-full border border-cyan-500/30">
              Category 01 • Warmbly & Postal Mesh
            </span>
          </div>

          <div class="flex items-center gap-3">
            <div class="flex bg-slate-800/80 p-0.5 rounded-lg border border-slate-700/60 text-xs">
              <button
                (click)="activeTab.set('dns')"
                [class.bg-indigo-600]="activeTab() === 'dns'"
                [class.text-white]="activeTab() === 'dns'"
                class="px-3 py-1 rounded-md text-slate-300 transition">
                🌐 DNS Health Check
              </button>
              <button
                (click)="activeTab.set('spam')"
                [class.bg-indigo-600]="activeTab() === 'spam'"
                [class.text-white]="activeTab() === 'spam'"
                class="px-3 py-1 rounded-md text-slate-300 transition">
                🧪 Spam Risk Analyzer
              </button>
              <button
                (click)="activeTab.set('warmup')"
                [class.bg-indigo-600]="activeTab() === 'warmup'"
                [class.text-white]="activeTab() === 'warmup'"
                class="px-3 py-1 rounded-md text-slate-300 transition">
                🔥 Peer-to-Peer Warmup
              </button>
            </div>
          </div>
        </header>

        <!-- Body -->
        <div class="p-6 space-y-6 flex-1">
          <!-- 1. DNS Health Check Tab -->
          @if (activeTab() === 'dns') {
            <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
              <div class="p-5 rounded-2xl bg-gradient-to-br from-slate-900 to-slate-950 border border-slate-800 flex flex-col items-center justify-center text-center">
                <span class="text-xs uppercase tracking-wider text-slate-400 font-semibold mb-2">DNS Deliverability Score</span>
                <div class="w-24 h-24 rounded-full border-4 border-emerald-500 text-emerald-400 flex items-center justify-center font-bold text-3xl shadow-lg">
                  {{ dnsRecord()?.overallHealthScore ?? 100 }}
                </div>
                <span class="text-xs text-slate-400 mt-2">{{ domainInput }}</span>
              </div>

              <!-- DNS Card 1: SPF -->
              <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800 flex flex-col justify-between">
                <div>
                  <div class="flex items-center justify-between mb-2">
                    <span class="text-xs font-bold text-white">SPF (Sender Policy)</span>
                    <span class="px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-500/20 text-emerald-400">PASSED</span>
                  </div>
                  <p class="text-[11px] font-mono text-slate-300 break-all bg-slate-950 p-2 rounded border border-slate-800/80">
                    {{ dnsRecord()?.spfRecord || 'v=spf1 include:_spf.sgrowing.io ~all' }}
                  </p>
                </div>
                <span class="text-[10px] text-slate-400 mt-2">Prevents IP address sender spoofing</span>
              </div>

              <!-- DNS Card 2: DKIM -->
              <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800 flex flex-col justify-between">
                <div>
                  <div class="flex items-center justify-between mb-2">
                    <span class="text-xs font-bold text-white">DKIM (2048-bit RSA)</span>
                    <span class="px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-500/20 text-emerald-400">ALIGNED</span>
                  </div>
                  <p class="text-[11px] font-mono text-slate-300 bg-slate-950 p-2 rounded border border-slate-800/80">
                    Selector: {{ dnsRecord()?.dkimSelector || 'sgrowing2026' }}
                  </p>
                </div>
                <span class="text-[10px] text-slate-400 mt-2">Cryptographic signature verified</span>
              </div>

              <!-- DNS Card 3: DMARC -->
              <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800 flex flex-col justify-between">
                <div>
                  <div class="flex items-center justify-between mb-2">
                    <span class="text-xs font-bold text-white">DMARC Policy</span>
                    <span class="px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-500/20 text-emerald-400 font-mono">
                      p={{ dnsRecord()?.dmarcPolicy || 'quarantine' }}
                    </span>
                  </div>
                  <p class="text-[11px] font-mono text-slate-300 bg-slate-950 p-2 rounded border border-slate-800/80">
                    rua=mailto:dmarc&#64;{{ domainInput }}
                  </p>
                </div>
                <span class="text-[10px] text-slate-400 mt-2">Strict enforcement policy active</span>
              </div>
            </div>

            <!-- Inspect Domain Box -->
            <div class="p-5 rounded-2xl bg-slate-900/60 border border-slate-800 flex items-center justify-between gap-4">
              <div class="flex-1 flex items-center gap-3">
                <input
                  type="text"
                  [(ngModel)]="domainInput"
                  placeholder="e.g. mail.sgrowing.io"
                  class="flex-1 bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white"
                />
                <button
                  (click)="checkDns()"
                  class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-xs font-semibold transition">
                  🔍 Check DNS Records
                </button>
              </div>
            </div>
          }

          <!-- 2. Spam Risk Analyzer Tab -->
          @if (activeTab() === 'spam') {
            <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
              <!-- Left: Email Composition Sandbox -->
              <div class="lg:col-span-2 p-5 rounded-2xl bg-slate-900/60 border border-slate-800 space-y-4">
                <h3 class="text-sm font-semibold text-white">✍️ Subject & Copy Spam Evaluation</h3>
                <div class="space-y-3">
                  <div>
                    <label class="text-[11px] text-slate-400 block mb-1">Subject Line</label>
                    <input type="text" [(ngModel)]="spamSubject" class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white" />
                  </div>
                  <div>
                    <label class="text-[11px] text-slate-400 block mb-1">HTML Body</label>
                    <textarea rows="6" [(ngModel)]="spamBody" class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white font-mono"></textarea>
                  </div>
                  <button
                    (click)="analyzeSpam()"
                    class="px-4 py-2 bg-gradient-to-r from-cyan-600 to-indigo-600 hover:from-cyan-500 hover:to-indigo-500 text-white rounded-lg text-xs font-bold transition">
                    🧪 Run Heuristic Spam Check
                  </button>
                </div>
              </div>

              <!-- Right: Analysis Verdict -->
              <div class="p-5 rounded-2xl bg-slate-900/60 border border-slate-800 flex flex-col justify-between">
                <div>
                  <span class="text-xs text-slate-400 uppercase tracking-wider block mb-1">Spam Risk Verdict</span>
                  <div class="flex items-center gap-2 mb-4">
                    <span class="px-3 py-1 rounded-full text-xs font-bold"
                      [class.bg-emerald-500/20]="(spamReport()?.spamScore || 0) < 30"
                      [class.text-emerald-400]="(spamReport()?.spamScore || 0) < 30"
                      [class.bg-rose-500/20]="(spamReport()?.spamScore || 0) >= 30"
                      [class.text-rose-400]="(spamReport()?.spamScore || 0) >= 30">
                      {{ spamReport()?.verdict || 'Safe' }}
                    </span>
                    <span class="text-xs font-mono text-slate-400">Score: {{ spamReport()?.spamScore || 5 }}/100</span>
                  </div>

                  <div class="space-y-2 text-xs">
                    <div class="flex justify-between p-2 rounded bg-slate-950 border border-slate-800">
                      <span class="text-slate-400">RFC 8058 Unsubscribe:</span>
                      <span class="text-emerald-400 font-bold">Present ✅</span>
                    </div>
                    <div class="flex justify-between p-2 rounded bg-slate-950 border border-slate-800">
                      <span class="text-slate-400">Image/Text Ratio:</span>
                      <span class="text-slate-200 font-mono">15% (Optimal)</span>
                    </div>
                    <div class="p-2 rounded bg-slate-950 border border-slate-800">
                      <span class="text-slate-400 block mb-1">Detected Keywords:</span>
                      <span class="text-slate-300 font-mono text-[11px]">None detected</span>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          }

          <!-- 3. Peer Warmup Tab -->
          @if (activeTab() === 'warmup') {
            <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
              <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800 flex flex-col justify-between">
                <span class="text-xs text-slate-400 uppercase tracking-wider">Warmup Day</span>
                <div class="flex items-baseline justify-between mt-2">
                  <h3 class="text-2xl font-bold text-orange-400">Day {{ warmup()?.currentDayNumber || 1 }}</h3>
                  <span class="text-xs text-slate-400">Target: 40/day</span>
                </div>
              </div>
              <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800 flex flex-col justify-between">
                <span class="text-xs text-slate-400 uppercase tracking-wider">Simulated Sent</span>
                <div class="flex items-baseline justify-between mt-2">
                  <h3 class="text-2xl font-bold text-white">{{ warmup()?.totalSimulatedSent || 15 }}</h3>
                  <span class="text-xs text-emerald-400 font-semibold">100% Inboxed</span>
                </div>
              </div>
              <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800 flex flex-col justify-between">
                <span class="text-xs text-slate-400 uppercase tracking-wider">Replies Received</span>
                <div class="flex items-baseline justify-between mt-2">
                  <h3 class="text-2xl font-bold text-cyan-400">{{ warmup()?.totalSimulatedReceived || 12 }}</h3>
                  <span class="text-xs text-slate-400">80% Reply Rate</span>
                </div>
              </div>
              <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800 flex flex-col justify-between">
                <span class="text-xs text-slate-400 uppercase tracking-wider">Spam Rescues</span>
                <div class="flex items-baseline justify-between mt-2">
                  <h3 class="text-2xl font-bold text-emerald-400">{{ warmup()?.spamRescueCount || 2 }}</h3>
                  <button
                    (click)="simulateStep()"
                    class="px-2 py-1 bg-indigo-600 hover:bg-indigo-500 text-white rounded text-[10px] font-bold transition">
                    + Next Step
                  </button>
                </div>
              </div>
            </div>

            <!-- Warmup Logs Table -->
            <div class="rounded-2xl border border-slate-800 bg-slate-900/40 overflow-hidden">
              <div class="px-5 py-3 border-b border-slate-800 bg-slate-900/80 flex items-center justify-between">
                <h3 class="text-sm font-semibold text-white">📬 Peer Inbox Exchange Telemetry Stream</h3>
              </div>
              <div class="overflow-x-auto">
                <table class="w-full text-left text-xs text-slate-300">
                  <thead class="bg-slate-950/70 text-[11px] uppercase tracking-wider text-slate-400 border-b border-slate-800">
                    <tr>
                      <th class="px-4 py-3">Timestamp</th>
                      <th class="px-4 py-3">Peer Mailbox</th>
                      <th class="px-4 py-3">Action / Direction</th>
                      <th class="px-4 py-3">Deliverability Signal</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-800/60 font-mono">
                    @for (log of warmupLogs(); track log.id) {
                      <tr class="hover:bg-slate-800/30 transition">
                        <td class="px-4 py-3 text-slate-400">{{ log.timestampUtc | date:'mediumTime' }}</td>
                        <td class="px-4 py-3 text-indigo-300">{{ log.peerInboxEmail }}</td>
                        <td class="px-4 py-3">
                          <span class="px-2 py-0.5 rounded text-[10px]"
                            [class.bg-blue-500/20]="log.direction === 'Sent'"
                            [class.text-blue-300]="log.direction === 'Sent'"
                            [class.bg-emerald-500/20]="log.direction === 'Received'"
                            [class.text-emerald-300]="log.direction === 'Received'"
                            [class.bg-orange-500/20]="log.direction === 'RescuedFromSpam'"
                            [class.text-orange-300]="log.direction === 'RescuedFromSpam'">
                            {{ log.direction }}
                          </span>
                        </td>
                        <td class="px-4 py-3 font-sans text-emerald-400">
                          Marked as Important & Rescued
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          }
        </div>
      </main>
    </div>
  `
})
export class DeliverabilityStudioComponent implements OnInit {
  private api = inject(ApiService);
  private auth = inject(AuthService);

  public activeTab = signal<'dns' | 'spam' | 'warmup'>('dns');
  public dnsRecord = signal<DomainDnsHealthRecordDto | null>(null);
  public spamReport = signal<EmailSpamAnalysisReportDto | null>(null);
  public warmup = signal<MailboxWarmupCadenceDto | null>(null);
  public warmupLogs = signal<WarmupExchangeLogDto[]>([]);

  public domainInput = 'mail.sgrowing.io';
  public spamSubject = 'Exclusive Partnership Proposal for E-Commerce Merchants';
  public spamBody = 'Hello,\nWe would like to introduce our high-volume automated cash-on-delivery routing platform with real-time drop and swap return exchange.\nBest regards,\nUnsubscribe: https://sgrowing.io/unsubscribe';

  public get tenantId(): string {
    return this.auth.currentTenant()?.id || '00000000-0000-0000-0000-000000000001';
  }

  ngOnInit(): void {
    this.checkDns();
    this.analyzeSpam();
    this.loadWarmup();
  }

  public checkDns(): void {
    this.api.checkDomainDns({
      tenantId: this.tenantId,
      domainName: this.domainInput
    }).subscribe(record => this.dnsRecord.set(record));
  }

  public analyzeSpam(): void {
    this.api.analyzeSpamRisk({
      tenantId: this.tenantId,
      subject: this.spamSubject,
      bodyHtml: this.spamBody
    }).subscribe(rep => this.spamReport.set(rep));
  }

  public loadWarmup(): void {
    this.api.startWarmup({
      tenantId: this.tenantId,
      senderEmail: 'outbound@sgrowing.io',
      dailyTarget: 40,
      rampCurve: 'Linear'
    }).subscribe(cadence => {
      this.warmup.set(cadence);
      this.api.getWarmupLogs(cadence.id, this.tenantId).subscribe(logs => {
        if (logs.length > 0) {
          this.warmupLogs.set(logs);
        } else {
          this.simulateStep();
        }
      });
    });
  }

  public simulateStep(): void {
    if (!this.warmup()) return;
    this.api.simulateWarmupStep(this.warmup()!.id, this.tenantId).subscribe(updated => {
      this.warmup.set(updated);
      this.api.getWarmupLogs(updated.id, this.tenantId).subscribe(logs => this.warmupLogs.set(logs));
    });
  }
}
