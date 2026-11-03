import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface RfpQuestionnaireRecord {
  questionnaireId: string;
  enterpriseAccountName: string;
  questionnaireType: string;
  totalQuestionsCount: number;
  answeredQuestionsCount: number;
  confidenceScoreAvgPct: number;
  status: string;
}

export interface RfpAnswerRecord {
  answerId: string;
  questionPrompt: string;
  synthesizedAnswer: string;
  canonicalSourceReference: string;
}

@Component({
  selector: 'app-rfp-response-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" data-testid="rfp-response-cockpit" class="p-6 bg-slate-900 text-slate-100 min-h-screen">
      <header class="mb-8 border-b border-slate-800 pb-4 flex justify-between items-center">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-white flex items-center gap-2">
            <span class="inline-block w-3 h-3 rounded-full bg-violet-500 animate-pulse"></span>
            Enterprise RFP Response & Security Questionnaire Mesh
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Automated SOC2/ISO/Cloud Architecture Questionnaire Auto-Fill, Semantic Vector Retrieval & Verified Compliance Attestation
          </p>
        </div>
        <div class="flex gap-3">
          <button (click)="generateNewAnswer()" class="px-4 py-2 bg-violet-600 hover:bg-violet-500 text-white font-medium rounded-lg shadow-sm transition">
            Auto-Synthesize RFP Answer
          </button>
        </div>
      </header>

      <!-- KPI Metrics Grid -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Active RFPs</span>
          <p class="text-2xl font-extrabold text-white mt-1">{{ questionnaires().length }}</p>
          <span class="text-xs text-violet-400">Enterprise Audits</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Synthesized Q&As</span>
          <p class="text-2xl font-extrabold text-white mt-1">195</p>
          <span class="text-xs text-emerald-400">96.0% Auto-Filled</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Confidence Score</span>
          <p class="text-2xl font-extrabold text-white mt-1">95.6%</p>
          <span class="text-xs text-cyan-400">Architect Attested</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Hours Saved</span>
          <p class="text-2xl font-extrabold text-white mt-1">184h</p>
          <span class="text-xs text-indigo-400">Engineering Workload</span>
        </div>
      </section>

      <!-- Main Grid -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-8">
        <!-- Questionnaires Table -->
        <section class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
          <h2 class="text-lg font-semibold text-white mb-4">Enterprise Questionnaires</h2>
          <div class="space-y-4">
            <div *ngFor="let q of questionnaires()" class="bg-slate-900/60 border border-slate-700/40 p-4 rounded-lg">
              <div class="flex justify-between items-center mb-1">
                <span class="font-bold text-white">{{ q.enterpriseAccountName }}</span>
                <span class="text-xs px-2 py-0.5 rounded font-mono bg-violet-950 text-violet-300 border border-violet-800">
                  {{ q.status }}
                </span>
              </div>
              <p class="text-xs text-slate-400 mb-2">Type: {{ q.questionnaireType }}</p>
              <div class="flex justify-between text-xs text-slate-300">
                <span>Progress: {{ q.answeredQuestionsCount }}/{{ q.totalQuestionsCount }} Questions</span>
                <span class="font-mono text-emerald-400 font-bold">{{ q.confidenceScoreAvgPct }}% Confidence</span>
              </div>
            </div>
          </div>
        </section>

        <!-- Synthesized Answers -->
        <section class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
          <h2 class="text-lg font-semibold text-white mb-4">Verified Synthesized Answers</h2>
          <div class="space-y-4">
            <div *ngFor="let a of answers()" class="bg-slate-900/60 border border-slate-700/40 p-4 rounded-lg">
              <span class="text-xs font-bold text-violet-300">{{ a.questionPrompt }}</span>
              <p class="text-xs text-slate-300 my-2 bg-slate-950 p-2.5 rounded border border-slate-800">
                {{ a.synthesizedAnswer }}
              </p>
              <div class="text-[10px] font-mono text-slate-400">
                Source: {{ a.canonicalSourceReference }}
              </div>
            </div>
          </div>
        </section>
      </div>
    </div>
  `
})
export class RfpResponseCockpitComponent {
  questionnaires = signal<RfpQuestionnaireRecord[]>([
    { questionnaireId: 'RFP-SEC-901', enterpriseAccountName: 'Global Morgan Stanley Tech Infrastructure', questionnaireType: 'SECURITY_SOC2_ISO', totalQuestionsCount: 120, answeredQuestionsCount: 115, confidenceScoreAvgPct: 96.4, status: 'READY_FOR_REVIEW' },
    { questionnaireId: 'RFP-COM-902', enterpriseAccountName: 'Nordic Health System Telephony', questionnaireType: 'TECHNICAL_ARCHITECTURE', totalQuestionsCount: 75, answeredQuestionsCount: 68, confidenceScoreAvgPct: 94.8, status: 'IN_PROGRESS' }
  ]);

  answers = signal<RfpAnswerRecord[]>([
    { answerId: 'ANS-SOC2-001', questionPrompt: 'Describe your data encryption posture at rest and in transit.', synthesizedAnswer: 'SGrowing employs AES-256-GCM encryption at rest with customer-managed KMS keys, and TLS 1.3 with mandatory ephemeral mTLS for all inter-pod traffic.', canonicalSourceReference: 'SOC2 Type II Report Section 4.2 & ISO/IEC 27001:2022' }
  ]);

  generateNewAnswer() {
    const newAnswer: RfpAnswerRecord = {
      answerId: `ANS-${Date.now().toString().slice(-4)}`,
      questionPrompt: 'What is your statutory SLA on multi-region failover and data recovery?',
      synthesizedAnswer: 'SGrowing guarantees sub-30 second multi-cloud automatic database failover with zero transactional loss via distributed Raft quorum consensus.',
      canonicalSourceReference: 'Business Continuity & Disaster Recovery Plan 2026'
    };
    this.answers.update(list => [newAnswer, ...list]);
  }
}
