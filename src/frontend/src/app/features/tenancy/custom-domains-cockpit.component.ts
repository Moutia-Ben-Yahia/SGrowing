import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface TenantCustomDomain {
  id: string;
  tenantId: string;
  domainName: string;
  verificationToken: string;
  expectedCname: string;
  isDnsVerified: boolean;
  sslStatus: string;
  sslExpiresAtUtc: string;
  theming: {
    primaryColor: string;
    accentColor: string;
    logoUrl?: string;
    faviconUrl?: string;
    customCss?: string;
    pageTitle: string;
  };
}

@Component({
  selector: 'app-custom-domains-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="custom-domains-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-700/60 pb-6">
        <div>
          <div class="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-indigo-400">
            <span class="inline-block w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
            Tenancy & Global Infrastructure • Horizon 46
          </div>
          <h1 class="text-3xl font-extrabold text-white tracking-tight mt-1">
            White-Label Custom Domains & SSL Engine
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Self-service multi-tenant vanity domains, automated ACME TLS 1.3 lifecycle & branded portal themer.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="toggleRegisterModal()"
            data-testid="open-register-domain-btn"
            class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-sm font-semibold transition-colors flex items-center gap-2 shadow-lg shadow-indigo-600/20"
          >
            <span>+</span> Connect Custom Domain
          </button>
        </div>
      </div>

      <!-- Overview Stats -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Active Domains</div>
          <div class="text-2xl font-bold text-white mt-1" data-testid="stat-active-domains">{{ activeDomainsCount() }}</div>
          <div class="text-emerald-400 text-xs mt-1">100% CNAME Validated</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">SSL Health</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1" data-testid="stat-ssl-health">TLS 1.3 Active</div>
          <div class="text-slate-400 text-xs mt-1">Let's Encrypt Auto-Renew</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Branded Portals</div>
          <div class="text-2xl font-bold text-indigo-400 mt-1">{{ domains().length }}</div>
          <div class="text-slate-400 text-xs mt-1">CSS Token Injected</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">DNS Propagation Rate</div>
          <div class="text-2xl font-bold text-white mt-1">99.8%</div>
          <div class="text-slate-400 text-xs mt-1">Global Anycast SNI</div>
        </div>
      </div>

      <!-- Main Layout: Domains Table & Selected Domain Detail/Branding Preview -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Domains List -->
        <div class="lg:col-span-2 bg-slate-800/80 border border-slate-700/60 rounded-xl overflow-hidden">
          <div class="p-4 border-b border-slate-700/60 flex items-center justify-between">
            <h2 class="text-lg font-bold text-white">Registered Custom Domains</h2>
            <span class="text-xs text-slate-400">{{ domains().length }} managed</span>
          </div>

          <div class="divide-y divide-slate-700/40">
            @for (dom of domains(); track dom.id) {
              <div
                (click)="selectDomain(dom)"
                [class.bg-slate-700/40]="selectedDomain()?.id === dom.id"
                class="p-4 hover:bg-slate-700/30 transition-colors cursor-pointer flex flex-col md:flex-row md:items-center justify-between gap-3"
                [attr.data-testid]="'domain-row-' + dom.id"
              >
                <div>
                  <div class="flex items-center gap-2">
                    <span class="font-semibold text-white">{{ dom.domainName }}</span>
                    <span
                      [class]="dom.isDnsVerified ? 'bg-emerald-900/60 text-emerald-400 border border-emerald-700/60' : 'bg-amber-900/60 text-amber-400 border border-amber-700/60'"
                      class="px-2 py-0.5 rounded text-xs font-medium"
                      [attr.data-testid]="'dns-badge-' + dom.id"
                    >
                      {{ dom.isDnsVerified ? 'DNS Verified' : 'DNS Pending' }}
                    </span>
                    <span
                      class="bg-indigo-900/60 text-indigo-300 border border-indigo-700/60 px-2 py-0.5 rounded text-xs font-medium"
                      [attr.data-testid]="'ssl-badge-' + dom.id"
                    >
                      SSL: {{ dom.sslStatus }}
                    </span>
                  </div>
                  <div class="text-xs text-slate-400 mt-1">
                    Tenant: <span class="text-slate-300 font-mono">{{ dom.tenantId }}</span> • CNAME Target: <span class="text-slate-300 font-mono">{{ dom.expectedCname }}</span>
                  </div>
                </div>

                <div class="flex items-center gap-2 self-end md:self-auto">
                  <button
                    (click)="verifyDns(dom); $event.stopPropagation()"
                    data-testid="verify-dns-btn"
                    class="px-3 py-1.5 bg-slate-700 hover:bg-slate-600 text-slate-200 text-xs font-medium rounded-lg transition-colors"
                  >
                    Check DNS
                  </button>
                  <button
                    (click)="renewSsl(dom); $event.stopPropagation()"
                    data-testid="renew-ssl-btn"
                    class="px-3 py-1.5 bg-indigo-600/80 hover:bg-indigo-600 text-white text-xs font-medium rounded-lg transition-colors"
                  >
                    Renew SSL
                  </button>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Selected Domain Preview / White-label Inspector -->
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 flex flex-col justify-between" data-testid="branding-preview-card">
          @if (selectedDomain(); as active) {
            <div class="space-y-4">
              <div class="flex items-center justify-between border-b border-slate-700/60 pb-3">
                <h3 class="font-bold text-white text-base">White-Label Branding Token</h3>
                <span class="text-xs px-2 py-0.5 rounded bg-slate-700 text-slate-300">Live Preview</span>
              </div>

              <!-- Portal Simulated Canvas -->
              <div
                class="rounded-xl border border-slate-700 p-4 space-y-3 shadow-inner"
                [style.background-color]="'#0f172a'"
                data-testid="portal-preview-frame"
              >
                <div class="flex items-center justify-between pb-2 border-b border-slate-700/60">
                  <div class="flex items-center gap-2">
                    <div
                      class="w-4 h-4 rounded-full"
                      [style.background-color]="active.theming.primaryColor"
                    ></div>
                    <span class="font-bold text-sm text-white">{{ active.theming.pageTitle }}</span>
                  </div>
                  <span class="text-[10px] text-emerald-400 font-mono bg-emerald-950 px-1.5 py-0.5 rounded border border-emerald-800">
                    HTTPS://{{ active.domainName }}
                  </span>
                </div>

                <div class="text-xs text-slate-300">
                  Welcome to the dedicated portal for <strong class="text-white">{{ active.domainName }}</strong>.
                </div>

                <div class="flex gap-2">
                  <button
                    class="px-3 py-1 text-xs font-semibold rounded text-white shadow"
                    [style.background-color]="active.theming.primaryColor"
                  >
                    Primary Action
                  </button>
                  <button
                    class="px-3 py-1 text-xs font-semibold rounded text-slate-900 shadow"
                    [style.background-color]="active.theming.accentColor"
                  >
                    Accent Action
                  </button>
                </div>
              </div>

              <!-- DNS Setup Instructions -->
              <div class="space-y-2 text-xs">
                <div class="font-semibold text-slate-300">Required DNS Records:</div>
                <div class="bg-slate-900/80 p-2.5 rounded-lg border border-slate-800 space-y-1 font-mono text-[11px]">
                  <div class="text-slate-400">Type: <span class="text-white">CNAME</span></div>
                  <div class="text-slate-400">Host: <span class="text-white">{{ active.domainName }}</span></div>
                  <div class="text-slate-400">Value: <span class="text-emerald-400">{{ active.expectedCname }}</span></div>
                </div>
                <div class="bg-slate-900/80 p-2.5 rounded-lg border border-slate-800 space-y-1 font-mono text-[11px]">
                  <div class="text-slate-400">Type: <span class="text-white">TXT (ACME)</span></div>
                  <div class="text-slate-400">Host: <span class="text-white">_acme-challenge.{{ active.domainName }}</span></div>
                  <div class="text-slate-400 truncate">Value: <span class="text-indigo-400">{{ active.verificationToken }}</span></div>
                </div>
              </div>
            </div>

            <div class="mt-4 pt-3 border-t border-slate-700/60 flex items-center justify-between text-xs text-slate-400">
              <span>SSL Valid Until:</span>
              <span class="text-white font-mono">{{ active.sslExpiresAtUtc | date:'mediumDate' }}</span>
            </div>
          } @else {
            <div class="text-center py-12 text-slate-400 text-sm">
              Select a domain to inspect DNS records and live white-label styling.
            </div>
          }
        </div>
      </div>

      <!-- Registration Modal -->
      @if (showRegisterModal()) {
        <div class="fixed inset-0 bg-slate-950/80 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div class="bg-slate-900 border border-slate-700 rounded-2xl max-w-md w-full p-6 space-y-4 shadow-2xl">
            <h3 class="text-lg font-bold text-white">Connect New Custom Domain</h3>
            <p class="text-xs text-slate-400">
              Enter the vanity domain you wish to map. DNS verification tokens and ACME TLS certificates will be generated automatically.
            </p>

            <div class="space-y-3 text-xs">
              <div>
                <label class="block text-slate-300 font-medium mb-1">Domain Name</label>
                <input
                  type="text"
                  [(ngModel)]="newDomainInput"
                  placeholder="e.g. portal.brand.com"
                  data-testid="input-domain-name"
                  class="w-full bg-slate-800 border border-slate-700 rounded-lg px-3 py-2 text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500"
                />
              </div>

              <div class="grid grid-cols-2 gap-3">
                <div>
                  <label class="block text-slate-300 font-medium mb-1">Primary Color</label>
                  <input
                    type="color"
                    [(ngModel)]="primaryColorInput"
                    class="w-full h-9 bg-slate-800 border border-slate-700 rounded-lg p-1 cursor-pointer"
                  />
                </div>
                <div>
                  <label class="block text-slate-300 font-medium mb-1">Accent Color</label>
                  <input
                    type="color"
                    [(ngModel)]="accentColorInput"
                    class="w-full h-9 bg-slate-800 border border-slate-700 rounded-lg p-1 cursor-pointer"
                  />
                </div>
              </div>
            </div>

            <div class="flex justify-end gap-3 pt-3 border-t border-slate-800">
              <button
                (click)="toggleRegisterModal()"
                class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg text-xs font-semibold"
              >
                Cancel
              </button>
              <button
                (click)="submitRegisterDomain()"
                data-testid="submit-register-domain-btn"
                class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-xs font-semibold shadow"
              >
                Register & Generate Tokens
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class CustomDomainsCockpitComponent {
  readonly domains = signal<TenantCustomDomain[]>([
    {
      id: 'dom_acme_enterprise',
      tenantId: 'tenant_acme_corp',
      domainName: 'portal.acmecorp.com',
      verificationToken: 'growth-os-verify=acme_7f9c2d1e8',
      expectedCname: 'cname.growth-os.cloud',
      isDnsVerified: true,
      sslStatus: 'Active',
      sslExpiresAtUtc: new Date(Date.now() + 75 * 86400000).toISOString(),
      theming: {
        primaryColor: '#1e40af',
        accentColor: '#f59e0b',
        pageTitle: 'Acme Corp Growth Portal'
      }
    },
    {
      id: 'dom_hyper_growth',
      tenantId: 'tenant_hyper_growth',
      domainName: 'app.hypergrowth.io',
      verificationToken: 'growth-os-verify=hyper_83b2a7e1',
      expectedCname: 'cname.growth-os.cloud',
      isDnsVerified: false,
      sslStatus: 'PendingVerification',
      sslExpiresAtUtc: new Date(Date.now() + 90 * 86400000).toISOString(),
      theming: {
        primaryColor: '#7c3aed',
        accentColor: '#10b981',
        pageTitle: 'HyperGrowth Cloud Console'
      }
    }
  ]);

  readonly selectedDomain = signal<TenantCustomDomain | null>(this.domains()[0]);
  readonly showRegisterModal = signal(false);

  newDomainInput = '';
  primaryColorInput = '#3b82f6';
  accentColorInput = '#10b981';

  readonly activeDomainsCount = computed(() =>
    this.domains().filter(d => d.isDnsVerified).length
  );

  selectDomain(domain: TenantCustomDomain): void {
    this.selectedDomain.set(domain);
  }

  toggleRegisterModal(): void {
    this.showRegisterModal.update(v => !v);
  }

  submitRegisterDomain(): void {
    if (!this.newDomainInput.trim()) return;

    const newDom: TenantCustomDomain = {
      id: `dom_${Date.now()}`,
      tenantId: 'tenant_self_service',
      domainName: this.newDomainInput.trim().toLowerCase(),
      verificationToken: `growth-os-verify=${Math.random().toString(36).substring(2, 12)}`,
      expectedCname: 'cname.growth-os.cloud',
      isDnsVerified: false,
      sslStatus: 'PendingVerification',
      sslExpiresAtUtc: new Date(Date.now() + 90 * 86400000).toISOString(),
      theming: {
        primaryColor: this.primaryColorInput,
        accentColor: this.accentColorInput,
        pageTitle: `${this.newDomainInput} Portal`
      }
    };

    this.domains.update(list => [newDom, ...list]);
    this.selectedDomain.set(newDom);
    this.newDomainInput = '';
    this.showRegisterModal.set(false);
  }

  verifyDns(domain: TenantCustomDomain): void {
    this.domains.update(list =>
      list.map(d => (d.id === domain.id ? { ...d, isDnsVerified: true, sslStatus: 'Active' } : d))
    );
    if (this.selectedDomain()?.id === domain.id) {
      this.selectedDomain.update(d => (d ? { ...d, isDnsVerified: true, sslStatus: 'Active' } : null));
    }
  }

  renewSsl(domain: TenantCustomDomain): void {
    const updatedExpiry = new Date(Date.now() + 90 * 86400000).toISOString();
    this.domains.update(list =>
      list.map(d => (d.id === domain.id ? { ...d, sslStatus: 'Active', sslExpiresAtUtc: updatedExpiry } : d))
    );
    if (this.selectedDomain()?.id === domain.id) {
      this.selectedDomain.update(d => (d ? { ...d, sslStatus: 'Active', sslExpiresAtUtc: updatedExpiry } : null));
    }
  }
}
