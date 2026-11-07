import { Component, signal, computed, inject, Optional, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslationService } from '../../core/services/translation.service';

export type LinkedInTouchType =
  | 'ProfileView'
  | 'ConnectionRequest'
  | 'PersonalizedFollowUp'
  | 'InMailMessage'
  | 'PostEngagement';

export type CadenceStepStatus =
  | 'Scheduled'
  | 'PendingApproval'
  | 'Sent'
  | 'Replied'
  | 'Skipped';

export type ProspectSeniority =
  | 'CXO'
  | 'VP'
  | 'Director'
  | 'Manager'
  | 'IndividualContributor';

export interface LinkedInProspectProfile {
  prospectId: string;
  fullName: string;
  jobTitle: string;
  companyName: string;
  seniority: ProspectSeniority;
  linkedInProfileUrl: string;
  location: string;
  industry: string;
  connected: boolean;
  companyDomain?: string;
  enrichedBio?: string;
  recentInterests?: string[];
}

export interface LinkedInCadenceStepRecord {
  stepId: string;
  cadenceId: string;
  prospectId: string;
  prospect?: LinkedInProspectProfile;
  dayOffset: number;
  touchType: LinkedInTouchType;
  status: CadenceStepStatus;
  messageTemplate: string;
  customizedContent?: string;
  executionTimestampUtc?: string;
  pacingDelaySeconds: number;
  aiPersonalizationScore: number;
  responseReceived?: string;
}

export interface LinkedInCampaignCadence {
  cadenceId: string;
  tenantId: string;
  name: string;
  targetPersona: string;
  description: string;
  isActive: boolean;
  dailyActionLimit: number;
  createdAtUtc: string;
  steps: LinkedInCadenceStepRecord[];
  activeProspectCount: number;
  connectionRatePercent: number;
  replyRatePercent: number;
}

export interface DraftMessageAiRequest {
  touchType: LinkedInTouchType;
  prospectName: string;
  companyName: string;
  jobTitle: string;
  seniority: ProspectSeniority;
  industry: string;
  keyPainPoint?: string;
  language: 'en' | 'ar';
}

export interface DraftMessageAiResponse {
  draftedSubject?: string;
  draftedMessage: string;
  touchType: LinkedInTouchType;
  personalizationHook: string;
  pacingWaitMinutes: number;
  confidenceScore: number;
  recommendedHashtags: string[];
}

export function getTouchTypeMeta(touch: LinkedInTouchType): { label: string; icon: string; badgeClass: string } {
  switch (touch) {
    case 'ProfileView':
      return { label: 'Profile View', icon: '👀', badgeClass: 'bg-cyan-500/20 text-cyan-300 border-cyan-500/30' };
    case 'PostEngagement':
      return { label: 'Post Engagement', icon: '💬', badgeClass: 'bg-purple-500/20 text-purple-300 border-purple-500/30' };
    case 'ConnectionRequest':
      return { label: 'Connection Invite', icon: '🤝', badgeClass: 'bg-emerald-500/20 text-emerald-300 border-emerald-500/30' };
    case 'PersonalizedFollowUp':
      return { label: 'Personalized Follow-Up', icon: '📬', badgeClass: 'bg-amber-500/20 text-amber-300 border-amber-500/30' };
    case 'InMailMessage':
      return { label: 'Executive InMail', icon: '⭐', badgeClass: 'bg-blue-500/20 text-blue-300 border-blue-500/30' };
  }
}

export function getStepStatusMeta(status: CadenceStepStatus): { label: string; badgeClass: string } {
  switch (status) {
    case 'Scheduled':
      return { label: 'Scheduled', badgeClass: 'bg-slate-700/60 text-slate-300 border-slate-600' };
    case 'PendingApproval':
      return { label: 'Pending Approval', badgeClass: 'bg-amber-500/20 text-amber-300 border-amber-500/40 animate-pulse' };
    case 'Sent':
      return { label: 'Sent (Paced)', badgeClass: 'bg-blue-500/20 text-blue-300 border-blue-500/30' };
    case 'Replied':
      return { label: 'Replied (Goal Met)', badgeClass: 'bg-emerald-500/20 text-emerald-300 border-emerald-500/30 font-semibold' };
    case 'Skipped':
      return { label: 'Skipped', badgeClass: 'bg-rose-500/20 text-rose-300 border-rose-500/30' };
  }
}

export function calculatePacingSafety(used: number, limit: number): { status: string; percentage: number; risk: 'Safe' | 'Moderate' | 'Warning' | 'Locked' } {
  const percentage = Math.min(100, Math.round((used / Math.max(1, limit)) * 100));
  if (used >= limit) {
    return {
      status: `Pacing Lock Engaged (${used}/${limit}) - Daily Cap Reached to Prevent LinkedIn Restriction`,
      percentage,
      risk: 'Locked'
    };
  }
  if (percentage >= 80) {
    return {
      status: `Approaching Quota (${used}/${limit}) - Throttling Pacing Interval (180s+ jitter)`,
      percentage,
      risk: 'Warning'
    };
  }
  if (percentage >= 50) {
    return {
      status: `Moderate Pacing Velocity (${used}/${limit}) - Randomized 60-120s Delays`,
      percentage,
      risk: 'Moderate'
    };
  }
  return {
    status: `Optimal Anti-Bot Health (${used}/${limit}) - Human Dwell-Time Emulation Active`,
    percentage,
    risk: 'Safe'
  };
}

export function generateLocalAiDraft(request: DraftMessageAiRequest): DraftMessageAiResponse {
  const isArabic = request.language === 'ar';
  const painPoint = request.keyPainPoint ||
    (isArabic ? 'تحقيق الامتثال السحابي وتخفيض زمن استجابة البنية المؤسسية' : 'achieving sovereign cloud compliance and enterprise system latency reduction');

  let subject = '';
  let message = '';
  let hook = '';
  let hashtags: string[] = [];
  let waitMinutes = 25;
  let confidence = 0.95;

  switch (request.touchType) {
    case 'ProfileView':
      waitMinutes = 15;
      confidence = 0.94;
      hashtags = ['#GCCEnterprise', '#DigitalTransformation'];
      if (isArabic) {
        subject = 'متابعة الملف الشخصي - اهتمام مشترك';
        hook = `الاطلاع على مبادرات ${request.companyName} في التحول الرقمي`;
        message = `أهلاً بك أستاذ ${request.prospectName}، تشرفت بالاطلاع على مسيرتكم الرائدة في ${request.companyName} وخاصة فيما يتعلق بـ ${painPoint}.`;
      } else {
        subject = 'Profile Interaction - Shared Enterprise Focus';
        hook = `Tracking ${request.companyName}'s strategic initiatives in ${request.industry}`;
        message = `Hello ${request.prospectName}, reviewed your recent updates regarding ${request.companyName}'s technology milestones, particularly around ${painPoint}. Impressive leadership.`;
      }
      break;

    case 'ConnectionRequest':
      waitMinutes = 30;
      confidence = 0.97;
      hashtags = ['#Vision2030', '#SaudiTech', '#FinTechGCC', '#CloudResilience'];
      if (isArabic) {
        subject = 'طلب تواصل - شبكة قيادات التقنية الخليجية';
        hook = `التوافق مع مستهدفات رؤية 2030 وتحديثات ${request.companyName}`;
        message = `السلام عليكم أستاذ ${request.prospectName}، يسعدني التواصل معكم لمتابعة تجاربكم في ${request.companyName}، وتبادل الرؤى حول أفضل ممارسات ${painPoint} في البيئة المؤسسية الخليجية.`;
      } else {
        subject = 'Connection Request - GCC Technology Leadership';
        hook = `Alignment with GCC digital modernization and ${request.companyName}'s architecture`;
        message = `Hi ${request.prospectName}, following ${request.companyName}'s growth with high admiration. Connecting to exchange peer benchmarks on solving ${painPoint} across the GCC enterprise ecosystem.`;
      }
      break;

    case 'PostEngagement':
      waitMinutes = 20;
      confidence = 0.95;
      hashtags = ['#EnterpriseArchitecture', '#GCCBusiness', '#Leadership'];
      if (isArabic) {
        subject = 'مشاركة تعليق نوعي على منشور القيادة';
        hook = `تحليل معمق يدعم أفكار ${request.prospectName} المنشورة`;
        message = `طرح قيّم جداً أستاذ ${request.prospectName}. بالفعل أثبتت التجربة في السوق الإقليمي أن معالجة ${painPoint} هي الأساس لتسريع العائد على الاستثمار التقني.`;
      } else {
        subject = 'Thought Leadership Insight Commentary';
        hook = `Value-add comment addressing ${request.prospectName}'s recent published viewpoints`;
        message = `Exceptional insights, ${request.prospectName}. In our recent GCC benchmark data, prioritizing ${painPoint} reduced operational friction by over 38% for enterprise leaders.`;
      }
      break;

    case 'PersonalizedFollowUp':
      waitMinutes = 45;
      confidence = 0.96;
      hashtags = ['#CaseStudy', '#EnterpriseBenchmark', '#GCCSuccess'];
      if (isArabic) {
        subject = 'تقرير مقارن حصري: قياس أداء المنظومات الرقمية في الخليج';
        hook = `دراسة حالة حصرية لقطاع ${request.industry}`;
        message = `مرحباً أستاذ ${request.prospectName}، شكراً لتواصلكم الكريم. أرفق لكم دراسة حالة موجزة حول كيفية مساعدة قيادات ${request.industry} في التغلب على ${painPoint} بكفاءة عالية وفق المعايير المحلية.`;
      } else {
        subject = 'Exclusive Benchmark: GCC Enterprise System Scalability';
        hook = `Dedicated case study for ${request.industry} decision-makers`;
        message = `Hi ${request.prospectName}, thanks for connecting. Sharing our newly released GCC benchmark briefing highlighting how ${request.industry} enterprises are successfully addressing ${painPoint}. Happy to share full findings.`;
      }
      break;

    case 'InMailMessage':
    default:
      waitMinutes = 60;
      confidence = 0.93;
      hashtags = ['#ExecutiveBriefing', '#GCCExpansion', '#CTOCommunity'];
      if (isArabic) {
        subject = `إحاطة تنفيذية موجزة: أثر حلول ${painPoint} في ${request.companyName}`;
        hook = `جلسة حوارية مخصصة للرؤساء التنفيذيين في الرياض ودبي`;
        message = `تحية طيبة أستاذ ${request.prospectName}، بصفتكم ${request.jobTitle} في ${request.companyName}، نود دعوتكم لجلسة نقاشية مغلقة لـ 15 دقيقة لاستعراض كيف تعالج المؤسسات الكبرى في المملكة والإمارات تحديات ${painPoint}. هل يناسبكم الثلاثاء القادم؟`;
      } else {
        subject = `Executive Briefing: Accelerating Resilience for ${request.companyName}`;
        hook = `Direct peer-to-peer 15-minute briefing invitation for ${request.seniority} leaders`;
        message = `Dear ${request.prospectName}, given your leadership as ${request.jobTitle} at ${request.companyName}, I would welcome a concise 15-minute peer exchange to discuss how leading GCC firms tackle ${painPoint}. Let me know if midweek works for you.`;
      }
      break;
  }

  return {
    draftedSubject: subject,
    draftedMessage: message,
    touchType: request.touchType,
    personalizationHook: hook,
    pacingWaitMinutes: waitMinutes,
    confidenceScore: confidence,
    recommendedHashtags: hashtags
  };
}

@Component({
  selector: 'app-linkedin-outreach-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="min-h-screen bg-slate-950 text-slate-100 p-4 sm:p-6 lg:p-8" [dir]="isRtl() ? 'rtl' : 'ltr'">
      <!-- Top Bar / Header -->
      <header class="flex flex-col md:flex-row md:items-center justify-between pb-6 border-b border-slate-800 gap-4">
        <div>
          <div class="flex items-center gap-3">
            <span class="inline-flex items-center justify-center p-2 rounded-lg bg-blue-600/20 text-blue-400 border border-blue-500/30 text-xl font-bold">
              in
            </span>
            <div>
              <h1 class="text-2xl sm:text-3xl font-extrabold tracking-tight text-white flex items-center gap-2">
                LinkedIn B2B Outreach & Social Selling Cadence Studio
              </h1>
              <p class="text-xs sm:text-sm text-slate-400">
                Domain D4, D8 & F2 &bull; Multi-Touch Cadences, Bilingual AI Personalization & Anti-Bot Pacing Safeguards
              </p>
            </div>
          </div>
        </div>

        <div class="flex items-center gap-3 self-end md:self-center">
          <span class="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
            <span class="w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
            Pacing Engine: Active
          </span>
          <button
            type="button"
            (click)="resetDailyQuota()"
            class="px-3 py-1.5 text-xs font-medium bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-md border border-slate-700 transition"
            title="Simulate 24-hour quota reset for anti-bot simulation">
            Reset 24h Quota
          </button>
        </div>
      </header>

      <!-- KPI Summary Cards -->
      <section class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3 sm:gap-4 my-6">
        <div class="bg-slate-900/90 border border-slate-800 rounded-xl p-4 flex flex-col justify-between">
          <span class="text-xs font-medium text-slate-400 uppercase tracking-wider">Active Cadences</span>
          <div class="mt-2 flex items-baseline justify-between">
            <span class="text-2xl font-bold text-white">{{ cadences().length }}</span>
            <span class="text-xs text-blue-400 font-medium">B2B Sequences</span>
          </div>
        </div>

        <div class="bg-slate-900/90 border border-slate-800 rounded-xl p-4 flex flex-col justify-between">
          <span class="text-xs font-medium text-slate-400 uppercase tracking-wider">Prospects in Pipeline</span>
          <div class="mt-2 flex items-baseline justify-between">
            <span class="text-2xl font-bold text-emerald-400">{{ activeProspectCount() }}</span>
            <span class="text-xs text-emerald-500/80 font-medium">Enterprise VIPs</span>
          </div>
        </div>

        <div class="bg-slate-900/90 border border-slate-800 rounded-xl p-4 flex flex-col justify-between">
          <span class="text-xs font-medium text-slate-400 uppercase tracking-wider">Executed Touches</span>
          <div class="mt-2 flex items-baseline justify-between">
            <span class="text-2xl font-bold text-blue-400">{{ totalExecutedTouches() }}</span>
            <span class="text-xs text-slate-400 font-medium">Sent & Paced</span>
          </div>
        </div>

        <div class="bg-slate-900/90 border border-slate-800 rounded-xl p-4 flex flex-col justify-between">
          <span class="text-xs font-medium text-slate-400 uppercase tracking-wider">Pending Approvals</span>
          <div class="mt-2 flex items-baseline justify-between">
            <span class="text-2xl font-bold text-amber-400">{{ pendingApprovalCount() }}</span>
            <span class="text-xs text-amber-500 font-medium">Requires Review</span>
          </div>
        </div>

        <div class="bg-slate-900/90 border border-slate-800 rounded-xl p-4 flex flex-col justify-between">
          <span class="text-xs font-medium text-slate-400 uppercase tracking-wider">Avg Connection Rate</span>
          <div class="mt-2 flex items-baseline justify-between">
            <span class="text-2xl font-bold text-purple-400">{{ avgConnectionRate() }}%</span>
            <span class="text-xs text-purple-400/80 font-medium">Target &gt; 35%</span>
          </div>
        </div>

        <div class="bg-slate-900/90 border border-slate-800 rounded-xl p-4 flex flex-col justify-between">
          <span class="text-xs font-medium text-slate-400 uppercase tracking-wider">Anti-Bot Quota</span>
          <div class="mt-2 flex items-baseline justify-between">
            <span class="text-2xl font-bold" [ngClass]="pacingSafety().risk === 'Locked' ? 'text-rose-400' : 'text-cyan-400'">
              {{ dailyQuotaUsed() }}/{{ dailyQuotaLimit() }}
            </span>
            <span class="text-xs" [ngClass]="pacingSafety().risk === 'Locked' ? 'text-rose-400' : 'text-slate-400'">
              {{ pacingSafety().percentage }}% Used
            </span>
          </div>
        </div>
      </section>

      <!-- Anti-Bot Health Banner -->
      <section class="mb-6 p-4 rounded-xl border flex flex-col md:flex-row md:items-center justify-between gap-4"
        [ngClass]="{
          'bg-cyan-950/30 border-cyan-800/40 text-cyan-200': pacingSafety().risk === 'Safe',
          'bg-amber-950/30 border-amber-800/40 text-amber-200': pacingSafety().risk === 'Moderate' || pacingSafety().risk === 'Warning',
          'bg-rose-950/30 border-rose-800/40 text-rose-200': pacingSafety().risk === 'Locked'
        }">
        <div class="flex items-center gap-3">
          <span class="text-2xl">
            @switch (pacingSafety().risk) {
              @case ('Safe') { 🛡️ }
              @case ('Moderate') { ⏳ }
              @case ('Warning') { ⚠️ }
              @case ('Locked') { 🛑 }
            }
          </span>
          <div>
            <div class="font-bold text-sm tracking-wide flex items-center gap-2">
              <span>Anti-Bot Account Protection System: {{ pacingSafety().risk }} Mode</span>
              <span class="text-xs font-mono px-2 py-0.5 rounded bg-black/40">
                Jitter delay: {{ pacingIntervalSeconds() }}s &plusmn; {{ jitterWindowSeconds() }}s
              </span>
            </div>
            <p class="text-xs mt-0.5 opacity-90">
              {{ pacingSafety().status }}
            </p>
          </div>
        </div>

        <div class="w-full md:w-64 bg-black/40 rounded-full h-2.5 overflow-hidden border border-slate-700/50">
          <div
            class="h-full transition-all duration-500"
            [style.width.%]="pacingSafety().percentage"
            [ngClass]="{
              'bg-cyan-500': pacingSafety().risk === 'Safe',
              'bg-amber-500': pacingSafety().risk === 'Moderate' || pacingSafety().risk === 'Warning',
              'bg-rose-500': pacingSafety().risk === 'Locked'
            }">
          </div>
        </div>
      </section>

      <!-- Main Layout: Sequence Visualizer + Steps List + AI Sandbox -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <!-- Left: Cadence Selection & Multi-Touch Sequence Flow (4 cols) -->
        <div class="lg:col-span-4 space-y-6">
          <!-- Cadence Selector Box -->
          <div class="bg-slate-900 border border-slate-800 rounded-xl p-5">
            <h2 class="text-sm font-semibold uppercase tracking-wider text-slate-400 mb-3">
              Selected Campaign Cadence
            </h2>

            <div class="space-y-3">
              @for (cadence of cadences(); track cadence.cadenceId) {
                <div
                  (click)="onSelectCadence(cadence.cadenceId)"
                  class="cursor-pointer p-3.5 rounded-lg border transition-all"
                  [ngClass]="cadence.cadenceId === selectedCadenceId()
                    ? 'bg-blue-950/40 border-blue-500/60 shadow-lg shadow-blue-950/20'
                    : 'bg-slate-950/50 border-slate-800 hover:border-slate-700'">
                  <div class="flex items-center justify-between">
                    <h3 class="font-bold text-sm text-white">{{ cadence.name }}</h3>
                    <span class="text-xs px-2 py-0.5 rounded-full"
                      [ngClass]="cadence.isActive ? 'bg-emerald-500/20 text-emerald-300' : 'bg-slate-700 text-slate-400'">
                      {{ cadence.isActive ? 'Active' : 'Paused' }}
                    </span>
                  </div>
                  <p class="text-xs text-slate-400 mt-1 line-clamp-2">{{ cadence.description }}</p>
                  <div class="flex items-center gap-4 mt-3 pt-2 border-t border-slate-800/80 text-[11px] text-slate-400">
                    <span>Target: <strong class="text-slate-200">{{ cadence.targetPersona }}</strong></span>
                  </div>
                </div>
              }
            </div>

            <!-- New Cadence Trigger Button -->
            <button
              type="button"
              (click)="createNewCadencePrompt()"
              class="w-full mt-4 py-2 px-3 text-xs font-semibold rounded-lg border border-dashed border-slate-700 text-slate-400 hover:text-white hover:border-blue-500/50 hover:bg-blue-950/20 transition flex items-center justify-center gap-1.5">
              <span>+ Add Enterprise Cadence</span>
            </button>
          </div>

          <!-- Multi-Touch Sequence Visualizer Flow -->
          <div class="bg-slate-900 border border-slate-800 rounded-xl p-5">
            <h2 class="text-sm font-semibold uppercase tracking-wider text-slate-400 mb-3 flex items-center justify-between">
              <span>Cadence Sequence Blueprint</span>
              <span class="text-xs font-normal lowercase text-blue-400">5-touch omnichannel</span>
            </h2>

            <div class="relative pl-6 space-y-4 before:content-[''] before:absolute before:left-2 before:top-2 before:bottom-2 before:w-0.5 before:bg-slate-800">
              @for (node of sequenceTimeline(); track node.day) {
                <div class="relative group">
                  <div class="absolute -left-[23px] top-1 w-4 h-4 rounded-full bg-slate-900 border-2"
                    [ngClass]="node.active ? 'border-blue-500 bg-blue-600' : 'border-slate-600'">
                  </div>
                  <div class="p-3 rounded-lg bg-slate-950/60 border border-slate-800/80">
                    <div class="flex items-center justify-between text-xs font-medium">
                      <span class="text-blue-400 font-bold">Day {{ node.day }}</span>
                      <span class="px-2 py-0.5 rounded text-[10px]" [ngClass]="node.badgeClass">
                        {{ node.icon }} {{ node.title }}
                      </span>
                    </div>
                    <p class="text-xs text-slate-300 mt-1.5">{{ node.action }}</p>
                    <span class="text-[10px] text-slate-500 mt-1 block">Anti-bot delay: ~{{ node.delay }}s</span>
                  </div>
                </div>
              }
            </div>
          </div>
        </div>

        <!-- Center: Interactive Step Queue & Approvals (5 cols) -->
        <div class="lg:col-span-5 space-y-6">
          <div class="bg-slate-900 border border-slate-800 rounded-xl p-5">
            <!-- Header & Filters -->
            <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-4 border-b border-slate-800">
              <div>
                <h2 class="text-base font-bold text-white flex items-center gap-2">
                  <span>Prospect Step Queue</span>
                  <span class="text-xs font-normal px-2 py-0.5 rounded-full bg-blue-500/20 text-blue-300">
                    {{ filteredSteps().length }} steps
                  </span>
                </h2>
                <p class="text-xs text-slate-400">Review, approve, or execute anti-bot paced social actions</p>
              </div>

              <!-- Filter Selector -->
              <div class="flex items-center gap-2">
                <select
                  [ngModel]="statusFilter()"
                  (ngModelChange)="onFilterStatus($event)"
                  class="bg-slate-950 text-xs text-slate-200 border border-slate-800 rounded-md px-2.5 py-1.5 focus:outline-none focus:border-blue-500">
                  <option value="All">All Statuses</option>
                  <option value="PendingApproval">Pending Approval</option>
                  <option value="Scheduled">Scheduled</option>
                  <option value="Sent">Sent</option>
                  <option value="Replied">Replied</option>
                </select>
              </div>
            </div>

            <!-- Steps List -->
            <div class="divide-y divide-slate-800/60 mt-2 max-h-[680px] overflow-y-auto pr-1">
              @if (filteredSteps().length === 0) {
                <div class="py-12 text-center text-slate-500 text-xs">
                  No cadence steps match the selected filter.
                </div>
              }

              @for (step of filteredSteps(); track step.stepId) {
                @let touchMeta = getTouchMeta(step.touchType);
                @let statusMeta = getStatusMeta(step.status);

                <div class="py-4 space-y-2.5">
                  <!-- Prospect Header -->
                  <div class="flex items-start justify-between gap-2">
                    <div>
                      <div class="flex items-center gap-2 flex-wrap">
                        <strong class="text-sm font-semibold text-white">
                          {{ step.prospect ? step.prospect.fullName : 'Prospect' }}
                        </strong>
                        <span class="text-xs text-slate-400">
                          &bull; {{ step.prospect ? step.prospect.jobTitle : '' }}
                        </span>
                      </div>
                      <span class="text-xs text-slate-400 block">
                        {{ step.prospect ? step.prospect.companyName : '' }} &bull; {{ step.prospect ? step.prospect.location : '' }}
                      </span>
                    </div>

                    <div class="flex flex-col items-end gap-1">
                      <span class="text-xs px-2 py-0.5 rounded border" [ngClass]="touchMeta.badgeClass">
                        {{ touchMeta.icon }} {{ touchMeta.label }}
                      </span>
                      <span class="text-[11px] px-2 py-0.5 rounded border" [ngClass]="statusMeta.badgeClass">
                        {{ statusMeta.label }}
                      </span>
                    </div>
                  </div>

                  <!-- Step Content Preview -->
                  <div class="p-3 bg-slate-950 rounded-lg border border-slate-800/80 text-xs text-slate-300">
                    <p class="italic">
                      "{{ step.customizedContent || step.messageTemplate }}"
                    </p>
                    @if (step.responseReceived) {
                      <div class="mt-2 pt-2 border-t border-slate-800 text-emerald-400 text-xs">
                        <strong>Reply received:</strong> {{ step.responseReceived }}
                      </div>
                    }
                  </div>

                  <!-- Step Metadata & Action Controls -->
                  <div class="flex items-center justify-between text-[11px] text-slate-400 flex-wrap gap-2 pt-1">
                    <span class="font-mono">
                      Day {{ step.dayOffset }} &bull; AI Score: {{ (step.aiPersonalizationScore * 100).toFixed(0) }}% &bull; Pacing delay: {{ step.pacingDelaySeconds }}s
                    </span>

                    <div class="flex items-center gap-1.5">
                      @if (step.status === 'PendingApproval' || step.status === 'Scheduled') {
                        <button
                          type="button"
                          (click)="onExecuteStep(step.stepId)"
                          [disabled]="pacingSafety().risk === 'Locked'"
                          class="px-2.5 py-1 text-xs font-semibold rounded bg-blue-600 hover:bg-blue-500 disabled:opacity-40 disabled:hover:bg-blue-600 text-white transition flex items-center gap-1">
                          <span>Approve & Paced Send</span>
                        </button>
                      }

                      @if (step.status === 'Sent') {
                        <button
                          type="button"
                          (click)="onMarkAsReplied(step.stepId)"
                          class="px-2.5 py-1 text-xs font-semibold rounded bg-emerald-600/30 hover:bg-emerald-600/50 text-emerald-300 border border-emerald-500/40 transition">
                          Mark as Replied
                        </button>
                      }

                      <button
                        type="button"
                        (click)="loadProspectToAiSandbox(step)"
                        class="px-2 py-1 text-xs text-slate-300 hover:text-white bg-slate-800 hover:bg-slate-700 rounded transition"
                        title="Load prospect into AI drafting sandbox">
                        ✨ Draft with AI
                      </button>
                    </div>
                  </div>
                </div>
              }
            </div>
          </div>
        </div>

        <!-- Right: AI Outreach Sandbox (3 cols) -->
        <div class="lg:col-span-3 space-y-6">
          <div class="bg-slate-900 border border-slate-800 rounded-xl p-5">
            <h2 class="text-sm font-semibold uppercase tracking-wider text-slate-400 mb-3 flex items-center justify-between">
              <span>Bilingual AI Sandbox</span>
              <span class="text-xs px-2 py-0.5 rounded bg-purple-500/20 text-purple-300 font-normal">
                GCC Tailored
              </span>
            </h2>

            <div class="space-y-3.5 text-xs">
              <!-- Touch Type Selection -->
              <div>
                <label class="block text-slate-400 mb-1">LinkedIn Touch Type</label>
                <select
                  [ngModel]="draftTouchType()"
                  (ngModelChange)="draftTouchType.set($event)"
                  class="w-full bg-slate-950 text-slate-200 border border-slate-800 rounded px-2.5 py-1.5 focus:border-blue-500 focus:outline-none">
                  <option value="ConnectionRequest">Connection Request (&lt; 300 chars)</option>
                  <option value="PersonalizedFollowUp">Personalized Follow-Up</option>
                  <option value="InMailMessage">Executive InMail</option>
                  <option value="PostEngagement">Post Engagement Comment</option>
                  <option value="ProfileView">Profile Dwell Note</option>
                </select>
              </div>

              <!-- Language & Seniority -->
              <div class="grid grid-cols-2 gap-2">
                <div>
                  <label class="block text-slate-400 mb-1">Language</label>
                  <select
                    [ngModel]="draftLanguage()"
                    (ngModelChange)="draftLanguage.set($event)"
                    class="w-full bg-slate-950 text-slate-200 border border-slate-800 rounded px-2.5 py-1.5 focus:border-blue-500 focus:outline-none">
                    <option value="en">English (B2B Enterprise)</option>
                    <option value="ar">العربية (الخليج والسعودية)</option>
                  </select>
                </div>
                <div>
                  <label class="block text-slate-400 mb-1">Seniority</label>
                  <select
                    [ngModel]="draftSeniority()"
                    (ngModelChange)="draftSeniority.set($event)"
                    class="w-full bg-slate-950 text-slate-200 border border-slate-800 rounded px-2.5 py-1.5 focus:border-blue-500 focus:outline-none">
                    <option value="CXO">CXO / C-Level</option>
                    <option value="VP">VP / Executive</option>
                    <option value="Director">Director</option>
                    <option value="Manager">Manager</option>
                  </select>
                </div>
              </div>

              <!-- Prospect & Company -->
              <div>
                <label class="block text-slate-400 mb-1">Prospect Name</label>
                <input
                  type="text"
                  [ngModel]="draftProspectName()"
                  (ngModelChange)="draftProspectName.set($event)"
                  class="w-full bg-slate-950 text-slate-200 border border-slate-800 rounded px-2.5 py-1.5 focus:border-blue-500 focus:outline-none"
                  placeholder="e.g. Tariq Al-Mansoor" />
              </div>

              <div>
                <label class="block text-slate-400 mb-1">Company & Industry</label>
                <div class="grid grid-cols-2 gap-2">
                  <input
                    type="text"
                    [ngModel]="draftCompanyName()"
                    (ngModelChange)="draftCompanyName.set($event)"
                    class="bg-slate-950 text-slate-200 border border-slate-800 rounded px-2.5 py-1.5 focus:border-blue-500 focus:outline-none"
                    placeholder="Company" />
                  <input
                    type="text"
                    [ngModel]="draftIndustry()"
                    (ngModelChange)="draftIndustry.set($event)"
                    class="bg-slate-950 text-slate-200 border border-slate-800 rounded px-2.5 py-1.5 focus:border-blue-500 focus:outline-none"
                    placeholder="Industry" />
                </div>
              </div>

              <!-- Pain point -->
              <div>
                <label class="block text-slate-400 mb-1">Key Value / Pain Point</label>
                <input
                  type="text"
                  [ngModel]="draftKeyPainPoint()"
                  (ngModelChange)="draftKeyPainPoint.set($event)"
                  class="w-full bg-slate-950 text-slate-200 border border-slate-800 rounded px-2.5 py-1.5 focus:border-blue-500 focus:outline-none"
                  placeholder="e.g. Saudi PDPL cloud data sovereignty" />
              </div>

              <!-- Generate Button -->
              <button
                type="button"
                (click)="generateAiDraft()"
                class="w-full py-2 px-3 rounded-lg font-semibold bg-gradient-to-r from-blue-600 to-purple-600 hover:from-blue-500 hover:to-purple-500 text-white transition flex items-center justify-center gap-1.5 shadow-md">
                <span>✨ Generate Personalized Pitch</span>
              </button>

              <!-- Output Box -->
              @if (generatedAiDraft(); as draft) {
                <div class="mt-4 pt-3 border-t border-slate-800 space-y-2">
                  <div class="flex items-center justify-between text-[11px] text-slate-400">
                    <span class="text-emerald-400 font-semibold">Confidence: {{ (draft.confidenceScore * 100).toFixed(0) }}%</span>
                    <span>Pacing wait: {{ draft.pacingWaitMinutes }}m</span>
                  </div>

                  @if (draft.draftedSubject) {
                    <div class="text-[11px] font-semibold text-slate-300">
                      Subject: {{ draft.draftedSubject }}
                    </div>
                  }

                  <div class="p-3 bg-slate-950 rounded-lg border border-slate-800 text-slate-200 whitespace-pre-wrap leading-relaxed">
                    {{ draft.draftedMessage }}
                  </div>

                  <div class="flex items-center justify-between text-[10px] text-slate-400">
                    <span>Hook: {{ draft.personalizationHook }}</span>
                    <span class="font-mono">{{ draft.draftedMessage.length }} chars</span>
                  </div>

                  <div class="flex flex-wrap gap-1 mt-1">
                    @for (tag of draft.recommendedHashtags; track tag) {
                      <span class="text-[10px] px-1.5 py-0.5 rounded bg-slate-800 text-blue-300">
                        {{ tag }}
                      </span>
                    }
                  </div>
                </div>
              }
            </div>
          </div>
        </div>
      </div>
    </div>
  `
})
export class LinkedInOutreachStudioComponent {
  // Signals
  cadences = signal<LinkedInCampaignCadence[]>([
    {
      cadenceId: '22222222-2222-2222-2222-222222222222',
      tenantId: '11111111-1111-1111-1111-111111111111',
      name: 'GCC Enterprise SaaS Decision-Makers (KSA & UAE)',
      targetPersona: 'Enterprise CTOs, CISOs & Heads of Digital Transformation',
      description: 'Multi-touch outbound social selling cadence designed for high-ACV enterprise accounts in Riyadh, Dubai, and Abu Dhabi with GCC compliance hooks and bilingual AI personalization.',
      isActive: true,
      dailyActionLimit: 40,
      createdAtUtc: new Date(Date.now() - 14 * 86400000).toISOString(),
      steps: [],
      activeProspectCount: 5,
      connectionRatePercent: 42.5,
      replyRatePercent: 28.0
    },
    {
      cadenceId: '22222222-2222-2222-2222-222222222223',
      tenantId: '11111111-1111-1111-1111-111111111111',
      name: 'DIFC & ADGM FinTech Founders Acceleration',
      targetPersona: 'FinTech Founders & Chief Product Officers',
      description: 'Focused outbound sequence targeting high-growth payments and RegTech leaders in UAE financial free zones.',
      isActive: true,
      dailyActionLimit: 30,
      createdAtUtc: new Date(Date.now() - 7 * 86400000).toISOString(),
      steps: [],
      activeProspectCount: 3,
      connectionRatePercent: 55.0,
      replyRatePercent: 33.3
    }
  ]);

  selectedCadenceId = signal<string>('22222222-2222-2222-2222-222222222222');
  statusFilter = signal<string>('All');

  // Anti-bot pacing engine signals
  dailyQuotaUsed = signal<number>(14);
  dailyQuotaLimit = signal<number>(40);
  pacingIntervalSeconds = signal<number>(85);
  jitterWindowSeconds = signal<number>(35);

  // Steps signal
  allSteps = signal<LinkedInCadenceStepRecord[]>([
    {
      stepId: '44444444-4444-4444-4444-444444444401',
      cadenceId: '22222222-2222-2222-2222-222222222222',
      prospectId: '33333333-3333-3333-3333-333333333331',
      prospect: {
        prospectId: '33333333-3333-3333-3333-333333333331',
        fullName: 'Tariq Al-Mansoor',
        jobTitle: 'VP of Digital Transformation',
        companyName: 'Saudi Telecom Cloud Services',
        seniority: 'VP',
        linkedInProfileUrl: 'https://linkedin.com/in/tariq-almansoor',
        location: 'Riyadh, Saudi Arabia',
        industry: 'Telecommunications & Cloud',
        connected: true
      },
      dayOffset: 1,
      touchType: 'ProfileView',
      status: 'Sent',
      messageTemplate: 'Warm profile view and interaction with recent Vision 2030 cloud announcement.',
      customizedContent: 'Dwell time 72s via Riyadh residential IP node.',
      pacingDelaySeconds: 45,
      aiPersonalizationScore: 0.94
    },
    {
      stepId: '44444444-4444-4444-4444-444444444402',
      cadenceId: '22222222-2222-2222-2222-222222222222',
      prospectId: '33333333-3333-3333-3333-333333333331',
      prospect: {
        prospectId: '33333333-3333-3333-3333-333333333331',
        fullName: 'Tariq Al-Mansoor',
        jobTitle: 'VP of Digital Transformation',
        companyName: 'Saudi Telecom Cloud Services',
        seniority: 'VP',
        linkedInProfileUrl: 'https://linkedin.com/in/tariq-almansoor',
        location: 'Riyadh, Saudi Arabia',
        industry: 'Telecommunications & Cloud',
        connected: true
      },
      dayOffset: 2,
      touchType: 'PostEngagement',
      status: 'Sent',
      messageTemplate: 'Like & comment on Saudi data residency compliance framework post.',
      customizedContent: 'Insightful contribution on KSA PDPL requirements and latency optimizations.',
      pacingDelaySeconds: 75,
      aiPersonalizationScore: 0.96
    },
    {
      stepId: '44444444-4444-4444-4444-444444444403',
      cadenceId: '22222222-2222-2222-2222-222222222222',
      prospectId: '33333333-3333-3333-3333-333333333331',
      prospect: {
        prospectId: '33333333-3333-3333-3333-333333333331',
        fullName: 'Tariq Al-Mansoor',
        jobTitle: 'VP of Digital Transformation',
        companyName: 'Saudi Telecom Cloud Services',
        seniority: 'VP',
        linkedInProfileUrl: 'https://linkedin.com/in/tariq-almansoor',
        location: 'Riyadh, Saudi Arabia',
        industry: 'Telecommunications & Cloud',
        connected: true
      },
      dayOffset: 4,
      touchType: 'ConnectionRequest',
      status: 'Replied',
      messageTemplate: 'Tariq, following STC Cloud regional expansion with high admiration.',
      customizedContent: 'Tariq, following STC Cloud sovereign data initiatives with high admiration. Would love to share our GCC enterprise benchmarks with your architecture team.',
      pacingDelaySeconds: 120,
      aiPersonalizationScore: 0.98,
      responseReceived: 'Thanks for reaching out! Let us review the benchmark next week.'
    },
    {
      stepId: '44444444-4444-4444-4444-444444444404',
      cadenceId: '22222222-2222-2222-2222-222222222222',
      prospectId: '33333333-3333-3333-3333-333333333332',
      prospect: {
        prospectId: '33333333-3333-3333-3333-333333333332',
        fullName: 'Noura Al-Zahrani',
        jobTitle: 'Head of Enterprise Architecture',
        companyName: 'SABIC Digital',
        seniority: 'Director',
        linkedInProfileUrl: 'https://linkedin.com/in/noura-alzahrani',
        location: 'Al-Khobar, Saudi Arabia',
        industry: 'Industrial Chemicals',
        connected: false
      },
      dayOffset: 4,
      touchType: 'ConnectionRequest',
      status: 'PendingApproval',
      messageTemplate: 'Noura, your work on SABIC event-driven architecture is inspiring.',
      customizedContent: 'Noura, your work scaling SABIC distributed event brokers is remarkable. Would value connecting and sharing our recent benchmark on industrial data mesh resilience.',
      pacingDelaySeconds: 110,
      aiPersonalizationScore: 0.95
    },
    {
      stepId: '44444444-4444-4444-4444-444444444405',
      cadenceId: '22222222-2222-2222-2222-222222222222',
      prospectId: '33333333-3333-3333-3333-333333333333',
      prospect: {
        prospectId: '33333333-3333-3333-3333-333333333333',
        fullName: 'Faisal Bin Rashid',
        jobTitle: 'Chief Technology Officer',
        companyName: 'Emirates FinTech Holdings',
        seniority: 'CXO',
        linkedInProfileUrl: 'https://linkedin.com/in/faisal-binrashid',
        location: 'Dubai, UAE',
        industry: 'FinTech & Banking',
        connected: false
      },
      dayOffset: 3,
      touchType: 'ConnectionRequest',
      status: 'PendingApproval',
      messageTemplate: 'Faisal, impressed by Emirates FinTech cross-border settlement latency.',
      customizedContent: 'Faisal, thrilled to see Emirates FinTech milestones in DIFC. Connecting to exchange notes on distributed ledger throughput.',
      pacingDelaySeconds: 130,
      aiPersonalizationScore: 0.97
    },
    {
      stepId: '44444444-4444-4444-4444-444444444406',
      cadenceId: '22222222-2222-2222-2222-222222222222',
      prospectId: '33333333-3333-3333-3333-333333333334',
      prospect: {
        prospectId: '33333333-3333-3333-3333-333333333334',
        fullName: 'Sara Al-Otaibi',
        jobTitle: 'Director of Cloud Infrastructure',
        companyName: 'Aramco Digital Ventures',
        seniority: 'Director',
        linkedInProfileUrl: 'https://linkedin.com/in/sara-alotaibi',
        location: 'Dhahran, Saudi Arabia',
        industry: 'Energy Automation',
        connected: false
      },
      dayOffset: 2,
      touchType: 'PostEngagement',
      status: 'PendingApproval',
      messageTemplate: 'High-value engagement drafted for Aramco industrial AI post.',
      customizedContent: 'Remarkable initiative by Aramco Digital on cyber-physical plant isolation. Truly sovereign.',
      pacingDelaySeconds: 85,
      aiPersonalizationScore: 0.94
    },
    {
      stepId: '44444444-4444-4444-4444-444444444407',
      cadenceId: '22222222-2222-2222-2222-222222222222',
      prospectId: '33333333-3333-3333-3333-333333333335',
      prospect: {
        prospectId: '33333333-3333-3333-3333-333333333335',
        fullName: 'Omar Al-Ghamdi',
        jobTitle: 'IT Operations Manager',
        companyName: 'Al Rajhi Banking Group',
        seniority: 'Manager',
        linkedInProfileUrl: 'https://linkedin.com/in/omar-alghamdi',
        location: 'Riyadh, Saudi Arabia',
        industry: 'Banking',
        connected: false
      },
      dayOffset: 1,
      touchType: 'ProfileView',
      status: 'Scheduled',
      messageTemplate: 'Scheduled warm profile view with randomized anti-bot jitter.',
      pacingDelaySeconds: 55,
      aiPersonalizationScore: 0.89
    }
  ]);

  // AI Sandbox Form Signals
  draftTouchType = signal<LinkedInTouchType>('ConnectionRequest');
  draftLanguage = signal<'en' | 'ar'>('en');
  draftSeniority = signal<ProspectSeniority>('VP');
  draftProspectName = signal<string>('Tariq Al-Mansoor');
  draftCompanyName = signal<string>('Saudi Telecom Cloud Services');
  draftJobTitle = signal<string>('VP of Digital Transformation');
  draftIndustry = signal<string>('Telecommunications & Cloud');
  draftKeyPainPoint = signal<string>('Saudi PDPL compliance & sovereign cloud latency');
  generatedAiDraft = signal<DraftMessageAiResponse | null>(null);

  // Computeds
  isRtl = computed(() => (this.translationService ? this.translationService.currentLocale() === 'ar-AR' : false));

  selectedCadence = computed(() => {
    return this.cadences().find(c => c.cadenceId === this.selectedCadenceId()) || this.cadences()[0];
  });

  filteredSteps = computed(() => {
    const list = this.allSteps().filter(s => s.cadenceId === this.selectedCadenceId());
    const filter = this.statusFilter();
    if (filter === 'All') return list;
    return list.filter(s => s.status === filter);
  });

  activeProspectCount = computed(() => {
    const prospects = this.allSteps()
      .filter(s => s.cadenceId === this.selectedCadenceId())
      .map(s => s.prospectId);
    return new Set(prospects).size;
  });

  totalExecutedTouches = computed(() => {
    return this.allSteps().filter(s => s.status === 'Sent' || s.status === 'Replied').length;
  });

  pendingApprovalCount = computed(() => {
    return this.allSteps().filter(s => s.status === 'PendingApproval').length;
  });

  avgConnectionRate = computed(() => {
    const connSteps = this.allSteps().filter(s => s.touchType === 'ConnectionRequest');
    if (connSteps.length === 0) return 0;
    const connected = connSteps.filter(s => s.status === 'Sent' || s.status === 'Replied').length;
    return Math.round((connected / connSteps.length) * 100);
  });

  pacingSafety = computed(() => {
    return calculatePacingSafety(this.dailyQuotaUsed(), this.dailyQuotaLimit());
  });

  sequenceTimeline = computed(() => [
    { day: 1, title: 'Profile Dwell View', icon: '👀', action: 'Warm visit from local IP with 60-90s human dwell time', delay: 45, badgeClass: 'bg-cyan-500/20 text-cyan-300', active: true },
    { day: 2, title: 'Thought Leadership Engagement', icon: '💬', action: 'Contextual like and insight comment on prospect publication', delay: 75, badgeClass: 'bg-purple-500/20 text-purple-300', active: true },
    { day: 4, title: 'Bilingual Connection Invite', icon: '🤝', action: 'High-relevance invite (< 300 chars) with GCC ecosystem hook', delay: 120, badgeClass: 'bg-emerald-500/20 text-emerald-300', active: true },
    { day: 7, title: 'Enterprise Benchmark Follow-Up', icon: '📬', action: '1st-degree follow-up sharing regional case study report', delay: 150, badgeClass: 'bg-amber-500/20 text-amber-300', active: false },
    { day: 12, title: 'Executive InMail Briefing', icon: '⭐', action: 'High-impact C-suite briefing invite with calendar link', delay: 180, badgeClass: 'bg-blue-500/20 text-blue-300', active: false }
  ]);

  constructor(@Optional() private translationService?: TranslationService) {
    // Generate initial draft
    this.generateAiDraft();
  }

  onSelectCadence(cadenceId: string): void {
    this.selectedCadenceId.set(cadenceId);
    const cad = this.cadences().find(c => c.cadenceId === cadenceId);
    if (cad) {
      this.dailyQuotaLimit.set(cad.dailyActionLimit);
    }
  }

  onFilterStatus(status: string): void {
    this.statusFilter.set(status);
  }

  onExecuteStep(stepId: string): void {
    if (this.dailyQuotaUsed() >= this.dailyQuotaLimit()) {
      alert('Anti-bot limit reached! Step queued for next window.');
      return;
    }

    this.allSteps.update(steps =>
      steps.map(s => {
        if (s.stepId === stepId) {
          return {
            ...s,
            status: 'Sent' as CadenceStepStatus,
            executionTimestampUtc: new Date().toISOString()
          };
        }
        return s;
      })
    );

    this.dailyQuotaUsed.update(n => n + 1);
  }

  onMarkAsReplied(stepId: string): void {
    this.allSteps.update(steps =>
      steps.map(s => {
        if (s.stepId === stepId) {
          return {
            ...s,
            status: 'Replied' as CadenceStepStatus,
            responseReceived: 'Prospect accepted connection and replied to social selling pitch.'
          };
        }
        return s;
      })
    );
  }

  loadProspectToAiSandbox(step: LinkedInCadenceStepRecord): void {
    if (step.prospect) {
      this.draftProspectName.set(step.prospect.fullName);
      this.draftCompanyName.set(step.prospect.companyName);
      this.draftJobTitle.set(step.prospect.jobTitle);
      this.draftSeniority.set(step.prospect.seniority);
      this.draftIndustry.set(step.prospect.industry);
    }
    this.draftTouchType.set(step.touchType);
    this.generateAiDraft();
  }

  generateAiDraft(): void {
    const draft = generateLocalAiDraft({
      touchType: this.draftTouchType(),
      prospectName: this.draftProspectName(),
      companyName: this.draftCompanyName(),
      jobTitle: this.draftJobTitle(),
      seniority: this.draftSeniority(),
      industry: this.draftIndustry(),
      keyPainPoint: this.draftKeyPainPoint(),
      language: this.draftLanguage()
    });
    this.generatedAiDraft.set(draft);
  }

  createNewCadencePrompt(): void {
    const name = `GCC Scale-Up Growth Cadence #${this.cadences().length + 1}`;
    const newCad: LinkedInCampaignCadence = {
      cadenceId: `cad-${Date.now()}`,
      tenantId: '11111111-1111-1111-1111-111111111111',
      name,
      targetPersona: 'Scale-Up Tech Founders & VPs',
      description: 'Rapid omnichannel outreach sequence for Series A-C tech ventures across Riyadh and Dubai.',
      isActive: true,
      dailyActionLimit: 35,
      createdAtUtc: new Date().toISOString(),
      steps: [],
      activeProspectCount: 0,
      connectionRatePercent: 0,
      replyRatePercent: 0
    };
    this.cadences.update(list => [...list, newCad]);
    this.onSelectCadence(newCad.cadenceId);
  }

  resetDailyQuota(): void {
    this.dailyQuotaUsed.set(0);
  }

  getTouchMeta(touch: LinkedInTouchType) {
    return getTouchTypeMeta(touch);
  }

  getStatusMeta(status: CadenceStepStatus) {
    return getStepStatusMeta(status);
  }
}
