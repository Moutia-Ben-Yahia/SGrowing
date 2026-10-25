import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { CustomerJourneyDto } from '../../core/models/platform.models';

@Component({
  selector: 'app-journey-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <!-- 1. Left Sidebar Navigation -->
      <aside class="w-64 border-r border-slate-800 bg-slate-900/80 flex flex-col justify-between p-4">
        <div>
          <div class="flex items-center gap-2 mb-6">
            <div class="h-8 w-8 rounded-lg bg-gradient-to-tr from-cyan-500 to-blue-600 flex items-center justify-center font-bold text-white shadow-lg shadow-cyan-500/20">
              ⚡
            </div>
            <div>
              <h1 class="text-sm font-semibold tracking-wide text-white">MarketingStudio</h1>
              <p class="text-xs text-cyan-400 font-medium">Customer Journeys</p>
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
            <a routerLink="/journeys" class="w-full flex items-center gap-2 px-3 py-2 rounded-lg text-sm bg-slate-800 text-cyan-400 transition font-medium">
              <span>⚡ Customer Journeys</span>
            </a>
            <a routerLink="/feedback" class="w-full flex items-center gap-2 px-3 py-2 rounded-lg text-sm text-slate-400 hover:text-slate-100 hover:bg-slate-800/60 transition">
              <span>⭐ Feedback & CSAT</span>
            </a>
            <a routerLink="/help" class="w-full flex items-center gap-2 px-3 py-2 rounded-lg text-sm text-slate-400 hover:text-slate-100 hover:bg-slate-800/60 transition">
              <span>📚 Help Center & AI Bot</span>
            </a>
          </nav>

          <!-- Behavioral Event Engine Card -->
          <div class="p-3 bg-gradient-to-b from-slate-800/90 to-slate-900 border border-cyan-500/30 rounded-xl space-y-2">
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold text-cyan-300 flex items-center gap-1.5">
                <span class="h-2 w-2 rounded-full bg-cyan-400 animate-pulse"></span>
                Event Engine Active
              </span>
              <span class="text-[10px] bg-cyan-500/10 text-cyan-300 border border-cyan-500/20 px-1.5 py-0.5 rounded">dittofeed</span>
            </div>
            <p class="text-[11px] text-slate-300 leading-relaxed">
              Listening to real-time events, behavioral branching, and automated multi-channel messaging.
            </p>
          </div>
        </div>

        <div class="border-t border-slate-800 pt-4">
          <button
            (click)="triggerDispatch()"
            [disabled]="isDispatching()"
            class="w-full py-2 px-3 bg-cyan-600 hover:bg-cyan-500 text-white text-xs font-semibold rounded-lg shadow-lg shadow-cyan-500/20 transition flex items-center justify-center gap-2 disabled:opacity-50">
            <span>⚡ Run Journey Dispatch</span>
          </button>
        </div>
      </aside>

      <!-- 2. Main Content Area -->
      <main class="flex-1 flex flex-col overflow-hidden bg-slate-950">
        <!-- Top Stats Header -->
        <header class="h-16 border-b border-slate-800 bg-slate-900/60 px-6 flex items-center justify-between">
          <h2 class="text-base font-semibold text-white">Event-Driven Customer Journeys</h2>
          <div class="flex items-center gap-6 text-xs">
            <div class="flex items-center gap-2">
              <span class="text-slate-400">Active Automations:</span>
              <span class="text-cyan-400 font-semibold font-mono">{{ journeys().length }}</span>
            </div>
            <div class="flex items-center gap-2">
              <span class="text-slate-400">Events Processed:</span>
              <span class="text-emerald-400 font-semibold font-mono">1,842</span>
            </div>
          </div>
        </header>

        <!-- Main Body: Simulator + Journey Canvas -->
        <div class="flex-1 overflow-y-auto p-6 space-y-6 max-w-6xl">
          <!-- Event Ingestion Simulator Card -->
          <div class="p-4 bg-slate-900 border border-slate-800 rounded-xl space-y-3">
            <div class="flex items-center justify-between">
              <div>
                <h3 class="text-sm font-semibold text-white">Live Event Simulator</h3>
                <p class="text-xs text-slate-400">Simulate customer actions to trigger active workflows in real-time.</p>
              </div>
            </div>
            <div class="flex flex-wrap gap-2">
              <button
                (click)="simulateEvent('user.signed_up')"
                class="px-3 py-1.5 bg-slate-800 hover:bg-cyan-500/20 hover:text-cyan-300 border border-slate-700 rounded-lg text-xs transition">
                + Simulate user.signed_up
              </button>
              <button
                (click)="simulateEvent('trial.expiring')"
                class="px-3 py-1.5 bg-slate-800 hover:bg-amber-500/20 hover:text-amber-300 border border-slate-700 rounded-lg text-xs transition">
                + Simulate trial.expiring
              </button>
              <button
                (click)="simulateEvent('cart.abandoned')"
                class="px-3 py-1.5 bg-slate-800 hover:bg-rose-500/20 hover:text-rose-300 border border-slate-700 rounded-lg text-xs transition">
                + Simulate cart.abandoned
              </button>
            </div>
          </div>

          <!-- Journey Visual Canvas List -->
          <div class="space-y-4">
            <h3 class="text-sm font-semibold text-slate-200">Active Lifecycle Journeys</h3>
            @for (j of journeys(); track j.id) {
              <div class="p-5 bg-slate-900 border border-slate-800 rounded-xl space-y-4">
                <div class="flex items-center justify-between">
                  <div class="flex items-center gap-3">
                    <span class="h-9 w-9 rounded-lg bg-cyan-500/10 border border-cyan-500/20 text-cyan-400 flex items-center justify-center font-bold text-sm">
                      ⚡
                    </span>
                    <div>
                      <h4 class="text-sm font-semibold text-white">{{ j.name }}</h4>
                      <p class="text-xs text-cyan-400 font-mono">Trigger: {{ j.triggerEvent }}</p>
                    </div>
                  </div>
                  <span class="px-2.5 py-0.5 rounded-full text-xs font-medium bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
                    Active
                  </span>
                </div>

                <!-- Step Sequence Pipeline -->
                <div class="grid grid-cols-1 md:grid-cols-3 gap-3 pt-2">
                  @for (s of j.steps; track s.stepOrder) {
                    <div class="p-3 bg-slate-950/60 border border-slate-800/80 rounded-lg space-y-1 relative">
                      <div class="flex items-center justify-between text-xs font-semibold text-slate-300">
                        <span>Step {{ s.stepOrder }}</span>
                        <span class="text-cyan-400 text-[10px] font-mono">
                          {{ s.stepType === 1 ? '✉️ Email' : '🟢 WhatsApp' }}
                        </span>
                      </div>
                      <p class="text-xs text-slate-200 font-medium truncate">{{ s.subjectTemplate || 'WhatsApp Direct' }}</p>
                      <p class="text-[11px] text-slate-400 line-clamp-2 leading-relaxed">{{ s.bodyTemplate }}</p>
                    </div>
                  }
                </div>
              </div>
            }
          </div>
        </div>
      </main>
    </div>
  `
})
export class JourneyStudioComponent implements OnInit {
  private api = inject(ApiService);

  public journeys = signal<CustomerJourneyDto[]>([]);
  public isDispatching = signal(false);
  private tenantId = '00000000-0000-0000-0000-000000000001';

  ngOnInit() {
    this.loadData();
  }

  private loadData() {
    this.api.getJourneys(this.tenantId).subscribe({
      next: (res) => {
        if (res && res.length > 0) this.journeys.set(res);
        else this.initMockJourneys();
      },
      error: () => this.initMockJourneys()
    });
  }

  private initMockJourneys() {
    this.journeys.set([
      {
        id: 'j-1',
        tenantId: this.tenantId,
        name: 'New User Onboarding Sequence',
        triggerEvent: 'user.signed_up',
        status: 2,
        createdAtUtc: new Date().toISOString(),
        steps: [
          {
            id: 's-1',
            stepOrder: 1,
            stepType: 1,
            subjectTemplate: 'Welcome to MarketingStudio!',
            bodyTemplate: 'Hi {{first_name}}, welcome to our omnicommunication platform. Let us know if you need setup help!',
            delaySeconds: 0
          },
          {
            id: 's-2',
            stepOrder: 2,
            stepType: 2,
            subjectTemplate: '',
            bodyTemplate: 'Hi {{first_name}}, this is your AI onboarding concierge. Have you connected your WhatsApp number yet?',
            delaySeconds: 86400
          }
        ]
      },
      {
        id: 'j-2',
        tenantId: this.tenantId,
        name: 'Trial Expiration Urgency Workflow',
        triggerEvent: 'trial.expiring',
        status: 2,
        createdAtUtc: new Date().toISOString(),
        steps: [
          {
            id: 's-3',
            stepOrder: 1,
            stepType: 1,
            subjectTemplate: 'Your trial ends in 48 hours',
            bodyTemplate: 'Hi {{first_name}}, upgrade today to keep your WhatsApp channels and AI SDR active without disruption.',
            delaySeconds: 0
          }
        ]
      }
    ]);
  }

  public simulateEvent(eventName: string) {
    this.api.triggerEvent(this.tenantId, '00000000-0000-0000-0000-000000000002', eventName).subscribe({
      next: () => alert(`Event '${eventName}' ingested successfully! Workflow triggered.`),
      error: () => alert(`Event '${eventName}' simulated locally.`)
    });
  }

  public triggerDispatch() {
    this.isDispatching.set(true);
    this.api.dispatchJourneys(this.tenantId).subscribe({
      next: (res) => {
        this.isDispatching.set(false);
        alert(`Dispatched ${res.processed} pending journey messages.`);
      },
      error: () => {
        this.isDispatching.set(false);
        alert('Simulated journey dispatch executed.');
      }
    });
  }
}
