import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface ScoredCallView {
  callId: string;
  leadName: string;
  companyName: string;
  overallScore: number;
  discovery: number;
  objectionHandling: number;
  rapport: number;
  closing: number;
  qualification: 'SalesQualifiedLead' | 'NurtureRequired' | 'Disqualified';
  strengths: string[];
  coachingNotes: string[];
}

export interface RebuttalView {
  category: string;
  framing: string;
  rebuttal: string;
  followUps: string[];
  latencyMs: number;
}

@Component({
  selector: 'app-objection-scoring-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="objection-scoring-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-700/60 pb-6">
        <div>
          <div class="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-rose-400">
            <span class="inline-block w-2 h-2 rounded-full bg-rose-400 animate-pulse"></span>
            Voice AI SDR & Sales Psychology • Horizon 58
          </div>
          <h1 class="text-3xl font-extrabold text-white tracking-tight mt-1">
            Enterprise Voice AI Objection Handling & Call Scoring
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Real-time multi-turn objection classification, psychological value rebuttals & post-call AI coaching rubric evaluations.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="testObjectionRebuttal()"
            data-testid="generate-rebuttal-btn"
            class="px-4 py-2 bg-rose-600 hover:bg-rose-500 text-white rounded-lg text-sm font-semibold transition-colors flex items-center gap-2 shadow-lg shadow-rose-600/20"
          >
            <span>🎙️</span> Test AI Objection Rebuttal
          </button>
        </div>
      </div>

      <!-- KPI Overview Stats -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Average Call Score</div>
          <div class="text-2xl font-bold text-white mt-1" data-testid="stat-avg-score">
            {{ avgScore() }}%
          </div>
          <div class="text-emerald-400 text-xs mt-1">Enterprise B2B Rubric Audited</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">SQL Qualification Rate</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1" data-testid="stat-sql-rate">
            {{ sqlRate() }}%
          </div>
          <div class="text-slate-400 text-xs mt-1">Meetings Confirmed on Calendar</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Top Objection Frequency</div>
          <div class="text-2xl font-bold text-rose-400 mt-1" data-testid="stat-top-objection">
            Budget / ROI
          </div>
          <div class="text-slate-400 text-xs mt-1">42% of Customer Hesitations</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Rebuttal Inference Latency</div>
          <div class="text-2xl font-bold text-sky-400 mt-1">145 ms</div>
          <div class="text-emerald-400 text-xs mt-1">Zero Awkward Silence Lag</div>
        </div>
      </div>

      <!-- Main Stage: Call Quality Ledger & Live Rebuttal Simulator -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <!-- Scored Calls (7 cols) -->
        <div class="lg:col-span-7 bg-slate-800/80 border border-slate-700/60 rounded-xl overflow-hidden">
          <div class="p-4 border-b border-slate-700/60 flex items-center justify-between">
            <h2 class="text-lg font-bold text-white">Audited Voice SDR Sessions</h2>
            <span class="text-xs text-slate-400 font-mono">{{ calls().length }} Evaluated</span>
          </div>

          <div class="divide-y divide-slate-700/40">
            @for (call of calls(); track call.callId) {
              <div
                (click)="selectCall(call)"
                [class.bg-slate-700/40]="selectedCall()?.callId === call.callId"
                class="p-4 hover:bg-slate-700/30 transition-colors cursor-pointer flex flex-col md:flex-row md:items-center justify-between gap-3"
                [attr.data-testid]="'call-row-' + call.callId"
              >
                <div>
                  <div class="flex items-center gap-2">
                    <span class="font-bold text-white text-sm">{{ call.leadName }}</span>
                    <span
                      [class]="call.qualification === 'SalesQualifiedLead' ? 'bg-emerald-900/60 text-emerald-300 border-emerald-700/60' : 'bg-amber-900/60 text-amber-300 border-amber-700/60'"
                      class="px-2 py-0.5 rounded text-[10px] font-semibold border"
                    >
                      {{ call.qualification }}
                    </span>
                  </div>
                  <div class="text-xs text-slate-400 mt-1">
                    Company: <strong class="text-slate-200">{{ call.companyName }}</strong> • Call ID: <span class="font-mono text-slate-300">{{ call.callId }}</span>
                  </div>
                  <div class="text-xs text-slate-400 mt-0.5 flex items-center gap-3">
                    <span>Discovery: <strong class="text-white">{{ call.discovery }}/25</strong></span>
                    <span>Objection: <strong class="text-rose-400">{{ call.objectionHandling }}/25</strong></span>
                    <span>Closing: <strong class="text-emerald-400">{{ call.closing }}/25</strong></span>
                  </div>
                </div>

                <div class="text-right self-end md:self-auto">
                  <div class="text-xs text-slate-400">Rubric Score</div>
                  <div class="text-lg font-extrabold font-mono text-emerald-400">
                    {{ call.overallScore }}%
                  </div>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Call Rubric Details & Rebuttal Simulator Console (5 cols) -->
        <div class="lg:col-span-5 bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 space-y-4">
          <div class="flex items-center justify-between border-b border-slate-700/60 pb-3">
            <div>
              <h3 class="font-bold text-white text-base">Coaching Rubric & Rebuttal Test</h3>
              <p class="text-xs text-slate-400">Voice AI Speech Psychology</p>
            </div>
            <span class="text-xs px-2.5 py-1 rounded bg-rose-950 text-rose-300 border border-rose-800">
              Active Model
            </span>
          </div>

          <!-- Live Objection Tester -->
          <div class="space-y-3">
            <div>
              <label class="block text-slate-300 text-xs font-semibold mb-1">Simulate Customer Objection</label>
              <input
                type="text"
                [(ngModel)]="customUtterance"
                data-testid="input-utterance"
                placeholder="e.g. Your platform seems too expensive compared to HubSpot..."
                class="w-full bg-slate-900 border border-slate-700 rounded-lg p-2.5 text-white text-xs focus:outline-none focus:border-rose-500"
              />
            </div>

            @if (activeRebuttal(); as reb) {
              <div class="p-3 bg-slate-900 rounded-xl border border-slate-800 space-y-2 text-xs" data-testid="rebuttal-result-card">
                <div class="flex items-center justify-between">
                  <span class="font-bold text-rose-400">{{ reb.category }}</span>
                  <span class="text-slate-400 font-mono text-[10px]">{{ reb.latencyMs }} ms</span>
                </div>
                <div class="text-[11px] text-amber-300 font-semibold">Framing: {{ reb.framing }}</div>
                <p class="text-slate-300 text-xs leading-relaxed border-l-2 border-rose-500 pl-2">
                  "{{ reb.rebuttal }}"
                </p>
                <div class="pt-1 text-[11px] text-slate-400">
                  <span class="font-semibold text-slate-300">Suggested Follow-Up:</span>
                  <ul class="list-disc list-inside mt-1 text-slate-400">
                    @for (fu of reb.followUps; track fu) {
                      <li>{{ fu }}</li>
                    }
                  </ul>
                </div>
              </div>
            }
          </div>

          <!-- Selected Call Rubric Coaching Notes -->
          @if (selectedCall(); as call) {
            <div class="pt-3 border-t border-slate-700/60 space-y-2 text-xs">
              <div class="font-bold text-white">AI Coach Analysis ({{ call.leadName }}):</div>
              <div class="space-y-1">
                @for (str of call.strengths; track str) {
                  <div class="p-2 bg-emerald-950/40 border border-emerald-900/60 rounded text-emerald-300 text-[11px]">
                    ✓ {{ str }}
                  </div>
                }
                @for (note of call.coachingNotes; track note) {
                  <div class="p-2 bg-amber-950/40 border border-amber-900/60 rounded text-amber-300 text-[11px]">
                    💡 {{ note }}
                  </div>
                }
              </div>
            </div>
          }
        </div>
      </div>
    </div>
  `
})
export class ObjectionScoringCockpitComponent {
  readonly calls = signal<ScoredCallView[]>([
    {
      callId: 'call_4091',
      leadName: 'David Sterling',
      companyName: 'Apex Cloud Solutions',
      overallScore: 92.0,
      discovery: 24,
      objectionHandling: 23,
      rapport: 22,
      closing: 23,
      qualification: 'SalesQualifiedLead',
      strengths: [
        'Overcame competitor objection with integration orchestration framing.',
        'Secured explicit agreement for pilot architecture review.'
      ],
      coachingNotes: [
        'Ask one more clarifying question around their existing SOC2 compliance review process.'
      ]
    },
    {
      callId: 'call_4092',
      leadName: 'Elena Rostova',
      companyName: 'Nordic FinTech Group',
      overallScore: 84.5,
      discovery: 21,
      objectionHandling: 21,
      rapport: 22,
      closing: 20,
      qualification: 'SalesQualifiedLead',
      strengths: [
        'Demonstrated strong knowledge of data residency requirements.'
      ],
      coachingNotes: [
        'Pivoted on pricing objection slightly late; recommend earlier cost of inaction anchor.'
      ]
    }
  ]);

  readonly selectedCall = signal<ScoredCallView | null>(this.calls()[0]);
  customUtterance = 'We already have HubSpot and Outreach, why would we need Growth OS?';

  readonly activeRebuttal = signal<RebuttalView | null>({
    category: 'Competitor Entrenchment',
    framing: 'Symbiotic Augmentation / Capability Gap Pivot',
    rebuttal: "That is great—they are great core systems of record. We do not ask teams to rip and replace their existing CRM. In fact, Growth OS functions as the real-time AI orchestration layer on top of it, feeding bi-directionally into your CRM while autonomous voice and multi-channel engines run at scale. Have you explored adding autonomous sub-second voice qualification into your current flow?",
    followUps: [
      'Are your reps currently hitting their daily outreach SLAs?',
      'What is your average response time to an inbound high-intent demo request?'
    ],
    latencyMs: 145.0
  });

  readonly avgScore = computed(() => {
    const list = this.calls();
    if (!list.length) return 0;
    return Math.round(list.reduce((acc, c) => acc + c.overallScore, 0) / list.length);
  });

  readonly sqlRate = computed(() => {
    const list = this.calls();
    if (!list.length) return 0;
    const sqls = list.filter(c => c.qualification === 'SalesQualifiedLead').length;
    return Math.round((sqls / list.length) * 100);
  });

  selectCall(call: ScoredCallView): void {
    this.selectedCall.set(call);
  }

  testObjectionRebuttal(): void {
    this.activeRebuttal.set({
      category: 'Budget and Pricing',
      framing: 'Cost of Inaction / Net ROI Anchor',
      rebuttal: "I completely understand budget is top of mind right now. Most enterprise leaders look at this as an immediate consolidation play—typically saving 30-40% by replacing 4 disjointed point solutions while increasing speed to lead by 8x. Would it make sense to run a quick 5-minute ROI model against your current stack?",
      followUps: [
        'What specific tools are currently in your outbound stack?',
        'How many hours per week does your team spend on manual lead qualification?'
      ],
      latencyMs: 138.0
    });
  }
}
