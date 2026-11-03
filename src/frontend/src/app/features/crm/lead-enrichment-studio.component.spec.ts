// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { LeadEnrichmentStudioComponent, EnrichedLead } from './lead-enrichment-studio.component';

describe('LeadEnrichmentStudioComponent (Autonomous Multi-Factor Lead Enrichment Radar)', () => {
  let component: LeadEnrichmentStudioComponent;
  let mockHttp: any;

  const sampleLead: EnrichedLead = {
    id: 'lead-test-1',
    email: 'cto@maghrebtech.tn',
    companyName: 'Maghreb Tech Solutions',
    domain: 'maghrebtech.tn',
    dnsVerification: {
      hasValidMxRecord: true,
      mxRecords: ['aspmx.l.google.com'],
      dnsLookupMs: 42,
      isDisposable: false,
      spfRecordFound: true,
      spfScore: 25,
      dkimValid: true,
      dkimScore: 25,
      dmarcEnforced: true,
      dmarcPolicy: 'reject',
      dmarcScore: 25,
      deliverabilityScore: 95,
      status: 'Valid',
      reason: 'All SPF, DKIM, DMARC passed'
    },
    firmographics: {
      domain: 'maghrebtech.tn',
      rootDomain: 'maghrebtech.tn',
      companyName: 'Maghreb Tech Solutions',
      industry: 'Software',
      headcountTier: '51-200',
      estimatedAnnualRevenue: '$5M - $10M',
      headquarters: 'Tunis, Tunisia',
      foundedYear: 2018,
      firmographicScore: 88
    },
    technographics: {
      technologies: [{ name: 'Angular', category: 'Frontend', confidence: 0.95 }],
      technographicScore: 85,
      stackSummary: 'Modern enterprise stack'
    },
    intentScore: 80,
    compositeScore: 87,
    scoreBreakdown: {
      firmographicWeight: 0.3,
      firmographicScore: 88,
      firmographicWeighted: 26.4,
      technographicWeight: 0.25,
      technographicScore: 85,
      technographicWeighted: 21.25,
      intentWeight: 0.25,
      intentScore: 80,
      intentWeighted: 20.0,
      deliverabilityWeight: 0.2,
      deliverabilityScore: 95,
      deliverabilityWeighted: 19.0,
      compositeScore: 86.65
    },
    qualificationStatus: 'SalesQualifiedLead',
    assignedSalesRep: 'Amine Khelil',
    createdAtUtc: new Date().toISOString(),
    updatedAtUtc: new Date().toISOString()
  };

  beforeEach(() => {
    mockHttp = {
      get: vi.fn().mockImplementation((url: string) => {
        if (url.includes('/metrics')) {
          return of({
            totalLeads: 1,
            sqlCount: 1,
            mqlCount: 0,
            unqualifiedCount: 0,
            disposableCount: 0,
            sqlRatePercent: 100,
            deliverabilityPassRatePercent: 100,
            averageLeadScore: 87,
            computedAtUtc: new Date().toISOString()
          });
        }
        if (url.includes('/enrichment')) {
          return of([sampleLead]);
        }
        return of({});
      }),
      post: vi.fn().mockImplementation((url: string, body: any) => {
        if (url.includes('/verify-email')) {
          return of({
            email: body?.email,
            domain: 'corp.com',
            details: {
              status: 'Valid',
              deliverabilityScore: 95,
              hasValidMxRecord: true,
              mxRecords: ['mx.corp.com'],
              dnsLookupMs: 30,
              isDisposable: false,
              spfRecordFound: true,
              spfScore: 25,
              dkimValid: true,
              dkimScore: 25,
              dmarcEnforced: true,
              dmarcPolicy: 'reject',
              dmarcScore: 25,
              reason: 'Valid'
            }
          });
        }
        if (url.includes('/recalculate')) {
          return of({
            totalProcessed: 1,
            sqlCount: 1,
            mqlCount: 0,
            unqualifiedCount: 0,
            averageScore: 87
          });
        }
        if (url.includes('/enrich')) {
          return of({
            ...sampleLead,
            id: 'lead-new-2',
            email: body?.email || 'new@corp.com',
            companyName: body?.companyName || 'Corp New'
          });
        }
        return of({});
      })
    };

    const injector = Injector.create({
      providers: [
        { provide: HttpClient, useValue: mockHttp }
      ]
    });

    component = runInInjectionContext(injector, () => new LeadEnrichmentStudioComponent());
    component.ngOnInit();
  });

  it('should initialize with enriched leads and metrics', () => {
    expect(component).toBeTruthy();
    expect(component.leads().length).toBeGreaterThan(0);
    expect(component.selectedLead()).not.toBeNull();
  });

  it('should select lead and display details', () => {
    component.selectLead(sampleLead);
    expect(component.selectedLead()).toBe(sampleLead);
    expect(component.selectedLead()?.email).toBe('cto@maghrebtech.tn');
  });

  it('should enrich a new lead and prepend to the leads signal', () => {
    const initialCount = component.leads().length;
    component.newLeadEmail = 'founder@carthagelog.tn';
    component.newCompanyName = 'Carthage Logistics';
    component.enrichNewLead();

    expect(mockHttp.post).toHaveBeenCalled();
    expect(component.leads().length).toBe(initialCount + 1);
    expect(component.leads()[0].companyName).toBe('Carthage Logistics');
    expect(component.isEnriching()).toBe(false);
  });

  it('should run standalone email deliverability verification', () => {
    component.newLeadEmail = 'info@test.com';
    component.runEmailVerificationOnly();

    expect(mockHttp.post).toHaveBeenCalled();
    expect(component.standaloneVerification()).not.toBeNull();
    expect(component.isVerifying()).toBe(false);
  });

  it('should trigger batch recalculation of composite scores', () => {
    component.recalculateAllScores();

    expect(mockHttp.post).toHaveBeenCalled();
    expect(component.isRecalculating()).toBe(false);
  });
});
