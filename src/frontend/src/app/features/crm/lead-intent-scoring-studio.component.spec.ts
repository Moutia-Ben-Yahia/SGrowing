// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import {
  LeadIntentScoringStudioComponent,
  LeadIntentRecord,
  IntentScoringOverview,
  IntentScoringConfig
} from './lead-intent-scoring-studio.component';
import { TranslationService } from '../../core/services/translation.service';

describe('LeadIntentScoringStudioComponent (Predictive Buying Intent & Lead Scoring Engine)', () => {
  let component: LeadIntentScoringStudioComponent;
  let mockHttp: any;
  let mockTranslation: any;

  const mockLeads: LeadIntentRecord[] = [
    {
      leadId: 'lead-1',
      tenantId: '11111111-1111-1111-1111-111111111111',
      contactEmail: 'omar@aramcodigital.com',
      contactName: 'Omar Al-Otaibi',
      jobTitle: 'VP Cloud Engineering',
      firmographics: {
        domain: 'aramcodigital.com',
        companyName: 'Aramco Digital Ventures',
        industry: 'B2B SaaS',
        employeeCountTier: '1000+',
        annualRevenueTier: '$100M+',
        headquartersRegion: 'MENA - Riyadh',
        foundedYear: 2021,
        fitScore: 96.0,
        grade: 'GradeA'
      },
      technographics: {
        installedStack: ['Salesforce', 'AWS', 'Snowflake'],
        compatibleCategories: ['CRM', 'Cloud'],
        hasCompetitorStack: true,
        technographicScore: 92.0
      },
      activeSignals: [
        {
          signalId: 'sig-1',
          type: 'DemoRequest',
          source: 'FirstParty',
          topicOrResource: '/demo-request',
          rawScore: 50.0,
          weight: 1.5,
          occurredAtUtc: new Date().toISOString()
        }
      ],
      scoreBreakdown: {
        firmographicWeight: 0.25,
        firmographicScore: 96.0,
        firmographicWeighted: 24.0,
        technographicWeight: 0.20,
        technographicScore: 92.0,
        technographicWeighted: 18.4,
        firstPartyBehavioralWeight: 0.35,
        firstPartyBehavioralRawScore: 75.0,
        firstPartyBehavioralDecayedScore: 74.8,
        firstPartyBehavioralWeighted: 26.18,
        thirdPartySurgeWeight: 0.20,
        thirdPartySurgeRawScore: 45.0,
        thirdPartySurgeDecayedScore: 40.0,
        thirdPartySurgeWeighted: 8.0,
        compositeScore: 86.6
      },
      compositeScore: 86.6,
      icpGrade: 'GradeA',
      buyingStage: 'ReadyToBuy',
      qualificationStage: 'SalesQualifiedLead',
      recommendedAction: 'ScheduleExecutiveBriefing',
      actionReasoning: 'Score 86.6 (ReadyToBuy) with prime ICP GradeA.',
      lastEvaluatedAtUtc: new Date().toISOString(),
      createdAtUtc: new Date().toISOString()
    },
    {
      leadId: 'lead-2',
      tenantId: '11111111-1111-1111-1111-111111111111',
      contactEmail: 'selim@maghreblogistics.tn',
      contactName: 'Selim Ben Salem',
      jobTitle: 'Chief Technology Officer',
      firmographics: {
        domain: 'maghreblogistics.tn',
        companyName: 'Maghreb Logistics Cloud',
        industry: 'E-commerce & Logistics',
        employeeCountTier: '51-200',
        annualRevenueTier: '$10M-$50M',
        headquartersRegion: 'MENA - Tunis',
        foundedYear: 2019,
        fitScore: 84.0,
        grade: 'GradeB'
      },
      technographics: {
        installedStack: ['PostgreSQL', 'Shopify Plus'],
        compatibleCategories: ['E-commerce', 'Database'],
        hasCompetitorStack: false,
        technographicScore: 82.0
      },
      activeSignals: [],
      scoreBreakdown: {
        firmographicWeight: 0.25,
        firmographicScore: 84.0,
        firmographicWeighted: 21.0,
        technographicWeight: 0.20,
        technographicScore: 82.0,
        technographicWeighted: 16.4,
        firstPartyBehavioralWeight: 0.35,
        firstPartyBehavioralRawScore: 50.0,
        firstPartyBehavioralDecayedScore: 45.0,
        firstPartyBehavioralWeighted: 15.75,
        thirdPartySurgeWeight: 0.20,
        thirdPartySurgeRawScore: 40.0,
        thirdPartySurgeDecayedScore: 35.0,
        thirdPartySurgeWeighted: 7.0,
        compositeScore: 65.2
      },
      compositeScore: 65.2,
      icpGrade: 'GradeB',
      buyingStage: 'Consideration',
      qualificationStage: 'MarketingQualifiedLead',
      recommendedAction: 'TriggerAutomatedTechnicalNurture',
      actionReasoning: 'MQL in Consideration stage with technical signals.',
      lastEvaluatedAtUtc: new Date().toISOString(),
      createdAtUtc: new Date().toISOString()
    },
    {
      leadId: 'lead-3',
      tenantId: '11111111-1111-1111-1111-111111111111',
      contactEmail: 'david@legacyhardware.de',
      contactName: 'David Klein',
      jobTitle: 'Procurement Specialist',
      firmographics: {
        domain: 'legacyhardware.de',
        companyName: 'Legacy Hardware Supplies',
        industry: 'Manufacturing',
        employeeCountTier: '11-50',
        annualRevenueTier: '<$1M',
        headquartersRegion: 'Europe - Berlin',
        foundedYear: 1995,
        fitScore: 35.0,
        grade: 'GradeD'
      },
      technographics: {
        installedStack: ['Access DB'],
        compatibleCategories: ['Legacy'],
        hasCompetitorStack: false,
        technographicScore: 20.0
      },
      activeSignals: [],
      scoreBreakdown: {
        firmographicWeight: 0.25,
        firmographicScore: 35.0,
        firmographicWeighted: 8.75,
        technographicWeight: 0.20,
        technographicScore: 20.0,
        technographicWeighted: 4.0,
        firstPartyBehavioralWeight: 0.35,
        firstPartyBehavioralRawScore: 10.0,
        firstPartyBehavioralDecayedScore: 5.0,
        firstPartyBehavioralWeighted: 1.75,
        thirdPartySurgeWeight: 0.20,
        thirdPartySurgeRawScore: 0.0,
        thirdPartySurgeDecayedScore: 0.0,
        thirdPartySurgeWeighted: 0.0,
        compositeScore: 14.5
      },
      compositeScore: 14.5,
      icpGrade: 'GradeD',
      buyingStage: 'Cold',
      qualificationStage: 'Unqualified',
      recommendedAction: 'DisqualifyOrSuppress',
      actionReasoning: 'ICP Grade D fit score. Disqualify or suppress.',
      lastEvaluatedAtUtc: new Date().toISOString(),
      createdAtUtc: new Date().toISOString()
    }
  ];

  const mockOverview: IntentScoringOverview = {
    totalLeads: 3,
    sqlCount: 1,
    mqlCount: 1,
    unqualifiedCount: 1,
    gradeACount: 1,
    gradeBCount: 1,
    gradeCCount: 0,
    gradeDCount: 1,
    averageCompositeScore: 55.4,
    topSurgingLeads: [mockLeads[0], mockLeads[1]],
    activeConfig: {
      tenantId: '11111111-1111-1111-1111-111111111111',
      weightFirmographic: 0.25,
      weightTechnographic: 0.20,
      weightFirstPartyBehavioral: 0.35,
      weightThirdPartySurge: 0.20,
      decayHalfLifeDays: 14,
      mqlThresholdScore: 60.0,
      sqlThresholdScore: 80.0,
      targetIndustries: ['B2B SaaS', 'Fintech'],
      targetEmployeeTiers: ['51-200', '1000+'],
      targetRegions: ['MENA - Riyadh'],
      updatedAtUtc: new Date().toISOString()
    },
    computedAtUtc: new Date().toISOString()
  };

  const mockConfig: IntentScoringConfig = {
    tenantId: '11111111-1111-1111-1111-111111111111',
    weightFirmographic: 0.25,
    weightTechnographic: 0.20,
    weightFirstPartyBehavioral: 0.35,
    weightThirdPartySurge: 0.20,
    decayHalfLifeDays: 14,
    mqlThresholdScore: 60.0,
    sqlThresholdScore: 80.0,
    targetIndustries: ['B2B SaaS', 'Fintech'],
    targetEmployeeTiers: ['51-200', '1000+'],
    targetRegions: ['MENA - Riyadh'],
    updatedAtUtc: new Date().toISOString()
  };

  beforeEach(() => {
    mockHttp = {
      get: vi.fn().mockImplementation((url: string) => {
        if (url.includes('/overview')) {
          return of(mockOverview);
        }
        if (url.includes('/config')) {
          return of(mockConfig);
        }
        if (url.includes('/crm/intent-scoring')) {
          return of(mockLeads);
        }
        return of({});
      }),
      post: vi.fn().mockImplementation((url: string, body: any) => {
        if (url.includes('/signals')) {
          return of({
            ...mockLeads[0],
            compositeScore: 92.5,
            activeSignals: [
              ...mockLeads[0].activeSignals,
              {
                signalId: 'sig-new',
                type: body?.type || 'DemoRequest',
                source: body?.source || 'FirstParty',
                topicOrResource: body?.topicOrResource || '/pricing',
                rawScore: body?.rawScore || 45,
                weight: body?.weight || 1.2,
                occurredAtUtc: new Date().toISOString()
              }
            ]
          });
        }
        if (url.includes('/leads')) {
          return of({
            ...mockLeads[0],
            leadId: 'lead-new',
            contactEmail: body?.contactEmail || 'new@test.com',
            contactName: body?.contactName || 'New User',
            firmographics: body?.firmographics || mockLeads[0].firmographics,
            compositeScore: 78.0
          });
        }
        if (url.includes('/recalculate')) {
          return of({
            processedCount: 3,
            sqlCount: 1,
            mqlCount: 1,
            unqualifiedCount: 1,
            averageScore: 55.4,
            recalculatedAtUtc: new Date().toISOString()
          });
        }
        if (url.includes('/reset')) {
          return of({ message: 'Reset ok' });
        }
        return of({});
      }),
      put: vi.fn().mockImplementation((url: string, body: any) => {
        if (url.includes('/config')) {
          return of({
            ...mockConfig,
            ...body
          });
        }
        return of({});
      })
    };

    mockTranslation = {
      currentLocale: signal<string>('en-US')
    };

    const injector = Injector.create({
      providers: [
        { provide: HttpClient, useValue: mockHttp },
        { provide: TranslationService, useValue: mockTranslation }
      ]
    });

    component = runInInjectionContext(injector, () => new LeadIntentScoringStudioComponent());
    component.ngOnInit();
  });

  it('1. should create and load all initial leads, overview, and configuration', () => {
    expect(component).toBeTruthy();
    expect(mockHttp.get).toHaveBeenCalledTimes(3);
    expect(component.leads().length).toBe(3);
    expect(component.overview()).not.toBeNull();
    expect(component.config()).not.toBeNull();
    expect(component.selectedLead()?.leadId).toBe('lead-1');
  });

  it('2. should select a lead and update selectedLead signal', () => {
    component.selectLead(mockLeads[1]);
    expect(component.selectedLead()).toBe(mockLeads[1]);
    expect(component.selectedLead()?.contactName).toBe('Selim Ben Salem');
  });

  it('3. should filter leads by search query (name, company, email)', () => {
    component.searchQuery.set('maghreb');
    expect(component.filteredLeads().length).toBe(1);
    expect(component.filteredLeads()[0].firmographics.companyName).toBe('Maghreb Logistics Cloud');

    component.searchQuery.set('david@legacyhardware.de');
    expect(component.filteredLeads().length).toBe(1);
    expect(component.filteredLeads()[0].contactName).toBe('David Klein');

    component.searchQuery.set('nonexistent');
    expect(component.filteredLeads().length).toBe(0);
  });

  it('4. should filter leads by qualification stage (SQL, MQL, Unqualified)', () => {
    component.stageFilter.set('SalesQualifiedLead');
    expect(component.filteredLeads().length).toBe(1);
    expect(component.filteredLeads()[0].leadId).toBe('lead-1');

    component.stageFilter.set('MarketingQualifiedLead');
    expect(component.filteredLeads().length).toBe(1);
    expect(component.filteredLeads()[0].leadId).toBe('lead-2');

    component.stageFilter.set('Unqualified');
    expect(component.filteredLeads().length).toBe(1);
    expect(component.filteredLeads()[0].leadId).toBe('lead-3');
  });

  it('5. should filter leads by ICP grade (GradeA, GradeB, GradeD)', () => {
    component.gradeFilter.set('GradeA');
    expect(component.filteredLeads().length).toBe(1);
    expect(component.filteredLeads()[0].icpGrade).toBe('GradeA');

    component.gradeFilter.set('GradeB');
    expect(component.filteredLeads().length).toBe(1);
    expect(component.filteredLeads()[0].icpGrade).toBe('GradeB');

    component.gradeFilter.set('GradeD');
    expect(component.filteredLeads().length).toBe(1);
    expect(component.filteredLeads()[0].icpGrade).toBe('GradeD');
  });

  it('6. should compute accurate counts for SQL, MQL, Grade A, and average composite score', () => {
    expect(component.sqlCount()).toBe(1);
    expect(component.mqlCount()).toBe(1);
    expect(component.gradeACount()).toBe(1);
    // (86.6 + 65.2 + 14.5) / 3 = 166.3 / 3 = 55.4
    expect(component.averageScore()).toBe(55.4);
  });

  it('7. should switch active tabs (matrix, ingest, config, analytics)', () => {
    expect(component.activeTab()).toBe('matrix');

    component.setTab('ingest');
    expect(component.activeTab()).toBe('ingest');

    component.setTab('config');
    expect(component.activeTab()).toBe('config');

    component.setTab('analytics');
    expect(component.activeTab()).toBe('analytics');
  });

  it('8. should ingest a new intent signal and update selected lead in state', () => {
    component.selectLead(mockLeads[0]);
    component.signalType = 'PricingPageView';
    component.signalSource = 'FirstParty';
    component.signalTopic = '/pricing/enterprise-custom';
    component.signalRawScore = 50;
    component.signalWeight = 1.5;

    component.ingestSignalForSelectedLead();

    expect(mockHttp.post).toHaveBeenCalled();
    expect(component.isIngesting()).toBe(false);
    expect(component.selectedLead()?.compositeScore).toBe(92.5);
  });

  it('9. should create a new prospect lead and prepend to leads signal', () => {
    const initialCount = component.leads().length;
    component.newContactEmail = 'fatima@alrajhipayments.sa';
    component.newContactName = 'Fatima Al-Harbi';
    component.newCompanyName = 'Al-Rajhi Digital Payments';
    component.newFitScore = 95;

    component.createNewLead();

    expect(mockHttp.post).toHaveBeenCalled();
    expect(component.leads().length).toBe(initialCount + 1);
    expect(component.selectedLead()?.contactEmail).toBe('fatima@alrajhipayments.sa');
  });

  it('10. should recalculate all scores via API endpoint', () => {
    component.recalculateAllScores();

    expect(mockHttp.post).toHaveBeenCalledWith('/api/v1/crm/intent-scoring/recalculate', {});
    expect(component.isRecalculating()).toBe(false);
  });

  it('11. should save updated configuration parameters', () => {
    component.configFirmographicWeight = 0.30;
    component.configTechnographicWeight = 0.20;
    component.configFirstPartyWeight = 0.30;
    component.configThirdPartyWeight = 0.20;
    component.configDecayHalfLifeDays = 7;
    component.configMqlThreshold = 55.0;
    component.configSqlThreshold = 75.0;

    component.saveConfiguration();

    expect(mockHttp.put).toHaveBeenCalledWith(
      '/api/v1/crm/intent-scoring/config',
      expect.objectContaining({
        weightFirmographic: 0.30,
        decayHalfLifeDays: 7,
        mqlThresholdScore: 55.0,
        sqlThresholdScore: 75.0
      })
    );
    expect(component.isSavingConfig()).toBe(false);
  });

  it('12. should select lead by id from dropdown', () => {
    component.onLeadSelectById('lead-2');
    expect(component.selectedLead()?.leadId).toBe('lead-2');
    expect(component.selectedLead()?.contactName).toBe('Selim Ben Salem');
  });

  it('13. should return correct badge classes and action icons', () => {
    expect(component.getGradeBadgeClass('GradeA')).toContain('text-emerald-400');
    expect(component.getGradeBadgeClass('GradeB')).toContain('text-sky-400');
    expect(component.getGradeBadgeClass('GradeC')).toContain('text-amber-400');
    expect(component.getGradeBadgeClass('GradeD')).toContain('text-rose-400');

    expect(component.getStageBadgeClass('SalesQualifiedLead')).toContain('text-purple-300');
    expect(component.getStageBadgeClass('MarketingQualifiedLead')).toContain('text-indigo-300');

    expect(component.getBuyingStageClass('ReadyToBuy')).toContain('animate-pulse');
    expect(component.getBuyingStageClass('HighIntent')).toContain('text-violet-300');

    expect(component.getNextActionIcon('ScheduleExecutiveBriefing')).toContain('VIP Briefing');
    expect(component.getNextActionIcon('RouteToSeniorSdrImmediate')).toContain('Instant SDR Dispatch');
    expect(component.getNextActionIcon('DisqualifyOrSuppress')).toContain('Suppressed');
  });

  it('14. should detect RTL when locale is ar-AR without calling isRtl() directly', () => {
    mockTranslation.currentLocale.set('ar-AR');
    expect(component.isRtl()).toBe(true);

    mockTranslation.currentLocale.set('en-US');
    expect(component.isRtl()).toBe(false);
  });
});
