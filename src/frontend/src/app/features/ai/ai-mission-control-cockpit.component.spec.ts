// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { AiMissionControlCockpitComponent } from './ai-mission-control-cockpit.component';

describe('AiMissionControlCockpitComponent', () => {
  let component: AiMissionControlCockpitComponent;

  beforeEach(() => {
    component = new AiMissionControlCockpitComponent();
  });

  it('should initialize with default signals and 5-node task DAG', () => {
    expect(component).toBeTruthy();
    expect(component.objectiveTitle()).toBe('Summer COD Promo in Riyadh');
    expect(component.strategicGoal()).toContain('3.5x Target ROAS');
    expect(component.budgetLimit()).toBe(15000);
    expect(component.targetGeo()).toBe('KSA / Riyadh & Jeddah');
    expect(component.currentMission()).not.toBeNull();
    expect(component.currentTasks().length).toBe(5);

    const taskIds = component.currentTasks().map(t => t.id);
    expect(taskIds).toEqual(['task-1', 'task-2', 'task-3', 'task-4', 'task-5']);

    const task5 = component.currentTasks().find(t => t.id === 'task-5');
    expect(task5?.requiresApproval).toBe(true);
    expect(task5?.agentName).toBe('ApprovalGatekeeper');
  });

  it('should execute task-1 and populate its output payload with market signals', () => {
    expect(component.completedTasksCount()).toBe(0);
    expect(component.canExecuteNext()).toBe(true);

    component.executeNextAutonomousStep();

    const task1 = component.currentTasks().find(t => t.id === 'task-1');
    expect(task1?.status).toBe('Succeeded');
    expect(task1?.output).toBeDefined();
    expect(task1?.output?.['predictedCODSuccessRate']).toBe('86.4%');
    expect(component.completedTasksCount()).toBe(1);
    expect(component.progressPercent()).toBe(20);
  });

  it('should step through tasks 1-4 and pause at task-5 in WaitingApproval status', () => {
    // Step 1: ResearchAgent (task-1)
    component.executeNextAutonomousStep();
    // Step 2: SeoAgent (task-2)
    component.executeNextAutonomousStep();
    // Step 3: ContentAgent (task-3)
    component.executeNextAutonomousStep();
    // Step 4: AdvertisingAgent (task-4) -> triggers task-5 to WaitingApproval
    component.executeNextAutonomousStep();

    const task5 = component.currentTasks().find(t => t.id === 'task-5');
    expect(task5?.status).toBe('WaitingApproval');
    expect(component.canApprove()).toBe(true);
    expect(component.canExecuteNext()).toBe(false);
    expect(component.currentMission()?.status).toBe('WaitingApproval');
  });

  it('should allow human-in-the-loop gatekeeper approval with reviewer notes to launch mission', () => {
    // Fast-forward to task-5 approval
    for (let i = 0; i < 4; i++) {
      component.executeNextAutonomousStep();
    }
    expect(component.canApprove()).toBe(true);

    component.reviewerNotes.set('Approved by VP Growth: Proceed with SAR 15K allocation');
    component.approveMission();

    const task5 = component.currentTasks().find(t => t.id === 'task-5');
    expect(task5?.status).toBe('Succeeded');
    expect(component.currentMission()?.status).toBe('Completed');
    expect(component.currentMission()?.reviewerNotes).toContain('Approved by VP Growth');
    expect(component.progressPercent()).toBe(100);
  });

  it('should allow human-in-the-loop gatekeeper to reject mission and mark as Failed', () => {
    for (let i = 0; i < 4; i++) {
      component.executeNextAutonomousStep();
    }
    expect(component.canApprove()).toBe(true);

    component.reviewerNotes.set('Budget cap too high for test market');
    component.rejectMission();

    expect(component.currentMission()?.status).toBe('Failed');
    const task5 = component.currentTasks().find(t => t.id === 'task-5');
    expect(task5?.status).toBe('Failed');
  });

  it('should reset state when planning a new mission', () => {
    component.executeNextAutonomousStep();
    expect(component.completedTasksCount()).toBe(1);

    component.planNewMission();

    expect(component.completedTasksCount()).toBe(0);
    expect(component.progressPercent()).toBe(0);
    expect(component.currentMission()?.status).toBe('Draft');
    expect(component.currentTasks().every(t => t.status === 'Pending')).toBe(true);
  });
});
