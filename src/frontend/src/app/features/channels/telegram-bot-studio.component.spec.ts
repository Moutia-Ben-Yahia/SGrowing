// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { TelegramBotStudioComponent } from './telegram-bot-studio.component';

describe('TelegramBotStudioComponent', () => {
  let component: TelegramBotStudioComponent;

  beforeEach(() => {
    component = new TelegramBotStudioComponent();
  });

  it('should initialize with default simulator tab', () => {
    expect(component).toBeTruthy();
    expect(component.activeTab()).toBe('simulator');
  });

  it('should navigate between Telegram bot studio tabs', () => {
    component.activeTab.set('fsm');
    expect(component.activeTab()).toBe('fsm');

    component.activeTab.set('sandbox');
    expect(component.activeTab()).toBe('sandbox');

    component.activeTab.set('broadcast');
    expect(component.activeTab()).toBe('broadcast');
  });

  it('should generate signed Telegram WebApp initData and validate HMAC signature', () => {
    component.activeTab.set('sandbox');
    component.generateSandboxInitData();
    expect(component.sandboxInitData).toBeTruthy();
    expect(component.sandboxInitData).toContain('hash=');

    component.runValidation();
    const outcome = component.validationOutcome();
    expect(outcome).toBeTruthy();
    expect(outcome?.isValid).toBe(true);
    expect(outcome?.hashesMatch).toBe(true);
  });

  it('should detect bit flip payload tampering in WebApp initData', () => {
    component.activeTab.set('sandbox');
    component.generateSandboxInitData();
    component.tamperSandboxInitData();

    component.runValidation();
    const outcome = component.validationOutcome();
    expect(outcome).toBeTruthy();
    expect(outcome?.isValid).toBe(false);
    expect(outcome?.hashesMatch).toBe(false);
  });
});
