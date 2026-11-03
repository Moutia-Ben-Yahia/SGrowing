import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface DialectProfile {
  code: string;
  name: string;
  idioms: string[];
  pitchShift: number;
  cadenceMultiplier: number;
}

export interface DialectUtterance {
  utteranceId: string;
  dialectCode: string;
  originalText: string;
  adaptedText: string;
  latencyMs: number;
}

@Component({
  selector: 'app-dialect-synthesis-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="dialect-synthesis-cockpit">
      <header class="cockpit-header">
        <div class="header-titles">
          <h1>Horizon 188: Real-Time Voice Dialect Idiom & Vernacular Synthesizer Mesh</h1>
          <p class="subtitle">Sub-45ms Caller Dialect Detection, Cultural Idiom Localization & Formant Contour Shaping</p>
        </div>
        <div class="header-badges">
          <span class="badge badge-accent" data-testid="profiles-count">Dialect Profiles: {{ profiles().length }}</span>
          <span class="badge badge-success">Synthesis Latency: 38ms</span>
        </div>
      </header>

      <div class="cockpit-grid">
        <!-- Dialect Profiles -->
        <section class="card profiles-card">
          <h2>Configured Vernacular Profiles</h2>
          <div class="profiles-list">
            @for (p of profiles(); track p.code) {
              <div 
                class="p-card" 
                [class.selected]="selectedProfile()?.code === p.code"
                (click)="selectProfile(p)"
                [attr.data-testid]="'dialect-card-' + p.code">
                <div class="p-top">
                  <h3>{{ p.name }} ({{ p.code }})</h3>
                  <span class="pitch-pill">Pitch: {{ p.pitchShift > 0 ? '+' : '' }}{{ p.pitchShift }}st</span>
                </div>
                <div class="cadence-tag">Cadence: {{ p.cadenceMultiplier }}x</div>
                <div class="idioms-list">
                  @for (idiom of p.idioms; track idiom) {
                    <span class="idiom-chip">"{{ idiom }}"</span>
                  }
                </div>
              </div>
            }
          </div>
        </section>

        <!-- Dialect Detection & Synthesis Simulator -->
        <section class="card synthesis-card">
          <h2>Live Dialect Adaptation Simulator</h2>
          <div class="sim-form">
            <label for="caller-input">Caller Spoken Utterance:</label>
            <textarea 
              id="caller-input" 
              rows="2" 
              [(ngModel)]="callerTranscript" 
              data-testid="caller-transcript-input"></textarea>

            <button 
              type="button" 
              class="btn-primary" 
              (click)="detectAndSynthesize()"
              data-testid="synthesize-utterance-btn">
              Detect Vernacular & Synthesize Dialect Speech
            </button>
          </div>

          @if (activeUtterance(); as u) {
            <div class="utterance-box" data-testid="adapted-speech-box">
              <div class="utt-header">
                <span class="dialect-pill">{{ u.dialectCode }}</span>
                <span class="latency-pill">{{ u.latencyMs }}ms Latency</span>
              </div>
              <div class="orig-text">
                <span class="lbl">Standard AI Response:</span>
                <p>{{ u.originalText }}</p>
              </div>
              <div class="adapted-text">
                <span class="lbl">Synthesized Vernacular Speech:</span>
                <p>"{{ u.adaptedText }}"</p>
              </div>
            </div>
          }
        </section>

        <!-- Engagement Lift Scorecard -->
        <section class="card metrics-card">
          <h2>Rapport & Conversion Lift</h2>
          <div class="metrics-list">
            <div class="m-row">
              <span>Sentiment Lift Delta:</span>
              <strong class="text-success">+38.0%</strong>
            </div>
            <div class="m-row">
              <span>Call Completion Rate:</span>
              <strong class="text-accent">94.6%</strong>
            </div>
            <div class="m-row">
              <span>Acoustic Pitch Contour:</span>
              <strong>Sub-harmonic Formant Shift Armed</strong>
            </div>
          </div>
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
    .cockpit-header h1 { margin: 0; font-size: 1.5rem; color: #38bdf8; }
    .subtitle { margin: 0.25rem 0 0 0; color: #94a3b8; font-size: 0.9rem; }
    .header-badges { display: flex; gap: 0.75rem; }
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
    .profiles-list { display: flex; flex-direction: column; gap: 0.75rem; }
    .p-card {
      background: #0f172a;
      border: 1px solid #334155;
      border-radius: 0.5rem;
      padding: 0.75rem;
      cursor: pointer;
      transition: all 0.2s ease;
    }
    .p-card:hover, .p-card.selected {
      border-color: #38bdf8;
      background: #1e293b;
    }
    .p-top { display: flex; justify-content: space-between; align-items: center; }
    .p-top h3 { margin: 0; font-size: 0.95rem; color: #f8fafc; }
    .pitch-pill { font-size: 0.75rem; background: #0369a1; padding: 0.2rem 0.4rem; border-radius: 0.25rem; }
    .cadence-tag { font-size: 0.8rem; color: #94a3b8; margin: 0.25rem 0; }
    .idioms-list { display: flex; flex-wrap: wrap; gap: 0.4rem; margin-top: 0.4rem; }
    .idiom-chip { background: #1e293b; font-size: 0.75rem; padding: 0.15rem 0.4rem; border-radius: 0.25rem; color: #38bdf8; }
    .sim-form { display: flex; flex-direction: column; gap: 0.75rem; }
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
    .utterance-box {
      margin-top: 1rem;
      background: #0f172a;
      border: 1px solid #0284c7;
      border-radius: 0.5rem;
      padding: 0.75rem;
      font-size: 0.85rem;
    }
    .utt-header { display: flex; justify-content: space-between; margin-bottom: 0.5rem; }
    .dialect-pill { background: #0369a1; padding: 0.2rem 0.5rem; border-radius: 0.25rem; font-weight: 600; }
    .latency-pill { color: #4ade80; }
    .orig-text { color: #94a3b8; margin-bottom: 0.5rem; }
    .orig-text p { margin: 0.2rem 0 0 0; }
    .adapted-text {
      background: #172554;
      padding: 0.5rem;
      border-left: 3px solid #38bdf8;
      border-radius: 0.25rem;
    }
    .adapted-text p { margin: 0.2rem 0 0 0; font-style: italic; color: #bae6fd; }
    .lbl { font-size: 0.75rem; text-transform: uppercase; font-weight: 600; }
    .metrics-list { display: flex; flex-direction: column; gap: 0.6rem; font-size: 0.85rem; }
    .m-row { display: flex; justify-content: space-between; }
    .m-row span { color: #94a3b8; }
    .text-success { color: #4ade80; }
    .text-accent { color: #38bdf8; }
  `]
})
export class DialectSynthesisCockpitComponent {
  profiles = signal<DialectProfile[]>([
    {
      code: 'EN-US-SOUTH',
      name: 'US Southern Vernacular',
      idioms: ["y'all", 'appreciate y\'all', 'reckon'],
      pitchShift: -1.2,
      cadenceMultiplier: 0.92
    },
    {
      code: 'EN-GB-SCOTTISH',
      name: 'Scottish English',
      idioms: ['wee bit', 'nae bother', 'aye'],
      pitchShift: 1.5,
      cadenceMultiplier: 1.05
    },
    {
      code: 'EN-SG-SINGLISH',
      name: 'Singapore Colloquial English',
      idioms: ['can lah', 'no issue lah', 'steady'],
      pitchShift: 0.8,
      cadenceMultiplier: 1.12
    }
  ]);

  selectedProfile = signal<DialectProfile | null>(this.profiles()[1]);
  callerTranscript = 'There is a small delay, but yes we will solve it with no problem.';
  activeUtterance = signal<DialectUtterance | null>(null);

  selectProfile(p: DialectProfile) {
    this.selectedProfile.set(p);
  }

  detectAndSynthesize() {
    const p = this.selectedProfile();
    let adapted = this.callerTranscript;

    if (p?.code === 'EN-GB-SCOTTISH') {
      adapted = 'There is a wee delay, but aye we will solve it nae bother.';
    } else if (p?.code === 'EN-US-SOUTH') {
      adapted = "There's a small delay, but we'll sort it for y'all, appreciate y'all!";
    } else if (p?.code === 'EN-SG-SINGLISH') {
      adapted = 'Small delay only, can lah, steady no issue lah!';
    }

    this.activeUtterance.set({
      utteranceId: 'UTT-' + Math.floor(Math.random() * 9000 + 1000),
      dialectCode: p?.code ?? 'EN-GB-SCOTTISH',
      originalText: this.callerTranscript,
      adaptedText: adapted,
      latencyMs: 38
    });
  }
}
