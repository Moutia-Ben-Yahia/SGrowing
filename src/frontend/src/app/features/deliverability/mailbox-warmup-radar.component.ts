import { Component, inject, signal, computed, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslationService } from '../../core/services/translation.service';

export interface WarmupAccountItem {
  id: string;
  email: string;
  smtpHost: string;
  dailySent: number;
  dailyTarget: number;
  rampDay: number;
  reputation: number;
  openRate: number;
  spamRescue: number;
  status: 'Healthy' | 'WarmingUp' | 'Throttled' | 'Degraded';
}

export interface DnsCheckItem {
  type: string;
  host: string;
  badge: 'PASS' | 'WARN' | 'FAIL';
  detail: string;
}

@Component({
  selector: 'app-mailbox-warmup-radar',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" class="p-6 max-w-7xl mx-auto space-y-6 text-slate-100 font-sans">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-5">
        <div>
          <div class="flex items-center gap-2">
            <span class="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">
              Horizon 16 • Domain D2 & D3
            </span>
            <span class="text-xs text-slate-400">P2P Warmup Swarm & DNS Guardian</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white mt-1">
            Mailbox Warmup Swarm & Deliverability Radar
          </h1>
          <p class="text-sm text-slate-400">
            Coordinated peer-to-peer synthetic inbox engagement and automated SPF, DKIM, DMARC, and BIMI deliverability health.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button (click)="triggerP2PExchange()"
                  class="px-3.5 py-2 rounded-xl bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-semibold shadow-lg shadow-emerald-600/30 transition">
            Simulate P2P Exchange 🚀
          </button>
        </div>
      </div>

      <!-- Top Metrics -->
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div class="bg-slate-900/60 border border-slate-800 p-4 rounded-2xl">
          <div class="text-xs text-slate-400 font-medium">Average Sender Score</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">{{ averageReputation() }}/100</div>
          <div class="text-xs text-emerald-400 mt-0.5">Top-Tier Inbox Placement</div>
        </div>

        <div class="bg-slate-900/60 border border-slate-800 p-4 rounded-2xl">
          <div class="text-xs text-slate-400 font-medium">Daily Warmup Volume</div>
          <div class="text-2xl font-bold text-indigo-400 mt-1">{{ totalDailySent() }} / {{ totalDailyTarget() }}</div>
          <div class="text-xs text-slate-500 mt-0.5">Across Enrolled Mailboxes</div>
        </div>

        <div class="bg-slate-900/60 border border-slate-800 p-4 rounded-2xl">
          <div class="text-xs text-slate-400 font-medium">Simulated Open Rate</div>
          <div class="text-2xl font-bold text-white mt-1">{{ averageOpenRate() }}%</div>
          <div class="text-xs text-slate-500 mt-0.5">Synthetic Engagement Swarm</div>
        </div>

        <div class="bg-slate-900/60 border border-slate-800 p-4 rounded-2xl">
          <div class="text-xs text-slate-400 font-medium">DNS Authentication</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">100% Pass</div>
          <div class="text-xs text-slate-500 mt-0.5">SPF • DKIM • DMARC • BIMI</div>
        </div>
      </div>

      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Mailbox Accounts List (2 Cols) -->
        <div class="lg:col-span-2 space-y-4">
          <div class="flex items-center justify-between">
            <h2 class="text-sm font-semibold text-slate-300 uppercase tracking-wider">Sending Mailboxes</h2>
            <span class="text-xs text-slate-500">{{ accounts().length }} Accounts Enrolled</span>
          </div>

          <div class="space-y-3">
            @for (acc of accounts(); track acc.id) {
              <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-4 flex flex-col md:flex-row md:items-center justify-between gap-4">
                <div class="space-y-1">
                  <div class="flex items-center gap-2">
                    <span class="font-semibold text-sm text-white">{{ acc.email }}</span>
                    <span class="text-xs px-2 py-0.5 rounded-full font-medium"
                          [ngClass]="acc.status === 'Healthy' ? 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/30' : 'bg-amber-500/20 text-amber-300 border border-amber-500/30'">
                      {{ acc.status }}
                    </span>
                  </div>
                  <div class="text-xs text-slate-400 flex items-center gap-3">
                    <span>Host: {{ acc.smtpHost }}</span>
                    <span>•</span>
                    <span>Ramp Day {{ acc.rampDay }}</span>
                    <span>•</span>
                    <span>Spam Rescue: {{ acc.spamRescue }}%</span>
                  </div>
                </div>

                <div class="flex items-center gap-4">
                  <div class="text-right">
                    <div class="text-xs text-slate-400">Reputation</div>
                    <div class="text-base font-bold" [ngClass]="acc.reputation >= 85 ? 'text-emerald-400' : 'text-amber-400'">
                      {{ acc.reputation }}
                    </div>
                  </div>

                  <div class="w-24 bg-slate-950 rounded-full h-2">
                    <div class="h-2 rounded-full bg-emerald-500" [style.width.%]="(acc.dailySent / acc.dailyTarget) * 100"></div>
                  </div>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- DNS Directives Radar (1 Col) -->
        <div class="space-y-4">
          <h2 class="text-sm font-semibold text-slate-300 uppercase tracking-wider">DNS Deliverability Directives</h2>

          <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-4 space-y-3">
            @for (check of dnsRecords(); track check.type) {
              <div class="p-3 bg-slate-950/80 rounded-xl border border-slate-800/80 space-y-1">
                <div class="flex items-center justify-between text-xs">
                  <div class="font-bold text-white flex items-center gap-1.5">
                    <span>{{ check.type }}</span>
                    <span class="text-slate-500 text-xs font-mono font-normal">({{ check.host }})</span>
                  </div>
                  <span class="px-1.5 py-0.5 rounded text-[10px] font-bold bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">
                    {{ check.badge }}
                  </span>
                </div>
                <p class="text-xs text-slate-400">{{ check.detail }}</p>
              </div>
            }

            <div class="pt-2">
              <button (click)="runDnsAudit()"
                      class="w-full py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-semibold rounded-xl border border-slate-700 transition">
                Refresh DNS Directives 🔄
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  `
})
export class MailboxWarmupRadarComponent {
  translation: TranslationService;
  private cdr?: ChangeDetectorRef;

  accounts = signal<WarmupAccountItem[]>([
    {
      id: 'mbx_01',
      email: 'alex.mercer@sgrowing.io',
      smtpHost: 'smtp.mailgun.org',
      dailySent: 28,
      dailyTarget: 50,
      rampDay: 6,
      reputation: 92.5,
      openRate: 91.2,
      spamRescue: 96.0,
      status: 'Healthy'
    },
    {
      id: 'mbx_02',
      email: 'growth.sdr@sgrowing.io',
      smtpHost: 'smtp.sendgrid.net',
      dailySent: 15,
      dailyTarget: 40,
      rampDay: 3,
      reputation: 84.0,
      openRate: 86.5,
      spamRescue: 92.0,
      status: 'WarmingUp'
    }
  ]);

  dnsRecords = signal<DnsCheckItem[]>([
    { type: 'SPF', host: 'sgrowing.io', badge: 'PASS', detail: 'v=spf1 include:_spf.sgrowing.io ~all' },
    { type: 'DKIM', host: 'k1._domainkey.sgrowing.io', badge: 'PASS', detail: '2048-bit RSA key valid' },
    { type: 'DMARC', host: '_dmarc.sgrowing.io', badge: 'PASS', detail: 'p=reject strict enforcement policy' },
    { type: 'BIMI', host: 'default._bimi.sgrowing.io', badge: 'PASS', detail: 'SVG verified logo configured' }
  ]);

  averageReputation = computed(() => {
    const list = this.accounts();
    if (list.length === 0) return 0;
    const sum = list.reduce((s, a) => s + a.reputation, 0);
    return Math.round(sum / list.length);
  });

  totalDailySent = computed(() => this.accounts().reduce((s, a) => s + a.dailySent, 0));
  totalDailyTarget = computed(() => this.accounts().reduce((s, a) => s + a.dailyTarget, 0));

  averageOpenRate = computed(() => {
    const list = this.accounts();
    if (list.length === 0) return 0;
    const sum = list.reduce((s, a) => s + a.openRate, 0);
    return Math.round(sum / list.length);
  });

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

  triggerP2PExchange(): void {
    const updated = this.accounts().map(a => {
      const newSent = Math.min(a.dailyTarget, a.dailySent + 10);
      const newRep = Math.min(99.0, a.reputation + 1.5);
      return {
        ...a,
        dailySent: newSent,
        reputation: Number(newRep.toFixed(1)),
        status: (newRep >= 85 ? 'Healthy' : 'WarmingUp') as 'Healthy' | 'WarmingUp'
      };
    });

    this.accounts.set(updated);
    this.cdr?.markForCheck();
  }

  runDnsAudit(): void {
    this.dnsRecords.set([
      { type: 'SPF', host: 'sgrowing.io', badge: 'PASS', detail: 'Validated with 10 lookup limit adhered' },
      { type: 'DKIM', host: 'k1._domainkey.sgrowing.io', badge: 'PASS', detail: 'Cryptographic selector confirmed' },
      { type: 'DMARC', host: '_dmarc.sgrowing.io', badge: 'PASS', detail: 'Strict quarantine/reject posture active' },
      { type: 'BIMI', host: 'default._bimi.sgrowing.io', badge: 'PASS', detail: 'Verified Mark Certificate (VMC) active' }
    ]);
    this.cdr?.markForCheck();
  }
}
