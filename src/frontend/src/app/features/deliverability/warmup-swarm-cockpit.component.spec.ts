// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { createEnvironmentInjector, signal } from '@angular/core';
import { of } from 'rxjs';
import { WarmupSwarmCockpitComponent } from './warmup-swarm-cockpit.component';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { WarmupDialogueThread, SeedPlacementReportDto } from '../../core/models/pillars.models';

describe('WarmupSwarmCockpitComponent', () => {
  let component: WarmupSwarmCockpitComponent;
  let apiServiceMock: any;
  let authServiceMock: any;

  const mockThread: WarmupDialogueThread = {
    threadId: 'th-100',
    threadSubject: 'Partnership & Co-Marketing Opportunity',
    turns: [
      {
        turnIndex: 1,
        senderEmail: 'outreach@growth-corp.com',
        recipientEmail: 'warmup-partner@peer-swarm.org',
        body: 'Hi Sarah, are you available for a brief sync next week?',
        dwellDelaySeconds: 45
      },
      {
        turnIndex: 2,
        senderEmail: 'warmup-partner@peer-swarm.org',
        recipientEmail: 'outreach@growth-corp.com',
        body: 'Sure! Tuesday morning works best for me.',
        dwellDelaySeconds: 60
      }
    ]
  };

  const mockPlacementReport: SeedPlacementReportDto = {
    testId: 'test-999',
    senderEmail: 'outreach@growth-corp.com',
    overallPlacementRate: 94.5,
    inboxCount: 19,
    spamCount: 1,
    missingCount: 0,
    totalSeeds: 20,
    espPlacements: [
      {
        esp: 'Google Workspace',
        inboxRate: 95.0,
        spamRate: 5.0,
        inboxCount: 9,
        spamCount: 1,
        totalSeeds: 10
      }
    ],
    inboxingRecommendations: [
      'Increase delay between batches to 90 seconds',
      'Maintain steady volume ramp'
    ]
  };

  beforeEach(() => {
    apiServiceMock = {
      generateWarmupThread: vi.fn().mockReturnValue(of(mockThread)),
      runPlacementTest: vi.fn().mockReturnValue(of(mockPlacementReport))
    };

    authServiceMock = {
      currentUser: signal({ id: 'u-1', tenantId: 't-1' })
    };

    const injector = createEnvironmentInjector([
      { provide: ApiService, useValue: apiServiceMock },
      { provide: AuthService, useValue: authServiceMock }
    ]);

    injector.runInContext(() => {
      component = new WarmupSwarmCockpitComponent();
    });
  });

  it('should initialize with default threads tab and parameters', () => {
    expect(component).toBeTruthy();
    expect(component.activeTab()).toBe('threads');
    expect(component.threadSender).toBe('outreach@growth-corp.com');
    expect(component.turnCount).toBe(4);
  });

  it('should switch between threads and placement tabs', () => {
    component.activeTab.set('placement');
    expect(component.activeTab()).toBe('placement');

    component.activeTab.set('threads');
    expect(component.activeTab()).toBe('threads');
  });

  it('should generate conversational dialogue thread', () => {
    component.generateThread();

    expect(apiServiceMock.generateWarmupThread).toHaveBeenCalledWith(
      expect.objectContaining({
        senderEmail: 'outreach@growth-corp.com',
        turnsCount: 4
      })
    );
    expect(component.dialogueThread()?.threadSubject).toBe('Partnership & Co-Marketing Opportunity');
    expect(component.isGeneratingThread()).toBe(false);
  });

  it('should run ESP seed placement test and record results', () => {
    component.runPlacementTest();

    expect(apiServiceMock.runPlacementTest).toHaveBeenCalledWith(
      expect.objectContaining({
        tenantId: 't-1',
        senderEmail: 'outreach@growth-corp.com'
      })
    );
    expect(component.placementReport()?.overallPlacementRate).toBe(94.5);
    expect(component.isRunningPlacement()).toBe(false);
  });

  it('should resolve ESP provider names correctly', () => {
    expect(component.getEspName('0')).toBe('Google Workspace');
    expect(component.getEspName('Microsoft')).toBe('Microsoft 365');
    expect(component.getEspName('Yahoo')).toBe('Yahoo / AOL');
    expect(component.getEspName('Zoho')).toBe('Zoho Mail');
    expect(component.getEspName('Custom')).toBe('Proton / Apple');
  });
});
