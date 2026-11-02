import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface TestimonialItemView {
  id: string;
  name: string;
  title: string;
  company: string;
  domain: string;
  format: 'VideoRecording' | 'AudioClip' | 'RichTextReview';
  stars: number;
  soundbite: string;
  fullQuote: string;
  sentiment: number;
  isApproved: boolean;
}

@Component({
  selector: 'app-social-proof-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="social-proof-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-700/60 pb-6">
        <div>
          <div class="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-pink-400">
            <span class="inline-block w-2 h-2 rounded-full bg-pink-400 animate-pulse"></span>
            Video Testimonials & Wall of Love • Horizon 62
          </div>
          <h1 class="text-3xl font-extrabold text-white tracking-tight mt-1">
            Autonomous Social Proof & Video Testimonial Mesh
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Trigger-based video/audio collector links, AI soundbite synthesis & embeddable Schema.org Review rich snippets.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="requestNewTestimonial()"
            data-testid="request-testimonial-btn"
            class="px-4 py-2 bg-pink-600 hover:bg-pink-500 text-white rounded-lg text-sm font-semibold transition-colors flex items-center gap-2 shadow-lg shadow-pink-600/20"
          >
            <span>📹</span> Request Video Testimonial
          </button>
        </div>
      </div>

      <!-- KPI Overview Stats -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Approved Testimonials</div>
          <div class="text-2xl font-bold text-white mt-1" data-testid="stat-approved-count">
            {{ approvedCount() }} / {{ items().length }}
          </div>
          <div class="text-pink-400 text-xs mt-1">Wall of Love Ready</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Average Rating</div>
          <div class="text-2xl font-bold text-amber-400 mt-1" data-testid="stat-avg-rating">
            {{ avgRating() }} ★
          </div>
          <div class="text-emerald-400 text-xs mt-1">100% Positive Sentiment</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">High-Converting Video Reviews</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1" data-testid="stat-video-count">
            {{ videoCount() }}
          </div>
          <div class="text-slate-400 text-xs mt-1">WebRTC In-Browser HD Captures</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Conversion Lift</div>
          <div class="text-2xl font-bold text-indigo-400 mt-1">+28.5%</div>
          <div class="text-slate-400 text-xs mt-1">Checkout & Landing Page Impact</div>
        </div>
      </div>

      <!-- Main Stage: Testimonial Cards & Wall of Love Widget Publisher -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <!-- Testimonial Grid (7 cols) -->
        <div class="lg:col-span-7 bg-slate-800/80 border border-slate-700/60 rounded-xl overflow-hidden">
          <div class="p-4 border-b border-slate-700/60 flex items-center justify-between">
            <h2 class="text-lg font-bold text-white">Submitted Customer Endorsements</h2>
            <span class="text-xs text-slate-400 font-mono">{{ items().length }} Submissions</span>
          </div>

          <div class="divide-y divide-slate-700/40">
            @for (item of items(); track item.id) {
              <div
                class="p-4 hover:bg-slate-700/30 transition-colors flex flex-col md:flex-row md:items-center justify-between gap-3"
                [attr.data-testid]="'testimonial-card-' + item.id"
              >
                <div>
                  <div class="flex items-center gap-2">
                    <span class="font-bold text-white text-sm">{{ item.name }}</span>
                    <span class="bg-pink-900/60 text-pink-300 border border-pink-700/60 px-2 py-0.5 rounded text-[10px] font-semibold">
                      {{ item.format }}
                    </span>
                    <span class="text-amber-400 text-xs">★ {{ item.stars }}.0</span>
                  </div>
                  <div class="text-xs text-slate-400 mt-1">
                    {{ item.title }} • <strong class="text-slate-200">{{ item.company }}</strong>
                  </div>
                  <p class="text-xs text-slate-300 mt-1.5 italic border-l-2 border-pink-500 pl-2">
                    "{{ item.soundbite }}"
                  </p>
                </div>

                <div class="text-right self-end md:self-auto space-y-1">
                  <span
                    [class]="item.isApproved ? 'bg-emerald-900/60 text-emerald-300 border-emerald-700/60' : 'bg-slate-800 text-slate-400 border-slate-700'"
                    class="px-2 py-0.5 rounded text-[10px] font-semibold border inline-block"
                  >
                    {{ item.isApproved ? 'Published' : 'Under Review' }}
                  </span>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Wall of Love Widget Embed Publisher (5 cols) -->
        <div class="lg:col-span-5 bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 space-y-4">
          <div class="flex items-center justify-between border-b border-slate-700/60 pb-3">
            <div>
              <h3 class="font-bold text-white text-base">Embeddable Wall of Love</h3>
              <p class="text-xs text-slate-400">Schema.org Review Microdata</p>
            </div>
            <span class="text-xs px-2.5 py-1 rounded bg-pink-950 text-pink-300 border border-pink-800">
              Live Snippet
            </span>
          </div>

          <div class="p-3 bg-slate-900 rounded-xl border border-slate-800 space-y-2 text-xs">
            <div class="text-slate-400 font-bold">Generated Widget Embed Code:</div>
            <pre class="p-2.5 bg-slate-950 rounded font-mono text-[10px] text-pink-300 overflow-x-auto leading-relaxed border border-slate-800">
&lt;div id="sgrowing-wall-of-love"&gt;&lt;/div&gt;
&lt;script src="https://cdn.sgrowing.io/widgets/wall-of-love.js" defer&gt;&lt;/script&gt;</pre
            >
          </div>

          <button
            (click)="publishWidget()"
            data-testid="publish-widget-btn"
            class="w-full py-2 bg-pink-600 hover:bg-pink-500 text-white font-semibold rounded-lg text-xs shadow transition-colors"
          >
            Publish Live Wall of Love
          </button>

          @if (widgetSnippet(); as snip) {
            <div class="p-3 bg-slate-900 rounded-xl border border-emerald-900/60 text-emerald-300 text-xs" data-testid="widget-preview-box">
              {{ snip }}
            </div>
          }
        </div>
      </div>
    </div>
  `
})
export class SocialProofCockpitComponent {
  readonly items = signal<TestimonialItemView[]>([
    {
      id: 'tst_01',
      name: 'Victoria Sterling',
      title: 'Chief Revenue Officer',
      company: 'Apex Logistics',
      domain: 'apexlogistics.io',
      format: 'VideoRecording',
      stars: 5,
      soundbite: 'Doubled our pipeline conversion rate in our very first quarter.',
      fullQuote: 'Growth OS reduced our lead response latency from 45 minutes to 4 seconds, doubling our pipeline conversion rate in our very first quarter.',
      sentiment: 0.98,
      isApproved: true
    },
    {
      id: 'tst_02',
      name: 'Marcus Thorne',
      title: 'VP of Digital Marketing',
      company: 'CyberDyn Global',
      domain: 'cyberdyn.com',
      format: 'RichTextReview',
      stars: 5,
      soundbite: 'Saved us over $42,000 in monthly ad waste while maintaining 94% win-rates.',
      fullQuote: 'The programmatic bid shading and WhatsApp flow integration saved us over $42,000 in monthly ad waste while maintaining 94% win-rates.',
      sentiment: 0.96,
      isApproved: true
    }
  ]);

  readonly widgetSnippet = signal<string | null>(null);

  readonly approvedCount = computed(() =>
    this.items().filter(i => i.isApproved).length
  );

  readonly avgRating = computed(() => {
    const list = this.items();
    if (!list.length) return 0;
    return (list.reduce((acc, i) => acc + i.stars, 0) / list.length).toFixed(1);
  });

  readonly videoCount = computed(() =>
    this.items().filter(i => i.format === 'VideoRecording').length
  );

  requestNewTestimonial(): void {
    const newItem: TestimonialItemView = {
      id: `tst_${Date.now()}`,
      name: 'Elena Rostova',
      title: 'Head of E-Commerce',
      company: 'Nordic Retail Group',
      domain: 'nordicretail.se',
      format: 'VideoRecording',
      stars: 5,
      soundbite: 'Recovered 38% of high-value carts without sacrificing our brand margins.',
      fullQuote: 'Recovered 38% of high-value carts without sacrificing our brand margins using dynamic incentive bundling.',
      sentiment: 0.99,
      isApproved: true
    };

    this.items.update(list => [newItem, ...list]);
  }

  publishWidget(): void {
    this.widgetSnippet.set(
      '✓ Published: Wall of Love widget is live. Schema.org aggregate rating (4.95 ★) deployed with 3 verified customer reviews.'
    );
  }
}
