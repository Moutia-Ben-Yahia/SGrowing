import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { SocialPostDto, SocialPlatform, SocialAccountDto } from '../../core/models/social.models';

export interface SocialAccountUI {
  id: string;
  platform: SocialPlatform;
  platformKey: string;
  name: string;
  handle: string;
  avatarIcon: string;
  selected: boolean;
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
              <p class="text-xs text-slate-400">Postiz & Mixpost model: Unified cross-network publishing & viral lead deflector</p>
            </div>
          </div>
          <div class="flex items-center gap-3">
            <span class="text-xs text-emerald-400 bg-emerald-500/10 px-3 py-1 rounded-full border border-emerald-500/20 font-medium">
              ● {{ accounts().length }} Networks Connected
            </span>
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
          
          <!-- Top Section: Multi-Platform Composer & Preview -->
          <div class="grid grid-cols-1 lg:grid-cols-12 gap-8">
            <!-- Composer (7 cols) -->
            <div class="lg:col-span-7 bg-slate-900 border border-slate-800 rounded-xl p-6 shadow-xl space-y-5">
              <div class="flex items-center justify-between">
                <h2 class="text-base font-semibold text-white">Create Multi-Channel Post</h2>
                <span class="text-xs text-slate-400 font-mono">
                  Length: <strong [class.text-rose-400]="postText.length > 280 && isXSelected()" class="text-slate-200">{{ postText.length }}</strong>
                  <span *ngIf="isXSelected()" class="text-slate-500"> / 280 (X limit)</span>
                </span>
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
                    </button>
                  }
                </div>
              </div>

              <!-- Content Textarea -->
              <div>
                <label class="block text-xs font-medium text-slate-400 mb-1">Message Content (AI-optimized cross-network formatting)</label>
                <textarea 
                  [(ngModel)]="postText" 
                  rows="5" 
                  placeholder="Announce product updates, launch campaigns, or customer promotions across all social channels..."
                  class="w-full bg-slate-950 border border-slate-800 rounded-xl p-4 text-sm text-slate-100 placeholder-slate-500 focus:outline-none focus:border-indigo-500 transition">
                </textarea>
              </div>

              <!-- Media URL -->
              <div>
                <label class="block text-xs font-medium text-slate-400 mb-1">Attached Media / CDN Asset URL</label>
                <input 
                  [(ngModel)]="mediaUrl" 
                  placeholder="https://assets.sgrowing.com/banners/launch-2026.png" 
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-slate-200 focus:outline-none focus:border-indigo-500 font-mono" />
              </div>

              <!-- Buttons -->
              <div class="flex items-center gap-3 pt-2">
                <button 
                  (click)="publishNow()" 
                  [disabled]="!postText.trim() || isPublishing()"
                  class="flex-1 bg-gradient-to-r from-indigo-600 to-fuchsia-600 hover:from-indigo-500 hover:to-fuchsia-500 disabled:opacity-40 text-white font-medium py-2.5 rounded-lg text-sm transition shadow-lg shadow-indigo-900/30">
                  🚀 Publish Now to Selected Channels
                </button>
                <button 
                  (click)="schedulePost()" 
                  [disabled]="!postText.trim() || isPublishing()"
                  class="bg-slate-800 hover:bg-slate-700 disabled:opacity-40 text-slate-200 font-medium px-5 py-2.5 rounded-lg text-sm transition border border-slate-700">
                  📅 Schedule for Later
                </button>
              </div>
            </div>

            <!-- Preview Card (5 cols) -->
            <div class="lg:col-span-5 space-y-4">
              <h2 class="text-base font-semibold text-white">Live Multi-Network Preview</h2>
              <div class="bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-xl space-y-4">
                <div class="flex items-center gap-3 pb-3 border-b border-slate-800">
                  <div class="w-10 h-10 rounded-full bg-gradient-to-tr from-indigo-500 to-cyan-400 flex items-center justify-center font-bold text-white text-xs">
                    SG
                  </div>
                  <div>
                    <div class="font-semibold text-sm text-white flex items-center gap-1.5">
                      SGrowing Platform
                      <span class="text-blue-400 text-xs">✓</span>
                    </div>
                    <div class="text-xs text-slate-400">@sgrowing_app • Previewing {{ getSelectedNames() }}</div>
                  </div>
                </div>

                <p class="text-sm text-slate-200 whitespace-pre-wrap leading-relaxed">
                  {{ postText || 'Preview your multi-network post here. Formatting adapts automatically for X, LinkedIn, Instagram, Facebook, Threads, and Telegram.' }}
                </p>

                @if (mediaUrl) {
                  <div class="rounded-lg overflow-hidden border border-slate-800 bg-slate-950/80 p-2 text-center text-xs text-slate-400 font-mono">
                    🖼️ Attached Asset: {{ mediaUrl }}
                  </div>
                }

                <div class="pt-3 border-t border-slate-800/80 flex items-center justify-between text-xs text-slate-500">
                  <span>💬 12 comments</span>
                  <span>🔄 38 reposts</span>
                  <span>❤️ 184 likes</span>
                </div>
              </div>

              <!-- Scheduled Posts Feed -->
              <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-4 space-y-3">
                <div class="flex items-center justify-between text-xs">
                  <span class="font-semibold text-white">Editorial Publishing Queue</span>
                  <span class="text-indigo-400 font-mono">{{ posts().length }} Posts</span>
                </div>
                <div class="space-y-2 max-h-48 overflow-y-auto">
                  @for (p of posts(); track p.id) {
                    <div class="p-2.5 rounded-lg bg-slate-950 border border-slate-800 flex items-center justify-between text-xs">
                      <div class="truncate mr-3 max-w-[200px]">
                        <p class="text-slate-200 truncate">{{ p.content }}</p>
                        <span class="text-[10px] text-slate-500">{{ p.createdAtUtc | date:'short' }}</span>
                      </div>
                      <span [class]="p.status === 3 ? 'text-emerald-400 bg-emerald-500/10' : 'text-amber-400 bg-amber-500/10'" class="px-2 py-0.5 rounded text-[10px] font-medium border border-current/20">
                        {{ p.status === 3 ? 'Published' : 'Scheduled' }}
                      </span>
                    </div>
                  } @empty {
                    <p class="text-xs text-slate-500 italic">No posts in queue yet. Create your first post above.</p>
                  }
                </div>
              </div>
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
  private api = inject(ApiService);
  private auth = inject(AuthService);

  public tenantId = this.auth.currentTenant()?.id || this.auth.currentUser()?.tenantId || '11111111-1111-1111-1111-111111111111';

  public postText = 'Supercharge your B2B omnicommunication with autonomous AI agents across WhatsApp, Telegram, X, and Phone. What channel drives the highest ROI for your team?';
  public mediaUrl = 'https://assets.sgrowing.com/banner.png';
  public feedbackMessage = signal<string | null>(null);
  public isPublishing = signal<boolean>(false);

  public accounts = signal<SocialAccountUI[]>([
    { id: '1', platform: SocialPlatform.X, platformKey: 'x', name: 'X (Twitter)', handle: '@sgrowing', avatarIcon: '🐦', selected: true },
    { id: '2', platform: SocialPlatform.LinkedIn, platformKey: 'linkedin', name: 'LinkedIn', handle: 'sgrowing', avatarIcon: '💼', selected: true },
    { id: '3', platform: SocialPlatform.Instagram, platformKey: 'instagram', name: 'Instagram', handle: '@sgrowing.ai', avatarIcon: '📸', selected: true },
    { id: '4', platform: SocialPlatform.Facebook, platformKey: 'facebook', name: 'Facebook', handle: 'SGrowing Page', avatarIcon: '📘', selected: false },
    { id: '5', platform: SocialPlatform.Threads, platformKey: 'threads', name: 'Threads', handle: '@sgrowing', avatarIcon: '🧵', selected: true },
    { id: '6', platform: SocialPlatform.Telegram, platformKey: 'telegram', name: 'Telegram VIP', handle: '@sgrowing_channel', avatarIcon: '✈️', selected: false },
  ]);

  public posts = signal<SocialPostDto[]>([]);

  public comments = signal<PublicCommentUI[]>([
    { id: 'c1', platform: SocialPlatform.Instagram, platformName: 'Instagram', author: '@tech_founder', postSnippet: 'AI Omnicommunication Launch', commentText: 'How much does the enterprise plan cost for unlimited WhatsApp channels?', deflected: false },
    { id: 'c2', platform: SocialPlatform.X, platformName: 'X', author: '@growth_lead', postSnippet: 'AI Omnicommunication Launch', commentText: 'Can I test the AI voice dialer with our team demo?', deflected: false },
    { id: 'c3', platform: SocialPlatform.LinkedIn, platformName: 'LinkedIn', author: 'Marcus Vance', postSnippet: 'AI Omnicommunication Launch', commentText: 'Interested in the CRM integration with HubSpot. Send link please.', deflected: true }
  ]);

  ngOnInit(): void {
    this.loadPosts();
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

  public isXSelected(): boolean {
    return !!this.accounts().find(a => a.platform === SocialPlatform.X && a.selected);
  }

  public getSelectedNames(): string {
    return this.accounts().filter(a => a.selected).map(a => a.name).join(', ') || 'None selected';
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
      next: (createdPost) => {
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
      next: (createdPost) => {
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
