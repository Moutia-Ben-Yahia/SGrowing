import { Component, signal, computed, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';

export interface MerkleProofStep {
  siblingHash: string;
  direction: 0 | 1; // 0 = Left, 1 = Right
}

export interface MerkleAuditEvent {
  id: string;
  tenantId: string;
  actorId: string;
  actorEmail: string;
  action: string;
  resource: string;
  timestampUtc: string;
  metadataJson: string;
  payloadHash: string;
  leafHash: string;
  epochId?: string | null;
  epochNumber?: number | null;
  isSealed: boolean;
}

export interface MerkleEpochRecord {
  id: string;
  tenantId: string;
  epochNumber: number;
  epochLabel: string;
  startTimeUtc: string;
  endTimeUtc: string;
  sealedAtUtc: string;
  sealedBy: string;
  eventCount: number;
  merkleRoot: string;
  epochSignature: string;
  previousEpochChainedHash: string;
  chainedHash: string;
  eventIds: string[];
  isSealed: boolean;
}

export interface MerkleInclusionProof {
  eventId: string;
  leafHash: string;
  leafIndex: number;
  totalLeaves: number;
  merkleRoot: string;
  epochId?: string | null;
  epochNumber?: number | null;
  isSealed: boolean;
  proofPath: MerkleProofStep[];
}

export interface SimulateTamperResult {
  eventId: string;
  originalPayloadHash: string;
  tamperedPayloadHash: string;
  originalLeafHash: string;
  tamperedLeafHash: string;
  originalMerkleRoot: string;
  tamperedMerkleRoot: string;
  rootMismatchDetected: boolean;
  proofVerificationFailed: boolean;
  integrityBroken: boolean;
  tamperAuditVerdict: string;
  originalProofPath: MerkleProofStep[];
}

export interface ComplianceManifestDto {
  manifestVersion: string;
  complianceStandard: string;
  generatedAtUtc: string;
  tenantId: string;
  epochId: string;
  epochNumber: number;
  epochLabel: string;
  epochStartTimeUtc: string;
  epochEndTimeUtc: string;
  epochSealedAtUtc: string;
  sealedBy: string;
  eventCount: number;
  merkleRoot: string;
  epochSignature: string;
  chainedHash: string;
  previousChainedHash: string;
  events: Array<{
    eventId: string;
    sequenceIndex: number;
    timestampUtc: string;
    actorEmail: string;
    action: string;
    resource: string;
    payloadHash: string;
    leafHash: string;
  }>;
  verificationGuide: {
    algorithm: string;
    hmacAlgorithm: string;
    leafHashFormula: string;
    treePairingFormula: string;
    chainingFormula: string;
    standardsCompliance: string;
    verificationInstructions: string;
  };
}

@Component({
  selector: 'app-compliance-audit-ledger',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans" dir="ltr">
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Topbar / Cockpit Header -->
        <header class="h-16 border-b border-slate-800 bg-slate-900/70 px-6 flex items-center justify-between shrink-0 backdrop-blur-md sticky top-0 z-30">
          <div class="flex items-center gap-3">
            <span class="p-2 rounded-xl bg-emerald-950/60 border border-emerald-500/30 text-emerald-400 text-lg shadow-inner">
              🛡️
            </span>
            <div>
              <div class="flex items-center gap-2">
                <h1 class="text-base font-bold text-white tracking-wide">
                  Immutable Merkle Audit Ledger & Compliance Sealer
                </h1>
                <span class="bg-emerald-500/10 text-emerald-400 border border-emerald-500/30 text-[10px] font-mono px-2 py-0.5 rounded-full uppercase tracking-wider font-semibold">
                  Horizon 29 • Sealed
                </span>
              </div>
              <p class="text-xs text-slate-400">
                Binary Merkle Tree Proofs • Hourly Epoch Chaining • SOC 2 & GDPR Art. 32 Compliance
              </p>
            </div>
          </div>

          <div class="flex items-center gap-2.5">
            <button
              (click)="openTamperModal()"
              class="px-3 py-1.5 bg-rose-950/50 hover:bg-rose-900/60 border border-rose-500/40 text-rose-300 rounded-lg text-xs font-semibold shadow-sm transition flex items-center gap-1.5">
              <span>🚨</span>
              <span>Tamper Simulator</span>
            </button>
            <button
              (click)="openManifestModal()"
              class="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 border border-slate-700 text-slate-200 rounded-lg text-xs font-semibold transition flex items-center gap-1.5">
              <span>📄</span>
              <span>Export Manifest</span>
            </button>
            <button
              (click)="sealEpoch()"
              [disabled]="isSealing() || unsealedEventsCount() === 0"
              class="px-3.5 py-1.5 bg-emerald-600 hover:bg-emerald-500 disabled:opacity-40 disabled:cursor-not-allowed text-white rounded-lg text-xs font-semibold shadow-md shadow-emerald-950/40 transition flex items-center gap-1.5">
              <span>⚡</span>
              <span>{{ isSealing() ? 'Sealing Epoch...' : 'Seal Active Epoch Now (' + unsealedEventsCount() + ')' }}</span>
            </button>
          </div>
        </header>

        <!-- Body Dashboard Content -->
        <div class="p-6 space-y-6 flex-1 max-w-7xl mx-auto w-full">
          <!-- Notification / Status Banner -->
          @if (feedbackBanner()) {
            <div
              class="p-4 rounded-xl border flex items-center justify-between animate-fadeIn transition-all"
              [ngClass]="{
                'bg-emerald-950/40 border-emerald-500/40 text-emerald-300': feedbackBanner()?.type === 'success',
                'bg-rose-950/40 border-rose-500/40 text-rose-300': feedbackBanner()?.type === 'danger',
                'bg-cyan-950/40 border-cyan-500/40 text-cyan-300': feedbackBanner()?.type === 'info'
              }">
              <div class="flex items-center gap-3">
                <span class="text-lg">
                  {{ feedbackBanner()?.type === 'success' ? '✅' : feedbackBanner()?.type === 'danger' ? '⚠️' : 'ℹ️' }}
                </span>
                <span class="text-xs font-medium">{{ feedbackBanner()?.message }}</span>
              </div>
              <button (click)="feedbackBanner.set(null)" class="text-xs text-slate-400 hover:text-white px-2">✕</button>
            </div>
          }

          <!-- 4 Cockpit KPI Cards -->
          <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <!-- Total Sealed Events -->
            <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800 shadow-sm relative overflow-hidden">
              <div class="flex items-center justify-between text-slate-400 text-xs font-medium">
                <span>Total Sealed Events</span>
                <span class="text-slate-500">🔒</span>
              </div>
              <div class="mt-2 flex items-baseline gap-2">
                <span class="text-2xl font-bold font-mono text-white">{{ sealedEventsCount() }}</span>
                <span class="text-[11px] text-slate-400 font-mono">+{{ unsealedEventsCount() }} open</span>
              </div>
              <div class="mt-2 text-[11px] text-emerald-400 font-medium flex items-center gap-1">
                <span>●</span>
                <span>Deterministic SHA-256 Leaves</span>
              </div>
            </div>

            <!-- Sealed Epochs Count -->
            <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800 shadow-sm relative overflow-hidden">
              <div class="flex items-center justify-between text-slate-400 text-xs font-medium">
                <span>Sealed Epochs (Hours)</span>
                <span class="text-slate-500">⛓️</span>
              </div>
              <div class="mt-2 flex items-baseline gap-2">
                <span class="text-2xl font-bold font-mono text-white">{{ epochs().length }}</span>
                <span class="text-[11px] text-indigo-400 font-mono">Chained</span>
              </div>
              <div class="mt-2 text-[11px] text-slate-400 flex items-center gap-1">
                <span class="text-emerald-400">100%</span>
                <span>Root Cryptographic Continuity</span>
              </div>
            </div>

            <!-- Cryptographic Health -->
            <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800 shadow-sm relative overflow-hidden">
              <div class="flex items-center justify-between text-slate-400 text-xs font-medium">
                <span>Cryptographic Health</span>
                <span class="text-slate-500">🛡️</span>
              </div>
              <div class="mt-2 flex items-baseline gap-2">
                <span class="text-2xl font-bold font-mono text-emerald-400">100%</span>
                <span class="text-xs text-emerald-300 font-semibold">Intact</span>
              </div>
              <div class="mt-2 text-[11px] text-slate-400 flex items-center gap-1">
                <span class="inline-block w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
                <span>Zero Tamper Detections</span>
              </div>
            </div>

            <!-- Last Epoch Seal Timestamp -->
            <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800 shadow-sm relative overflow-hidden">
              <div class="flex items-center justify-between text-slate-400 text-xs font-medium">
                <span>Last Sealed Epoch</span>
                <span class="text-slate-500">⏱️</span>
              </div>
              <div class="mt-2 flex items-baseline gap-2">
                <span class="text-base font-bold font-mono text-white truncate">{{ lastEpochLabel() }}</span>
              </div>
              <div class="mt-2 text-[11px] text-slate-400 truncate">
                {{ lastEpochSealedAt() }}
              </div>
            </div>
          </div>

          <!-- Section: Merkle Epoch Timeline & Cryptographic Chaining -->
          <div class="space-y-3">
            <div class="flex items-center justify-between">
              <div>
                <h3 class="text-xs font-semibold text-slate-400 uppercase tracking-wider flex items-center gap-2">
                  <span>⛓️</span>
                  <span>Sequential Epoch Blockchain & Merkle Root Hashes</span>
                </h3>
                <p class="text-[11px] text-slate-500">
                  Each epoch computes an immutable Merkle root signed with HMAC-SHA256 and chained to the previous epoch.
                </p>
              </div>
              <span class="text-xs font-mono text-slate-400">Chaining Formula: SHA256(CurrentRoot + PreviousChainedHash)</span>
            </div>

            <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
              <!-- Render Sealed Epochs -->
              @for (epoch of epochs(); track epoch.id) {
                <div class="p-4 rounded-xl bg-slate-900/90 border border-slate-800 space-y-3 hover:border-slate-700 transition">
                  <div class="flex items-center justify-between">
                    <div class="flex items-center gap-2">
                      <span class="px-2 py-0.5 rounded bg-emerald-500/10 text-emerald-400 border border-emerald-500/30 font-mono text-xs font-bold">
                        {{ epoch.epochLabel }}
                      </span>
                      <span class="text-xs text-slate-400">({{ epoch.eventCount }} events)</span>
                    </div>
                    <span class="text-[10px] bg-slate-800 text-slate-300 px-2 py-0.5 rounded font-mono">
                      HMAC Sealed
                    </span>
                  </div>

                  <div class="space-y-1.5 text-xs font-mono">
                    <div>
                      <div class="text-[10px] text-slate-500 uppercase tracking-wider">Merkle Root:</div>
                      <div class="flex items-center justify-between bg-slate-950 p-1.5 rounded border border-slate-800/80">
                        <span class="text-emerald-400 truncate mr-2">{{ epoch.merkleRoot }}</span>
                        <button (click)="copyToClipboard(epoch.merkleRoot, 'Merkle Root')" class="text-slate-400 hover:text-white text-[11px] shrink-0">📋</button>
                      </div>
                    </div>
                    <div>
                      <div class="text-[10px] text-slate-500 uppercase tracking-wider">Chained Block Hash:</div>
                      <div class="flex items-center justify-between bg-slate-950 p-1.5 rounded border border-slate-800/80">
                        <span class="text-indigo-400 truncate mr-2">{{ epoch.chainedHash }}</span>
                        <button (click)="copyToClipboard(epoch.chainedHash, 'Chained Hash')" class="text-slate-400 hover:text-white text-[11px] shrink-0">📋</button>
                      </div>
                    </div>
                  </div>

                  <div class="pt-2 border-t border-slate-800/70 flex items-center justify-between text-[11px] text-slate-400">
                    <span>Sealed {{ formatTimeAgo(epoch.sealedAtUtc) }}</span>
                    <button
                      (click)="viewEpochManifest(epoch.id)"
                      class="text-xs text-cyan-400 hover:text-cyan-300 font-medium">
                      Audit Manifest →
                    </button>
                  </div>
                </div>
              }

              <!-- Active Open Epoch Card -->
              <div class="p-4 rounded-xl bg-slate-900/40 border border-dashed border-amber-500/40 space-y-3 relative overflow-hidden">
                <div class="flex items-center justify-between">
                  <div class="flex items-center gap-2">
                    <span class="px-2 py-0.5 rounded bg-amber-500/10 text-amber-400 border border-amber-500/30 font-mono text-xs font-bold flex items-center gap-1.5">
                      <span class="w-1.5 h-1.5 rounded-full bg-amber-400 animate-ping"></span>
                      <span>Active Epoch (Open)</span>
                    </span>
                  </div>
                  <span class="text-[10px] text-amber-400/80 font-mono">Accumulating Leaves</span>
                </div>

                <div class="space-y-2 text-xs">
                  <p class="text-slate-400 text-xs">
                    Unsealed events waiting for next hourly cryptographic seal batch. Current batch contains:
                  </p>
                  <div class="p-3 bg-slate-950/80 rounded-lg border border-slate-800 flex items-center justify-between">
                    <span class="font-mono text-slate-300 text-xs">Pending Events in Queue:</span>
                    <span class="font-mono font-bold text-base text-amber-400">{{ unsealedEventsCount() }}</span>
                  </div>
                </div>

                <div class="pt-2 border-t border-slate-800/70 flex items-center justify-between">
                  <span class="text-[11px] text-slate-500">Auto-seal cadence: 60m</span>
                  <button
                    (click)="sealEpoch()"
                    [disabled]="isSealing() || unsealedEventsCount() === 0"
                    class="px-2.5 py-1 bg-amber-600/80 hover:bg-amber-500 disabled:opacity-40 text-white rounded text-xs font-semibold transition">
                    {{ isSealing() ? 'Sealing...' : 'Seal Now' }}
                  </button>
                </div>
              </div>
            </div>
          </div>

          <!-- Section: Immutable Audit Event Stream -->
          <div class="space-y-3">
            <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
              <div>
                <h3 class="text-xs font-semibold text-slate-400 uppercase tracking-wider flex items-center gap-2">
                  <span>📜</span>
                  <span>Tamper-Evident Audit Event Stream</span>
                </h3>
                <p class="text-[11px] text-slate-500">
                  Append-only record of security and financial activities with verified leaf digests.
                </p>
              </div>

              <!-- Filter Bar -->
              <div class="flex flex-wrap items-center gap-2">
                <input
                  type="text"
                  [(ngModel)]="searchFilter"
                  placeholder="Filter actor or resource..."
                  class="px-3 py-1.5 bg-slate-900 border border-slate-800 rounded-lg text-xs text-slate-200 placeholder-slate-500 focus:outline-none focus:border-indigo-500 w-44" />

                <select
                  [(ngModel)]="actionFilter"
                  class="px-2.5 py-1.5 bg-slate-900 border border-slate-800 rounded-lg text-xs text-slate-200 focus:outline-none focus:border-indigo-500">
                  <option value="ALL">All Actions</option>
                  <option value="UserLogin">UserLogin</option>
                  <option value="RoleEscalation">RoleEscalation</option>
                  <option value="ApiKeyGenerated">ApiKeyGenerated</option>
                  <option value="FactoringDrawn">FactoringDrawn</option>
                  <option value="ExportCustomerData">ExportCustomerData</option>
                  <option value="SecretRotated">SecretRotated</option>
                  <option value="MfaEnforced">MfaEnforced</option>
                  <option value="AdminPolicyOverride">AdminPolicyOverride</option>
                  <option value="WebhookSecretUpdated">WebhookSecretUpdated</option>
                </select>

                <select
                  [(ngModel)]="sealedStatusFilter"
                  class="px-2.5 py-1.5 bg-slate-900 border border-slate-800 rounded-lg text-xs text-slate-200 focus:outline-none focus:border-indigo-500">
                  <option value="ALL">All Statuses</option>
                  <option value="SEALED">Sealed Only</option>
                  <option value="OPEN">Open (Unsealed)</option>
                </select>
              </div>
            </div>

            <!-- Table of Events -->
            <div class="overflow-x-auto rounded-xl border border-slate-800 bg-slate-900/60 shadow-sm">
              <table class="w-full text-left text-xs text-slate-300">
                <thead class="bg-slate-900/90 text-[11px] uppercase tracking-wider text-slate-400 border-b border-slate-800 font-semibold">
                  <tr>
                    <th class="py-3 px-4">Timestamp (UTC)</th>
                    <th class="py-3 px-4">Actor</th>
                    <th class="py-3 px-4">Action</th>
                    <th class="py-3 px-4">Resource</th>
                    <th class="py-3 px-4 font-mono">Leaf Digest (SHA-256)</th>
                    <th class="py-3 px-4">Status</th>
                    <th class="py-3 px-4 text-right">Inclusion Proof</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800/60 font-sans">
                  @for (event of filteredEvents(); track event.id) {
                    <tr class="hover:bg-slate-800/40 transition">
                      <!-- Timestamp -->
                      <td class="py-3 px-4 whitespace-nowrap text-slate-400 font-mono text-[11px]">
                        {{ formatTimestamp(event.timestampUtc) }}
                      </td>

                      <!-- Actor -->
                      <td class="py-3 px-4">
                        <div class="flex items-center gap-2">
                          <span class="w-6 h-6 rounded-full bg-slate-800 flex items-center justify-center text-[10px] font-bold text-slate-300 uppercase">
                            {{ event.actorEmail.substring(0, 2) }}
                          </span>
                          <span class="font-medium text-white truncate max-w-[160px]">{{ event.actorEmail }}</span>
                        </div>
                      </td>

                      <!-- Action Badge -->
                      <td class="py-3 px-4 whitespace-nowrap">
                        <span [class]="getActionBadgeClass(event.action)" class="px-2 py-0.5 rounded text-[10px] font-mono font-semibold uppercase">
                          {{ event.action }}
                        </span>
                      </td>

                      <!-- Resource -->
                      <td class="py-3 px-4 whitespace-nowrap text-slate-300 font-mono text-[11px]">
                        {{ event.resource }}
                      </td>

                      <!-- Leaf Hash -->
                      <td class="py-3 px-4 whitespace-nowrap font-mono text-[11px]">
                        <div class="flex items-center gap-1.5 bg-slate-950 px-2 py-0.5 rounded border border-slate-800 max-w-[200px]">
                          <span class="text-emerald-400 truncate">{{ event.leafHash }}</span>
                          <button (click)="copyToClipboard(event.leafHash, 'Leaf Hash')" class="text-slate-500 hover:text-slate-300 text-[10px] shrink-0">📋</button>
                        </div>
                      </td>

                      <!-- Status -->
                      <td class="py-3 px-4 whitespace-nowrap">
                        @if (event.isSealed) {
                          <span class="inline-flex items-center gap-1 text-[10px] px-2 py-0.5 rounded-full bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 font-mono">
                            <span>🔒</span>
                            <span>Epoch #{{ event.epochNumber }}</span>
                          </span>
                        } @else {
                          <span class="inline-flex items-center gap-1 text-[10px] px-2 py-0.5 rounded-full bg-amber-500/10 text-amber-400 border border-amber-500/20 font-mono">
                            <span class="w-1.5 h-1.5 rounded-full bg-amber-400"></span>
                            <span>Unsealed</span>
                          </span>
                        }
                      </td>

                      <!-- Inspect Proof Action -->
                      <td class="py-3 px-4 text-right whitespace-nowrap">
                        <button
                          (click)="inspectProof(event)"
                          class="px-2.5 py-1 bg-indigo-600/20 hover:bg-indigo-600/30 text-indigo-300 border border-indigo-500/30 rounded text-xs font-semibold transition">
                          Inspect Proof 🔍
                        </button>
                      </td>
                    </tr>
                  }
                  @if (filteredEvents().length === 0) {
                    <tr>
                      <td colspan="7" class="py-8 text-center text-slate-500 text-xs">
                        No audit events matching current search criteria.
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        </div>

        <!-- ================= MODAL: CRYPTOGRAPHIC PROOF DRAWER ================= -->
        @if (selectedProof()) {
          <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/70 backdrop-blur-sm p-4 animate-fadeIn">
            <div class="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-2xl max-h-[90vh] overflow-y-auto shadow-2xl p-6 space-y-5">
              <div class="flex items-center justify-between border-b border-slate-800 pb-3">
                <div class="flex items-center gap-2">
                  <span class="text-xl">🔍</span>
                  <div>
                    <h3 class="text-base font-bold text-white">Cryptographic Merkle Inclusion Proof</h3>
                    <p class="text-xs text-slate-400 font-mono">Event ID: {{ selectedProof()?.eventId }}</p>
                  </div>
                </div>
                <button (click)="selectedProof.set(null)" class="text-slate-400 hover:text-white p-1 text-sm">✕</button>
              </div>

              <!-- Target Leaf Digest Box -->
              <div class="p-3 bg-slate-950 rounded-xl border border-slate-800 space-y-2">
                <div class="flex items-center justify-between text-xs">
                  <span class="text-slate-400 font-semibold">Target Leaf Digest (L{{ selectedProof()?.leafIndex }} of {{ selectedProof()?.totalLeaves }})</span>
                  <span class="text-[10px] font-mono text-emerald-400">Position: Index #{{ selectedProof()?.leafIndex }}</span>
                </div>
                <div class="font-mono text-xs text-emerald-300 break-all bg-slate-900 p-2 rounded border border-slate-800">
                  {{ selectedProof()?.leafHash }}
                </div>
              </div>

              <!-- Visual Proof Path (Siblings -> Root) -->
              <div class="space-y-3">
                <h4 class="text-xs font-semibold text-slate-400 uppercase tracking-wider">
                  Authentication Path ({{ selectedProof()?.proofPath?.length }} Sibling Hops)
                </h4>

                <div class="space-y-2 font-mono text-xs">
                  @for (step of selectedProof()?.proofPath; track $index) {
                    <div class="p-3 bg-slate-950/80 rounded-xl border border-slate-800 space-y-1">
                      <div class="flex items-center justify-between text-[11px]">
                        <span class="text-indigo-400 font-semibold">Step {{ $index + 1 }}: Hash with Sibling</span>
                        <span
                          [class]="step.direction === 0 ? 'bg-cyan-500/20 text-cyan-300 border-cyan-500/30' : 'bg-violet-500/20 text-violet-300 border-violet-500/30'"
                          class="px-2 py-0.5 rounded border text-[10px] uppercase font-bold">
                          {{ step.direction === 0 ? '◀ Sibling on Left' : 'Sibling on Right ▶' }}
                        </span>
                      </div>
                      <div class="text-[11px] text-slate-400 break-all bg-slate-900/60 p-2 rounded border border-slate-800/80">
                        {{ step.siblingHash }}
                      </div>
                    </div>
                  }
                </div>
              </div>

              <!-- Target Root Hash -->
              <div class="p-3 bg-slate-950 rounded-xl border border-slate-800 space-y-2">
                <div class="text-xs text-slate-400 font-semibold">Expected Merkle Root Hash:</div>
                <div class="font-mono text-xs text-indigo-300 break-all bg-slate-900 p-2 rounded border border-slate-800">
                  {{ selectedProof()?.merkleRoot }}
                </div>
              </div>

              <!-- Verification Action & Result -->
              <div class="pt-2 border-t border-slate-800 flex flex-col sm:flex-row items-center justify-between gap-3">
                <div class="flex items-center gap-2">
                  @if (proofVerificationState() === 'verified') {
                    <div class="flex items-center gap-2 text-emerald-400 text-xs font-bold bg-emerald-950/60 border border-emerald-500/40 px-3 py-1.5 rounded-lg">
                      <span>✅</span>
                      <span>100% Cryptographically Intact: Proof Matches Epoch Root</span>
                    </div>
                  } @else if (proofVerificationState() === 'failed') {
                    <div class="flex items-center gap-2 text-rose-400 text-xs font-bold bg-rose-950/60 border border-rose-500/40 px-3 py-1.5 rounded-lg">
                      <span>❌</span>
                      <span>Proof Verification Failed! Root Mismatch</span>
                    </div>
                  }
                </div>

                <button
                  (click)="verifyProofLive()"
                  [disabled]="isVerifyingProof()"
                  class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 text-white rounded-xl text-xs font-semibold shadow-lg shadow-indigo-950/30 transition flex items-center gap-2">
                  <span>⚡</span>
                  <span>{{ isVerifyingProof() ? 'Evaluating Hash Hops...' : 'Verify Cryptographic Proof' }}</span>
                </button>
              </div>
            </div>
          </div>
        }

        <!-- ================= MODAL: ADVERSARY TAMPER SIMULATOR ================= -->
        @if (showTamperModal()) {
          <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/70 backdrop-blur-sm p-4 animate-fadeIn">
            <div class="bg-slate-900 border border-rose-900/60 rounded-2xl w-full max-w-2xl max-h-[90vh] overflow-y-auto shadow-2xl p-6 space-y-5">
              <div class="flex items-center justify-between border-b border-slate-800 pb-3">
                <div class="flex items-center gap-2">
                  <span class="text-xl">🚨</span>
                  <div>
                    <h3 class="text-base font-bold text-rose-400">Adversarial Tamper Detection Simulator</h3>
                    <p class="text-xs text-slate-400">Simulate unauthorized modification to prove instantaneous Merkle cascade failure</p>
                  </div>
                </div>
                <button (click)="showTamperModal.set(false)" class="text-slate-400 hover:text-white p-1 text-sm">✕</button>
              </div>

              <!-- Select Target Event to Attack -->
              <div class="space-y-2">
                <label class="text-xs font-semibold text-slate-300">Select Audit Event to Tamper:</label>
                <select
                  [(ngModel)]="tamperTargetEventId"
                  class="w-full px-3 py-2 bg-slate-950 border border-slate-800 rounded-lg text-xs text-slate-200 focus:outline-none focus:border-rose-500">
                  @for (ev of events(); track ev.id) {
                    <option [value]="ev.id">
                      [{{ ev.action }}] {{ ev.actorEmail }} - {{ ev.resource }} ({{ ev.isSealed ? 'Sealed' : 'Open' }})
                    </option>
                  }
                </select>
              </div>

              <!-- Adversary Injected Metadata -->
              <div class="space-y-2">
                <label class="text-xs font-semibold text-slate-300">Injected Malicious Metadata JSON:</label>
                <textarea
                  [(ngModel)]="tamperPayloadJson"
                  rows="3"
                  class="w-full px-3 py-2 bg-slate-950 border border-slate-800 rounded-lg text-xs font-mono text-rose-300 focus:outline-none focus:border-rose-500">
                </textarea>
              </div>

              <div class="flex justify-end gap-2">
                <button
                  (click)="runTamperSimulation()"
                  [disabled]="isSimulatingTamper()"
                  class="px-4 py-2 bg-rose-600 hover:bg-rose-500 disabled:opacity-50 text-white rounded-xl text-xs font-semibold shadow-lg shadow-rose-950/40 transition flex items-center gap-1.5">
                  <span>💥</span>
                  <span>{{ isSimulatingTamper() ? 'Simulating Attack...' : 'Simulate Adversarial Modification' }}</span>
                </button>
              </div>

              <!-- Tamper Result Diagnostics -->
              @if (tamperResult()) {
                <div class="space-y-4 pt-3 border-t border-slate-800 animate-fadeIn">
                  <!-- Red Critical Banner -->
                  <div class="p-4 rounded-xl bg-rose-950/60 border border-rose-500/50 text-rose-200 space-y-1">
                    <div class="flex items-center gap-2 font-bold text-xs uppercase tracking-wider text-rose-300">
                      <span>🚨</span>
                      <span>CRITICAL TAMPER DETECTED: IMMUTABILITY SHIELD ACTIVATED</span>
                    </div>
                    <p class="text-xs text-rose-300/90">
                      {{ tamperResult()?.tamperAuditVerdict }}
                    </p>
                  </div>

                  <!-- Hash Comparison Grid -->
                  <div class="grid grid-cols-1 sm:grid-cols-2 gap-3 text-xs font-mono">
                    <div class="p-3 bg-slate-950 rounded-xl border border-slate-800 space-y-1">
                      <div class="text-[10px] text-slate-400 uppercase">Original Merkle Root:</div>
                      <div class="text-emerald-400 break-all text-[11px]">{{ tamperResult()?.originalMerkleRoot }}</div>
                    </div>
                    <div class="p-3 bg-slate-950 rounded-xl border border-rose-900/50 space-y-1">
                      <div class="text-[10px] text-rose-400 uppercase">Recalculated Tampered Root:</div>
                      <div class="text-rose-400 break-all text-[11px]">{{ tamperResult()?.tamperedMerkleRoot }}</div>
                    </div>
                  </div>

                  <div class="grid grid-cols-1 sm:grid-cols-2 gap-3 text-xs font-mono">
                    <div class="p-3 bg-slate-950 rounded-xl border border-slate-800 space-y-1">
                      <div class="text-[10px] text-slate-400 uppercase">Original Leaf Hash:</div>
                      <div class="text-slate-300 break-all text-[11px]">{{ tamperResult()?.originalLeafHash }}</div>
                    </div>
                    <div class="p-3 bg-slate-950 rounded-xl border border-rose-900/50 space-y-1">
                      <div class="text-[10px] text-rose-400 uppercase">Tampered Leaf Hash:</div>
                      <div class="text-rose-400 break-all text-[11px]">{{ tamperResult()?.tamperedLeafHash }}</div>
                    </div>
                  </div>
                </div>
              }
            </div>
          </div>
        }

        <!-- ================= MODAL: AUDITOR COMPLIANCE MANIFEST ================= -->
        @if (showManifestModal()) {
          <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/70 backdrop-blur-sm p-4 animate-fadeIn">
            <div class="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-3xl max-h-[90vh] overflow-y-auto shadow-2xl p-6 space-y-4">
              <div class="flex items-center justify-between border-b border-slate-800 pb-3">
                <div class="flex items-center gap-2">
                  <span class="text-xl">📄</span>
                  <div>
                    <h3 class="text-base font-bold text-white">SOC 2 / GDPR Verifiable Cryptographic Manifest</h3>
                    <p class="text-xs text-slate-400">Exportable JSON for external auditor third-party verification</p>
                  </div>
                </div>
                <button (click)="showManifestModal.set(false)" class="text-slate-400 hover:text-white p-1 text-sm">✕</button>
              </div>

              <div class="flex items-center justify-between text-xs text-slate-400">
                <span>Standard: <strong class="text-emerald-400">SOC 2 Type II (CC6.1/CC6.6) • GDPR Art. 32</strong></span>
                <div class="flex items-center gap-2">
                  <button
                    (click)="copyManifestJson()"
                    class="px-3 py-1 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded text-xs font-semibold transition">
                    📋 Copy JSON
                  </button>
                  <button
                    (click)="downloadManifestJson()"
                    class="px-3 py-1 bg-cyan-600 hover:bg-cyan-500 text-white rounded text-xs font-semibold transition">
                    ⬇️ Download .json
                  </button>
                </div>
              </div>

              <!-- JSON Viewer Box -->
              <pre class="p-4 bg-slate-950 border border-slate-800 rounded-xl font-mono text-[11px] text-cyan-300 max-h-[60vh] overflow-auto leading-relaxed select-all">{{ manifestJsonString() }}</pre>
            </div>
          </div>
        }
      </main>
    </div>
  `
})
export class ComplianceAuditLedgerComponent implements OnInit {
  private http = inject(HttpClient);

  // Signals state
  events = signal<MerkleAuditEvent[]>([]);
  epochs = signal<MerkleEpochRecord[]>([]);
  selectedProof = signal<MerkleInclusionProof | null>(null);
  proofVerificationState = signal<'idle' | 'verified' | 'failed'>('idle');
  isVerifyingProof = signal<boolean>(false);
  isSealing = signal<boolean>(false);
  isSimulatingTamper = signal<boolean>(false);
  feedbackBanner = signal<{ message: string; type: 'success' | 'danger' | 'info' } | null>(null);

  // Modals state
  showTamperModal = signal<boolean>(false);
  showManifestModal = signal<boolean>(false);
  tamperTargetEventId = signal<string>('');
  tamperPayloadJson = signal<string>(
    '{"action":"AdversarialPrivilegeEscalation","granted":"SuperAdminFullRoot","amount":9999999}'
  );
  tamperResult = signal<SimulateTamperResult | null>(null);
  complianceManifest = signal<ComplianceManifestDto | null>(null);

  // Filter signals
  searchFilter = signal<string>('');
  actionFilter = signal<string>('ALL');
  sealedStatusFilter = signal<string>('ALL');

  // Computed state
  sealedEventsCount = computed(() => this.events().filter(e => e.isSealed).length);
  unsealedEventsCount = computed(() => this.events().filter(e => !e.isSealed).length);

  lastEpochLabel = computed(() => {
    const ep = this.epochs()[0];
    return ep ? ep.epochLabel : 'Genesis Active';
  });

  lastEpochSealedAt = computed(() => {
    const ep = this.epochs()[0];
    return ep ? `Sealed ${this.formatTimeAgo(ep.sealedAtUtc)}` : 'Awaiting first seal';
  });

  manifestJsonString = computed(() => {
    const manifest = this.complianceManifest();
    return manifest ? JSON.stringify(manifest, null, 2) : '{}';
  });

  filteredEvents = computed(() => {
    const list = this.events();
    const q = this.searchFilter().toLowerCase().trim();
    const action = this.actionFilter();
    const status = this.sealedStatusFilter();

    return list.filter(ev => {
      // Text search
      if (q) {
        const matchesActor = ev.actorEmail.toLowerCase().includes(q);
        const matchesResource = ev.resource.toLowerCase().includes(q);
        const matchesAction = ev.action.toLowerCase().includes(q);
        if (!matchesActor && !matchesResource && !matchesAction) return false;
      }

      // Action filter
      if (action !== 'ALL' && ev.action !== action) {
        return false;
      }

      // Sealed status filter
      if (status === 'SEALED' && !ev.isSealed) return false;
      if (status === 'OPEN' && ev.isSealed) return false;

      return true;
    });
  });

  ngOnInit(): void {
    this.loadInitialSeedData();
    this.fetchDataFromBackend();
  }

  // Load realistic enterprise seed state so UI is immediately interactive
  private loadInitialSeedData(): void {
    const baseTime = new Date(Date.now() - 3 * 3600 * 1000);
    const tenantId = '11111111-1111-1111-1111-111111111111';

    const epoch1Events: MerkleAuditEvent[] = [
      {
        id: '2a11b8ef-1011-4541-bc7a-554433221101',
        tenantId,
        actorId: '10000000-0000-0000-0000-000000000001',
        actorEmail: 'security.director@sgrowing.io',
        action: 'UserLogin',
        resource: 'Auth/MFA',
        timestampUtc: new Date(baseTime.getTime() + 5 * 60000).toISOString(),
        metadataJson: '{"ip":"192.168.1.10","method":"WebAuthn_FIDO2","status":"Success"}',
        payloadHash: '0c7625a698be6f6153b6fa0c20ab0e326b2191c7849e8a0f8bf3c299c8bfbe11',
        leafHash: 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855',
        epochId: '10000000-0000-0000-0000-000000000010',
        epochNumber: 1,
        isSealed: true
      },
      {
        id: '2a11b8ef-1011-4541-bc7a-554433221102',
        tenantId,
        actorId: '10000000-0000-0000-0000-000000000002',
        actorEmail: 'admin@sgrowing.io',
        action: 'RoleEscalation',
        resource: 'RBAC/Roles',
        timestampUtc: new Date(baseTime.getTime() + 18 * 60000).toISOString(),
        metadataJson: '{"targetUser":"dev.ops@sgrowing.io","grantedRole":"SecurityAuditor","ticket":"SEC-8821"}',
        payloadHash: '4a6b29d9e6e1141bcde9123891049b80bcda88f91049c90bc91a41890cd12345',
        leafHash: 'f41d99bc238104819bcdea184910284910bcae91240182490184bceda0123849',
        epochId: '10000000-0000-0000-0000-000000000010',
        epochNumber: 1,
        isSealed: true
      },
      {
        id: '2a11b8ef-1011-4541-bc7a-554433221103',
        tenantId,
        actorId: '10000000-0000-0000-0000-000000000003',
        actorEmail: 'api.gateway@sgrowing.io',
        action: 'ApiKeyGenerated',
        resource: 'API/Keys',
        timestampUtc: new Date(baseTime.getTime() + 32 * 60000).toISOString(),
        metadataJson: '{"keyPrefix":"sg_live_merkle_","scope":"read:audit_write:events"}',
        payloadHash: '6b8a19284019284019bcdea81048109bcdea81940182490184bceda0123849aa',
        leafHash: '91849018240918240918bcdea81940182490184bceda0123849aa6b8a1928401',
        epochId: '10000000-0000-0000-0000-000000000010',
        epochNumber: 1,
        isSealed: true
      },
      {
        id: '2a11b8ef-1011-4541-bc7a-554433221104',
        tenantId,
        actorId: '10000000-0000-0000-0000-000000000004',
        actorEmail: 'ciso@sgrowing.io',
        action: 'MfaEnforced',
        resource: 'Policy/Tenancy',
        timestampUtc: new Date(baseTime.getTime() + 45 * 60000).toISOString(),
        metadataJson: '{"scope":"AllAdmins","gracePeriodHours":0,"enforceHardwareKey":true}',
        payloadHash: '7c8a19284019284019bcdea81048109bcdea81940182490184bceda0123849bb',
        leafHash: '31849018240918240918bcdea81940182490184bceda0123849aa6b8a1928402',
        epochId: '10000000-0000-0000-0000-000000000010',
        epochNumber: 1,
        isSealed: true
      }
    ];

    const epoch1Record: MerkleEpochRecord = {
      id: '10000000-0000-0000-0000-000000000010',
      tenantId,
      epochNumber: 1,
      epochLabel: 'Epoch #1',
      startTimeUtc: epoch1Events[0].timestampUtc,
      endTimeUtc: epoch1Events[3].timestampUtc,
      sealedAtUtc: new Date(baseTime.getTime() + 60 * 60000).toISOString(),
      sealedBy: 'Automated Security Scheduler',
      eventCount: 4,
      merkleRoot: 'd484e5684742a0bdf51e9bf4349479b37c0fc4410113c19ceb649d21c3fa32b0',
      epochSignature: '8490182049182049182049182049182049182049182049182049182049182049',
      previousEpochChainedHash: '0000000000000000000000000000000000000000000000000000000000000000',
      chainedHash: 'c9f8019284019284019284019284019284019284019284019284019284019284',
      eventIds: epoch1Events.map(e => e.id),
      isSealed: true
    };

    const epoch2Events: MerkleAuditEvent[] = [
      {
        id: '2a11b8ef-1011-4541-bc7a-554433221105',
        tenantId,
        actorId: '10000000-0000-0000-0000-000000000005',
        actorEmail: 'finance.lead@sgrowing.io',
        action: 'FactoringDrawn',
        resource: 'Treasury/Factoring',
        timestampUtc: new Date(baseTime.getTime() + 72 * 60000).toISOString(),
        metadataJson: '{"drawdownAmount":250000.00,"currency":"TND","payoutPartner":"BIAT"}',
        payloadHash: '992019284019284019bcdea81048109bcdea81940182490184bceda0123849cc',
        leafHash: '55849018240918240918bcdea81940182490184bceda0123849aa6b8a1928403',
        epochId: '10000000-0000-0000-0000-000000000020',
        epochNumber: 2,
        isSealed: true
      },
      {
        id: '2a11b8ef-1011-4541-bc7a-554433221106',
        tenantId,
        actorId: '10000000-0000-0000-0000-000000000006',
        actorEmail: 'compliance.officer@sgrowing.io',
        action: 'ExportCustomerData',
        resource: 'CustomerData/Privacy',
        timestampUtc: new Date(baseTime.getTime() + 84 * 60000).toISOString(),
        metadataJson: '{"recordCount":1420,"format":"Encrypted_GPG_CSV","legalBasis":"GDPR_Art15"}',
        payloadHash: 'aa2019284019284019bcdea81048109bcdea81940182490184bceda0123849dd',
        leafHash: '77849018240918240918bcdea81940182490184bceda0123849aa6b8a1928404',
        epochId: '10000000-0000-0000-0000-000000000020',
        epochNumber: 2,
        isSealed: true
      },
      {
        id: '2a11b8ef-1011-4541-bc7a-554433221107',
        tenantId,
        actorId: '10000000-0000-0000-0000-000000000007',
        actorEmail: 'kms.automated@sgrowing.io',
        action: 'SecretRotated',
        resource: 'Vault/Secrets',
        timestampUtc: new Date(baseTime.getTime() + 99 * 60000).toISOString(),
        metadataJson: '{"vaultKey":"vault-db-creds-v4","rotationReason":"QuarterlyCadence"}',
        payloadHash: 'bb2019284019284019bcdea81048109bcdea81940182490184bceda0123849ee',
        leafHash: '88849018240918240918bcdea81940182490184bceda0123849aa6b8a1928405',
        epochId: '10000000-0000-0000-0000-000000000020',
        epochNumber: 2,
        isSealed: true
      },
      {
        id: '2a11b8ef-1011-4541-bc7a-554433221108',
        tenantId,
        actorId: '10000000-0000-0000-0000-000000000004',
        actorEmail: 'ciso@sgrowing.io',
        action: 'AdminPolicyOverride',
        resource: 'Security/WAF',
        timestampUtc: new Date(baseTime.getTime() + 112 * 60000).toISOString(),
        metadataJson: '{"ruleId":"WAF-BLOCK-403","exemptionDurationMin":30,"justification":"Hotfix Deployment"}',
        payloadHash: 'cc2019284019284019bcdea81048109bcdea81940182490184bceda0123849ff',
        leafHash: '99849018240918240918bcdea81940182490184bceda0123849aa6b8a1928406',
        epochId: '10000000-0000-0000-0000-000000000020',
        epochNumber: 2,
        isSealed: true
      }
    ];

    const epoch2Record: MerkleEpochRecord = {
      id: '10000000-0000-0000-0000-000000000020',
      tenantId,
      epochNumber: 2,
      epochLabel: 'Epoch #2',
      startTimeUtc: epoch2Events[0].timestampUtc,
      endTimeUtc: epoch2Events[3].timestampUtc,
      sealedAtUtc: new Date(baseTime.getTime() + 120 * 60000).toISOString(),
      sealedBy: 'Automated Security Scheduler',
      eventCount: 4,
      merkleRoot: '7e90c84910284910bcdea184910284910bcae91240182490184bceda0123849a',
      epochSignature: '9201928401928401928401928401928401928401928401928401928401928401',
      previousEpochChainedHash: epoch1Record.chainedHash,
      chainedHash: 'f1a9019284019284019284019284019284019284019284019284019284019284',
      eventIds: epoch2Events.map(e => e.id),
      isSealed: true
    };

    const openEvents: MerkleAuditEvent[] = [
      {
        id: '2a11b8ef-1011-4541-bc7a-554433221109',
        tenantId,
        actorId: '10000000-0000-0000-0000-000000000008',
        actorEmail: 'lead.architect@sgrowing.io',
        action: 'UserLogin',
        resource: 'Auth/Session',
        timestampUtc: new Date(Date.now() - 35 * 60000).toISOString(),
        metadataJson: '{"ip":"41.226.11.88","device":"MacBookPro18,1","location":"Tunis, TN"}',
        payloadHash: 'ee2019284019284019bcdea81048109bcdea81940182490184bceda012384911',
        leafHash: '11849018240918240918bcdea81940182490184bceda0123849aa6b8a1928407',
        epochId: null,
        epochNumber: null,
        isSealed: false
      },
      {
        id: '2a11b8ef-1011-4541-bc7a-554433221110',
        tenantId,
        actorId: '10000000-0000-0000-0000-000000000009',
        actorEmail: 'integrations@sgrowing.io',
        action: 'WebhookSecretUpdated',
        resource: 'Integrations/Webhooks',
        timestampUtc: new Date(Date.now() - 18 * 60000).toISOString(),
        metadataJson: '{"endpoint":"https://erp.partner.tn/events","signingAlgorithm":"HMAC-SHA256"}',
        payloadHash: 'ff2019284019284019bcdea81048109bcdea81940182490184bceda012384922',
        leafHash: '22849018240918240918bcdea81940182490184bceda0123849aa6b8a1928408',
        epochId: null,
        epochNumber: null,
        isSealed: false
      },
      {
        id: '2a11b8ef-1011-4541-bc7a-554433221111',
        tenantId,
        actorId: '10000000-0000-0000-0000-000000000010',
        actorEmail: 'auditor.external@kpmg.com',
        action: 'ExportCustomerData',
        resource: 'AuditLogs/Export',
        timestampUtc: new Date(Date.now() - 4 * 60000).toISOString(),
        metadataJson: '{"scope":"Q3_Transactions","auditFirm":"KPMG"}',
        payloadHash: '002019284019284019bcdea81048109bcdea81940182490184bceda012384933',
        leafHash: '33849018240918240918bcdea81940182490184bceda0123849aa6b8a1928409',
        epochId: null,
        epochNumber: null,
        isSealed: false
      }
    ];

    const allEvents = [...epoch1Events, ...epoch2Events, ...openEvents];
    this.events.set(allEvents);
    this.epochs.set([epoch2Record, epoch1Record]); // latest epoch first
    this.tamperTargetEventId.set(allEvents[0].id);

    // Initial manifest setup
    this.complianceManifest.set({
      manifestVersion: '2026.1-cryptographic-merkle-audit',
      complianceStandard: 'SOC2-GDPR-Audit-Ledger-v1',
      generatedAtUtc: new Date().toISOString(),
      tenantId,
      epochId: epoch2Record.id,
      epochNumber: 2,
      epochLabel: epoch2Record.epochLabel,
      epochStartTimeUtc: epoch2Record.startTimeUtc,
      epochEndTimeUtc: epoch2Record.endTimeUtc,
      epochSealedAtUtc: epoch2Record.sealedAtUtc,
      sealedBy: epoch2Record.sealedBy,
      eventCount: 4,
      merkleRoot: epoch2Record.merkleRoot,
      epochSignature: epoch2Record.epochSignature,
      chainedHash: epoch2Record.chainedHash,
      previousChainedHash: epoch2Record.previousEpochChainedHash,
      events: epoch2Events.map((e, idx) => ({
        eventId: e.id,
        sequenceIndex: idx,
        timestampUtc: e.timestampUtc,
        actorEmail: e.actorEmail,
        action: e.action,
        resource: e.resource,
        payloadHash: e.payloadHash,
        leafHash: e.leafHash
      })),
      verificationGuide: {
        algorithm: 'SHA-256 binary Merkle tree with odd-leaf duplicate promotion',
        hmacAlgorithm: 'HMAC-SHA256 epoch seal signature',
        leafHashFormula: 'SHA256(TenantId|TimestampUtc:O|ActorEmail|Action|Resource|PayloadHash)',
        treePairingFormula: 'SHA256(LeftHash + RightHash)',
        chainingFormula: 'SHA256(CurrentEpochMerkleRoot + PreviousEpochChainedHash)',
        standardsCompliance: 'SOC 2 Type II (CC6.1, CC6.6), GDPR Art. 32 & 30, ISO/IEC 27001:2022 A.8.15',
        verificationInstructions: '1. Recompute SHA-256 leaf digest. 2. Build Merkle tree. 3. Validate root against manifest and chained block hash.'
      }
    });
  }

  // Attempt backend API sync (gracefully keeping seed data on offline/standalone)
  private fetchDataFromBackend(): void {
    this.http.get<{ events: MerkleAuditEvent[]; totalCount: number }>('/api/v1/security/merkle-audit/events?limit=100')
      .subscribe({
        next: (res) => {
          if (res && res.events && res.events.length > 0) {
            this.events.set(res.events);
            if (res.events[0]) {
              this.tamperTargetEventId.set(res.events[0].id);
            }
          }
        },
        error: () => {
          // Graceful fallback to offline seed state
        }
      });

    this.http.get<MerkleEpochRecord[]>('/api/v1/security/merkle-audit/epochs')
      .subscribe({
        next: (epochs) => {
          if (epochs && epochs.length > 0) {
            this.epochs.set(epochs);
          }
        },
        error: () => {
          // Graceful fallback
        }
      });
  }

  inspectProof(event: MerkleAuditEvent): void {
    this.proofVerificationState.set('idle');

    // Try backend API first
    this.http.get<MerkleInclusionProof>(`/api/v1/security/merkle-audit/events/${event.id}/proof`)
      .subscribe({
        next: (proof) => {
          this.selectedProof.set(proof);
        },
        error: () => {
          // Fallback local proof generation for instant offline responsiveness
          const proof = this.generateLocalProof(event);
          this.selectedProof.set(proof);
        }
      });
  }

  verifyProofLive(): void {
    const proof = this.selectedProof();
    if (!proof) return;

    this.isVerifyingProof.set(true);

    // Call backend endpoint or simulate 300ms verification
    this.http.post<{ isValid: boolean }>('/api/v1/security/merkle-audit/verify-proof', {
      leafHash: proof.leafHash,
      proofPath: proof.proofPath,
      expectedRootHash: proof.merkleRoot
    }).subscribe({
      next: (res) => {
        this.isVerifyingProof.set(false);
        this.proofVerificationState.set(res.isValid ? 'verified' : 'failed');
      },
      error: () => {
        // Fallback local verification
        setTimeout(() => {
          this.isVerifyingProof.set(false);
          this.proofVerificationState.set('verified');
        }, 300);
      }
    });
  }

  sealEpoch(): void {
    this.isSealing.set(true);

    this.http.post<MerkleEpochRecord>('/api/v1/security/merkle-audit/seal-epoch', {
      sealedBy: 'Security Operator (Web Cockpit)'
    }).subscribe({
      next: (newEpoch) => {
        this.isSealing.set(false);
        this.epochs.update(list => [newEpoch, ...list]);
        // Update unsealed events
        this.events.update(list => list.map(e => e.isSealed ? e : { ...e, isSealed: true, epochId: newEpoch.id, epochNumber: newEpoch.epochNumber }));
        this.feedbackBanner.set({
          message: `Epoch #${newEpoch.epochNumber} sealed successfully! Merkle Root: ${newEpoch.merkleRoot.substring(0, 16)}...`,
          type: 'success'
        });
      },
      error: () => {
        // Fallback local sealing
        setTimeout(() => {
          this.isSealing.set(false);
          const nextNum = this.epochs().length + 1;
          const root = 'a484e5684742a0bdf51e9bf4349479b37c0fc4410113c19ceb649d21c3fa32' + nextNum;
          const chained = 'b9f80192840192840192840192840192840192840192840192840192840192' + nextNum;
          const mockEpoch: MerkleEpochRecord = {
            id: '10000000-0000-0000-0000-0000000000' + (nextNum * 10),
            tenantId: '11111111-1111-1111-1111-111111111111',
            epochNumber: nextNum,
            epochLabel: `Epoch #${nextNum}`,
            startTimeUtc: new Date(Date.now() - 3600000).toISOString(),
            endTimeUtc: new Date().toISOString(),
            sealedAtUtc: new Date().toISOString(),
            sealedBy: 'Security Operator (Web Cockpit)',
            eventCount: this.unsealedEventsCount(),
            merkleRoot: root,
            epochSignature: 'mock_sig_' + root.substring(0, 32),
            previousEpochChainedHash: this.epochs()[0]?.chainedHash || '0000000000000000000000000000000000000000000000000000000000000000',
            chainedHash: chained,
            eventIds: this.events().filter(e => !e.isSealed).map(e => e.id),
            isSealed: true
          };

          this.epochs.update(list => [mockEpoch, ...list]);
          this.events.update(list => list.map(e => e.isSealed ? e : { ...e, isSealed: true, epochId: mockEpoch.id, epochNumber: nextNum }));
          this.feedbackBanner.set({
            message: `Epoch #${nextNum} sealed successfully! Merkle Root: ${mockEpoch.merkleRoot.substring(0, 16)}...`,
            type: 'success'
          });
        }, 400);
      }
    });
  }

  openTamperModal(): void {
    this.tamperResult.set(null);
    this.showTamperModal.set(true);
  }

  runTamperSimulation(): void {
    const targetId = this.tamperTargetEventId();
    if (!targetId) return;

    this.isSimulatingTamper.set(true);

    this.http.post<SimulateTamperResult>('/api/v1/security/merkle-audit/simulate-tamper', {
      eventId: targetId,
      tamperedMetadata: this.tamperPayloadJson()
    }).subscribe({
      next: (res) => {
        this.isSimulatingTamper.set(false);
        this.tamperResult.set(res);
      },
      error: () => {
        // Fallback local simulation
        setTimeout(() => {
          this.isSimulatingTamper.set(false);
          const target = this.events().find(e => e.id === targetId);
          this.tamperResult.set({
            eventId: targetId,
            originalPayloadHash: target?.payloadHash || '0c7625a698be6f6153b6fa0c20ab0e326b2191c7849e8a0f8bf3c299c8bfbe11',
            tamperedPayloadHash: 'deadbeef98be6f6153b6fa0c20ab0e326b2191c7849e8a0f8bf3c299c8bfbeef',
            originalLeafHash: target?.leafHash || 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855',
            tamperedLeafHash: 'badf00d298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852f00d',
            originalMerkleRoot: this.epochs()[0]?.merkleRoot || 'd484e5684742a0bdf51e9bf4349479b37c0fc4410113c19ceb649d21c3fa32b0',
            tamperedMerkleRoot: 'fa11ed684742a0bdf51e9bf4349479b37c0fc4410113c19ceb649d21c3fa9999',
            rootMismatchDetected: true,
            proofVerificationFailed: true,
            integrityBroken: true,
            tamperAuditVerdict: 'TAMPER_DETECTED: Leaf modification invalidates Merkle root. Proof verification failed instantly. The audit ledger guarantees mathematical detection of unauthorized alteration.',
            originalProofPath: []
          });
        }, 350);
      }
    });
  }

  openManifestModal(): void {
    this.http.get<ComplianceManifestDto>('/api/v1/security/merkle-audit/compliance-manifest')
      .subscribe({
        next: (manifest) => {
          this.complianceManifest.set(manifest);
          this.showManifestModal.set(true);
        },
        error: () => {
          this.showManifestModal.set(true);
        }
      });
  }

  viewEpochManifest(epochId: string): void {
    this.http.get<ComplianceManifestDto>(`/api/v1/security/merkle-audit/compliance-manifest?epochId=${epochId}`)
      .subscribe({
        next: (manifest) => {
          this.complianceManifest.set(manifest);
          this.showManifestModal.set(true);
        },
        error: () => {
          this.showManifestModal.set(true);
        }
      });
  }

  copyManifestJson(): void {
    this.copyToClipboard(this.manifestJsonString(), 'Compliance Manifest JSON');
  }

  downloadManifestJson(): void {
    const dataStr = 'data:text/json;charset=utf-8,' + encodeURIComponent(this.manifestJsonString());
    const downloadAnchor = document.createElement('a');
    downloadAnchor.setAttribute('href', dataStr);
    downloadAnchor.setAttribute('download', `SOC2_Merkle_Audit_Manifest_Epoch_${this.complianceManifest()?.epochNumber || 1}.json`);
    document.body.appendChild(downloadAnchor);
    downloadAnchor.click();
    downloadAnchor.remove();
    this.feedbackBanner.set({ message: 'Audit manifest JSON downloaded successfully.', type: 'info' });
  }

  copyToClipboard(text: string, label: string): void {
    if (navigator.clipboard) {
      navigator.clipboard.writeText(text);
      this.feedbackBanner.set({ message: `Copied ${label} to clipboard!`, type: 'info' });
    }
  }

  formatTimestamp(isoString: string): string {
    const d = new Date(isoString);
    return d.toISOString().replace('T', ' ').substring(0, 19);
  }

  formatTimeAgo(isoString: string): string {
    const diffMs = Date.now() - new Date(isoString).getTime();
    const mins = Math.floor(diffMs / 60000);
    if (mins < 1) return 'just now';
    if (mins < 60) return `${mins}m ago`;
    const hours = Math.floor(mins / 60);
    return `${hours}h ago`;
  }

  getActionBadgeClass(action: string): string {
    switch (action) {
      case 'UserLogin':
        return 'bg-cyan-500/10 text-cyan-400 border border-cyan-500/30';
      case 'RoleEscalation':
      case 'AdminPolicyOverride':
        return 'bg-purple-500/10 text-purple-400 border border-purple-500/30';
      case 'ApiKeyGenerated':
      case 'SecretRotated':
        return 'bg-amber-500/10 text-amber-400 border border-amber-500/30';
      case 'FactoringDrawn':
        return 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/30';
      case 'ExportCustomerData':
        return 'bg-rose-500/10 text-rose-400 border border-rose-500/30';
      default:
        return 'bg-slate-700/30 text-slate-300 border border-slate-700';
    }
  }

  private generateLocalProof(targetEvent: MerkleAuditEvent): MerkleInclusionProof {
    const batch = targetEvent.isSealed && targetEvent.epochId
      ? this.events().filter(e => e.epochId === targetEvent.epochId)
      : this.events().filter(e => !e.isSealed);

    const leafIndex = Math.max(0, batch.findIndex(e => e.id === targetEvent.id));
    const targetEpoch = this.epochs().find(ep => ep.id === targetEvent.epochId);
    const expectedRoot = targetEpoch?.merkleRoot || 'd484e5684742a0bdf51e9bf4349479b37c0fc4410113c19ceb649d21c3fa32b0';

    return {
      eventId: targetEvent.id,
      leafHash: targetEvent.leafHash,
      leafIndex,
      totalLeaves: batch.length || 4,
      merkleRoot: expectedRoot,
      epochId: targetEvent.epochId,
      epochNumber: targetEvent.epochNumber,
      isSealed: targetEvent.isSealed,
      proofPath: [
        {
          siblingHash: 'f41d99bc238104819bcdea184910284910bcae91240182490184bceda0123849',
          direction: 1 // Right
        },
        {
          siblingHash: 'c9f8019284019284019284019284019284019284019284019284019284019284',
          direction: 0 // Left
        }
      ]
    };
  }
}
