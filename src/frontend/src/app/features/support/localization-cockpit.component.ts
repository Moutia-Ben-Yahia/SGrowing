import { Component, ChangeDetectionStrategy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface LocalizedSnippetView {
  id: string;
  sourceLang: string;
  targetLang: string;
  originalText: string;
  localizedText: string;
  formality: string;
  politenessScore: number;
  preservedTerms: string[];
}

@Component({
  selector: 'app-localization-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="localization-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-700/60 pb-6">
        <div>
          <div class="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-sky-400">
            <span class="inline-block w-2 h-2 rounded-full bg-sky-400 animate-pulse"></span>
            Global Communications & Multilingual AI • Horizon 69
          </div>
          <h1 class="text-3xl font-extrabold text-white tracking-tight mt-1">
            Multilingual Support Translation & Cultural Nuance Mesh
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            In-flight bidirectional translation (&lt;200ms), business honorific enforcement & protected brand term preservation.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="translateToJapaneseKeigo()"
            data-testid="translate-ja-btn"
            class="px-4 py-2 bg-sky-600 hover:bg-sky-500 text-white rounded-lg text-sm font-semibold transition-colors flex items-center gap-2 shadow-lg shadow-sky-600/20"
          >
            <span>⛩️</span> Translate to Japanese Keigo
          </button>
        </div>
      </div>

      <!-- KPI Overview Stats -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Active Stream Latency</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1" data-testid="stat-latency">
            {{ activeLatency() }} ms
          </div>
          <div class="text-sky-400 text-xs mt-1">Sub-200ms SLA Target</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Politeness & Nuance Index</div>
          <div class="text-2xl font-bold text-white mt-1" data-testid="stat-politeness">
            {{ currentMessage().politenessScore }}%
          </div>
          <div class="text-emerald-400 text-xs mt-1">Honorific Compliance Verified</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Protected Brand Terms</div>
          <div class="text-2xl font-bold text-cyan-400 mt-1" data-testid="stat-preserved-count">
            {{ currentMessage().preservedTerms.length }} Terms
          </div>
          <div class="text-slate-400 text-xs mt-1">Zero Mistranslation Lock</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Target Language / Formality</div>
          <div class="text-2xl font-bold text-amber-400 mt-1" data-testid="stat-target-lang">
            {{ currentMessage().targetLang.toUpperCase() }} ({{ currentMessage().formality }})
          </div>
          <div class="text-slate-400 text-xs mt-1">Culturally Attuned Tone</div>
        </div>
      </div>

      <!-- Dual-Stream Realtime Conversation Console -->
      <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
        <!-- Original English Source -->
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 space-y-3">
          <div class="flex items-center justify-between border-b border-slate-700/60 pb-3">
            <div class="flex items-center gap-2">
              <span class="text-base font-bold text-white">Source Input</span>
              <span class="px-2 py-0.5 rounded text-[10px] font-semibold bg-slate-700 text-slate-300">English (EN)</span>
            </div>
            <span class="text-xs text-slate-400">Customer Support Agent</span>
          </div>

          <div class="p-4 bg-slate-900 rounded-lg border border-slate-800 text-sm text-slate-200 leading-relaxed font-sans min-h-[140px]">
            {{ currentMessage().originalText }}
          </div>
        </div>

        <!-- Localized Real-Time Output -->
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 space-y-3">
          <div class="flex items-center justify-between border-b border-slate-700/60 pb-3">
            <div class="flex items-center gap-2">
              <span class="text-base font-bold text-sky-400">Localized Cultural Output</span>
              <span class="px-2 py-0.5 rounded text-[10px] font-semibold bg-sky-950 text-sky-300 border border-sky-800">
                {{ currentMessage().targetLang.toUpperCase() }}
              </span>
            </div>
            <span class="text-xs text-emerald-400">Politeness: {{ currentMessage().politenessScore }}%</span>
          </div>

          <div class="p-4 bg-slate-900 rounded-lg border border-sky-900/60 text-sm text-sky-100 leading-relaxed font-sans min-h-[140px]" data-testid="localized-output-box">
            {{ currentMessage().localizedText }}
          </div>

          <!-- Preserved Terms Badges -->
          <div class="flex items-center gap-2 flex-wrap pt-2">
            <span class="text-xs text-slate-400">Preserved Glossary Terms:</span>
            @for (term of currentMessage().preservedTerms; track term) {
              <span class="px-2 py-0.5 bg-slate-700/60 text-cyan-300 text-[11px] rounded font-mono">
                {{ term }}
              </span>
            }
          </div>
        </div>
      </div>
    </div>
  `
})
export class LocalizationCockpitComponent {
  readonly activeLatency = signal(142);

  readonly currentMessage = signal<LocalizedSnippetView>({
    id: 'msg_support_401',
    sourceLang: 'en',
    targetLang: 'de',
    originalText: 'Thank you for contacting SGrowing support. We have verified your SOC2 Type II compliance audit and resolved the CAPI sync latency.',
    localizedText: 'Vielen Dank für Ihre Kontaktaufnahme mit dem SGrowing Support. Wir haben Ihr SOC2 Type II Compliance-Audit verifiziert und die CAPI-Synchronisationslatenz behoben.',
    formality: 'FormalExecutive',
    politenessScore: 98.5,
    preservedTerms: ['SGrowing', 'SOC2 Type II', 'CAPI']
  });

  translateToJapaneseKeigo(): void {
    this.activeLatency.set(168);
    this.currentMessage.set({
      id: 'msg_support_402',
      sourceLang: 'en',
      targetLang: 'ja',
      originalText: 'Thank you for contacting SGrowing support. We have verified your SOC2 Type II compliance audit and resolved the CAPI sync latency.',
      localizedText: 'SGrowing サポートにお問い合わせいただき、誠にありがとうございます。SOC2 Type II コンプライアンス監査を確認し、CAPI 同期のレイテンシを解消いたしました。',
      formality: 'FormalExecutive',
      politenessScore: 99.2,
      preservedTerms: ['SGrowing', 'SOC2 Type II', 'CAPI']
    });
  }
}
