// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { createEnvironmentInjector } from '@angular/core';
import { LanguageSwitcherComponent } from './language-switcher.component';
import { TranslationService } from '../services/translation.service';

describe('LanguageSwitcherComponent', () => {
  let component: LanguageSwitcherComponent;
  let translationService: TranslationService;

  beforeEach(() => {
    localStorage.clear();
    const injector = createEnvironmentInjector([TranslationService]);
    injector.runInContext(() => {
      component = new LanguageSwitcherComponent();
      translationService = component.translation;
    });
  });

  it('should create the component successfully', () => {
    expect(component).toBeTruthy();
    expect(translationService).toBeTruthy();
  });

  it('should have 5 supported locales including Arabic (ar-AR)', () => {
    const locales = translationService.supportedLocales;
    expect(locales.length).toBe(5);

    const arabic = locales.find(l => l.code === 'ar-AR');
    expect(arabic).toBeTruthy();
    expect(arabic?.name).toContain('العربية');
    expect(arabic?.flag).toBe('🇹🇳');
    expect(arabic?.currency).toBe('TND');
  });

  it('should switch to Arabic and update document lang attribute', () => {
    translationService.setLocale('ar-AR');
    expect(translationService.currentLocale()).toBe('ar-AR');
    expect(document.documentElement.getAttribute('lang')).toBe('ar-AR');
    expect(document.documentElement.getAttribute('dir')).toBe('ltr');
    expect(translationService.t('nav.inbox')).toBe('المحادثات');
    expect(translationService.t('common.resolve')).toBe('حل وإنهاء');
  });

  it('should switch to French and update document attributes accordingly', () => {
    translationService.setLocale('fr-FR');
    expect(translationService.currentLocale()).toBe('fr-FR');
    expect(document.documentElement.getAttribute('lang')).toBe('fr-FR');
    expect(translationService.t('nav.inbox')).toBe('Conversations');
  });

  it('should switch to German and verify translations and document attributes', () => {
    translationService.setLocale('de-DE');
    expect(translationService.currentLocale()).toBe('de-DE');
    expect(document.documentElement.getAttribute('lang')).toBe('de-DE');
    expect(translationService.t('nav.inbox')).toBe('Konversationen');
  });
});
