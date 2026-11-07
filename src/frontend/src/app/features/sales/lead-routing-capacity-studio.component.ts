import { Component, signal, computed, ChangeDetectionStrategy, Optional, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslationService } from '../../core/services/translation.service';

export type SdrAvailabilityStatus = 'Online' | 'Busy' | 'Away' | 'Offline';
export type LeadSlaBreachStatus = 'WithinSla' | 'Warning' | 'Breached' | 'Reassigned';

export interface SdrRepProfile {
  sdrId: string;
  tenantId: string;
  repName: string;
  repEmail: string;
  status: SdrAvailabilityStatus;
  maxCapacity: number;
  currentActiveLeads: number;
  quotaWeight: number;
  assignedTerritories: string[];
  skills: string[];
  lastAssignedAtUtc: string;
}

export interface LeadAssignmentRecord {
  assignmentId: string;
  tenantId: string;
  leadId: string;
  leadName: string;
  leadEmail: string;
  companyName: string;
  territory: string;
  sdrId: string;
  sdrName: string;
  assignedAtUtc: string;
  slaDueAtUtc: string;
  slaStatus: LeadSlaBreachStatus;
  firstContactedAtUtc: string | null;
  reassignedFromSdrId: string | null;
}

export interface RoutingDecision {
  assignedSdr: SdrRepProfile;
  matchTier: 'TerritoryAndSkill' | 'TerritoryOnly' | 'SkillOnly' | 'CapacityOverflow';
  rationale: string;
  assignment: LeadAssignmentRecord;
}

@Component({
  selector: 'app-lead-routing-capacity-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="studio-container" dir="ltr" data-testid="lead-routing-capacity-studio">
      <!-- Header -->
      <header class="studio-header">
        <div class="header-titles">
          <div class="badge-pill">PRD Domain F1 & B1 · Autonomous Inbound Routing</div>
          <h1>Autonomous Lead Routing & SDR Capacity Balancer</h1>
          <p class="subtitle">
            Real-Time SLA Radar, Intelligent Skill/Territory Matching & Capacity Load Arbiter
          </p>
        </div>
        <div class="header-actions">
          <button class="btn btn-secondary" (click)="checkAndTriggerSlaBreaches()" data-testid="check-sla-btn">
            <span class="icon">⚡</span> Evaluate SLA Radar
          </button>
          <button class="btn btn-outline" (click)="resetToDefaults()" data-testid="reset-btn">
            <span class="icon">↺</span> Reset State
          </button>
        </div>
      </header>

      <!-- KPI Metrics Bar -->
      <section class="kpi-grid">
        <div class="kpi-card" data-testid="kpi-online-sdrs">
          <div class="kpi-label">Active / Online SDRs</div>
          <div class="kpi-value text-accent">{{ onlineSdrCount() }} <span class="kpi-sub">/ {{ totalSdrCount() }} Reps</span></div>
          <div class="kpi-meta">Capacity pool readiness: {{ readinessRate() }}%</div>
        </div>

        <div class="kpi-card" data-testid="kpi-active-leads">
          <div class="kpi-label">Active Routed Leads</div>
          <div class="kpi-value text-primary">{{ totalActiveRoutedLeads() }}</div>
          <div class="kpi-meta">Currently in SLA pursuit queue</div>
        </div>

        <div class="kpi-card" data-testid="kpi-sla-compliance">
          <div class="kpi-label">SLA Compliance Rate</div>
          <div class="kpi-value" [ngClass]="slaCompliancePercent() >= 80 ? 'text-success' : 'text-danger'">
            {{ slaCompliancePercent() }}%
          </div>
          <div class="kpi-meta">15-min first-touch target</div>
        </div>

        <div class="kpi-card" data-testid="kpi-avg-contact-time">
          <div class="kpi-label">Avg First Contact Time</div>
          <div class="kpi-value text-info">{{ avgFirstContactMinutes() }} <span class="kpi-sub">min</span></div>
          <div class="kpi-meta">Target: &lt; 15.0 min</div>
        </div>
      </section>

      <!-- Main Layout: 2 Columns (Roster & Radar, plus Simulator) -->
      <div class="studio-grid">
        <!-- Left: SDR Roster & Capacity Balancer -->
        <section class="card roster-card">
          <div class="card-header">
            <div>
              <h2>Live SDR Roster & Capacity Grid</h2>
              <p class="section-desc">Weighted quota capacity meters and real-time availability controls</p>
            </div>
            <span class="badge badge-neutral">{{ sdrs().length }} Reps Registered</span>
          </div>

          <div class="roster-list">
            @for (sdr of sdrs(); track sdr.sdrId) {
              @let loadPct = Math.round((sdr.currentActiveLeads / sdr.maxCapacity) * 100);
              <div class="sdr-card" [attr.data-testid]="'sdr-card-' + sdr.sdrId">
                <div class="sdr-top">
                  <div class="sdr-identity">
                    <h3>{{ sdr.repName }}</h3>
                    <span class="sdr-email">{{ sdr.repEmail }}</span>
                  </div>
                  <div class="sdr-status-controls">
                    <span class="status-pill" [ngClass]="'status-' + sdr.status.toLowerCase()">
                      {{ sdr.status }}
                    </span>
                    <select
                      class="status-select"
                      [value]="sdr.status"
                      (change)="updateRepStatus(sdr.sdrId, $any($event.target).value)"
                      [attr.data-testid]="'status-select-' + sdr.sdrId">
                      <option value="Online">Online</option>
                      <option value="Busy">Busy</option>
                      <option value="Away">Away</option>
                      <option value="Offline">Offline</option>
                    </select>
                  </div>
                </div>

                <!-- Capacity Meter -->
                <div class="capacity-meter">
                  <div class="meter-labels">
                    <span>Active Load: <strong>{{ sdr.currentActiveLeads }} / {{ sdr.maxCapacity }}</strong> leads</span>
                    <span class="load-badge" [ngClass]="getLoadBadgeClass(loadPct)">{{ loadPct }}% Full</span>
                  </div>
                  <div class="meter-track">
                    <div
                      class="meter-fill"
                      [style.width.%]="Math.min(100, loadPct)"
                      [ngClass]="getLoadBarClass(loadPct)"></div>
                  </div>
                </div>

                <!-- Rep Details: Territories & Skills -->
                <div class="sdr-meta-grid">
                  <div class="meta-item">
                    <span class="meta-label">Quota Weight:</span>
                    <span class="meta-value">{{ sdr.quotaWeight }}x</span>
                  </div>
                  <div class="meta-item">
                    <span class="meta-label">Territories:</span>
                    <div class="tag-row">
                      @for (t of sdr.assignedTerritories; track t) {
                        <span class="tag-chip tag-territory">{{ t }}</span>
                      }
                    </div>
                  </div>
                  <div class="meta-item">
                    <span class="meta-label">Skills:</span>
                    <div class="tag-row">
                      @for (sk of sdr.skills; track sk) {
                        <span class="tag-chip tag-skill">{{ sk }}</span>
                      }
                    </div>
                  </div>
                </div>
              </div>
            }
          </div>
        </section>

        <!-- Right: Inbound Routing Simulator -->
        <section class="card simulator-card">
          <div class="card-header">
            <div>
              <h2>Inbound Routing Simulator</h2>
              <p class="section-desc">Test autonomous multi-factor dispatch engine in real time</p>
            </div>
            <span class="badge badge-accent">Live Simulation</span>
          </div>

          <form class="sim-form" (ngSubmit)="simulateInboundLead()">
            <div class="form-row">
              <div class="form-group">
                <label>Lead Full Name</label>
                <input
                  type="text"
                  [(ngModel)]="simLeadName"
                  name="simLeadName"
                  placeholder="e.g. Faisal Al-Zahrani"
                  required
                  data-testid="sim-lead-name" />
              </div>
              <div class="form-group">
                <label>Work Email</label>
                <input
                  type="email"
                  [(ngModel)]="simLeadEmail"
                  name="simLeadEmail"
                  placeholder="e.g. faisal@aramcocloud.sa"
                  required
                  data-testid="sim-lead-email" />
              </div>
            </div>

            <div class="form-row">
              <div class="form-group">
                <label>Company Name</label>
                <input
                  type="text"
                  [(ngModel)]="simCompanyName"
                  name="simCompanyName"
                  placeholder="e.g. PetroLink Systems"
                  required
                  data-testid="sim-company-name" />
              </div>
              <div class="form-group">
                <label>Target Territory</label>
                <select [(ngModel)]="simTerritory" name="simTerritory" data-testid="sim-territory">
                  <option value="Riyadh">Riyadh (Central Province)</option>
                  <option value="Jeddah">Jeddah (Western Province)</option>
                  <option value="Dammam">Dammam (Eastern Province)</option>
                  <option value="Dubai / GCC">Dubai / GCC</option>
                </select>
              </div>
            </div>

            <div class="form-row">
              <div class="form-group">
                <label>Required Skill Domain</label>
                <select [(ngModel)]="simSkill" name="simSkill" data-testid="sim-skill">
                  <option value="B2B SaaS">B2B SaaS</option>
                  <option value="Retail FinTech">Retail FinTech</option>
                  <option value="Enterprise Logistics">Enterprise Logistics</option>
                  <option value="Cross-Border E-Commerce">Cross-Border E-Commerce</option>
                </select>
              </div>
              <div class="form-group">
                <label>Est. Deal Size (SAR)</label>
                <input
                  type="number"
                  [(ngModel)]="simDealSizeSar"
                  name="simDealSizeSar"
                  min="5000"
                  step="5000"
                  data-testid="sim-deal-size" />
              </div>
            </div>

            <button type="submit" class="btn btn-primary btn-block" data-testid="sim-submit-btn">
              <span class="icon">🎯</span> Route Lead via Autonomous Balancer
            </button>
          </form>

          <!-- Routing Decision Card -->
          @if (lastDecision(); as decision) {
            <div class="decision-box" data-testid="decision-result">
              <div class="decision-top">
                <span class="decision-badge">Routing Decision · {{ decision.matchTier }}</span>
                <span class="sla-clock">SLA Due in 15m</span>
              </div>
              <div class="decision-body">
                <div class="decision-sdr">
                  <strong>Assigned Rep:</strong> {{ decision.assignedSdr.repName }}
                  <span class="sdr-email">({{ decision.assignedSdr.repEmail }})</span>
                </div>
                <div class="decision-rationale">
                  {{ decision.rationale }}
                </div>
              </div>
            </div>
          }
        </section>
      </div>

      <!-- Real-Time SLA Radar Table -->
      <section class="card radar-card">
        <div class="card-header">
          <div>
            <h2>Real-Time Inbound SLA Radar</h2>
            <p class="section-desc">Active SLA countdowns, breach triage, and one-click contact logging</p>
          </div>
          <div class="radar-legend">
            <span class="legend-item"><span class="dot dot-success"></span> Within SLA</span>
            <span class="legend-item"><span class="dot dot-warning"></span> Warning (&lt;5m)</span>
            <span class="legend-item"><span class="dot dot-danger"></span> Breached</span>
            <span class="legend-item"><span class="dot dot-purple"></span> Reassigned</span>
          </div>
        </div>

        <div class="table-container">
          <table class="radar-table" data-testid="sla-radar-table">
            <thead>
              <tr>
                <th>Lead & Company</th>
                <th>Territory & Domain</th>
                <th>Assigned SDR</th>
                <th>SLA Due At</th>
                <th>Status</th>
                <th>First Contact</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              @for (item of assignments(); track item.assignmentId) {
                <tr [attr.data-testid]="'assignment-row-' + item.assignmentId">
                  <td>
                    <div class="lead-info">
                      <strong class="lead-name">{{ item.leadName }}</strong>
                      <span class="lead-email">{{ item.leadEmail }}</span>
                      <span class="company-name">{{ item.companyName }}</span>
                    </div>
                  </td>
                  <td>
                    <span class="tag-chip tag-territory">{{ item.territory }}</span>
                  </td>
                  <td>
                    <div class="assigned-sdr-cell">
                      <strong>{{ item.sdrName }}</strong>
                      @if (item.reassignedFromSdrId) {
                        <span class="reassigned-tag">Reallocated</span>
                      }
                    </div>
                  </td>
                  <td>
                    <span class="sla-time">{{ formatTime(item.slaDueAtUtc) }}</span>
                  </td>
                  <td>
                    <span class="status-pill" [ngClass]="'status-' + item.slaStatus.toLowerCase()">
                      {{ item.slaStatus }}
                    </span>
                  </td>
                  <td>
                    @if (item.firstContactedAtUtc) {
                      <span class="contacted-stamp text-success">✓ {{ formatTime(item.firstContactedAtUtc) }}</span>
                    } @else {
                      <span class="pending-stamp text-muted">Awaiting Outreach</span>
                    }
                  </td>
                  <td>
                    <div class="action-buttons">
                      @if (!item.firstContactedAtUtc) {
                        <button
                          class="btn btn-sm btn-action"
                          (click)="logFirstContact(item.assignmentId)"
                          [attr.data-testid]="'contact-btn-' + item.assignmentId">
                          Log Contact
                        </button>
                      } @else {
                        <span class="badge badge-success">Closed</span>
                      }
                    </div>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>
    </div>
  `,
  styles: [`
    :host { display: block; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; color: #e2e8f0; background: #0b1120; min-height: 100vh; padding: 1.5rem; }
    .studio-container { max-width: 1440px; margin: 0 auto; display: flex; flex-direction: column; gap: 1.5rem; }
    .studio-header { display: flex; justify-content: space-between; align-items: flex-start; border-bottom: 1px solid #1e293b; padding-bottom: 1.25rem; }
    .badge-pill { display: inline-block; font-size: 0.75rem; font-weight: 600; text-transform: uppercase; letter-spacing: 0.05em; background: #1e293b; color: #38bdf8; padding: 0.25rem 0.6rem; border-radius: 9999px; margin-bottom: 0.5rem; }
    h1 { font-size: 1.75rem; font-weight: 700; color: #f8fafc; margin: 0 0 0.25rem; }
    .subtitle { color: #94a3b8; margin: 0; font-size: 0.95rem; }
    .header-actions { display: flex; gap: 0.75rem; }
    .btn { display: inline-flex; align-items: center; gap: 0.4rem; padding: 0.55rem 1.1rem; font-size: 0.875rem; font-weight: 600; border-radius: 0.5rem; border: 1px solid transparent; cursor: pointer; transition: all 0.15s ease-in-out; }
    .btn-primary { background: #0284c7; color: #ffffff; border-color: #0284c7; }
    .btn-primary:hover { background: #0369a1; }
    .btn-secondary { background: #0f172a; color: #38bdf8; border-color: #0284c7; }
    .btn-secondary:hover { background: #1e293b; }
    .btn-outline { background: transparent; color: #94a3b8; border-color: #334155; }
    .btn-outline:hover { background: #1e293b; color: #f1f5f9; }
    .btn-sm { padding: 0.35rem 0.75rem; font-size: 0.75rem; }
    .btn-block { width: 100%; justify-content: center; }
    .btn-action { background: #1e293b; color: #38bdf8; border: 1px solid #38bdf8; }
    .btn-action:hover { background: #0284c7; color: #ffffff; }
    .kpi-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 1rem; }
    .kpi-card { background: #0f172a; border: 1px solid #1e293b; border-radius: 0.75rem; padding: 1.25rem; display: flex; flex-direction: column; gap: 0.4rem; }
    .kpi-label { font-size: 0.8rem; font-weight: 600; color: #94a3b8; text-transform: uppercase; letter-spacing: 0.05em; }
    .kpi-value { font-size: 2rem; font-weight: 800; line-height: 1; }
    .kpi-sub { font-size: 0.9rem; font-weight: 500; color: #64748b; }
    .kpi-meta { font-size: 0.75rem; color: #64748b; }
    .text-primary { color: #38bdf8; } .text-accent { color: #818cf8; } .text-success { color: #34d399; } .text-warning { color: #fbbf24; } .text-danger { color: #f87171; }
    .studio-grid { display: grid; grid-template-columns: 1.4fr 1fr; gap: 1.5rem; }
    @media (max-width: 1024px) { .studio-grid { grid-template-columns: 1fr; } }
    .card { background: #0f172a; border: 1px solid #1e293b; border-radius: 0.75rem; padding: 1.25rem; display: flex; flex-direction: column; gap: 1rem; }
    .card-header { display: flex; justify-content: space-between; align-items: flex-start; border-bottom: 1px solid #1e293b; padding-bottom: 0.75rem; }
    .card-header h2 { font-size: 1.15rem; font-weight: 700; margin: 0 0 0.2rem; color: #f1f5f9; }
    .section-desc { font-size: 0.8rem; color: #94a3b8; margin: 0; }
    .badge { font-size: 0.75rem; font-weight: 600; padding: 0.25rem 0.6rem; border-radius: 9999px; }
    .badge-accent { background: #312e81; color: #a5b4fc; } .badge-neutral { background: #1e293b; color: #94a3b8; } .badge-success { background: #064e3b; color: #6ee7b7; }
    .roster-list { display: flex; flex-direction: column; gap: 1rem; }
    .sdr-card { background: #1e293b40; border: 1px solid #334155; border-radius: 0.6rem; padding: 1rem; display: flex; flex-direction: column; gap: 0.75rem; }
    .sdr-top { display: flex; justify-content: space-between; align-items: center; }
    .sdr-identity h3 { margin: 0; font-size: 1rem; font-weight: 700; color: #f8fafc; }
    .sdr-email { font-size: 0.75rem; color: #64748b; }
    .sdr-status-controls { display: flex; align-items: center; gap: 0.5rem; }
    .status-select { background: #0f172a; border: 1px solid #334155; color: #e2e8f0; border-radius: 0.35rem; font-size: 0.75rem; padding: 0.25rem 0.5rem; cursor: pointer; }
    .status-pill { font-size: 0.7rem; font-weight: 700; text-transform: uppercase; padding: 0.2rem 0.55rem; border-radius: 9999px; }
    .status-online, .status-withinsla { background: #064e3b; color: #34d399; }
    .status-busy { background: #312e81; color: #a5b4fc; }
    .status-away, .status-warning { background: #78350f; color: #fcd34d; }
    .status-offline { background: #27272a; color: #a1a1aa; }
    .status-breached { background: #7f1d1d; color: #f87171; }
    .status-reassigned { background: #4c1d95; color: #c084fc; }
    .capacity-meter { display: flex; flex-direction: column; gap: 0.35rem; }
    .meter-labels { display: flex; justify-content: space-between; font-size: 0.8rem; color: #94a3b8; }
    .load-badge { font-weight: 700; font-size: 0.75rem; }
    .load-badge.safe { color: #34d399; } .load-badge.moderate { color: #fbbf24; } .load-badge.danger { color: #f87171; }
    .meter-track { background: #0f172a; border-radius: 9999px; height: 8px; overflow: hidden; border: 1px solid #334155; }
    .meter-fill { height: 100%; border-radius: 9999px; transition: width 0.3s ease; }
    .meter-fill.bar-safe { background: #10b981; } .meter-fill.bar-moderate { background: #f59e0b; } .meter-fill.bar-danger { background: #ef4444; }
    .sdr-meta-grid { display: flex; flex-direction: column; gap: 0.35rem; font-size: 0.75rem; border-top: 1px solid #1e293b; padding-top: 0.5rem; }
    .meta-item { display: flex; align-items: center; gap: 0.5rem; }
    .meta-label { color: #64748b; min-width: 90px; }
    .tag-row { display: flex; flex-wrap: wrap; gap: 0.35rem; }
    .tag-chip { font-size: 0.7rem; padding: 0.15rem 0.45rem; border-radius: 0.35rem; }
    .tag-territory { background: #0284c726; color: #38bdf8; border: 1px solid #0284c750; }
    .tag-skill { background: #818cf826; color: #a5b4fc; border: 1px solid #818cf850; }
    .sim-form { display: flex; flex-direction: column; gap: 0.85rem; }
    .form-row { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
    .form-group { display: flex; flex-direction: column; gap: 0.25rem; }
    .form-group label { font-size: 0.75rem; font-weight: 600; color: #94a3b8; }
    .form-group input, .form-group select { background: #1e293b; border: 1px solid #334155; color: #f1f5f9; border-radius: 0.45rem; padding: 0.5rem 0.75rem; font-size: 0.85rem; }
    .form-group input:focus, .form-group select:focus { outline: none; border-color: #0284c7; }
    .decision-box { margin-top: 0.5rem; background: #0284c715; border: 1px solid #0284c7; border-radius: 0.5rem; padding: 1rem; display: flex; flex-direction: column; gap: 0.5rem; }
    .decision-top { display: flex; justify-content: space-between; align-items: center; }
    .decision-badge { font-size: 0.75rem; font-weight: 700; color: #38bdf8; text-transform: uppercase; }
    .sla-clock { font-size: 0.75rem; color: #fcd34d; font-weight: 600; }
    .decision-sdr { font-size: 0.95rem; color: #f8fafc; }
    .decision-rationale { font-size: 0.8rem; color: #94a3b8; }
    .radar-legend { display: flex; gap: 1rem; font-size: 0.75rem; color: #94a3b8; }
    .legend-item { display: flex; align-items: center; gap: 0.35rem; }
    .dot { width: 8px; height: 8px; border-radius: 50%; display: inline-block; }
    .dot-success { background: #10b981; } .dot-warning { background: #f59e0b; } .dot-danger { background: #ef4444; } .dot-purple { background: #a855f7; }
    .table-container { overflow-x: auto; }
    .radar-table { width: 100%; border-collapse: collapse; font-size: 0.85rem; }
    .radar-table th { text-align: left; padding: 0.65rem 0.85rem; border-bottom: 1px solid #1e293b; color: #64748b; font-size: 0.75rem; text-transform: uppercase; letter-spacing: 0.05em; }
    .radar-table td { padding: 0.85rem; border-bottom: 1px solid #1e293b; vertical-align: middle; }
    .lead-info { display: flex; flex-direction: column; gap: 0.15rem; }
    .lead-name { color: #f8fafc; font-size: 0.9rem; }
    .lead-email { font-size: 0.75rem; color: #64748b; }
    .company-name { font-size: 0.75rem; color: #94a3b8; }
    .assigned-sdr-cell { display: flex; flex-direction: column; gap: 0.2rem; }
    .reassigned-tag { font-size: 0.65rem; color: #c084fc; background: #4c1d9540; padding: 0.1rem 0.35rem; border-radius: 0.25rem; width: fit-content; }
    .sla-time { font-family: monospace; font-size: 0.85rem; color: #f1f5f9; }
    .contacted-stamp { font-size: 0.75rem; font-weight: 600; }
    .pending-stamp { font-size: 0.75rem; }
    .action-buttons { display: flex; gap: 0.4rem; }
  `]
})
export class LeadRoutingCapacityStudioComponent {
  public translation: TranslationService;
  protected readonly Math = Math;

  public readonly defaultTenantId = '11111111-1111-1111-1111-111111111111';

  // Seeded SDR roster
  public readonly sdrs = signal<SdrRepProfile[]>([
    {
      sdrId: '22222222-1111-1111-1111-111111111111',
      tenantId: '11111111-1111-1111-1111-111111111111',
      repName: 'Tariq Al-Ghamdi',
      repEmail: 'tariq.ghamdi@sgrowing.com',
      status: 'Online',
      maxCapacity: 15,
      currentActiveLeads: 6,
      quotaWeight: 1.2,
      assignedTerritories: ['Riyadh', 'Central Province', 'KSA'],
      skills: ['B2B SaaS', 'Enterprise', 'Outbound AI'],
      lastAssignedAtUtc: new Date(Date.now() - 45 * 60000).toISOString()
    },
    {
      sdrId: '33333333-1111-1111-1111-111111111111',
      tenantId: '11111111-1111-1111-1111-111111111111',
      repName: 'Reem Al-Khatib',
      repEmail: 'reem.khatib@sgrowing.com',
      status: 'Online',
      maxCapacity: 12,
      currentActiveLeads: 4,
      quotaWeight: 1.0,
      assignedTerritories: ['Jeddah', 'Western Province', 'KSA'],
      skills: ['Retail FinTech', 'Payment Gateways', 'SME'],
      lastAssignedAtUtc: new Date(Date.now() - 30 * 60000).toISOString()
    },
    {
      sdrId: '44444444-1111-1111-1111-111111111111',
      tenantId: '11111111-1111-1111-1111-111111111111',
      repName: 'Zaid Al-Otaibi',
      repEmail: 'zaid.otaibi@sgrowing.com',
      status: 'Online',
      maxCapacity: 10,
      currentActiveLeads: 8,
      quotaWeight: 0.8,
      assignedTerritories: ['Dammam', 'Eastern Province', 'KSA'],
      skills: ['Enterprise Logistics', 'Supply Chain', 'Heavy Industry'],
      lastAssignedAtUtc: new Date(Date.now() - 15 * 60000).toISOString()
    },
    {
      sdrId: '55555555-1111-1111-1111-111111111111',
      tenantId: '11111111-1111-1111-1111-111111111111',
      repName: 'Sarah Mansour',
      repEmail: 'sarah.mansour@sgrowing.com',
      status: 'Away',
      maxCapacity: 10,
      currentActiveLeads: 2,
      quotaWeight: 1.0,
      assignedTerritories: ['Dubai / GCC', 'UAE', 'GCC'],
      skills: ['Cross-Border E-Commerce', 'Omnichannel', 'Luxury Retail'],
      lastAssignedAtUtc: new Date(Date.now() - 120 * 60000).toISOString()
    }
  ]);

  // Seeded Lead Assignments for SLA Radar
  public readonly assignments = signal<LeadAssignmentRecord[]>([
    {
      assignmentId: 'aaaaaaaa-1111-1111-1111-111111111111',
      tenantId: '11111111-1111-1111-1111-111111111111',
      leadId: 'baaaaaaa-1111-1111-1111-111111111111',
      leadName: 'Omar Al-Falih',
      leadEmail: 'omar@cloudscale-sa.com',
      companyName: 'CloudScale KSA',
      territory: 'Riyadh',
      sdrId: '22222222-1111-1111-1111-111111111111',
      sdrName: 'Tariq Al-Ghamdi',
      assignedAtUtc: new Date(Date.now() - 5 * 60000).toISOString(),
      slaDueAtUtc: new Date(Date.now() + 10 * 60000).toISOString(),
      slaStatus: 'WithinSla',
      firstContactedAtUtc: null,
      reassignedFromSdrId: null
    },
    {
      assignmentId: 'bbbbbbbb-1111-1111-1111-111111111111',
      tenantId: '11111111-1111-1111-1111-111111111111',
      leadId: 'bbaaaaaa-1111-1111-1111-111111111111',
      leadName: 'Noor Bint Sultan',
      leadEmail: 'noor@redseapay.com',
      companyName: 'RedSea Pay FinTech',
      territory: 'Jeddah',
      sdrId: '33333333-1111-1111-1111-111111111111',
      sdrName: 'Reem Al-Khatib',
      assignedAtUtc: new Date(Date.now() - 12 * 60000).toISOString(),
      slaDueAtUtc: new Date(Date.now() + 3 * 60000).toISOString(),
      slaStatus: 'Warning',
      firstContactedAtUtc: null,
      reassignedFromSdrId: null
    },
    {
      assignmentId: 'cccccccc-1111-1111-1111-111111111111',
      tenantId: '11111111-1111-1111-1111-111111111111',
      leadId: 'bcaaaaaa-1111-1111-1111-111111111111',
      leadName: 'Khalid Al-Dossari',
      leadEmail: 'khalid@gulfcoldchain.sa',
      companyName: 'Gulf Cold Chain Logistics',
      territory: 'Dammam',
      sdrId: '44444444-1111-1111-1111-111111111111',
      sdrName: 'Zaid Al-Otaibi',
      assignedAtUtc: new Date(Date.now() - 25 * 60000).toISOString(),
      slaDueAtUtc: new Date(Date.now() - 10 * 60000).toISOString(),
      slaStatus: 'Breached',
      firstContactedAtUtc: null,
      reassignedFromSdrId: null
    },
    {
      assignmentId: 'dddddddd-1111-1111-1111-111111111111',
      tenantId: '11111111-1111-1111-1111-111111111111',
      leadId: 'bdaaaaaa-1111-1111-1111-111111111111',
      leadName: 'Fatima Al-Harbi',
      leadEmail: 'fatima@saudiretail.com',
      companyName: 'Saudi Retail Group',
      territory: 'Riyadh',
      sdrId: '22222222-1111-1111-1111-111111111111',
      sdrName: 'Tariq Al-Ghamdi',
      assignedAtUtc: new Date(Date.now() - 60 * 60000).toISOString(),
      slaDueAtUtc: new Date(Date.now() - 45 * 60000).toISOString(),
      slaStatus: 'WithinSla',
      firstContactedAtUtc: new Date(Date.now() - 54 * 60000).toISOString(),
      reassignedFromSdrId: null
    }
  ]);

  // Routing Simulator Form State
  public simLeadName = 'Sultan Al-Olayan';
  public simLeadEmail = 'sultan@saudihealth.com';
  public simCompanyName = 'Saudi Health Tech Systems';
  public simTerritory = 'Riyadh';
  public simSkill = 'B2B SaaS';
  public simDealSizeSar = 150000;

  public readonly lastDecision = signal<RoutingDecision | null>(null);

  // Computed KPIs
  public readonly totalSdrCount = computed(() => this.sdrs().length);

  public readonly onlineSdrCount = computed(() =>
    this.sdrs().filter(s => s.status === 'Online').length
  );

  public readonly readinessRate = computed(() => {
    const total = this.totalSdrCount();
    return total > 0 ? Math.round((this.onlineSdrCount() / total) * 100) : 0;
  });

  public readonly totalActiveRoutedLeads = computed(() =>
    this.assignments().filter(a => a.firstContactedAtUtc === null).length
  );

  public readonly slaCompliancePercent = computed(() => {
    const list = this.assignments();
    if (list.length === 0) return 100;
    const compliant = list.filter(a =>
      a.slaStatus === 'WithinSla' || a.slaStatus === 'Warning' ||
      (a.firstContactedAtUtc && new Date(a.firstContactedAtUtc).getTime() <= new Date(a.slaDueAtUtc).getTime())
    ).length;
    return Math.round((compliant / list.length) * 100);
  });

  public readonly avgFirstContactMinutes = computed(() => {
    const contacted = this.assignments().filter(a => a.firstContactedAtUtc !== null);
    if (contacted.length === 0) return 4.2;
    const totalMins = contacted.reduce((acc, curr) => {
      const assigned = new Date(curr.assignedAtUtc).getTime();
      const contactedTime = new Date(curr.firstContactedAtUtc!).getTime();
      return acc + (contactedTime - assigned) / 60000;
    }, 0);
    return Math.round((totalMins / contacted.length) * 10) / 10;
  });

  constructor(@Optional() translationService?: TranslationService) {
    if (translationService) this.translation = translationService;
    else { try { this.translation = inject(TranslationService); } catch { this.translation = new TranslationService(); } }
  }

  public updateRepStatus(sdrId: string, newStatus: SdrAvailabilityStatus): void {
    this.sdrs.update(list =>
      list.map(s => s.sdrId === sdrId ? { ...s, status: newStatus } : s)
    );
  }

  public simulateInboundLead(): void {
    const eligibleReps = this.sdrs().filter(s =>
      s.status === 'Online' && s.currentActiveLeads < s.maxCapacity
    );

    if (eligibleReps.length === 0) {
      alert('Simulation Warning: All SDRs are offline or at maximum capacity. Cannot auto-route lead.');
      return;
    }

    // Matching tiers
    const matchesTerritory = (s: SdrRepProfile) =>
      s.assignedTerritories.some(t =>
        t.toLowerCase().includes(this.simTerritory.toLowerCase()) ||
        this.simTerritory.toLowerCase().includes(t.toLowerCase())
      );

    const matchesSkill = (s: SdrRepProfile) =>
      s.skills.some(sk =>
        sk.toLowerCase().includes(this.simSkill.toLowerCase()) ||
        this.simSkill.toLowerCase().includes(sk.toLowerCase())
      );

    const tier1 = eligibleReps.filter(s => matchesTerritory(s) && matchesSkill(s));
    const tier2 = eligibleReps.filter(matchesTerritory);
    const tier3 = eligibleReps.filter(matchesSkill);

    let chosenPool: SdrRepProfile[];
    let matchTier: RoutingDecision['matchTier'];

    if (tier1.length > 0) {
      chosenPool = tier1;
      matchTier = 'TerritoryAndSkill';
    } else if (tier2.length > 0) {
      chosenPool = tier2;
      matchTier = 'TerritoryOnly';
    } else if (tier3.length > 0) {
      chosenPool = tier3;
      matchTier = 'SkillOnly';
    } else {
      chosenPool = eligibleReps;
      matchTier = 'CapacityOverflow';
    }

    // Weighted least-loaded selection
    const sorted = [...chosenPool].sort((a, b) => {
      const loadA = (a.currentActiveLeads / Math.max(1, a.maxCapacity)) / Math.max(0.1, a.quotaWeight);
      const loadB = (b.currentActiveLeads / Math.max(1, b.maxCapacity)) / Math.max(0.1, b.quotaWeight);
      return loadA - loadB;
    });

    const selected = sorted[0];

    // Increment selected rep active lead count
    this.sdrs.update(list =>
      list.map(s => s.sdrId === selected.sdrId ? {
        ...s,
        currentActiveLeads: s.currentActiveLeads + 1,
        lastAssignedAtUtc: new Date().toISOString()
      } : s)
    );

    const now = new Date();
    const slaDue = new Date(now.getTime() + 15 * 60000);

    const newAssignment: LeadAssignmentRecord = {
      assignmentId: `sim-${Date.now()}`,
      tenantId: this.defaultTenantId,
      leadId: `lead-${Date.now()}`,
      leadName: this.simLeadName,
      leadEmail: this.simLeadEmail,
      companyName: this.simCompanyName,
      territory: this.simTerritory,
      sdrId: selected.sdrId,
      sdrName: selected.repName,
      assignedAtUtc: now.toISOString(),
      slaDueAtUtc: slaDue.toISOString(),
      slaStatus: 'WithinSla',
      firstContactedAtUtc: null,
      reassignedFromSdrId: null
    };

    this.assignments.update(prev => [newAssignment, ...prev]);

    this.lastDecision.set({
      assignedSdr: selected,
      matchTier,
      rationale: `Matched ${selected.repName} (${selected.assignedTerritories[0]}, ${selected.skills[0]}) via ${matchTier} heuristic with current active load ${selected.currentActiveLeads + 1}/${selected.maxCapacity}.`,
      assignment: newAssignment
    });
  }

  public logFirstContact(assignmentId: string): void {
    const target = this.assignments().find(a => a.assignmentId === assignmentId);
    if (!target || target.firstContactedAtUtc !== null) return;

    const contactTime = new Date();
    const isWithinSla = contactTime.getTime() <= new Date(target.slaDueAtUtc).getTime();
    const finalStatus: LeadSlaBreachStatus = isWithinSla ? 'WithinSla' : 'Breached';

    this.assignments.update(list =>
      list.map(a => a.assignmentId === assignmentId ? {
        ...a,
        firstContactedAtUtc: contactTime.toISOString(),
        slaStatus: finalStatus
      } : a)
    );

    // Decrement rep active leads
    this.sdrs.update(list =>
      list.map(s => s.sdrId === target.sdrId ? {
        ...s,
        currentActiveLeads: Math.max(0, s.currentActiveLeads - 1)
      } : s)
    );
  }

  public checkAndTriggerSlaBreaches(): void {
    const now = Date.now();
    const pending = this.assignments().filter(a => a.firstContactedAtUtc === null && a.slaStatus !== 'Reassigned');

    for (const assignment of pending) {
      const due = new Date(assignment.slaDueAtUtc).getTime();
      if (now > due) {
        // Find another online SDR with capacity
        const alternativeSdr = this.sdrs().find(s =>
          s.status === 'Online' &&
          s.currentActiveLeads < s.maxCapacity &&
          s.sdrId !== assignment.sdrId
        );

        if (alternativeSdr) {
          // Decrement previous SDR
          this.sdrs.update(list =>
            list.map(s => s.sdrId === assignment.sdrId ? {
              ...s,
              currentActiveLeads: Math.max(0, s.currentActiveLeads - 1)
            } : s.sdrId === alternativeSdr.sdrId ? {
              ...s,
              currentActiveLeads: s.currentActiveLeads + 1,
              lastAssignedAtUtc: new Date().toISOString()
            } : s)
          );

          // Update assignment to Reassigned
          this.assignments.update(list =>
            list.map(a => a.assignmentId === assignment.assignmentId ? {
              ...a,
              sdrId: alternativeSdr.sdrId,
              sdrName: alternativeSdr.repName,
              reassignedFromSdrId: assignment.sdrId,
              slaStatus: 'Reassigned',
              assignedAtUtc: new Date().toISOString(),
              slaDueAtUtc: new Date(Date.now() + 15 * 60000).toISOString()
            } : a)
          );
        } else {
          // Mark Breached
          this.assignments.update(list =>
            list.map(a => a.assignmentId === assignment.assignmentId ? {
              ...a,
              slaStatus: 'Breached'
            } : a)
          );
        }
      } else if (due - now <= 5 * 60000 && assignment.slaStatus === 'WithinSla') {
        this.assignments.update(list =>
          list.map(a => a.assignmentId === assignment.assignmentId ? {
            ...a,
            slaStatus: 'Warning'
          } : a)
        );
      }
    }
  }

  public resetToDefaults(): void {
    this.sdrs.set([
      {
        sdrId: '22222222-1111-1111-1111-111111111111',
        tenantId: '11111111-1111-1111-1111-111111111111',
        repName: 'Tariq Al-Ghamdi',
        repEmail: 'tariq.ghamdi@sgrowing.com',
        status: 'Online',
        maxCapacity: 15,
        currentActiveLeads: 6,
        quotaWeight: 1.2,
        assignedTerritories: ['Riyadh', 'Central Province', 'KSA'],
        skills: ['B2B SaaS', 'Enterprise', 'Outbound AI'],
        lastAssignedAtUtc: new Date(Date.now() - 45 * 60000).toISOString()
      },
      {
        sdrId: '33333333-1111-1111-1111-111111111111',
        tenantId: '11111111-1111-1111-1111-111111111111',
        repName: 'Reem Al-Khatib',
        repEmail: 'reem.khatib@sgrowing.com',
        status: 'Online',
        maxCapacity: 12,
        currentActiveLeads: 4,
        quotaWeight: 1.0,
        assignedTerritories: ['Jeddah', 'Western Province', 'KSA'],
        skills: ['Retail FinTech', 'Payment Gateways', 'SME'],
        lastAssignedAtUtc: new Date(Date.now() - 30 * 60000).toISOString()
      },
      {
        sdrId: '44444444-1111-1111-1111-111111111111',
        tenantId: '11111111-1111-1111-1111-111111111111',
        repName: 'Zaid Al-Otaibi',
        repEmail: 'zaid.otaibi@sgrowing.com',
        status: 'Online',
        maxCapacity: 10,
        currentActiveLeads: 8,
        quotaWeight: 0.8,
        assignedTerritories: ['Dammam', 'Eastern Province', 'KSA'],
        skills: ['Enterprise Logistics', 'Supply Chain', 'Heavy Industry'],
        lastAssignedAtUtc: new Date(Date.now() - 15 * 60000).toISOString()
      },
      {
        sdrId: '55555555-1111-1111-1111-111111111111',
        tenantId: '11111111-1111-1111-1111-111111111111',
        repName: 'Sarah Mansour',
        repEmail: 'sarah.mansour@sgrowing.com',
        status: 'Away',
        maxCapacity: 10,
        currentActiveLeads: 2,
        quotaWeight: 1.0,
        assignedTerritories: ['Dubai / GCC', 'UAE', 'GCC'],
        skills: ['Cross-Border E-Commerce', 'Omnichannel', 'Luxury Retail'],
        lastAssignedAtUtc: new Date(Date.now() - 120 * 60000).toISOString()
      }
    ]);

    this.lastDecision.set(null);
  }

  public getLoadBadgeClass(pct: number): string {
    if (pct < 70) return 'safe';
    if (pct < 90) return 'moderate';
    return 'danger';
  }

  public getLoadBarClass(pct: number): string {
    if (pct < 70) return 'bar-safe';
    if (pct < 90) return 'bar-moderate';
    return 'bar-danger';
  }

  public formatTime(iso: string): string {
    try {
      const d = new Date(iso);
      return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    } catch {
      return iso;
    }
  }
}
