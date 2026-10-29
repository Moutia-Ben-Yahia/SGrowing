import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import {
  SocialPostDto,
  SocialPlatform,
  SocialAccountDto,
  ViralHookSuggestionDto,
  PlatformAdaptedPostDto,
  TokenHealthAuditReportDto
} from '../../core/models/social.models';

export interface SocialAccountUI {
  id: string;
  platform: SocialPlatform;
  platformKey: string;
  name: string;
  handle: string;
  avatarIcon: string;
  charLimit: number;
  aspectRatio: string;
  selected: boolean;
  tokenExpiringSoon?: boolean;
}

export interface PublicCommentUI {
  id: string;
  platform: SocialPlatform;
  platformName: string;
  author: string;
  postSnippet: string;
  commentText: string;
  deflected: boolean;
}

@Component({
  selector: 'app-social-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen bg-slate-950 text-slate-100 font-sans overflow-hidden">
      <!-- Main Content -->
      <div class="flex-1 flex flex-col h-full overflow-hidden">
        <!-- Header -->
        <header class="h-16 border-b border-slate-800 bg-slate-900/50 backdrop-blur px-8 flex items-center justify-between shrink-0">
          <div class="flex items-center gap-3">
            <a routerLink="/" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← Inbox
            </a>
            <span class="text-slate-700">|</span>
            <div class="w-8 h-8 rounded-lg bg-gradient-to-tr from-fuchsia-600 to-indigo-600 text-white flex items-center justify-center font-bold text-sm shadow-lg shadow-fuchsia-900/30">
              #
            </div>
            <div>
              <h1 class="text-lg font-semibold text-white tracking-tight">Social Media Multi-Publishing Studio</h1>
              <p class="text-xs text-slate-400">Omnichannel publishing matrix, AI viral hooks & token health auditor</p>
            </div>
          </div>
          <div class="flex items-center gap-3">
            <!-- Token Health Status Pill -->
            <button
              (click)="runTokenAudit()"
              [disabled]="isAuditingTokens()"
              class="px-3 py-1.5 rounded-full text-xs font-medium border flex items-center gap-1.5 transition"
              [class]="hasTokenAlerts() 
                ? 'bg-rose-500/10 text-rose-400 border-rose-500/30 hover:bg-rose-500/20' 
                : 'bg-emerald-500/10 text-emerald-400 border-emerald-500/20 hover:bg-emerald-500/20'">
              <span class="w-2 h-2 rounded-full" [class]="hasTokenAlerts() ? 'bg-rose-400 animate-ping' : 'bg-emerald-400'"></span>
              <span>{{ tokenStatusText() }}</span>
            </button>

            <button 
              (click)="triggerPublishDue()"
              [disabled]="isPublishing()"
              class="px-3 py-1.5 rounded-lg text-xs bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 transition">
              ⚡ Trigger Due Posts
            </button>
          </div>
        </header>

        <!-- Notification Banner -->
        @if (feedbackMessage()) {
          <div class="bg-indigo-600/20 border-b border-indigo-500/30 px-8 py-2.5 text-xs text-indigo-300 flex items-center justify-between">
            <span>{{ feedbackMessage() }}</span>
            <button (click)="feedbackMessage.set(null)" class="text-slate-400 hover:text-white">✕</button>
          </div>
        }

        <!-- Body -->
        <div class="flex-1 overflow-y-auto p-8 space-y-8">
          
          <!-- Viral Hook Generator & Selector Bar -->
          <div class="bg-gradient-to-r from-slate-900 via-indigo-950/40 to-slate-900 border border-indigo-900/40 rounded-xl p-5 shadow-xl space-y-4">
            <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
              <div>
                <h3 class="text-sm font-semibold text-white flex items-center gap-2">
                  <span class="text-indigo-400">⚡</span>
                  Viral Hook Engine (Multi-Angle Generator)
                </h3>
                <p class="text-xs text-slate-400">Generate high-virality hooks (Contrarian, Statistical, Question, Story) and inject them directly into your draft.</p>
              </div>
              <div class="flex items-center gap-2">
                <input 
                  [(ngModel)]="hookTopic" 
                  placeholder="e.g. B2B SaaS Growth, AI SDR..." 
                  class="bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-slate-200 placeholder-slate-500 focus:outline-none focus:border-indigo-500" />
                <button 
                  (click)="generateHooks()"
                  [disabled]="isGeneratingHooks()"
                  class="px-3 py-1.5 rounded-lg text-xs font-medium bg-indigo-600 hover:bg-indigo-500 disabled:opacity-40 text-white transition shadow-sm">
                  {{ isGeneratingHooks() ? 'Generating...' : '✨ Generate Hooks' }}
                </button>
              </div>
            </div>

            <!-- Hook Suggestions Chips -->
            <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-3">
              @for (h of viralHooks(); track h.hookText) {
                <div class="p-3 rounded-lg bg-slate-950/80 border border-slate-800 hover:border-indigo-500/50 transition flex flex-col justify-between space-y-2 group">
                  <div class="space-y-1">
                    <div class="flex items-center justify-between">
                      <span class="text-[10px] font-semibold px-2 py-0.5 rounded-full uppercase tracking-wider"
                        [class]="getAngleBadgeClass(h.angle)">
                        {{ h.angle }}
                      </span>
                      <span class="text-[10px] font-mono text-emerald-400 font-medium">
                        🔥 {{ h.estimatedViralityScore }}% virality
                      </span>
                    </div>
                    <p class="text-xs text-slate-300 line-clamp-3 leading-snug">"{{ h.hookText }}"</p>
                  </div>
                  <button 
                    (click)="injectHook(h.hookText)"
                    class="w-full text-[11px] font-medium py-1 rounded bg-slate-900 group-hover:bg-indigo-600 text-slate-300 group-hover:text-white border border-slate-800 group-hover:border-indigo-500 transition text-center">
                    + Inject Hook into Post
                  </button>
                </div>
              }
            </div>
          </div>

          <!-- Main Workspace: Composer (Left) + Multi-Platform Matrix (Right) -->
          <div class="grid grid-cols-1 lg:grid-cols-12 gap-8">
            <!-- Composer (6 cols) -->
            <div class="lg:col-span-6 bg-slate-900 border border-slate-800 rounded-xl p-6 shadow-xl space-y-5">
              <div class="flex items-center justify-between">
                <h2 class="text-base font-semibold text-white">Create Multi-Channel Post</h2>
                <button
                  (click)="autoAdaptWithAi()"
                  [disabled]="isAdaptingWithAi() || !postText.trim()"
                  class="text-xs px-2.5 py-1 rounded-md bg-indigo-500/10 hover:bg-indigo-500/20 text-indigo-300 border border-indigo-500/30 transition flex items-center gap-1">
                  <span>{{ isAdaptingWithAi() ? 'Adapting...' : '🪄 AI Re-Adapt All Channels' }}</span>
                </button>
              </div>

              <!-- Network Selector -->
              <div>
                <label class="block text-xs font-medium text-slate-400 mb-2">Target Networks</label>
                <div class="flex flex-wrap gap-2">
                  @for (acc of accounts(); track acc.id) {
                    <button 
                      (click)="toggleAccount(acc)"
                      [class]="acc.selected ? 'bg-indigo-600 text-white border-indigo-500 shadow-md shadow-indigo-900/30' : 'bg-slate-950 text-slate-400 border-slate-800 hover:border-slate-700'"
                      class="px-3 py-1.5 rounded-lg text-xs font-medium border flex items-center gap-1.5 transition">
                      <span>{{ acc.avatarIcon }}</span>
                      <span>{{ acc.name }}</span>
                      @if (acc.tokenExpiringSoon) {
                        <span class="w-1.5 h-1.5 rounded-full bg-rose-400" title="Token expires soon"></span>
                      }
                    </button>
                  }
                </div>
              </div>

              <!-- Content Textarea -->
              <div>
                <div class="flex items-center justify-between mb-1">
                  <label class="text-xs font-medium text-slate-400">Master Draft Content</label>
                  <span class="text-xs font-mono text-slate-400">Total Characters: <strong class="text-slate-100">{{ postText.length }}</strong></span>
                </div>
                <textarea 
                  [(ngModel)]="postText" 
                  rows="6" 
                  placeholder="Announce product updates, launch campaigns, or customer promotions across all social channels..."
                  class="w-full bg-slate-950 border border-slate-800 rounded-xl p-4 text-sm text-slate-100 placeholder-slate-500 focus:outline-none focus:border-indigo-500 transition leading-relaxed">
                </textarea>
              </div>

              <!-- Media URL & Aspect Ratio Preset -->
              <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1">Attached Media / CDN Asset URL</label>
                  <input 
                    [(ngModel)]="mediaUrl" 
                    placeholder="https://assets.sgrowing.com/banners/launch-2026.png" 
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-slate-200 focus:outline-none focus:border-indigo-500 font-mono" />
                </div>
                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1">Preferred Aspect Ratio</label>
                  <select 
                    [(ngModel)]="selectedAspectRatio"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-slate-200 focus:outline-none focus:border-indigo-500">
                    <option value="1:1 Square">1:1 Square (Feed Standard)</option>
                    <option value="9:16 Vertical Reel/Story">9:16 Vertical Reel/Story (TikTok & IG)</option>
                  </select>
                </div>
              </div>

              <!-- Action Buttons -->
              <div class="flex items-center gap-3 pt-2">
                <button 
                  (click)="publishNow()" 
                  [disabled]="!postText.trim() || isPublishing()"
                  class="flex-1 bg-gradient-to-r from-indigo-600 to-fuchsia-600 hover:from-indigo-500 hover:to-fuchsia-500 disabled:opacity-40 text-white font-medium py-2.5 rounded-lg text-sm transition shadow-lg shadow-indigo-900/30 flex items-center justify-center gap-2">
                  <span>🚀</span>
                  <span>{{ isPublishing() ? 'Publishing...' : 'Publish to Target Networks' }}</span>
                </button>
                <button 
                  (click)="schedulePost()" 
                  [disabled]="!postText.trim() || isPublishing()"
                  class="bg-slate-800 hover:bg-slate-700 disabled:opacity-40 text-slate-200 font-medium px-5 py-2.5 rounded-lg text-sm transition border border-slate-700">
                  📅 Schedule
                </button>
              </div>

              <!-- Scheduled Posts Feed -->
              <div class="bg-slate-950/60 border border-slate-800 rounded-xl p-4 space-y-3 pt-4">
                <div class="flex items-center justify-between text-xs">
                  <span class="font-semibold text-white">Editorial Publishing Queue</span>
                  <span class="text-indigo-400 font-mono">{{ posts().length }} Posts</span>
                </div>
                <div class="space-y-2 max-h-40 overflow-y-auto pr-1">
                  @for (p of posts(); track p.id) {
                    <div class="p-2.5 rounded-lg bg-slate-900 border border-slate-800 flex items-center justify-between text-xs">
                      <div class="truncate mr-3 max-w-[240px]">
                        <p class="text-slate-200 truncate">{{ p.content }}</p>
                        <span class="text-[10px] text-slate-500">{{ p.createdAtUtc | date:'short' }}</span>
                      </div>
                      <span [class]="p.status === 3 ? 'text-emerald-400 bg-emerald-500/10' : 'text-amber-400 bg-amber-500/10'" class="px-2 py-0.5 rounded text-[10px] font-medium border border-current/20 shrink-0">
                        {{ p.status === 3 ? 'Published' : 'Scheduled' }}
                      </span>
                    </div>
                  } @empty {
                    <p class="text-xs text-slate-500 italic">No posts in queue yet. Create your first post above.</p>
                  }
                </div>
              </div>
            </div>

            <!-- Multi-Platform Live Preview Matrix (6 cols) -->
            <div class="lg:col-span-6 space-y-4">
              <!-- Matrix Header & Mode Switcher -->
              <div class="flex items-center justify-between">
                <div>
                  <h2 class="text-base font-semibold text-white flex items-center gap-2">
                    <span>Multi-Platform Live Preview Matrix</span>
                  </h2>
                  <p class="text-xs text-slate-400">Real-time character countdown, platform limits & aspect ratio rules</p>
                </div>
                <!-- View Mode Toggle -->
                <div class="flex items-center bg-slate-900 border border-slate-800 rounded-lg p-0.5 text-xs">
                  <button 
                    (click)="previewMode.set('tabbed')"
                    [class]="previewMode() === 'tabbed' ? 'bg-indigo-600 text-white shadow' : 'text-slate-400 hover:text-white'"
                    class="px-2.5 py-1 rounded font-medium transition">
                    Tabbed
                  </button>
                  <button 
                    (click)="previewMode.set('grid')"
                    [class]="previewMode() === 'grid' ? 'bg-indigo-600 text-white shadow' : 'text-slate-400 hover:text-white'"
                    class="px-2.5 py-1 rounded font-medium transition">
                    2x2 Grid
                  </button>
                </div>
              </div>

              <!-- TABBED VIEW -->
              @if (previewMode() === 'tabbed') {
                <div class="space-y-3">
                  <!-- Platform Switcher Tabs -->
                  <div class="flex items-center gap-1.5 border-b border-slate-800 pb-2 overflow-x-auto">
                    @for (channel of previewChannels; track channel.platform) {
                      <button 
                        (click)="activePreviewPlatform.set(channel.platform)"
                        [class]="activePreviewPlatform() === channel.platform 
                          ? 'bg-slate-800 text-white border-indigo-500' 
                          : 'bg-slate-900/50 text-slate-400 border-slate-800 hover:text-slate-200'"
                        class="px-3 py-1.5 rounded-lg text-xs font-medium border flex items-center gap-1.5 transition whitespace-nowrap">
                        <span>{{ channel.icon }}</span>
                        <span>{{ channel.name }}</span>
                        <!-- Status indicator dot -->
                        <span 
                          class="w-1.5 h-1.5 rounded-full"
                          [class]="isContentWithinLimit(channel.platform) ? 'bg-emerald-400' : 'bg-rose-400 animate-pulse'">
                        </span>
                      </button>
                    }
                  </div>

                  <!-- Active Platform Card Preview -->
                  @for (channel of previewChannels; track channel.platform) {
                    @if (activePreviewPlatform() === channel.platform) {
                      <div class="bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-xl space-y-4">
                        <!-- Channel Header & Limit Meter -->
                        <div class="flex flex-col sm:flex-row sm:items-center justify-between pb-3 border-b border-slate-800 gap-2">
                          <div class="flex items-center gap-2.5">
                            <span class="text-xl">{{ channel.icon }}</span>
                            <div>
                              <div class="font-semibold text-sm text-white flex items-center gap-1.5">
                                {{ channel.name }} Preview
                                <span class="text-indigo-400 text-xs">✓ Verified</span>
                              </div>
                              <div class="text-[11px] text-slate-400">
                                Aspect Ratio: <span class="text-slate-200 font-mono">{{ getPlatformAspectRatio(channel.platform) }}</span>
                                • Hashtags: <span class="text-indigo-300 font-medium">{{ channel.hashtagRule }}</span>
                              </div>
                            </div>
                          </div>

                          <!-- Live Character Countdown Meter -->
                          <div class="text-right">
                            <div class="text-xs font-mono font-medium flex items-center gap-1 justify-end"
                              [class]="getMeterColorClass(channel.platform)">
                              @if (getRemainingChars(channel.platform) >= 0) {
                                <span>● {{ getRemainingChars(channel.platform) }} chars left</span>
                              } @else {
                                <span class="bg-rose-500/20 text-rose-300 px-2 py-0.5 rounded border border-rose-500/40">
                                  ⚠️ {{ Math.abs(getRemainingChars(channel.platform)) }} chars OVER limit!
                                </span>
                              }
                            </div>
                            <div class="text-[10px] text-slate-500 font-mono mt-0.5">
                              {{ getAdaptedContent(channel.platform).length }} / {{ channel.charLimit }} limit
                            </div>
                          </div>
                        </div>

                        <!-- Progress Bar -->
                        <div class="w-full bg-slate-950 h-1.5 rounded-full overflow-hidden">
                          <div 
                            class="h-full transition-all duration-300"
                            [style.width.%]="getUsagePercentage(channel.platform)"
                            [class]="getProgressBarClass(channel.platform)">
                          </div>
                        </div>

                        <!-- Platform Formatted Post Mock -->
                        <div class="p-4 rounded-xl bg-slate-950 border border-slate-800 space-y-3">
                          <div class="flex items-center justify-between text-xs text-slate-400">
                            <span class="font-medium text-slate-200">{{ channel.accountHandle }}</span>
                            <span class="text-[10px] text-slate-500">Just now</span>
                          </div>

                          <p class="text-sm text-slate-200 whitespace-pre-wrap leading-relaxed">
                            {{ getAdaptedContent(channel.platform) }}
                          </p>

                          <!-- Hashtags Display -->
                          <div class="flex flex-wrap gap-1.5 pt-1">
                            @for (tag of getRecommendedHashtags(channel.platform); track tag) {
                              <span class="text-[11px] text-indigo-400 bg-indigo-500/10 px-2 py-0.5 rounded font-mono">
                                {{ tag }}
                              </span>
                            }
                          </div>

                          @if (mediaUrl) {
                            <div class="rounded-lg overflow-hidden border border-slate-800 bg-slate-900 p-2.5 text-center text-xs text-slate-400 font-mono">
                              🖼️ Media Attached ({{ getPlatformAspectRatio(channel.platform) }}): {{ mediaUrl }}
                            </div>
                          }

                          <div class="pt-3 border-t border-slate-800/80 flex items-center justify-between text-xs text-slate-500">
                            <span>💬 {{ channel.mockStats.comments }}</span>
                            <span>🔄 {{ channel.mockStats.shares }}</span>
                            <span>❤️ {{ channel.mockStats.likes }}</span>
                          </div>
                        </div>
                      </div>
                    }
                  }
                </div>
              }

              <!-- 2x2 GRID VIEW (Matrix) -->
              @if (previewMode() === 'grid') {
                <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  @for (channel of previewChannels; track channel.platform) {
                    <div class="bg-slate-900 border border-slate-800 rounded-xl p-4 shadow-xl space-y-3 flex flex-col justify-between">
                      <div class="space-y-2">
                        <!-- Mini Channel Header -->
                        <div class="flex items-center justify-between pb-2 border-b border-slate-800">
                          <div class="flex items-center gap-1.5">
                            <span class="text-base">{{ channel.icon }}</span>
                            <span class="text-xs font-semibold text-white">{{ channel.name }}</span>
                          </div>
                          <!-- Live Countdown Badge -->
                          <span 
                            class="text-[10px] font-mono px-2 py-0.5 rounded font-medium border"
                            [class]="getRemainingChars(channel.platform) >= 0 
                              ? 'bg-slate-950 text-slate-300 border-slate-800' 
                              : 'bg-rose-500/20 text-rose-300 border-rose-500/30'">
                            {{ getRemainingChars(channel.platform) }} left
                          </span>
                        </div>

                        <!-- Progress Bar -->
                        <div class="w-full bg-slate-950 h-1 rounded-full overflow-hidden">
                          <div 
                            class="h-full transition-all duration-300"
                            [style.width.%]="getUsagePercentage(channel.platform)"
                            [class]="getProgressBarClass(channel.platform)">
                          </div>
                        </div>

                        <!-- Content Preview -->
                        <p class="text-xs text-slate-300 line-clamp-4 leading-relaxed whitespace-pre-wrap">
                          {{ getAdaptedContent(channel.platform) }}
                        </p>
                      </div>

                      <!-- Footer Aspect Ratio & Tags -->
                      <div class="pt-2 border-t border-slate-800/60 flex items-center justify-between text-[10px] text-slate-400">
                        <span>{{ getPlatformAspectRatio(channel.platform) }}</span>
                        <span class="text-indigo-400 font-mono">{{ getRecommendedHashtags(channel.platform).length }} tags</span>
                      </div>
                    </div>
                  }
                </div>
              }
            </div>
          </div>

          <!-- Bottom Section: Public Comments & 1-Click AI Deflector -->
          <div class="bg-slate-900 border border-slate-800 rounded-xl p-6 shadow-xl space-y-4">
            <div class="flex items-center justify-between">
              <div>
                <h2 class="text-base font-semibold text-white flex items-center gap-2">
                  <span class="w-2 h-2 rounded-full bg-amber-400 animate-pulse"></span>
                  Viral Lead Capture: Public Comment $\rightarrow$ Private DM Deflector
                </h2>
                <p class="text-xs text-slate-400">
                  Auto-captures commenters asking for info, pricing, or demos, and replies with private automated direct messages.
                </p>
              </div>
            </div>

            <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4 pt-2">
              @for (c of comments(); track c.id) {
                <div class="bg-slate-950 border border-slate-800 rounded-lg p-4 space-y-2.5">
                  <div class="flex items-center justify-between text-xs">
                    <span class="font-semibold text-indigo-400">{{ c.author }}</span>
                    <span class="bg-slate-900 px-2 py-0.5 rounded text-slate-400 font-mono text-[10px]">{{ c.platformName }}</span>
                  </div>
                  <p class="text-xs text-slate-300 italic bg-slate-900/60 p-2.5 rounded border border-slate-800">
                    "{{ c.commentText }}"
                  </p>
                  <div class="flex items-center justify-between pt-1">
                    @if (c.deflected) {
                      <span class="text-xs text-emerald-400 font-medium">✓ Deflected: Private DM Sent</span>
                    } @else {
                      <button 
                        (click)="deflectToDm(c)"
                        class="bg-indigo-600 hover:bg-indigo-500 text-white text-xs px-3 py-1.5 rounded font-medium transition shadow-sm">
                        ⚡ Send Private DM
                      </button>
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
export class SocialStudioComponent implements OnInit {
  public readonly Math = Math;
  private api = inject(ApiService);
  private auth = inject(AuthService);

  public tenantId = this.auth.currentTenant()?.id || this.auth.currentUser()?.tenantId || '11111111-1111-1111-1111-111111111111';

  public postText = 'Supercharge your B2B omnicommunication with autonomous AI agents across WhatsApp, Telegram, X, and Phone. What channel drives the highest ROI for your team?';
  public mediaUrl = 'https://assets.sgrowing.com/banner.png';
  public selectedAspectRatio = '1:1 Square';
  public feedbackMessage = signal<string | null>(null);
  public isPublishing = signal<boolean>(false);
  public isGeneratingHooks = signal<boolean>(false);
  public isAdaptingWithAi = signal<boolean>(false);
  public isAuditingTokens = signal<boolean>(false);
  public tokenAuditReport = signal<TokenHealthAuditReportDto | null>(null);

  public previewMode = signal<'tabbed' | 'grid'>('tabbed');
  public activePreviewPlatform = signal<SocialPlatform>(SocialPlatform.X);
  public hookTopic = 'B2B SaaS Growth';

  public previewChannels = [
    {
      platform: SocialPlatform.X,
      name: 'X (Twitter)',
      icon: '🐦',
      charLimit: 280,
      hashtagRule: '1-2 hashtags',
      accountHandle: '@sgrowing',
      mockStats: { comments: 14, shares: 42, likes: 218 }
    },
    {
      platform: SocialPlatform.LinkedIn,
      name: 'LinkedIn',
      icon: '💼',
      charLimit: 3000,
      hashtagRule: '3-5 hashtags',
      accountHandle: 'SGrowing Global',
      mockStats: { comments: 28, shares: 19, likes: 356 }
    },
    {
      platform: SocialPlatform.Instagram,
      name: 'Instagram',
      icon: '📸',
      charLimit: 2200,
      hashtagRule: '3-5 hashtags',
      accountHandle: '@sgrowing.ai',
      mockStats: { comments: 45, shares: 12, likes: 612 }
    },
    {
      platform: SocialPlatform.TikTok,
      name: 'TikTok',
      icon: '🎵',
      charLimit: 2200,
      hashtagRule: '3-5 hashtags',
      accountHandle: '@sgrowing_tok',
      mockStats: { comments: 88, shares: 74, likes: 1420 }
    }
  ];

  public accounts = signal<SocialAccountUI[]>([
    { id: '1', platform: SocialPlatform.X, platformKey: 'x', name: 'X (Twitter)', handle: '@sgrowing', avatarIcon: '🐦', charLimit: 280, aspectRatio: '1:1 Square', selected: true },
    { id: '2', platform: SocialPlatform.LinkedIn, platformKey: 'linkedin', name: 'LinkedIn', handle: 'sgrowing', avatarIcon: '💼', charLimit: 3000, aspectRatio: '1:1 Square', selected: true },
    { id: '3', platform: SocialPlatform.Instagram, platformKey: 'instagram', name: 'Instagram', handle: '@sgrowing.ai', avatarIcon: '📸', charLimit: 2200, aspectRatio: '1:1 Square / 9:16 Reel', selected: true },
    { id: '4', platform: SocialPlatform.TikTok, platformKey: 'tiktok', name: 'TikTok', handle: '@sgrowing_tok', avatarIcon: '🎵', charLimit: 2200, aspectRatio: '9:16 Vertical Reel', selected: true },
    { id: '5', platform: SocialPlatform.Facebook, platformKey: 'facebook', name: 'Facebook', handle: 'SGrowing Page', avatarIcon: '📘', charLimit: 2200, aspectRatio: '1:1 Square', selected: false },
    { id: '6', platform: SocialPlatform.Threads, platformKey: 'threads', name: 'Threads', handle: '@sgrowing', avatarIcon: '🧵', charLimit: 500, aspectRatio: '1:1 Square', selected: false },
    { id: '7', platform: SocialPlatform.Telegram, platformKey: 'telegram', name: 'Telegram VIP', handle: '@sgrowing_channel', avatarIcon: '✈️', charLimit: 4096, aspectRatio: '1:1 Square', selected: false }
  ]);

  public viralHooks = signal<ViralHookSuggestionDto[]>([
    { hookText: "90% of companies get B2B SaaS Growth completely wrong. Here's why:", angle: 'Contrarian', estimatedViralityScore: 94.5 },
    { hookText: 'We scaled revenue by 340% using this single B2B SaaS Growth framework:', angle: 'Statistical', estimatedViralityScore: 91.2 },
    { hookText: 'If you had to restart B2B SaaS Growth from scratch in 2026, what would you do differently?', angle: 'Question', estimatedViralityScore: 88.0 },
    { hookText: 'Last year, our team made a critical mistake with B2B SaaS Growth. Here is what it taught us:', angle: 'Story', estimatedViralityScore: 92.8 }
  ]);

  public posts = signal<SocialPostDto[]>([]);

  public comments = signal<PublicCommentUI[]>([
    { id: 'c1', platform: SocialPlatform.Instagram, platformName: 'Instagram', author: '@tech_founder', postSnippet: 'AI Omnicommunication Launch', commentText: 'How much does the enterprise plan cost for unlimited WhatsApp channels?', deflected: false },
    { id: 'c2', platform: SocialPlatform.X, platformName: 'X', author: '@growth_lead', postSnippet: 'AI Omnicommunication Launch', commentText: 'Can I test the AI voice dialer with our team demo?', deflected: false },
    { id: 'c3', platform: SocialPlatform.LinkedIn, platformName: 'LinkedIn', author: 'Marcus Vance', postSnippet: 'AI Omnicommunication Launch', commentText: 'Interested in the CRM integration with HubSpot. Send link please.', deflected: true }
  ]);

  public hasTokenAlerts = computed(() => {
    const report = this.tokenAuditReport();
    if (report && report.expiringOrRequiredRefreshCount > 0) return true;
    return this.accounts().some(a => a.tokenExpiringSoon);
  });

  public tokenStatusText = computed(() => {
    const report = this.tokenAuditReport();
    if (report) {
      return report.expiringOrRequiredRefreshCount > 0
        ? `⚠️ ${report.expiringOrRequiredRefreshCount} Token Refresh Required`
        : `🛡️ Tokens Healthy (${report.healthyAccountsCount}/${report.totalAccountsAudited})`;
    }
    return `🛡️ All Tokens Healthy`;
  });

  ngOnInit(): void {
    this.loadPosts();
    this.runTokenAudit();
  }

  public loadPosts(): void {
    this.api.getSocialPosts(this.tenantId).subscribe({
      next: (data) => this.posts.set(data),
      error: () => {}
    });
  }

  public toggleAccount(acc: SocialAccountUI): void {
    acc.selected = !acc.selected;
  }

  public runTokenAudit(): void {
    this.isAuditingTokens.set(true);
    this.api.auditSocialTokenHealth(this.tenantId).subscribe({
      next: (report) => {
        this.isAuditingTokens.set(false);
        this.tokenAuditReport.set(report);
        if (report.expiringOrRequiredRefreshCount > 0) {
          this.feedbackMessage.set(`⚠️ Token Health Alert: ${report.expiringOrRequiredRefreshCount} accounts have expiring or required refresh tokens.`);
        }
      },
      error: () => this.isAuditingTokens.set(false)
    });
  }

  public generateHooks(): void {
    this.isGeneratingHooks.set(true);
    this.api.generateViralHooks(this.hookTopic || 'B2B SaaS Growth', this.tenantId).subscribe({
      next: (hooks) => {
        this.isGeneratingHooks.set(false);
        if (hooks && hooks.length > 0) {
          this.viralHooks.set(hooks);
          this.feedbackMessage.set(`✨ Generated ${hooks.length} viral hook angles for "${this.hookTopic}".`);
        }
      },
      error: () => {
        this.isGeneratingHooks.set(false);
      }
    });
  }

  public injectHook(hookText: string): void {
    this.postText = `${hookText}\n\n${this.postText.trim()}`;
    this.feedbackMessage.set(`⚡ Injected viral hook into post content!`);
  }

  public autoAdaptWithAi(): void {
    this.isAdaptingWithAi.set(true);
    const selected = this.accounts().filter(a => a.selected).map(a => a.platform);
    this.api.rewritePostForPlatforms(this.postText, selected, this.tenantId, this.selectedAspectRatio).subscribe({
      next: (adapted) => {
        this.isAdaptingWithAi.set(false);
        this.feedbackMessage.set(`🪄 AI adapted post across ${adapted.length} target channels with character limits & aspect ratio rules!`);
      },
      error: () => this.isAdaptingWithAi.set(false)
    });
  }

  public getAdaptedContent(platform: SocialPlatform): string {
    const raw = this.postText.trim();
    if (!raw) return 'Enter your master post content in the composer to preview...';

    switch (platform) {
      case SocialPlatform.X:
        return raw.length > 270 ? raw.slice(0, 267) + '...' : raw;

      case SocialPlatform.LinkedIn:
        return `💡 Executive Takeaway:\n\n${raw}\n\nWhat are your thoughts on this approach?`;

      case SocialPlatform.Instagram:
        if (this.selectedAspectRatio.includes('9:16')) {
          return `🔥 REEL HOOK: ${raw}\n.\n.\nSave this for later! 📌`;
        }
        return `${raw}\n.\n.\nDrop a comment below! 👇`;

      case SocialPlatform.TikTok:
        return `🚀 POV: ${raw}\n\nDouble tap if you agree! 🎯`;

      default:
        return raw;
    }
  }

  public getRecommendedHashtags(platform: SocialPlatform): string[] {
    switch (platform) {
      case SocialPlatform.X:
        return ['#Growth', '#Tech'];
      case SocialPlatform.LinkedIn:
        return ['#Leadership', '#B2B', '#SalesOps', '#Innovation'];
      case SocialPlatform.Instagram:
        return ['#marketing', '#growth', '#business', '#strategy'];
      case SocialPlatform.TikTok:
        return ['#TikTokGrowth', '#TechTok', '#BusinessTips', '#Viral'];
      default:
        return ['#growth'];
    }
  }

  public getPlatformAspectRatio(platform: SocialPlatform): string {
    if (platform === SocialPlatform.TikTok) return '9:16 Vertical Reel/Story';
    if (platform === SocialPlatform.Instagram && this.selectedAspectRatio.includes('9:16')) {
      return '9:16 Vertical Reel/Story';
    }
    return '1:1 Square';
  }

  public getPlatformLimit(platform: SocialPlatform): number {
    switch (platform) {
      case SocialPlatform.X: return 280;
      case SocialPlatform.LinkedIn: return 3000;
      case SocialPlatform.Instagram: return 2200;
      case SocialPlatform.TikTok: return 2200;
      default: return 280;
    }
  }

  public getRemainingChars(platform: SocialPlatform): number {
    const limit = this.getPlatformLimit(platform);
    const length = this.getAdaptedContent(platform).length;
    return limit - length;
  }

  public isContentWithinLimit(platform: SocialPlatform): boolean {
    return this.getRemainingChars(platform) >= 0;
  }

  public getUsagePercentage(platform: SocialPlatform): number {
    const limit = this.getPlatformLimit(platform);
    const length = this.getAdaptedContent(platform).length;
    return Math.min(100, Math.round((length / limit) * 100));
  }

  public getMeterColorClass(platform: SocialPlatform): string {
    const remaining = this.getRemainingChars(platform);
    if (remaining < 0) return 'text-rose-400';
    if (remaining < 50) return 'text-amber-400';
    return 'text-emerald-400';
  }

  public getProgressBarClass(platform: SocialPlatform): string {
    const remaining = this.getRemainingChars(platform);
    if (remaining < 0) return 'bg-rose-500';
    if (remaining < 50) return 'bg-amber-500';
    return 'bg-emerald-500';
  }

  public getAngleBadgeClass(angle: string): string {
    switch (angle.toLowerCase()) {
      case 'contrarian': return 'bg-rose-500/20 text-rose-300 border border-rose-500/30';
      case 'statistical': return 'bg-sky-500/20 text-sky-300 border border-sky-500/30';
      case 'question': return 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/30';
      case 'story': return 'bg-purple-500/20 text-purple-300 border border-purple-500/30';
      default: return 'bg-slate-800 text-slate-300';
    }
  }

  public publishNow(): void {
    const selectedPlatforms = this.accounts().filter(a => a.selected).map(a => a.platform);
    if (selectedPlatforms.length === 0) {
      this.feedbackMessage.set('Please select at least one target network.');
      return;
    }

    this.isPublishing.set(true);
    this.api.createSocialPost({
      tenantId: this.tenantId,
      content: this.postText,
      targetPlatforms: selectedPlatforms,
      scheduledAtUtc: new Date(Date.now() - 5000).toISOString(),
      mediaUrls: this.mediaUrl ? [this.mediaUrl] : []
    }).subscribe({
      next: () => {
        this.api.publishDueSocialPosts(this.tenantId).subscribe({
          next: () => {
            this.isPublishing.set(false);
            this.feedbackMessage.set(`🎉 Successfully published across ${selectedPlatforms.length} networks!`);
            this.loadPosts();
          },
          error: () => this.isPublishing.set(false)
        });
      },
      error: () => this.isPublishing.set(false)
    });
  }

  public schedulePost(): void {
    const selectedPlatforms = this.accounts().filter(a => a.selected).map(a => a.platform);
    if (selectedPlatforms.length === 0) {
      this.feedbackMessage.set('Please select at least one target network.');
      return;
    }

    const scheduledDate = new Date();
    scheduledDate.setDate(scheduledDate.getDate() + 1);
    scheduledDate.setHours(15, 0, 0, 0);

    this.isPublishing.set(true);
    this.api.createSocialPost({
      tenantId: this.tenantId,
      content: this.postText,
      targetPlatforms: selectedPlatforms,
      scheduledAtUtc: scheduledDate.toISOString(),
      mediaUrls: this.mediaUrl ? [this.mediaUrl] : []
    }).subscribe({
      next: () => {
        this.isPublishing.set(false);
        this.feedbackMessage.set(`📅 Scheduled post for ${scheduledDate.toLocaleDateString()} at 15:00.`);
        this.loadPosts();
      },
      error: () => this.isPublishing.set(false)
    });
  }

  public triggerPublishDue(): void {
    this.isPublishing.set(true);
    this.api.publishDueSocialPosts(this.tenantId).subscribe({
      next: (res) => {
        this.isPublishing.set(false);
        this.feedbackMessage.set(`⚡ Processed due posts. Dispatched: ${res.count}`);
        this.loadPosts();
      },
      error: () => this.isPublishing.set(false)
    });
  }

  public deflectToDm(comment: PublicCommentUI): void {
    this.api.deflectSocialComment({
      tenantId: this.tenantId,
      platform: comment.platform,
      postExternalId: 'ext_post_101',
      commenterUsername: comment.author.replace('@', ''),
      commentText: comment.commentText
    }).subscribe({
      next: (res) => {
        comment.deflected = true;
        this.feedbackMessage.set(`💬 Sent automated private DM to ${comment.author}: "${res.replyText}"`);
      }
    });
  }
}
