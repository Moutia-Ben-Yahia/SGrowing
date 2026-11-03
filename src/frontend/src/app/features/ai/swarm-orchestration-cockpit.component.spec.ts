// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { SwarmOrchestrationCockpitComponent } from './swarm-orchestration-cockpit.component';
import { TranslationService } from '../../core/services/translation.service';

describe('SwarmOrchestrationCockpitComponent', () => {
  let component: SwarmOrchestrationCockpitComponent;

  beforeEach(() => {
    component = new SwarmOrchestrationCockpitComponent(new TranslationService());
  });

  it('should initialize with default mission, 5 tasks, and 1 pending HITL task', () => {
    expect(component.activeMission().tasks.length).toBe(5);
    expect(component.completedTasksCount()).toBe(4);
    expect(component.pendingApprovalTasks().length).toBe(1);
    expect(component.activeMission().status).toBe('Active');
  });

  it('should approve high-risk task and achieve mission completion', () => {
    component.approveTask('t5', true);

    expect(component.pendingApprovalTasks().length).toBe(0);
    expect(component.completedTasksCount()).toBe(5);
    expect(component.activeMission().status).toBe('Completed');
    expect(component.activeMission().scratchpad['mission_phase']).toBe('MissionObjectiveAchieved');
  });

  it('should reject a high-risk task and record reason', () => {
    component.approveTask('t5', false);

    const task = component.activeMission().tasks.find(t => t.id === 't5');
    expect(task?.status).toBe('Rejected');
    expect(task?.output).toContain('REJECTED');
  });

  it('should launch a new mission and update scratchpad', () => {
    component.launchNewMission();

    expect(component.activeMission().title).toBe('Real-Time Black Friday Viral Blitz');
    expect(component.activeMission().tasks.length).toBe(2);
  });

  it('should append context note to scratchpad memory', () => {
    const initialNotesCount = component.scratchpadEntries().length;
    component.addMemoryNote();

    expect(component.scratchpadEntries().length).toBe(initialNotesCount + 1);
  });
});
