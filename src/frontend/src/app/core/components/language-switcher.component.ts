import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslationService, SupportedLocale } from '../services/translation.service';

@Component({
  selector: 'app-language-switcher',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="inline-flex items-center gap-1 bg-slate-900/90 border border-slate-800 rounded-lg p-1 shadow-sm" data-testid="language-switcher">
      @for (loc of translation.supportedLocales; track loc.code) {
        <button
          type="button"
          (click)="translation.setLocale(loc.code)"
          [attr.data-testid]="'locale-btn-' + loc.code"
          [attr.aria-label]="loc.name"
          [title]="loc.name"
          [class.bg-indigo-600]="translation.currentLocale() === loc.code"
          [class.text-white]="translation.currentLocale() === loc.code"
          [class.shadow-md]="translation.currentLocale() === loc.code"
          [class.text-slate-400]="translation.currentLocale() !== loc.code"
          [class.hover:text-slate-200]="translation.currentLocale() !== loc.code"
          [class.hover:bg-slate-800]="translation.currentLocale() !== loc.code"
          class="px-2 py-1 rounded text-xs font-medium transition flex items-center gap-1 cursor-pointer">
          <span class="text-sm leading-none">{{ loc.flag }}</span>
          <span class="font-mono text-[11px] uppercase">{{ loc.code.substring(0, 2) }}</span>
        </button>
      }
    </div>
  `
})
export class LanguageSwitcherComponent {
  public readonly translation = inject(TranslationService);
}
