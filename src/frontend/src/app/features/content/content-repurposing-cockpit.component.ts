import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface DerivativeView {
  derivativeId: string;
  channel: 'LinkedIn' | 'XTwitter' | 'Newsletter' | 'SlideCarousel';
  title: string;
  body: string;
  readSeconds: number;
  hashtags: string[];
}

export interface CarouselSlideView {
  slideNum: number;
  header: string;
  bullets: string;
}

@Component({
  selector: 'app-content-repurposing-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="content-repurposing-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-700/60 pb-6">
        <div>
          <div class="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-pink-400">
            <span class="inline-block w-2 h-2 rounded-full bg-pink-400 animate-pulse"></span>
            Omnichannel Content Distribution • Horizon 71
          </div>
          <h1 class="text-3xl font-extrabold text-white tracking-tight mt-1">
            Autonomous Multi-Agent Content Repurposing Engine
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Deconstruct cornerstone keynotes & whitepapers into LinkedIn carousels, X threads, and executive newsletters.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="generateShortVideoScript()"
            data-testid="generate-short-btn"
            class="px-4 py-2 bg-pink-600 hover:bg-pink-500 text-white rounded-lg text-sm font-semibold transition-colors flex items-center gap-2 shadow-lg shadow-pink-600/20"
          >
            <span>🎬</span> Synthesize TikTok / Shorts Script
          </button>
        </div>
      </div>

      <!-- KPI Overview Stats -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Active Derivatives</div>
          <div class="text-2xl font-bold text-white mt-1" data-testid="stat-derivatives-count">
            {{ derivatives().length }} Formats
          </div>
          <div class="text-pink-400 text-xs mt-1">Asset: SDR Outbound Keynote</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Carousel Slide Count</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1" data-testid="stat-slide-count">
            {{ slides().length }} Slides
          </div>
          <div class="text-slate-400 text-xs mt-1">LinkedIn PDF Ready</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Total Audience Reach</div>
          <div class="text-2xl font-bold text-cyan-400 mt-1" data-testid="stat-est-reach">
            42,500
          </div>
          <div class="text-emerald-400 text-xs mt-1">Multi-Platform Distribution</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Time Savings Ratio</div>
          <div class="text-2xl font-bold text-amber-400 mt-1" data-testid="stat-savings">
            8.5x
          </div>
          <div class="text-slate-400 text-xs mt-1">45s AI synthesis vs 6 hrs manual</div>
        </div>
      </div>

      <!-- Derivatives Grid & Slide Carousel Canvas -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <!-- Derivatives (7 cols) -->
        <div class="lg:col-span-7 bg-slate-800/80 border border-slate-700/60 rounded-xl overflow-hidden">
          <div class="p-4 border-b border-slate-700/60 flex items-center justify-between">
            <h2 class="text-lg font-bold text-white">Synthesized Multichannel Content</h2>
            <span class="text-xs text-slate-400 font-mono">{{ derivatives().length }} Formats</span>
          </div>

          <div class="divide-y divide-slate-700/40">
            @for (item of derivatives(); track item.derivativeId) {
              <div class="p-4 hover:bg-slate-700/30 transition-colors space-y-2">
                <div class="flex items-center justify-between">
                  <div class="flex items-center gap-2">
                    <span class="font-bold text-white text-sm">{{ item.title }}</span>
                    <span class="px-2 py-0.5 rounded text-[10px] font-semibold bg-pink-950 text-pink-300 border border-pink-800">
                      {{ item.channel }}
                    </span>
                  </div>
                  <span class="text-xs text-slate-400">{{ item.readSeconds }}s read</span>
                </div>

                <p class="text-xs text-slate-300 whitespace-pre-line leading-relaxed bg-slate-900 p-3 rounded-lg border border-slate-800 font-sans">
                  {{ item.body }}
                </p>

                <div class="flex items-center gap-2 flex-wrap text-[11px] text-cyan-400">
                  @for (tag of item.hashtags; track tag) {
                    <span>{{ tag }}</span>
                  }
                </div>
              </div>
            }
          </div>
        </div>

        <!-- 5-Slide Carousel Preview (5 cols) -->
        <div class="lg:col-span-5 bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 space-y-4">
          <div class="flex items-center justify-between border-b border-slate-700/60 pb-3">
            <div>
              <h3 class="font-bold text-white text-base">LinkedIn Slide Carousel</h3>
              <p class="text-xs text-slate-400">PDF Document Layout</p>
            </div>
            <span class="text-xs px-2.5 py-1 rounded bg-emerald-950 text-emerald-300 border border-emerald-800">
              5 Slides Generated
            </span>
          </div>

          <div class="space-y-3">
            @for (slide of slides(); track slide.slideNum) {
              <div class="p-3 bg-slate-900 rounded-lg border border-slate-800 space-y-1">
                <div class="flex items-center justify-between">
                  <span class="text-xs font-bold text-pink-400">Slide {{ slide.slideNum }}</span>
                  <span class="text-[11px] text-white font-semibold">{{ slide.header }}</span>
                </div>
                <p class="text-[11px] text-slate-400 leading-normal">{{ slide.bullets }}</p>
              </div>
            }
          </div>
        </div>
      </div>
    </div>
  `
})
export class ContentRepurposingCockpitComponent {
  readonly derivatives = signal<DerivativeView[]>([
    {
      derivativeId: 'deriv_li_1',
      channel: 'LinkedIn',
      title: 'Why SDR Cold Outreach Died in 2026',
      body: 'Traditional cold emailing is burning your domain reputation. Here is the data: 1.2% response vs 8.4% with autonomous swarms.\n\n3 key takeaways:\n1. De-anonymize buying intent before sending.\n2. Involve the whole buying committee.\n3. Automate objection handling under 150ms.',
      readSeconds: 45,
      hashtags: ['#B2BGrowth', '#AgenticAI', '#OutboundRevolution']
    },
    {
      derivativeId: 'deriv_x_1',
      channel: 'XTwitter',
      title: 'The Death of the 200-SDR Sales Floor [1/5]',
      body: '1/ The typical $100k B2B SaaS deal now involves 8 decision makers.\n2/ Blasting 1 persona is dead on arrival.\n3/ Autonomous swarms map the committee and tailor multi-year ramps.',
      readSeconds: 30,
      hashtags: ['#SalesTech', '#AI']
    },
    {
      derivativeId: 'deriv_nl_1',
      channel: 'Newsletter',
      title: 'Executive Briefing: Modernizing Your Go-To-Market Engine',
      body: 'Dear Founder,\n\nWe recently analyzed 10,000 enterprise B2B sales cycles. Teams relying on static sequences saw reply rates collapse to 1.2%, while autonomous multi-agent meshes achieved 8.4% engagement.',
      readSeconds: 90,
      hashtags: ['#ExecutiveBriefing']
    }
  ]);

  readonly slides = signal<CarouselSlideView[]>([
    { slideNum: 1, header: 'The Outbound Dilemma', bullets: 'SDR reply rates dropped to 1.2% in 2026.' },
    { slideNum: 2, header: 'The Real Cause', bullets: '8+ stakeholders per buying committee; single-lead outreach fails.' },
    { slideNum: 3, header: 'The Autonomous Solution', bullets: 'Coordinated agent swarms handle research and CISO briefs.' },
    { slideNum: 4, header: 'The Business Impact', bullets: '8.4% reply rate, 3.4x productivity multiplier.' },
    { slideNum: 5, header: 'Takeaway & Action', bullets: 'Upgrade to the AI-Native Growth OS today.' }
  ]);

  generateShortVideoScript(): void {
    const videoScript: DerivativeView = {
      derivativeId: `deriv_video_${Date.now()}`,
      channel: 'SlideCarousel',
      title: '60s TikTok / Shorts Video Script',
      body: '[Hook] If your sales team is still manually writing cold emails, stop scrolling.\n[Body] Here is why top Series B companies switched to autonomous swarms.',
      readSeconds: 60,
      hashtags: ['#Shorts', '#ViralAI']
    };

    this.derivatives.update(list => [...list, videoScript]);
  }
}
