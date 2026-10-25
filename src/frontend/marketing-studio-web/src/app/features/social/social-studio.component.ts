import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface SocialAccountUI {
  id: string;
  platform: 'facebook' | 'instagram' | 'threads' | 'x' | 'linkedin' | 'telegram';
  name: string;
  handle: string;
  avatarIcon: string;
  selected: boolean;
}

export interface SocialPostUI {
  id: string;
  content: string;
  platforms: string[];
  status: 'Scheduled' | 'Published' | 'Draft';
  scheduledTime: string;
}

export interface PublicCommentUI {
  id: string;
  platform: string;
  author: string;
  postSnippet: string;
  commentText: string;
  deflected: boolean;
}

@Component({
  selector: 'app-social-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="flex h-screen bg-slate-950 text-slate-100 font-sans overflow-hidden">
      <!-- Main Content -->
      <div class="flex-1 flex flex-col h-full overflow-hidden">
        <!-- Header -->
        <header class="h-16 border-b border-slate-800 bg-slate-900/50 backdrop-blur px-8 flex items-center justify-between">
          <div class="flex items-center gap-3">
            <div class="w-8 h-8 rounded-lg bg-gradient-to-tr from-fuchsia-600 to-indigo-600 text-white flex items-center justify-center font-bold text-sm shadow-lg shadow-fuchsia-900/30">
              #
            </div>
            <div>
              <h1 class="text-lg font-semibold text-white tracking-tight">Social Media Multi-Publishing Studio</h1>
              <p class="text-xs text-slate-400">Postiz & Mixpost model: Unified cross-publishing & viral AI deflector</p>
            </div>
          </div>
          <div class="flex items-center gap-2">
            <span class="text-xs text-emerald-400 bg-emerald-500/10 px-3 py-1 rounded-full border border-emerald-500/20 font-medium">
              ● 6 Networks Connected
            </span>
          </div>
        </header>

        <!-- Body -->
        <div class="flex-1 overflow-y-auto p-8 space-y-8">
          
          <!-- Top Section: Multi-Platform Composer & Preview -->
          <div class="grid grid-cols-1 lg:grid-cols-12 gap-8">
            <!-- Composer (7 cols) -->
            <div class="lg:col-span-7 bg-slate-900 border border-slate-800 rounded-xl p-6 shadow-xl space-y-5">
              <div class="flex items-center justify-between">
                <h2 class="text-base font-semibold text-white">Create Multi-Channel Post</h2>
                <span class="text-xs text-slate-400 font-mono">
                  Length: <strong [class.text-rose-400]="postText.length > 280" class="text-slate-200">{{ postText.length }}</strong>
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
                <label class="block text-xs font-medium text-slate-400 mb-1">Message Content (AI-optimized)</label>
                <textarea 
                  [(ngModel)]="postText" 
                  rows="5" 
                  placeholder="Announce product updates, market insights, or special promotions across all social channels..."
                  class="w-full bg-slate-950 border border-slate-800 rounded-xl p-4 text-sm text-slate-100 placeholder-slate-500 focus:outline-none focus:border-indigo-500 transition">
                </textarea>
              </div>

              <!-- Media URL -->
              <div>
                <label class="block text-xs font-medium text-slate-400 mb-1">Image / Asset URL</label>
                <input 
                  [(ngModel)]="mediaUrl" 
                  placeholder="https://assets.sgrowing.com/banners/launch-2026.png" 
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-slate-200 focus:outline-none focus:border-indigo-500 font-mono" />
              </div>

              <!-- Buttons -->
              <div class="flex items-center gap-3 pt-2">
                <button 
                  (click)="publishNow()" 
                  [disabled]="!postText.trim()"
                  class="flex-1 bg-gradient-to-r from-indigo-600 to-fuchsia-600 hover:from-indigo-500 hover:to-fuchsia-500 disabled:opacity-40 text-white font-medium py-2.5 rounded-lg text-sm transition shadow-lg shadow-indigo-900/30">
                  🚀 Publish Now to All Selected
                </button>
                <button 
                  (click)="schedulePost()" 
                  [disabled]="!postText.trim()"
                  class="bg-slate-800 hover:bg-slate-700 disabled:opacity-40 text-slate-200 font-medium px-5 py-2.5 rounded-lg text-sm transition border border-slate-700">
                  📅 Schedule
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
                    <div class="text-xs text-slate-400">@sgrowing_app • Just now</div>
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
                    <span class="bg-slate-900 px-2 py-0.5 rounded text-slate-400 font-mono text-[10px]">{{ c.platform }}</span>
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
  public postText = 'Supercharge your B2B omnicommunication with autonomous AI agents across WhatsApp, Telegram, X, and Phone. What channel drives the highest ROI for your team?';
  public mediaUrl = 'https://assets.sgrowing.com/banner.png';

  public accounts = signal<SocialAccountUI[]>([
    { id: '1', platform: 'x', name: 'X (Twitter)', handle: '@sgrowing', avatarIcon: '🐦', selected: true },
    { id: '2', platform: 'linkedin', name: 'LinkedIn', handle: 'sgrowing', avatarIcon: '💼', selected: true },
    { id: '3', platform: 'instagram', name: 'Instagram', handle: '@sgrowing.ai', avatarIcon: '📸', selected: true },
    { id: '4', platform: 'facebook', name: 'Facebook', handle: 'SGrowing Page', avatarIcon: '📘', selected: false },
    { id: '5', platform: 'threads', name: 'Threads', handle: '@sgrowing', avatarIcon: '🧵', selected: true },
    { id: '6', platform: 'telegram', name: 'Telegram VIP', handle: '@sgrowing_channel', avatarIcon: '✈️', selected: false },
  ]);

  public comments = signal<PublicCommentUI[]>([
    { id: 'c1', platform: 'Instagram', author: '@tech_founder', postSnippet: 'AI Omnicommunication Launch', commentText: 'How much does the enterprise plan cost for unlimited WhatsApp channels?', deflected: false },
    { id: 'c2', platform: 'X', author: '@growth_lead', postSnippet: 'AI Omnicommunication Launch', commentText: 'Can I test the AI voice dialer with our team demo?', deflected: false },
    { id: 'c3', platform: 'LinkedIn', author: 'Marcus Vance', postSnippet: 'AI Omnicommunication Launch', commentText: 'Interested in the CRM integration with HubSpot. Send link please.', deflected: true }
  ]);

  ngOnInit(): void {}

  public toggleAccount(acc: SocialAccountUI): void {
    acc.selected = !acc.selected;
  }

  public isXSelected(): boolean {
    return !!this.accounts().find(a => a.platform === 'x' && a.selected);
  }

  public publishNow(): void {
    alert(`Published post to ${this.accounts().filter(a => a.selected).map(a => a.name).join(', ')} via Outbox!`);
  }

  public schedulePost(): void {
    alert('Post scheduled in SGrowing editorial calendar!');
  }

  public deflectToDm(comment: PublicCommentUI): void {
    comment.deflected = true;
  }
}
