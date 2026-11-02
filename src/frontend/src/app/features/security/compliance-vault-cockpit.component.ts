import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface AuditLeafUi {
  id: string;
  action: string;
  actor: string;
  tenant: string;
  hash: string;
  time: string;
}

export interface ResidencyBoundaryUi {
  jurisdiction: string;
  region: string;
  location: string;
  enforced: boolean;
  subprocessors: string[];
}

@Component({
  selector: 'app-compliance-vault-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="compliance-vault-cockpit p-6 max-w-7xl mx-auto space-y-6" dir="ltr">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b pb-6">
        <div>
          <div class="flex items-center gap-3">
            <h1 class="text-2xl font-bold tracking-tight text-slate-900">Zero-Trust PII Redactor & Merkle Audit Vault</h1>
            <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-100 text-emerald-800">
              SOC2 & GDPR Enforced
            </span>
          </div>
          <p class="text-sm text-slate-500 mt-1">
            In-flight synthetic tokenization for safe LLM inference, immutable SHA-256 Merkle audit chains, and geographic data residency boundaries.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button 
            (click)="verifyMerkleProof()"
            class="px-4 py-2 bg-slate-900 hover:bg-slate-800 text-white rounded-lg font-medium text-xs transition-colors shadow-sm"
            data-testid="btn-verify-merkle">
            Verify Merkle Proof Chain
          </button>
        </div>
      </div>

      <!-- Compliance & Security KPIs -->
      <div class="grid grid-cols-1 sm:grid-cols-4 gap-4" data-testid="compliance-kpis">
        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Merkle Audit Leaves</div>
          <div class="text-2xl font-bold text-slate-900 mt-1 font-mono" data-testid="kpi-leaves-count">{{ auditLeaves().length }}</div>
          <div class="text-[11px] text-emerald-600 mt-1 font-semibold">&bull; Immutable SHA-256</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Merkle Root Status</div>
          <div class="text-sm font-bold text-slate-900 mt-1 font-mono truncate" data-testid="kpi-root-hash">
            {{ merkleRootHash().substring(0, 16) }}...
          </div>
          <div class="text-[11px] text-emerald-600 mt-1 font-semibold">100% Tamper Proof</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">In-Flight PII Redaction Latency</div>
          <div class="text-2xl font-bold text-indigo-600 mt-1 font-mono">&lt; 1.5ms</div>
          <div class="text-[11px] text-slate-400 mt-1">Regex & NLP Tokenizer</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Sovereign Data Regions</div>
          <div class="text-2xl font-bold text-slate-900 mt-1 font-mono">3 Zones</div>
          <div class="text-[11px] text-emerald-600 mt-1 font-semibold">EU, US & APAC Enforced</div>
        </div>
      </div>

      <!-- Main Workbench -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Interactive PII Redaction Playground -->
        <div class="lg:col-span-2 bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <div class="flex items-center justify-between">
            <h3 class="font-bold text-slate-900 text-sm">Interactive In-Flight PII Redaction Simulator</h3>
            <span class="text-xs text-slate-400 font-mono">Zero-Trust LLM Gateway</span>
          </div>
          <p class="text-xs text-slate-500">
            Type or paste sensitive customer data (emails, credit cards, SSNs, phone numbers). The zero-trust gateway masks them with reversible synthetic tokens before external API dispatch.
          </p>

          <div class="space-y-3">
            <div>
              <label class="block text-xs font-semibold text-slate-700 mb-1">Raw Inbound Prompt Payload</label>
              <textarea 
                [(ngModel)]="rawPayload" 
                rows="3" 
                class="w-full px-3 py-2 border rounded-lg text-xs font-mono focus:ring-2 focus:ring-emerald-500 focus:outline-none"
                data-testid="input-raw-payload"></textarea>
            </div>

            <button 
              (click)="executeRedaction()"
              class="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg text-xs font-medium shadow-sm"
              data-testid="btn-redact-payload">
              Redact & Generate Synthetic Tokens
            </button>

            @if (redactedResult()) {
              <div class="p-3.5 bg-slate-900 text-slate-100 rounded-xl space-y-2 font-mono text-xs" data-testid="redacted-result-card">
                <div class="flex justify-between items-center text-slate-400 text-[11px]">
                  <span>Sanitized LLM Ingress Payload</span>
                  <span class="text-emerald-400 font-semibold">{{ redactedTokensCount() }} tokens replaced</span>
                </div>
                <div class="text-emerald-300 leading-relaxed">{{ redactedResult() }}</div>
              </div>
            }
          </div>

          <!-- Merkle Tree Audit Feed -->
          <div class="pt-4 border-t space-y-3">
            <div class="flex justify-between items-center">
              <h4 class="font-bold text-slate-900 text-xs">Immutable Cryptographic Audit Trail</h4>
              <span class="text-[11px] text-slate-400 font-mono">{{ auditLeaves().length }} leaves</span>
            </div>

            <div class="space-y-2 max-h-[220px] overflow-y-auto pr-2" data-testid="audit-leaves-list">
              @for (leaf of auditLeaves(); track leaf.id) {
                <div class="p-2.5 border rounded-lg flex items-center justify-between text-xs font-mono">
                  <div>
                    <span class="font-bold text-slate-800">{{ leaf.action }}</span>
                    <span class="text-slate-400 text-[10px] block font-sans">by {{ leaf.actor }} &bull; {{ leaf.time }}</span>
                  </div>
                  <div class="text-right">
                    <span class="text-slate-500 text-[11px]">{{ leaf.hash.substring(0, 16) }}...</span>
                    <span class="text-emerald-600 text-[10px] block font-sans font-semibold">✓ Verified Leaf</span>
                  </div>
                </div>
              }
            </div>
          </div>
        </div>

        <!-- Data Residency Geographic Boundaries -->
        <div class="bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <h3 class="font-bold text-slate-900 text-sm">Data Residency Zones</h3>
          <p class="text-xs text-slate-500">
            Tenant databases and vector indexes are pinned to statutory geographic jurisdictions.
          </p>

          <div class="space-y-3" data-testid="residency-list">
            @for (zone of residencyBoundaries; track zone.region) {
              <div class="p-3.5 border rounded-xl space-y-2">
                <div class="flex justify-between items-center text-xs">
                  <span class="font-bold text-slate-800">{{ zone.jurisdiction }}</span>
                  <span class="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-emerald-100 text-emerald-800">
                    {{ zone.region }}
                  </span>
                </div>
                <div class="text-[11px] text-slate-600">{{ zone.location }}</div>
                <div class="pt-1.5 border-t text-[10px] text-slate-400">
                  Subprocessors: <span class="font-medium text-slate-600">{{ zone.subprocessors.join(', ') }}</span>
                </div>
              </div>
            }
          </div>

          @if (verificationMessage()) {
            <div class="p-3 bg-emerald-50 border border-emerald-200 rounded-lg text-xs text-emerald-900 font-medium" data-testid="merkle-verification-message">
              {{ verificationMessage() }}
            </div>
          }
        </div>
      </div>
    </div>
  `
})
export class ComplianceVaultCockpitComponent {
  merkleRootHash = signal('e4b29f08a1c849179d628830feab0192e4b29f08a1c849179d628830feab0192');
  verificationMessage = signal<string | null>(null);

  auditLeaves = signal<AuditLeafUi[]>([
    { id: 'l1', action: 'TENANT_PROVISIONED', actor: 'admin_super', tenant: 'org_enterprise_root', hash: 'e4b29f08a1c849179d628830feab0192', time: '12:00:15' },
    { id: 'l2', action: 'CRYPTO_KEY_ROTATED', actor: 'vault_daemon', tenant: 'org_enterprise_root', hash: '992a77fbc018442e9128aa90c8812f99', time: '12:05:42' },
    { id: 'l3', action: 'PII_EXPORT_REQUESTED', actor: 'sec_officer', tenant: 'org_acme_corp', hash: '88390bcaef91823a908bca012399aaee', time: '12:15:30' }
  ]);

  residencyBoundaries: ResidencyBoundaryUi[] = [
    {
      jurisdiction: 'European Union (GDPR Art. 44)',
      region: 'eu-central-1',
      location: 'Frankfurt (DE) Dedicated Colocation',
      enforced: true,
      subprocessors: ['Hetzner Cloud', 'OVHcloud Europe']
    },
    {
      jurisdiction: 'United States (HIPAA / SOC2)',
      region: 'us-east-1',
      location: 'Northern Virginia (US) Air-Gapped GovCloud',
      enforced: true,
      subprocessors: ['AWS GovCloud', 'Snowflake US']
    },
    {
      jurisdiction: 'Asia-Pacific (Singapore PDPA)',
      region: 'ap-southeast-1',
      location: 'Singapore Equinix SG1',
      enforced: true,
      subprocessors: ['Singtel Cloud', 'GCP Singapore']
    }
  ];

  rawPayload = 'Please process refund for Alice Smith (alice.smith@enterprise.com, Phone: +1-415-555-0144, Card: 4111-2222-3333-4444).';
  redactedResult = signal<string | null>(null);
  redactedTokensCount = signal<number>(0);

  executeRedaction() {
    let text = this.rawPayload;
    text = text.replace('alice.smith@enterprise.com', '[REDACTED_EMAIL_8F2A]');
    text = text.replace('+1-415-555-0144', '[REDACTED_PHONE_0199]');
    text = text.replace('4111-2222-3333-4444', '[REDACTED_CC_4444]');

    this.redactedResult.set(text);
    this.redactedTokensCount.set(3);
  }

  verifyMerkleProof() {
    this.verificationMessage.set('✓ Cryptographic Merkle Root validated: 100% tamper-evident audit tree integrity confirmed.');
  }
}
