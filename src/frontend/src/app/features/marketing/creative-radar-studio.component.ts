import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import {
  UgcVideoScriptDto,
  UgcScriptSceneDto,
  CompetitorAdRecordDto,
  CompetitorPriceTrackerDto,
  CompetitorRadarSummaryDto
} from '../../core/models/horizons-advanced.models';

@Component({
  selector: 'app-creative-radar-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="h-full flex flex-col bg-slate-950 text-slate-100 overflow-y-auto font-sans">
      <!-- Topbar Header -->
      <div class="p-6 border-b border-slate-800 bg-slate-900/40 backdrop-blur flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div class="flex items-center gap-3">
          <a routerLink="/" class="text-xs text-slate-400 hover:text-white px-3 py-1.5 rounded-lg bg-slate-900 border border-slate-800 transition">
            ← Inbox
          </a>
          <div>
            <div class="flex items-center gap-2">
              <span class="text-2xl">🎬</span>
              <h1 class="text-xl font-bold tracking-tight text-white">Autonomous Creative Studio & Competitor Radar</h1>
              <span class="px-2.5 py-0.5 rounded-full text-xs font-mono bg-purple-500/10 text-purple-400 border border-purple-500/20">
                Horizon 3 • AI UGC & Meta Intelligence
              </span>
            </div>
            <p class="text-xs text-slate-400 mt-1">
              Generate 5 psychological video hooks & Darija/Arabic/French UGC scripts, monitor Meta ad library campaigns, and track real-time price disparities.
            </p>
          </div>
        </div>

        <!-- Navigation Tabs -->
        <div class="flex items-center gap-1 bg-slate-900 p-1 rounded-xl border border-slate-800 text-xs">
          <button
            (click)="activeTab.set('ugc')"
            [class.bg-purple-600]="activeTab() === 'ugc'"
            [class.text-white]="activeTab() === 'ugc'"
            class="px-3.5 py-1.5 rounded-lg text-slate-400 hover:text-white transition font-medium">
            🎥 UGC Script Generator
          </button>
          <button
            (click)="activeTab.set('competitor-ads')"
            [class.bg-purple-600]="activeTab() === 'competitor-ads'"
            [class.text-white]="activeTab() === 'competitor-ads'"
            class="px-3.5 py-1.5 rounded-lg text-slate-400 hover:text-white transition font-medium">
            👁️ Competitor Ad Library
          </button>
          <button
            (click)="activeTab.set('price-radar')"
            [class.bg-purple-600]="activeTab() === 'price-radar'"
            [class.text-white]="activeTab() === 'price-radar'"
            class="px-3.5 py-1.5 rounded-lg text-slate-400 hover:text-white transition font-medium">
            🎯 Price Disparity Radar
          </button>
        </div>
      </div>

      <!-- Main Body -->
      <div class="p-6 space-y-6 flex-1">
        <!-- 1. UGC Script Generator Tab -->
        @if (activeTab() === 'ugc') {
          <div class="space-y-6">
            <!-- Product Parameters Card -->
            <div class="p-5 rounded-2xl bg-slate-900/60 border border-slate-800 space-y-4">
              <div class="flex items-center justify-between">
                <div>
                  <h3 class="text-sm font-bold text-white">Generate 5 Multi-Angle UGC Video Scripts</h3>
                  <p class="text-xs text-slate-400">Generates Problem-Agitator, Competitor Callout, Cultural COD Reassurance, Unboxing ASMR, and Scarcity Deals.</p>
                </div>
                <button
                  (click)="generateScripts()"
                  [disabled]="isGenerating()"
                  class="px-4 py-2 bg-purple-600 hover:bg-purple-500 disabled:opacity-50 text-white rounded-lg text-xs font-bold transition shadow-lg shadow-purple-600/20 flex items-center gap-2">
                  <span>✨</span>
                  <span>{{ isGenerating() ? 'Generating Scripts...' : 'Generate 5 UGC Angles' }}</span>
                </button>
              </div>

              <div class="grid grid-cols-1 md:grid-cols-4 gap-4 text-xs">
                <div>
                  <label class="text-slate-400 block mb-1 font-medium">Product SKU</label>
                  <input [(ngModel)]="targetSku" class="w-full bg-slate-950 border border-slate-800 rounded-lg p-2 text-white font-mono" />
                </div>
                <div>
                  <label class="text-slate-400 block mb-1 font-medium">Product Title</label>
                  <input [(ngModel)]="targetProductName" class="w-full bg-slate-950 border border-slate-800 rounded-lg p-2 text-white" />
                </div>
                <div>
                  <label class="text-slate-400 block mb-1 font-medium">Language / Dialect</label>
                  <select [(ngModel)]="targetLanguage" class="w-full bg-slate-950 border border-slate-800 rounded-lg p-2 text-white">
                    <option value="Darija">Darija (Maghreb Dialect)</option>
                    <option value="Arabic">Modern Standard Arabic</option>
                    <option value="French">French</option>
                  </select>
                </div>
                <div>
                  <label class="text-slate-400 block mb-1 font-medium">Target Demographic</label>
                  <input [(ngModel)]="targetAudience" class="w-full bg-slate-950 border border-slate-800 rounded-lg p-2 text-white" />
                </div>
              </div>
            </div>

            <!-- Scripts Display -->
            <div class="grid grid-cols-1 gap-6">
              @for (script of scripts(); track script.id) {
                <div class="p-5 rounded-2xl bg-slate-900/40 border border-slate-800 space-y-4 hover:border-purple-500/40 transition">
                  <div class="flex items-center justify-between">
                    <div class="flex items-center gap-3">
                      <span class="px-2.5 py-1 rounded-full text-xs font-mono font-bold bg-purple-500/20 text-purple-400 border border-purple-500/30">
                        {{ getAngleLabel(script.hookAngle) }}
                      </span>
                      <h4 class="text-sm font-bold text-white">{{ script.productName }}</h4>
                      <span class="text-xs text-slate-400 font-mono">⏱️ {{ script.estimatedDurationSeconds }}s</span>
                    </div>

                    <button
                      (click)="copyScriptText(script)"
                      class="px-3 py-1 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 text-xs font-medium transition">
                      📋 Copy Script
                    </button>
                  </div>

                  <!-- Hook Quote -->
                  <div class="p-3 bg-purple-950/20 rounded-xl border border-purple-500/20 text-xs">
                    <span class="text-[10px] text-purple-400 font-mono uppercase font-bold block mb-1">🎣 3-Second Scroll-Stopper Hook</span>
                    <p class="text-slate-200 font-medium italic">"{{ script.hookText }}"</p>
                  </div>

                  <!-- Scene Breakdown -->
                  <div class="space-y-2">
                    <span class="text-[11px] text-slate-400 font-mono uppercase font-semibold">Scene Breakdown (Shooting Storyboard)</span>
                    <div class="grid grid-cols-1 md:grid-cols-4 gap-3">
                      @for (sc of parseScenes(script.scenesJson); track sc.sceneNumber) {
                        <div class="p-3 rounded-xl bg-slate-950/70 border border-slate-800/80 space-y-2 text-xs">
                          <div class="flex items-center justify-between text-[10px] font-mono">
                            <span class="text-purple-400 font-bold">Scene {{ sc.sceneNumber }}</span>
                            <span class="text-slate-500">{{ sc.durationSeconds }}s</span>
                          </div>
                          <div>
                            <span class="text-slate-400 text-[10px] block">Visual Cue</span>
                            <p class="text-slate-300 text-[11px]">{{ sc.visualCue }}</p>
                          </div>
                          <div>
                            <span class="text-slate-400 text-[10px] block">Voiceover ({{ script.language }})</span>
                            <p class="text-emerald-400 text-[11px] font-medium leading-relaxed">"{{ sc.voiceoverText }}"</p>
                          </div>
                          <div class="text-[10px] text-amber-400 font-mono">
                            Overlay: [{{ sc.onscreenText }}]
                          </div>
                        </div>
                      }
                    </div>
                  </div>

                  <!-- CTA Footer -->
                  <div class="flex items-center justify-between pt-2 border-t border-slate-800/60 text-xs text-slate-400">
                    <span>Call To Action: <strong class="text-white">{{ script.callToAction }}</strong></span>
                    <span class="text-[10px] font-mono text-slate-500">Created: {{ script.createdAtUtc | date:'medium' }}</span>
                  </div>
                </div>
              }
            </div>
          </div>
        }

        <!-- 2. Competitor Ad Library Tab -->
        @if (activeTab() === 'competitor-ads') {
          <div class="space-y-6">
            <div class="p-5 rounded-2xl bg-slate-900/60 border border-slate-800 flex flex-col md:flex-row md:items-center justify-between gap-4">
              <div>
                <h3 class="text-sm font-bold text-white">Competitor Meta & TikTok Ad Intelligence</h3>
                <p class="text-xs text-slate-400">Track active competitor campaigns, ad spend estimates, and threat levels.</p>
              </div>
              <div class="flex items-center gap-3">
                <input [(ngModel)]="newCompetitorName" placeholder="Competitor (e.g. TrendyStore TN)" class="px-3 py-2 bg-slate-950 border border-slate-800 rounded-lg text-xs text-slate-200" />
                <button
                  (click)="ingestAd()"
                  class="px-4 py-2 bg-purple-600 hover:bg-purple-500 text-white rounded-lg text-xs font-bold transition">
                  Ingest Campaign
                </button>
              </div>
            </div>

            <!-- Ad Cards Table -->
            <div class="rounded-2xl border border-slate-800 overflow-hidden bg-slate-900/40">
              <table class="w-full text-left text-xs">
                <thead class="bg-slate-900/80 text-slate-400 uppercase font-mono border-b border-slate-800">
                  <tr>
                    <th class="px-4 py-3">Competitor</th>
                    <th class="px-4 py-3">Platform</th>
                    <th class="px-4 py-3">Hook Snippet</th>
                    <th class="px-4 py-3">Offer Details</th>
                    <th class="px-4 py-3">Est. Daily Spend</th>
                    <th class="px-4 py-3">Threat Level</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800/60 font-mono">
                  @for (ad of competitorAds(); track ad.id) {
                    <tr class="hover:bg-slate-900/40">
                      <td class="px-4 py-3 text-white font-sans font-bold">{{ ad.competitorName }}</td>
                      <td class="px-4 py-3">
                        <span class="px-2 py-0.5 rounded text-[10px] font-bold"
                          [ngClass]="ad.platform === 'Meta' ? 'bg-blue-500/20 text-blue-400' : 'bg-pink-500/20 text-pink-400'">
                          {{ ad.platform }}
                        </span>
                      </td>
                      <td class="px-4 py-3 text-slate-300 font-sans italic max-w-xs truncate">{{ ad.hookSnippet }}</td>
                      <td class="px-4 py-3 text-amber-400 font-sans">{{ ad.offerDetails }}</td>
                      <td class="px-4 py-3 text-emerald-400">\${{ ad.estimatedDailySpend }} / day</td>
                      <td class="px-4 py-3">
                        <span class="px-2 py-0.5 rounded-full text-[10px] font-bold"
                          [ngClass]="{
                            'bg-rose-500/20 text-rose-400 border border-rose-500/30': ad.threatLevel === 'High',
                            'bg-amber-500/20 text-amber-400 border border-amber-500/30': ad.threatLevel === 'Medium',
                            'bg-slate-500/20 text-slate-400': ad.threatLevel === 'Low'
                          }">
                          {{ ad.threatLevel }}
                        </span>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }

        <!-- 3. Price Disparity Radar Tab -->
        @if (activeTab() === 'price-radar') {
          <div class="space-y-6">
            <!-- Summary Stats -->
            <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
              <div class="p-5 rounded-2xl bg-slate-900/60 border border-slate-800 space-y-1">
                <span class="text-xs text-slate-400 font-mono uppercase">Monitored SKU Prices</span>
                <div class="text-2xl font-bold font-mono text-white">{{ priceAlerts().length }}</div>
                <span class="text-[11px] text-slate-500">Cross-store scraping active</span>
              </div>
              <div class="p-5 rounded-2xl bg-slate-900/60 border border-rose-500/30 space-y-1">
                <span class="text-xs text-rose-400 font-mono uppercase">Price Deficits (Under-cut)</span>
                <div class="text-2xl font-bold font-mono text-rose-400">
                  {{ countDeficitSkus() }}
                </div>
                <span class="text-[11px] text-slate-500">Competitors selling cheaper</span>
              </div>
              <div class="p-5 rounded-2xl bg-slate-900/60 border border-emerald-500/30 space-y-1">
                <span class="text-xs text-emerald-400 font-mono uppercase">Healthy Margin SKUs</span>
                <div class="text-2xl font-bold font-mono text-emerald-400">
                  {{ priceAlerts().length - countDeficitSkus() }}
                </div>
                <span class="text-[11px] text-slate-500">Superior value positioning</span>
              </div>
            </div>

            <!-- Disparity Table -->
            <div class="rounded-2xl border border-slate-800 overflow-hidden bg-slate-900/40">
              <table class="w-full text-left text-xs">
                <thead class="bg-slate-900/80 text-slate-400 uppercase font-mono border-b border-slate-800">
                  <tr>
                    <th class="px-4 py-3">SKU</th>
                    <th class="px-4 py-3">Competitor Store</th>
                    <th class="px-4 py-3">Their Price</th>
                    <th class="px-4 py-3">Our Price</th>
                    <th class="px-4 py-3">Price Gap</th>
                    <th class="px-4 py-3">AI Counter-Offer Recommendation</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800/60 font-mono">
                  @for (item of priceAlerts(); track item.id) {
                    <tr class="hover:bg-slate-900/40">
                      <td class="px-4 py-3 text-cyan-400 font-bold">{{ item.targetSku }}</td>
                      <td class="px-4 py-3 text-white font-sans">{{ item.competitorName }}</td>
                      <td class="px-4 py-3 text-rose-400 font-bold">{{ item.competitorPrice | number:'1.2-2' }} TND</td>
                      <td class="px-4 py-3 text-emerald-400 font-bold">{{ item.ourPrice | number:'1.2-2' }} TND</td>
                      <td class="px-4 py-3">
                        <span class="px-2 py-0.5 rounded text-[10px] font-bold"
                          [ngClass]="item.priceDifferencePercentage < 0 ? 'bg-rose-500/20 text-rose-400' : 'bg-emerald-500/20 text-emerald-400'">
                          {{ item.priceDifferencePercentage > 0 ? '+' : '' }}{{ item.priceDifferencePercentage | number:'1.1-1' }}%
                        </span>
                      </td>
                      <td class="px-4 py-3 font-sans">
                        <span class="px-2.5 py-1 rounded-lg text-[11px] font-medium bg-amber-500/10 text-amber-300 border border-amber-500/20 block">
                          ⚡ {{ item.recommendedCounterAction }}
                        </span>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }
      </div>
    </div>
  `
})
export class CreativeRadarStudioComponent implements OnInit {
  private api = inject(ApiService);
  private auth = inject(AuthService);

  public tenantId = this.auth.currentTenant()?.id || 'a0000000-0000-0000-0000-000000000001';
  public activeTab = signal<'ugc' | 'competitor-ads' | 'price-radar'>('ugc');

  public targetSku = 'EAR-PRO-ANC';
  public targetProductName = 'Wireless ANC Earbuds Pro';
  public targetLanguage = 'Darija';
  public targetAudience = 'Shoppers 18-35 in Grand Tunis & Sahel';

  public isGenerating = signal<boolean>(false);
  public scripts = signal<UgcVideoScriptDto[]>([]);
  public competitorAds = signal<CompetitorAdRecordDto[]>([]);
  public priceAlerts = signal<CompetitorPriceTrackerDto[]>([]);

  public newCompetitorName = 'ElectroPlus TN';

  ngOnInit(): void {
    this.loadData();
  }

  public loadData(): void {
    // Generate initial scripts if empty
    this.api.getUgcScripts(this.tenantId, this.targetSku).subscribe({
      next: data => {
        if (data && data.length > 0) {
          this.scripts.set(data);
        } else {
          this.generateScripts();
        }
      },
      error: () => this.generateScripts()
    });

    // Load radar
    this.api.getCompetitorRadar(this.tenantId).subscribe({
      next: summary => {
        if (summary) {
          this.competitorAds.set(summary.activeCompetitorCampaigns || []);
          this.priceAlerts.set(summary.priceAlerts || []);
        } else {
          this.fallbackCompetitorData();
        }
      },
      error: () => this.fallbackCompetitorData()
    });
  }

  public generateScripts(): void {
    this.isGenerating.set(true);
    this.api.generateUgcScripts({
      tenantId: this.tenantId,
      sku: this.targetSku,
      productName: this.targetProductName,
      language: this.targetLanguage,
      targetAudience: this.targetAudience
    }).subscribe({
      next: data => {
        this.scripts.set(data);
        this.isGenerating.set(false);
      },
      error: () => {
        this.isGenerating.set(false);
        this.fallbackScripts();
      }
    });
  }

  private fallbackScripts(): void {
    const scenes: UgcScriptSceneDto[] = [
      { sceneNumber: 1, visualCue: 'Extreme close up of broken cheap plastic headphones', creatorAction: 'Frustrated sigh looking at camera', voiceoverText: 'T3ebt mel les écouteurs li ymotou fi jem3a?', onscreenText: 'Stop throwing away your money 💸', durationSeconds: 3 },
      { sceneNumber: 2, visualCue: 'Unbox sleek metallic magnetic case with satisfying click', creatorAction: 'Opens box and shows build quality', voiceoverText: 'Chouf el finition metal w ANC el 7a9i9i.', onscreenText: 'Active Noise Cancellation 🔇', durationSeconds: 5 },
      { sceneNumber: 3, visualCue: 'Delivery courier handing parcel at doorstep with cash payment', creatorAction: 'Inspects product before paying driver', voiceoverText: 'W a7sen 7aja: khallas ki touslek leddar ma3 Yalidine w Aramex.', onscreenText: 'Paiement à la livraison 100% Garanti 🚚', durationSeconds: 4 },
      { sceneNumber: 4, visualCue: 'Call to action button on phone screen', creatorAction: 'Clicks order button', voiceoverText: 'Enzel 3ala Commande taw w a5let 3al remise.', onscreenText: 'Commander Maintenant -50% 🔥', durationSeconds: 3 }
    ];

    this.scripts.set([
      {
        id: 'sc-1',
        tenantId: this.tenantId,
        productSku: this.targetSku,
        productName: this.targetProductName,
        language: 'Darija',
        targetAudience: this.targetAudience,
        hookAngle: 1,
        hookText: 'T3ebt mel les écouteurs li ymotou fi jem3a? Chouf el 7all.',
        scenesJson: JSON.stringify(scenes),
        callToAction: 'Enzel 3ala Commande taw w a5let 3al remise.',
        estimatedDurationSeconds: 15,
        createdAtUtc: new Date().toISOString()
      },
      {
        id: 'sc-2',
        tenantId: this.tenantId,
        productSku: this.targetSku,
        productName: this.targetProductName,
        language: 'Darija',
        targetAudience: this.targetAudience,
        hookAngle: 3,
        hookText: 'Khallas ki touslek leddar — 100% garanti avec Yalidine & Aramex.',
        scenesJson: JSON.stringify(scenes),
        callToAction: 'Chouf el avis w commander.',
        estimatedDurationSeconds: 15,
        createdAtUtc: new Date().toISOString()
      }
    ]);
  }

  private fallbackCompetitorData(): void {
    this.competitorAds.set([
      {
        id: 'cad-1',
        tenantId: this.tenantId,
        competitorName: 'BazarExpress TN',
        platform: 'Meta',
        hookSnippet: 'Ecouteurs sans fil pas cher livraison gratuite',
        offerDetails: 'Buy 1 Get 1 Free for 79 TND',
        estimatedDailySpend: 150,
        threatLevel: 'High',
        isActive: true,
        firstSeenAtUtc: new Date().toISOString(),
        lastSeenAtUtc: new Date().toISOString()
      },
      {
        id: 'cad-2',
        tenantId: this.tenantId,
        competitorName: 'TunisieTech Store',
        platform: 'TikTok',
        hookSnippet: 'Unboxing wireless earbuds with active noise cancel',
        offerDetails: '95 TND + 7 TND shipping',
        estimatedDailySpend: 80,
        threatLevel: 'Medium',
        isActive: true,
        firstSeenAtUtc: new Date().toISOString(),
        lastSeenAtUtc: new Date().toISOString()
      }
    ]);

    this.priceAlerts.set([
      {
        id: 'cp-1',
        tenantId: this.tenantId,
        targetSku: 'EAR-PRO-ANC',
        competitorName: 'BazarExpress TN',
        competitorUrl: 'https://bazarexpress.tn/earbuds',
        competitorPrice: 79.0,
        ourPrice: 89.0,
        priceDifferencePercentage: -11.2,
        recommendedCounterAction: 'Counter with 2-pack bundle at 139 TND + Free Doorstep Delivery',
        lastCheckedAtUtc: new Date().toISOString()
      },
      {
        id: 'cp-2',
        tenantId: this.tenantId,
        targetSku: 'WATCH-ULTRA-S9',
        competitorName: 'SmartTN',
        competitorUrl: 'https://smart.tn/watch',
        competitorPrice: 165.0,
        ourPrice: 149.0,
        priceDifferencePercentage: 9.7,
        recommendedCounterAction: 'Highlight premium OLED screen and 2-year warranty badge',
        lastCheckedAtUtc: new Date().toISOString()
      }
    ]);
  }

  public getAngleLabel(angle: number): string {
    switch (angle) {
      case 1: return 'Angle 1: Problem-Agitator';
      case 2: return 'Angle 2: Competitor Callout';
      case 3: return 'Angle 3: Cultural COD Trust';
      case 4: return 'Angle 4: ASMR Sensory Unboxing';
      case 5: return 'Angle 5: Scarcity Flash Deal';
      default: return 'UGC Video Script';
    }
  }

  public parseScenes(scenesJson: string): UgcScriptSceneDto[] {
    try {
      return JSON.parse(scenesJson);
    } catch {
      return [];
    }
  }

  public countDeficitSkus(): number {
    return this.priceAlerts().filter(p => p.priceDifferencePercentage < 0).length;
  }

  public copyScriptText(script: UgcVideoScriptDto): void {
    const scenes = this.parseScenes(script.scenesJson);
    let full = `=== ${script.productName} UGC SCRIPT (${this.getAngleLabel(script.hookAngle)}) ===\n`;
    full += `HOOK: ${script.hookText}\n\n`;
    scenes.forEach(s => {
      full += `[Scene ${s.sceneNumber} - ${s.durationSeconds}s]\nVisual: ${s.visualCue}\nVoiceover: ${s.voiceoverText}\nOverlay: ${s.onscreenText}\n\n`;
    });
    full += `CTA: ${script.callToAction}`;

    navigator.clipboard?.writeText(full);
    alert('UGC Script copied to clipboard!');
  }

  public ingestAd(): void {
    this.api.ingestCompetitorAd({
      tenantId: this.tenantId,
      competitorName: this.newCompetitorName,
      platform: 'Meta',
      hookSnippet: 'Flash sale 48h only with cash on delivery',
      offerDetails: 'Free case included',
      estimatedDailySpend: 120,
      threatLevel: 'Medium'
    }).subscribe(ad => {
      this.competitorAds.update(prev => [ad, ...prev]);
      alert(`Competitor campaign for ${ad.competitorName} ingested into Radar.`);
    });
  }
}
