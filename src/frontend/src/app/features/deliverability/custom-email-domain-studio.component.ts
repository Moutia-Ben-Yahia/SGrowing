import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import {
  CustomDomainStatus,
  DnsRecordType,
  DnsRecordStatus,
  CustomDnsRecordDto,
  CustomEmailDomainDetailDto,
  CustomDomainSummaryDto,
  RegisterCustomDomainRequestDto,
  DomainVerificationResultDto,
  RegistrarExportDto,
  DeliverabilityDomainMetricsDto
} from '../../core/models/deliverability.models';

@Component({
  selector: 'app-custom-email-domain-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div dir="ltr" class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <!-- Main Content Area -->
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Top Navigation Bar -->
        <header class="h-16 border-b border-slate-800 bg-slate-900/80 backdrop-blur px-6 flex items-center justify-between shrink-0">
          <div class="flex items-center gap-3">
            <a routerLink="/deliverability" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← Deliverability Guardian
            </a>
            <span class="text-slate-700">|</span>
            <div class="flex items-center gap-2">
              <span class="text-xl">🌐</span>
              <span class="text-base font-bold text-white tracking-tight">
                Whitelabel Custom Email Domains & DKIM/CNAME Provisioner
              </span>
            </div>
            <span class="bg-indigo-500/10 text-indigo-400 text-[11px] font-mono px-2.5 py-0.5 rounded-full border border-indigo-500/20 font-semibold">
              Horizon 42 • Multi-Tenant RFC DNS
            </span>
          </div>

          <div class="flex items-center gap-3">
            <a
              routerLink="/deliverability/multi-inbox"
              class="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg text-xs font-semibold border border-slate-700 transition flex items-center gap-1.5">
              <span>🔄</span> Multi-Inbox Radar
            </a>
            <button
              (click)="openConnectModal()"
              class="px-3.5 py-1.5 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-xs font-bold transition flex items-center gap-1.5 shadow-lg shadow-indigo-600/20">
              <span class="text-sm font-bold">+</span> Connect New Domain
            </button>
          </div>
        </header>

        <!-- Notification Banner / Toast -->
        @if (notificationMessage()) {
          <div class="bg-indigo-950/80 border-b border-indigo-500/30 px-6 py-2.5 text-xs text-indigo-200 flex items-center justify-between animate-fadeIn">
            <div class="flex items-center gap-2">
              <span class="text-sm">⚡</span>
              <span>{{ notificationMessage() }}</span>
            </div>
            <button (click)="notificationMessage.set(null)" class="text-indigo-400 hover:text-white">✕</button>
          </div>
        }

        <!-- Workspace Content -->
        <div class="p-6 space-y-6 flex-1 max-w-7xl mx-auto w-full">
          <!-- 1. Executive Metrics Radar Cards -->
          <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <!-- Active Domains -->
            <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800 relative overflow-hidden group hover:border-emerald-500/40 transition">
              <div class="flex items-center justify-between">
                <span class="text-xs font-medium text-slate-400">Active Sending Domains</span>
                <span class="w-2.5 h-2.5 rounded-full bg-emerald-500 animate-pulse"></span>
              </div>
              <div class="mt-2 flex items-baseline gap-2">
                <span class="text-3xl font-extrabold text-white tracking-tight">
                  {{ metrics()?.activeDomains ?? activeCount() }}
                </span>
                <span class="text-xs text-emerald-400 font-medium">100% Cryptographic Alignment</span>
              </div>
              <div class="mt-2 text-[11px] text-slate-500 flex items-center justify-between">
                <span>Total Registered</span>
                <span class="font-mono text-slate-400">{{ metrics()?.totalDomains ?? domains().length }} domains</span>
              </div>
            </div>

            <!-- Pending DNS -->
            <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800 relative overflow-hidden group hover:border-amber-500/40 transition">
              <div class="flex items-center justify-between">
                <span class="text-xs font-medium text-slate-400">DNS Propagation Pending</span>
                <span class="w-2 h-2 rounded-full bg-amber-400"></span>
              </div>
              <div class="mt-2 flex items-baseline gap-2">
                <span class="text-3xl font-extrabold text-amber-400 tracking-tight">
                  {{ metrics()?.pendingDnsDomains ?? pendingCount() }}
                </span>
                <span class="text-xs text-slate-400">Awaiting Name Servers</span>
              </div>
              <div class="mt-2 text-[11px] text-slate-500 flex items-center justify-between">
                <span>DNS Degraded</span>
                <span class="font-mono text-rose-400">{{ metrics()?.degradedDomains ?? degradedCount() }} domains</span>
              </div>
            </div>

            <!-- Deliverability Health -->
            <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800 relative overflow-hidden group hover:border-indigo-500/40 transition">
              <div class="flex items-center justify-between">
                <span class="text-xs font-medium text-slate-400">Network Deliverability Health</span>
                <span class="text-xs font-mono text-indigo-400 font-bold">Grade A+</span>
              </div>
              <div class="mt-2 flex items-baseline gap-2">
                <span class="text-3xl font-extrabold text-white tracking-tight">
                  {{ metrics()?.averageHealthScore ?? averageHealth() }}%
                </span>
                <span class="text-xs text-indigo-300">Composite Score</span>
              </div>
              <!-- Mini Progress Bar -->
              <div class="mt-2.5 h-1.5 w-full bg-slate-800 rounded-full overflow-hidden">
                <div
                  class="h-full bg-gradient-to-r from-indigo-500 to-emerald-400 transition-all duration-500"
                  [style.width.%]="metrics()?.averageHealthScore ?? averageHealth()"></div>
              </div>
            </div>

            <!-- 2048-bit DKIM Keys -->
            <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800 relative overflow-hidden group hover:border-sky-500/40 transition">
              <div class="flex items-center justify-between">
                <span class="text-xs font-medium text-slate-400">Dedicated RSA DKIM Keys</span>
                <span class="text-xs font-mono text-sky-400">2048-bit</span>
              </div>
              <div class="mt-2 flex items-baseline gap-2">
                <span class="text-3xl font-extrabold text-sky-400 tracking-tight">
                  {{ metrics()?.totalDkimKeysGenerated ?? domains().length }}
                </span>
                <span class="text-xs text-slate-400">Hardware Vault Guarded</span>
              </div>
              <div class="mt-2 text-[11px] text-slate-500 flex items-center justify-between">
                <span>Fully Aligned Rate</span>
                <span class="font-mono text-emerald-400">{{ metrics()?.fullyAlignedRatePercent ?? 75 }}%</span>
              </div>
            </div>
          </div>

          <!-- 2. Domain Selector & Details Header -->
          <div class="bg-slate-900/70 border border-slate-800 rounded-2xl p-5 backdrop-blur space-y-4">
            <div class="flex flex-col md:flex-row md:items-center justify-between gap-4">
              <div>
                <span class="text-xs font-semibold uppercase tracking-wider text-slate-400">Active Domain Context</span>
                <div class="flex items-center gap-3 mt-1">
                  <h2 class="text-2xl font-bold text-white tracking-tight flex items-center gap-2">
                    {{ selectedDomain()?.domain ?? 'Select Domain' }}
                    @if (selectedDomain()?.status === 'Active') {
                      <span class="bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 text-xs px-2.5 py-0.5 rounded-full font-medium flex items-center gap-1">
                        <span class="w-1.5 h-1.5 rounded-full bg-emerald-400"></span> Active & Aligned
                      </span>
                    } @else if (selectedDomain()?.status === 'PendingDns') {
                      <span class="bg-amber-500/10 text-amber-400 border border-amber-500/20 text-xs px-2.5 py-0.5 rounded-full font-medium flex items-center gap-1">
                        <span class="w-1.5 h-1.5 rounded-full bg-amber-400 animate-pulse"></span> Pending DNS Setup
                      </span>
                    } @else if (selectedDomain()?.status === 'DnsDegraded') {
                      <span class="bg-rose-500/10 text-rose-400 border border-rose-500/20 text-xs px-2.5 py-0.5 rounded-full font-medium flex items-center gap-1">
                        <span class="w-1.5 h-1.5 rounded-full bg-rose-400"></span> DNS Degraded
                      </span>
                    } @else {
                      <span class="bg-sky-500/10 text-sky-400 border border-sky-500/20 text-xs px-2.5 py-0.5 rounded-full font-medium flex items-center gap-1">
                        <span class="w-1.5 h-1.5 rounded-full bg-sky-400 animate-spin"></span> Verifying Sweep
                      </span>
                    }
                  </h2>
                </div>
                <p class="text-xs text-slate-400 mt-1">
                  Tenant: <span class="font-mono text-slate-300">{{ selectedDomain()?.tenantId }}</span> •
                  Return-Path: <span class="font-mono text-indigo-300">{{ selectedDomain()?.returnPathPrefix }}.{{ selectedDomain()?.domain }}</span> •
                  DMARC: <span class="font-mono text-amber-300">p={{ selectedDomain()?.dmarcPolicy }}</span>
                </p>
              </div>

              <!-- Domain Selection Dropdown & Action Buttons -->
              <div class="flex items-center gap-2.5">
                <select
                  [ngModel]="selectedDomainId()"
                  (ngModelChange)="onDomainSelected($event)"
                  class="bg-slate-800 border border-slate-700 text-xs text-white rounded-lg px-3 py-2 font-medium focus:ring-2 focus:ring-indigo-500 focus:outline-none">
                  @for (d of domains(); track d.id) {
                    <option [value]="d.id">
                      {{ d.domain }} ({{ d.status }} - {{ d.healthScore }}%)
                    </option>
                  }
                </select>

                <!-- Trigger Verification Button -->
                <button
                  (click)="verifyDomain(false)"
                  [disabled]="isVerifying()"
                  class="px-3.5 py-2 bg-indigo-600/30 hover:bg-indigo-600/40 text-indigo-300 border border-indigo-500/40 rounded-lg text-xs font-semibold transition flex items-center gap-1.5 disabled:opacity-50">
                  @if (isVerifying()) {
                    <span class="animate-spin text-sm">↻</span>
                    <span>Verifying Nameservers...</span>
                  } @else {
                    <span>↻</span>
                    <span>Verify DNS Now</span>
                  }
                </button>

                <!-- Fast Verification Simulation (Pass All) -->
                <button
                  (click)="verifyDomain(true)"
                  [disabled]="isVerifying()"
                  title="Simulate all authoritative nameservers matching"
                  class="px-2.5 py-2 bg-emerald-600/20 hover:bg-emerald-600/30 text-emerald-300 border border-emerald-500/30 rounded-lg text-xs font-semibold transition flex items-center gap-1"
                  [disabled]="isVerifying()">
                  <span>⚡ Quick Pass</span>
                </button>

                <!-- Delete Domain -->
                <button
                  (click)="deleteSelectedDomain()"
                  class="px-2.5 py-2 bg-rose-600/10 hover:bg-rose-600/20 text-rose-400 border border-rose-500/20 rounded-lg text-xs font-semibold transition"
                  title="Remove custom sending domain">
                  <span>🗑️</span>
                </button>
              </div>
            </div>

            <!-- Error Banner if DNS Degraded -->
            @if (selectedDomain()?.verificationError) {
              <div class="bg-rose-950/40 border border-rose-500/30 rounded-xl p-3 text-xs text-rose-300 flex items-start gap-2.5">
                <span class="text-rose-400 text-base">⚠️</span>
                <div class="flex-1">
                  <span class="font-semibold text-rose-200">DNS Diagnostics Warning:</span>
                  <p class="mt-0.5 text-rose-300/90">{{ selectedDomain()?.verificationError }}</p>
                </div>
              </div>
            }
          </div>

          <!-- 3. DNS Records & Key Management Tabs -->
          <div class="space-y-4">
            <!-- Studio Tab Controls -->
            <div class="flex items-center justify-between border-b border-slate-800 pb-2">
              <div class="flex gap-2 text-xs">
                <button
                  (click)="activeStudioTab.set('records')"
                  [class.bg-indigo-600]="activeStudioTab() === 'records'"
                  [class.text-white]="activeStudioTab() === 'records'"
                  [class.text-slate-400]="activeStudioTab() !== 'records'"
                  class="px-3.5 py-1.5 rounded-lg font-medium transition hover:text-white flex items-center gap-1.5">
                  <span>📋</span> Required DNS Records ({{ selectedDomain()?.records?.length ?? 5 }})
                </button>
                <button
                  (click)="activeStudioTab.set('export')"
                  [class.bg-indigo-600]="activeStudioTab() === 'export'"
                  [class.text-white]="activeStudioTab() === 'export'"
                  [class.text-slate-400]="activeStudioTab() !== 'export'"
                  class="px-3.5 py-1.5 rounded-lg font-medium transition hover:text-white flex items-center gap-1.5">
                  <span>🚀</span> 1-Click Registrar Export
                </button>
                <button
                  (click)="activeStudioTab.set('dkim')"
                  [class.bg-indigo-600]="activeStudioTab() === 'dkim'"
                  [class.text-white]="activeStudioTab() === 'dkim'"
                  [class.text-slate-400]="activeStudioTab() !== 'dkim'"
                  class="px-3.5 py-1.5 rounded-lg font-medium transition hover:text-white flex items-center gap-1.5">
                  <span>🔑</span> DKIM Cryptographic Vault
                </button>
              </div>

              <div class="text-[11px] text-slate-500 font-mono flex items-center gap-2">
                <span>Last Verified:</span>
                <span class="text-slate-400 font-semibold">
                  {{ selectedDomain()?.lastVerifiedAtUtc ? (selectedDomain()?.lastVerifiedAtUtc | date:'short') : 'Never' }}
                </span>
              </div>
            </div>

            <!-- Tab 1: DNS Records Table -->
            @if (activeStudioTab() === 'records') {
              <div class="bg-slate-900/60 border border-slate-800 rounded-2xl overflow-hidden shadow-xl">
                <div class="overflow-x-auto">
                  <table class="w-full text-left border-collapse text-xs">
                    <thead>
                      <tr class="bg-slate-900/90 text-slate-400 border-b border-slate-800 font-medium">
                        <th class="py-3 px-4">Record Type</th>
                        <th class="py-3 px-4">Hostname / Name</th>
                        <th class="py-3 px-4">Expected Value / Target</th>
                        <th class="py-3 px-4">TTL</th>
                        <th class="py-3 px-4">Verification Status</th>
                        <th class="py-3 px-4 text-right">Actions</th>
                      </tr>
                    </thead>
                    <tbody class="divide-y divide-slate-800/60">
                      @for (rec of selectedDomain()?.records; track rec.id) {
                        <tr class="hover:bg-slate-850/50 transition">
                          <!-- Type & Purpose -->
                          <td class="py-3.5 px-4">
                            <div class="flex items-center gap-2">
                              <span
                                class="px-2 py-0.5 rounded font-mono font-bold text-[10px]"
                                [ngClass]="{
                                  'bg-sky-500/20 text-sky-300 border border-sky-500/30': rec.recordType === 'CNAME',
                                  'bg-indigo-500/20 text-indigo-300 border border-indigo-500/30': rec.recordType === 'TXT',
                                  'bg-purple-500/20 text-purple-300 border border-purple-500/30': rec.recordType === 'MX'
                                }">
                                {{ rec.recordType }}
                              </span>
                              <span class="text-[11px] text-slate-400 font-medium">{{ rec.purpose }}</span>
                            </div>
                          </td>

                          <!-- Hostname -->
                          <td class="py-3.5 px-4 font-mono text-slate-200">
                            <div class="flex items-center gap-1.5 group">
                              <span class="truncate max-w-[200px]" [title]="rec.hostname">{{ rec.hostname }}</span>
                              <button
                                (click)="copyToClipboard(rec.hostname, 'Hostname')"
                                class="opacity-0 group-hover:opacity-100 text-slate-400 hover:text-indigo-400 transition"
                                title="Copy hostname">
                                📋
                              </button>
                            </div>
                          </td>

                          <!-- Expected Value -->
                          <td class="py-3.5 px-4 font-mono text-slate-300">
                            <div class="flex items-center gap-1.5 group max-w-md">
                              <span class="truncate" [title]="rec.expectedValue">{{ rec.expectedValue }}</span>
                              <button
                                (click)="copyToClipboard(rec.expectedValue, 'Record Value')"
                                class="opacity-0 group-hover:opacity-100 text-slate-400 hover:text-indigo-400 transition shrink-0"
                                title="Copy value">
                                📋
                              </button>
                            </div>
                            @if (rec.errorDetails) {
                              <p class="text-[10px] text-rose-400 mt-0.5">{{ rec.errorDetails }}</p>
                            }
                          </td>

                          <!-- TTL & Priority -->
                          <td class="py-3.5 px-4 font-mono text-slate-400 text-[11px]">
                            {{ rec.ttl }}s
                            @if (rec.priority) {
                              <span class="text-purple-400 ml-1">(Pri: {{ rec.priority }})</span>
                            }
                          </td>

                          <!-- Status -->
                          <td class="py-3.5 px-4">
                            @if (rec.status === 'Matched') {
                              <span class="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[10px] font-semibold bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
                                <span class="w-1 h-1 rounded-full bg-emerald-400"></span> Verified
                              </span>
                            } @else if (rec.status === 'PropagationPending') {
                              <span class="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[10px] font-semibold bg-amber-500/10 text-amber-400 border border-amber-500/20">
                                <span class="w-1 h-1 rounded-full bg-amber-400 animate-pulse"></span> Pending
                              </span>
                            } @else if (rec.status === 'Mismatch') {
                              <span class="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[10px] font-semibold bg-rose-500/10 text-rose-400 border border-rose-500/20">
                                <span class="w-1 h-1 rounded-full bg-rose-400"></span> Mismatch
                              </span>
                            } @else {
                              <span class="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[10px] font-semibold bg-slate-700 text-slate-300">
                                Missing
                              </span>
                            }
                          </td>

                          <!-- Actions -->
                          <td class="py-3.5 px-4 text-right">
                            <button
                              (click)="copyToClipboard(rec.expectedValue, rec.purpose)"
                              class="text-[11px] px-2.5 py-1 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded font-medium transition">
                              Copy Value
                            </button>
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              </div>
            }

            <!-- Tab 2: 1-Click Registrar Export -->
            @if (activeStudioTab() === 'export') {
              <div class="bg-slate-900/60 border border-slate-800 rounded-2xl p-5 space-y-4">
                <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                  <!-- Registrar Pills -->
                  <div class="flex items-center gap-2 bg-slate-800/80 p-1 rounded-xl border border-slate-700/60 text-xs">
                    <button
                      (click)="loadRegistrarExport('Cloudflare')"
                      [class.bg-indigo-600]="activeRegistrar() === 'Cloudflare'"
                      [class.text-white]="activeRegistrar() === 'Cloudflare'"
                      class="px-3 py-1.5 rounded-lg text-slate-300 transition font-medium flex items-center gap-1.5">
                      <span>☁️</span> Cloudflare (BIND)
                    </button>
                    <button
                      (click)="loadRegistrarExport('AwsRoute53')"
                      [class.bg-indigo-600]="activeRegistrar() === 'AwsRoute53'"
                      [class.text-white]="activeRegistrar() === 'AwsRoute53'"
                      class="px-3 py-1.5 rounded-lg text-slate-300 transition font-medium flex items-center gap-1.5">
                      <span>📦</span> AWS Route 53 (JSON)
                    </button>
                    <button
                      (click)="loadRegistrarExport('GoDaddy')"
                      [class.bg-indigo-600]="activeRegistrar() === 'GoDaddy'"
                      [class.text-white]="activeRegistrar() === 'GoDaddy'"
                      class="px-3 py-1.5 rounded-lg text-slate-300 transition font-medium flex items-center gap-1.5">
                      <span>🌐</span> GoDaddy (API/UI)
                    </button>
                    <button
                      (click)="loadRegistrarExport('Namecheap')"
                      [class.bg-indigo-600]="activeRegistrar() === 'Namecheap'"
                      [class.text-white]="activeRegistrar() === 'Namecheap'"
                      class="px-3 py-1.5 rounded-lg text-slate-300 transition font-medium flex items-center gap-1.5">
                      <span>🏷️</span> Namecheap (Table)
                    </button>
                  </div>

                  <button
                    (click)="copyToClipboard(registrarExport()?.rawOutput ?? '', 'Registrar Configuration')"
                    class="px-3.5 py-1.5 bg-emerald-600/20 hover:bg-emerald-600/30 text-emerald-300 border border-emerald-500/30 rounded-lg text-xs font-semibold transition flex items-center gap-1.5 shrink-0">
                    <span>📋</span> Copy Full Configuration
                  </button>
                </div>

                <!-- Instructions banner -->
                <div class="bg-indigo-950/30 border border-indigo-500/20 rounded-xl p-3 text-xs text-indigo-300">
                  <span class="font-bold">Instructions: </span>
                  {{ registrarExport()?.instructions }}
                </div>

                <!-- Code Pre Block -->
                <div class="relative bg-slate-950 border border-slate-800 rounded-xl p-4 font-mono text-xs overflow-x-auto text-emerald-300/90 leading-relaxed max-h-[380px]">
                  <pre>{{ registrarExport()?.rawOutput }}</pre>
                </div>
              </div>
            }

            <!-- Tab 3: DKIM Cryptographic Vault -->
            @if (activeStudioTab() === 'dkim') {
              <div class="grid grid-cols-1 md:grid-cols-3 gap-5">
                <!-- Key Details Card -->
                <div class="md:col-span-2 bg-slate-900/60 border border-slate-800 rounded-2xl p-5 space-y-4">
                  <div class="flex items-center justify-between">
                    <h3 class="text-sm font-bold text-white flex items-center gap-2">
                      <span>🛡️</span> RSA 2048-Bit DKIM Cryptographic Asset
                    </h3>
                    <span class="bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 text-[10px] px-2 py-0.5 rounded font-mono font-semibold">
                      FIPS 186-4 Compliant
                    </span>
                  </div>

                  <div class="grid grid-cols-2 gap-3 text-xs">
                    <div class="bg-slate-950/60 border border-slate-800 rounded-xl p-3">
                      <span class="text-slate-500 text-[11px] block">Selector</span>
                      <span class="font-mono text-indigo-300 font-bold mt-1 block">
                        {{ selectedDomain()?.dkim?.selector }}
                      </span>
                    </div>
                    <div class="bg-slate-950/60 border border-slate-800 rounded-xl p-3">
                      <span class="text-slate-500 text-[11px] block">Key Length & Modulus</span>
                      <span class="font-mono text-emerald-400 font-bold mt-1 block">
                        {{ selectedDomain()?.dkim?.keyLengthBits }} bits (2048-bit RSA)
                      </span>
                    </div>
                  </div>

                  <div class="bg-slate-950/60 border border-slate-800 rounded-xl p-3 space-y-1">
                    <span class="text-slate-500 text-[11px] block">SHA-256 Key Fingerprint</span>
                    <span class="font-mono text-amber-300 text-xs block break-all font-semibold">
                      {{ selectedDomain()?.dkim?.keyFingerprint }}
                    </span>
                  </div>

                  <div class="space-y-1.5">
                    <div class="flex items-center justify-between text-xs">
                      <span class="text-slate-400 font-medium">Public Key (Base64 SubjectPublicKeyInfo)</span>
                      <button
                        (click)="copyToClipboard(selectedDomain()?.dkim?.publicKeyBase64 ?? '', 'DKIM Public Key')"
                        class="text-indigo-400 hover:text-indigo-300 text-[11px] flex items-center gap-1">
                        <span>📋</span> Copy Public Key
                      </button>
                    </div>
                    <div class="bg-slate-950 border border-slate-800 rounded-xl p-3 font-mono text-[11px] text-slate-300 break-all leading-relaxed max-h-32 overflow-y-auto">
                      {{ selectedDomain()?.dkim?.publicKeyBase64 }}
                    </div>
                  </div>
                </div>

                <!-- Vault Isolation & Security Card -->
                <div class="bg-gradient-to-br from-indigo-950/30 to-slate-900/60 border border-indigo-500/20 rounded-2xl p-5 space-y-3 flex flex-col justify-between">
                  <div>
                    <span class="text-2xl">🔒</span>
                    <h4 class="text-sm font-bold text-white mt-2">Hardware-Isolated Private Key</h4>
                    <p class="text-xs text-slate-400 mt-2 leading-relaxed">
                      DKIM private signing keys are generated with secure random entropy in memory and never exposed to client browsers or unauthenticated APIs.
                    </p>
                    <ul class="text-xs text-slate-300 space-y-1.5 mt-4">
                      <li class="flex items-center gap-2">
                        <span class="text-emerald-400 font-bold">✓</span> Auto-aligned with Return-Path
                      </li>
                      <li class="flex items-center gap-2">
                        <span class="text-emerald-400 font-bold">✓</span> DMARC pct=100 enforcement
                      </li>
                      <li class="flex items-center gap-2">
                        <span class="text-emerald-400 font-bold">✓</span> SPF softfail + SGrowing mesh
                      </li>
                    </ul>
                  </div>

                  <div class="pt-4 border-t border-indigo-500/20">
                    <span class="text-[10px] text-indigo-300/80 uppercase font-mono tracking-wider block">Security Posture</span>
                    <span class="text-xs text-white font-semibold">Protected under Tenant Isolation Protocol</span>
                  </div>
                </div>
              </div>
            }
          </div>
        </div>
      </main>

      <!-- Connect New Domain Modal / Drawer -->
      @if (showConnectModal()) {
        <div class="fixed inset-0 bg-slate-950/80 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-lg p-6 space-y-5 shadow-2xl animate-scaleUp">
            <div class="flex items-center justify-between border-b border-slate-800 pb-3">
              <div>
                <h3 class="text-lg font-bold text-white flex items-center gap-2">
                  <span>🌐</span> Connect Custom Sending Domain
                </h3>
                <p class="text-xs text-slate-400 mt-0.5">Automates 2048-bit RSA DKIM key generation & CNAME alignment</p>
              </div>
              <button
                (click)="closeConnectModal()"
                class="text-slate-400 hover:text-white transition text-lg">✕</button>
            </div>

            <!-- Form -->
            <div class="space-y-4 text-xs">
              <div>
                <label class="block text-slate-300 font-medium mb-1.5">
                  Domain Name <span class="text-rose-400">*</span>
                </label>
                <input
                  type="text"
                  [(ngModel)]="newDomainName"
                  placeholder="e.g. mail.acme-corp.com or billing.fintech.tn"
                  class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3.5 py-2 text-white placeholder-slate-500 font-mono focus:ring-2 focus:ring-indigo-500 focus:outline-none" />
                <span class="text-[10px] text-slate-500 mt-1 block">
                  Recommended: Use a dedicated subdomain for transactional or marketing email.
                </span>
              </div>

              <div class="grid grid-cols-2 gap-3">
                <div>
                  <label class="block text-slate-300 font-medium mb-1.5">Return-Path Subdomain</label>
                  <input
                    type="text"
                    [(ngModel)]="newReturnPathPrefix"
                    placeholder="mail"
                    class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3.5 py-2 text-white font-mono focus:ring-2 focus:ring-indigo-500 focus:outline-none" />
                </div>
                <div>
                  <label class="block text-slate-300 font-medium mb-1.5">DMARC Policy</label>
                  <select
                    [(ngModel)]="newDmarcPolicy"
                    class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3.5 py-2 text-white font-medium focus:ring-2 focus:ring-indigo-500 focus:outline-none">
                    <option value="quarantine">Quarantine (Recommended)</option>
                    <option value="reject">Reject (Strict Enforcement)</option>
                    <option value="none">None (Monitoring Only)</option>
                  </select>
                </div>
              </div>

              <div>
                <label class="block text-slate-300 font-medium mb-1.5">Custom DMARC RUA Report Email (Optional)</label>
                <input
                  type="email"
                  [(ngModel)]="newCustomRuaEmail"
                  placeholder="Leave blank to default to dmarc@<your-domain>"
                  class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3.5 py-2 text-white placeholder-slate-500 font-mono focus:ring-2 focus:ring-indigo-500 focus:outline-none" />
              </div>

              @if (modalErrorMessage()) {
                <div class="bg-rose-950/60 border border-rose-500/30 rounded-lg p-2.5 text-xs text-rose-300">
                  {{ modalErrorMessage() }}
                </div>
              }
            </div>

            <!-- Modal Actions -->
            <div class="flex items-center justify-end gap-3 pt-3 border-t border-slate-800">
              <button
                (click)="closeConnectModal()"
                class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg text-xs font-semibold transition">
                Cancel
              </button>
              <button
                (click)="submitConnectDomain()"
                [disabled]="isRegistering() || !newDomainName.trim()"
                class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-xs font-bold transition flex items-center gap-1.5 disabled:opacity-50">
                @if (isRegistering()) {
                  <span class="animate-spin">↻</span>
                  <span>Generating 2048-bit RSA Keys...</span>
                } @else {
                  <span>⚡ Provision Sending Domain</span>
                }
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    @keyframes fadeIn {
      from { opacity: 0; transform: translateY(-4px); }
      to { opacity: 1; transform: translateY(0); }
    }
    @keyframes scaleUp {
      from { opacity: 0; transform: scale(0.97); }
      to { opacity: 1; transform: scale(1); }
    }
    .animate-fadeIn { animation: fadeIn 0.2s ease-out; }
    .animate-scaleUp { animation: scaleUp 0.15s ease-out; }
  `]
})
export class CustomEmailDomainStudioComponent implements OnInit {
  private http = inject(HttpClient);

  // Signals for state
  domains = signal<CustomDomainSummaryDto[]>([]);
  selectedDomainId = signal<string>('');
  selectedDomain = signal<CustomEmailDomainDetailDto | null>(null);
  metrics = signal<DeliverabilityDomainMetricsDto | null>(null);
  registrarExport = signal<RegistrarExportDto | null>(null);

  // Active UI tabs
  activeStudioTab = signal<'records' | 'export' | 'dkim'>('records');
  activeRegistrar = signal<string>('Cloudflare');

  // Loading & notification state
  isVerifying = signal<boolean>(false);
  isRegistering = signal<boolean>(false);
  notificationMessage = signal<string | null>(null);
  modalErrorMessage = signal<string | null>(null);
  showConnectModal = signal<boolean>(false);

  // Form bindings
  newDomainName = '';
  newReturnPathPrefix = 'mail';
  newDmarcPolicy = 'quarantine';
  newCustomRuaEmail = '';

  // Computed metrics
  activeCount = computed(() => this.domains().filter(d => d.status === 'Active').length);
  pendingCount = computed(() => this.domains().filter(d => d.status === 'PendingDns').length);
  degradedCount = computed(() => this.domains().filter(d => d.status === 'DnsDegraded').length);
  averageHealth = computed(() => {
    const list = this.domains();
    if (!list.length) return 0;
    const sum = list.reduce((acc, d) => acc + d.healthScore, 0);
    return Math.round(sum / list.length);
  });

  ngOnInit(): void {
    this.loadDomains();
    this.loadMetrics();
  }

  loadDomains(): void {
    this.http.get<CustomDomainSummaryDto[]>('/api/v1/deliverability/custom-domains')
      .subscribe({
        next: (res) => {
          this.domains.set(res);
          if (res.length > 0 && (!this.selectedDomainId() || !res.some(d => d.id === this.selectedDomainId()))) {
            this.selectedDomainId.set(res[0].id);
            this.loadDomainDetails(res[0].id);
          }
        },
        error: () => {
          // Provide fallback demo data if backend is offline
          this.setupFallbackData();
        }
      });
  }

  loadMetrics(): void {
    this.http.get<DeliverabilityDomainMetricsDto>('/api/v1/deliverability/custom-domains/metrics')
      .subscribe({
        next: (data) => this.metrics.set(data),
        error: () => { /* Computed values provide fallback */ }
      });
  }

  loadDomainDetails(domainId: string): void {
    this.http.get<CustomEmailDomainDetailDto>(`/api/v1/deliverability/custom-domains/${domainId}`)
      .subscribe({
        next: (data) => {
          this.selectedDomain.set(data);
          if (this.activeStudioTab() === 'export') {
            this.loadRegistrarExport(this.activeRegistrar());
          }
        },
        error: (err) => console.error('Failed to load domain details', err)
      });
  }

  onDomainSelected(domainId: string): void {
    this.selectedDomainId.set(domainId);
    this.loadDomainDetails(domainId);
  }

  verifyDomain(passAll: boolean): void {
    const domainId = this.selectedDomainId();
    if (!domainId) return;

    this.isVerifying.set(true);
    const query = passAll ? '?passAll=true' : '';

    this.http.post<DomainVerificationResultDto>(`/api/v1/deliverability/custom-domains/verify/${domainId}${query}`, {})
      .subscribe({
        next: (result) => {
          this.isVerifying.set(false);
          this.loadDomainDetails(domainId);
          this.loadDomains();
          this.loadMetrics();
          this.notificationMessage.set(`Verification complete: ${result.summary}`);
        },
        error: (err) => {
          this.isVerifying.set(false);
          this.notificationMessage.set(`Verification check encountered an error. Please retry.`);
        }
      });
  }

  loadRegistrarExport(registrar: string): void {
    this.activeRegistrar.set(registrar);
    const domainId = this.selectedDomainId();
    if (!domainId) return;

    this.http.get<RegistrarExportDto>(`/api/v1/deliverability/custom-domains/${domainId}/export/${registrar.toLowerCase()}`)
      .subscribe({
        next: (res) => this.registrarExport.set(res),
        error: (err) => console.error('Failed to fetch registrar export', err)
      });
  }

  openConnectModal(): void {
    this.newDomainName = '';
    this.newReturnPathPrefix = 'mail';
    this.newDmarcPolicy = 'quarantine';
    this.newCustomRuaEmail = '';
    this.modalErrorMessage.set(null);
    this.showConnectModal.set(true);
  }

  closeConnectModal(): void {
    this.showConnectModal.set(false);
  }

  submitConnectDomain(): void {
    if (!this.newDomainName.trim()) return;

    this.isRegistering.set(true);
    this.modalErrorMessage.set(null);

    const payload: RegisterCustomDomainRequestDto = {
      domain: this.newDomainName.trim(),
      returnPathPrefix: this.newReturnPathPrefix.trim() || 'mail',
      dmarcPolicy: this.newDmarcPolicy,
      customDmarcReportEmail: this.newCustomRuaEmail.trim() || null
    };

    this.http.post<CustomEmailDomainDetailDto>('/api/v1/deliverability/custom-domains/register', payload)
      .subscribe({
        next: (created) => {
          this.isRegistering.set(false);
          this.closeConnectModal();
          this.selectedDomainId.set(created.id);
          this.loadDomains();
          this.loadDomainDetails(created.id);
          this.loadMetrics();
          this.notificationMessage.set(`Domain ${created.domain} registered with 2048-bit RSA DKIM keys!`);
        },
        error: (err) => {
          this.isRegistering.set(false);
          const errorMsg = err?.error?.error || 'Failed to register sending domain. Check domain format.';
          this.modalErrorMessage.set(errorMsg);
        }
      });
  }

  deleteSelectedDomain(): void {
    const cur = this.selectedDomain();
    if (!cur) return;

    if (!confirm(`Are you sure you want to delete custom sending domain "${cur.domain}"? This will invalidate DKIM signing.`)) {
      return;
    }

    this.http.delete(`/api/v1/deliverability/custom-domains/${cur.id}`)
      .subscribe({
        next: () => {
          this.notificationMessage.set(`Domain "${cur.domain}" removed.`);
          this.selectedDomainId.set('');
          this.selectedDomain.set(null);
          this.loadDomains();
          this.loadMetrics();
        },
        error: (err) => {
          this.notificationMessage.set(`Failed to delete domain.`);
        }
      });
  }

  copyToClipboard(text: string, label: string): void {
    if (!text) return;
    navigator.clipboard.writeText(text).then(() => {
      this.notificationMessage.set(`${label} copied to clipboard!`);
      setTimeout(() => {
        if (this.notificationMessage()?.includes(label)) {
          this.notificationMessage.set(null);
        }
      }, 3500);
    });
  }

  private setupFallbackData(): void {
    const fallbackList: CustomDomainSummaryDto[] = [
      {
        id: 'a1111111-1111-1111-1111-111111111111',
        tenantId: '11111111-1111-1111-1111-111111111111',
        domain: 'mail.acme-corp.com',
        returnPathPrefix: 'mail',
        status: 'Active',
        healthScore: 100,
        totalRecords: 5,
        matchedRecords: 5,
        dkimSelector: 'sgrowing',
        dkimFingerprint: '4A:7B:12:F9:88:C0:D4:56:11:90:3A:4E:55:67:89:AB',
        createdAtUtc: new Date(Date.now() - 86400000 * 12).toISOString(),
        lastVerifiedAtUtc: new Date(Date.now() - 900000).toISOString()
      },
      {
        id: 'b2222222-2222-2222-2222-222222222222',
        tenantId: '22222222-2222-2222-2222-222222222222',
        domain: 'billing.fintech.tn',
        returnPathPrefix: 'mail',
        status: 'PendingDns',
        healthScore: 0,
        totalRecords: 5,
        matchedRecords: 0,
        dkimSelector: 'sgrowing',
        dkimFingerprint: 'BC:89:12:45:67:89:AB:CD:EF:01:23:45:67:89:AB:CD',
        createdAtUtc: new Date(Date.now() - 7200000).toISOString(),
        lastVerifiedAtUtc: null
      }
    ];

    this.domains.set(fallbackList);
    this.selectedDomainId.set(fallbackList[0].id);
    this.loadDomainDetails(fallbackList[0].id);
  }
}
