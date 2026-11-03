// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { createEnvironmentInjector } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { DocumentTelemetryStudioComponent } from './document-telemetry-studio.component';

describe('DocumentTelemetryStudioComponent', () => {
  let component: DocumentTelemetryStudioComponent;
  let httpMock: any;

  beforeEach(() => {
    httpMock = {
      get: vi.fn().mockImplementation((url: string) => {
        return throwError(() => new Error('API offline - use fallback'));
      }),
      post: vi.fn().mockReturnValue(of({ alertTriggered: true, triggerReason: 'Exceeded Pricing Benchmark' }))
    };

    const injector = createEnvironmentInjector([
      { provide: HttpClient, useValue: httpMock }
    ]);

    injector.runInContext(() => {
      component = new DocumentTelemetryStudioComponent();
      component.ngOnInit();
    });
  });

  afterEach(() => {
    component.ngOnDestroy();
  });

  it('initializes and populates fallback documents and telemetry data when API is offline', () => {
    expect(component.documents().length).toBeGreaterThan(0);
    expect(component.selectedDocId()).toBe(component.documents()[0].id);
    expect(component.selectedDoc()).toBeDefined();
    expect(component.pageHeatmapSummaries().length).toBeGreaterThan(0);
    expect(component.sessions().length).toBeGreaterThan(0);
  });

  it('switches selected document and updates page telemetry', () => {
    if (component.documents().length > 1) {
      const secondDocId = component.documents()[1].id;
      component.onSelectDocument(secondDocId);

      expect(component.selectedDocId()).toBe(secondDocId);
      expect(component.simulatorPage()).toBe(1);
    }
  });

  it('navigates through simulator pages with prevPage and nextPage', () => {
    component.simulatorPage.set(1);
    component.nextPage();
    expect(component.simulatorPage()).toBe(2);

    component.prevPage();
    expect(component.simulatorPage()).toBe(1);

    // PrevPage on page 1 should remain 1
    component.prevPage();
    expect(component.simulatorPage()).toBe(1);
  });

  it('adjusts dwell time and scroll depth sliders and updates buying intent score', () => {
    component.onDwellSliderChange(45);
    expect(component.simulatorDwellSec()).toBe(45);

    component.onScrollSliderChange(90);
    expect(component.simulatorScrollDepth()).toBe(90);

    expect(component.simulatedScore()).toBeGreaterThanOrEqual(0);
    expect(['Hot', 'Warm', 'Casual', 'Cold']).toContain(component.simulatedTier());
  });

  it('sends simulator heartbeat and handles alert trigger', () => {
    component.sendSimulatorHeartbeat(5000);
    expect(httpMock.post).toHaveBeenCalled();
    expect(component.simulatorAlert()).toBe('Exceeded Pricing Benchmark');
  });

  it('opens and closes create link modal', () => {
    component.openCreateModal();
    expect(component.isCreateModalOpen()).toBe(true);

    component.closeCreateModal();
    expect(component.isCreateModalOpen()).toBe(false);
  });
});
