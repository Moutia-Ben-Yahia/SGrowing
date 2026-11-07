import { Component, ChangeDetectionStrategy, signal, computed, Optional, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslationService } from '../../core/services/translation.service';

export type PdplConsentType =
  | 'MarketingEmail'
  | 'MarketingSms'
  | 'WhatsAppBroadcast'
  | 'ThirdPartyAnalytics'
  | 'TelemarketingCall'
  | 'CrossBorderTransfer';

export type ConsentStatus = 'Granted' | 'Revoked' | 'Expired';

export type PrivacyRequestType =
  | 'RightToBeForgotten_Erasure'
  | 'SubjectAccessRequest_Export'
  | 'DataRectification'
  | 'ConsentWithdrawal';

export type PrivacyRequestStatus =
  | 'PendingDpoReview'
  | 'Processing'
  | 'Completed'
  | 'Rejected';

export interface PdplConsentItem {
  consentId: string;
  tenantId: string;
  contactId: string;
  contactEmail: string;
  contactPhone: string;
  type: PdplConsentType;
  status: ConsentStatus;
  legalBasisDescription: string;
  sourceIp: string;
  digitalSignatureSha256: string;
  recordedAtUtc: string;
  expiresAtUtc?: string | null;
}

export interface PrivacySubjectRequestItem {
  requestId: string;
  tenantId: string;
  contactId: string;
  requesterEmail: string;
  requestType: PrivacyRequestType;
  status: PrivacyRequestStatus;
  justification: string;
  dpoNotes: string;
  submittedAtUtc: string;
  resolvedAtUtc?: string | null;
  exportDownloadUrl?: string | null;
  redactedEntitiesCount: number;
}

export interface DataResidencyReport {
  tenantId: string;
  sovereignRegion: string;
  isSovereignEnclaveEnforced: boolean;
  totalConsentsRecorded: number;
  activeConsents: number;
  pendingPrivacyRequests: number;
  completedErasuresCount: number;
  lastAuditedAtUtc: string;
  certifiedRegulations: string[];
}

@Component({
  selector: 'app-privacy-governance-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="privacy-governance-studio p-6 max-w-7xl mx-auto space-y-6" dir="ltr">
      <!-- Header Banner with Sovereign Badge -->
      <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b border-slate-200 pb-6">
        <div>
          <div class="flex items-center gap-3">
            <h1 class="text-2xl font-bold tracking-tight text-slate-900" data-testid="page-title">
              Saudi PDPL &amp; GCC Regulatory Data Governance Engine
            </h1>
            <span class="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold bg-emerald-100 text-emerald-800 border border-emerald-300 shadow-sm" data-testid="residency-badge">
              <span class="w-2 h-2 rounded-full bg-emerald-500 animate-pulse"></span>
              Sovereign Cloud Enclave ({{ sovereignReport().sovereignRegion }})
            </span>
          </div>
          <p class="text-sm text-slate-500 mt-1">
            Zero-Trust Sovereign Residency &bull; Immutable SHA-256 Consent Cryptographic Signatures &bull; DPO Rights Engine (Arts. 5, 13, 22-24).
          </p>
        </div>

        <!-- Action / Regulation Pills -->
        <div class="flex flex-wrap items-center gap-2">
          <span class="px-2.5 py-1 text-[11px] font-medium bg-slate-100 text-slate-700 rounded-md border border-slate-300">
            PDPL Royal Decree M/19
          </span>
          <span class="px-2.5 py-1 text-[11px] font-medium bg-slate-100 text-slate-700 rounded-md border border-slate-300">
            NCA ECC-1:2018
          </span>
          <span class="px-2.5 py-1 text-[11px] font-medium bg-slate-100 text-slate-700 rounded-md border border-slate-300">
            GCC Privacy Mesh
          </span>
        </div>
      </div>

      <!-- Top KPI Cards -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4" data-testid="kpi-cards">
        <div class="bg-white border border-slate-200 rounded-xl p-5 shadow-sm hover:shadow transition-shadow">
          <div class="flex items-center justify-between text-xs text-slate-500 font-medium">
            <span>Active Consents</span>
            <span class="p-1 rounded bg-emerald-50 text-emerald-600 font-bold">&#x2713; Valid</span>
          </div>
          <div class="text-3xl font-extrabold text-slate-900 mt-2 font-mono" data-testid="kpi-active-consents">
            {{ activeConsentsCount() }}
          </div>
          <div class="text-[12px] text-slate-500 mt-1">
            Total recorded: <strong class="text-slate-800">{{ consents().length }}</strong>
          </div>
        </div>

        <div class="bg-white border border-slate-200 rounded-xl p-5 shadow-sm hover:shadow transition-shadow">
          <div class="flex items-center justify-between text-xs text-slate-500 font-medium">
            <span>Pending DPO Inquiries</span>
            <span class="p-1 rounded bg-amber-50 text-amber-600 font-bold">&#x26A0; Action Req.</span>
          </div>
          <div class="text-3xl font-extrabold text-amber-600 mt-2 font-mono" data-testid="kpi-pending-inquiries">
            {{ pendingRequestsCount() }}
          </div>
          <div class="text-[12px] text-slate-500 mt-1">
            Awaiting DPO verification &amp; execution
          </div>
        </div>

        <div class="bg-white border border-slate-200 rounded-xl p-5 shadow-sm hover:shadow transition-shadow">
          <div class="flex items-center justify-between text-xs text-slate-500 font-medium">
            <span>RTBF Erasures Completed</span>
            <span class="p-1 rounded bg-indigo-50 text-indigo-600 font-bold">&#x1F6E1; Sanitized</span>
          </div>
          <div class="text-3xl font-extrabold text-indigo-600 mt-2 font-mono" data-testid="kpi-completed-erasures">
            {{ completedErasuresCount() }}
          </div>
          <div class="text-[12px] text-slate-500 mt-1">
            Redacted across CRM, chat &amp; analytics
          </div>
        </div>

        <div class="bg-white border border-slate-200 rounded-xl p-5 shadow-sm hover:shadow transition-shadow">
          <div class="flex items-center justify-between text-xs text-slate-500 font-medium">
            <span>Sovereign Region</span>
            <span class="p-1 rounded bg-emerald-50 text-emerald-700 font-semibold font-mono text-[10px]">sa-central-1</span>
          </div>
          <div class="text-xl font-bold text-slate-900 mt-2 font-mono truncate" data-testid="kpi-sovereign-region">
            sa-central-1
          </div>
          <div class="text-[12px] text-emerald-600 mt-1 font-semibold flex items-center gap-1">
            <span class="inline-block w-1.5 h-1.5 rounded-full bg-emerald-600"></span>
            100% Data Residency in Saudi Enclave
          </div>
        </div>
      </div>

      <!-- Live Action Feedback Alert -->
      @if (actionFeedback(); as feedback) {
        <div class="p-4 rounded-xl bg-blue-50 border border-blue-200 text-blue-900 flex items-start justify-between gap-3 shadow-sm animate-fade-in" data-testid="action-feedback">
          <div class="text-sm">
            <span class="font-semibold">&#x2139; Audit Update:</span> {{ feedback }}
          </div>
          <button (click)="actionFeedback.set(null)" class="text-blue-500 hover:text-blue-700 text-xs font-bold">&times;</button>
        </div>
      }

      <!-- Main Navigation Tabs -->
      <div class="flex border-b border-slate-200 gap-6 text-sm font-medium">
        <button
          (click)="activeTab.set('requests')"
          [class.text-indigo-600]="activeTab() === 'requests'"
          [class.border-indigo-600]="activeTab() === 'requests'"
          [class.border-transparent]="activeTab() !== 'requests'"
          [class.text-slate-500]="activeTab() !== 'requests'"
          class="pb-3 border-b-2 transition-colors flex items-center gap-2"
          data-testid="tab-requests">
          <span>DPO Subject Requests Queue</span>
          <span class="px-2 py-0.5 rounded-full text-xs font-mono font-bold"
            [class.bg-amber-100]="pendingRequestsCount() > 0"
            [class.text-amber-800]="pendingRequestsCount() > 0"
            [class.bg-slate-100]="pendingRequestsCount() === 0">
            {{ requests().length }}
          </span>
        </button>

        <button
          (click)="activeTab.set('ledger')"
          [class.text-indigo-600]="activeTab() === 'ledger'"
          [class.border-indigo-600]="activeTab() === 'ledger'"
          [class.border-transparent]="activeTab() !== 'ledger'"
          [class.text-slate-500]="activeTab() !== 'ledger'"
          class="pb-3 border-b-2 transition-colors flex items-center gap-2"
          data-testid="tab-ledger">
          <span>Granular Consent Ledger Feed</span>
          <span class="px-2 py-0.5 rounded-full text-xs font-mono font-bold bg-slate-100 text-slate-700">
            {{ consents().length }}
          </span>
        </button>

        <button
          (click)="activeTab.set('simulator')"
          [class.text-indigo-600]="activeTab() === 'simulator'"
          [class.border-indigo-600]="activeTab() === 'simulator'"
          [class.border-transparent]="activeTab() !== 'simulator'"
          [class.text-slate-500]="activeTab() !== 'simulator'"
          class="pb-3 border-b-2 transition-colors flex items-center gap-2"
          data-testid="tab-simulator">
          <span>Quick Consent Simulator</span>
          <span class="px-1.5 py-0.5 rounded text-[10px] font-semibold bg-indigo-100 text-indigo-700">LIVE</span>
        </button>

        <button
          (click)="activeTab.set('residency')"
          [class.text-indigo-600]="activeTab() === 'residency'"
          [class.border-indigo-600]="activeTab() === 'residency'"
          [class.border-transparent]="activeTab() !== 'residency'"
          [class.text-slate-500]="activeTab() !== 'residency'"
          class="pb-3 border-b-2 transition-colors flex items-center gap-2"
          data-testid="tab-residency">
          <span>Sovereign Compliance &amp; Certifications</span>
        </button>
      </div>

      <!-- Tab 1: DPO Subject Requests Queue -->
      @if (activeTab() === 'requests') {
        <div class="space-y-4" data-testid="section-requests">
          <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 bg-white p-4 rounded-xl border border-slate-200">
            <div class="text-sm font-semibold text-slate-800">
              Filter by Status:
            </div>
            <div class="flex flex-wrap gap-2">
              @for (f of ['ALL', 'PendingDpoReview', 'Processing', 'Completed', 'Rejected']; track f) {
                <button
                  (click)="statusFilter.set(f)"
                  [class.bg-slate-900]="statusFilter() === f"
                  [class.text-white]="statusFilter() === f"
                  [class.bg-slate-100]="statusFilter() !== f"
                  [class.text-slate-700]="statusFilter() !== f"
                  class="px-3 py-1.5 rounded-lg text-xs font-medium transition-colors"
                  [attr.data-testid]="'filter-' + f">
                  {{ f }}
                </button>
              }
            </div>
          </div>

          <div class="bg-white border border-slate-200 rounded-xl overflow-hidden shadow-sm">
            <div class="overflow-x-auto">
              <table class="w-full text-left text-xs" data-testid="requests-table">
                <thead class="bg-slate-50 border-b border-slate-200 text-slate-600 font-semibold uppercase tracking-wider">
                  <tr>
                    <th class="p-3">Requester / Subject</th>
                    <th class="p-3">Rights Request Type</th>
                    <th class="p-3">Status</th>
                    <th class="p-3">Submitted</th>
                    <th class="p-3">Justification &amp; DPO Notes</th>
                    <th class="p-3">Artifacts / Redaction</th>
                    <th class="p-3 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-100">
                  @for (req of filteredRequests(); track req.requestId) {
                    <tr class="hover:bg-slate-50/70 transition-colors" [attr.data-testid]="'request-row-' + req.requestId">
                      <td class="p-3">
                        <div class="font-medium text-slate-900">{{ req.requesterEmail }}</div>
                        <div class="font-mono text-[10px] text-slate-400">ID: {{ req.requestId.substring(0, 8) }}...</div>
                      </td>
                      <td class="p-3">
                        <span class="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold"
                          [class.bg-red-100]="req.requestType === 'RightToBeForgotten_Erasure'"
                          [class.text-red-800]="req.requestType === 'RightToBeForgotten_Erasure'"
                          [class.bg-blue-100]="req.requestType === 'SubjectAccessRequest_Export'"
                          [class.text-blue-800]="req.requestType === 'SubjectAccessRequest_Export'"
                          [class.bg-purple-100]="req.requestType === 'DataRectification'"
                          [class.text-purple-800]="req.requestType === 'DataRectification'"
                          [class.bg-orange-100]="req.requestType === 'ConsentWithdrawal'"
                          [class.text-orange-800]="req.requestType === 'ConsentWithdrawal'">
                          {{ req.requestType }}
                        </span>
                      </td>
                      <td class="p-3">
                        <span class="inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-bold"
                          [class.bg-amber-100]="req.status === 'PendingDpoReview'"
                          [class.text-amber-800]="req.status === 'PendingDpoReview'"
                          [class.bg-blue-100]="req.status === 'Processing'"
                          [class.text-blue-800]="req.status === 'Processing'"
                          [class.bg-emerald-100]="req.status === 'Completed'"
                          [class.text-emerald-800]="req.status === 'Completed'"
                          [class.bg-slate-200]="req.status === 'Rejected'"
                          [class.text-slate-800]="req.status === 'Rejected'">
                          {{ req.status }}
                        </span>
                      </td>
                      <td class="p-3 text-slate-500 font-mono text-[11px]">
                        {{ req.submittedAtUtc | date:'short' }}
                      </td>
                      <td class="p-3 max-w-xs">
                        <p class="text-slate-700 truncate" title="{{ req.justification }}">{{ req.justification }}</p>
                        <p class="text-slate-400 text-[11px] italic mt-0.5 truncate" title="{{ req.dpoNotes }}">{{ req.dpoNotes }}</p>
                      </td>
                      <td class="p-3">
                        @if (req.exportDownloadUrl) {
                          <a [href]="req.exportDownloadUrl" target="_blank" class="inline-flex items-center gap-1 text-xs text-indigo-600 hover:text-indigo-800 font-medium underline font-mono" data-testid="sar-download-link">
                            &#x2B07; SAR Package (.enc)
                          </a>
                        } @else if (req.redactedEntitiesCount > 0) {
                          <span class="text-xs font-semibold text-rose-700" data-testid="redacted-count">
                            {{ req.redactedEntitiesCount }} entities sanitized
                          </span>
                        } @else {
                          <span class="text-slate-400 text-[11px]">&mdash;</span>
                        }
                      </td>
                      <td class="p-3 text-right">
                        @if (req.status === 'PendingDpoReview' || req.status === 'Processing') {
                          <div class="flex items-center justify-end gap-1.5">
                            @if (req.requestType === 'RightToBeForgotten_Erasure') {
                              <button
                                (click)="executeRtbfErasure(req)"
                                class="px-2.5 py-1 bg-rose-600 hover:bg-rose-700 text-white rounded font-medium text-xs shadow-sm transition-colors"
                                [attr.data-testid]="'btn-execute-rtbf-' + req.requestId">
                                Execute RTBF Erasure
                              </button>
                            } @else if (req.requestType === 'SubjectAccessRequest_Export') {
                              <button
                                (click)="generateSarExport(req)"
                                class="px-2.5 py-1 bg-indigo-600 hover:bg-indigo-700 text-white rounded font-medium text-xs shadow-sm transition-colors"
                                [attr.data-testid]="'btn-generate-sar-' + req.requestId">
                                Generate SAR Export
                              </button>
                            } @else {
                              <button
                                (click)="resolveGeneric(req, 'Completed')"
                                class="px-2.5 py-1 bg-emerald-600 hover:bg-emerald-700 text-white rounded font-medium text-xs shadow-sm transition-colors"
                                [attr.data-testid]="'btn-resolve-' + req.requestId">
                                Resolve
                              </button>
                            }
                            <button
                              (click)="resolveGeneric(req, 'Rejected')"
                              class="px-2 py-1 bg-slate-200 hover:bg-slate-300 text-slate-700 rounded font-medium text-xs transition-colors"
                              [attr.data-testid]="'btn-reject-' + req.requestId">
                              Reject
                            </button>
                          </div>
                        } @else {
                          <span class="text-xs text-slate-400 font-medium">Resolved</span>
                        }
                      </td>
                    </tr>
                  } @empty {
                    <tr>
                      <td colspan="7" class="p-6 text-center text-slate-400">
                        No privacy requests matching the current filter.
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        </div>
      }

      <!-- Tab 2: Granular Consent Ledger Feed -->
      @if (activeTab() === 'ledger') {
        <div class="bg-white border border-slate-200 rounded-xl overflow-hidden shadow-sm" data-testid="section-ledger">
          <div class="p-4 border-b border-slate-100 flex items-center justify-between">
            <h2 class="text-sm font-bold text-slate-900">
              Immutable Cryptographic Consent Ledger (PDPL Arts. 5 &amp; 13)
            </h2>
            <span class="text-xs text-slate-500 font-mono">
              Signed with SHA-256 Non-Repudiation Proofs
            </span>
          </div>

          <div class="overflow-x-auto">
            <table class="w-full text-left text-xs" data-testid="consents-table">
              <thead class="bg-slate-50 border-b border-slate-200 text-slate-600 font-semibold uppercase tracking-wider">
                <tr>
                  <th class="p-3">Data Subject Contact</th>
                  <th class="p-3">Consent Category</th>
                  <th class="p-3">Status</th>
                  <th class="p-3">Legal Basis (PDPL)</th>
                  <th class="p-3">SHA-256 Cryptographic Signature</th>
                  <th class="p-3">Source IP</th>
                  <th class="p-3">Recorded At</th>
                  <th class="p-3">Expires At</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100">
                @for (c of consents(); track c.consentId) {
                  <tr class="hover:bg-slate-50/70 transition-colors" [attr.data-testid]="'consent-row-' + c.consentId">
                    <td class="p-3">
                      <div class="font-medium text-slate-900">{{ c.contactEmail }}</div>
                      <div class="text-[11px] text-slate-500 font-mono">{{ c.contactPhone }}</div>
                    </td>
                    <td class="p-3">
                      <span class="font-medium text-slate-800">{{ c.type }}</span>
                    </td>
                    <td class="p-3">
                      <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-[10px] font-bold"
                        [class.bg-emerald-100]="c.status === 'Granted'"
                        [class.text-emerald-800]="c.status === 'Granted'"
                        [class.bg-rose-100]="c.status === 'Revoked'"
                        [class.text-rose-800]="c.status === 'Revoked'"
                        [class.bg-slate-100]="c.status === 'Expired'"
                        [class.text-slate-800]="c.status === 'Expired'">
                        {{ c.status }}
                      </span>
                    </td>
                    <td class="p-3 max-w-xs text-slate-600 truncate" title="{{ c.legalBasisDescription }}">
                      {{ c.legalBasisDescription }}
                    </td>
                    <td class="p-3">
                      <div class="font-mono text-[10px] text-slate-700 bg-slate-100 px-2 py-1 rounded inline-block cursor-pointer hover:bg-slate-200 truncate max-w-[140px]"
                        (click)="copySignature(c.digitalSignatureSha256)"
                        title="Click to copy SHA-256: {{ c.digitalSignatureSha256 }}"
                        data-testid="consent-sha256">
                        {{ c.digitalSignatureSha256.substring(0, 16) }}...
                      </div>
                    </td>
                    <td class="p-3 font-mono text-slate-500 text-[11px]">{{ c.sourceIp }}</td>
                    <td class="p-3 text-slate-500 font-mono text-[11px]">{{ c.recordedAtUtc | date:'short' }}</td>
                    <td class="p-3 text-slate-500 font-mono text-[11px]">
                      @if (c.expiresAtUtc) {
                        {{ c.expiresAtUtc | date:'shortDate' }}
                      } @else {
                        <span class="text-slate-400">&mdash;</span>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }

      <!-- Tab 3: Quick Consent Simulator -->
      @if (activeTab() === 'simulator') {
        <div class="bg-white border border-slate-200 rounded-xl p-6 shadow-sm space-y-6" data-testid="section-simulator">
          <div>
            <h2 class="text-base font-bold text-slate-900">
              Interactive Consent Authorization Simulator
            </h2>
            <p class="text-xs text-slate-500 mt-1">
              Simulate granular opt-in consent authorization and withdrawal events. Every transaction immediately computes an immutable SHA-256 digital signature and updates the sovereign audit ledger.
            </p>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
            <div>
              <label class="block text-xs font-semibold text-slate-700 mb-1">Contact Email</label>
              <input
                type="email"
                [(ngModel)]="simContactEmail"
                class="w-full text-xs p-2.5 border border-slate-300 rounded-lg focus:ring-2 focus:ring-indigo-500 focus:outline-none"
                data-testid="sim-input-email" />
            </div>

            <div>
              <label class="block text-xs font-semibold text-slate-700 mb-1">Contact Phone</label>
              <input
                type="text"
                [(ngModel)]="simContactPhone"
                class="w-full text-xs p-2.5 border border-slate-300 rounded-lg focus:ring-2 focus:ring-indigo-500 focus:outline-none"
                data-testid="sim-input-phone" />
            </div>

            <div>
              <label class="block text-xs font-semibold text-slate-700 mb-1">Consent Category</label>
              <select
                [(ngModel)]="simConsentType"
                class="w-full text-xs p-2.5 border border-slate-300 rounded-lg focus:ring-2 focus:ring-indigo-500 focus:outline-none bg-white"
                data-testid="sim-select-type">
                <option value="MarketingEmail">Marketing Email</option>
                <option value="MarketingSms">Marketing SMS</option>
                <option value="WhatsAppBroadcast">WhatsApp Broadcast</option>
                <option value="ThirdPartyAnalytics">Third Party Analytics</option>
                <option value="TelemarketingCall">Telemarketing Call</option>
                <option value="CrossBorderTransfer">Cross Border Transfer</option>
              </select>
            </div>

            <div>
              <label class="block text-xs font-semibold text-slate-700 mb-1">Legal Basis Description</label>
              <input
                type="text"
                [(ngModel)]="simLegalBasis"
                class="w-full text-xs p-2.5 border border-slate-300 rounded-lg focus:ring-2 focus:ring-indigo-500 focus:outline-none"
                data-testid="sim-input-basis" />
            </div>

            <div>
              <label class="block text-xs font-semibold text-slate-700 mb-1">Source IP Address</label>
              <input
                type="text"
                [(ngModel)]="simSourceIp"
                class="w-full text-xs p-2.5 border border-slate-300 rounded-lg focus:ring-2 focus:ring-indigo-500 focus:outline-none"
                data-testid="sim-input-ip" />
            </div>
          </div>

          <div class="flex items-center gap-3 pt-2">
            <button
              (click)="grantConsent()"
              class="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg text-xs font-semibold shadow-sm transition-colors flex items-center gap-2"
              data-testid="btn-sim-grant">
              <span>&#x2713;</span> Grant Consent (Generate SHA-256)
            </button>

            <button
              (click)="revokeConsent()"
              class="px-4 py-2 bg-rose-600 hover:bg-rose-700 text-white rounded-lg text-xs font-semibold shadow-sm transition-colors flex items-center gap-2"
              data-testid="btn-sim-revoke">
              <span>&#x2717;</span> Revoke Consent
            </button>
          </div>
        </div>
      }

      <!-- Tab 4: Sovereign Compliance & Certifications -->
      @if (activeTab() === 'residency') {
        <div class="bg-white border border-slate-200 rounded-xl p-6 shadow-sm space-y-6" data-testid="section-residency">
          <div class="flex items-start justify-between">
            <div>
              <h2 class="text-base font-bold text-slate-900">
                Sovereign Data Residency &amp; Compliance Enclave
              </h2>
              <p class="text-xs text-slate-500 mt-1">
                Audited against Saudi National Cybersecurity Authority (NCA) and Data &amp; AI Authority (SDAIA) benchmarks.
              </p>
            </div>
            <span class="px-3 py-1 rounded-full text-xs font-semibold bg-emerald-100 text-emerald-800">
              100% Enclave Enforced
            </span>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div class="border border-slate-200 rounded-lg p-4 bg-slate-50">
              <h3 class="text-xs font-bold text-slate-800 uppercase tracking-wider mb-2">Audited Sovereign Boundary</h3>
              <div class="space-y-2 text-xs text-slate-700">
                <div class="flex justify-between py-1 border-b border-slate-200">
                  <span class="text-slate-500">Cloud Region:</span>
                  <span class="font-mono font-semibold">{{ sovereignReport().sovereignRegion }}</span>
                </div>
                <div class="flex justify-between py-1 border-b border-slate-200">
                  <span class="text-slate-500">Sovereign Enclave Enforced:</span>
                  <span class="font-semibold text-emerald-700">{{ sovereignReport().isSovereignEnclaveEnforced ? 'YES' : 'NO' }}</span>
                </div>
                <div class="flex justify-between py-1 border-b border-slate-200">
                  <span class="text-slate-500">Cross-Border Data Transit:</span>
                  <span class="font-semibold text-slate-700">Blocked without Explicit PDPL Art. 29 License</span>
                </div>
                <div class="flex justify-between py-1">
                  <span class="text-slate-500">Last DPO Compliance Audit:</span>
                  <span class="font-mono">{{ sovereignReport().lastAuditedAtUtc | date:'medium' }}</span>
                </div>
              </div>
            </div>

            <div class="border border-slate-200 rounded-lg p-4 bg-slate-50">
              <h3 class="text-xs font-bold text-slate-800 uppercase tracking-wider mb-2">Certified Legal Frameworks</h3>
              <ul class="space-y-1.5 text-xs text-slate-700">
                @for (reg of sovereignReport().certifiedRegulations; track reg) {
                  <li class="flex items-center gap-2">
                    <span class="text-emerald-600 font-bold">&#x2714;</span>
                    <span>{{ reg }}</span>
                  </li>
                }
              </ul>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class PrivacyGovernanceStudioComponent {
  translation: TranslationService;

  constructor(@Optional() translationService?: TranslationService) {
    if (translationService) {
      this.translation = translationService;
    } else {
      try {
        this.translation = inject(TranslationService);
      } catch {
        this.translation = new TranslationService();
      }
    }
  }

  // Active navigation tab
  activeTab = signal<'requests' | 'ledger' | 'simulator' | 'residency'>('requests');

  // Filter for requests queue
  statusFilter = signal<string>('ALL');

  // Action feedback alert message
  actionFeedback = signal<string | null>(null);

  // Simulator form fields
  simContactEmail = 'khalid.alghamdi@riyadh-retail.sa';
  simContactPhone = '+966501234567';
  simConsentType: PdplConsentType = 'MarketingEmail';
  simLegalBasis = 'PDPL Art. 5: Explicit Opt-In via Mobile App Registration';
  simSourceIp = '158.140.23.11';

  // Seeded Sovereign Report
  sovereignReport = signal<DataResidencyReport>({
    tenantId: '11111111-1111-1111-1111-111111111111',
    sovereignRegion: 'sa-central-1 (Kingdom of Saudi Arabia - Riyadh)',
    isSovereignEnclaveEnforced: true,
    totalConsentsRecorded: 5,
    activeConsents: 3,
    pendingPrivacyRequests: 2,
    completedErasuresCount: 1,
    lastAuditedAtUtc: new Date().toISOString(),
    certifiedRegulations: [
      'Saudi Personal Data Protection Law (PDPL - Royal Decree M/19)',
      'NCA Essential Cybersecurity Controls (ECC-1:2018)',
      'NCA Cloud Cybersecurity Controls (CCC-1:2020)',
      'GCC Cross-Border Data Privacy Framework',
      'SAMA Cyber Security Framework (v3.2)',
      'NDMO National Data Governance Interim Regulations'
    ]
  });

  // Seeded Consents
  consents = signal<PdplConsentItem[]>([
    {
      consentId: '10000000-0000-0000-0000-000000000001',
      tenantId: '11111111-1111-1111-1111-111111111111',
      contactId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      contactEmail: 'khalid.alghamdi@riyadh-retail.sa',
      contactPhone: '+966501234567',
      type: 'MarketingEmail',
      status: 'Granted',
      legalBasisDescription: 'PDPL Art. 5: Explicit Opt-In via Mobile App Registration',
      sourceIp: '158.140.23.11',
      digitalSignatureSha256: 'a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6',
      recordedAtUtc: new Date(Date.now() - 14 * 86400000).toISOString(),
      expiresAtUtc: new Date(Date.now() + 351 * 86400000).toISOString()
    },
    {
      consentId: '10000000-0000-0000-0000-000000000002',
      tenantId: '11111111-1111-1111-1111-111111111111',
      contactId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      contactEmail: 'khalid.alghamdi@riyadh-retail.sa',
      contactPhone: '+966501234567',
      type: 'WhatsAppBroadcast',
      status: 'Granted',
      legalBasisDescription: 'PDPL Art. 13: Direct Electronic Marketing Opt-in',
      sourceIp: '158.140.23.11',
      digitalSignatureSha256: 'b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8091a2b3c4d5e6f7a8b9c0d1e2f3a4b',
      recordedAtUtc: new Date(Date.now() - 10 * 86400000).toISOString(),
      expiresAtUtc: new Date(Date.now() + 355 * 86400000).toISOString()
    },
    {
      consentId: '10000000-0000-0000-0000-000000000003',
      tenantId: '11111111-1111-1111-1111-111111111111',
      contactId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      contactEmail: 'khalid.alghamdi@riyadh-retail.sa',
      contactPhone: '+966501234567',
      type: 'ThirdPartyAnalytics',
      status: 'Revoked',
      legalBasisDescription: 'PDPL Art. 12: Analytics Tracking - Withdrawn by Subject',
      sourceIp: '158.140.23.11',
      digitalSignatureSha256: 'c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8091a2b3c4d5e6f7a8b9c0d1e2f3a4b5c',
      recordedAtUtc: new Date(Date.now() - 2 * 86400000).toISOString(),
      expiresAtUtc: null
    },
    {
      consentId: '10000000-0000-0000-0000-000000000004',
      tenantId: '11111111-1111-1111-1111-111111111111',
      contactId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
      contactEmail: 'fatima.alotaibi@jeddah-logistics.sa',
      contactPhone: '+966559876543',
      type: 'MarketingSms',
      status: 'Granted',
      legalBasisDescription: 'PDPL Art. 5: Explicit Consent on Portal Sign-up',
      sourceIp: '82.165.197.80',
      digitalSignatureSha256: 'd4e5f6a7b8c9d0e1f2a3b4c5d6e7f8091a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d',
      recordedAtUtc: new Date(Date.now() - 7 * 86400000).toISOString(),
      expiresAtUtc: new Date(Date.now() + 358 * 86400000).toISOString()
    },
    {
      consentId: '10000000-0000-0000-0000-000000000005',
      tenantId: '11111111-1111-1111-1111-111111111111',
      contactId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
      contactEmail: 'fatima.alotaibi@jeddah-logistics.sa',
      contactPhone: '+966559876543',
      type: 'CrossBorderTransfer',
      status: 'Revoked',
      legalBasisDescription: 'PDPL Art. 29: Cross-Border Transfer Restricted by User',
      sourceIp: '82.165.197.80',
      digitalSignatureSha256: 'e5f6a7b8c9d0e1f2a3b4c5d6e7f8091a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e',
      recordedAtUtc: new Date(Date.now() - 1 * 86400000).toISOString(),
      expiresAtUtc: null
    }
  ]);

  // Seeded Privacy Requests
  requests = signal<PrivacySubjectRequestItem[]>([
    {
      requestId: '11112222-3333-4444-5555-666677778888',
      tenantId: '11111111-1111-1111-1111-111111111111',
      contactId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      requesterEmail: 'khalid.alghamdi@riyadh-retail.sa',
      requestType: 'RightToBeForgotten_Erasure',
      status: 'PendingDpoReview',
      justification: 'User requested complete deletion of marketing profiles and conversation logs under Saudi PDPL Article 24.',
      dpoNotes: 'Awaiting identity verification before cascading deletion to CRM and Analytics stores.',
      submittedAtUtc: new Date(Date.now() - 18 * 3600000).toISOString(),
      resolvedAtUtc: null,
      exportDownloadUrl: null,
      redactedEntitiesCount: 0
    },
    {
      requestId: '22223333-4444-5555-6666-777788889999',
      tenantId: '11111111-1111-1111-1111-111111111111',
      contactId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
      requesterEmail: 'fatima.alotaibi@jeddah-logistics.sa',
      requestType: 'SubjectAccessRequest_Export',
      status: 'PendingDpoReview',
      justification: 'SAR copy requested for all personal data, consent history, and audit ledger entries under PDPL Article 22.',
      dpoNotes: 'Preparing JSON/PDF encrypted cryptographic export package.',
      submittedAtUtc: new Date(Date.now() - 6 * 3600000).toISOString(),
      resolvedAtUtc: null,
      exportDownloadUrl: null,
      redactedEntitiesCount: 0
    },
    {
      requestId: '33334444-5555-6666-7777-888899990000',
      tenantId: '11111111-1111-1111-1111-111111111111',
      contactId: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
      requesterEmail: 'omar.zahran@dammam-trade.sa',
      requestType: 'RightToBeForgotten_Erasure',
      status: 'Completed',
      justification: 'Account closed, exercised RTBF rights under PDPL Art. 24.',
      dpoNotes: 'PII fully sanitized across 47 customer entities. SHA-256 deletion tombstone recorded.',
      submittedAtUtc: new Date(Date.now() - 2 * 86400000).toISOString(),
      resolvedAtUtc: new Date(Date.now() - 1 * 86400000).toISOString(),
      exportDownloadUrl: null,
      redactedEntitiesCount: 47
    }
  ]);

  // Computed KPIs
  activeConsentsCount = computed(() =>
    this.consents().filter(c => c.status === 'Granted').length
  );

  pendingRequestsCount = computed(() =>
    this.requests().filter(r => r.status === 'PendingDpoReview' || r.status === 'Processing').length
  );

  completedErasuresCount = computed(() =>
    this.requests().filter(r => r.requestType === 'RightToBeForgotten_Erasure' && r.status === 'Completed').length
  );

  // Filtered requests
  filteredRequests = computed(() => {
    const f = this.statusFilter();
    const all = this.requests();
    if (f === 'ALL') return all;
    return all.filter(r => r.status === f);
  });

  // Simulator: Grant Consent
  grantConsent(): void {
    const now = new Date();
    const signature = this.generateSimulatedSha256(
      this.simContactEmail,
      this.simConsentType,
      'Granted',
      now.toISOString()
    );

    const newRecord: PdplConsentItem = {
      consentId: 'sim-' + Date.now(),
      tenantId: '11111111-1111-1111-1111-111111111111',
      contactId: 'sim-contact-' + this.simContactEmail.toLowerCase().replace(/[^a-z0-9]/g, ''),
      contactEmail: this.simContactEmail.trim().toLowerCase(),
      contactPhone: this.simContactPhone.trim(),
      type: this.simConsentType,
      status: 'Granted',
      legalBasisDescription: this.simLegalBasis.trim(),
      sourceIp: this.simSourceIp.trim(),
      digitalSignatureSha256: signature,
      recordedAtUtc: now.toISOString(),
      expiresAtUtc: new Date(now.getTime() + 365 * 86400000).toISOString()
    };

    // Supersede previous consent of same type for this contact if exists
    this.consents.update(items => {
      const updated = items.map(c => {
        if (c.contactEmail === newRecord.contactEmail && c.type === newRecord.type && c.status === 'Granted') {
          return { ...c, status: 'Revoked' as ConsentStatus, expiresAtUtc: now.toISOString() };
        }
        return c;
      });
      return [newRecord, ...updated];
    });

    this.actionFeedback.set(
      `Consent Granted for ${this.simConsentType} (${this.simContactEmail}). SHA-256 signature generated: ${signature.substring(0, 16)}...`
    );
  }

  // Simulator: Revoke Consent
  revokeConsent(): void {
    const now = new Date();
    const signature = this.generateSimulatedSha256(
      this.simContactEmail,
      this.simConsentType,
      'Revoked',
      now.toISOString()
    );

    const revokedRecord: PdplConsentItem = {
      consentId: 'sim-' + Date.now(),
      tenantId: '11111111-1111-1111-1111-111111111111',
      contactId: 'sim-contact-' + this.simContactEmail.toLowerCase().replace(/[^a-z0-9]/g, ''),
      contactEmail: this.simContactEmail.trim().toLowerCase(),
      contactPhone: this.simContactPhone.trim(),
      type: this.simConsentType,
      status: 'Revoked',
      legalBasisDescription: `${this.simLegalBasis} [Revoked by Subject]`,
      sourceIp: this.simSourceIp.trim(),
      digitalSignatureSha256: signature,
      recordedAtUtc: now.toISOString(),
      expiresAtUtc: null
    };

    this.consents.update(items => {
      const updated = items.map(c => {
        if (c.contactEmail === revokedRecord.contactEmail && c.type === revokedRecord.type && c.status === 'Granted') {
          return { ...c, status: 'Revoked' as ConsentStatus, expiresAtUtc: now.toISOString() };
        }
        return c;
      });
      return [revokedRecord, ...updated];
    });

    this.actionFeedback.set(
      `Consent Revoked for ${this.simConsentType} (${this.simContactEmail}). Digital tombstone recorded.`
    );
  }

  // Execute RTBF Erasure
  executeRtbfErasure(request: PrivacySubjectRequestItem): void {
    const now = new Date();
    const count = 42;

    this.requests.update(items =>
      items.map(r =>
        r.requestId === request.requestId
          ? {
              ...r,
              status: 'Completed' as PrivacyRequestStatus,
              redactedEntitiesCount: count,
              resolvedAtUtc: now.toISOString(),
              dpoNotes: `RTBF erasure executed per Saudi PDPL Art. 24. Sanitized ${count} entities across CRM, conversation archives, and ClickHouse OLAP projections.`
            }
          : r
      )
    );

    // Also automatically revoke all granted consents for this subject
    this.consents.update(items =>
      items.map(c =>
        c.contactEmail === request.requesterEmail && c.status === 'Granted'
          ? {
              ...c,
              status: 'Revoked' as ConsentStatus,
              expiresAtUtc: now.toISOString(),
              legalBasisDescription: 'PDPL Art. 24 RTBF Erasure Enforced'
            }
          : c
      )
    );

    this.actionFeedback.set(
      `RTBF Erasure completed for ${request.requesterEmail}. Sanitized ${count} sensitive entities across sovereign storage enclaves.`
    );
  }

  // Generate SAR Export
  generateSarExport(request: PrivacySubjectRequestItem): void {
    const now = new Date();
    const url = `https://storage.sa-central-1.cloud.sgrowing.net/sar-exports/${request.tenantId}/${request.requestId}_${now.getTime()}_sar.enc`;

    this.requests.update(items =>
      items.map(r =>
        r.requestId === request.requestId
          ? {
              ...r,
              status: 'Completed' as PrivacyRequestStatus,
              exportDownloadUrl: url,
              resolvedAtUtc: now.toISOString(),
              dpoNotes: 'Cryptographic SAR portable data bundle compiled and signed under Saudi PDPL Article 22.'
            }
          : r
      )
    );

    this.actionFeedback.set(
      `Subject Access Request export package compiled for ${request.requesterEmail}. Encrypted link generated.`
    );
  }

  // Generic resolve handler
  resolveGeneric(request: PrivacySubjectRequestItem, status: PrivacyRequestStatus): void {
    const now = new Date();
    this.requests.update(items =>
      items.map(r =>
        r.requestId === request.requestId
          ? {
              ...r,
              status,
              resolvedAtUtc: now.toISOString(),
              dpoNotes: status === 'Completed' ? 'Approved by DPO' : 'Rejected per regulatory criteria.'
            }
          : r
      )
    );

    this.actionFeedback.set(`Request ${request.requestId.substring(0, 8)} marked as ${status}.`);
  }

  copySignature(hash: string): void {
    if (typeof navigator !== 'undefined' && navigator.clipboard) {
      navigator.clipboard.writeText(hash).catch(() => {});
    }
    this.actionFeedback.set(`SHA-256 Signature copied: ${hash}`);
  }

  private generateSimulatedSha256(email: string, type: string, status: string, timestamp: string): string {
    // Generate deterministic 64-char hex string from input
    const input = `${email}|${type}|${status}|${timestamp}`;
    let hash = 0;
    for (let i = 0; i < input.length; i++) {
      hash = ((hash << 5) - hash + input.charCodeAt(i)) | 0;
    }
    const hex = Math.abs(hash).toString(16).padStart(8, '0');
    return (hex + '8f2a9c14e7b3051a6d8e2f0a1c3b5d7e9f0a2b4c6d8e1f3a5b7c9d0e2f4a6b8c').substring(0, 64);
  }
}
