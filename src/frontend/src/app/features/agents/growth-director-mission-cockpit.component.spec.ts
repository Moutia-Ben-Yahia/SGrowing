// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { GrowthDirectorMissionCockpitComponent } from './growth-director-mission-cockpit.component';
import { TranslationService } from '../../core/services/translation.service';

describe('GrowthDirectorMissionCockpitComponent (Domain J3 Supervised Autonomous Mission Loop)', () => {
  let component: GrowthDirectorMissionCockpitComponent;
  let translation: TranslationService;

  beforeEach(() => {
    localStorage.clear();
    translation = new TranslationService();
    translation.setLocale('en-US');
    component = new GrowthDirectorMissionCockpitComponent(translation);
  });

  it('should initialize with 4 decomposed agent tasks and calculated total budget', () => {
    expect(component.tasks().length).toBe(4);
    expect(component.totalAllocatedBudget()).toBe(1250); // 200 + 250 + 650 + 150
    expect(component.requiresHumanReview()).toBe(true);
    expect(component.missionStatus()).toBe('PendingApproval');
  });

  it('should flag tasks with spend > $250 for human approval', () => {
    const adTask = component.tasks().find(t => t.agentType === 'Ad_Optimizer');
    expect(adTask).toBeDefined();
    expect(adTask!.allocatedBudget).toBe(650);
    expect(adTask!.requiresApproval).toBe(true);
    expect(adTask!.isApproved).toBe(false);

    const seoTask = component.tasks().find(t => t.agentType === 'SEO_Specialist');
    expect(seoTask).toBeDefined();
    expect(seoTask!.allocatedBudget).toBe(200);
    expect(seoTask!.requiresApproval).toBe(false);
  });

  it('should approve all tasks and transition mission status to Executed on HITL signoff', () => {
    expect(component.requiresHumanReview()).toBe(true);

    component.approveAndExecute();

    expect(component.requiresHumanReview()).toBe(false);
    expect(component.missionStatus()).toBe('Executed');
    expect(component.tasks().every(t => t.status === 'Executed')).toBe(true);
    expect(component.auditTrail()[0]).toContain('HITL Review approved by Operator');
  });

  it('should reset mission status and record audit log on decomposeMission call', () => {
    component.approveAndExecute();
    expect(component.missionStatus()).toBe('Executed');

    component.decomposeMission();
    expect(component.missionStatus()).toBe('PendingApproval');
    expect(component.auditTrail()[0]).toContain('Decomposed mission for target: 100 Qualified Leads');
  });

  it('should return 0 totalAllocatedBudget and false requiresHumanReview when tasks list is empty', () => {
    component.tasks.set([]);
    expect(component.totalAllocatedBudget()).toBe(0);
    expect(component.requiresHumanReview()).toBe(false);
  });
});
