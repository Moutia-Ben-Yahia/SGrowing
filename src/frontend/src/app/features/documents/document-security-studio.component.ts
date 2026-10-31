import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface AuditRow {
  id: string;
  viewer: string;
  ip: string;
  action: string;
  dwell: string;
  time: string;
}

@Component({
  selector: 'app-document-security-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen">
      <!-- Header -->
      <div class="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 mb-8 pb-6 border-b border-slate-800">
        <div>
          <div class="flex items-center gap-3">
            <span class="p-2 bg-emerald-500/10 text-emerald-400 rounded-lg border border-emerald-500/20 text-xl font-bold">🛡️</span>
            <div>
              <h1 class="text-2xl font-bold text-white tracking-tight">Document Security & PII Redaction Studio</h1>
              <p class="text-slate-400 text-sm">Automated GDPR PII masking, forensic dynamic watermarking, burn-on-read links & access audit ledger</p>
            </div>
          </div>
        </div>
        <div class="flex items-center gap-3">
          <button (click)="createShareLink()" class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-sm font-medium shadow-lg shadow-emerald-900/30 transition">
            + Generate Burn-on-Read Link
          </button>
        </div>
      </div>

      <!-- KPI Summary Cards -->
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
        <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
          <div class="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-1">PII Spans Redacted</div>
          <div class="text-3xl font-extrabold text-emerald-400">1,482</div>
          <div class="text-xs text-emerald-400 mt-1">100% compliance score</div>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
          <div class="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-1">Active Ephemeral Links</div>
          <div class="text-3xl font-extrabold text-indigo-400">14</div>
          <div class="text-xs text-slate-400 mt-1">Expiring within 60 mins</div>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
          <div class="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-1">Burned Links Today</div>
          <div class="text-3xl font-extrabold text-amber-400">29</div>
          <div class="text-xs text-amber-400 mt-1">Burned after single view</div>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
          <div class="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-1">Forensic Watermarks Rendered</div>
          <div class="text-3xl font-extrabold text-purple-400">312</div>
          <div class="text-xs text-purple-400 mt-1">SHA-256 fingerprint verified</div>
        </div>
      </div>

      <!-- Main Layout -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Live Redaction & Watermark Preview -->
        <div class="lg:col-span-2 space-y-6">
          <div class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
            <h2 class="text-lg font-semibold text-white mb-4">Live PII Redaction & Watermark Sandbox</h2>

            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
              <!-- Raw Text Panel -->
              <div>
                <label class="block text-xs font-semibold uppercase tracking-wider text-slate-400 mb-2">Original Sensitive Input</label>
                <textarea [value]="sampleRawText" rows="6" class="w-full p-3 bg-slate-900 border border-slate-700 rounded-lg text-xs font-mono text-slate-300 focus:outline-none focus:border-emerald-500 resize-none"></textarea>
              </div>

              <!-- Redacted Payload Panel -->
              <div>
                <label class="block text-xs font-semibold uppercase tracking-wider text-emerald-400 mb-2">Secure Redacted Output</label>
                <div class="p-3 bg-slate-900 border border-emerald-500/30 rounded-lg text-xs font-mono text-emerald-300 min-h-[148px] overflow-auto relative">
                  <div>{{ redactedPreview() }}</div>
                  <div class="absolute inset-0 flex items-center justify-center pointer-events-none rotate-[-25deg] select-none opacity-20 text-rose-500 text-xl font-black">
                    CONFIDENTIAL • SAMI BEN ALI • 197.2.14.88
                  </div>
                </div>
              </div>
            </div>

            <!-- Redaction Category Toggles -->
            <div class="mt-4 pt-4 border-t border-slate-700/60 flex flex-wrap gap-2 text-xs">
              <span class="px-2.5 py-1 rounded-full bg-emerald-500/20 text-emerald-300 border border-emerald-500/30">Credit Cards: Masked ✓</span>
              <span class="px-2.5 py-1 rounded-full bg-emerald-500/20 text-emerald-300 border border-emerald-500/30">Bank RIB / IBAN: Masked ✓</span>
              <span class="px-2.5 py-1 rounded-full bg-emerald-500/20 text-emerald-300 border border-emerald-500/30">Tax ID (Matricule Fiscal): Masked ✓</span>
              <span class="px-2.5 py-1 rounded-full bg-emerald-500/20 text-emerald-300 border border-emerald-500/30">National Identity (CIN): Masked ✓</span>
            </div>
          </div>

          <!-- Document Access Audit Ledger -->
          <div class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
            <h2 class="text-lg font-semibold text-white mb-4">Forensic Document Access Audit Ledger</h2>
            <div class="overflow-x-auto">
              <table class="w-full text-left text-xs text-slate-300">
                <thead class="bg-slate-800 text-slate-400 uppercase text-[10px] tracking-wider border-b border-slate-700">
                  <tr>
                    <th class="p-3">Viewer Identity</th>
                    <th class="p-3">IP Address</th>
                    <th class="p-3">Action</th>
                    <th class="p-3">Dwell Time</th>
                    <th class="p-3">Timestamp</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800">
                  <tr *ngFor="let row of audits()" class="hover:bg-slate-800/50 transition">
                    <td class="p-3 font-semibold text-white">{{ row.viewer }}</td>
                    <td class="p-3 font-mono text-slate-400">{{ row.ip }}</td>
                    <td class="p-3">
                      <span class="px-2 py-0.5 rounded bg-amber-500/20 text-amber-300 border border-amber-500/30">{{ row.action }}</span>
                    </td>
                    <td class="p-3">{{ row.dwell }}</td>
                    <td class="p-3 text-slate-500">{{ row.time }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>

        <!-- Sidebar: Watermark Policy & Burn Rules -->
        <div class="space-y-6">
          <div class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
            <h2 class="text-lg font-semibold text-white mb-3">Forensic Watermark Stamp</h2>
            <div class="space-y-3 text-xs text-slate-300">
              <div class="p-3 bg-slate-800/70 border border-slate-700 rounded-lg">
                <div class="font-semibold text-white mb-1">Dynamic Viewer Fingerprint</div>
                <p class="text-slate-400">Embeds viewer account email, corporate IP address, and server UTC microsecond timestamp.</p>
              </div>
              <div class="p-3 bg-slate-800/70 border border-slate-700 rounded-lg">
                <div class="font-semibold text-white mb-1">Cryptographic Tamper Hash</div>
                <p class="text-slate-400 font-mono text-[11px]">SHA-256: 4f89b910a3c87e21...</p>
              </div>
            </div>
          </div>

          <div class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
            <h2 class="text-lg font-semibold text-white mb-3">Burn-on-Read Policy</h2>
            <p class="text-xs text-slate-400 leading-relaxed mb-3">
              One-time view tokens self-destruct immediately after viewing is completed. Subsequent access attempts return an immutable <strong>Link Burned</strong> barrier.
            </p>
            <div class="p-3 bg-rose-500/10 border border-rose-500/20 rounded-lg text-xs text-rose-300">
              🔒 Screenshots & print attempts trigger forensic watermarking and log audit events.
            </div>
          </div>
        </div>
      </div>
    </div>
  `
})
export class DocumentSecurityStudioComponent {
  sampleRawText = `CONTRACT AGREEMENT:
Client: Apex Global Logistics
National ID (CIN): 09876543
Tax Registration: 1234567M/A/M/000
Bank Account (RIB): 08012345678901234567
Primary Credit Card: 4532 9876 5432 1098
Contact: contact@apexlogistics.com`;

  redactedPreview = computed(() => {
    return `CONTRACT AGREEMENT:
Client: Apex Global Logistics
National ID (CIN): [REDACTED:NATIONALID]
Tax Registration: [REDACTED:TAXID]
Bank Account (RIB): [REDACTED:BANKACCOUNTRID]
Primary Credit Card: [REDACTED:CREDITCARD]
Contact: [REDACTED:EMAILADDRESS]`;
  });

  audits = signal<AuditRow[]>([
    { id: '1', viewer: 'auditor@kpmg.com', ip: '197.12.88.4', action: 'ViewedAndBurned', dwell: '74s', time: '4m ago' },
    { id: '2', viewer: 'investor@sequoia.com', ip: '54.210.12.9', action: 'Viewed', dwell: '180s', time: '32m ago' },
    { id: '3', viewer: 'cfo@client.io', ip: '196.203.4.11', action: 'ViewedAndBurned', dwell: '42s', time: '1h ago' }
  ]);

  createShareLink(): void {
    // Demonstration action
  }
}
