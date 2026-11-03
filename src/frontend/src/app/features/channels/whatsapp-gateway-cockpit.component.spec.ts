// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { createEnvironmentInjector } from '@angular/core';
import { WhatsAppGatewayCockpitComponent } from './whatsapp-gateway-cockpit.component';
import { TranslationService } from '../../core/services/translation.service';

describe('WhatsAppGatewayCockpitComponent', () => {
  let component: WhatsAppGatewayCockpitComponent;

  beforeEach(() => {
    const injector = createEnvironmentInjector([TranslationService]);
    injector.runInContext(() => {
      component = new WhatsAppGatewayCockpitComponent();
    });
  });

  it('should initialize with WhatsApp instances and newsletter channels', () => {
    expect(component).toBeTruthy();
    expect(component.instances().length).toBeGreaterThan(0);
    expect(component.newsletterChannels().length).toBe(2);
    expect(component.connectedCount()).toBeGreaterThanOrEqual(1);
    expect(component.totalMemoryUsage()).toBeGreaterThan(0);
  });

  it('should generate pairing code for instance connection', () => {
    component.generatePairingCode();
    const code = component.generatedCode();
    expect(code).toBeTruthy();
    expect(code).toMatch(/^\d{4}-\d{4}$/);
  });

  it('should add reactions to VIP newsletter broadcast channels', () => {
    const channel = component.newsletterChannels()[0];
    const initialFire = channel.reactions.find(r => r.emoji === '🔥')?.count || 0;

    component.addReaction(channel.id, '🔥');

    const updated = component.newsletterChannels()[0];
    const updatedFire = updated.reactions.find(r => r.emoji === '🔥')?.count;
    expect(updatedFire).toBe(initialFire + 1);
  });

  it('should return appropriate CSS badge styling for instance connection statuses', () => {
    expect(component.getStatusBadge('Connected')).toContain('text-emerald-400');
    expect(component.getStatusBadge('Pairing')).toContain('text-amber-400');
    expect(component.getStatusBadge('Disconnected')).toContain('text-slate-400');
  });
});
