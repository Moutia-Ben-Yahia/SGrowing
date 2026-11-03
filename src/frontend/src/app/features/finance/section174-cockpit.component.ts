import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface ScheduleItem {
  scheduleId: string;
  taxYear: number;
  jurisdiction: string;
  totalExpenditureUsd: number;
  year1DeductionUsd: number;
  annualSubsequentDeductionUsd: number;
  taxShieldUsd: number;
}

export interface LaborAllocItem {
  allocationId: string;
  sprintId: string;
  squadName: string;
  payrollUsd: number;
  qualifyingRatio: number;
  qualifyingAmountUsd: number;
}

@Component({
  selector: 'app-section174-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="section174-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 174 · Sovereign Tax & R&D Capitalization</div>
        <h1>Autonomous Sovereign R&D Section 174 Amortization & Capitalized Software Ledger</h1>
        <p class="subtitle">TCJA Section 174 mandatory R&D capitalization schedules, US 5-year vs Foreign 15-year mid-year amortization, and dual-basis tax shield accounting</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-active-schedules">
          <div class="metric-label">Active Tax Schedules</div>
          <div class="metric-value positive">{{ activeSchedulesCount() }}</div>
          <div class="metric-sub">Multi-year amortization schedules</div>
        </div>
        <div class="metric-card" data-testid="metric-domestic-rd">
          <div class="metric-label">US Domestic Capitalized</div>
          <div class="metric-value positive">\${{ (totalDomesticUsd() / 1000000).toFixed(1) }}M</div>
          <div class="metric-sub">5-year straight line schedule</div>
        </div>
        <div class="metric-card" data-testid="metric-foreign-rd">
          <div class="metric-label">Foreign R&D Capitalized</div>
          <div class="metric-value">\${{ (totalForeignUsd() / 1000000).toFixed(1) }}M</div>
          <div class="metric-sub">15-year straight line schedule</div>
        </div>
        <div class="metric-card" data-testid="metric-tax-savings">
          <div class="metric-label">Annual Tax Deduction</div>
          <div class="metric-value positive">\${{ (annualDeductionsUsd() / 1000000).toFixed(2) }}M</div>
          <div class="metric-sub">Cash tax shield realized this tax year</div>
        </div>
      </section>

      <section class="schedules-section">
        <div class="section-header">
          <h2>Section 174 Amortization Schedules</h2>
          <button class="action-btn" (click)="createNewSchedule()" data-testid="create-schedule-btn">
            + Create Tax Schedule
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="schedules-table">
            <thead>
              <tr>
                <th>Schedule ID</th>
                <th>Tax Year</th>
                <th>Jurisdiction</th>
                <th>Total Capitalized</th>
                <th>Year 1 Deduction</th>
                <th>Subsequent Annual</th>
                <th>Tax Shield</th>
              </tr>
            </thead>
            <tbody>
              @for (s of schedules(); track s.scheduleId) {
                <tr [attr.data-testid]="'schedule-row-' + s.scheduleId">
                  <td><code>{{ s.scheduleId }}</code></td>
                  <td><strong>{{ s.taxYear }}</strong></td>
                  <td><span class="juris-badge">{{ s.jurisdiction }}</span></td>
                  <td>\${{ (s.totalExpenditureUsd / 1000000).toFixed(1) }}M</td>
                  <td><span class="deduct-pill">\${{ (s.year1DeductionUsd / 1000000).toFixed(2) }}M</span></td>
                  <td>\${{ (s.annualSubsequentDeductionUsd / 1000000).toFixed(2) }}M</td>
                  <td><span class="shield-pill">+\${{ (s.taxShieldUsd / 1000).toFixed(1) }}k</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="allocations-section">
        <h2>Engineering Labor & Cloud Capitalization Allocations</h2>
        <div class="allocations-grid" data-testid="allocations-grid">
          @for (a of allocations(); track a.allocationId) {
            <div class="alloc-card" [attr.data-testid]="'alloc-card-' + a.allocationId">
              <div class="a-header">
                <strong>{{ a.squadName }}</strong>
                <span class="ratio-badge">{{ (a.qualifyingRatio * 100).toFixed(0) }}% R&D</span>
              </div>
              <div class="a-body">
                <p><strong>Sprint:</strong> {{ a.sprintId }}</p>
                <p><strong>Total Payroll:</strong> \${{ a.payrollUsd.toLocaleString() }}</p>
                <p><strong>Capitalized to Section 174:</strong> <span class="cap-val">\${{ a.qualifyingAmountUsd.toLocaleString() }}</span></p>
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
    .badge { display: inline-block; padding: 4px 10px; background: #ecfdf5; color: #047857; border-radius: 9999px; font-size: 12px; font-weight: 600; margin-bottom: 8px; }
    h1 { font-size: 26px; font-weight: 700; margin: 0 0 8px 0; color: #065f46; }
    .subtitle { color: #64748b; font-size: 14px; margin: 0; max-width: 900px; line-height: 1.5; }
    .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 16px; margin-bottom: 28px; }
    .metric-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .metric-label { font-size: 12px; font-weight: 600; text-transform: uppercase; color: #64748b; margin-bottom: 6px; }
    .metric-value { font-size: 28px; font-weight: 800; color: #0f172a; margin-bottom: 4px; }
    .metric-value.positive { color: #059669; }
    .metric-sub { font-size: 12px; color: #94a3b8; }
    .schedules-section, .allocations-section { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 20px; margin-bottom: 24px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    h2 { font-size: 18px; font-weight: 700; margin: 0; color: #1e293b; }
    .action-btn { background: #059669; color: #ffffff; border: none; border-radius: 6px; padding: 8px 14px; font-size: 13px; font-weight: 600; cursor: pointer; transition: background 0.15s; }
    .action-btn:hover { background: #047857; }
    .table-wrapper { overflow-x: auto; }
    .data-table { width: 100%; border-collapse: collapse; font-size: 13px; text-align: left; }
    .data-table th { background: #f8fafc; padding: 10px 14px; font-weight: 600; color: #475569; border-bottom: 1px solid #e2e8f0; }
    .data-table td { padding: 12px 14px; border-bottom: 1px solid #f1f5f9; vertical-align: middle; }
    .primary-cell strong { color: #0f172a; }
    .juris-badge { background: #e0f2fe; color: #0369a1; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .deduct-pill { color: #059669; font-weight: 700; }
    .shield-pill { background: #dcfce7; color: #15803d; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .allocations-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 16px; margin-top: 14px; }
    .alloc-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .a-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
    .ratio-badge { background: #ecfdf5; color: #047857; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .a-body p { margin: 4px 0; font-size: 12px; }
    .cap-val { color: #059669; font-weight: 700; }
  `]
})
export class Section174CockpitComponent {
  activeSchedulesCount = signal(2);
  totalDomesticUsd = signal(14500000);
  totalForeignUsd = signal(4200000);
  annualDeductionsUsd = signal(1590000);

  schedules = signal<ScheduleItem[]>([
    {
      scheduleId: 'SCHED-174-2025-US',
      taxYear: 2025,
      jurisdiction: 'UnitedStatesDomestic5Year',
      totalExpenditureUsd: 14500000,
      year1DeductionUsd: 1450000,
      annualSubsequentDeductionUsd: 2900000,
      taxShieldUsd: 304500
    },
    {
      scheduleId: 'SCHED-174-2025-FOR',
      taxYear: 2025,
      jurisdiction: 'ForeignResearch15Year',
      totalExpenditureUsd: 4200000,
      year1DeductionUsd: 140000,
      annualSubsequentDeductionUsd: 280000,
      taxShieldUsd: 29400
    }
  ]);

  allocations = signal<LaborAllocItem[]>([
    {
      allocationId: 'ALLOC-LABOR-001',
      sprintId: 'SPRINT-2026-W34',
      squadName: 'Platform Core Mesh & eBPF Networking',
      payrollUsd: 850000,
      qualifyingRatio: 0.92,
      qualifyingAmountUsd: 782000
    }
  ]);

  createNewSchedule(): void {
    const newSched: ScheduleItem = {
      scheduleId: `SCHED-174-2026-US`,
      taxYear: 2026,
      jurisdiction: 'UnitedStatesDomestic5Year',
      totalExpenditureUsd: 18000000,
      year1DeductionUsd: 1800000,
      annualSubsequentDeductionUsd: 3600000,
      taxShieldUsd: 378000
    };
    this.schedules.update(list => [...list, newSched]);
    this.activeSchedulesCount.update(v => v + 1);
  }
}
