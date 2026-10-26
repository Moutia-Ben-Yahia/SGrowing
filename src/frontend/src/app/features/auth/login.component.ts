import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="min-h-screen w-screen flex items-center justify-center bg-slate-950 text-slate-100 font-sans p-4 relative overflow-hidden">
      <!-- Glow Gradients -->
      <div class="absolute -top-40 -left-40 w-96 h-96 bg-emerald-500/10 rounded-full blur-3xl pointer-events-none"></div>
      <div class="absolute -bottom-40 -right-40 w-96 h-96 bg-teal-500/10 rounded-full blur-3xl pointer-events-none"></div>

      <div class="w-full max-w-md bg-slate-900/90 border border-slate-800 rounded-2xl shadow-2xl p-8 backdrop-blur-xl relative z-10">
        <!-- Brand Header -->
        <div class="flex items-center gap-3 mb-8">
          <div class="h-10 w-10 rounded-xl bg-gradient-to-tr from-emerald-500 to-teal-400 flex items-center justify-center font-bold text-white shadow-lg shadow-emerald-500/25 text-lg">
            ⚡
          </div>
          <div>
            <h1 class="text-xl font-bold tracking-tight text-white">SGrowing OS</h1>
            <p class="text-xs text-slate-400">Enterprise AI Omnicommunication & Commerce</p>
          </div>
        </div>

        <!-- Mode Switcher -->
        <div class="flex bg-slate-950/60 p-1 rounded-xl border border-slate-800/80 mb-6">
          <button
            type="button"
            (click)="mode.set('login')"
            [class.bg-slate-800]="mode() === 'login'"
            [class.text-white]="mode() === 'login'"
            class="flex-1 py-2 text-xs font-semibold rounded-lg text-slate-400 transition">
            Sign In
          </button>
          <button
            type="button"
            (click)="mode.set('register')"
            [class.bg-slate-800]="mode() === 'register'"
            [class.text-white]="mode() === 'register'"
            class="flex-1 py-2 text-xs font-semibold rounded-lg text-slate-400 transition">
            Create Workspace
          </button>
        </div>

        <!-- Error Message -->
        @if (errorMessage()) {
          <div class="mb-5 p-3 rounded-xl bg-rose-500/10 border border-rose-500/20 text-rose-400 text-xs flex items-center gap-2">
            <span>⚠️</span>
            <span>{{ errorMessage() }}</span>
          </div>
        }

        <!-- Login Form -->
        @if (mode() === 'login') {
          <form (ngSubmit)="handleLogin()" class="space-y-4">
            <div>
              <label class="block text-xs font-medium text-slate-300 mb-1.5">Work Email</label>
              <input
                type="email"
                [(ngModel)]="email"
                name="email"
                required
                placeholder="operator@sgrowing.io"
                class="w-full px-3.5 py-2.5 bg-slate-950 border border-slate-800 rounded-xl text-xs text-white placeholder-slate-500 focus:outline-none focus:border-emerald-500 transition" />
            </div>

            <div>
              <label class="block text-xs font-medium text-slate-300 mb-1.5">Password</label>
              <input
                type="password"
                [(ngModel)]="password"
                name="password"
                required
                placeholder="••••••••••••"
                class="w-full px-3.5 py-2.5 bg-slate-950 border border-slate-800 rounded-xl text-xs text-white placeholder-slate-500 focus:outline-none focus:border-emerald-500 transition" />
            </div>

            <button
              type="submit"
              [disabled]="isLoading()"
              class="w-full py-2.5 px-4 bg-gradient-to-r from-emerald-500 to-teal-500 hover:from-emerald-400 hover:to-teal-400 text-white font-semibold text-xs rounded-xl shadow-lg shadow-emerald-500/20 transition disabled:opacity-50">
              {{ isLoading() ? 'Authenticating...' : 'Sign In to Workspace' }}
            </button>

            <!-- Quick Demo Fill -->
            <div class="pt-2">
              <button
                type="button"
                (click)="fillDemoAdmin()"
                class="w-full text-center text-[11px] text-slate-500 hover:text-slate-300 transition">
                ⚡ Use pre-seeded Demo Admin
              </button>
            </div>
          </form>
        }

        <!-- Register Form -->
        @if (mode() === 'register') {
          <form (ngSubmit)="handleRegister()" class="space-y-3.5">
            <div>
              <label class="block text-xs font-medium text-slate-300 mb-1">Organization Name</label>
              <input
                type="text"
                [(ngModel)]="orgName"
                name="orgName"
                required
                placeholder="Atlas Trading Corp"
                class="w-full px-3.5 py-2 bg-slate-950 border border-slate-800 rounded-xl text-xs text-white placeholder-slate-500 focus:outline-none focus:border-emerald-500 transition" />
            </div>

            <div>
              <label class="block text-xs font-medium text-slate-300 mb-1">Your Full Name</label>
              <input
                type="text"
                [(ngModel)]="fullName"
                name="fullName"
                required
                placeholder="Sarah Mansour"
                class="w-full px-3.5 py-2 bg-slate-950 border border-slate-800 rounded-xl text-xs text-white placeholder-slate-500 focus:outline-none focus:border-emerald-500 transition" />
            </div>

            <div>
              <label class="block text-xs font-medium text-slate-300 mb-1">Work Email</label>
              <input
                type="email"
                [(ngModel)]="email"
                name="email"
                required
                placeholder="sarah@atlastrading.com"
                class="w-full px-3.5 py-2 bg-slate-950 border border-slate-800 rounded-xl text-xs text-white placeholder-slate-500 focus:outline-none focus:border-emerald-500 transition" />
            </div>

            <div>
              <label class="block text-xs font-medium text-slate-300 mb-1">Master Password (min 8 chars)</label>
              <input
                type="password"
                [(ngModel)]="password"
                name="password"
                required
                minlength="8"
                placeholder="••••••••••••"
                class="w-full px-3.5 py-2 bg-slate-950 border border-slate-800 rounded-xl text-xs text-white placeholder-slate-500 focus:outline-none focus:border-emerald-500 transition" />
            </div>

            <button
              type="submit"
              [disabled]="isLoading()"
              class="w-full py-2.5 px-4 bg-gradient-to-r from-emerald-500 to-teal-500 hover:from-emerald-400 hover:to-teal-400 text-white font-semibold text-xs rounded-xl shadow-lg shadow-emerald-500/20 transition disabled:opacity-50">
              {{ isLoading() ? 'Creating Workspace...' : 'Create Enterprise Organization' }}
            </button>
          </form>
        }
      </div>
    </div>
  `
})
export class LoginComponent {
  private authService = inject(AuthService);
  private router = inject(Router);

  public mode = signal<'login' | 'register'>('login');
  public email = '';
  public password = '';
  public orgName = '';
  public fullName = '';
  public errorMessage = signal<string | null>(null);
  public isLoading = signal<boolean>(false);

  public fillDemoAdmin(): void {
    this.email = 'admin@sgrowing.io';
    this.password = 'SecurePassword999!';
  }

  public handleLogin(): void {
    if (!this.email || !this.password) return;
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.authService.login({ email: this.email, password: this.password }).subscribe({
      next: () => {
        this.isLoading.set(false);
        this.router.navigate(['/']);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.errorMessage.set(err.error?.error || 'Authentication failed. Please verify credentials.');
      }
    });
  }

  public handleRegister(): void {
    if (!this.orgName || !this.fullName || !this.email || !this.password) return;
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.authService.register({
      organizationName: this.orgName,
      fullName: this.fullName,
      email: this.email,
      password: this.password
    }).subscribe({
      next: () => {
        this.isLoading.set(false);
        this.router.navigate(['/']);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.errorMessage.set(err.error?.error || 'Registration failed. Please check inputs.');
      }
    });
  }
}
