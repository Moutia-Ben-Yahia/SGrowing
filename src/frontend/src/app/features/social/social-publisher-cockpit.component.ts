import { Component, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslationService } from '../../core/services/translation.service';

export type SocialPlatform = 'Facebook' | 'Instagram' | 'LinkedIn' | 'X' | 'TikTok';
export type SocialPostStatus = 'Draft' | 'PendingApproval' | 'Approved' | 'Scheduled' | 'Published';

export interface SocialPostItem {
  id: string;
  platforms: SocialPlatform[];
  content: string;
  mediaUrls: string[];
  scheduledTime: string;
  status: SocialPostStatus;
  impressions: number;
  clicks: number;
  likes: number;
}

@Component({
  selector: 'app-social-publisher-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b border-slate-700 pb-5">
        <div>
          <div class="flex items-center gap-3">
            <span class="p-2 bg-pink-600/20 text-pink-400 rounded-lg text-xl">📱</span>
            <div>
              <h1 class="text-2xl font-bold text-white tracking-tight">Social Media Publisher & Content Studio</h1>
              <p class="text-sm text-slate-400">Meta & LinkedIn direct scheduling, approval matrix, and live engagement analytics</p>
            </div>
          </div>
        </div>
        <div class="flex items-center gap-3">
          <span class="inline-flex items-center px-3 py-1 rounded-full text-xs font-semibold bg-emerald-500/20 text-emerald-300 border border-emerald-500/30">
            ● Meta & LinkedIn Graph Connected
          </span>
        </div>
      </div>

      <!-- KPI Metrics -->
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div class="p-4 bg-slate-900 border border-slate-800 rounded-xl space-y-1">
          <span class="text-xs text-slate-400">Total Impressions</span>
          <p class="text-2xl font-bold text-white font-mono">{{ totalImpressions() | number }}</p>
          <span class="text-[11px] text-emerald-400">+14.2% this week</span>
        </div>
        <div class="p-4 bg-slate-900 border border-slate-800 rounded-xl space-y-1">
          <span class="text-xs text-slate-400">Total Link Clicks</span>
          <p class="text-2xl font-bold text-cyan-400 font-mono">{{ totalClicks() | number }}</p>
          <span class="text-[11px] text-cyan-400">Avg CTR: 3.8%</span>
        </div>
        <div class="p-4 bg-slate-900 border border-slate-800 rounded-xl space-y-1">
          <span class="text-xs text-slate-400">Posts in Flight</span>
          <p class="text-2xl font-bold text-amber-400 font-mono">{{ posts().length }}</p>
          <span class="text-[11px] text-slate-400">Across 4 networks</span>
        </div>
        <div class="p-4 bg-slate-900 border border-slate-800 rounded-xl space-y-1">
          <span class="text-xs text-slate-400">Pending Approvals</span>
          <p class="text-2xl font-bold text-pink-400 font-mono">{{ pendingApprovalCount() }}</p>
          <span class="text-[11px] text-pink-400">Requires Lead Signoff</span>
        </div>
      </div>

      <!-- Composer & Live Mockup Grid -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <!-- Composer (7 cols) -->
        <div class="lg:col-span-7 bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4">
          <h2 class="text-base font-semibold text-white flex items-center gap-2">
            <span>✍️</span> Compose Multi-Platform Post
          </h2>

          <!-- Platform Selector -->
          <div class="space-y-1.5">
            <label class="text-xs text-slate-400">Target Platforms</label>
            <div class="flex flex-wrap gap-2">
              @for (p of availablePlatforms; track p) {
                <button (click)="togglePlatform(p)"
                        class="px-3 py-1.5 rounded-lg text-xs font-medium transition flex items-center gap-1.5"
                        [ngClass]="selectedPlatforms().includes(p) ? 'bg-indigo-600 text-white font-semibold' : 'bg-slate-800 text-slate-400 hover:text-slate-200'">
                  <span>{{ getPlatformIcon(p) }}</span>
                  <span>{{ p }}</span>
                </button>
              }
            </div>
          </div>

          <!-- Post Content -->
          <div class="space-y-1.5">
            <div class="flex justify-between items-center text-xs text-slate-400">
              <label>Post Copy</label>
              <span class="font-mono" [ngClass]="postContent().length > 280 ? 'text-amber-400' : 'text-slate-400'">
                {{ postContent().length }} chars
              </span>
            </div>
            <textarea [(ngModel)]="postContent"
                      rows="4"
                      placeholder="Write your growth-driven message, announce product releases, or engage your B2B audience..."
                      class="w-full bg-slate-950 border border-slate-800 rounded-xl p-3 text-sm text-slate-100 placeholder-slate-500 focus:outline-none focus:border-indigo-500"></textarea>
          </div>

          <!-- Media URLs -->
          <div class="space-y-1.5">
            <label class="text-xs text-slate-400">Media Asset URL (Image / Video)</label>
            <input type="text" [(ngModel)]="mediaUrl"
                   placeholder="https://assets.sgrowing.io/creatives/q4-announcement.png"
                   class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-xs text-slate-200 focus:outline-none focus:border-indigo-500">
          </div>

          <!-- Schedule Options & Submit -->
          <div class="pt-2 flex flex-col sm:flex-row items-center justify-between gap-3 border-t border-slate-800">
            <div class="flex items-center gap-2">
              <input type="checkbox" id="requireApproval" [(ngModel)]="requireApproval"
                     class="rounded bg-slate-800 border-slate-700 text-indigo-600 focus:ring-0">
              <label for="requireApproval" class="text-xs text-slate-300">Submit for Manager Approval</label>
            </div>
            <div class="flex items-center gap-2">
              <button (click)="createPost(true)"
                      class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-semibold rounded-lg border border-slate-700 transition">
                Save Draft
              </button>
              <button (click)="createPost(false)"
                      class="px-5 py-2 bg-gradient-to-r from-pink-600 to-indigo-600 hover:from-pink-500 hover:to-indigo-500 text-white text-xs font-semibold rounded-lg shadow-md transition">
                {{ requireApproval ? 'Submit for Signoff' : 'Schedule Post 🚀' }}
              </button>
            </div>
          </div>
        </div>

        <!-- Live Feed Mockup (5 cols) -->
        <div class="lg:col-span-5 bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4">
          <div class="flex items-center justify-between border-b border-slate-800 pb-3">
            <h2 class="text-base font-semibold text-white flex items-center gap-2">
              <span>👁️</span> Live Platform Preview
            </h2>
            <div class="flex gap-1">
              @for (preview of ['LinkedIn', 'Instagram']; track preview) {
                <button (click)="previewMode.set(preview)"
                        class="px-2.5 py-1 text-[11px] rounded transition"
                        [ngClass]="previewMode() === preview ? 'bg-indigo-600 text-white' : 'bg-slate-800 text-slate-400'">
                  {{ preview }}
                </button>
              }
            </div>
          </div>

          <!-- Preview Container -->
          <div class="p-4 bg-slate-950 rounded-xl border border-slate-800 space-y-3">
            <div class="flex items-center gap-2.5">
              <div class="w-9 h-9 rounded-full bg-indigo-600 flex items-center justify-center font-bold text-white text-sm">
                SG
              </div>
              <div>
                <p class="text-xs font-semibold text-white">SGrowing Growth OS</p>
                <p class="text-[10px] text-slate-400">Promoted • Just now</p>
              </div>
            </div>

            <p class="text-xs text-slate-200 whitespace-pre-line leading-relaxed">
              {{ postContent() || 'Your preview text will render here dynamically as you write...' }}
            </p>

            @if (mediaUrl) {
              <div class="w-full h-40 bg-slate-900 rounded-lg flex items-center justify-center border border-slate-800 text-slate-500 text-xs overflow-hidden">
                <span class="truncate px-2">{{ mediaUrl }}</span>
              </div>
            }

            <div class="pt-2 border-t border-slate-800 flex justify-around text-slate-400 text-xs">
              <span class="hover:text-slate-200 cursor-pointer">👍 Like</span>
              <span class="hover:text-slate-200 cursor-pointer">💬 Comment</span>
              <span class="hover:text-slate-200 cursor-pointer">↗️ Share</span>
            </div>
          </div>
        </div>
      </div>

      <!-- Published & Scheduled Posts Queue -->
      <div class="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4">
        <h3 class="text-base font-semibold text-white flex items-center gap-2">
          <span>📋</span> Scheduled & Published Content Queue
        </h3>

        <div class="overflow-x-auto">
          <table class="w-full text-left text-xs">
            <thead class="text-slate-400 border-b border-slate-800">
              <tr>
                <th class="py-2.5 px-3">Networks</th>
                <th class="py-2.5 px-3">Content Snippet</th>
                <th class="py-2.5 px-3">Scheduled Time</th>
                <th class="py-2.5 px-3">Status</th>
                <th class="py-2.5 px-3">Impressions</th>
                <th class="py-2.5 px-3">Action</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-800">
              @for (post of posts(); track post.id) {
                <tr class="hover:bg-slate-800/40 transition">
                  <td class="py-3 px-3">
                    <div class="flex gap-1">
                      @for (plt of post.platforms; track plt) {
                        <span class="px-1.5 py-0.5 rounded text-[10px] bg-slate-800 text-slate-300 font-mono">
                          {{ plt }}
                        </span>
                      }
                    </div>
                  </td>
                  <td class="py-3 px-3 max-w-xs truncate text-slate-200">{{ post.content }}</td>
                  <td class="py-3 px-3 font-mono text-slate-400">{{ post.scheduledTime }}</td>
                  <td class="py-3 px-3">
                    <span class="px-2 py-0.5 rounded text-[10px] font-bold"
                          [ngClass]="{
                            'bg-emerald-500/20 text-emerald-300': post.status === 'Published',
                            'bg-cyan-500/20 text-cyan-300': post.status === 'Scheduled',
                            'bg-pink-500/20 text-pink-300': post.status === 'PendingApproval',
                            'bg-slate-700 text-slate-300': post.status === 'Draft'
                          }">
                      {{ post.status }}
                    </span>
                  </td>
                  <td class="py-3 px-3 font-mono text-cyan-300">{{ post.impressions | number }}</td>
                  <td class="py-3 px-3">
                    @if (post.status === 'PendingApproval') {
                      <button (click)="approvePost(post.id)"
                              class="px-2.5 py-1 bg-emerald-600 hover:bg-emerald-500 text-white rounded text-[10px] font-semibold transition">
                        Signoff & Schedule
                      </button>
                    } @else {
                      <span class="text-slate-500 text-[11px]">—</span>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `
})
export class SocialPublisherCockpitComponent {
  translation: TranslationService;

  availablePlatforms: SocialPlatform[] = ['LinkedIn', 'Instagram', 'Facebook', 'X'];
  selectedPlatforms = signal<SocialPlatform[]>(['LinkedIn', 'Instagram']);
  postContent = signal<string>('Announcing our new AI Growth Director: coordinate SEO, CAPI ads, and DAG nurture workflows autonomously.');
  mediaUrl = 'https://assets.sgrowing.io/creatives/q4-announcement.png';
  requireApproval = false;
  previewMode = signal<string>('LinkedIn');

  posts = signal<SocialPostItem[]>([
    {
      id: 'post_001',
      platforms: ['LinkedIn', 'Facebook'],
      content: 'Scaling CAC down by 42% using deterministic CDP identity stitching. Read our engineering benchmark.',
      mediaUrls: ['https://assets.sgrowing.io/benchmarks.png'],
      scheduledTime: 'Today, 14:00 UTC',
      status: 'Published',
      impressions: 14200,
      clicks: 580,
      likes: 310
    },
    {
      id: 'post_002',
      platforms: ['Instagram', 'TikTok'],
      content: 'Behind the scenes: how our autonomous growth agent optimizes ad spend in real-time.',
      mediaUrls: ['https://assets.sgrowing.io/bts.png'],
      scheduledTime: 'Tomorrow, 09:30 UTC',
      status: 'PendingApproval',
      impressions: 0,
      clicks: 0,
      likes: 0
    }
  ]);

  totalImpressions = computed(() => this.posts().reduce((sum, p) => sum + p.impressions, 0));
  totalClicks = computed(() => this.posts().reduce((sum, p) => sum + p.clicks, 0));
  pendingApprovalCount = computed(() => this.posts().filter(p => p.status === 'PendingApproval').length);

  constructor(translationService?: TranslationService) {
    if (translationService) {
      this.translation = translationService;
    } else {
      try {
        this.translation = inject(TranslationService);
      } catch {
        this.translation = new TranslationService();
      }
    }
  }

  togglePlatform(platform: SocialPlatform): void {
    const current = this.selectedPlatforms();
    if (current.includes(platform)) {
      if (current.length > 1) {
        this.selectedPlatforms.set(current.filter(p => p !== platform));
      }
    } else {
      this.selectedPlatforms.set([...current, platform]);
    }
  }

  getPlatformIcon(platform: SocialPlatform): string {
    switch (platform) {
      case 'LinkedIn': return '💼';
      case 'Instagram': return '📸';
      case 'Facebook': return '📘';
      case 'X': return '🐦';
      case 'TikTok': return '🎵';
    }
  }

  createPost(isDraft: boolean): void {
    const status: SocialPostStatus = isDraft
      ? 'Draft'
      : (this.requireApproval ? 'PendingApproval' : 'Scheduled');

    const newPost: SocialPostItem = {
      id: 'post_' + Math.random().toString(36).substring(2, 8),
      platforms: [...this.selectedPlatforms()],
      content: this.postContent(),
      mediaUrls: this.mediaUrl ? [this.mediaUrl] : [],
      scheduledTime: isDraft ? 'Draft' : 'Today, 18:00 UTC',
      status,
      impressions: 0,
      clicks: 0,
      likes: 0
    };

    this.posts.update(list => [newPost, ...list]);
  }

  approvePost(id: string): void {
    this.posts.update(list =>
      list.map(p => p.id === id ? { ...p, status: 'Scheduled', scheduledTime: 'Today, 19:00 UTC' } : p)
    );
  }
}
