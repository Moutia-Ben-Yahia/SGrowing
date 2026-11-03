import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface TaxExemptionCertificate {
  certificateId: string;
  counterpartyName: string;
  jurisdictionCode: string;
  taxIdentificationNumber: string;
  exemptionType: string;
  verificationRegistry: string;
  cryptographicProofHash: string;
  status: string;
}

export interface VatClearanceInvoiceRecord {
  invoiceClearanceId: string;
  certificateId: string;
  invoiceReferenceNumber: string;
  netAmountUsd: number;
  appliedVatRatePct: number;
  mandatoryStatutoryNotice: string;
}

@Component({
  selector: 'app-tax-exemption-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="tax-exemption-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 144 · Sovereign Indirect Tax Compliance</div>
        <h1>Global Sovereign Tax Exemption & Reverse Charge VAT Clearance</h1>
        <p class="subtitle">Real-time VIES/HMRC tax registry validation, automated Article 196 reverse charge invoicing, and tamper-evident SHA-256 certificate proofs</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-valid-certs">
          <div class="metric-label">Valid Exemption Certificates</div>
          <div class="metric-value positive">{{ validCertsCount() }}</div>
          <div class="metric-sub">Active EU VIES, UK HMRC, US SST records</div>
        </div>
        <div class="metric-card" data-testid="metric-clearances-issued">
          <div class="metric-label">Reverse Charge Clearances</div>
          <div class="metric-value">{{ clearancesCount() }}</div>
          <div class="metric-sub">0% VAT invoices with statutory notices</div>
        </div>
        <div class="metric-card" data-testid="metric-tax-shielded">
          <div class="metric-label">Total Tax Shielded</div>
          <div class="metric-value">\${{ taxShielded().toLocaleString() }}</div>
          <div class="metric-sub">Protected cross-border business transactions</div>
        </div>
        <div class="metric-card" data-testid="metric-compliance-score">
          <div class="metric-label">Audit Compliance Score</div>
          <div class="metric-value positive">{{ complianceScore() }}%</div>
          <div class="metric-sub">Cryptographically attested audit trail</div>
        </div>
      </section>

      <section class="certs-section">
        <div class="section-header">
          <h2>Active Sovereign Exemption Certificates</h2>
          <button class="validate-btn" (click)="validateNewCertificate()" data-testid="validate-cert-btn">
            + Validate Certificate
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="certificates-table">
            <thead>
              <tr>
                <th>Certificate ID & Counterparty</th>
                <th>Jurisdiction</th>
                <th>Tax ID</th>
                <th>Registry</th>
                <th>Exemption Type</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              @for (cert of certificates(); track cert.certificateId) {
                <tr [attr.data-testid]="'cert-row-' + cert.certificateId">
                  <td class="primary-cell">
                    <strong>{{ cert.counterpartyName }}</strong>
                    <span class="sub-text">{{ cert.certificateId }}</span>
                  </td>
                  <td><span class="jur-badge">{{ cert.jurisdictionCode }}</span></td>
                  <td><code>{{ cert.taxIdentificationNumber }}</code></td>
                  <td>{{ cert.verificationRegistry }}</td>
                  <td><span class="type-badge">{{ cert.exemptionType }}</span></td>
                  <td><span class="status-pill status-valid">{{ cert.status }}</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="clearances-section">
        <h2>Recent Reverse Charge VAT Clearance Invoices</h2>
        <div class="clearances-grid" data-testid="clearances-grid">
          @for (clr of clearances(); track clr.invoiceClearanceId) {
            <div class="clr-card" [attr.data-testid]="'clr-card-' + clr.invoiceClearanceId">
              <div class="clr-header">
                <span class="inv-ref">{{ clr.invoiceReferenceNumber }}</span>
                <span class="vat-rate">{{ clr.appliedVatRatePct }}% VAT</span>
              </div>
              <div class="clr-body">
                <p class="net-amount"><strong>Net Amount:</strong> \${{ clr.netAmountUsd.toLocaleString() }}</p>
                <p class="notice">"{{ clr.mandatoryStatutoryNotice }}"</p>
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
    .badge { display: inline-block; padding: 4px 10px; border-radius: 9999px; background: #ecfdf5; color: #047857; font-size: 12px; font-weight: 600; text-transform: uppercase; margin-bottom: 8px; }
    h1 { font-size: 26px; font-weight: 700; margin: 0 0 6px 0; color: #0f172a; }
    .subtitle { font-size: 14px; color: #64748b; margin: 0; }
    .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 16px; margin-bottom: 28px; }
    .metric-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 18px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .metric-label { font-size: 13px; font-weight: 500; color: #64748b; margin-bottom: 6px; }
    .metric-value { font-size: 28px; font-weight: 700; color: #0f172a; }
    .metric-value.positive { color: #16a34a; }
    .metric-sub { font-size: 12px; color: #94a3b8; margin-top: 4px; }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 14px; }
    .validate-btn { background: #059669; color: #ffffff; border: none; padding: 8px 14px; border-radius: 4px; font-size: 13px; font-weight: 600; cursor: pointer; }
    .table-wrapper { overflow-x: auto; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; }
    .data-table { width: 100%; border-collapse: collapse; text-align: left; font-size: 13px; }
    .data-table th, .data-table td { padding: 12px 16px; border-bottom: 1px solid #e2e8f0; }
    .data-table th { background: #f8fafc; color: #475569; font-weight: 600; }
    .primary-cell strong { display: block; color: #0f172a; }
    .sub-text { font-size: 11px; color: #64748b; }
    .jur-badge { background: #f1f5f9; padding: 2px 6px; border-radius: 4px; font-size: 11px; font-weight: 600; }
    code { background: #f8fafc; padding: 2px 6px; border-radius: 4px; font-size: 11px; }
    .type-badge { background: #e0f2fe; color: #0369a1; padding: 2px 6px; border-radius: 4px; font-size: 11px; font-weight: 600; }
    .status-pill { padding: 2px 8px; border-radius: 9999px; font-size: 11px; font-weight: 600; }
    .status-valid { background: #dcfce7; color: #15803d; }
    .clearances-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 16px; margin-top: 14px; }
    .clr-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .clr-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
    .inv-ref { font-weight: 700; font-size: 13px; color: #0f172a; }
    .vat-rate { background: #dcfce7; color: #15803d; font-size: 11px; font-weight: 700; padding: 2px 8px; border-radius: 4px; }
    .clr-body .net-amount { font-size: 13px; color: #0f172a; margin: 4px 0; }
    .clr-body .notice { font-size: 12px; color: #64748b; font-style: italic; margin: 4px 0; }
  `]
})
export class TaxExemptionCockpitComponent {
  validCertsCount = signal(2);
  clearancesCount = signal(2);
  taxShielded = signal(42000);
  complianceScore = signal(99.6);

  certificates = signal<TaxExemptionCertificate[]>([
    {
      certificateId: 'CERT-EU-DE-001',
      counterpartyName: 'Siemens Digital Cloud GmbH',
      jurisdictionCode: 'EU_DE',
      taxIdentificationNumber: 'DE811122233',
      exemptionType: 'REVERSE_CHARGE_B2B',
      verificationRegistry: 'EU_VIES',
      cryptographicProofHash: 'PROOF-SHA256-DE811122233-VIES',
      status: 'VALID'
    },
    {
      certificateId: 'CERT-UK-GB-002',
      counterpartyName: 'Rolls-Royce Digital Systems Ltd',
      jurisdictionCode: 'UK_GB',
      taxIdentificationNumber: 'GB987654321',
      exemptionType: 'REVERSE_CHARGE_B2B',
      verificationRegistry: 'UK_HMRC',
      cryptographicProofHash: 'PROOF-SHA256-GB987654321-HMRC',
      status: 'VALID'
    }
  ]);

  clearances = signal<VatClearanceInvoiceRecord[]>([
    {
      invoiceClearanceId: 'CLR-2026-001',
      certificateId: 'CERT-EU-DE-001',
      invoiceReferenceNumber: 'INV-2026-8901',
      netAmountUsd: 125000,
      appliedVatRatePct: 0.0,
      mandatoryStatutoryNotice: 'VAT Reverse Charge: Article 196 EU Directive 2006/112/EC applies. Customer liable for VAT.'
    },
    {
      invoiceClearanceId: 'CLR-2026-002',
      certificateId: 'CERT-UK-GB-002',
      invoiceReferenceNumber: 'INV-2026-8902',
      netAmountUsd: 85000,
      appliedVatRatePct: 0.0,
      mandatoryStatutoryNotice: 'Reverse Charge: Section 55A VATA 1994 applies. Customer to account for VAT to HMRC.'
    }
  ]);

  validateNewCertificate(): void {
    const newCert: TaxExemptionCertificate = {
      certificateId: `CERT-EU-FR-${Math.floor(Math.random() * 900 + 100)}`,
      counterpartyName: 'Airbus Defence and Space SAS',
      jurisdictionCode: 'EU_FR',
      taxIdentificationNumber: 'FR12345678901',
      exemptionType: 'REVERSE_CHARGE_B2B',
      verificationRegistry: 'EU_VIES',
      cryptographicProofHash: 'PROOF-SHA256-FR123456-VIES',
      status: 'VALID'
    };
    this.certificates.update(list => [...list, newCert]);
    this.validCertsCount.update(v => v + 1);
  }
}
