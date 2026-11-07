import { Component, signal, computed, inject, Optional, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslationService } from '../../core/services/translation.service';
import { LanguageSwitcherComponent } from '../../core/components/language-switcher.component';

export type VideoAspectRatio = 'Vertical_9_16' | 'Square_1_1' | 'Landscape_16_9';

export type ArabicRegionalDialect =
  | 'SaudiNajdi'
  | 'SaudiHejazi'
  | 'GulfKhaleeji'
  | 'Egyptian'
  | 'ModernStandardArabic';

export type AdCreativeStage =
  | 'HookIntro'
  | 'ProblemStatement'
  | 'ProductShowcase'
  | 'SocialProofTestimonial'
  | 'CallToAction';

export interface VideoStoryboardScene {
  sceneNumber: number;
  stage: AdCreativeStage;
  visualPrompt: string;
  voiceoverScript: string;
  durationSeconds: number;
  backgroundTrack: string;
  textOverlayText: string;
}

export interface VideoAdCampaignProject {
  projectId: string;
  tenantId: string;
  projectTitle: string;
  productSku: string;
  targetAudience: string;
  dialect: ArabicRegionalDialect;
  aspectRatio: VideoAspectRatio;
  scenes: VideoStoryboardScene[];
  totalDurationSeconds: number;
  estimatedRenderCostSar: number;
  createdAt: string;
}

export interface CreativeVariantRecord {
  variantId: string;
  projectId: string;
  tenantId: string;
  variantCode: string;
  hookText: string;
  ctaText: string;
  banditExplorationScore: number;
  predictedCtrPercent: number;
  generatedAt: string;
}

export interface SynthesizedScriptResult {
  targetDialect: ArabicRegionalDialect;
  standardScript: string;
  localizedScript: string;
  dialectIdiomsUsed: string[];
}

export function getDialectDisplayLabel(dialect: ArabicRegionalDialect): string {
  switch (dialect) {
    case 'SaudiNajdi':
      return 'Saudi Najdi (الرياض والوسطى)';
    case 'SaudiHejazi':
      return 'Saudi Hejazi (جدة ومكة الغربية)';
    case 'GulfKhaleeji':
      return 'Gulf Khaleeji (الخليج العربي)';
    case 'Egyptian':
      return 'Egyptian Masri (المصرية)';
    case 'ModernStandardArabic':
      return 'Modern Standard Arabic (الفصحى)';
  }
}

export function getAspectRatioLabel(ratio: VideoAspectRatio): string {
  switch (ratio) {
    case 'Vertical_9_16':
      return '9:16 Vertical (TikTok / Reels)';
    case 'Square_1_1':
      return '1:1 Square (Instagram / Snap Feed)';
    case 'Landscape_16_9':
      return '16:9 Landscape (YouTube / OTT)';
  }
}

export function getStageMeta(stage: AdCreativeStage): { label: string; icon: string; badgeClass: string } {
  switch (stage) {
    case 'HookIntro':
      return { label: 'Hook Intro (0-4s)', icon: '⚡', badgeClass: 'bg-amber-500/20 text-amber-300 border-amber-500/30' };
    case 'ProblemStatement':
      return { label: 'Problem Agitation', icon: '⚠️', badgeClass: 'bg-rose-500/20 text-rose-300 border-rose-500/30' };
    case 'ProductShowcase':
      return { label: 'Product Showcase', icon: '✨', badgeClass: 'bg-indigo-500/20 text-indigo-300 border-indigo-500/30' };
    case 'SocialProofTestimonial':
      return { label: 'Social Proof', icon: '⭐', badgeClass: 'bg-emerald-500/20 text-emerald-300 border-emerald-500/30' };
    case 'CallToAction':
      return { label: 'Call To Action', icon: '🚀', badgeClass: 'bg-cyan-500/20 text-cyan-300 border-cyan-500/30' };
  }
}

export function synthesizeRegionalDialect(
  standardText: string,
  targetDialect: ArabicRegionalDialect
): SynthesizedScriptResult {
  const text = standardText?.trim() || 'ما رأيك في تجربة منتجنا المميز اليوم؟ نحن هنا لخدمتكم، لا تفوتوا العرض الحصري.';
  const idiomsUsed: string[] = [];
  let localized = text;

  const replacePhrases = (searchTerms: string[], replacement: string) => {
    for (const term of searchTerms) {
      if (localized.toLowerCase().includes(term.toLowerCase())) {
        localized = localized.replace(new RegExp(term, 'gi'), replacement);
        if (!idiomsUsed.includes(replacement)) idiomsUsed.push(replacement);
      }
    }
  };

  switch (targetDialect) {
    case 'SaudiNajdi':
      replacePhrases(['ما رأيك', 'هل تعلم', 'ما رأيكم'], 'وش رايك');
      replacePhrases(['يسرنا', 'بكل سرور', 'نحن هنا لخدمتكم', 'يسعدنا'], 'أبشر بعزك');
      replacePhrases(['مرحباً', 'أهلاً وسهلاً', 'أهلاً'], 'يا هلا ومسهلا');
      replacePhrases(['لا تفوت', 'لا تفوتوا', 'سارعوا', 'سارع'], 'لا يفوتك');
      replacePhrases(['كثيراً', 'جداً'], 'بالحيل');
      replacePhrases(['الآن', 'فوراً'], 'الحين');

      if (!idiomsUsed.includes('يا هلا ومسهلا')) {
        localized = 'يا هلا ومسهلا! ' + localized;
        idiomsUsed.push('يا هلا ومسهلا');
      }
      if (!idiomsUsed.includes('وش رايك')) {
        localized = localized + ' وش رايك نجربها سوا؟';
        idiomsUsed.push('وش رايك');
      }
      if (!idiomsUsed.includes('أبشر بعزك')) {
        localized = localized + ' أبشر بعزك بأفضل جودة.';
        idiomsUsed.push('أبشر بعزك');
      }
      if (!idiomsUsed.includes('لا يفوتك')) {
        localized = localized + ' لا يفوتك العرض الحين!';
        idiomsUsed.push('لا يفوتك');
      }
      break;

    case 'SaudiHejazi':
      replacePhrases(['ما رأيك', 'ما الأمر', 'ما القصة', 'ما المشكلة'], 'إيش الهرجة');
      replacePhrases(['سيدي', 'عزيزي', 'يا صديقي'], 'يا سيدي');
      replacePhrases(['حسب رغبتك', 'كما تحب', 'كما تريد'], 'على كيفك');
      replacePhrases(['الآن', 'فوراً', 'حالياً'], 'دحين');
      replacePhrases(['لا داعي', 'بدون شك'], 'ما يحتاج');

      if (!idiomsUsed.includes('إيش الهرجة')) {
        localized = 'إيش الهرجة يا سيدي؟ ' + localized;
        idiomsUsed.push('إيش الهرجة', 'يا سيدي');
      }
      if (!idiomsUsed.includes('على كيفك')) {
        localized = localized + ' وكل شيء على كيفك دحين!';
        idiomsUsed.push('على كيفك', 'دحين');
      }
      break;

    case 'GulfKhaleeji':
      replacePhrases(['ما رأيك', 'ماذا تعتقد'], 'شنو رايك');
      replacePhrases(['أبشر', 'بكل سرور', 'يسرنا'], 'فالك طيب');
      replacePhrases(['كثيراً', 'جداً'], 'وايد');
      replacePhrases(['الآن', 'فوراً'], 'الحين');
      replacePhrases(['شكراً'], 'ما تقصر');

      if (!idiomsUsed.includes('شنو رايك')) {
        localized = 'شنو رايك؟ ' + localized;
        idiomsUsed.push('شنو رايك');
      }
      if (!idiomsUsed.includes('فالك طيب')) {
        localized = localized + ' وفالك طيب ويانا وايد!';
        idiomsUsed.push('فالك طيب', 'وايد');
      }
      break;

    case 'Egyptian':
      replacePhrases(['ما رأيك'], 'إيه رأيك');
      replacePhrases(['سيدي', 'صديقي'], 'يا باشا');
      replacePhrases(['الآن', 'فوراً'], 'دلوقتي');
      replacePhrases(['كثيراً', 'جداً'], 'أوي');
      replacePhrases(['بالتوفيق', 'مبارك'], 'على البركة');
      replacePhrases(['لا يفوتك', 'لا تفوتوا'], 'ما يتفوتش');

      if (!idiomsUsed.includes('إيه رأيك')) {
        localized = 'يا باشا إيه رأيك؟ ' + localized;
        idiomsUsed.push('يا باشا', 'إيه رأيك');
      }
      if (!idiomsUsed.includes('دلوقتي')) {
        localized = localized + ' العرض ما يتفوتش دلوقتي خالص!';
        idiomsUsed.push('ما يتفوتش', 'دلوقتي');
      }
      break;

    case 'ModernStandardArabic':
    default:
      idiomsUsed.push('ما رأيكم', 'أهلاً وسهلاً', 'الآن وحصرياً');
      if (!localized.includes('أهلاً وسهلاً')) {
        localized = 'أهلاً وسهلاً بكم! ' + localized;
      }
      break;
  }

  return {
    targetDialect,
    standardScript: text,
    localizedScript: localized,
    dialectIdiomsUsed: idiomsUsed
  };
}

@Component({
  selector: 'app-video-creative-pipeline-studio',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, RouterLink, LanguageSwitcherComponent],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans" dir="ltr">
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Top Navigation Header -->
        <header class="h-14 border-b border-slate-800 bg-slate-900/80 backdrop-blur-md px-6 flex items-center justify-between shrink-0 sticky top-0 z-20">
          <div class="flex items-center gap-3">
            <a routerLink="/marketing" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← Marketing
            </a>
            <span class="text-slate-700">|</span>
            <span class="text-base font-bold text-white flex items-center gap-2">
              🎬 Generative AI Video Ad & Storyboard Engine
            </span>
            <span class="bg-indigo-500/20 text-indigo-300 text-[10px] font-mono px-2 py-0.5 rounded-full border border-indigo-500/30">
              PRD Domain C5 & J1/J3 • Regional Dialect Lip-Sync • Bandit Optimization
            </span>
          </div>

          <div class="flex items-center gap-3">
            <app-language-switcher></app-language-switcher>
            <div class="text-xs text-slate-400 bg-slate-800/80 px-3 py-1.5 rounded-lg border border-slate-700 flex items-center gap-2">
              <span class="w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
              <span>Active Tenant: <strong class="text-white">Default Enterprise</strong></span>
            </div>
          </div>
        </header>

        <!-- Main Body Content -->
        <div class="p-6 space-y-6 max-w-7xl mx-auto w-full">
          <!-- Notification Status Banner -->
          @if (statusMessage()) {
            <div class="p-3 bg-emerald-950/80 border border-emerald-800 text-emerald-200 text-xs rounded-xl flex items-center justify-between">
              <span class="flex items-center gap-2"><span>✨</span> {{ statusMessage() }}</span>
              <button (click)="statusMessage.set(null)" class="text-emerald-400 hover:text-white">✕</button>
            </div>
          }

          <!-- Top 4 KPI Metrics -->
          <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <div class="p-4 rounded-xl bg-slate-900 border border-slate-800 flex items-center justify-between" data-testid="kpi-active-projects">
              <div>
                <p class="text-[11px] font-medium uppercase tracking-wider text-slate-400">Active Video Projects</p>
                <h3 class="text-2xl font-black text-white mt-1">{{ activeProjectsCount() }}</h3>
                <span class="text-[10px] text-indigo-400">All storyboard campaigns</span>
              </div>
              <div class="w-10 h-10 rounded-lg bg-indigo-500/10 border border-indigo-500/20 flex items-center justify-center text-indigo-400 text-lg">
                📽️
              </div>
            </div>

            <div class="p-4 rounded-xl bg-slate-900 border border-slate-800 flex items-center justify-between" data-testid="kpi-rendered-variants">
              <div>
                <p class="text-[11px] font-medium uppercase tracking-wider text-slate-400">Rendered Ad Variants</p>
                <h3 class="text-2xl font-black text-emerald-400 mt-1">{{ renderedVariantsCount() }}</h3>
                <span class="text-[10px] text-emerald-500/80">Multi-armed bandit pool</span>
              </div>
              <div class="w-10 h-10 rounded-lg bg-emerald-500/10 border border-emerald-500/20 flex items-center justify-center text-emerald-400 text-lg">
                🎯
              </div>
            </div>

            <div class="p-4 rounded-xl bg-slate-900 border border-slate-800 flex items-center justify-between" data-testid="kpi-avg-predicted-ctr">
              <div>
                <p class="text-[11px] font-medium uppercase tracking-wider text-slate-400">Avg Predicted CTR</p>
                <h3 class="text-2xl font-black text-cyan-400 mt-1">{{ avgPredictedCtr() }}</h3>
                <span class="text-[10px] text-cyan-500/80">Bandit Thompson sampling</span>
              </div>
              <div class="w-10 h-10 rounded-lg bg-cyan-500/10 border border-cyan-500/20 flex items-center justify-center text-cyan-400 text-lg">
                📈
              </div>
            </div>

            <div class="p-4 rounded-xl bg-slate-900 border border-slate-800 flex items-center justify-between" data-testid="kpi-regional-coverage">
              <div>
                <p class="text-[11px] font-medium uppercase tracking-wider text-slate-400">Regional Dialect Coverage</p>
                <h3 class="text-2xl font-black text-amber-400 mt-1">{{ dialectCoverageCount() }} Regions</h3>
                <span class="text-[10px] text-amber-500/80">Saudi Najdi, Hejazi, Khaleeji, Egypt</span>
              </div>
              <div class="w-10 h-10 rounded-lg bg-amber-500/10 border border-amber-500/20 flex items-center justify-center text-amber-400 text-lg">
                🗣️
              </div>
            </div>
          </div>

          <!-- Project Selector & Storyboard Header Bar -->
          <div class="bg-slate-900 rounded-xl border border-slate-800 p-5 space-y-4">
            <div class="flex flex-col md:flex-row md:items-center justify-between gap-4">
              <div class="space-y-1">
                <div class="flex items-center gap-2">
                  <h2 class="text-base font-bold text-white flex items-center gap-2">
                    <span>🎞️</span> Visual Storyboard Canvas
                  </h2>
                  @let activeP = selectedProject();
                  <span class="px-2 py-0.5 rounded text-[10px] font-semibold bg-indigo-500/20 text-indigo-300 border border-indigo-500/30">
                    {{ getAspectRatioLabel(activeP.aspectRatio) }}
                  </span>
                  <span class="px-2 py-0.5 rounded text-[10px] font-semibold bg-emerald-500/20 text-emerald-300 border border-emerald-500/30">
                    {{ getDialectDisplayLabel(activeP.dialect) }}
                  </span>
                </div>
                <p class="text-xs text-slate-400">
                  Target: <span class="text-slate-200">{{ activeP.targetAudience }}</span> • SKU: <code class="text-indigo-300 font-mono">{{ activeP.productSku }}</code>
                </p>
              </div>

              <!-- Project Switcher & Actions -->
              <div class="flex items-center gap-3">
                <select
                  [ngModel]="selectedProjectId()"
                  (ngModelChange)="onSelectProject($event)"
                  data-testid="project-selector-dropdown"
                  class="bg-slate-950 border border-slate-700 text-xs rounded-lg px-3 py-2 text-white focus:outline-none focus:border-indigo-500">
                  @for (proj of projects(); track proj.projectId) {
                    <option [value]="proj.projectId">{{ proj.projectTitle }}</option>
                  }
                </select>

                <div class="flex items-center gap-2 text-xs bg-slate-950 px-3 py-2 rounded-lg border border-slate-800 text-slate-300">
                  <span>⏱️ <strong>{{ activeP.totalDurationSeconds }}s</strong></span>
                  <span class="text-slate-600">|</span>
                  <span class="text-amber-400 font-bold">SAR {{ activeP.estimatedRenderCostSar.toFixed(2) }}</span>
                </div>
              </div>
            </div>

            <!-- Storyboard Scenes Ribbon -->
            <div class="grid grid-cols-1 md:grid-cols-3 lg:grid-cols-5 gap-3 pt-2">
              @for (scene of activeP.scenes; track scene.sceneNumber) {
                @let meta = getStageMeta(scene.stage);
                <div class="bg-slate-950/70 border border-slate-800 hover:border-indigo-500/50 rounded-xl p-3.5 flex flex-col justify-between transition group space-y-3" [attr.data-testid]="'storyboard-scene-' + scene.sceneNumber">
                  <!-- Scene Header -->
                  <div class="flex items-center justify-between">
                    <span class="w-6 h-6 rounded-full bg-slate-800 text-[11px] font-bold text-slate-300 flex items-center justify-center">
                      #{{ scene.sceneNumber }}
                    </span>
                    <span class="text-[10px] font-mono px-2 py-0.5 rounded border flex items-center gap-1" [ngClass]="meta.badgeClass">
                      <span>{{ meta.icon }}</span> {{ meta.label }}
                    </span>
                    <span class="text-[10px] text-slate-400 font-mono bg-slate-900 px-1.5 py-0.5 rounded border border-slate-800">
                      {{ scene.durationSeconds }}s
                    </span>
                  </div>

                  <!-- Text Overlay Preview -->
                  <div class="p-2 rounded bg-indigo-950/30 border border-indigo-900/40 text-[11px] text-indigo-200 font-medium text-right" dir="rtl">
                    {{ scene.textOverlayText }}
                  </div>

                  <!-- Voiceover Script preview -->
                  <div class="space-y-1">
                    <span class="text-[10px] uppercase font-bold text-slate-500 tracking-wider">Voiceover Script</span>
                    <p class="text-xs text-slate-300 leading-relaxed text-right line-clamp-3" dir="rtl">
                      "{{ scene.voiceoverScript }}"
                    </p>
                  </div>

                  <!-- Visual AI Prompt Preview -->
                  <div class="pt-2 border-t border-slate-800/80 space-y-1">
                    <span class="text-[10px] uppercase font-bold text-slate-500 tracking-wider">AI Prompt</span>
                    <p class="text-[11px] text-slate-400 italic line-clamp-2">
                      {{ scene.visualPrompt }}
                    </p>
                    <div class="flex items-center justify-between text-[10px] text-slate-500 pt-1">
                      <span>🎵 {{ scene.backgroundTrack }}</span>
                    </div>
                  </div>
                </div>
              }
            </div>
          </div>

          <!-- Two Column Studio: Dialect Synthesizer & Multi-Armed Bandit Matrix -->
          <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
            <!-- Left: Regional Dialect & Lip-Sync Synthesizer -->
            <div class="lg:col-span-5 bg-slate-900 rounded-xl border border-slate-800 p-5 space-y-4">
              <div class="flex items-center justify-between">
                <div>
                  <h3 class="text-sm font-bold text-white flex items-center gap-2">
                    <span>🗣️</span> Regional Dialect Synthesizer
                  </h3>
                  <p class="text-xs text-slate-400">Localized idiom injection & lip-sync cultural adaptation</p>
                </div>
                <span class="text-[10px] font-mono px-2 py-0.5 rounded bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
                  Real-Time NLP
                </span>
              </div>

              <!-- Dialect Selection Buttons -->
              <div class="space-y-1.5">
                <label class="text-[11px] uppercase font-bold text-slate-400 tracking-wider">Target Arab Region</label>
                <div class="grid grid-cols-2 gap-2">
                  @for (opt of dialectOptions; track opt.id) {
                    <button
                      type="button"
                      (click)="targetDialect.set(opt.id)"
                      [attr.data-testid]="'dialect-option-' + opt.id"
                      class="px-2.5 py-1.5 text-xs rounded-lg border text-left transition flex items-center justify-between"
                      [ngClass]="targetDialect() === opt.id ? 'bg-indigo-600/30 border-indigo-500 text-white font-semibold' : 'bg-slate-950 border-slate-800 text-slate-400 hover:text-white'">
                      <span>{{ opt.name }}</span>
                      <span class="text-xs">{{ opt.flag }}</span>
                    </button>
                  }
                </div>
              </div>

              <!-- Input Standard Script -->
              <div class="space-y-1.5">
                <label class="text-[11px] uppercase font-bold text-slate-400 tracking-wider">Source Script (Standard Arabic / English)</label>
                <textarea
                  [(ngModel)]="draftScript"
                  data-testid="dialect-script-input"
                  rows="3"
                  class="w-full bg-slate-950 border border-slate-800 text-xs rounded-lg p-2.5 text-slate-200 focus:outline-none focus:border-indigo-500 leading-relaxed text-right"
                  dir="rtl"
                  placeholder="اكتب أو الصق النص هنا ليتم تحويله إلى اللهجة العامية...">
                </textarea>
              </div>

              <!-- Action Button -->
              <button
                (click)="synthesizeScript()"
                data-testid="synthesize-script-btn"
                class="w-full py-2 bg-gradient-to-r from-indigo-600 to-cyan-600 hover:from-indigo-500 hover:to-cyan-500 text-white rounded-lg text-xs font-semibold shadow-md shadow-indigo-900/30 transition flex items-center justify-center gap-2">
                <span>🪄</span> Synthesize Regional Dialect Script
              </button>

              <!-- Output Synthesized Preview -->
              @if (synthesizedResult(); as res) {
                <div class="bg-slate-950 rounded-lg p-3.5 border border-indigo-500/30 space-y-3" data-testid="synthesized-result-box">
                  <div class="flex items-center justify-between">
                    <span class="text-[11px] font-bold text-indigo-300">
                      ✨ Localized Regional Script ({{ getDialectDisplayLabel(res.targetDialect) }})
                    </span>
                    <button
                      (click)="applySynthesizedToActiveHook()"
                      data-testid="apply-to-hook-btn"
                      class="text-[10px] text-cyan-400 hover:text-cyan-300 font-semibold underline">
                      Apply to Hook Scene
                    </button>
                  </div>

                  <p class="text-xs text-white leading-relaxed text-right bg-slate-900/80 p-2.5 rounded border border-slate-800 font-medium" dir="rtl">
                    {{ res.localizedScript }}
                  </p>

                  <!-- Detected / Injected Idioms -->
                  <div class="space-y-1">
                    <span class="text-[10px] text-slate-400 block">Vernacular Idioms Injected:</span>
                    <div class="flex flex-wrap gap-1.5">
                      @for (idiom of res.dialectIdiomsUsed; track idiom) {
                        <span class="px-2 py-0.5 rounded-full text-[10px] bg-amber-500/20 text-amber-300 border border-amber-500/30 font-medium" data-testid="idiom-chip">
                          {{ idiom }}
                        </span>
                      }
                    </div>
                  </div>
                </div>
              }
            </div>

            <!-- Right: Dynamic Creative Variant Matrix & Multi-Armed Bandit -->
            <div class="lg:col-span-7 bg-slate-900 rounded-xl border border-slate-800 p-5 space-y-4">
              <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                <div>
                  <h3 class="text-sm font-bold text-white flex items-center gap-2">
                    <span>🎰</span> Dynamic Creative Variant Matrix
                  </h3>
                  <p class="text-xs text-slate-400">Multi-Armed Bandit (MAB) with Thomson Exploration & CTR Predictor</p>
                </div>

                <div class="flex items-center gap-2">
                  <select
                    [(ngModel)]="variantCountToGenerate"
                    data-testid="variant-count-select"
                    class="bg-slate-950 border border-slate-800 text-xs rounded-lg px-2.5 py-1.5 text-slate-200">
                    <option [value]="3">3 Variants</option>
                    <option [value]="5">5 Variants</option>
                  </select>
                  <button
                    (click)="generateBanditVariants()"
                    data-testid="generate-variants-btn"
                    class="py-1.5 px-3 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-xs font-semibold shadow-md shadow-emerald-900/30 transition flex items-center gap-1.5">
                    <span>⚡</span> Generate Variants
                  </button>
                </div>
              </div>

              <!-- Variants Table -->
              <div class="overflow-x-auto rounded-lg border border-slate-800">
                <table class="w-full text-left text-xs text-slate-300" data-testid="variants-matrix-table">
                  <thead class="bg-slate-950/80 text-slate-400 uppercase text-[10px] tracking-wider border-b border-slate-800">
                    <tr>
                      <th class="px-3.5 py-2.5">Code</th>
                      <th class="px-3.5 py-2.5">Hook Angle</th>
                      <th class="px-3.5 py-2.5">CTA Text</th>
                      <th class="px-3.5 py-2.5 text-center">Exploration Score</th>
                      <th class="px-3.5 py-2.5 text-right">Predicted CTR</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-800">
                    @for (v of activeProjectVariants(); track v.variantId) {
                      <tr class="hover:bg-slate-800/40 transition">
                        <td class="px-3.5 py-2.5 font-mono text-[11px] text-indigo-400 font-bold whitespace-nowrap">
                          {{ v.variantCode }}
                        </td>
                        <td class="px-3.5 py-2.5 text-right font-medium max-w-[200px] truncate text-slate-200" dir="rtl">
                          {{ v.hookText }}
                        </td>
                        <td class="px-3.5 py-2.5 text-right text-slate-300 max-w-[160px] truncate" dir="rtl">
                          {{ v.ctaText }}
                        </td>
                        <td class="px-3.5 py-2.5">
                          <div class="flex items-center gap-2 justify-center">
                            <div class="w-16 bg-slate-800 rounded-full h-1.5 overflow-hidden">
                              <div
                                class="h-full bg-cyan-400 rounded-full"
                                [style.width.%]="v.banditExplorationScore * 100"></div>
                            </div>
                            <span class="font-mono text-[10px] text-slate-400">
                              {{ v.banditExplorationScore.toFixed(2) }}
                            </span>
                          </div>
                        </td>
                        <td class="px-3.5 py-2.5 text-right whitespace-nowrap">
                          <span class="px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-500/20 text-emerald-300 border border-emerald-500/30">
                            {{ v.predictedCtrPercent.toFixed(2) }}%
                          </span>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        </div>
      </main>
    </div>
  `
})
export class VideoCreativePipelineStudioComponent {
  public translation!: TranslationService;

  public getAspectRatioLabel = getAspectRatioLabel;
  public getDialectDisplayLabel = getDialectDisplayLabel;
  public getStageMeta = getStageMeta;

  public dialectOptions: { id: ArabicRegionalDialect; name: string; flag: string }[] = [
    { id: 'SaudiNajdi', name: 'Saudi Najdi (الوسطى)', flag: '🇸🇦' },
    { id: 'SaudiHejazi', name: 'Saudi Hejazi (الغربية)', flag: '🇸🇦' },
    { id: 'GulfKhaleeji', name: 'Gulf Khaleeji', flag: '🇦🇪' },
    { id: 'Egyptian', name: 'Egyptian Masri', flag: '🇪🇬' },
    { id: 'ModernStandardArabic', name: 'Standard Arabic', flag: '🌍' }
  ];

  // Seeded Projects
  public projects = signal<VideoAdCampaignProject[]>([
    {
      projectId: 'a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d',
      tenantId: '11111111-1111-1111-1111-111111111111',
      projectTitle: 'Ramadan Luxury Oud Collection - TikTok Hook',
      productSku: 'OUD-ROYAL-RAMADAN-2026',
      targetAudience: 'Affluent GCC Perfume Collectors & Ramadan Gift Buyers',
      dialect: 'SaudiNajdi',
      aspectRatio: 'Vertical_9_16',
      scenes: [
        {
          sceneNumber: 1,
          stage: 'HookIntro',
          visualPrompt: 'Cinematic close-up of dark vintage agarwood resin bubbling on charcoal, golden Ramadan crescent backdrop',
          voiceoverScript: 'وش رايك في عطر يثبت فخامتك بكل مجلس برمضان؟ أبشر بعزك مع دهن العود الملكي المعتّق',
          durationSeconds: 4,
          backgroundTrack: 'Ambient Oud & Subtle Reverb',
          textOverlayText: 'وش رايك في الفخامة الحقيقية؟ 🌙'
        },
        {
          sceneNumber: 2,
          stage: 'ProblemStatement',
          visualPrompt: 'Frustrated Gulf shopper looking at regular perfumes that evaporate within 30 minutes in Saudi heat',
          voiceoverScript: 'العطور التجارية تطير بربع ساعة وما تثبت بحرارة الصيف أو زحمة التراويح',
          durationSeconds: 5,
          backgroundTrack: 'Low Tension Strings',
          textOverlayText: 'العطور العادية ما تدوم؟ ⏳'
        },
        {
          sceneNumber: 3,
          stage: 'ProductShowcase',
          visualPrompt: 'Ultra-luxury 3D rotating crystal bottle of Royal Heritage Aged Cambodi Oud with 24k gold leaf',
          voiceoverScript: 'خلطتنا النادرة من دهن عود كمبودي صافي معتق لأكثر من 15 سنة، تركيز زيت 100% بدون كحول',
          durationSeconds: 6,
          backgroundTrack: 'Majestic Arabic Dulcimer Beats',
          textOverlayText: 'معتق 15 سنة • ثبات 72 ساعة 🔥'
        },
        {
          sceneNumber: 4,
          stage: 'SocialProofTestimonial',
          visualPrompt: 'Saudi VIP client receiving compliments at Riyadh majlis gathering, verified 5-star review carousel',
          voiceoverScript: 'أكثر من 18 ألف عميل بالرياض وجدة يشهدون لثبات الرائحة لأكثر من يومين كاملة',
          durationSeconds: 5,
          backgroundTrack: 'Inspiring Acoustic Pulse',
          textOverlayText: 'تقييم 4.9/5 من أكثر من 18,000 عميل ⭐'
        },
        {
          sceneNumber: 5,
          stage: 'CallToAction',
          visualPrompt: 'Dynamic animated badge: Ramadan 40% OFF with fast Riyadh delivery in 2 hours box',
          voiceoverScript: 'يا هلا ومسهلا! اطلب الحين قبل نفاد الكمية واستلم خلال ساعتين بالرياض مع شحن مجاني',
          durationSeconds: 5,
          backgroundTrack: 'Energetic Arabic Trap Riser',
          textOverlayText: 'خصم 40% لرمضان • اطلب الحين! 🚀'
        }
      ],
      totalDurationSeconds: 25,
      estimatedRenderCostSar: 45.0,
      createdAt: '2026-11-01T10:00:00Z'
    },
    {
      projectId: 'b2c3d4e5-f6a7-8b9c-0d1e-2f3a4b5c6d7e',
      tenantId: '11111111-1111-1111-1111-111111111111',
      projectTitle: 'Smart Cloud POS for Cafes & Restaurants',
      productSku: 'POS-CLOUD-CAFE-ENTERPRISE',
      targetAudience: 'F&B Business Owners & Coffee Shop Managers in GCC',
      dialect: 'GulfKhaleeji',
      aspectRatio: 'Square_1_1',
      scenes: [
        {
          sceneNumber: 1,
          stage: 'HookIntro',
          visualPrompt: 'Fast-paced split screen of long cafe queues vs seamless NFC tap payment on modern tablet POS',
          voiceoverScript: 'شنو رايك بطابور يخلص بثواني وفواتيرك كلها مربوطة مباشرة مع الزكاة والضريبة؟',
          durationSeconds: 4,
          backgroundTrack: 'Upbeat Modern Tech Groove',
          textOverlayText: 'وداعاً لزحمة الكاشير! ☕💳'
        },
        {
          sceneNumber: 2,
          stage: 'ProblemStatement',
          visualPrompt: 'Cafe owner stressed over tablet crashing during rush hour and missing order tickets',
          voiceoverScript: 'الأنظمة القديمة تعلق وقت الذروة وتضيع الطلبات وتخرب تجربة الزبون',
          durationSeconds: 5,
          backgroundTrack: 'Dynamic Synths & Hi-Hats',
          textOverlayText: 'النظام علق وقت الذروة؟ ⚠️'
        },
        {
          sceneNumber: 3,
          stage: 'ProductShowcase',
          visualPrompt: 'Sleek dual-screen cloud POS showing live inventory sync, ZATCA Phase 2 QR generation',
          voiceoverScript: 'نظامنا السحابي الذكي يشتغل أوفلاين وأونلاين، تقارير مبيعات لحظية وإدارة طاولات متكاملة',
          durationSeconds: 6,
          backgroundTrack: 'Confident Tech Momentum',
          textOverlayText: 'سحابي • معتمد من هيئة الزكاة والضريبة 📊'
        },
        {
          sceneNumber: 4,
          stage: 'CallToAction',
          visualPrompt: 'Barista happily serving coffee while tablet displays 14-day zero-risk trial activation button',
          voiceoverScript: 'فالك طيب! سجل الحين وجرّب النظام مجاناً لمدة 14 يوم بدون أي التزام مالي',
          durationSeconds: 5,
          backgroundTrack: 'Smooth Corporate Finish',
          textOverlayText: 'جرّب مجاناً 14 يوم • فالك طيب 💼'
        }
      ],
      totalDurationSeconds: 20,
      estimatedRenderCostSar: 35.0,
      createdAt: '2026-11-03T14:00:00Z'
    }
  ]);

  // Seeded Variants
  public allVariants = signal<CreativeVariantRecord[]>([
    {
      variantId: '11112222-3333-4444-5555-666677778881',
      projectId: 'a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d',
      tenantId: '11111111-1111-1111-1111-111111111111',
      variantCode: 'VAR-NAJ-01',
      hookText: 'وش رايك في عطر يثبت فخامتك بكل مجلس برمضان؟',
      ctaText: 'أبشر بعزك! اطلب الحين واستلم بساعتين 🚀',
      banditExplorationScore: 0.88,
      predictedCtrPercent: 5.42,
      generatedAt: '2026-11-02T08:30:00Z'
    },
    {
      variantId: '11112222-3333-4444-5555-666677778882',
      projectId: 'a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d',
      tenantId: '11111111-1111-1111-1111-111111111111',
      variantCode: 'VAR-NAJ-02',
      hookText: 'عطورك تطير بربع ساعة؟ دهن العود الملكي يثبت 72 ساعة',
      ctaText: 'لا يفوتك عرض رمضان 40% خصم 🌙',
      banditExplorationScore: 0.65,
      predictedCtrPercent: 4.85,
      generatedAt: '2026-11-02T08:35:00Z'
    },
    {
      variantId: '11112222-3333-4444-5555-666677778883',
      projectId: 'a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d',
      tenantId: '11111111-1111-1111-1111-111111111111',
      variantCode: 'VAR-NAJ-03',
      hookText: 'يا هلا ومسهلا! سر الفخامة الملكية صار بين إيديك',
      ctaText: 'اطلب الحين قبل نفاد الكمية وشحن مجاني 📦',
      banditExplorationScore: 0.42,
      predictedCtrPercent: 4.15,
      generatedAt: '2026-11-02T08:40:00Z'
    },
    {
      variantId: '22223333-4444-5555-6666-777788889991',
      projectId: 'b2c3d4e5-f6a7-8b9c-0d1e-2f3a4b5c6d7e',
      tenantId: '11111111-1111-1111-1111-111111111111',
      variantCode: 'VAR-GUL-01',
      hookText: 'شنو رايك بكاشير ذكي يخلص الطابور بثواني مع الزكاة والضريبة؟',
      ctaText: 'فالك طيب! جرّب مجاناً 14 يوم بدون أي التزام ☕',
      banditExplorationScore: 0.91,
      predictedCtrPercent: 5.1,
      generatedAt: '2026-11-04T09:00:00Z'
    },
    {
      variantId: '22223333-4444-5555-6666-777788889992',
      projectId: 'b2c3d4e5-f6a7-8b9c-0d1e-2f3a4b5c6d7e',
      tenantId: '11111111-1111-1111-1111-111111111111',
      variantCode: 'VAR-GUL-02',
      hookText: 'تعبت من تعليق نظام الكاشير وقت ذروة المقاهي؟',
      ctaText: 'انضم لأكثر من 500 كافيه وابدأ التجربة المجانية ⚡',
      banditExplorationScore: 0.58,
      predictedCtrPercent: 4.3,
      generatedAt: '2026-11-04T09:10:00Z'
    }
  ]);

  public selectedProjectId = signal<string>('a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d');

  public selectedProject = computed(() => {
    return this.projects().find(p => p.projectId === this.selectedProjectId()) ?? this.projects()[0];
  });

  public activeProjectVariants = computed(() => {
    const projId = this.selectedProject().projectId;
    return this.allVariants().filter(v => v.projectId === projId);
  });

  // KPI Computeds
  public activeProjectsCount = computed(() => this.projects().length);
  public renderedVariantsCount = computed(() => this.allVariants().length);
  public avgPredictedCtr = computed(() => {
    const variants = this.allVariants();
    if (!variants.length) return '0.0%';
    const sum = variants.reduce((acc, curr) => acc + curr.predictedCtrPercent, 0);
    return (sum / variants.length).toFixed(1) + '%';
  });
  public dialectCoverageCount = computed(() => {
    const dialects = new Set(this.projects().map(p => p.dialect));
    return dialects.size;
  });

  // Dialect Synthesizer Form State
  public targetDialect = signal<ArabicRegionalDialect>('SaudiNajdi');
  public draftScript = signal<string>(
    'ما رأيك في تجربة منتجنا المميز اليوم؟ يسرنا خدمتك، ولا تفوت فرصة الاستفادة من العرض الحصري.'
  );
  public synthesizedResult = signal<SynthesizedScriptResult | null>(null);

  // Variant Generator Form State
  public variantCountToGenerate = signal<number>(3);
  public statusMessage = signal<string | null>(null);

  constructor(@Optional() translationService?: TranslationService) {
    if (translationService) {
      this.translation = translationService;
    } else {
      try {
        this.translation = inject(TranslationService);
      } catch {
        this.translation = new TranslationService();
      }
    }
    // Pre-synthesize default dialect preview
    this.synthesizeScript();
  }

  public onSelectProject(projectId: string): void {
    this.selectedProjectId.set(projectId);
    this.targetDialect.set(this.selectedProject().dialect);
  }

  public synthesizeScript(): void {
    const result = synthesizeRegionalDialect(this.draftScript(), this.targetDialect());
    this.synthesizedResult.set(result);
    this.statusMessage.set(`Synthesized regional dialect script for ${getDialectDisplayLabel(this.targetDialect())}`);
  }

  public applySynthesizedToActiveHook(): void {
    const res = this.synthesizedResult();
    if (!res) return;

    const currentProject = this.selectedProject();
    const updatedScenes = currentProject.scenes.map(s => {
      if (s.stage === 'HookIntro') {
        return { ...s, voiceoverScript: res.localizedScript };
      }
      return s;
    });

    const updatedProjects = this.projects().map(p => {
      if (p.projectId === currentProject.projectId) {
        return { ...p, scenes: updatedScenes };
      }
      return p;
    });

    this.projects.set(updatedProjects);
    this.statusMessage.set('Applied localized regional script to Hook Scene voiceover!');
  }

  public generateBanditVariants(): void {
    const currentProject = this.selectedProject();
    const count = Number(this.variantCountToGenerate()) || 3;
    const existing = this.allVariants().filter(v => v.projectId === currentProject.projectId);

    const dialectCode = currentProject.dialect === 'SaudiNajdi'
      ? 'NAJ'
      : currentProject.dialect === 'SaudiHejazi'
        ? 'HEJ'
        : currentProject.dialect === 'GulfKhaleeji'
          ? 'GUL'
          : 'EGY';

    const newVariants: CreativeVariantRecord[] = [];
    for (let i = 1; i <= count; i++) {
      const idx = existing.length + i;
      const explorationScore = Math.round((0.4 + 0.55 * (1 / Math.sqrt(idx))) * 100) / 100;
      const predictedCtr = Math.round((3.8 + (idx % 4) * 0.65) * 100) / 100;

      newVariants.push({
        variantId: `var-new-${Date.now()}-${i}`,
        projectId: currentProject.projectId,
        tenantId: currentProject.tenantId,
        variantCode: `VAR-${dialectCode}-${idx < 10 ? '0' + idx : idx}`,
        hookText: `${currentProject.projectTitle} - زاوية إبداعية #${idx}`,
        ctaText: 'اطلب الحين واستفد من العرض الحصري! 🚀',
        banditExplorationScore: explorationScore,
        predictedCtrPercent: predictedCtr,
        generatedAt: new Date().toISOString()
      });
    }

    this.allVariants.set([...this.allVariants(), ...newVariants]);
    this.statusMessage.set(`Generated ${count} multi-armed bandit creative variants with exploration scores!`);
  }
}
