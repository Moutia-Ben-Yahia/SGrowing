// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { of, throwError } from 'rxjs';
import { CreativeRadarStudioComponent } from './creative-radar-studio.component';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { UgcVideoScriptDto, CompetitorRadarSummaryDto } from '../../core/models/horizons-advanced.models';

describe('CreativeRadarStudioComponent (Autonomous Creative Studio & Competitor Radar)', () => {
  let component: CreativeRadarStudioComponent;
  let apiServiceMock: {
    getUgcScripts: ReturnType<typeof vi.fn>;
    generateUgcScripts: ReturnType<typeof vi.fn>;
    getCompetitorRadar: ReturnType<typeof vi.fn>;
    ingestCompetitorAd: ReturnType<typeof vi.fn>;
  };
  let authServiceMock: {
    currentTenant: ReturnType<typeof vi.fn>;
  };

  const sampleScript: UgcVideoScriptDto = {
    id: 'script-1',
    tenantId: 'a0000000-0000-0000-0000-000000000001',
    productSku: 'EAR-PRO-ANC',
    productName: 'Wireless ANC Earbuds Pro',
    language: 'Darija',
    targetAudience: 'Shoppers 18-35 in Grand Tunis & Sahel',
    hookAngle: 1,
    hookText: 'T3ebt mel les écouteurs li ymotou fi jem3a?',
    scenesJson: JSON.stringify([
      { sceneNumber: 1, visualCue: 'Broken earphones', creatorAction: 'Sigh', voiceoverText: 'Check this out', onscreenText: 'Stop wasting money', durationSeconds: 3 }
    ]),
    callToAction: 'Order now with COD',
    estimatedDurationSeconds: 15,
    createdAtUtc: new Date().toISOString()
  };

  const sampleRadar: CompetitorRadarSummaryDto = {
    totalMonitoredCompetitors: 5,
    activeCampaignsCount: 2,
    priceDisparityAlertsCount: 2,
    activeCompetitorCampaigns: [
      {
        id: 'ad-101',
        tenantId: 'a0000000-0000-0000-0000-000000000001',
        competitorName: 'ElectroShop TN',
        platform: 'Meta',
        hookSnippet: 'Wireless buds with 50% discount',
        offerDetails: 'Free doorstep shipping',
        estimatedDailySpend: 150,
        threatLevel: 'High',
        isActive: true,
        firstSeenAtUtc: new Date().toISOString(),
        lastSeenAtUtc: new Date().toISOString()
      }
    ],
    priceAlerts: [
      {
        id: 'p-101',
        tenantId: 'a0000000-0000-0000-0000-000000000001',
        targetSku: 'EAR-PRO-ANC',
        competitorName: 'BazarExpress TN',
        competitorUrl: 'https://bazarexpress.tn',
        competitorPrice: 79.0,
        ourPrice: 89.0,
        priceDifferencePercentage: -11.2,
        recommendedCounterAction: 'Counter with 2-pack bundle',
        lastCheckedAtUtc: new Date().toISOString()
      }
    ]
  };

  beforeEach(() => {
    apiServiceMock = {
      getUgcScripts: vi.fn().mockReturnValue(of([sampleScript])),
      generateUgcScripts: vi.fn().mockReturnValue(of([sampleScript])),
      getCompetitorRadar: vi.fn().mockReturnValue(of(sampleRadar)),
      ingestCompetitorAd: vi.fn().mockReturnValue(of(sampleRadar.activeCompetitorCampaigns[0]))
    };

    authServiceMock = {
      currentTenant: vi.fn().mockReturnValue({
        id: 'a0000000-0000-0000-0000-000000000001',
        name: 'SGrowing Brand'
      })
    };

    const injector = Injector.create({
      providers: [
        { provide: ApiService, useValue: apiServiceMock },
        { provide: AuthService, useValue: authServiceMock }
      ]
    });

    component = runInInjectionContext(injector, () => new CreativeRadarStudioComponent());
    component.ngOnInit();
  });

  it('should initialize and load UGC scripts and competitor radar data', () => {
    expect(component).toBeTruthy();
    expect(apiServiceMock.getUgcScripts).toHaveBeenCalledWith('a0000000-0000-0000-0000-000000000001', 'EAR-PRO-ANC');
    expect(apiServiceMock.getCompetitorRadar).toHaveBeenCalledWith('a0000000-0000-0000-0000-000000000001');
    expect(component.scripts().length).toBe(1);
    expect(component.competitorAds().length).toBe(1);
    expect(component.priceAlerts().length).toBe(1);
  });

  it('should switch tabs between ugc, competitor-ads, and price-radar', () => {
    component.activeTab.set('competitor-ads');
    expect(component.activeTab()).toBe('competitor-ads');

    component.activeTab.set('price-radar');
    expect(component.activeTab()).toBe('price-radar');

    component.activeTab.set('ugc');
    expect(component.activeTab()).toBe('ugc');
  });

  it('should fallback to local simulation if API fails', () => {
    apiServiceMock.getUgcScripts.mockReturnValue(throwError(() => new Error('Offline')));
    apiServiceMock.generateUgcScripts.mockReturnValue(throwError(() => new Error('Offline')));
    apiServiceMock.getCompetitorRadar.mockReturnValue(throwError(() => new Error('Offline')));

    component.loadData();

    expect(component.scripts().length).toBeGreaterThan(0);
    expect(component.competitorAds().length).toBeGreaterThan(0);
    expect(component.priceAlerts().length).toBeGreaterThan(0);
  });

  it('should parse script scenes JSON and calculate deficit SKUs count', () => {
    const scenes = component.parseScenes(sampleScript.scenesJson);
    expect(scenes.length).toBe(1);
    expect(scenes[0].visualCue).toBe('Broken earphones');

    expect(component.countDeficitSkus()).toBe(1);
  });

  it('should map hook angles to descriptive angle labels', () => {
    expect(component.getAngleLabel(1)).toBe('Angle 1: Problem-Agitator');
    expect(component.getAngleLabel(2)).toBe('Angle 2: Competitor Callout');
    expect(component.getAngleLabel(3)).toBe('Angle 3: Cultural COD Trust');
    expect(component.getAngleLabel(4)).toBe('Angle 4: ASMR Sensory Unboxing');
    expect(component.getAngleLabel(5)).toBe('Angle 5: Scarcity Flash Deal');
    expect(component.getAngleLabel(99)).toBe('UGC Video Script');
  });
});
