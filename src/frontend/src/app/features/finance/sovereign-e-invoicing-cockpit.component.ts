import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface SovereignTaxPortalProfile {
  portalCode: string;
  countryName: string;
  standardFormat: string;
  regulatoryMandate: string;
}

export interface ClearedEInvoiceRecord {
  invoiceNumber: string;
  portalCode: string;
  buyerTaxId: string;
  totalAmount: number;
  vatAmount: number;
  currency: string;
  sovereignClearanceUuid: string;
  status: string;
}

@Component({
  selector: 'app-sovereign-e-invoicing-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" data-testid="sovereign-e-invoicing-cockpit" class="p-6 bg-slate-900 text-slate-100 min-h-screen">
      <header class="mb-8 border-b border-slate-800 pb-4 flex justify-between items-center">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-white flex items-center gap-2">
            <span class="inline-block w-3 h-3 rounded-full bg-emerald-500 animate-pulse"></span>
            Cross-Border Statutory E-Invoicing & Sovereign Clearance Mesh
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            PEPPOL, ZATCA Phase 2, SAT CFDI 4.0 & Chorus Pro Hardware-Attested Real-Time CTC Tax Clearance
          </p>
        </div>
        <div class="flex gap-3">
          <button (click)="generateClearanceInvoice()" class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white font-medium rounded-lg shadow-sm transition">
            Generate Sovereign E-Invoice
          </button>
        </div>
      </header>

      <!-- KPI Metrics Grid -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Cleared Invoices</span>
          <p class="text-2xl font-extrabold text-white mt-1">{{ invoices().length }}</p>
          <span class="text-xs text-emerald-400">Government Sealed</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Tax Portals</span>
          <p class="text-2xl font-extrabold text-indigo-400 mt-1">{{ portals().length }}</p>
          <span class="text-xs text-slate-400">EU, SA, MX, FR Mandates</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Clearance Success</span>
          <p class="text-2xl font-extrabold text-emerald-400 mt-1">100.0%</p>
          <span class="text-xs text-slate-400">Zero Rejected Tax Filings</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">VAT Cleared</span>
          <p class="text-2xl font-extrabold text-cyan-400 mt-1">$50.5K</p>
          <span class="text-xs text-emerald-400">Real-Time CTC Remitted</span>
        </div>
      </section>

      <!-- Main Panels -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Supported Portals -->
        <section class="bg-slate-800/40 border border-slate-700/50 rounded-xl p-5">
          <h2 class="text-lg font-semibold text-white mb-4">Sovereign Mandates</h2>
          <div class="space-y-3">
            <div *ngFor="let p of portals()" class="p-3 bg-slate-900/60 border border-slate-700/40 rounded-lg">
              <div class="flex justify-between items-center mb-1">
                <span class="font-medium text-slate-200 text-sm">{{ p.countryName }}</span>
                <span class="px-2 py-0.5 text-xs bg-emerald-500/20 text-emerald-300 rounded font-semibold font-mono">{{ p.portalCode }}</span>
              </div>
              <p class="text-xs text-slate-400 mb-1">Syntax: {{ p.standardFormat }}</p>
              <div class="text-[10px] font-mono text-slate-400 bg-slate-950 p-1.5 rounded truncate">
                {{ p.regulatoryMandate }}
              </div>
            </div>
          </div>
        </section>

        <!-- Cleared Invoices Table -->
        <section class="lg:col-span-2 bg-slate-800/40 border border-slate-700/50 rounded-xl p-5">
          <h2 class="text-lg font-semibold text-white mb-4">Cryptographically Cleared Invoices</h2>
          <div class="overflow-x-auto">
            <table class="w-full text-left border-collapse text-sm">
              <thead>
                <tr class="border-b border-slate-700 text-slate-400 text-xs">
                  <th class="pb-3 font-medium">Invoice Number</th>
                  <th class="pb-3 font-medium">Portal</th>
                  <th class="pb-3 font-medium">Total / VAT</th>
                  <th class="pb-3 font-medium">Clearance UUID</th>
                  <th class="pb-3 font-medium">Status</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800">
                <tr *ngFor="let inv of invoices()" class="hover:bg-slate-800/50 transition">
                  <td class="py-3 font-mono text-xs text-emerald-400 font-semibold">{{ inv.invoiceNumber }}</td>
                  <td class="py-3 text-slate-300 text-xs">{{ inv.portalCode }}</td>
                  <td class="py-3 font-mono">
                    <span class="font-bold text-slate-200">{{ inv.totalAmount.toLocaleString() }} {{ inv.currency }}</span>
                    <span class="text-[10px] text-slate-400 block">VAT: {{ inv.vatAmount.toLocaleString() }}</span>
                  </td>
                  <td class="py-3 font-mono text-slate-400 text-xs truncate max-w-[160px]">{{ inv.sovereignClearanceUuid }}</td>
                  <td class="py-3">
                    <span class="px-2 py-0.5 text-[10px] rounded font-semibold font-mono bg-emerald-500/20 text-emerald-300">
                      {{ inv.status }}
                    </span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </div>
    </div>
  `
})
export class SovereignEInvoicingCockpitComponent {
  portals = signal<SovereignTaxPortalProfile[]>([
    { portalCode: 'PEPPOL_EU', countryName: 'European Union', standardFormat: 'UBL 2.1 XML', regulatoryMandate: 'EU Directive 2014/55/EU' },
    { portalCode: 'ZATCA_SA', countryName: 'Saudi Arabia', standardFormat: 'UBL 2.1 XML (Fatoora Phase 2)', regulatoryMandate: 'ZATCA E-Invoicing Mandate' },
    { portalCode: 'SAT_MX', countryName: 'Mexico', standardFormat: 'CFDI 4.0 XML', regulatoryMandate: 'Servicio de Administración Tributaria' },
    { portalCode: 'CHORUS_PRO_FR', countryName: 'France', standardFormat: 'Factur-X / UBL', regulatoryMandate: 'Direction Générale des Finances Publiques' }
  ]);

  invoices = signal<ClearedEInvoiceRecord[]>([
    { invoiceNumber: 'INV-EU-2026-8899', portalCode: 'PEPPOL_EU', buyerTaxId: 'DE123456789', totalAmount: 120000, vatAmount: 22800, currency: 'EUR', sovereignClearanceUuid: 'UUID-PEPPOL-0x99AABB1122', status: 'CLEARED_BY_TAX_AUTHORITY' },
    { invoiceNumber: 'INV-SA-2026-4411', portalCode: 'ZATCA_SA', buyerTaxId: '300000000000003', totalAmount: 185000, vatAmount: 27750, currency: 'SAR', sovereignClearanceUuid: 'UUID-ZATCA-0x7788441122', status: 'CLEARED_BY_TAX_AUTHORITY' }
  ]);

  generateClearanceInvoice() {
    const newInv: ClearedEInvoiceRecord = {
      invoiceNumber: `INV-FR-${Date.now().toString().slice(-4)}`,
      portalCode: 'CHORUS_PRO_FR',
      buyerTaxId: 'FR9988776655',
      totalAmount: 95000,
      vatAmount: 19000,
      currency: 'EUR',
      sovereignClearanceUuid: `UUID-CHORUS-0x${Date.now().toString(16).toUpperCase()}`,
      status: 'CLEARED_BY_TAX_AUTHORITY'
    };
    this.invoices.update(list => [newInv, ...list]);
  }
}
