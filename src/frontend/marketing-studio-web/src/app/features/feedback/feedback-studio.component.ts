import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { SurveyFormDto, SurveyResponseDto } from '../../core/models/platform.models';

@Component({
  selector: 'app-feedback-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <!-- 1. Left Sidebar Navigation -->
      <aside class="w-64 border-r border-slate-800 bg-slate-900/80 flex flex-col justify-between p-4">
        <div>
          <div class="flex items-center gap-2 mb-6">
            <div class="h-8 w-8 rounded-lg bg-gradient-to-tr from-emerald-500 to-teal-400 flex items-center justify-center font-bold text-white shadow-lg shadow-emerald-500/20">
              ⭐
            </div>
            <div>
              <h1 class="text-sm font-semibold tracking-wide text-white">MarketingStudio</h1>
              <p class="text-xs text-emerald-400 font-medium">Feedback & CSAT</p>
            </div>
          </div>

          <nav class="space-y-1 mb-6">
            <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider px-2">Navigation</span>
            <a routerLink="/" class="w-full flex items-center gap-2 px-3 py-2 rounded-lg text-sm text-slate-400 hover:text-slate-100 hover:bg-slate-800/60 transition">
              <span>📥 Omnichannel Inbox</span>
            </a>
            <a routerLink="/outbound" class="w-full flex items-center gap-2 px-3 py-2 rounded-lg text-sm text-slate-400 hover:text-slate-100 hover:bg-slate-800/60 transition">
              <span>🚀 Autonomous AI SDR</span>
            </a>
            <a routerLink="/journeys" class="w-full flex items-center gap-2 px-3 py-2 rounded-lg text-sm text-slate-400 hover:text-slate-100 hover:bg-slate-800/60 transition">
              <span>⚡ Customer Journeys</span>
            </a>
            <a routerLink="/feedback" class="w-full flex items-center gap-2 px-3 py-2 rounded-lg text-sm bg-slate-800 text-emerald-400 transition font-medium">
              <span>⭐ Feedback & CSAT</span>
            </a>
            <a routerLink="/help" class="w-full flex items-center gap-2 px-3 py-2 rounded-lg text-sm text-slate-400 hover:text-slate-100 hover:bg-slate-800/60 transition">
              <span>📚 Help Center & AI Bot</span>
            </a>
          </nav>

          <!-- Closed-Loop Retention Card -->
          <div class="p-3 bg-gradient-to-b from-slate-800/90 to-slate-900 border border-emerald-500/30 rounded-xl space-y-2">
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold text-emerald-300 flex items-center gap-1.5">
                <span class="h-2 w-2 rounded-full bg-emerald-400"></span>
                Retention Sentinel
              </span>
              <span class="text-[10px] bg-emerald-500/10 text-emerald-300 border border-emerald-500/20 px-1.5 py-0.5 rounded">Formbricks</span>
            </div>
            <p class="text-[11px] text-slate-300 leading-relaxed">
              Auto-flags negative feedback and triggers instant high-priority alerts to prevent churn.
            </p>
          </div>
        </div>
      </aside>

      <!-- 2. Main Content Area -->
      <main class="flex-1 flex flex-col overflow-hidden bg-slate-950">
        <!-- Header -->
        <header class="h-16 border-b border-slate-800 bg-slate-900/60 px-6 flex items-center justify-between">
          <h2 class="text-base font-semibold text-white">Customer Feedback, CSAT & Surveys</h2>
          <div class="flex items-center gap-6 text-xs">
            <div class="flex items-center gap-2">
              <span class="text-slate-400">Average CSAT:</span>
              <span class="text-emerald-400 font-semibold font-mono">4.8 / 5.0</span>
            </div>
            <div class="flex items-center gap-2">
              <span class="text-slate-400">NPS Score:</span>
              <span class="text-cyan-400 font-semibold font-mono">+64</span>
            </div>
          </div>
        </header>

        <!-- Dynamic Grid: Survey Tester & Responses Stream -->
        <div class="flex-1 overflow-y-auto p-6 space-y-6 max-w-6xl">
          <!-- Interactive Feedback Widget Preview -->
          <div class="p-5 bg-slate-900 border border-slate-800 rounded-xl space-y-4">
            <div>
              <h3 class="text-sm font-semibold text-white">Interactive In-Chat Survey Preview</h3>
              <p class="text-xs text-slate-400">This widget appears at the end of resolved WhatsApp or WebChat conversations.</p>
            </div>

            <div class="p-4 bg-slate-950 border border-emerald-500/30 rounded-lg space-y-3 max-w-md">
              <p class="text-xs font-medium text-slate-200">How would you rate your support experience today?</p>
              <div class="flex gap-2">
                @for (star of [1,2,3,4,5]; track star) {
                  <button
                    (click)="selectedScore.set(star)"
                    [class.bg-emerald-500]="selectedScore() >= star"
                    [class.text-slate-950]="selectedScore() >= star"
                    [class.bg-slate-800]="selectedScore() < star"
                    class="h-8 w-8 rounded-lg text-xs font-bold transition flex items-center justify-center">
                    {{ star }}★
                  </button>
                }
              </div>

              <input
                type="text"
                [(ngModel)]="feedbackComment"
                placeholder="Optional feedback..."
                class="w-full bg-slate-900 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-slate-200 placeholder-slate-500 focus:outline-none focus:border-emerald-500"
              />

              <button
                (click)="submitSampleFeedback()"
                class="px-3 py-1.5 bg-emerald-500 hover:bg-emerald-600 text-slate-950 font-semibold text-xs rounded-lg transition">
                Submit Feedback
              </button>
            </div>
          </div>

          <!-- Real-Time Feedback Feed -->
          <div class="space-y-4">
            <h3 class="text-sm font-semibold text-slate-200">Recent Customer Responses</h3>
            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
              @for (r of responses(); track r.id) {
                <div class="p-4 bg-slate-900 border border-slate-800 rounded-xl space-y-2">
                  <div class="flex items-center justify-between">
                    <span class="text-xs font-mono font-bold" [class.text-emerald-400]="r.score >= 4" [class.text-amber-400]="r.score === 3" [class.text-rose-400]="r.score <= 2">
                      Score: {{ r.score }} / 5 ★
                    </span>
                    <span class="text-[10px] px-2 py-0.5 rounded-full font-medium"
                      [class.bg-emerald-500-10]="r.sentiment === 1"
                      [class.text-emerald-400]="r.sentiment === 1"
                      [class.bg-rose-500-10]="r.sentiment === 3"
                      [class.text-rose-400]="r.sentiment === 3">
                      {{ r.sentiment === 1 ? 'Positive' : (r.sentiment === 3 ? 'Negative / At-Risk' : 'Neutral') }}
                    </span>
                  </div>
                  @if (r.feedbackText) {
                    <p class="text-xs text-slate-300 italic">"{{ r.feedbackText }}"</p>
                  }
                  <span class="text-[10px] text-slate-500">{{ r.createdAtUtc | date:'short' }}</span>
                </div>
              }
            </div>
          </div>
        </div>
      </main>
    </div>
  `
})
export class FeedbackStudioComponent implements OnInit {
  private api = inject(ApiService);

  public selectedScore = signal(5);
  public feedbackComment = '';
  public responses = signal<SurveyResponseDto[]>([]);
  private tenantId = '00000000-0000-0000-0000-000000000001';

  ngOnInit() {
    this.loadData();
  }

  private loadData() {
    this.api.getFeedbackResponses(this.tenantId).subscribe({
      next: (res) => {
        if (res && res.length > 0) this.responses.set(res);
        else this.initMockResponses();
      },
      error: () => this.initMockResponses()
    });
  }

  private initMockResponses() {
    this.responses.set([
      {
        id: 'r-1',
        surveyFormId: 'f-1',
        contactId: 'c-1',
        score: 5,
        feedbackText: 'The AI Copilot resolved my delivery question in under a minute. Wonderful!',
        sentiment: 1,
        createdAtUtc: new Date().toISOString()
      },
      {
        id: 'r-2',
        surveyFormId: 'f-1',
        contactId: 'c-2',
        score: 2,
        feedbackText: 'Support was slow to answer during peak hours, feature was broken.',
        sentiment: 3,
        createdAtUtc: new Date(Date.now() - 3600000).toISOString()
      }
    ]);
  }

  public submitSampleFeedback() {
    const score = this.selectedScore();
    const comment = this.feedbackComment;

    this.api.submitFeedback(this.tenantId, '00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000002', score, comment).subscribe({
      next: (res) => {
        this.responses.update(list => [res, ...list]);
        this.feedbackComment = '';
        alert(`Feedback submitted! Sentiment: ${res.sentiment === 1 ? 'Positive' : 'At-Risk'}`);
      },
      error: () => {
        const mock: SurveyResponseDto = {
          id: 'r-' + Date.now(),
          surveyFormId: 'f-1',
          contactId: 'c-mock',
          score: score,
          feedbackText: comment,
          sentiment: score >= 4 ? 1 : 3,
          createdAtUtc: new Date().toISOString()
        };
        this.responses.update(list => [mock, ...list]);
        this.feedbackComment = '';
        alert(`Feedback submitted locally! Sentiment: ${mock.sentiment === 1 ? 'Positive' : 'At-Risk'}`);
      }
    });
  }
}
