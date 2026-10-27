// @vitest-environment jsdom
import { describe, it, expect, beforeEach } from 'vitest';
import { TranslationService, SUPPORTED_LOCALES, SupportedLocale } from './translation.service';

describe('TranslationService (Multilingual Native English & LTR)', () => {
  let service: TranslationService;

  beforeEach(() => {
    localStorage.clear();
    service = new TranslationService();
  });

  it('should default to en-US primary native English', () => {
    expect(service.currentLocale()).toBe('en-US');
    expect(service.currentLocaleInfo().name).toBe('English (US)');
  });

  it('should maintain strict LTR document direction', () => {
    expect(document.documentElement.getAttribute('dir')).toBe('ltr');
    expect(document.documentElement.getAttribute('lang')).toBe('en-US');

    service.setLocale('fr-FR');
    expect(document.documentElement.getAttribute('dir')).toBe('ltr');
    expect(document.documentElement.getAttribute('lang')).toBe('fr-FR');

    service.setLocale('es-ES');
    expect(document.documentElement.getAttribute('dir')).toBe('ltr');
    expect(document.documentElement.getAttribute('lang')).toBe('es-ES');

    service.setLocale('de-DE');
    expect(document.documentElement.getAttribute('dir')).toBe('ltr');
    expect(document.documentElement.getAttribute('lang')).toBe('de-DE');
  });

  it('should translate keys correctly across supported locales', () => {
    expect(service.t('nav.inbox')).toBe('Conversations');
    expect(service.t('common.resolve')).toBe('Resolve');

    service.setLocale('fr-FR');
    expect(service.t('nav.inbox')).toBe('Conversations');
    expect(service.t('common.resolve')).toBe('Résoudre');
    expect(service.t('common.healthy')).toBe('Opérationnel');

    service.setLocale('es-ES');
    expect(service.t('common.resolve')).toBe('Resolver');
    expect(service.t('common.healthy')).toBe('Operativo');

    service.setLocale('de-DE');
    expect(service.t('common.resolve')).toBe('Abschließen');
    expect(service.t('common.healthy')).toBe('Betriebsbereit');
  });

  it('should format currency accurately according to locale', () => {
    service.setLocale('en-US');
    const usdFormatted = service.formatCurrency(1250.50, 'USD');
    expect(usdFormatted).toContain('1,250.50');

    service.setLocale('fr-FR');
    const eurFormatted = service.formatCurrency(1250.50, 'EUR');
    expect(eurFormatted).toMatch(/1\s?250,50/);
  });

  it('should format dates accurately according to locale', () => {
    const testDate = new Date(Date.UTC(2026, 9, 27, 14, 30));
    service.setLocale('en-US');
    const usDate = service.formatDate(testDate);
    expect(usDate).toBeTruthy();

    service.setLocale('de-DE');
    const deDate = service.formatDate(testDate);
    expect(deDate).toBeTruthy();
  });

  it('should persist selected locale to localStorage', () => {
    service.setLocale('de-DE');
    expect(localStorage.getItem('sgrowing_locale')).toBe('de-DE');

    const freshService = new TranslationService();
    expect(freshService.currentLocale()).toBe('de-DE');
  });

  it('should handle null, undefined, NaN, and invalid dates gracefully', () => {
    service.setLocale('en-US');
    expect(service.formatCurrency(NaN)).toContain('0.00');
    expect(service.formatCurrency(null as any)).toContain('0.00');

    expect(service.formatDate(null)).toBe('-');
    expect(service.formatDate(undefined)).toBe('-');
    expect(service.formatDate('not-a-valid-date')).toBe('-');
  });
});
