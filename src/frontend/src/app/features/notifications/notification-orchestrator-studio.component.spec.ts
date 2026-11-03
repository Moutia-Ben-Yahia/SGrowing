// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { NotificationOrchestratorStudioComponent } from './notification-orchestrator-studio.component';

describe('NotificationOrchestratorStudioComponent', () => {
  let component: NotificationOrchestratorStudioComponent;

  beforeEach(() => {
    component = new NotificationOrchestratorStudioComponent();
  });

  it('should initialize with pre-seeded notification feed rows and calculate total dispatched', () => {
    expect(component).toBeTruthy();
    expect(component.feed().length).toBe(3);
    expect(component.totalDispatched()).toBe(423);
  });

  it('should return styled CSS badge classes for channels and statuses', () => {
    expect(component.getChannelBadge('InApp')).toContain('text-indigo-300');
    expect(component.getChannelBadge('WhatsApp')).toContain('text-emerald-300');
    expect(component.getChannelBadge('Sms')).toContain('text-amber-300');
    expect(component.getChannelBadge('Email')).toContain('text-rose-300');

    expect(component.getStatusBadge('Delivered')).toContain('text-blue-300');
    expect(component.getStatusBadge('Read')).toContain('text-emerald-400');
    expect(component.getStatusBadge('FallenBack')).toContain('text-amber-300');
  });

  it('should dispatch new notification and prepend to feed stream', () => {
    const initialCount = component.feed().length;
    component.openDispatchModal();

    expect(component.feed().length).toBe(initialCount + 1);
    expect(component.feed()[0].title).toBe('Live security policy re-evaluation alert');
    expect(component.feed()[0].channel).toBe('InApp');
  });

  it('should execute fallback sweep without throwing', () => {
    expect(() => component.runFallbackSweep()).not.toThrow();
  });
});
