import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface VideoTemplate {
  templateId: string;
  templateName: string;
  aspectRatio: string;
  durationSeconds: number;
  dynamicLayers: string[];
  status: string;
}

export interface RenderedVariant {
  variantId: string;
  templateId: string;
  targetAudience: string;
  localizedCurrency: string;
  displayedPrice: number;
  renditionUrl: string;
  cdnStatus: string;
}

@Component({
  selector: 'app-video-dco-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" data-testid="video-dco-cockpit" class="p-6 bg-slate-900 text-slate-100 min-h-screen">
      <header class="mb-8 border-b border-slate-800 pb-4 flex justify-between items-center">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-white flex items-center gap-2">
            <span class="inline-block w-3 h-3 rounded-full bg-indigo-500 animate-pulse"></span>
            Programmatic Video DCO & Real-Time Render Mesh
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Distributed Dynamic Video Manifest Synthesis & Localized Real-Time Composition Engine
          </p>
        </div>
        <div class="flex gap-3">
          <button (click)="renderNewVariant()" class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white font-medium rounded-lg shadow-sm transition">
            Dispatch 4K Render Job
          </button>
        </div>
      </header>

      <!-- KPI Metrics Grid -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Active Templates</span>
          <p class="text-2xl font-extrabold text-white mt-1">{{ templates().length }}</p>
          <span class="text-xs text-indigo-400">16:9, 9:16, 1:1 Ready</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Rendered Variants</span>
          <p class="text-2xl font-extrabold text-indigo-400 mt-1">{{ variants().length }}</p>
          <span class="text-xs text-slate-400">Localized across 18 Currencies</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Avg Render Latency</span>
          <p class="text-2xl font-extrabold text-emerald-400 mt-1">1.45s</p>
          <span class="text-xs text-slate-400">GPU Accelerated FFmpeg</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">CDN Cache Hit Ratio</span>
          <p class="text-2xl font-extrabold text-cyan-400 mt-1">{{ cdnHitRatio() }}%</p>
          <span class="text-xs text-emerald-400">Warmed Edge POPs</span>
        </div>
      </section>

      <!-- Main Panels -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Templates List -->
        <section class="bg-slate-800/40 border border-slate-700/50 rounded-xl p-5">
          <h2 class="text-lg font-semibold text-white mb-4">Composition Templates</h2>
          <div class="space-y-3">
            <div *ngFor="let tpl of templates()" class="p-3 bg-slate-900/60 border border-slate-700/40 rounded-lg">
              <div class="flex justify-between items-center mb-1">
                <span class="font-medium text-slate-200 text-sm">{{ tpl.templateName }}</span>
                <span class="px-2 py-0.5 text-xs bg-slate-700 text-slate-300 rounded font-mono">{{ tpl.aspectRatio }}</span>
              </div>
              <p class="text-xs text-slate-400 mb-2">Duration: {{ tpl.durationSeconds }}s (60 fps)</p>
              <div class="flex flex-wrap gap-1">
                <span *ngFor="let layer of tpl.dynamicLayers" class="px-1.5 py-0.5 bg-indigo-950 text-indigo-300 text-[10px] rounded border border-indigo-800/40">
                  {{ layer }}
                </span>
              </div>
            </div>
          </div>
        </section>

        <!-- Variants Table -->
        <section class="lg:col-span-2 bg-slate-800/40 border border-slate-700/50 rounded-xl p-5">
          <h2 class="text-lg font-semibold text-white mb-4">Active Rendered Variants</h2>
          <div class="overflow-x-auto">
            <table class="w-full text-left border-collapse text-sm">
              <thead>
                <tr class="border-b border-slate-700 text-slate-400 text-xs">
                  <th class="pb-3 font-medium">Variant ID</th>
                  <th class="pb-3 font-medium">Target Audience</th>
                  <th class="pb-3 font-medium">Currency / Price</th>
                  <th class="pb-3 font-medium">CDN Edge Status</th>
                  <th class="pb-3 font-medium">Rendition URL</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800">
                <tr *ngFor="let v of variants()" class="hover:bg-slate-800/50 transition">
                  <td class="py-3 font-mono text-xs text-indigo-300">{{ v.variantId }}</td>
                  <td class="py-3 font-medium text-slate-200">{{ v.targetAudience }}</td>
                  <td class="py-3 text-emerald-400 font-mono">{{ v.localizedCurrency }} {{ v.displayedPrice }}</td>
                  <td class="py-3">
                    <span class="px-2 py-0.5 text-xs rounded font-mono bg-emerald-500/20 text-emerald-300">
                      {{ v.cdnStatus }}
                    </span>
                  </td>
                  <td class="py-3 text-slate-400 font-mono text-xs truncate max-w-[200px]">{{ v.renditionUrl }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </div>
    </div>
  `
})
export class VideoDcoCockpitComponent {
  templates = signal<VideoTemplate[]>([
    { templateId: 'TPL-LANDSCAPE-169', templateName: 'B2B Enterprise Showcase 16:9', aspectRatio: '16:9', durationSeconds: 30, dynamicLayers: ['VideoBackground', 'LocalizedLogo', 'ValuePropositionText', 'DynamicPriceBadge', 'OutroCTA'], status: 'ACTIVE' },
    { templateId: 'TPL-VERTICAL-916', templateName: 'TikTok & Reels Dynamic Teaser 9:16', aspectRatio: '9:16', durationSeconds: 15, dynamicLayers: ['HookIntro', 'UGCReviewerCard', 'AnimatedOfferSticker', 'SwipeUpCTA'], status: 'ACTIVE' },
    { templateId: 'TPL-SQUARE-11', templateName: 'LinkedIn Feed Carousel 1:1', aspectRatio: '1:1', durationSeconds: 20, dynamicLayers: ['StatCounterLayer', 'CustomerQuoteLayer', 'BookDemoButton'], status: 'ACTIVE' }
  ]);

  variants = signal<RenderedVariant[]>([
    { variantId: 'VAR-EU-DE-01', templateId: 'TPL-LANDSCAPE-169', targetAudience: 'DACH Enterprise FinTech', localizedCurrency: 'EUR', displayedPrice: 4990, renditionUrl: 'https://cdn.sgrowing.io/dco/render/eu-de-fintech-1080p.mp4', cdnStatus: 'WARMED_EDGE' },
    { variantId: 'VAR-US-NY-02', templateId: 'TPL-VERTICAL-916', targetAudience: 'US SaaS Founders', localizedCurrency: 'USD', displayedPrice: 299, renditionUrl: 'https://cdn.sgrowing.io/dco/render/us-saas-founders-4k.mp4', cdnStatus: 'WARMED_EDGE' }
  ]);

  cdnHitRatio = signal(97.8);

  renderNewVariant() {
    const newVar: RenderedVariant = {
      variantId: `VAR-APAC-${Date.now().toString().slice(-4)}`,
      templateId: 'TPL-LANDSCAPE-169',
      targetAudience: 'APAC Enterprise FinTech',
      localizedCurrency: 'SGD',
      displayedPrice: 7500,
      renditionUrl: 'https://cdn.sgrowing.io/dco/render/apac-fintech-4k.mp4',
      cdnStatus: 'WARMED_EDGE'
    };
    this.variants.update(list => [newVar, ...list]);
  }
}
