import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface StageNodeUi {
  id: string;
  name: string;
  stage: string;
  waitCondition?: string;
  dropoffRate: number;
  enrolledCount: number;
}

export interface CustomerJourneyUi {
  id: string;
  name: string;
  audience: string;
  stages: StageNodeUi[];
  maxTouchesPerWeek: number;
  conversionRate: number;
  activeEnrollments: number;
}

@Component({
  selector: 'app-journey-orchestrator-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="journey-orchestrator-cockpit p-6 max-w-7xl mx-auto space-y-6" dir="ltr">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b pb-6">
        <div>
          <div class="flex items-center gap-3">
            <h1 class="text-2xl font-bold tracking-tight text-slate-900">Visual Customer Lifecycle Journey Orchestrator</h1>
            <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-indigo-100 text-indigo-800">
              Omnichannel Fatigue Gate Active
            </span>
          </div>
          <p class="text-sm text-slate-500 mt-1">
            Orchestrate multi-stage customer progressions with behavioral wait-until gates, drop-off analytics, and frequency cap defenses.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button 
            (click)="showEnrollModal.set(true)"
            class="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg font-medium text-xs transition-colors shadow-sm"
            data-testid="btn-open-enroll">
            + Enroll Contact
          </button>
        </div>
      </div>

      <!-- Lifecycle KPIs -->
      <div class="grid grid-cols-1 sm:grid-cols-4 gap-4" data-testid="journey-kpis">
        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Active Journey Contacts</div>
          <div class="text-2xl font-bold text-slate-900 mt-1 font-mono" data-testid="kpi-enrolled-count">
            {{ activeJourney().activeEnrollments | number:'1.0-0' }}
          </div>
          <div class="text-[11px] text-indigo-600 mt-1 font-semibold">Across 4 Progression Stages</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">End-to-End Conversion Rate</div>
          <div class="text-2xl font-bold text-emerald-600 mt-1 font-mono" data-testid="kpi-conversion-rate">
            {{ activeJourney().conversionRate }}%
          </div>
          <div class="text-[11px] text-slate-400 mt-1">Lead to Enterprise ARR</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Fatigue Cap Threshold</div>
          <div class="text-2xl font-bold text-slate-900 mt-1 font-mono">
            {{ activeJourney().maxTouchesPerWeek }} touches / wk
          </div>
          <div class="text-[11px] text-slate-400 mt-1">24h Cooldown Spacing</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Fatigue Suppression Shield</div>
          <div class="text-2xl font-bold text-emerald-600 mt-1 font-mono">342 Suppressed</div>
          <div class="text-[11px] text-slate-400 mt-1">Unsubscribe Churn Prevented</div>
        </div>
      </div>

      <!-- Main Visual Journey Canvas -->
      <div class="bg-white border rounded-xl p-6 shadow-sm space-y-6">
        <div class="flex items-center justify-between">
          <div>
            <h3 class="font-bold text-slate-900 text-sm">{{ activeJourney().name }}</h3>
            <span class="text-xs text-slate-400">Audience: {{ activeJourney().audience }}</span>
          </div>
          <span class="px-2.5 py-1 rounded-full text-xs font-semibold bg-emerald-50 text-emerald-700 border border-emerald-200">
            ● Live Journey Running
          </span>
        </div>

        <!-- Stage Nodes Visual Flow -->
        <div class="grid grid-cols-1 md:grid-cols-4 gap-4 relative" data-testid="journey-stages-flow">
          @for (stage of activeJourney().stages; track stage.id; let idx = $index) {
            <div class="p-4 border rounded-xl bg-slate-50/70 hover:bg-white hover:border-indigo-300 transition-all space-y-3 relative group">
              <div class="flex justify-between items-center text-xs">
                <span class="px-2 py-0.5 rounded font-mono font-bold text-[10px] bg-indigo-100 text-indigo-800">
                  Step 0{{ idx + 1 }}
                </span>
                <span class="text-[11px] text-rose-600 font-medium">
                  &darr; {{ stage.dropoffRate }}% drop
                </span>
              </div>

              <div>
                <h4 class="font-bold text-slate-900 text-sm">{{ stage.name }}</h4>
                <span class="text-[11px] text-slate-500 block font-mono">{{ stage.stage }}</span>
              </div>

              @if (stage.waitCondition) {
                <div class="p-2 bg-amber-50 border border-amber-200 rounded text-[11px] text-amber-800">
                  <span class="font-bold block">Wait-Until Gate:</span>
                  {{ stage.waitCondition }}
                </div>
              }

              <div class="pt-2 border-t flex justify-between items-center text-xs text-slate-500 font-mono">
                <span>In Stage:</span>
                <strong class="text-slate-800">{{ stage.enrolledCount }} contacts</strong>
              </div>
            </div>
          }
        </div>
      </div>

      <!-- Interactive Fatigue Policy Tester -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div class="lg:col-span-2 bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <h3 class="font-bold text-slate-900 text-sm">Fatigue Policy Pre-Flight Simulator</h3>
          <p class="text-xs text-slate-500">
            Test whether an outbound touch (WhatsApp, Voice, Email) violates rolling frequency caps or 24h spacing rules for a given contact.
          </p>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div>
              <label class="block text-xs font-semibold text-slate-700 mb-1">Target Contact Email</label>
              <input 
                type="text" 
                [(ngModel)]="testContactEmail" 
                class="w-full px-3 py-2 border rounded-lg text-xs font-mono focus:ring-2 focus:ring-indigo-500 focus:outline-none"
                data-testid="input-fatigue-email" />
            </div>

            <div>
              <label class="block text-xs font-semibold text-slate-700 mb-1">Outbound Channel</label>
              <select 
                [(ngModel)]="testChannel" 
                class="w-full px-3 py-2 border rounded-lg text-xs focus:ring-2 focus:ring-indigo-500 focus:outline-none"
                data-testid="select-fatigue-channel">
                <option value="WhatsApp">WhatsApp Broadcast</option>
                <option value="Voice">AI SDR Voice Call</option>
                <option value="Email">Outbound Email Sequence</option>
              </select>
            </div>
          </div>

          <button 
            (click)="testFatigue()"
            class="px-4 py-2 bg-slate-900 hover:bg-slate-800 text-white rounded-lg text-xs font-medium shadow-sm"
            data-testid="btn-test-fatigue">
            Check Outreach Eligibility
          </button>

          @if (fatigueCheckResult()) {
            <div class="p-3.5 rounded-xl border text-xs"
              [ngClass]="fatigueCheckResult()?.canSend ? 'bg-emerald-50 border-emerald-200 text-emerald-900' : 'bg-rose-50 border-rose-200 text-rose-900'"
              data-testid="fatigue-result-card">
              <div class="font-bold text-sm mb-1">
                {{ fatigueCheckResult()?.canSend ? '✓ Approved for Outreach' : '✕ Outbound Touch Blocked (Fatigue Shield)' }}
              </div>
              <p>{{ fatigueCheckResult()?.reason }}</p>
            </div>
          }
        </div>

        <!-- Enrollment Quick Summary -->
        <div class="bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <h3 class="font-bold text-slate-900 text-sm">Recent Journey Enrollments</h3>
          <div class="space-y-3" data-testid="recent-enrollments-list">
            @for (enr of enrollments(); track enr.email) {
              <div class="p-3 border rounded-lg text-xs space-y-1">
                <div class="flex justify-between items-center">
                  <span class="font-mono font-semibold text-slate-800">{{ enr.email }}</span>
                  <span class="px-1.5 py-0.5 rounded text-[10px] font-bold bg-indigo-50 text-indigo-700">
                    {{ enr.stage }}
                  </span>
                </div>
                <div class="text-[11px] text-slate-400">Touches this week: {{ enr.touches }} / 3</div>
              </div>
            }
          </div>
        </div>
      </div>

      <!-- Enroll Modal -->
      @if (showEnrollModal()) {
        <div class="fixed inset-0 bg-slate-900/40 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div class="bg-white rounded-2xl max-w-md w-full p-6 space-y-4 shadow-xl border">
            <div class="flex items-center justify-between border-b pb-3">
              <h3 class="font-bold text-slate-900 text-base">Enroll Target Contact in Journey</h3>
              <button (click)="showEnrollModal.set(false)" class="text-slate-400 hover:text-slate-600">&times;</button>
            </div>

            <div class="space-y-3">
              <div>
                <label class="block text-xs font-semibold text-slate-700 mb-1">Work Email</label>
                <input 
                  type="email" 
                  [(ngModel)]="newContactEmail" 
                  class="w-full px-3 py-2 border rounded-lg text-xs font-mono focus:ring-2 focus:ring-indigo-500 focus:outline-none"
                  data-testid="input-new-enroll-email" />
              </div>
            </div>

            <div class="flex justify-end gap-2 pt-3 border-t">
              <button 
                (click)="showEnrollModal.set(false)"
                class="px-3 py-1.5 border rounded-lg text-xs font-medium text-slate-600 hover:bg-slate-50">
                Cancel
              </button>
              <button 
                (click)="enrollContact()"
                class="px-4 py-1.5 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-xs font-medium shadow-sm"
                data-testid="btn-submit-enroll">
                Enroll Contact
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class JourneyOrchestratorCockpitComponent {
  showEnrollModal = signal(false);

  activeJourney = signal<CustomerJourneyUi>({
    id: 'jrn_enterprise_plo',
    name: 'Enterprise Product-Led Onboarding & Expansion',
    audience: 'B2B SaaS Founders & RevOps Leaders',
    maxTouchesPerWeek: 3,
    conversionRate: 46.8,
    activeEnrollments: 1420,
    stages: [
      { id: 's1', name: 'Inbound Lead Qualification', stage: 'LeadCapture', waitCondition: 'Wait 24h for email verification', dropoffRate: 12.5, enrolledCount: 412 },
      { id: 's2', name: 'First AI Agent Deployment', stage: 'TrialOnboarding', waitCondition: 'Wait 48h for first agent run', dropoffRate: 18.2, enrolledCount: 520 },
      { id: 's3', name: 'Team Seeding & Expansion', stage: 'ValueActivation', waitCondition: 'Wait 72h for 5 team invites', dropoffRate: 14.0, enrolledCount: 340 },
      { id: 's4', name: 'Annual Enterprise Upgrade', stage: 'AccountExpansion', dropoffRate: 8.5, enrolledCount: 148 }
    ]
  });

  enrollments = signal<{ email: string; stage: string; touches: number }[]>([
    { email: 'cto@acme.corp', stage: 'TrialOnboarding', touches: 2 },
    { email: 'vp_sales@growthly.io', stage: 'ValueActivation', touches: 1 },
    { email: 'head_ops@nexus.com', stage: 'LeadCapture', touches: 0 }
  ]);

  testContactEmail = 'cto@acme.corp';
  testChannel = 'WhatsApp';
  fatigueCheckResult = signal<{ canSend: boolean; reason: string } | null>(null);

  newContactEmail = 'founder@startup.io';

  testFatigue() {
    if (this.testContactEmail.includes('acme')) {
      this.fatigueCheckResult.set({
        canSend: false,
        reason: 'Contact received touch 18 hours ago. Minimum 24-hour cooldown spacing active.'
      });
    } else {
      this.fatigueCheckResult.set({
        canSend: true,
        reason: 'Contact has 0 touches this week and is fully eligible for outreach.'
      });
    }
  }

  enrollContact() {
    this.enrollments.update(list => [
      { email: this.newContactEmail, stage: 'LeadCapture', touches: 0 },
      ...list
    ]);
    this.activeJourney.update(j => ({
      ...j,
      activeEnrollments: j.activeEnrollments + 1
    }));
    this.showEnrollModal.set(false);
  }
}
