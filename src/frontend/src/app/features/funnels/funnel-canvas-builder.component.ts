import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface CanvasElementUi {
  id: string;
  type: string;
  title: string;
  subtitle?: string;
  order: number;
}

@Component({
  selector: 'app-funnel-canvas-builder',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="funnel-canvas-builder p-6 max-w-7xl mx-auto space-y-6" dir="ltr">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b pb-6">
        <div>
          <div class="flex items-center gap-3">
            <h1 class="text-2xl font-bold tracking-tight text-slate-900">Drag-and-Drop Funnel & Landing Page Canvas</h1>
            <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-blue-100 text-blue-800">
              Live Responsive Viewports
            </span>
          </div>
          <p class="text-sm text-slate-500 mt-1">
            Build high-converting funnels with modular block trees, responsive breakpoint simulations, and instant SSR export.
          </p>
        </div>

        <!-- Viewport Mode Switcher & Export -->
        <div class="flex items-center gap-3">
          <div class="flex rounded-lg border bg-slate-50 p-1 text-xs font-medium" data-testid="viewport-switcher">
            <button 
              (click)="selectedViewport.set('desktop')"
              class="px-2.5 py-1 rounded transition-colors"
              [ngClass]="selectedViewport() === 'desktop' ? 'bg-white shadow text-slate-900 font-bold' : 'text-slate-500'"
              data-testid="btn-viewport-desktop">
              Desktop (1440px)
            </button>
            <button 
              (click)="selectedViewport.set('tablet')"
              class="px-2.5 py-1 rounded transition-colors"
              [ngClass]="selectedViewport() === 'tablet' ? 'bg-white shadow text-slate-900 font-bold' : 'text-slate-500'"
              data-testid="btn-viewport-tablet">
              Tablet (768px)
            </button>
            <button 
              (click)="selectedViewport.set('mobile')"
              class="px-2.5 py-1 rounded transition-colors"
              [ngClass]="selectedViewport() === 'mobile' ? 'bg-white shadow text-slate-900 font-bold' : 'text-slate-500'"
              data-testid="btn-viewport-mobile">
              Mobile (375px)
            </button>
          </div>

          <button 
            (click)="compileAndExportHtml()"
            class="px-4 py-2 bg-slate-900 hover:bg-slate-800 text-white rounded-lg font-medium text-xs transition-colors shadow-sm"
            data-testid="btn-export-html">
            Export Production HTML
          </button>
        </div>
      </div>

      <!-- Builder Workspace -->
      <div class="grid grid-cols-1 lg:grid-cols-4 gap-6">
        <!-- Elements Sidebar -->
        <div class="bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <div class="flex items-center justify-between">
            <h3 class="font-bold text-slate-900 text-sm">Component Palette</h3>
            <span class="text-xs text-slate-400 font-mono">{{ elements().length }} on page</span>
          </div>
          <p class="text-xs text-slate-500">Click to append modular components to canvas.</p>

          <div class="space-y-2" data-testid="element-palette">
            <button 
              (click)="addElement('HeroBlock', 'Autonomous AI Revenue Engine', 'Sub-150ms Duplex Voice SDR Fleet')"
              class="w-full text-left p-2.5 border rounded-lg hover:border-blue-400 hover:bg-blue-50/50 transition-colors text-xs font-semibold text-slate-700"
              data-testid="btn-add-hero">
              + Hero Banner Block
            </button>
            <button 
              (click)="addElement('PricingCards', '3-Tier Transparent Pricing', 'Starter, Growth & Enterprise Air-Gapped')"
              class="w-full text-left p-2.5 border rounded-lg hover:border-blue-400 hover:bg-blue-50/50 transition-colors text-xs font-semibold text-slate-700"
              data-testid="btn-add-pricing">
              + Pricing Table Grid
            </button>
            <button 
              (click)="addElement('LeadForm', 'Instant Sandbox Claim Form', 'Work Email, Team Size & WebRTC Demo')"
              class="w-full text-left p-2.5 border rounded-lg hover:border-blue-400 hover:bg-blue-50/50 transition-colors text-xs font-semibold text-slate-700"
              data-testid="btn-add-form">
              + Lead Capture Form
            </button>
            <button 
              (click)="addElement('TestimonialGrid', 'Verified Enterprise Results', 'Median 340% ROI within 90 days')"
              class="w-full text-left p-2.5 border rounded-lg hover:border-blue-400 hover:bg-blue-50/50 transition-colors text-xs font-semibold text-slate-700"
              data-testid="btn-add-testimonials">
              + Customer Testimonials
            </button>
          </div>

          <!-- Global Theme Tokens -->
          <div class="pt-4 border-t space-y-3">
            <h4 class="font-bold text-slate-900 text-xs">Global Theme Tokens</h4>
            <div class="space-y-2 text-xs">
              <div class="flex justify-between items-center">
                <span class="text-slate-500">Primary Color</span>
                <span class="font-mono font-bold text-indigo-600">#4f46e5</span>
              </div>
              <div class="flex justify-between items-center">
                <span class="text-slate-500">Accent Color</span>
                <span class="font-mono font-bold text-emerald-600">#10b981</span>
              </div>
              <div class="flex justify-between items-center">
                <span class="text-slate-500">Corner Radius</span>
                <span class="font-mono text-slate-700">rounded-xl</span>
              </div>
            </div>
          </div>
        </div>

        <!-- Visual Canvas Simulator -->
        <div class="lg:col-span-3 bg-slate-100/70 border rounded-2xl p-6 flex flex-col items-center justify-start min-h-[500px]">
          <div 
            class="bg-white border rounded-2xl shadow-lg transition-all duration-300 p-6 space-y-6 w-full"
            [ngClass]="{
              'max-w-full': selectedViewport() === 'desktop',
              'max-w-[768px]': selectedViewport() === 'tablet',
              'max-w-[375px]': selectedViewport() === 'mobile'
            }"
            data-testid="canvas-preview-container">
            
            <div class="border-b pb-3 flex justify-between items-center text-xs text-slate-400">
              <span>Viewport: <strong class="text-slate-700">{{ selectedViewport().toUpperCase() }}</strong></span>
              <span class="font-mono">{{ elements().length }} rendered blocks</span>
            </div>

            <!-- Rendered Canvas Blocks -->
            <div class="space-y-4" data-testid="canvas-blocks-list">
              @for (item of elements(); track item.id; let idx = $index) {
                <div class="p-4 border rounded-xl bg-white hover:border-blue-400 transition-colors relative group shadow-sm">
                  <div class="flex justify-between items-center text-xs mb-1">
                    <span class="font-bold text-slate-800">{{ item.title }}</span>
                    <span class="px-2 py-0.5 rounded text-[10px] font-mono bg-slate-100 text-slate-600">
                      {{ item.type }}
                    </span>
                  </div>
                  @if (item.subtitle) {
                    <p class="text-xs text-slate-500">{{ item.subtitle }}</p>
                  }
                  <div class="pt-2 mt-2 border-t flex justify-end">
                    <button 
                      (click)="removeElement(item.id)"
                      class="text-[11px] text-rose-600 hover:underline">
                      Remove Block
                    </button>
                  </div>
                </div>
              }
            </div>
          </div>
        </div>
      </div>

      <!-- Export Code Modal -->
      @if (showExportModal()) {
        <div class="fixed inset-0 bg-slate-900/40 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div class="bg-white rounded-2xl max-w-2xl w-full p-6 space-y-4 shadow-xl border">
            <div class="flex items-center justify-between border-b pb-3">
              <h3 class="font-bold text-slate-900 text-base">Exported Production HTML ({{ selectedViewport() }})</h3>
              <button (click)="showExportModal.set(false)" class="text-slate-400 hover:text-slate-600">&times;</button>
            </div>

            <pre class="bg-slate-900 text-slate-100 p-4 rounded-xl text-xs font-mono max-h-60 overflow-y-auto leading-relaxed" data-testid="exported-html-code">{{ exportedHtml() }}</pre>

            <div class="flex justify-end pt-3 border-t">
              <button 
                (click)="showExportModal.set(false)"
                class="px-4 py-1.5 bg-slate-900 text-white rounded-lg text-xs font-medium">
                Done
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class FunnelCanvasBuilderComponent {
  selectedViewport = signal<'desktop' | 'tablet' | 'mobile'>('desktop');
  showExportModal = signal(false);
  exportedHtml = signal('');

  elements = signal<CanvasElementUi[]>([
    { id: 'el_1', type: 'HeroBlock', title: 'Scale Revenue Autonomously With AI SDRs', subtitle: 'Deploy duplex voice agents, automated WhatsApp broadcasts, and predictive attribution.', order: 0 },
    { id: 'el_2', type: 'PricingCards', title: '3-Tier Transparent Pricing Grid', subtitle: 'Starter ($2.5k), Growth ($6.5k), Enterprise Air-Gapped ($12.5k)', order: 1 },
    { id: 'el_3', type: 'LeadForm', title: 'Claim 14-Day Free Sandbox Pilot', subtitle: 'Instant WebRTC audio demo and API key provisioning.', order: 2 }
  ]);

  addElement(type: string, title: string, subtitle: string) {
    const newEl: CanvasElementUi = {
      id: 'el_' + Date.now(),
      type,
      title,
      subtitle,
      order: this.elements().length
    };
    this.elements.update(list => [...list, newEl]);
  }

  removeElement(id: string) {
    this.elements.update(list => list.filter(e => e.id !== id));
  }

  compileAndExportHtml() {
    const vp = this.selectedViewport();
    const blocksHtml = this.elements().map(e => `  <section class="block my-6">\n    <h2>${e.title}</h2>\n    <p>${e.subtitle || ''}</p>\n  </section>`).join('\n');
    this.exportedHtml.set(`<!-- SGrowing Canvas Compiled (${vp}) -->\n<div class="funnel-root">\n${blocksHtml}\n</div>`);
    this.showExportModal.set(true);
  }
}
