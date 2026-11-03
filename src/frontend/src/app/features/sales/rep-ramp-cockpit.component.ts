import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface RepCompetency {
  competencyName: string;
  score: number;
  targetScore: number;
  status: string;
}

export interface RepProfile {
  repId: string;
  name: string;
  role: string;
  stage: string;
  weeksInRole: number;
  targetWeeksToRamp: number;
  competencies: RepCompetency[];
  quotaAttainmentPercentage: number;
  dealsClosed: number;
}

export interface ShadowCallGuidance {
  guidanceId: string;
  repId: string;
  detectedIntent: string;
  missedCue: string;
  recommendedWhisperPrompt: string;
  microCoachingTip: string;
  confidenceScore: number;
}

@Component({
  selector: 'app-rep-ramp-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="rep-ramp-cockpit">
      <header class="cockpit-header">
        <div class="header-titles">
          <h1>Horizon 181: Sales Rep Ramp Acceleration & Shadow Co-Pilot</h1>
          <p class="subtitle">Autonomous SDR & AE Quota Ramp Velocity with Real-Time Shadow Call Whispering</p>
        </div>
        <div class="header-badges">
          <span class="badge badge-accent" data-testid="active-reps-count">Active Ramping Reps: {{ reps().length }}</span>
          <span class="badge badge-success">Average Time-to-Ramp: 45 Days</span>
        </div>
      </header>

      <div class="cockpit-grid">
        <!-- Rep Profiles List -->
        <section class="card reps-list-card">
          <h2>Ramping Sales Reps</h2>
          <div class="reps-grid">
            @for (rep of reps(); track rep.repId) {
              <div 
                class="rep-card" 
                [class.selected]="selectedRep()?.repId === rep.repId"
                (click)="selectRep(rep)"
                [attr.data-testid]="'rep-card-' + rep.repId">
                <div class="rep-info">
                  <h3>{{ rep.name }}</h3>
                  <span class="role-tag">{{ rep.role }}</span>
                  <div class="stage-tag">Stage: {{ rep.stage }} (Week {{ rep.weeksInRole }}/{{ rep.targetWeeksToRamp }})</div>
                </div>
                <div class="rep-stats">
                  <div class="stat-item">
                    <span class="label">Quota Attainment</span>
                    <span class="val">{{ rep.quotaAttainmentPercentage }}%</span>
                  </div>
                  <div class="stat-item">
                    <span class="label">Deals Closed</span>
                    <span class="val">{{ rep.dealsClosed }}</span>
                  </div>
                </div>
              </div>
            }
          </div>
        </section>

        <!-- Live Shadow Call Simulator -->
        <section class="card shadow-call-card">
          <h2>Live Shadow-Call AI Co-Pilot Simulator</h2>
          <div class="simulator-form">
            <label for="transcript-input">Prospect Live Speech Transcript Segment:</label>
            <textarea
              id="transcript-input"
              rows="3"
              [(ngModel)]="liveTranscript"
              placeholder="e.g. We love the platform but your price is too high and our budget is locked."
              data-testid="live-transcript-input"></textarea>

            <button 
              type="button" 
              class="btn-primary" 
              (click)="generateGuidance()"
              data-testid="generate-guidance-btn">
              Generate AI Whisper & Micro-Coaching
            </button>
          </div>

          @if (activeGuidance(); as guidance) {
            <div class="guidance-box" data-testid="whisper-guidance-box">
              <div class="guidance-header">
                <span class="intent-pill">Detected: {{ guidance.detectedIntent }}</span>
                <span class="confidence">Confidence: {{ guidance.confidenceScore }}%</span>
              </div>
              <div class="missed-cue">
                <strong>Missed Cue:</strong> {{ guidance.missedCue }}
              </div>
              <div class="whisper-prompt">
                <strong>Recommended Whisper Intervention:</strong>
                <p>"{{ guidance.recommendedWhisperPrompt }}"</p>
              </div>
              <div class="coaching-tip">
                <strong>Micro-Coaching Tip:</strong> {{ guidance.microCoachingTip }}
              </div>
            </div>
          }
        </section>

        <!-- Competency & Trajectory Matrix -->
        <section class="card trajectory-card" data-testid="trajectory-card">
          <h2>Ramp Velocity & Competency Breakdown</h2>
          @if (selectedRep(); as rep) {
            <div class="competencies-list">
              <h3>{{ rep.name }} — Competencies</h3>
              @for (comp of rep.competencies; track comp.competencyName) {
                <div class="comp-row">
                  <span class="comp-name">{{ comp.competencyName }}</span>
                  <div class="progress-bar-container">
                    <div class="progress-fill" [style.width.%]="comp.score"></div>
                  </div>
                  <span class="comp-scores">{{ comp.score }}/{{ comp.targetScore }} ({{ comp.status }})</span>
                </div>
              }
            </div>
          }
        </section>
      </div>
    </div>
  `,
  styles: [`
    .cockpit-container {
      padding: 1.5rem;
      background: #0f172a;
      color: #f8fafc;
      min-height: 100vh;
      font-family: system-ui, -apple-system, sans-serif;
    }
    .cockpit-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 2rem;
      border-bottom: 1px solid #334155;
      padding-bottom: 1rem;
    }
    .cockpit-header h1 {
      margin: 0;
      font-size: 1.5rem;
      color: #38bdf8;
    }
    .subtitle {
      margin: 0.25rem 0 0 0;
      color: #94a3b8;
      font-size: 0.9rem;
    }
    .header-badges {
      display: flex;
      gap: 0.75rem;
    }
    .badge {
      padding: 0.35rem 0.75rem;
      border-radius: 9999px;
      font-size: 0.8rem;
      font-weight: 600;
    }
    .badge-accent { background: #0369a1; color: #e0f2fe; }
    .badge-success { background: #15803d; color: #dcfce7; }
    .cockpit-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(340px, 1fr));
      gap: 1.5rem;
    }
    .card {
      background: #1e293b;
      border: 1px solid #334155;
      border-radius: 0.75rem;
      padding: 1.25rem;
    }
    .card h2 {
      margin-top: 0;
      font-size: 1.15rem;
      color: #e2e8f0;
      border-bottom: 1px solid #334155;
      padding-bottom: 0.5rem;
    }
    .reps-grid {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }
    .rep-card {
      background: #0f172a;
      border: 1px solid #334155;
      border-radius: 0.5rem;
      padding: 0.75rem;
      cursor: pointer;
      transition: all 0.2s ease;
    }
    .rep-card:hover, .rep-card.selected {
      border-color: #38bdf8;
      background: #1e293b;
    }
    .rep-card h3 { margin: 0 0 0.25rem 0; font-size: 1rem; color: #f8fafc; }
    .role-tag { font-size: 0.8rem; color: #94a3b8; }
    .stage-tag { font-size: 0.75rem; color: #38bdf8; margin-top: 0.25rem; }
    .rep-stats {
      display: flex;
      gap: 1.5rem;
      margin-top: 0.5rem;
      font-size: 0.85rem;
    }
    .stat-item .label { color: #64748b; margin-right: 0.25rem; }
    .stat-item .val { color: #4ade80; font-weight: bold; }
    .simulator-form {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }
    textarea {
      background: #0f172a;
      border: 1px solid #334155;
      border-radius: 0.375rem;
      color: #f8fafc;
      padding: 0.5rem;
      font-family: inherit;
    }
    .btn-primary {
      background: #2563eb;
      color: #fff;
      border: none;
      border-radius: 0.375rem;
      padding: 0.5rem 1rem;
      font-weight: 600;
      cursor: pointer;
      transition: background 0.2s;
    }
    .btn-primary:hover { background: #1d4ed8; }
    .guidance-box {
      margin-top: 1rem;
      background: #0f172a;
      border: 1px solid #0284c7;
      border-radius: 0.5rem;
      padding: 0.85rem;
      font-size: 0.85rem;
    }
    .guidance-header {
      display: flex;
      justify-content: space-between;
      margin-bottom: 0.5rem;
    }
    .intent-pill {
      background: #0369a1;
      padding: 0.2rem 0.5rem;
      border-radius: 0.25rem;
      font-weight: 600;
    }
    .confidence { color: #38bdf8; }
    .missed-cue { color: #f87171; margin-bottom: 0.5rem; }
    .whisper-prompt {
      background: #172554;
      padding: 0.5rem;
      border-left: 3px solid #38bdf8;
      border-radius: 0.25rem;
      margin-bottom: 0.5rem;
    }
    .whisper-prompt p { margin: 0.25rem 0 0 0; font-style: italic; color: #bae6fd; }
    .coaching-tip { color: #cbd5e1; }
    .comp-row {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      margin-bottom: 0.5rem;
      font-size: 0.8rem;
    }
    .comp-name { width: 140px; color: #cbd5e1; }
    .progress-bar-container {
      flex: 1;
      background: #334155;
      height: 8px;
      border-radius: 4px;
      overflow: hidden;
    }
    .progress-fill { background: #38bdf8; height: 100%; }
    .comp-scores { width: 150px; text-align: right; color: #94a3b8; }
  `]
})
export class RepRampCockpitComponent {
  reps = signal<RepProfile[]>([
    {
      repId: 'REP-RAMP-101',
      name: 'Alex Vance',
      role: 'Enterprise Account Executive',
      stage: 'Shadowing',
      weeksInRole: 4,
      targetWeeksToRamp: 12,
      quotaAttainmentPercentage: 28.5,
      dealsClosed: 1,
      competencies: [
        { competencyName: 'Product Depth', score: 85, targetScore: 90, status: 'Progressing' },
        { competencyName: 'Economic Discovery', score: 62, targetScore: 85, status: 'AttentionNeeded' },
        { competencyName: 'Objection Handling', score: 78, targetScore: 85, status: 'OnTrack' },
        { competencyName: 'Pricing Negotiation', score: 55, targetScore: 80, status: 'CriticalGap' }
      ]
    },
    {
      repId: 'REP-RAMP-102',
      name: 'Elena Rostova',
      role: 'Strategic SDR',
      stage: 'SoloRamping',
      weeksInRole: 8,
      targetWeeksToRamp: 10,
      quotaAttainmentPercentage: 86.4,
      dealsClosed: 14,
      competencies: [
        { competencyName: 'Cold Outbound Prospecting', score: 92, targetScore: 90, status: 'Mastered' },
        { competencyName: 'ICP Qualification', score: 88, targetScore: 85, status: 'Mastered' },
        { competencyName: 'Objection Handling', score: 84, targetScore: 85, status: 'OnTrack' }
      ]
    }
  ]);

  selectedRep = signal<RepProfile | null>(this.reps()[0]);
  liveTranscript = 'We love the platform but your price is too high and our budget is locked.';
  activeGuidance = signal<ShadowCallGuidance | null>(null);

  selectRep(rep: RepProfile) {
    this.selectedRep.set(rep);
  }

  generateGuidance() {
    const text = this.liveTranscript.toLowerCase();
    let intent = 'Discovery Inquiry';
    let missed = 'No explicit cost-of-inaction quantified';
    let whisper = 'Ask: "What happens to your Q4 deliverables if this infrastructure problem remains unsolved?"';
    let coaching = 'Pivot away from technical feature listing to executive business impact and ROI timeline.';
    let conf = 88;

    if (text.includes('price') || text.includes('budget')) {
      intent = 'Budget & Pricing Constraint';
      missed = 'Prospect mentioned pricing pushback before value baseline was established';
      whisper = 'Anchor on Net Value: "Before we discuss tiers, can we confirm the $200k in downtime this prevents?"';
      coaching = 'Defend margin: Never offer immediate discounting without trade-offs on commitment length or scope.';
      conf = 94;
    }

    this.activeGuidance.set({
      guidanceId: 'GUIDE-' + Math.floor(Math.random() * 9000 + 1000),
      repId: this.selectedRep()?.repId ?? 'REP-RAMP-101',
      detectedIntent: intent,
      missedCue: missed,
      recommendedWhisperPrompt: whisper,
      microCoachingTip: coaching,
      confidenceScore: conf
    });
  }
}
