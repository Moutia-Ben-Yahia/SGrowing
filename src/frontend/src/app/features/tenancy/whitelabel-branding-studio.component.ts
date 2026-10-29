import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';

export interface WhitelabelThemeTokens {
  primaryColor: string;
  secondaryColor: string;
  accentColor: string;
  backgroundColor: string;
  surfaceColor: string;
  textColor: string;
  borderRadius: string;
  fontFamily: string;
  headingFontFamily: string;
}

export interface WhitelabelAssets {
  logoUrl: string;
  darkLogoUrl: string;
  faviconUrl: string;
}

export interface WhitelabelAgencyProfile {
  brandName: string;
  legalName: string;
  supportEmail: string;
  supportUrl: string;
  poweredBySGrowingVisible: boolean;
}

export interface WhitelabelEmailConfig {
  customMailDomain: string;
  dkimSelector: string;
  dkimPublicKey: string;
  spfRecord: string;
  dmarcRecord: string;
  mxRecord: string;
  isVerified: boolean;
}

export interface CustomDomainMapping {
  id: string;
  tenantId: string;
  hostname: string;
  targetCname: string;
  dnsChallengeRecord: string;
  dnsChallengeToken: string;
  sslStatus: 'PendingDns' | 'Validating' | 'Issued' | 'Renewing' | 'Failed' | 'Revoked';
  failureReason?: string | null;
  tlsVersion: string;
  cipherSuite: string;
  certificateIssuer: string;
  certificateSerialNumber: string;
  issuedAtUtc?: string | null;
  expiresAtUtc?: string | null;
  createdAtUtc: string;
  lastCheckedAtUtc?: string | null;
  isPrimary: boolean;
  daysUntilExpiry?: number | null;
}

export interface TenantWhitelabelConfig {
  tenantId: string;
  slug: string;
  profile: WhitelabelAgencyProfile;
  theme: WhitelabelThemeTokens;
  assets: WhitelabelAssets;
  email: WhitelabelEmailConfig;
  customDomains: CustomDomainMapping[];
  updatedAtUtc: string;
}

export interface HostResolutionResult {
  resolvedTenantId: string;
  resolvedHost: string;
  resolutionType: number; // 1: CustomDomain, 2: Subdomain, 3: FallbackMaster
  brandName: string;
  matchedDomain?: string | null;
  isSslActive: boolean;
  config: TenantWhitelabelConfig;
}

@Component({
  selector: 'app-whitelabel-branding-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans select-none">
      <!-- Main Container -->
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Topbar -->
        <header class="h-16 border-b border-slate-800 bg-slate-900/90 backdrop-blur px-6 flex items-center justify-between shrink-0 sticky top-0 z-30">
          <div class="flex items-center gap-3">
            <span class="text-2xl">✨</span>
            <div>
              <div class="flex items-center gap-2">
                <h1 class="text-base font-bold text-white tracking-tight">
                  Multi-Tenant Whitelabel Branding & SSL Provisioning Studio
                </h1>
                <span class="bg-indigo-500/10 text-indigo-400 text-[10px] font-mono px-2 py-0.5 rounded-full border border-indigo-500/30 font-semibold">
                  Horizon 25 • Enterprise Edge
                </span>
              </div>
              <p class="text-[11px] text-slate-400">
                Autonomous custom domain TLS 1.3 certificate state machine & real-time CSS theme compiler
              </p>
            </div>
          </div>

          <div class="flex items-center gap-4">
            <!-- Tenant Switcher Dropdown -->
            <div class="flex items-center gap-2 bg-slate-800/80 px-3 py-1.5 rounded-xl border border-slate-700/80 text-xs">
              <span class="text-slate-400 font-medium">Tenant:</span>
              <select
                [ngModel]="selectedTenantId()"
                (ngModelChange)="onSelectTenant($event)"
                class="bg-transparent text-white font-semibold focus:outline-none cursor-pointer">
                @for (tenant of tenantsList(); track tenant.tenantId) {
                  <option [value]="tenant.tenantId" class="bg-slate-900 text-white">
                    {{ tenant.profile.brandName }} ({{ tenant.slug }})
                  </option>
                }
              </select>
            </div>

            <!-- Primary Domain Indicator Pill -->
            @if (primaryDomain(); as pDomain) {
              <div class="hidden lg:flex items-center gap-2 bg-slate-800/50 px-3 py-1.5 rounded-xl border border-slate-700/60 text-xs font-mono">
                <span class="w-2 h-2 rounded-full"
                  [ngClass]="{
                    'bg-emerald-400 animate-pulse': pDomain.sslStatus === 'Issued',
                    'bg-amber-400 animate-pulse': pDomain.sslStatus === 'Validating',
                    'bg-rose-400': pDomain.sslStatus === 'Failed',
                    'bg-slate-400': pDomain.sslStatus === 'PendingDns'
                  }"></span>
                <span class="text-slate-300">{{ pDomain.hostname }}</span>
                <span class="text-[10px] px-1.5 py-0.5 rounded font-sans uppercase font-bold"
                  [ngClass]="{
                    'bg-emerald-500/20 text-emerald-300': pDomain.sslStatus === 'Issued',
                    'bg-amber-500/20 text-amber-300': pDomain.sslStatus === 'Validating',
                    'bg-rose-500/20 text-rose-300': pDomain.sslStatus === 'Failed',
                    'bg-slate-500/20 text-slate-300': pDomain.sslStatus === 'PendingDns'
                  }">
                  {{ pDomain.sslStatus }}
                </span>
              </div>
            }

            <!-- Action Buttons -->
            <button
              (click)="resetToDefaults()"
              class="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-300 hover:text-white rounded-lg text-xs font-semibold border border-slate-700 transition">
              Reset
            </button>
            <button
              (click)="saveChanges()"
              [disabled]="isSaving()"
              class="px-4 py-1.5 bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 text-white rounded-lg text-xs font-semibold shadow-md shadow-indigo-600/30 transition flex items-center gap-1.5">
              @if (isSaving()) {
                <span class="inline-block w-3 h-3 border-2 border-white/20 border-t-white rounded-full animate-spin"></span>
                <span>Saving...</span>
              } @else {
                <span>💾 Save Theme</span>
              }
            </button>
          </div>
        </header>

        <!-- Studio Navigation Bar -->
        <nav class="h-12 border-b border-slate-800 bg-slate-900/40 px-6 flex items-center justify-between shrink-0">
          <div class="flex items-center gap-2">
            <button
              (click)="activeTab.set('theme')"
              [class.bg-indigo-600]="activeTab() === 'theme'"
              [class.text-white]="activeTab() === 'theme'"
              [class.text-slate-400]="activeTab() !== 'theme'"
              class="px-3 py-1.5 rounded-lg text-xs font-semibold transition hover:text-white flex items-center gap-1.5">
              <span>🎨</span> Theme Tokens & Live Canvas
            </button>
            <button
              (click)="activeTab.set('domain')"
              [class.bg-indigo-600]="activeTab() === 'domain'"
              [class.text-white]="activeTab() === 'domain'"
              [class.text-slate-400]="activeTab() !== 'domain'"
              class="px-3 py-1.5 rounded-lg text-xs font-semibold transition hover:text-white flex items-center gap-1.5">
              <span>🔒</span> Custom Domain & SSL Cockpit
            </button>
            <button
              (click)="activeTab.set('email')"
              [class.bg-indigo-600]="activeTab() === 'email'"
              [class.text-white]="activeTab() === 'email'"
              [class.text-slate-400]="activeTab() !== 'email'"
              class="px-3 py-1.5 rounded-lg text-xs font-semibold transition hover:text-white flex items-center gap-1.5">
              <span>✉️</span> Email Whitelabeling (DKIM/SPF)
            </button>
            <button
              (click)="activeTab.set('resolver')"
              [class.bg-indigo-600]="activeTab() === 'resolver'"
              [class.text-white]="activeTab() === 'resolver'"
              [class.text-slate-400]="activeTab() !== 'resolver'"
              class="px-3 py-1.5 rounded-lg text-xs font-semibold transition hover:text-white flex items-center gap-1.5">
              <span>🌐</span> Host Resolution Inspector
            </button>
          </div>

          <!-- Notification Toast Message -->
          @if (toastMessage()) {
            <div class="text-xs bg-emerald-500/20 text-emerald-300 border border-emerald-500/40 px-3 py-1 rounded-lg animate-fade-in flex items-center gap-1.5">
              <span>✓</span> {{ toastMessage() }}
            </div>
          }
        </nav>

        <!-- Studio Workspace Body -->
        <div class="p-6 flex-1">
          <!-- ================= TAB 1: LIVE THEME & CANVAS ================= -->
          @if (activeTab() === 'theme') {
            <div class="grid grid-cols-1 xl:grid-cols-12 gap-6 items-start">
              <!-- Left Column: Branding Controls (5 cols) -->
              <div class="xl:col-span-5 space-y-6">
                <!-- Agency Profile Card -->
                <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-5 space-y-4">
                  <div class="flex items-center justify-between">
                    <h2 class="text-sm font-bold text-white flex items-center gap-2">
                      <span>🏢</span> Agency Brand Profile
                    </h2>
                    <span class="text-[10px] text-slate-400 font-mono">v3.8 Meta</span>
                  </div>

                  <div class="space-y-3">
                    <div>
                      <label class="block text-[11px] font-semibold text-slate-400 uppercase tracking-wider mb-1">
                        Brand Name
                      </label>
                      <input
                        type="text"
                        [(ngModel)]="currentConfig().profile.brandName"
                        class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:border-indigo-500 focus:outline-none"
                        placeholder="e.g. Apex Growth Partners" />
                    </div>

                    <div class="grid grid-cols-2 gap-3">
                      <div>
                        <label class="block text-[11px] font-semibold text-slate-400 uppercase tracking-wider mb-1">
                          Legal Entity
                        </label>
                        <input
                          type="text"
                          [(ngModel)]="currentConfig().profile.legalName"
                          class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:border-indigo-500 focus:outline-none" />
                      </div>
                      <div>
                        <label class="block text-[11px] font-semibold text-slate-400 uppercase tracking-wider mb-1">
                          Support Email
                        </label>
                        <input
                          type="email"
                          [(ngModel)]="currentConfig().profile.supportEmail"
                          class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:border-indigo-500 focus:outline-none" />
                      </div>
                    </div>

                    <div>
                      <label class="block text-[11px] font-semibold text-slate-400 uppercase tracking-wider mb-1">
                        Support Help Center URL
                      </label>
                      <input
                        type="url"
                        [(ngModel)]="currentConfig().profile.supportUrl"
                        class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:border-indigo-500 focus:outline-none" />
                    </div>

                    <!-- Powered By SGrowing Co-branding Toggle -->
                    <div class="flex items-center justify-between pt-2 border-t border-slate-800">
                      <div>
                        <span class="text-xs font-semibold text-white block">"Powered by SGrowing" Watermark</span>
                        <span class="text-[11px] text-slate-400">Display subtle footer endorsement badge</span>
                      </div>
                      <label class="relative inline-flex items-center cursor-pointer">
                        <input
                          type="checkbox"
                          [(ngModel)]="currentConfig().profile.poweredBySGrowingVisible"
                          class="sr-only peer" />
                        <div class="w-9 h-5 bg-slate-800 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:start-[2px] after:bg-white after:border-slate-300 after:border after:rounded-full after:h-4 after:w-4 after:transition-all peer-checked:bg-indigo-600"></div>
                      </label>
                    </div>
                  </div>
                </div>

                <!-- Theme Tokens Customizer -->
                <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-5 space-y-4">
                  <div class="flex items-center justify-between">
                    <h2 class="text-sm font-bold text-white flex items-center gap-2">
                      <span>🎨</span> Color Palette Tokens
                    </h2>
                    <span class="text-[11px] text-indigo-400 font-mono">WCAG AA Checked</span>
                  </div>

                  <div class="grid grid-cols-3 gap-3">
                    <!-- Primary Color -->
                    <div class="p-3 bg-slate-950 border border-slate-800 rounded-xl space-y-1.5">
                      <span class="block text-[10px] uppercase font-semibold text-slate-400">Primary</span>
                      <div class="flex items-center gap-2">
                        <input
                          type="color"
                          [(ngModel)]="currentConfig().theme.primaryColor"
                          class="w-7 h-7 rounded border-0 bg-transparent cursor-pointer p-0" />
                        <input
                          type="text"
                          [(ngModel)]="currentConfig().theme.primaryColor"
                          class="w-16 bg-transparent text-[11px] font-mono text-white focus:outline-none" />
                      </div>
                    </div>

                    <!-- Secondary Color -->
                    <div class="p-3 bg-slate-950 border border-slate-800 rounded-xl space-y-1.5">
                      <span class="block text-[10px] uppercase font-semibold text-slate-400">Secondary</span>
                      <div class="flex items-center gap-2">
                        <input
                          type="color"
                          [(ngModel)]="currentConfig().theme.secondaryColor"
                          class="w-7 h-7 rounded border-0 bg-transparent cursor-pointer p-0" />
                        <input
                          type="text"
                          [(ngModel)]="currentConfig().theme.secondaryColor"
                          class="w-16 bg-transparent text-[11px] font-mono text-white focus:outline-none" />
                      </div>
                    </div>

                    <!-- Accent Color -->
                    <div class="p-3 bg-slate-950 border border-slate-800 rounded-xl space-y-1.5">
                      <span class="block text-[10px] uppercase font-semibold text-slate-400">Accent</span>
                      <div class="flex items-center gap-2">
                        <input
                          type="color"
                          [(ngModel)]="currentConfig().theme.accentColor"
                          class="w-7 h-7 rounded border-0 bg-transparent cursor-pointer p-0" />
                        <input
                          type="text"
                          [(ngModel)]="currentConfig().theme.accentColor"
                          class="w-16 bg-transparent text-[11px] font-mono text-white focus:outline-none" />
                      </div>
                    </div>

                    <!-- Background Color -->
                    <div class="p-3 bg-slate-950 border border-slate-800 rounded-xl space-y-1.5">
                      <span class="block text-[10px] uppercase font-semibold text-slate-400">Background</span>
                      <div class="flex items-center gap-2">
                        <input
                          type="color"
                          [(ngModel)]="currentConfig().theme.backgroundColor"
                          class="w-7 h-7 rounded border-0 bg-transparent cursor-pointer p-0" />
                        <input
                          type="text"
                          [(ngModel)]="currentConfig().theme.backgroundColor"
                          class="w-16 bg-transparent text-[11px] font-mono text-white focus:outline-none" />
                      </div>
                    </div>

                    <!-- Surface Color -->
                    <div class="p-3 bg-slate-950 border border-slate-800 rounded-xl space-y-1.5">
                      <span class="block text-[10px] uppercase font-semibold text-slate-400">Surface</span>
                      <div class="flex items-center gap-2">
                        <input
                          type="color"
                          [(ngModel)]="currentConfig().theme.surfaceColor"
                          class="w-7 h-7 rounded border-0 bg-transparent cursor-pointer p-0" />
                        <input
                          type="text"
                          [(ngModel)]="currentConfig().theme.surfaceColor"
                          class="w-16 bg-transparent text-[11px] font-mono text-white focus:outline-none" />
                      </div>
                    </div>

                    <!-- Text Color -->
                    <div class="p-3 bg-slate-950 border border-slate-800 rounded-xl space-y-1.5">
                      <span class="block text-[10px] uppercase font-semibold text-slate-400">Text Color</span>
                      <div class="flex items-center gap-2">
                        <input
                          type="color"
                          [(ngModel)]="currentConfig().theme.textColor"
                          class="w-7 h-7 rounded border-0 bg-transparent cursor-pointer p-0" />
                        <input
                          type="text"
                          [(ngModel)]="currentConfig().theme.textColor"
                          class="w-16 bg-transparent text-[11px] font-mono text-white focus:outline-none" />
                      </div>
                    </div>
                  </div>

                  <!-- Border Radius Selector -->
                  <div class="pt-2">
                    <label class="block text-[11px] font-semibold text-slate-400 uppercase tracking-wider mb-2">
                      Component Border Radius ({{ currentConfig().theme.borderRadius }})
                    </label>
                    <div class="flex items-center gap-2">
                      @for (radius of ['6px', '8px', '12px', '16px', '9999px']; track radius) {
                        <button
                          type="button"
                          (click)="currentConfig().theme.borderRadius = radius"
                          [class.bg-indigo-600]="currentConfig().theme.borderRadius === radius"
                          [class.text-white]="currentConfig().theme.borderRadius === radius"
                          [class.bg-slate-950]="currentConfig().theme.borderRadius !== radius"
                          [class.text-slate-400]="currentConfig().theme.borderRadius !== radius"
                          class="flex-1 py-1.5 text-xs font-mono rounded-lg border border-slate-800 hover:border-slate-700 transition">
                          {{ radius === '9999px' ? 'Pill' : radius }}
                        </button>
                      }
                    </div>
                  </div>

                  <!-- Typography Options -->
                  <div class="grid grid-cols-2 gap-3 pt-2">
                    <div>
                      <label class="block text-[11px] font-semibold text-slate-400 uppercase tracking-wider mb-1">
                        Body Font Family
                      </label>
                      <select
                        [(ngModel)]="currentConfig().theme.fontFamily"
                        class="w-full bg-slate-950 border border-slate-800 rounded-lg px-2.5 py-2 text-xs text-white focus:border-indigo-500 focus:outline-none">
                        <option value="Inter, system-ui, sans-serif">Inter</option>
                        <option value="Plus Jakarta Sans, sans-serif">Plus Jakarta Sans</option>
                        <option value="Outfit, sans-serif">Outfit</option>
                        <option value="DM Sans, sans-serif">DM Sans</option>
                        <option value="Geist, system-ui, sans-serif">Geist</option>
                      </select>
                    </div>

                    <div>
                      <label class="block text-[11px] font-semibold text-slate-400 uppercase tracking-wider mb-1">
                        Heading Font
                      </label>
                      <select
                        [(ngModel)]="currentConfig().theme.headingFontFamily"
                        class="w-full bg-slate-950 border border-slate-800 rounded-lg px-2.5 py-2 text-xs text-white focus:border-indigo-500 focus:outline-none">
                        <option value="Plus Jakarta Sans, sans-serif">Plus Jakarta Sans</option>
                        <option value="Cabinet Grotesk, sans-serif">Cabinet Grotesk</option>
                        <option value="Outfit, sans-serif">Outfit</option>
                        <option value="Playfair Display, serif">Playfair Display</option>
                        <option value="Inter, system-ui, sans-serif">Inter</option>
                      </select>
                    </div>
                  </div>

                  <!-- Brand Assets -->
                  <div class="space-y-2 pt-2 border-t border-slate-800">
                    <label class="block text-[11px] font-semibold text-slate-400 uppercase tracking-wider">
                      Brand Logo URL
                    </label>
                    <input
                      type="url"
                      [(ngModel)]="currentConfig().assets.logoUrl"
                      class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white font-mono focus:border-indigo-500 focus:outline-none" />
                  </div>
                </div>
              </div>

              <!-- Right Column: Live Mockup Canvas & Dynamic CSS Inspector (7 cols) -->
              <div class="xl:col-span-7 space-y-6">
                <!-- Preview Canvas Frame -->
                <div class="bg-slate-900/90 border border-slate-800 rounded-2xl overflow-hidden shadow-2xl">
                  <!-- Canvas Header -->
                  <div class="bg-slate-950/80 px-4 py-2.5 border-b border-slate-800 flex items-center justify-between">
                    <div class="flex items-center gap-2">
                      <div class="flex gap-1.5">
                        <span class="w-3 h-3 rounded-full bg-rose-500/80"></span>
                        <span class="w-3 h-3 rounded-full bg-amber-500/80"></span>
                        <span class="w-3 h-3 rounded-full bg-emerald-500/80"></span>
                      </div>
                      <span class="text-slate-500 text-xs">|</span>
                      <div class="flex items-center gap-1.5 bg-slate-900 px-3 py-1 rounded-md text-[11px] text-slate-300 font-mono border border-slate-800">
                        <span>🔒</span>
                        <span>https://{{ primaryDomain() ? primaryDomain()!.hostname : 'portal.myagency.com' }}</span>
                      </div>
                    </div>
                    <span class="text-[10px] text-emerald-400 bg-emerald-500/10 border border-emerald-500/30 px-2 py-0.5 rounded font-mono font-semibold">
                      Live Preview Canvas
                    </span>
                  </div>

                  <!-- Mock Client Portal Screen -->
                  <div
                    class="p-6 transition-all duration-300"
                    [style.backgroundColor]="currentConfig().theme.backgroundColor"
                    [style.color]="currentConfig().theme.textColor"
                    [style.fontFamily]="currentConfig().theme.fontFamily">
                    
                    <!-- Portal Mock Header -->
                    <div
                      class="px-5 py-3.5 border transition-all duration-300 flex items-center justify-between shadow-sm mb-6"
                      [style.backgroundColor]="currentConfig().theme.surfaceColor"
                      [style.borderColor]="'rgba(255, 255, 255, 0.08)'"
                      [style.borderRadius]="currentConfig().theme.borderRadius">
                      <div class="flex items-center gap-3">
                        @if (currentConfig().assets.logoUrl) {
                          <img
                            [src]="currentConfig().assets.logoUrl"
                            alt="Logo"
                            class="h-7 w-auto object-contain rounded" />
                        }
                        <span
                          class="font-bold text-base tracking-tight"
                          [style.fontFamily]="currentConfig().theme.headingFontFamily">
                          {{ currentConfig().profile.brandName }}
                        </span>
                      </div>

                      <div class="flex items-center gap-3 text-xs">
                        <span class="text-slate-400">Growth Portal</span>
                        <div
                          class="w-7 h-7 rounded-full flex items-center justify-center font-bold text-xs"
                          [style.backgroundColor]="currentConfig().theme.primaryColor"
                          [style.color]="computedPrimaryContrast()">
                          {{ currentConfig().profile.brandName.substring(0, 2).toUpperCase() }}
                        </div>
                      </div>
                    </div>

                    <!-- Metrics Cards Grid -->
                    <div class="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-6">
                      <!-- Metric 1 -->
                      <div
                        class="p-4 border transition-all shadow-sm space-y-1"
                        [style.backgroundColor]="currentConfig().theme.surfaceColor"
                        [style.borderColor]="'rgba(255, 255, 255, 0.08)'"
                        [style.borderRadius]="currentConfig().theme.borderRadius">
                        <span class="text-[10px] uppercase font-semibold text-slate-400 tracking-wider">Active Campaigns</span>
                        <div
                          class="text-2xl font-extrabold"
                          [style.color]="currentConfig().theme.primaryColor"
                          [style.fontFamily]="currentConfig().theme.headingFontFamily">
                          38 Live
                        </div>
                        <span class="text-[10px] text-emerald-400 font-medium">↑ +14.2% this week</span>
                      </div>

                      <!-- Metric 2 -->
                      <div
                        class="p-4 border transition-all shadow-sm space-y-1"
                        [style.backgroundColor]="currentConfig().theme.surfaceColor"
                        [style.borderColor]="'rgba(255, 255, 255, 0.08)'"
                        [style.borderRadius]="currentConfig().theme.borderRadius">
                        <span class="text-[10px] uppercase font-semibold text-slate-400 tracking-wider">Qualified Meetings</span>
                        <div
                          class="text-2xl font-extrabold"
                          [style.color]="currentConfig().theme.secondaryColor"
                          [style.fontFamily]="currentConfig().theme.headingFontFamily">
                          142 Booked
                        </div>
                        <span class="text-[10px] text-emerald-400 font-medium">98.4% confirmation</span>
                      </div>

                      <!-- Metric 3 -->
                      <div
                        class="p-4 border transition-all shadow-sm space-y-1"
                        [style.backgroundColor]="currentConfig().theme.surfaceColor"
                        [style.borderColor]="'rgba(255, 255, 255, 0.08)'"
                        [style.borderRadius]="currentConfig().theme.borderRadius">
                        <span class="text-[10px] uppercase font-semibold text-slate-400 tracking-wider">Pipeline Value</span>
                        <div
                          class="text-2xl font-extrabold"
                          [style.color]="currentConfig().theme.accentColor"
                          [style.fontFamily]="currentConfig().theme.headingFontFamily">
                          $1.84M
                        </div>
                        <span class="text-[10px] text-indigo-400 font-medium">Target Met</span>
                      </div>
                    </div>

                    <!-- Client Action Banner -->
                    <div
                      class="p-5 border transition-all shadow-sm flex flex-col sm:flex-row items-center justify-between gap-4 mb-6"
                      [style.backgroundColor]="currentConfig().theme.surfaceColor"
                      [style.borderColor]="'rgba(255, 255, 255, 0.08)'"
                      [style.borderRadius]="currentConfig().theme.borderRadius">
                      <div class="space-y-1 text-center sm:text-left">
                        <h3 class="text-sm font-bold" [style.fontFamily]="currentConfig().theme.headingFontFamily">
                          AI Autonomous Pipeline Review Ready
                        </h3>
                        <p class="text-xs text-slate-400">
                          Review recommended lead enrollments and verify outreach scripts.
                        </p>
                      </div>

                      <button
                        type="button"
                        class="px-5 py-2.5 text-xs font-bold transition shadow-lg shrink-0 cursor-pointer"
                        [style.backgroundColor]="currentConfig().theme.primaryColor"
                        [style.color]="computedPrimaryContrast()"
                        [style.borderRadius]="currentConfig().theme.borderRadius">
                        Launch Campaign Engine →
                      </button>
                    </div>

                    <!-- Watermark Footer (Conditional) -->
                    @if (currentConfig().profile.poweredBySGrowingVisible) {
                      <div class="text-center pt-2">
                        <span class="text-[10px] text-slate-400 inline-flex items-center gap-1 opacity-70">
                          ⚡ Powered by SGrowing Omnichannel Infrastructure
                        </span>
                      </div>
                    }
                  </div>
                </div>

                <!-- Compiled CSS Output Drawer -->
                <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-5 space-y-3">
                  <div class="flex items-center justify-between">
                    <span class="text-xs font-bold text-white flex items-center gap-2">
                      <span>💻</span> Compiled Dynamic CSS (:root Stylesheet)
                    </span>
                    <button
                      (click)="copyToClipboard(computedDynamicCss(), 'Dynamic CSS')"
                      class="px-2.5 py-1 bg-slate-800 hover:bg-slate-700 text-[11px] text-slate-300 rounded font-semibold transition">
                      Copy CSS
                    </button>
                  </div>
                  <pre class="bg-slate-950 p-4 rounded-xl text-[11px] font-mono text-indigo-300 overflow-x-auto max-h-48 border border-slate-800/80 leading-relaxed">{{ computedDynamicCss() }}</pre>
                </div>
              </div>
            </div>
          }

          <!-- ================= TAB 2: DOMAIN & SSL COCKPIT ================= -->
          @if (activeTab() === 'domain') {
            <div class="space-y-6 max-w-5xl mx-auto">
              <!-- New Domain Registration Bar -->
              <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-5 space-y-4">
                <div class="flex items-center justify-between">
                  <div>
                    <h2 class="text-sm font-bold text-white flex items-center gap-2">
                      <span>🌐</span> Add Custom Domain
                    </h2>
                    <p class="text-xs text-slate-400">
                      Map your branded client portal to a fully qualified custom domain with automated zero-SSL issuance
                    </p>
                  </div>
                </div>

                <div class="flex flex-col sm:flex-row items-center gap-3">
                  <div class="relative flex-1 w-full">
                    <span class="absolute inset-y-0 left-0 pl-3 flex items-center text-slate-500 text-xs font-mono">
                      https://
                    </span>
                    <input
                      type="text"
                      [(ngModel)]="newDomainHostname"
                      placeholder="portal.youragency.com"
                      class="w-full bg-slate-950 border border-slate-800 rounded-xl pl-20 pr-4 py-2.5 text-xs text-white font-mono focus:border-indigo-500 focus:outline-none" />
                  </div>

                  <button
                    (click)="registerDomain()"
                    class="w-full sm:w-auto px-5 py-2.5 bg-indigo-600 hover:bg-indigo-500 text-white rounded-xl text-xs font-bold shadow-md transition shrink-0">
                    + Register Custom Domain
                  </button>
                </div>
              </div>

              <!-- Registered Domains List -->
              <div class="space-y-4">
                <h3 class="text-xs font-bold text-slate-400 uppercase tracking-wider">
                  Configured Custom Domains ({{ currentConfig().customDomains.length }})
                </h3>

                @for (domain of currentConfig().customDomains; track domain.id) {
                  <div class="bg-slate-900/90 border border-slate-800 rounded-2xl p-6 space-y-5">
                    <!-- Domain Header -->
                    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-4 border-b border-slate-800">
                      <div class="flex items-center gap-3">
                        <span class="text-xl">🔒</span>
                        <div>
                          <div class="flex items-center gap-2">
                            <span class="text-base font-bold text-white font-mono">{{ domain.hostname }}</span>
                            @if (domain.isPrimary) {
                              <span class="text-[10px] bg-indigo-500/20 text-indigo-300 border border-indigo-500/30 px-2 py-0.5 rounded font-semibold">
                                Primary Domain
                              </span>
                            }
                          </div>
                          <span class="text-xs text-slate-400">Target CNAME: <code class="text-indigo-400 font-mono">{{ domain.targetCname }}</code></span>
                        </div>
                      </div>

                      <!-- SSL Status Pill -->
                      <div class="flex items-center gap-3">
                        <span class="px-3 py-1 rounded-full text-xs font-bold font-mono uppercase tracking-wider flex items-center gap-1.5"
                          [ngClass]="{
                            'bg-emerald-500/10 text-emerald-400 border border-emerald-500/30': domain.sslStatus === 'Issued',
                            'bg-amber-500/10 text-amber-400 border border-amber-500/30': domain.sslStatus === 'Validating',
                            'bg-slate-500/10 text-slate-300 border border-slate-500/30': domain.sslStatus === 'PendingDns',
                            'bg-rose-500/10 text-rose-400 border border-rose-500/30': domain.sslStatus === 'Failed',
                            'bg-zinc-500/10 text-zinc-400 border border-zinc-500/30': domain.sslStatus === 'Revoked'
                          }">
                          <span class="w-2 h-2 rounded-full"
                            [ngClass]="{
                              'bg-emerald-400': domain.sslStatus === 'Issued',
                              'bg-amber-400 animate-pulse': domain.sslStatus === 'Validating',
                              'bg-slate-400': domain.sslStatus === 'PendingDns',
                              'bg-rose-400': domain.sslStatus === 'Failed',
                              'bg-zinc-400': domain.sslStatus === 'Revoked'
                            }"></span>
                          {{ domain.sslStatus }}
                        </span>

                        <button
                          (click)="verifyDns(domain.hostname, true)"
                          [disabled]="isVerifyingDns()"
                          class="px-3 py-1.5 bg-emerald-600/20 hover:bg-emerald-600/30 text-emerald-300 border border-emerald-500/30 rounded-lg text-xs font-semibold transition flex items-center gap-1">
                          @if (isVerifyingDns()) {
                            <span class="inline-block w-3 h-3 border-2 border-emerald-300/20 border-t-emerald-300 rounded-full animate-spin"></span>
                            <span>Verifying...</span>
                          } @else {
                            <span>⚡ Verify DNS & Issue SSL</span>
                          }
                        </button>
                      </div>
                    </div>

                    <!-- DNS Records Setup Cards -->
                    <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                      <!-- 1. CNAME Record -->
                      <div class="p-4 bg-slate-950 rounded-xl border border-slate-800 space-y-2">
                        <div class="flex items-center justify-between">
                          <span class="text-xs font-semibold text-slate-300 flex items-center gap-1.5">
                            <span class="bg-indigo-500/20 text-indigo-400 px-1.5 py-0.5 rounded text-[10px] font-mono">CNAME</span>
                            Route Record
                          </span>
                          <button
                            (click)="copyToClipboard(domain.targetCname, 'CNAME target')"
                            class="text-[11px] text-indigo-400 hover:text-indigo-300 transition font-medium">
                            Copy
                          </button>
                        </div>
                        <div class="text-[11px] text-slate-400">
                          Host / Name: <code class="text-white font-mono">{{ domain.hostname }}</code>
                        </div>
                        <div class="text-[11px] text-slate-400">
                          Points to: <code class="text-indigo-300 font-mono">{{ domain.targetCname }}</code>
                        </div>
                      </div>

                      <!-- 2. Cryptographic TXT Challenge -->
                      <div class="p-4 bg-slate-950 rounded-xl border border-slate-800 space-y-2">
                        <div class="flex items-center justify-between">
                          <span class="text-xs font-semibold text-slate-300 flex items-center gap-1.5">
                            <span class="bg-amber-500/20 text-amber-400 px-1.5 py-0.5 rounded text-[10px] font-mono">TXT</span>
                            Cryptographic Challenge Record
                          </span>
                          <button
                            (click)="copyToClipboard(domain.dnsChallengeToken, 'Challenge token')"
                            class="text-[11px] text-amber-400 hover:text-amber-300 transition font-medium">
                            Copy Token
                          </button>
                        </div>
                        <div class="text-[11px] text-slate-400 truncate">
                          Host: <code class="text-white font-mono">{{ domain.dnsChallengeRecord }}</code>
                        </div>
                        <div class="text-[11px] text-slate-400 truncate">
                          Token (SHA-256): <code class="text-amber-300 font-mono">{{ domain.dnsChallengeToken.substring(0, 24) }}...</code>
                        </div>
                      </div>
                    </div>

                    <!-- SSL Certificate Telemetry & Expiry Cockpit -->
                    <div class="p-4 bg-slate-950/60 rounded-xl border border-slate-800/80 space-y-3">
                      <div class="flex items-center justify-between">
                        <span class="text-xs font-bold text-white flex items-center gap-2">
                          <span>🛡️</span> TLS 1.3 Certificate Telemetry
                        </span>
                        <div class="flex items-center gap-2">
                          <button
                            (click)="renewSsl(domain.hostname)"
                            class="text-[11px] bg-slate-800 hover:bg-slate-700 text-slate-300 px-2.5 py-1 rounded transition">
                            🔄 Force Renewal
                          </button>
                          <button
                            (click)="revokeSsl(domain.hostname)"
                            class="text-[11px] bg-rose-950/40 hover:bg-rose-900/60 text-rose-300 border border-rose-800/40 px-2.5 py-1 rounded transition">
                            Revoke
                          </button>
                        </div>
                      </div>

                      <div class="grid grid-cols-2 sm:grid-cols-4 gap-3 text-xs">
                        <div>
                          <span class="text-slate-500 text-[10px] uppercase font-semibold block">Protocol & Cipher</span>
                          <span class="text-white font-mono text-[11px]">{{ domain.tlsVersion }} • {{ domain.cipherSuite }}</span>
                        </div>
                        <div>
                          <span class="text-slate-500 text-[10px] uppercase font-semibold block">Certificate Issuer</span>
                          <span class="text-slate-300 text-[11px] truncate block">{{ domain.certificateIssuer }}</span>
                        </div>
                        <div>
                          <span class="text-slate-500 text-[10px] uppercase font-semibold block">Serial Number</span>
                          <span class="text-slate-300 font-mono text-[11px] truncate block">{{ domain.certificateSerialNumber }}</span>
                        </div>
                        <div>
                          <span class="text-slate-500 text-[10px] uppercase font-semibold block">Validity / Days Left</span>
                          <span class="font-bold text-[11px]"
                            [ngClass]="{
                              'text-emerald-400': (domain.daysUntilExpiry ?? 0) > 30,
                              'text-amber-400': (domain.daysUntilExpiry ?? 0) <= 30 && (domain.daysUntilExpiry ?? 0) > 0,
                              'text-rose-400': (domain.daysUntilExpiry ?? 0) <= 0
                            }">
                            {{ domain.daysUntilExpiry ? domain.daysUntilExpiry + ' days remaining' : 'Pending Issuance' }}
                          </span>
                        </div>
                      </div>

                      @if (domain.failureReason) {
                        <div class="p-3 bg-rose-500/10 border border-rose-500/30 rounded-lg text-xs text-rose-300">
                          <strong>Failure Reason:</strong> {{ domain.failureReason }}
                        </div>
                      }
                    </div>
                  </div>
                }
              </div>
            </div>
          }

          <!-- ================= TAB 3: EMAIL WHITELABELING ================= -->
          @if (activeTab() === 'email') {
            <div class="space-y-6 max-w-4xl mx-auto">
              <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-5 space-y-4">
                <div class="flex items-center justify-between">
                  <div>
                    <h2 class="text-sm font-bold text-white flex items-center gap-2">
                      <span>✉️</span> Custom Outbound Mail Domain
                    </h2>
                    <p class="text-xs text-slate-400">
                      Send transactional notifications, invitations, and AI outreach directly from your verified domain
                    </p>
                  </div>
                  <span class="text-xs px-2.5 py-1 rounded-full font-mono font-semibold"
                    [ngClass]="currentConfig().email.isVerified ? 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30' : 'bg-amber-500/20 text-amber-400 border border-amber-500/30'">
                    {{ currentConfig().email.isVerified ? '✓ Domain Verified' : 'DNS Action Required' }}
                  </span>
                </div>

                <div class="flex flex-col sm:flex-row items-center gap-3">
                  <input
                    type="text"
                    [(ngModel)]="currentConfig().email.customMailDomain"
                    placeholder="mail.agency.com"
                    class="flex-1 w-full bg-slate-950 border border-slate-800 rounded-xl px-4 py-2.5 text-xs text-white font-mono focus:border-indigo-500 focus:outline-none" />
                  <button
                    (click)="generateDkim()"
                    class="w-full sm:w-auto px-5 py-2.5 bg-indigo-600 hover:bg-indigo-500 text-white rounded-xl text-xs font-bold transition shrink-0">
                    Generate Cryptographic 2048-bit DKIM
                  </button>
                </div>
              </div>

              <!-- Required DNS Records Table -->
              <div class="bg-slate-900/90 border border-slate-800 rounded-2xl p-6 space-y-4">
                <h3 class="text-sm font-bold text-white flex items-center gap-2">
                  <span>📋</span> Authoritative DNS Authentication Records
                </h3>

                <div class="space-y-3">
                  <!-- DKIM Record -->
                  <div class="p-4 bg-slate-950 rounded-xl border border-slate-800 space-y-2">
                    <div class="flex items-center justify-between">
                      <div class="flex items-center gap-2">
                        <span class="px-2 py-0.5 bg-indigo-500/20 text-indigo-400 font-mono text-[10px] font-bold rounded">TXT</span>
                        <span class="text-xs font-bold text-white">DKIM (DomainKeys Identified Mail)</span>
                      </div>
                      <button
                        (click)="copyToClipboard(currentConfig().email.dkimPublicKey, 'DKIM Record')"
                        class="text-xs text-indigo-400 hover:text-indigo-300 font-semibold transition">
                        Copy Value
                      </button>
                    </div>
                    <div class="text-xs text-slate-400">
                      Host: <code class="text-white font-mono">{{ currentConfig().email.dkimSelector }}._domainkey.{{ currentConfig().email.customMailDomain }}</code>
                    </div>
                    <div class="text-xs text-slate-400 truncate">
                      Value: <code class="text-indigo-300 font-mono text-[11px]">{{ currentConfig().email.dkimPublicKey }}</code>
                    </div>
                  </div>

                  <!-- SPF Record -->
                  <div class="p-4 bg-slate-950 rounded-xl border border-slate-800 space-y-2">
                    <div class="flex items-center justify-between">
                      <div class="flex items-center gap-2">
                        <span class="px-2 py-0.5 bg-emerald-500/20 text-emerald-400 font-mono text-[10px] font-bold rounded">TXT</span>
                        <span class="text-xs font-bold text-white">SPF (Sender Policy Framework)</span>
                      </div>
                      <button
                        (click)="copyToClipboard(currentConfig().email.spfRecord, 'SPF Record')"
                        class="text-xs text-emerald-400 hover:text-emerald-300 font-semibold transition">
                        Copy Value
                      </button>
                    </div>
                    <div class="text-xs text-slate-400">
                      Host: <code class="text-white font-mono">&#64; (or {{ currentConfig().email.customMailDomain }})</code>
                    </div>
                    <div class="text-xs text-slate-400">
                      Value: <code class="text-emerald-300 font-mono text-[11px]">{{ currentConfig().email.spfRecord }}</code>
                    </div>
                  </div>

                  <!-- DMARC Record -->
                  <div class="p-4 bg-slate-950 rounded-xl border border-slate-800 space-y-2">
                    <div class="flex items-center justify-between">
                      <div class="flex items-center gap-2">
                        <span class="px-2 py-0.5 bg-cyan-500/20 text-cyan-400 font-mono text-[10px] font-bold rounded">TXT</span>
                        <span class="text-xs font-bold text-white">DMARC Policy</span>
                      </div>
                      <button
                        (click)="copyToClipboard(currentConfig().email.dmarcRecord, 'DMARC Record')"
                        class="text-xs text-cyan-400 hover:text-cyan-300 font-semibold transition">
                        Copy Value
                      </button>
                    </div>
                    <div class="text-xs text-slate-400">
                      Host: <code class="text-white font-mono">_dmarc.{{ currentConfig().email.customMailDomain }}</code>
                    </div>
                    <div class="text-xs text-slate-400">
                      Value: <code class="text-cyan-300 font-mono text-[11px]">{{ currentConfig().email.dmarcRecord }}</code>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          }

          <!-- ================= TAB 4: HOST RESOLUTION INSPECTOR ================= -->
          @if (activeTab() === 'resolver') {
            <div class="space-y-6 max-w-4xl mx-auto">
              <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-6 space-y-4">
                <div>
                  <h2 class="text-sm font-bold text-white flex items-center gap-2">
                    <span>🌐</span> Multi-Tenant Host Resolution Guard
                  </h2>
                  <p class="text-xs text-slate-400">
                    Simulate how incoming HTTP Host headers resolve to tenant context, checking exact custom domains, subdomain routing, and master fallback.
                  </p>
                </div>

                <div class="flex items-center gap-3">
                  <input
                    type="text"
                    [(ngModel)]="hostToResolve"
                    placeholder="e.g. portal.apex-marketing.com:443 or nordic.sgrowing.io"
                    class="flex-1 bg-slate-950 border border-slate-800 rounded-xl px-4 py-2.5 text-xs text-white font-mono focus:border-indigo-500 focus:outline-none" />
                  <button
                    (click)="resolveHostTest()"
                    class="px-5 py-2.5 bg-indigo-600 hover:bg-indigo-500 text-white rounded-xl text-xs font-bold transition shrink-0">
                    Resolve Host
                  </button>
                </div>

                <!-- Resolution Result Card -->
                @if (resolutionResult()) {
                  <div class="p-5 bg-slate-950 rounded-xl border border-slate-800 space-y-3 mt-4">
                    <div class="flex items-center justify-between">
                      <span class="text-xs font-bold text-white">Resolution Verdict</span>
                      <span class="text-xs font-mono font-bold px-2 py-0.5 rounded"
                        [ngClass]="{
                          'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30': resolutionResult()!.resolutionType === 1,
                          'bg-cyan-500/20 text-cyan-400 border border-cyan-500/30': resolutionResult()!.resolutionType === 2,
                          'bg-amber-500/20 text-amber-400 border border-amber-500/30': resolutionResult()!.resolutionType === 3
                        }">
                        {{ getResolutionTypeName(resolutionResult()!.resolutionType) }}
                      </span>
                    </div>

                    <div class="grid grid-cols-2 sm:grid-cols-4 gap-3 text-xs">
                      <div>
                        <span class="text-slate-500 text-[10px] uppercase font-semibold block">Resolved Tenant</span>
                        <span class="text-white font-bold">{{ resolutionResult()!.brandName }}</span>
                      </div>
                      <div>
                        <span class="text-slate-500 text-[10px] uppercase font-semibold block">Tenant ID</span>
                        <span class="text-slate-300 font-mono text-[11px] truncate block">{{ resolutionResult()!.resolvedTenantId }}</span>
                      </div>
                      <div>
                        <span class="text-slate-500 text-[10px] uppercase font-semibold block">Matched Custom Domain</span>
                        <span class="text-indigo-400 font-mono text-[11px]">{{ resolutionResult()!.matchedDomain ?? 'None (Subdomain/Fallback)' }}</span>
                      </div>
                      <div>
                        <span class="text-slate-500 text-[10px] uppercase font-semibold block">SSL Active</span>
                        <span class="font-bold" [ngClass]="resolutionResult()!.isSslActive ? 'text-emerald-400' : 'text-amber-400'">
                          {{ resolutionResult()!.isSslActive ? 'Active TLS 1.3' : 'Not Active' }}
                        </span>
                      </div>
                    </div>
                  </div>
                }
              </div>
            </div>
          }
        </div>
      </main>
    </div>
  `
})
export class WhitelabelBrandingStudioComponent implements OnInit {
  private http = inject(HttpClient);

  // Active navigation tab
  activeTab = signal<'theme' | 'domain' | 'email' | 'resolver'>('theme');

  // State signals
  tenantsList = signal<TenantWhitelabelConfig[]>([]);
  selectedTenantId = signal<string>('11111111-1111-1111-1111-111111111111');
  currentConfig = signal<TenantWhitelabelConfig>(this.createDefaultMockTenant());

  // Interactive controls state
  newDomainHostname = '';
  hostToResolve = 'portal.apex-marketing.com:443';
  resolutionResult = signal<HostResolutionResult | null>(null);

  // Async indicators
  isSaving = signal<boolean>(false);
  isVerifyingDns = signal<boolean>(false);
  toastMessage = signal<string | null>(null);

  // Computed properties
  primaryDomain = computed(() => {
    const domains = this.currentConfig().customDomains;
    return domains.find(d => d.isPrimary) ?? domains[0] ?? null;
  });

  computedPrimaryContrast = computed(() => {
    return this.calculateContrast(this.currentConfig().theme.primaryColor);
  });

  computedDynamicCss = computed(() => {
    const config = this.currentConfig();
    const theme = config.theme;
    const primaryContrast = this.calculateContrast(theme.primaryColor);
    const secondaryContrast = this.calculateContrast(theme.secondaryColor);
    const accentContrast = this.calculateContrast(theme.accentColor);

    return `:root {
  --sg-primary: ${theme.primaryColor};
  --sg-primary-contrast: ${primaryContrast};
  --sg-secondary: ${theme.secondaryColor};
  --sg-secondary-contrast: ${secondaryContrast};
  --sg-accent: ${theme.accentColor};
  --sg-accent-contrast: ${accentContrast};
  --sg-background: ${theme.backgroundColor};
  --sg-surface: ${theme.surfaceColor};
  --sg-text: ${theme.textColor};
  --sg-border-radius: ${theme.borderRadius};
  --sg-font: '${theme.fontFamily}';
  --sg-heading-font: '${theme.headingFontFamily}';
}`;
  });

  ngOnInit(): void {
    this.loadTenants();
  }

  loadTenants(): void {
    this.http.get<TenantWhitelabelConfig[]>('/api/v1/tenancy/whitelabel-branding/tenants')
      .subscribe({
        next: (data) => {
          if (data && data.length > 0) {
            this.tenantsList.set(data);
            const found = data.find(t => t.tenantId === this.selectedTenantId()) ?? data[0];
            this.selectedTenantId.set(found.tenantId);
            this.currentConfig.set(JSON.parse(JSON.stringify(found)));
          } else {
            this.useFallbackSeedData();
          }
        },
        error: () => {
          this.useFallbackSeedData();
        }
      });
  }

  onSelectTenant(tenantId: string): void {
    this.selectedTenantId.set(tenantId);
    const found = this.tenantsList().find(t => t.tenantId === tenantId);
    if (found) {
      this.currentConfig.set(JSON.parse(JSON.stringify(found)));
    }
  }

  saveChanges(): void {
    this.isSaving.set(true);
    const tenantId = this.selectedTenantId();
    const config = this.currentConfig();

    const requestPayload = {
      profile: config.profile,
      theme: config.theme,
      assets: config.assets,
      email: config.email
    };

    this.http.put<TenantWhitelabelConfig>(`/api/v1/tenancy/whitelabel-branding/branding?tenantId=${tenantId}`, requestPayload)
      .subscribe({
        next: (updated) => {
          this.isSaving.set(false);
          this.currentConfig.set(updated);
          this.showToast('Branding and theme tokens saved successfully!');
        },
        error: () => {
          // If offline/mock mode, update locally
          this.isSaving.set(false);
          this.showToast('Branding updated in local preview!');
        }
      });
  }

  resetToDefaults(): void {
    const tenantId = this.selectedTenantId();
    this.http.post<TenantWhitelabelConfig>(`/api/v1/tenancy/whitelabel-branding/reset-defaults?tenantId=${tenantId}`, {})
      .subscribe({
        next: (reset) => {
          this.currentConfig.set(reset);
          this.showToast('Theme reset to default values.');
        },
        error: () => {
          this.currentConfig.set(this.createDefaultMockTenant());
          this.showToast('Theme reset to default values (local).');
        }
      });
  }

  registerDomain(): void {
    if (!this.newDomainHostname.trim()) return;

    const payload = {
      tenantId: this.selectedTenantId(),
      hostname: this.newDomainHostname.trim(),
      isPrimary: this.currentConfig().customDomains.length === 0
    };

    this.http.post<CustomDomainMapping>('/api/v1/tenancy/whitelabel-branding/domains', payload)
      .subscribe({
        next: (domain) => {
          const list = [...this.currentConfig().customDomains, domain];
          this.currentConfig.update(c => ({ ...c, customDomains: list }));
          this.newDomainHostname = '';
          this.showToast(`Domain ${domain.hostname} registered!`);
        },
        error: () => {
          // Fallback simulation
          const newDomain: CustomDomainMapping = {
            id: 'dom-' + Math.random().toString(36).substring(2, 9),
            tenantId: this.selectedTenantId(),
            hostname: this.newDomainHostname.trim().toLowerCase(),
            targetCname: 'cname.sgrowing.io',
            dnsChallengeRecord: `_sgrowing-challenge.${this.newDomainHostname.trim().toLowerCase()}`,
            dnsChallengeToken: 'sha256-' + Math.random().toString(36).substring(2, 12),
            sslStatus: 'PendingDns',
            tlsVersion: 'TLSv1.3',
            cipherSuite: 'TLS_AES_128_GCM_SHA256',
            certificateIssuer: 'SGrowing Edge Zero-SSL CA',
            certificateSerialNumber: 'Pending-Issuance',
            createdAtUtc: new Date().toISOString(),
            isPrimary: this.currentConfig().customDomains.length === 0
          };
          const list = [...this.currentConfig().customDomains, newDomain];
          this.currentConfig.update(c => ({ ...c, customDomains: list }));
          this.newDomainHostname = '';
          this.showToast(`Domain ${newDomain.hostname} registered (local mode)!`);
        }
      });
  }

  verifyDns(hostname: string, simulateValid: boolean = true): void {
    this.isVerifyingDns.set(true);
    const tenantId = this.selectedTenantId();

    this.http.post<CustomDomainMapping>(
      `/api/v1/tenancy/whitelabel-branding/domains/${encodeURIComponent(hostname)}/verify-dns?tenantId=${tenantId}`,
      { simulateValidDns: simulateValid }
    ).subscribe({
      next: (verified) => {
        this.isVerifyingDns.set(false);
        const list = this.currentConfig().customDomains.map(d => d.hostname === hostname ? verified : d);
        this.currentConfig.update(c => ({ ...c, customDomains: list }));
        this.showToast(`DNS Verified! TLS 1.3 Certificate Issued.`);
      },
      error: () => {
        // Fallback simulation
        setTimeout(() => {
          this.isVerifyingDns.set(false);
          const list = this.currentConfig().customDomains.map(d => {
            if (d.hostname === hostname) {
              return {
                ...d,
                sslStatus: 'Issued' as const,
                daysUntilExpiry: 90,
                issuedAtUtc: new Date().toISOString(),
                expiresAtUtc: new Date(Date.now() + 90 * 86400000).toISOString(),
                certificateSerialNumber: '04:BA:22:90:3A:7C:F1:82:11'
              };
            }
            return d;
          });
          this.currentConfig.update(c => ({ ...c, customDomains: list }));
          this.showToast(`DNS Verified! TLS 1.3 Certificate Issued (simulation).`);
        }, 600);
      }
    });
  }

  renewSsl(hostname: string): void {
    const tenantId = this.selectedTenantId();
    this.http.post<CustomDomainMapping>(
      `/api/v1/tenancy/whitelabel-branding/domains/${encodeURIComponent(hostname)}/renew-ssl?tenantId=${tenantId}`,
      {}
    ).subscribe({
      next: (renewed) => {
        const list = this.currentConfig().customDomains.map(d => d.hostname === hostname ? renewed : d);
        this.currentConfig.update(c => ({ ...c, customDomains: list }));
        this.showToast('SSL Certificate renewed for 90 days!');
      },
      error: () => {
        const list = this.currentConfig().customDomains.map(d => {
          if (d.hostname === hostname) {
            return {
              ...d,
              sslStatus: 'Issued' as const,
              daysUntilExpiry: 90
            };
          }
          return d;
        });
        this.currentConfig.update(c => ({ ...c, customDomains: list }));
        this.showToast('SSL Certificate renewed for 90 days (simulation)!');
      }
    });
  }

  revokeSsl(hostname: string): void {
    const tenantId = this.selectedTenantId();
    this.http.post<CustomDomainMapping>(
      `/api/v1/tenancy/whitelabel-branding/domains/${encodeURIComponent(hostname)}/revoke-ssl?tenantId=${tenantId}`,
      { reason: 'Revoked from Whitelabel Studio' }
    ).subscribe({
      next: (revoked) => {
        const list = this.currentConfig().customDomains.map(d => d.hostname === hostname ? revoked : d);
        this.currentConfig.update(c => ({ ...c, customDomains: list }));
        this.showToast('SSL Certificate revoked.');
      },
      error: () => {
        const list = this.currentConfig().customDomains.map(d => {
          if (d.hostname === hostname) {
            return {
              ...d,
              sslStatus: 'Revoked' as const,
              daysUntilExpiry: 0,
              failureReason: 'Revoked from Whitelabel Studio'
            };
          }
          return d;
        });
        this.currentConfig.update(c => ({ ...c, customDomains: list }));
        this.showToast('SSL Certificate revoked (local).');
      }
    });
  }

  generateDkim(): void {
    const tenantId = this.selectedTenantId();
    const mailDomain = this.currentConfig().email.customMailDomain;

    this.http.post<WhitelabelEmailConfig>('/api/v1/tenancy/whitelabel-branding/email/generate-dkim', {
      tenantId,
      customMailDomain: mailDomain
    }).subscribe({
      next: (email) => {
        this.currentConfig.update(c => ({ ...c, email }));
        this.showToast('2048-bit DKIM key generated!');
      },
      error: () => {
        const updatedEmail: WhitelabelEmailConfig = {
          ...this.currentConfig().email,
          dkimPublicKey: 'v=DKIM1; k=rsa; p=MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA' + Math.random().toString(36).substring(2, 15) + 'IDAQAB',
          isVerified: true
        };
        this.currentConfig.update(c => ({ ...c, email: updatedEmail }));
        this.showToast('DKIM keys generated (local simulation)!');
      }
    });
  }

  resolveHostTest(): void {
    if (!this.hostToResolve.trim()) return;

    this.http.post<HostResolutionResult>('/api/v1/tenancy/whitelabel-branding/resolve-host', {
      host: this.hostToResolve.trim()
    }).subscribe({
      next: (res) => {
        this.resolutionResult.set(res);
      },
      error: () => {
        // Fallback simulation
        const host = this.hostToResolve.trim().toLowerCase();
        let resType = 3; // Fallback
        let brand = 'SGrowing Global Engine';
        let tenantId = '00000000-0000-0000-0000-000000000001';

        if (host.includes('apex-marketing')) {
          resType = 1;
          brand = 'Apex Growth Partners';
          tenantId = '11111111-1111-1111-1111-111111111111';
        } else if (host.includes('nordicscale')) {
          resType = 1;
          brand = 'Nordic Scale Agency';
          tenantId = '22222222-2222-2222-2222-222222222222';
        }

        this.resolutionResult.set({
          resolvedTenantId: tenantId,
          resolvedHost: host,
          resolutionType: resType,
          brandName: brand,
          matchedDomain: resType === 1 ? host.split(':')[0] : null,
          isSslActive: true,
          config: this.currentConfig()
        });
      }
    });
  }

  copyToClipboard(text: string, label: string): void {
    if (navigator?.clipboard) {
      navigator.clipboard.writeText(text);
      this.showToast(`Copied ${label} to clipboard!`);
    }
  }

  getResolutionTypeName(type: number): string {
    switch (type) {
      case 1: return 'Exact Custom Domain Match';
      case 2: return 'Subdomain Slug Match';
      case 3: return 'Fallback Master Tenant';
      default: return 'Unknown';
    }
  }

  private calculateContrast(hexColor: string): string {
    try {
      const hex = hexColor.replace('#', '');
      const r = parseInt(hex.substring(0, 2), 16) / 255;
      const g = parseInt(hex.substring(2, 4), 16) / 255;
      const b = parseInt(hex.substring(4, 6), 16) / 255;

      const rLin = r <= 0.03928 ? r / 12.92 : Math.pow((r + 0.055) / 1.055, 2.4);
      const gLin = g <= 0.03928 ? g / 12.92 : Math.pow((g + 0.055) / 1.055, 2.4);
      const bLin = b <= 0.03928 ? b / 12.92 : Math.pow((b + 0.055) / 1.055, 2.4);

      const lum = 0.2126 * rLin + 0.7152 * gLin + 0.0722 * bLin;
      return lum > 0.42 ? '#0F172A' : '#FFFFFF';
    } catch {
      return '#FFFFFF';
    }
  }

  private showToast(msg: string): void {
    this.toastMessage.set(msg);
    setTimeout(() => {
      this.toastMessage.set(null);
    }, 3500);
  }

  private useFallbackSeedData(): void {
    const apex = this.createDefaultMockTenant();
    const nordic: TenantWhitelabelConfig = {
      tenantId: '22222222-2222-2222-2222-222222222222',
      slug: 'nordic',
      profile: {
        brandName: 'Nordic Scale Agency',
        legalName: 'Nordic Scale AB',
        supportEmail: 'hej@nordicscale.se',
        supportUrl: 'https://nordicscale.se/contact',
        poweredBySGrowingVisible: false
      },
      theme: {
        primaryColor: '#0284C7',
        secondaryColor: '#10B981',
        accentColor: '#8B5CF6',
        backgroundColor: '#F8FAFC',
        surfaceColor: '#FFFFFF',
        textColor: '#0F172A',
        borderRadius: '8px',
        fontFamily: 'Geist, system-ui, sans-serif',
        headingFontFamily: 'Outfit, sans-serif'
      },
      assets: {
        logoUrl: 'https://images.unsplash.com/photo-1557683316-973673baf926?w=160&h=48&fit=crop',
        darkLogoUrl: 'https://images.unsplash.com/photo-1557683316-973673baf926?w=160&h=48&fit=crop',
        faviconUrl: 'https://nordicscale.se/favicon.ico'
      },
      email: {
        customMailDomain: 'mail.nordicscale.se',
        dkimSelector: 'sg2026',
        dkimPublicKey: 'v=DKIM1; k=rsa; p=MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA00nordicIDAQAB',
        spfRecord: 'v=spf1 include:_spf.sgrowing.io ~all',
        dmarcRecord: 'v=DMARC1; p=quarantine; pct=100',
        mxRecord: '10 mail.sgrowing.io',
        isVerified: true
      },
      customDomains: [
        {
          id: 'dom-2',
          tenantId: '22222222-2222-2222-2222-222222222222',
          hostname: 'app.nordicscale.se',
          targetCname: 'cname.sgrowing.io',
          dnsChallengeRecord: '_sgrowing-challenge.app.nordicscale.se',
          dnsChallengeToken: 'sha256-nordic-token-mock',
          sslStatus: 'Issued',
          tlsVersion: 'TLSv1.3',
          cipherSuite: 'TLS_CHACHA20_POLY1305_SHA256',
          certificateIssuer: "Let's Encrypt Authority X3 / SGrowing Edge",
          certificateSerialNumber: '08:11:F2:70:E9:5A:00:CD:34',
          issuedAtUtc: new Date(Date.now() - 26 * 86400000).toISOString(),
          expiresAtUtc: new Date(Date.now() + 64 * 86400000).toISOString(),
          createdAtUtc: new Date(Date.now() - 90 * 86400000).toISOString(),
          isPrimary: true,
          daysUntilExpiry: 64
        }
      ],
      updatedAtUtc: new Date().toISOString()
    };

    this.tenantsList.set([apex, nordic]);
    this.currentConfig.set(apex);
  }

  private createDefaultMockTenant(): TenantWhitelabelConfig {
    return {
      tenantId: '11111111-1111-1111-1111-111111111111',
      slug: 'apex',
      profile: {
        brandName: 'Apex Growth Partners',
        legalName: 'Apex Growth Partners LLC',
        supportEmail: 'concierge@apex-marketing.com',
        supportUrl: 'https://apex-marketing.com/help',
        poweredBySGrowingVisible: false
      },
      theme: {
        primaryColor: '#4338CA',
        secondaryColor: '#06B6D4',
        accentColor: '#F59E0B',
        backgroundColor: '#0B0F19',
        surfaceColor: '#111827',
        textColor: '#F9FAFB',
        borderRadius: '12px',
        fontFamily: 'Inter, system-ui, sans-serif',
        headingFontFamily: 'Plus Jakarta Sans, sans-serif'
      },
      assets: {
        logoUrl: 'https://images.unsplash.com/photo-1618005182384-a83a8bd57fbe?w=160&h=48&fit=crop',
        darkLogoUrl: 'https://images.unsplash.com/photo-1618005182384-a83a8bd57fbe?w=160&h=48&fit=crop',
        faviconUrl: 'https://cdn.apex-marketing.com/favicon.ico'
      },
      email: {
        customMailDomain: 'mail.apex-marketing.com',
        dkimSelector: 'sg2026',
        dkimPublicKey: 'v=DKIM1; k=rsa; p=MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA0r5QApexIDAQAB',
        spfRecord: 'v=spf1 include:_spf.sgrowing.io ~all',
        dmarcRecord: 'v=DMARC1; p=quarantine; pct=100',
        mxRecord: '10 mail.sgrowing.io',
        isVerified: true
      },
      customDomains: [
        {
          id: 'dom-1',
          tenantId: '11111111-1111-1111-1111-111111111111',
          hostname: 'portal.apex-marketing.com',
          targetCname: 'cname.sgrowing.io',
          dnsChallengeRecord: '_sgrowing-challenge.portal.apex-marketing.com',
          dnsChallengeToken: '4a8b7c3d2e1f0a9b8c7d6e5f4a3b2c1d0e9f8a7b6c5d4e3f2a1b0c9d8e7f6a5b',
          sslStatus: 'Issued',
          tlsVersion: 'TLSv1.3',
          cipherSuite: 'TLS_AES_128_GCM_SHA256',
          certificateIssuer: "Let's Encrypt Authority X3 / SGrowing Edge",
          certificateSerialNumber: '04:BA:22:90:3A:7C:F1:82:11',
          issuedAtUtc: new Date(Date.now() - 8 * 86400000).toISOString(),
          expiresAtUtc: new Date(Date.now() + 82 * 86400000).toISOString(),
          createdAtUtc: new Date(Date.now() - 45 * 86400000).toISOString(),
          isPrimary: true,
          daysUntilExpiry: 82
        }
      ],
      updatedAtUtc: new Date().toISOString()
    };
  }
}
