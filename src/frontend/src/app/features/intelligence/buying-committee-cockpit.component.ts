import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface CommitteeMemberView {
  memberId: string;
  fullName: string;
  title: string;
  email: string;
  role: 'Champion' | 'EconomicBuyer' | 'TechnicalEvaluator' | 'LegalProcurement' | 'SecurityCiso' | 'EndUserInfluencer';
  engagementScore: number;
  hasApproved: boolean;
}

export interface PersonaGapView {
  missingRole: string;
  riskLevel: 'Critical' | 'High' | 'Medium';
  recommendation: string;
}

@Component({
  selector: 'app-buying-committee-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="buying-committee-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-700/60 pb-6">
        <div>
          <div class="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-indigo-400">
            <span class="inline-block w-2 h-2 rounded-full bg-indigo-400 animate-pulse"></span>
            Account Intelligence & Deal Strategy • Horizon 66
          </div>
          <h1 class="text-3xl font-extrabold text-white tracking-tight mt-1">
            Enterprise ABM Buying Committee Hierarchy & Consensus Graph
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Autonomous multi-stakeholder mapping, missing persona gap alerts & role-tailored outreach playbooks.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="addSecurityCisoMember()"
            data-testid="add-ciso-btn"
            class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-sm font-semibold transition-colors flex items-center gap-2 shadow-lg shadow-indigo-600/20"
          >
            <span>🛡️</span> Add CISO Stakeholder
          </button>
        </div>
      </div>

      <!-- KPI Overview Stats -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Identified Stakeholders</div>
          <div class="text-2xl font-bold text-white mt-1" data-testid="stat-member-count">
            {{ members().length }} Members
          </div>
          <div class="text-indigo-400 text-xs mt-1">Acme Corporation Org Graph</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Consensus Readiness</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1" data-testid="stat-consensus-rate">
            {{ consensusScore() }}%
          </div>
          <div class="text-slate-400 text-xs mt-1">Deal Readiness: {{ dealReadiness() }}</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Approved Stakeholders</div>
          <div class="text-2xl font-bold text-cyan-400 mt-1" data-testid="stat-approved-count">
            {{ approvedCount() }} of {{ members().length }}
          </div>
          <div class="text-emerald-400 text-xs mt-1">Executive Sign-offs Secured</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Persona Gaps</div>
          <div class="text-2xl font-bold text-amber-400 mt-1" data-testid="stat-gaps-count">
            {{ gaps().length }} Alerts
          </div>
          <div class="text-slate-400 text-xs mt-1">Missing Key Decision Makers</div>
        </div>
      </div>

      <!-- Main Grid: Committee Members vs Gaps & Playbooks -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <!-- Committee Hierarchy (7 cols) -->
        <div class="lg:col-span-7 bg-slate-800/80 border border-slate-700/60 rounded-xl overflow-hidden">
          <div class="p-4 border-b border-slate-700/60 flex items-center justify-between">
            <h2 class="text-lg font-bold text-white">Mapped Buying Committee Roles</h2>
            <span class="text-xs text-slate-400 font-mono">acmecorp.com</span>
          </div>

          <div class="divide-y divide-slate-700/40">
            @for (mem of members(); track mem.memberId) {
              <div class="p-4 hover:bg-slate-700/30 transition-colors flex flex-col md:flex-row md:items-center justify-between gap-3">
                <div>
                  <div class="flex items-center gap-2">
                    <span class="font-bold text-white text-sm">{{ mem.fullName }}</span>
                    <span class="px-2 py-0.5 rounded text-[10px] font-semibold bg-indigo-950 text-indigo-300 border border-indigo-800">
                      {{ mem.role }}
                    </span>
                    @if (mem.hasApproved) {
                      <span class="px-1.5 py-0.5 rounded text-[10px] font-semibold bg-emerald-950 text-emerald-300 border border-emerald-800">
                        ✓ Approved
                      </span>
                    }
                  </div>
                  <div class="text-xs text-slate-400 mt-1">
                    {{ mem.title }} • <span class="font-mono text-cyan-300">{{ mem.email }}</span>
                  </div>
                  <div class="text-xs text-slate-400 mt-0.5">
                    Engagement Index: <strong class="text-indigo-300">{{ mem.engagementScore }}%</strong>
                  </div>
                </div>

                <div class="flex items-center gap-2">
                  @if (!mem.hasApproved) {
                    <button
                      (click)="toggleApprove(mem)"
                      data-testid="approve-member-btn"
                      class="px-3 py-1 bg-emerald-600 hover:bg-emerald-500 text-white font-semibold rounded text-xs transition-colors"
                    >
                      Authorize Sign-off
                    </button>
                  }
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Gaps & Autonomous Playbooks (5 cols) -->
        <div class="lg:col-span-5 space-y-6">
          <!-- Gap Alerts -->
          <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4 space-y-3">
            <h3 class="text-base font-bold text-white flex items-center justify-between">
              <span>Risk & Persona Gap Alerts</span>
              <span class="text-xs text-amber-400 font-normal">{{ gaps().length }} Active</span>
            </h3>

            @if (gaps().length === 0) {
              <div class="p-3 bg-emerald-950/40 border border-emerald-800/60 rounded-lg text-emerald-300 text-xs">
                ✓ Full Buying Committee Coverage Secured. No deal blockers detected.
              </div>
            } @else {
              <div class="space-y-2">
                @for (gap of gaps(); track gap.missingRole) {
                  <div class="p-3 bg-slate-900 border border-slate-700 rounded-lg space-y-1">
                    <div class="flex items-center justify-between">
                      <span class="font-bold text-amber-300 text-xs">Missing: {{ gap.missingRole }}</span>
                      <span class="text-[10px] px-1.5 py-0.5 rounded bg-rose-950 text-rose-300 border border-rose-800 font-semibold">
                        {{ gap.riskLevel }} Risk
                      </span>
                    </div>
                    <p class="text-slate-400 text-xs leading-relaxed">{{ gap.recommendation }}</p>
                  </div>
                }
              </div>
            }
          </div>

          <!-- Role Playbooks -->
          <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4 space-y-3">
            <h3 class="text-base font-bold text-white">Autonomous Persona Playbooks</h3>
            <div class="space-y-2 text-xs">
              <div class="p-3 bg-slate-900 rounded-lg border border-slate-800 space-y-1">
                <div class="font-semibold text-indigo-300">Champion: Internal Business Case & ROI Deck</div>
                <div class="text-slate-400 text-[11px]">Recommended: Slack / Email • 3.4x productivity multiplier.</div>
              </div>
              <div class="p-3 bg-slate-900 rounded-lg border border-slate-800 space-y-1">
                <div class="font-semibold text-emerald-300">Economic Buyer: 3-Year TCO Calculator</div>
                <div class="text-slate-400 text-[11px]">Recommended: Executive Email • Payback in 4.2 months.</div>
              </div>
              <div class="p-3 bg-slate-900 rounded-lg border border-slate-800 space-y-1">
                <div class="font-semibold text-cyan-300">Technical Evaluator: Enterprise Architecture Specs</div>
                <div class="text-slate-400 text-[11px]">Recommended: Technical Briefing • Sub-5ms caching & VPC isolation.</div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  `
})
export class BuyingCommitteeCockpitComponent {
  readonly members = signal<CommitteeMemberView[]>([
    {
      memberId: 'mem_1',
      fullName: 'Sarah Jenkins',
      title: 'VP of Sales Operations',
      email: 'sjenkins@acmecorp.com',
      role: 'Champion',
      engagementScore: 92.5,
      hasApproved: true
    },
    {
      memberId: 'mem_2',
      fullName: 'Michael Vance',
      title: 'Chief Financial Officer',
      email: 'mvance@acmecorp.com',
      role: 'EconomicBuyer',
      engagementScore: 88.0,
      hasApproved: true
    },
    {
      memberId: 'mem_3',
      fullName: 'David Chen',
      title: 'Head of Enterprise Architecture',
      email: 'dchen@acmecorp.com',
      role: 'TechnicalEvaluator',
      engagementScore: 76.0,
      hasApproved: false
    }
  ]);

  readonly gaps = signal<PersonaGapView[]>([
    {
      missingRole: 'SecurityCiso',
      riskLevel: 'High',
      recommendation: 'Missing CISO / InfoSec signoff for enterprise data ingress.'
    },
    {
      missingRole: 'LegalProcurement',
      riskLevel: 'Medium',
      recommendation: 'Procurement contact missing. MSA redlining may be delayed.'
    }
  ]);

  readonly approvedCount = computed(() =>
    this.members().filter(m => m.hasApproved).length
  );

  readonly consensusScore = computed(() => {
    const list = this.members();
    if (list.length === 0) return 0;
    const approved = this.approvedCount();
    const avgEngagement = list.reduce((acc, m) => acc + m.engagementScore, 0) / list.length;
    return Math.round(((approved / list.length) * 60) + (avgEngagement * 0.4));
  });

  readonly dealReadiness = computed(() => {
    const score = this.consensusScore();
    if (score >= 80 && this.gaps().length === 0) return 'Consensus Ready To Close';
    if (score >= 50) return 'Moderate Progress';
    return 'High Risk Incomplete';
  });

  toggleApprove(member: CommitteeMemberView): void {
    this.members.update(list =>
      list.map(m => m.memberId === member.memberId ? { ...m, hasApproved: true } : m)
    );
  }

  addSecurityCisoMember(): void {
    const ciso: CommitteeMemberView = {
      memberId: 'mem_ciso_99',
      fullName: 'Elena Rostova',
      title: 'Chief Information Security Officer',
      email: 'erostova@acmecorp.com',
      role: 'SecurityCiso',
      engagementScore: 91.0,
      hasApproved: true
    };

    this.members.update(list => [...list, ciso]);
    this.gaps.update(list => list.filter(g => g.missingRole !== 'SecurityCiso'));
  }
}
