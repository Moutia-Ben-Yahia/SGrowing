import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface CallAudit {
  auditId: string;
  callId: string;
  agentName: string;
  timestamp: string;
  durationSeconds: number;
  scriptAdherenceScore: number;
  piiRedactionVerified: boolean;
  mandatoryDisclosureRecited: boolean;
  status: 'PendingAudit' | 'CompliantPassed' | 'FlaggedForReview' | 'ViolationCritical';
  flaggedSnippets: string[];
}

@Component({
  selector: 'app-call-compliance-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen" dir="ltr" data-testid="call-compliance-cockpit">
      <header class="mb-6 flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-4">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-2.5 py-1 text-xs font-semibold rounded bg-rose-500/20 text-rose-400 border border-rose-500/30">Horizon 93</span>
            <span class="text-xs text-slate-400">Contact Center QA & Speech Auditing</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white mt-1">Autonomous Voice Agent QA & Call Compliance Scoring</h1>
          <p class="text-sm text-slate-400">Automated full-corpus speech auditing, PII scrubbing verification, and script adherence compliance.</p>
        </div>
      </header>

      <!-- KPI Summary -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-audited-calls">
          <div class="text-xs font-medium text-slate-400 uppercase">Total Audited Corpus</div>
          <div class="text-2xl font-bold text-white mt-1">{{ audits().length }} Calls</div>
          <div class="text-xs text-slate-400 mt-1">100% Speech-to-Text Coverage</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-average-adherence">
          <div class="text-xs font-medium text-slate-400 uppercase">Avg Script Adherence</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">{{ averageAdherence() | number:'1.1-1' }}%</div>
          <div class="text-xs text-slate-400 mt-1">Mandatory disclosures benchmark</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-pii-scrubbed">
          <div class="text-xs font-medium text-slate-400 uppercase">PII Redaction Rate</div>
          <div class="text-2xl font-bold text-cyan-400 mt-1">100% Verified</div>
          <div class="text-xs text-slate-400 mt-1">Zero PCI-DSS/HIPAA leaks in audio</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-flagged-calls">
          <div class="text-xs font-medium text-slate-400 uppercase">Flagged for Review</div>
          <div class="text-2xl font-bold text-rose-400 mt-1">{{ flaggedCount() }} Calls</div>
          <div class="text-xs text-slate-400 mt-1">Missing disclosures or low adherence</div>
        </div>
      </section>

      <!-- Audits Grid -->
      <section class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div class="lg:col-span-2 space-y-4">
          <h2 class="text-lg font-semibold text-white">Call Compliance Corpus</h2>
          <div class="space-y-3">
            @for (aud of audits(); track aud.auditId) {
              <div
                class="bg-slate-800/60 border border-slate-700 p-4 rounded-xl hover:border-slate-600 transition-all cursor-pointer"
                [class.ring-2]="selectedAudit()?.auditId === aud.auditId"
                [class.ring-rose-500]="selectedAudit()?.auditId === aud.auditId"
                (click)="selectAudit(aud)"
                data-testid="call-compliance-card"
              >
                <div class="flex items-center justify-between">
                  <div>
                    <h3 class="font-semibold text-white text-base">{{ aud.agentName }}</h3>
                    <p class="text-xs text-slate-400 mt-0.5">Call ID: {{ aud.callId }} &bull; Duration: {{ aud.durationSeconds }}s</p>
                  </div>
                  <div class="text-right">
                    <span class="text-xs px-2.5 py-1 rounded font-medium"
                      [class.bg-emerald-500/20]="aud.status === 'CompliantPassed'"
                      [class.text-emerald-400]="aud.status === 'CompliantPassed'"
                      [class.bg-rose-500/20]="aud.status === 'FlaggedForReview'"
                      [class.text-rose-400]="aud.status === 'FlaggedForReview'"
                    >
                      {{ aud.status }}
                    </span>
                    <div class="text-xs text-slate-300 mt-1">Adherence: {{ aud.scriptAdherenceScore }}%</div>
                  </div>
                </div>

                @if (aud.flaggedSnippets.length > 0) {
                  <div class="mt-3 p-2 bg-rose-500/10 border border-rose-500/20 rounded text-xs text-rose-300">
                    <strong>Flag:</strong> {{ aud.flaggedSnippets[0] }}
                  </div>
                }

                <div class="mt-4 flex items-center justify-between pt-2 border-t border-slate-700/60">
                  <span class="text-xs text-slate-400">PII Redaction: <strong class="text-emerald-400">Verified</strong></span>
                  @if (aud.status === 'FlaggedForReview') {
                    <button
                      (click)="approveCompliance(aud.auditId); $event.stopPropagation()"
                      data-testid="btn-approve-audit"
                      class="px-3 py-1 bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-semibold rounded shadow transition-colors"
                    >
                      Approve Override
                    </button>
                  } @else {
                    <span class="text-xs text-emerald-400 font-semibold" data-testid="compliance-passed-indicator">
                      QA Signed Off
                    </span>
                  }
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Detail Sidebar -->
        <div class="bg-slate-800/60 border border-slate-700 p-5 rounded-xl flex flex-col justify-between">
          <div>
            <h2 class="text-lg font-semibold text-white mb-3">Speech QA Telemetry</h2>
            @if (selectedAudit(); as sel) {
              <div class="space-y-4">
                <div class="p-3 bg-slate-900/60 rounded-lg border border-slate-700/60">
                  <div class="text-xs text-slate-400 uppercase">Selected Call Session</div>
                  <div class="text-base font-bold text-white mt-0.5">{{ sel.agentName }}</div>
                  <div class="text-xs text-rose-400 mt-1">Audit Record: {{ sel.auditId }}</div>
                </div>

                <div class="space-y-2 text-sm">
                  <div class="flex justify-between py-1 border-b border-slate-700/50">
                    <span class="text-slate-400">Adherence Score:</span>
                    <span class="font-mono text-emerald-400">{{ sel.scriptAdherenceScore }}%</span>
                  </div>
                  <div class="flex justify-between py-1 border-b border-slate-700/50">
                    <span class="text-slate-400">Mandatory Disclosure:</span>
                    <span class="font-mono" [class.text-emerald-400]="sel.mandatoryDisclosureRecited" [class.text-rose-400]="!sel.mandatoryDisclosureRecited">
                      {{ sel.mandatoryDisclosureRecited ? 'Passed' : 'Missed' }}
                    </span>
                  </div>
                  <div class="flex justify-between py-1 border-b border-slate-700/50">
                    <span class="text-slate-400">Call Duration:</span>
                    <span class="font-mono text-white">{{ sel.durationSeconds }}s</span>
                  </div>
                </div>

                @if (sel.status === 'FlaggedForReview') {
                  <button
                    (click)="approveCompliance(sel.auditId)"
                    class="w-full mt-4 py-2 bg-gradient-to-r from-emerald-600 to-teal-600 hover:from-emerald-500 hover:to-teal-500 text-white font-semibold rounded text-sm shadow transition-all"
                  >
                    Grant Supervisor QA Approval
                  </button>
                }
              </div>
            } @else {
              <p class="text-xs text-slate-400">Select an audit record to inspect speech QA transcript.</p>
            }
          </div>

          <div class="mt-6 p-3 bg-slate-900/80 rounded border border-slate-800 text-xs text-slate-400">
            <span class="text-rose-400 font-semibold">Regulatory Hardening:</span> Automatically archives audited audio chunks with immutable cryptographic signatures.
          </div>
        </div>
      </section>
    </div>
  `
})
export class CallComplianceCockpitComponent {
  audits = signal<CallAudit[]>([
    {
      auditId: 'audit_rec_101',
      callId: 'call_voice_01',
      agentName: 'AI Agent (Atlas SDR)',
      timestamp: '2026-11-03T02:00:00Z',
      durationSeconds: 180,
      scriptAdherenceScore: 96.5,
      piiRedactionVerified: true,
      mandatoryDisclosureRecited: true,
      status: 'CompliantPassed',
      flaggedSnippets: []
    },
    {
      auditId: 'audit_rec_102',
      callId: 'call_voice_02',
      agentName: 'Human Specialist (Elena Rostova)',
      timestamp: '2026-11-03T02:15:00Z',
      durationSeconds: 240,
      scriptAdherenceScore: 88.0,
      piiRedactionVerified: true,
      mandatoryDisclosureRecited: true,
      status: 'CompliantPassed',
      flaggedSnippets: []
    },
    {
      auditId: 'audit_rec_103',
      callId: 'call_voice_03',
      agentName: 'AI Agent (Titan Support)',
      timestamp: '2026-11-03T02:30:00Z',
      durationSeconds: 120,
      scriptAdherenceScore: 64.0,
      piiRedactionVerified: true,
      mandatoryDisclosureRecited: false,
      status: 'FlaggedForReview',
      flaggedSnippets: ['Mandatory recording disclosure statement skipped at 00:03']
    }
  ]);

  selectedAudit = signal<CallAudit | null>(this.audits()[2]);

  averageAdherence = computed(() => {
    const list = this.audits();
    if (!list.length) return 0;
    return list.reduce((sum, a) => sum + a.scriptAdherenceScore, 0) / list.length;
  });

  flaggedCount = computed(() =>
    this.audits().filter(a => a.status === 'FlaggedForReview').length
  );

  selectAudit(audit: CallAudit): void {
    this.selectedAudit.set(audit);
  }

  approveCompliance(auditId: string): void {
    this.audits.update(list =>
      list.map(a => {
        if (a.auditId === auditId) {
          return {
            ...a,
            status: 'CompliantPassed',
            flaggedSnippets: []
          };
        }
        return a;
      })
    );

    if (this.selectedAudit()?.auditId === auditId) {
      this.selectedAudit.update(curr => {
        if (!curr) return null;
        return {
          ...curr,
          status: 'CompliantPassed',
          flaggedSnippets: []
        };
      });
    }
  }
}
