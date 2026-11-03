import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface InvoiceItem {
  invoiceId: string;
  debtorName: string;
  creditorName: string;
  amountUsd: number;
  category: string;
  status: string;
}

export interface ParticipantItem {
  subsidiaryName: string;
  grossPayableUsd: number;
  grossReceivableUsd: number;
  netPositionUsd: number;
}

@Component({
  selector: 'app-intercompany-netting-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="intercompany-netting-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 179 · Global Treasury & Transfer Pricing</div>
        <h1>Autonomous Real-Time Intercompany Trade Settlement & Multilateral Netting Ledger</h1>
        <p class="subtitle">Matrix debt netting across multinational subsidiaries, reducing cross-border banking FX and settlement fees by >80% with cryptographic audit receipts</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-subsidiaries-count">
          <div class="metric-label">Participating Subsidiaries</div>
          <div class="metric-value positive">{{ subsidiariesCount() }}</div>
          <div class="metric-sub">Global corporate legal entities</div>
        </div>
        <div class="metric-card" data-testid="metric-gross-volume">
          <div class="metric-label">Gross Invoiced Volume</div>
          <div class="metric-value">\${{ (grossVolumeUsd() / 1000000).toFixed(1) }}M</div>
          <div class="metric-sub">Total intercompany invoices active</div>
        </div>
        <div class="metric-card" data-testid="metric-net-required">
          <div class="metric-label">Cash Settlement Required</div>
          <div class="metric-value positive">\${{ (netRequiredUsd() / 1000000).toFixed(1) }}M</div>
          <div class="metric-sub">After multilateral netting cycle</div>
        </div>
        <div class="metric-card" data-testid="metric-netting-efficiency">
          <div class="metric-label">Settlement Efficiency</div>
          <div class="metric-value positive">{{ nettingEfficiencyPct() }}%</div>
          <div class="metric-sub">Eliminated cross-border wire volume</div>
        </div>
      </section>

      <section class="invoices-section">
        <div class="section-header">
          <h2>Intercompany Invoices Pending Netting</h2>
          <button class="action-btn" (click)="addIntercompanyInvoice()" data-testid="add-invoice-btn">
            + Add Intercompany Invoice
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="invoices-table">
            <thead>
              <tr>
                <th>Invoice ID</th>
                <th>Debtor Entity</th>
                <th>Creditor Entity</th>
                <th>Amount (USD)</th>
                <th>Transfer Pricing Category</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              @for (i of invoices(); track i.invoiceId) {
                <tr [attr.data-testid]="'invoice-row-' + i.invoiceId">
                  <td><code>{{ i.invoiceId }}</code></td>
                  <td>{{ i.debtorName }}</td>
                  <td class="primary-cell">
                    <strong>{{ i.creditorName }}</strong>
                  </td>
                  <td><strong>\${{ (i.amountUsd / 1000000).toFixed(2) }}M</strong></td>
                  <td><span class="tp-badge">{{ i.category }}</span></td>
                  <td><span class="status-pill status-pending">{{ i.status }}</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="participants-section">
        <h2>Subsidiary Net Positions (Post-Matrix Netting)</h2>
        <div class="participants-grid" data-testid="participants-grid">
          @for (p of participants(); track p.subsidiaryName) {
            <div class="p-card" [attr.data-testid]="'p-card-' + p.subsidiaryName">
              <div class="p-header">
                <strong>{{ p.subsidiaryName }}</strong>
                @if (p.netPositionUsd >= 0) {
                  <span class="net-creditor">NET CREDITOR</span>
                } @else {
                  <span class="net-debtor">NET DEBTOR</span>
                }
              </div>
              <div class="p-body">
                <p><strong>Gross Payables:</strong> \${{ (p.grossPayableUsd / 1000000).toFixed(2) }}M</p>
                <p><strong>Gross Receivables:</strong> \${{ (p.grossReceivableUsd / 1000000).toFixed(2) }}M</p>
                <p><strong>Net Wire Required:</strong> <span class="net-wire">\${{ (p.netPositionUsd / 1000000).toFixed(2) }}M</span></p>
              </div>
            </div>
          }
        </div>
      </section>
    </div>
  `,
  styles: [`
    .cockpit-container { padding: 24px; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; background: #f8fafc; min-height: 100vh; color: #0f172a; }
    .cockpit-header { margin-bottom: 24px; }
    .badge { display: inline-block; padding: 4px 10px; background: #ecfdf5; color: #065f46; border-radius: 9999px; font-size: 12px; font-weight: 600; margin-bottom: 8px; }
    h1 { font-size: 26px; font-weight: 700; margin: 0 0 8px 0; color: #064e3b; }
    .subtitle { color: #64748b; font-size: 14px; margin: 0; max-width: 900px; line-height: 1.5; }
    .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 16px; margin-bottom: 28px; }
    .metric-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .metric-label { font-size: 12px; font-weight: 600; text-transform: uppercase; color: #64748b; margin-bottom: 6px; }
    .metric-value { font-size: 28px; font-weight: 800; color: #0f172a; margin-bottom: 4px; }
    .metric-value.positive { color: #059669; }
    .metric-sub { font-size: 12px; color: #94a3b8; }
    .invoices-section, .participants-section { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 20px; margin-bottom: 24px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    h2 { font-size: 18px; font-weight: 700; margin: 0; color: #1e293b; }
    .action-btn { background: #059669; color: #ffffff; border: none; border-radius: 6px; padding: 8px 14px; font-size: 13px; font-weight: 600; cursor: pointer; transition: background 0.15s; }
    .action-btn:hover { background: #047857; }
    .table-wrapper { overflow-x: auto; }
    .data-table { width: 100%; border-collapse: collapse; font-size: 13px; text-align: left; }
    .data-table th { background: #f8fafc; padding: 10px 14px; font-weight: 600; color: #475569; border-bottom: 1px solid #e2e8f0; }
    .data-table td { padding: 12px 14px; border-bottom: 1px solid #f1f5f9; vertical-align: middle; }
    .primary-cell strong { color: #0f172a; }
    .tp-badge { background: #f1f5f9; color: #334155; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .status-pill { padding: 3px 8px; border-radius: 9999px; font-size: 11px; font-weight: 600; }
    .status-pending { background: #fef9c3; color: #854d0e; }
    .participants-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 16px; margin-top: 14px; }
    .p-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .p-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
    .net-creditor { background: #dcfce7; color: #15803d; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .net-debtor { background: #fee2e2; color: #b91c1c; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .p-body p { margin: 4px 0; font-size: 12px; }
    .net-wire { font-weight: 800; color: #0f172a; }
  `]
})
export class IntercompanyNettingCockpitComponent {
  subsidiariesCount = signal(3);
  grossVolumeUsd = signal(9500000);
  netRequiredUsd = signal(1900000);
  nettingEfficiencyPct = signal(80.0);

  invoices = signal<InvoiceItem[]>([
    {
      invoiceId: 'IC-INV-001',
      debtorName: 'SGrowing Technologies Inc (US)',
      creditorName: 'SGrowing International Limited (Ireland)',
      amountUsd: 4500000,
      category: 'IP_ROYALTY_COST_SHARING',
      status: 'PendingInvoiced'
    },
    {
      invoiceId: 'IC-INV-002',
      debtorName: 'SGrowing International Limited (Ireland)',
      creditorName: 'SGrowing Technologies Inc (US)',
      amountUsd: 3800000,
      category: 'MANAGEMENT_SHARED_SERVICES',
      status: 'PendingInvoiced'
    }
  ]);

  participants = signal<ParticipantItem[]>([
    {
      subsidiaryName: 'SGrowing Technologies Inc (US)',
      grossPayableUsd: 4500000,
      grossReceivableUsd: 3800000,
      netPositionUsd: -700000
    },
    {
      subsidiaryName: 'SGrowing International Limited (Ireland)',
      grossPayableUsd: 3800000,
      grossReceivableUsd: 4500000,
      netPositionUsd: 700000
    }
  ]);

  addIntercompanyInvoice(): void {
    const newInv: InvoiceItem = {
      invoiceId: `IC-INV-00${this.invoices().length + 1}`,
      debtorName: 'SGrowing Japan KK (Tokyo)',
      creditorName: 'SGrowing Technologies Inc (US)',
      amountUsd: 1200000,
      category: 'CLOUD_HOSTING_INFRASTRUCTURE',
      status: 'PendingInvoiced'
    };
    this.invoices.update(list => [...list, newInv]);
  }
}
