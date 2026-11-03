// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { createEnvironmentInjector } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { ProposalViewerComponent } from './proposal-viewer.component';

describe('ProposalViewerComponent', () => {
  let component: ProposalViewerComponent;
  let httpMock: any;
  let activatedRouteMock: any;

  beforeEach(() => {
    activatedRouteMock = {
      paramMap: of({
        get: (key: string) => (key === 'token' ? 'prop-token-xyz-123' : null)
      })
    };

    httpMock = {
      get: vi.fn().mockImplementation(() => throwError(() => new Error('API offline'))),
      post: vi.fn().mockReturnValue(of({
        envelopeId: 'env-999',
        proposalTitle: 'Strategic Partnership Proposal',
        signerName: 'Alex Mercer',
        signerEmail: 'alex@mercer.com',
        completedAtUtc: new Date().toISOString(),
        cryptographicDigestSha256: '9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08',
        status: 'Completed',
        certificateUrl: '/certificates/cert.pdf',
        message: 'Successfully sealed'
      }))
    };

    const injector = createEnvironmentInjector([
      { provide: ActivatedRoute, useValue: activatedRouteMock },
      { provide: HttpClient, useValue: httpMock }
    ]);

    injector.runInContext(() => {
      component = new ProposalViewerComponent();
      component.ngOnInit();
    });
  });

  afterEach(() => {
    component.ngOnDestroy();
  });

  it('initializes proposal from token and seeds default proposal data', () => {
    expect(component.token()).toBe('prop-token-xyz-123');
    expect(component.proposal()).toBeDefined();
    expect(component.proposal()?.title).toContain('Platform Mandate');
    expect(component.currentPage()).toBe(1);
    expect(component.totalPages()).toBeGreaterThanOrEqual(1);
    expect(component.readingProgressPercent()).toBeGreaterThan(0);
  });

  it('navigates between pages and detects pricing page', () => {
    expect(component.currentPage()).toBe(1);

    component.nextPage();
    expect(component.currentPage()).toBe(2);

    component.previousPage();
    expect(component.currentPage()).toBe(1);

    component.goToPage(3);
    expect(component.currentPage()).toBe(3);
    expect(component.isCurrentPagePricing()).toBe(true);
  });

  it('controls zoom in, zoom out, and reset zoom', () => {
    expect(component.zoom()).toBe(100);

    component.zoomIn();
    expect(component.zoom()).toBe(110);

    component.zoomOut();
    expect(component.zoom()).toBe(100);

    component.zoomOut();
    expect(component.zoom()).toBe(90);

    component.resetZoom();
    expect(component.zoom()).toBe(100);
  });

  it('evaluates acceptance readiness through canSubmitAcceptance', () => {
    expect(component.canSubmitAcceptance()).toBe(false);

    component.signerName.set('Elena Rostova');
    component.signerEmail.set('elena@enterprise.io');
    expect(component.canSubmitAcceptance()).toBe(false);

    component.agreementChecked.set(true);
    expect(component.canSubmitAcceptance()).toBe(true);
  });

  it('submits acceptance and transitions proposal to completed with certificate', () => {
    component.signerName.set('Elena Rostova');
    component.signerEmail.set('elena@enterprise.io');
    component.agreementChecked.set(true);

    component.submitAcceptance();

    expect(component.acceptanceResult()).toBeDefined();
    expect(component.acceptanceResult()?.status).toBe('Completed');
    expect(component.isCompleted()).toBe(true);
    expect(component.showAcceptanceCertificateModal).toBe(true);
  });
});
