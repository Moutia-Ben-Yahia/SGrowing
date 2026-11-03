// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { createEnvironmentInjector } from '@angular/core';
import { DocumentsStudioComponent } from './documents-studio.component';

describe('DocumentsStudioComponent', () => {
  let component: DocumentsStudioComponent;

  beforeEach(() => {
    const injector = createEnvironmentInjector([]);
    injector.runInContext(() => {
      component = new DocumentsStudioComponent();
      component.ngOnInit();
    });
  });

  it('initializes with default envelopes and selects first envelope', () => {
    expect(component.envelopes().length).toBeGreaterThanOrEqual(2);
    expect(component.selectedEnvelope()).toBeDefined();
    expect(component.selectedEnvelope()?.id).toBe('env-101');
    expect(component.completedCount()).toBe(1);
  });

  it('opens telemetry modal for a specific envelope', () => {
    const env2 = component.envelopes()[1];
    component.openTelemetryModal(env2);

    expect(component.selectedEnvelope()?.id).toBe(env2.id);
    expect(component.showTelemetryModal()).toBe(true);

    component.showTelemetryModal.set(false);
    expect(component.showTelemetryModal()).toBe(false);
  });

  it('simulates sign progression and completes envelope when all signers sign', () => {
    const env1 = component.envelopes()[0];
    expect(env1.status).toBe('PartiallySigned');
    expect(env1.signers.some(s => s.status === 'Pending')).toBe(true);

    component.simulateSign(env1);

    expect(env1.status).toBe('Completed');
    expect(env1.completedAtUtc).toBeDefined();
    expect(component.completedCount()).toBe(2);
  });

  it('creates and prepends a new envelope when title is provided', () => {
    component.newTitle = 'Vendor Master Services Agreement 2027';
    component.newDocUrl = 'https://cdn.sgrowing.io/contracts/vendor-2027.pdf';
    component.newIsSequential = true;

    const initialLength = component.envelopes().length;
    component.saveEnvelope();

    expect(component.envelopes().length).toBe(initialLength + 1);
    expect(component.envelopes()[0].title).toBe('Vendor Master Services Agreement 2027');
    expect(component.selectedEnvelope()?.title).toBe('Vendor Master Services Agreement 2027');
    expect(component.showCreateModal()).toBe(false);
    expect(component.newTitle).toBe('');
  });

  it('does not add envelope if newTitle is empty', () => {
    component.newTitle = '';
    const initialLength = component.envelopes().length;

    component.saveEnvelope();
    expect(component.envelopes().length).toBe(initialLength);
  });
});
