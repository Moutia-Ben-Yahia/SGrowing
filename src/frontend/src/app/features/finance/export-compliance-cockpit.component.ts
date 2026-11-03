import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface ScreeningItem {
  screeningId: string;
  transactionReference: string;
  destinationCountryIso: string;
  consigneeEntityName: string;
  eccnClassification: string;
  deniedPartiesListCleared: boolean;
  clearanceStatus: string;
}

export interface ClearanceTokenItem {
  tokenId: string;
  transactionReference: string;
  cryptographicSignatureHash: string;
  validUntil: string;
  authorizingOfficer: string;
}

@Component({
  selector: 'app-export-compliance-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="export-compliance-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 159 · Sovereign Defense & Dual-Use Trade</div>
        <h1>Autonomous Sovereign Export Control & Dual-Use EAR/ITAR Compliance Ledger</h1>
        <p class="subtitle">Real-time Commerce Control List (CCL) ECCN screening, automated OFAC SDN verification, and cryptographic export clearance token ledger</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-screened-tx">
          <div class="metric-label">Screened Transactions</div>
          <div class="metric-value">{{ totalScreenedTransactions() }}</div>
          <div class="metric-sub">Cross-border software & dual-use orders</div>
        </div>
        <div class="metric-card" data-testid="metric-clearance-rate">
          <div class="metric-label">Automated Clearance</div>
          <div class="metric-value positive">{{ automatedClearanceRate() }}%</div>
          <div class="metric-sub">Instant cryptographic token grant</div>
        </div>
        <div class="metric-card" data-testid="metric-dual-use-items">
          <div class="metric-label">Dual-Use Items Tracked</div>
          <div class="metric-value">{{ dualUseItemsTracked() }}</div>
          <div class="metric-sub">ECCN Category 5 InfoSec items</div>
        </div>
        <div class="metric-card" data-testid="metric-blocked-sanctioned">
          <div class="metric-label">Sanctioned Blocked</div>
          <div class="metric-value positive">{{ blockedSanctionedAttempts() }}</div>
          <div class="metric-sub">OFAC / BIS Denied Persons prevented</div>
        </div>
      </section>

      <section class="screenings-section">
        <div class="section-header">
          <h2>Export Transaction Screening Ledger</h2>
          <button class="action-btn" (click)="screenNewTransaction()" data-testid="screen-transaction-btn">
            + Screen Transaction
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="screenings-table">
            <thead>
              <tr>
                <th>Screening ID</th>
                <th>Transaction Ref</th>
                <th>Country / Consignee</th>
                <th>ECCN</th>
                <th>Denied List</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              @for (scr of screenings(); track scr.screeningId) {
                <tr [attr.data-testid]="'screening-row-' + scr.screeningId">
                  <td><code>{{ scr.screeningId }}</code></td>
                  <td><strong>{{ scr.transactionReference }}</strong></td>
                  <td class="primary-cell">
                    <strong>{{ scr.consigneeEntityName }}</strong>
                    <span class="sub-text">ISO: {{ scr.destinationCountryIso }}</span>
                  </td>
                  <td><span class="eccn-badge">{{ scr.eccnClassification }}</span></td>
                  <td>
                    @if (scr.deniedPartiesListCleared) {
                      <span class="cleared-badge">CLEARED</span>
                    } @else {
                      <span class="blocked-badge">DENIED</span>
                    }
                  </td>
                  <td><span class="status-pill status-exempt">{{ scr.clearanceStatus }}</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="tokens-section">
        <h2>Active Cryptographic Export Tokens</h2>
        <div class="tokens-grid" data-testid="tokens-grid">
          @for (tok of tokens(); track tok.tokenId) {
            <div class="token-card" [attr.data-testid]="'token-card-' + tok.tokenId">
              <div class="tok-header">
                <strong>{{ tok.tokenId }}</strong>
                <span class="officer-badge">{{ tok.authorizingOfficer }}</span>
              </div>
              <div class="tok-body">
                <p><strong>Transaction:</strong> {{ tok.transactionReference }}</p>
                <p><strong>Signature:</strong> <code>{{ tok.cryptographicSignatureHash }}</code></p>
                <p><strong>Valid:</strong> {{ tok.validUntil }}</p>
              </div>
            </div>
          }
        </div>
      </section>
    </div>
  `,
  styles: [`
    .cockpit-container { padding: 24px; max-width: 1200px; margin: 0 auto; font-family: system-ui, -apple-system, sans-serif; color: #1e293b; }
    .cockpit-header { margin-bottom: 24px; }
    .badge { display: inline-block; padding: 4px 10px; border-radius: 9999px; background: #fef3c7; color: #b45309; font-size: 12px; font-weight: 600; text-transform: uppercase; margin-bottom: 8px; }
    h1 { font-size: 26px; font-weight: 700; margin: 0 0 6px 0; color: #0f172a; }
    .subtitle { font-size: 14px; color: #64748b; margin: 0; }
    .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 16px; margin-bottom: 28px; }
    .metric-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 18px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .metric-label { font-size: 13px; font-weight: 500; color: #64748b; margin-bottom: 6px; }
    .metric-value { font-size: 28px; font-weight: 700; color: #0f172a; }
    .metric-value.positive { color: #16a34a; }
    .metric-sub { font-size: 12px; color: #94a3b8; margin-top: 4px; }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 14px; }
    .action-btn { background: #059669; color: #ffffff; border: none; padding: 8px 14px; border-radius: 4px; font-size: 13px; font-weight: 600; cursor: pointer; }
    .table-wrapper { overflow-x: auto; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; }
    .data-table { width: 100%; border-collapse: collapse; text-align: left; font-size: 13px; }
    .data-table th, .data-table td { padding: 12px 16px; border-bottom: 1px solid #e2e8f0; }
    .data-table th { background: #f8fafc; color: #475569; font-weight: 600; }
    .primary-cell strong { display: block; color: #0f172a; }
    .sub-text { font-size: 11px; color: #64748b; display: block; margin-top: 2px; }
    code { background: #f8fafc; padding: 2px 6px; border-radius: 4px; font-size: 11px; }
    .eccn-badge { background: #e0e7ff; color: #3730a3; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .cleared-badge { background: #dcfce7; color: #15803d; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .blocked-badge { background: #fee2e2; color: #991b1b; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .status-pill { padding: 2px 8px; border-radius: 9999px; font-size: 11px; font-weight: 600; }
    .status-exempt { background: #dcfce7; color: #15803d; }
    .tokens-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 16px; margin-top: 14px; }
    .token-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .tok-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
    .officer-badge { background: #f1f5f9; color: #334155; font-size: 11px; font-weight: 700; padding: 2px 8px; border-radius: 4px; }
    .tok-body p { margin: 4px 0; font-size: 12px; }
  `]
})
export class ExportComplianceCockpitComponent {
  totalScreenedTransactions = signal(422);
  automatedClearanceRate = signal(98.1);
  dualUseItemsTracked = signal(20);
  blockedSanctionedAttempts = signal(2);

  screenings = signal<ScreeningItem[]>([
    {
      screeningId: 'SCR-EXP-001',
      transactionReference: 'TX-CLOUD-INFRA-8801',
      destinationCountryIso: 'DE',
      consigneeEntityName: 'Siemens Energy Global GmbH',
      eccnClassification: '5D002',
      deniedPartiesListCleared: true,
      clearanceStatus: 'CLEARED_EXEMPT'
    },
    {
      screeningId: 'SCR-EXP-002',
      transactionReference: 'TX-AI-QUANTUM-9912',
      destinationCountryIso: 'SG',
      consigneeEntityName: 'DBS Quantum Innovations Pte',
      eccnClassification: '5A002.a',
      deniedPartiesListCleared: true,
      clearanceStatus: 'CLEARED_EXEMPT'
    }
  ]);

  tokens = signal<ClearanceTokenItem[]>([
    {
      tokenId: 'TOK-CLR-001',
      transactionReference: 'TX-CLOUD-INFRA-8801',
      cryptographicSignatureHash: 'SIG-ED25519-77A912FF88C',
      validUntil: '30 Days Remaining',
      authorizingOfficer: 'OFFICER-COMPLIANCE-US-BIS-91'
    }
  ]);

  screenNewTransaction(): void {
    const newScr: ScreeningItem = {
      screeningId: `SCR-EXP-00${this.screenings().length + 1}`,
      transactionReference: 'TX-CRYPTO-ROUTER-9941',
      destinationCountryIso: 'JP',
      consigneeEntityName: 'NTT Communications Corp',
      eccnClassification: '5D002',
      deniedPartiesListCleared: true,
      clearanceStatus: 'CLEARED_EXEMPT'
    };
    this.screenings.update(list => [...list, newScr]);
    this.totalScreenedTransactions.update(v => v + 1);
  }
}
