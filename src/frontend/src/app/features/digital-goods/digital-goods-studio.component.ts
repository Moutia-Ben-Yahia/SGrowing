import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { DigitalAssetDto, DigitalDownloadTokenDto, ProductLicenseKeyDto, PimVariantMatrixDto } from '../../core/models/digital-goods.models';

@Component({
  selector: 'app-digital-goods-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="h-full flex flex-col bg-slate-950 text-slate-100 overflow-y-auto">
      <!-- Header -->
      <div class="p-6 border-b border-slate-800 bg-slate-900/40 backdrop-blur flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <div class="flex items-center gap-2">
            <span class="text-2xl">📦</span>
            <h1 class="text-xl font-bold tracking-tight text-white">Digital Goods & PIM Matrix Studio</h1>
            <span class="px-2.5 py-0.5 rounded-full text-xs font-mono bg-purple-500/10 text-purple-400 border border-purple-500/20">
              Ghost / Spree Architecture
            </span>
          </div>
          <p class="text-xs text-slate-400 mt-1">
            Manage downloadable assets, expiring tokens, software licenses, multi-attribute variant matrix & automated feeds.
          </p>
        </div>

        <!-- Navigation Tabs -->
        <div class="flex items-center gap-1 bg-slate-900 p-1 rounded-xl border border-slate-800 text-xs">
          <button
            (click)="activeTab.set('assets')"
            [class.bg-indigo-600]="activeTab() === 'assets'"
            [class.text-white]="activeTab() === 'assets'"
            class="px-3 py-1.5 rounded-lg text-slate-400 hover:text-white transition">
            📁 Digital Assets
          </button>
          <button
            (click)="activeTab.set('licenses')"
            [class.bg-indigo-600]="activeTab() === 'licenses'"
            [class.text-white]="activeTab() === 'licenses'"
            class="px-3 py-1.5 rounded-lg text-slate-400 hover:text-white transition">
            🔑 Software Licenses
          </button>
          <button
            (click)="activeTab.set('matrix')"
            [class.bg-indigo-600]="activeTab() === 'matrix'"
            [class.text-white]="activeTab() === 'matrix'"
            class="px-3 py-1.5 rounded-lg text-slate-400 hover:text-white transition">
            ⚡ PIM Matrix & Feeds
          </button>
        </div>
      </div>

      <!-- Content Area -->
      <div class="p-6 space-y-6 flex-1">
        <!-- 1. Assets Tab -->
        @if (activeTab() === 'assets') {
          <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
            <!-- Asset Creator -->
            <div class="p-5 rounded-2xl bg-slate-900/60 border border-slate-800 space-y-4">
              <h3 class="text-sm font-bold text-slate-200">Upload Digital Product</h3>
              <div class="space-y-3 text-xs">
                <div>
                  <label class="text-slate-400 block mb-1">Asset Title</label>
                  <input [(ngModel)]="newAssetTitle" class="w-full px-3 py-2 bg-slate-950 border border-slate-800 rounded-lg text-slate-200" placeholder="e.g. Master Tunisian COD Playbook" />
                </div>
                <div>
                  <label class="text-slate-400 block mb-1">File Name</label>
                  <input [(ngModel)]="newAssetFileName" class="w-full px-3 py-2 bg-slate-950 border border-slate-800 rounded-lg text-slate-200" placeholder="playbook.pdf" />
                </div>
                <div class="grid grid-cols-2 gap-3">
                  <div>
                    <label class="text-slate-400 block mb-1">Max Downloads</label>
                    <input type="number" [(ngModel)]="newAssetMaxDownloads" class="w-full px-3 py-2 bg-slate-950 border border-slate-800 rounded-lg text-slate-200" />
                  </div>
                  <div>
                    <label class="text-slate-400 block mb-1">Expiry (Hours)</label>
                    <input type="number" [(ngModel)]="newAssetExpiryHours" class="w-full px-3 py-2 bg-slate-950 border border-slate-800 rounded-lg text-slate-200" />
                  </div>
                </div>
                <button
                  (click)="createAsset()"
                  class="w-full py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg font-medium transition shadow-lg shadow-indigo-600/20">
                  Publish Digital Asset
                </button>
              </div>
            </div>

            <!-- Assets List -->
            <div class="lg:col-span-2 space-y-4">
              <h3 class="text-sm font-bold text-slate-200">Registered Digital Assets ({{ assets().length }})</h3>
              <div class="space-y-3">
                @for (a of assets(); track a.id) {
                  <div class="p-4 rounded-xl bg-slate-900/40 border border-slate-800 flex items-center justify-between">
                    <div>
                      <h4 class="text-sm font-bold text-white">{{ a.title }}</h4>
                      <p class="text-xs text-slate-400 font-mono mt-0.5">{{ a.fileName }} • {{ (a.fileSizeBytes / 1000000).toFixed(1) }} MB • Limit: {{ a.maxAllowedDownloads }} tries</p>
                    </div>
                    <button
                      (click)="issueToken(a.id)"
                      class="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-indigo-400 rounded-lg text-xs font-mono border border-slate-700 transition">
                      🔗 Issue Token
                    </button>
                  </div>
                }
              </div>

              @if (lastIssuedToken()) {
                <div class="p-4 rounded-xl bg-indigo-500/10 border border-indigo-500/20 space-y-2">
                  <span class="text-xs font-semibold text-indigo-400">✨ Token Issued Successfully</span>
                  <div class="flex items-center gap-2">
                    <input [value]="'http://localhost:5000/api/digital/download/' + lastIssuedToken()?.token" readonly class="w-full px-3 py-1.5 bg-slate-950 border border-slate-800 rounded text-xs font-mono text-slate-300" />
                    <a [href]="'http://localhost:5000/api/digital/download/' + lastIssuedToken()?.token" target="_blank" class="px-3 py-1.5 bg-indigo-600 text-white rounded text-xs">Test</a>
                  </div>
                </div>
              }
            </div>
          </div>
        }

        <!-- 2. Licenses Tab -->
        @if (activeTab() === 'licenses') {
          <div class="space-y-6">
            <div class="p-5 rounded-2xl bg-slate-900/60 border border-slate-800 flex items-center justify-between">
              <div>
                <h3 class="text-sm font-bold text-white">Issue License Keys</h3>
                <p class="text-xs text-slate-400 mt-1">Generate serial keys for downloadable software packages & add-ons.</p>
              </div>
              <div class="flex items-center gap-3">
                <input [(ngModel)]="licenseSku" placeholder="Product SKU (e.g. SOFT-COD-AUTO)" class="px-3 py-2 bg-slate-950 border border-slate-800 rounded-lg text-xs text-slate-200" />
                <button
                  (click)="generateLicenses()"
                  class="px-4 py-2 bg-purple-600 hover:bg-purple-500 text-white rounded-lg text-xs font-bold transition">
                  Generate 3 Keys
                </button>
              </div>
            </div>

            <!-- Table -->
            <div class="rounded-2xl border border-slate-800 overflow-hidden bg-slate-900/40">
              <table class="w-full text-left text-xs">
                <thead class="bg-slate-900/80 text-slate-400 uppercase font-mono border-b border-slate-800">
                  <tr>
                    <th class="px-4 py-3">License Serial Key</th>
                    <th class="px-4 py-3">Product SKU</th>
                    <th class="px-4 py-3">Seats</th>
                    <th class="px-4 py-3">Status</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800/60 font-mono">
                  @for (k of licenses(); track k.id) {
                    <tr class="hover:bg-slate-900/40">
                      <td class="px-4 py-3 text-indigo-400 font-bold">{{ k.licenseKey }}</td>
                      <td class="px-4 py-3 text-slate-300">{{ k.productSku }}</td>
                      <td class="px-4 py-3 text-slate-400">{{ k.activatedSeats }} / {{ k.maxSeats }} Seats</td>
                      <td class="px-4 py-3">
                        <span class="px-2 py-0.5 rounded bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">{{ k.status }}</span>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }

        <!-- 3. Matrix & Feeds Tab -->
        @if (activeTab() === 'matrix') {
          <div class="space-y-6">
            <div class="p-5 rounded-2xl bg-slate-900/60 border border-slate-800 flex flex-col md:flex-row md:items-center justify-between gap-4">
              <div>
                <h3 class="text-sm font-bold text-white">PIM Variant Matrix Generator</h3>
                <p class="text-xs text-slate-400 mt-1">Cross-product generation of Size × Color × Material with real-time Google/Meta feeds.</p>
              </div>
              <div class="flex items-center gap-3">
                <a href="http://localhost:5000/api/catalog/feeds/google-merchant.xml?tenantId={{ tenantId }}" target="_blank" class="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-amber-400 rounded-lg text-xs font-mono border border-slate-700 flex items-center gap-1.5 transition">
                  🛍️ Google XML Feed ↗
                </a>
                <a href="http://localhost:5000/api/catalog/feeds/meta-catalog.csv?tenantId={{ tenantId }}" target="_blank" class="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-blue-400 rounded-lg text-xs font-mono border border-slate-700 flex items-center gap-1.5 transition">
                  📱 Meta CSV Feed ↗
                </a>
                <button
                  (click)="generateMatrixDemo()"
                  class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-xs font-bold transition">
                  ⚡ Generate Matrix
                </button>
              </div>
            </div>

            <!-- Matrix Results Table -->
            <div class="rounded-2xl border border-slate-800 overflow-hidden bg-slate-900/40">
              <table class="w-full text-left text-xs">
                <thead class="bg-slate-900/80 text-slate-400 uppercase font-mono border-b border-slate-800">
                  <tr>
                    <th class="px-4 py-3">Variant SKU</th>
                    <th class="px-4 py-3">Attributes</th>
                    <th class="px-4 py-3">Price</th>
                    <th class="px-4 py-3">Inventory</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800/60 font-mono">
                  @for (m of variants(); track m.id) {
                    <tr class="hover:bg-slate-900/40">
                      <td class="px-4 py-3 text-slate-200 font-bold">{{ m.variantSku }}</td>
                      <td class="px-4 py-3 text-slate-400">{{ m.attributesJson }}</td>
                      <td class="px-4 py-3 text-emerald-400 font-bold">\${{ m.price.toFixed(2) }}</td>
                      <td class="px-4 py-3 text-slate-300">{{ m.stockQuantity }} units</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }
      </div>
    </div>
  `
})
export class DigitalGoodsStudioComponent implements OnInit {
  private api = inject(ApiService);
  private auth = inject(AuthService);

  public tenantId = this.auth.currentTenant()?.id || 'a0000000-0000-0000-0000-000000000001';
  public activeTab = signal<'assets' | 'licenses' | 'matrix'>('assets');

  public assets = signal<DigitalAssetDto[]>([]);
  public licenses = signal<ProductLicenseKeyDto[]>([]);
  public variants = signal<PimVariantMatrixDto[]>([]);
  public lastIssuedToken = signal<DigitalDownloadTokenDto | null>(null);

  public newAssetTitle = 'Tunisian COD Scaling Blueprint';
  public newAssetFileName = 'scaling-blueprint.pdf';
  public newAssetMaxDownloads = 5;
  public newAssetExpiryHours = 72;
  public licenseSku = 'PRO-SUITE';

  ngOnInit(): void {
    this.loadAll();
  }

  public loadAll(): void {
    this.api.getDigitalAssets(this.tenantId).subscribe(assets => {
      if (assets.length === 0) {
        this.createAsset();
      } else {
        this.assets.set(assets);
      }
    });

    this.api.getLicenseKeys(this.tenantId).subscribe(keys => this.licenses.set(keys));
    this.api.getVariantMatrix(this.tenantId, 'HOODIE-EXP').subscribe(vars => this.variants.set(vars));
  }

  public createAsset(): void {
    this.api.createDigitalAsset({
      tenantId: this.tenantId,
      title: this.newAssetTitle,
      fileName: this.newAssetFileName,
      storagePath: 'downloads/blueprints/' + this.newAssetFileName,
      mimeType: 'application/pdf',
      fileSizeBytes: 8_500_000,
      maxAllowedDownloads: this.newAssetMaxDownloads,
      expiryHours: this.newAssetExpiryHours
    }).subscribe(a => {
      this.assets.update(prev => [a, ...prev]);
    });
  }

  public issueToken(assetId: string): void {
    this.api.issueDownloadToken({
      tenantId: this.tenantId,
      assetId
    }).subscribe(t => {
      this.lastIssuedToken.set(t);
    });
  }

  public generateLicenses(): void {
    this.api.generateLicenseKeys({
      tenantId: this.tenantId,
      productSku: this.licenseSku,
      count: 3,
      seatsPerKey: 5
    }).subscribe(keys => {
      this.licenses.update(prev => [...keys, ...prev]);
    });
  }

  public generateMatrixDemo(): void {
    this.api.generateVariantMatrix({
      tenantId: this.tenantId,
      baseSku: 'HOODIE-EXP',
      basePrice: 79.99,
      attributes: {
        Size: ['S', 'M', 'L', 'XL'],
        Color: ['Charcoal', 'Navy Blue']
      }
    }).subscribe(matrix => {
      this.variants.set(matrix);
    });
  }
}
