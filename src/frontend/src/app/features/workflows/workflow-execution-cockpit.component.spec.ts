import { describe, it, expect, beforeEach } from 'vitest';
import { WorkflowExecutionCockpitComponent } from './workflow-execution-cockpit.component';
import { TranslationService } from '../../core/services/translation.service';

describe('WorkflowExecutionCockpitComponent', () => {
  let component: WorkflowExecutionCockpitComponent;

  beforeEach(() => {
    component = new WorkflowExecutionCockpitComponent(new TranslationService());
  });

  it('should initialize with 2 executions and compute success rate', () => {
    expect(component.executions().length).toBe(2);
    expect(component.activeExecutionId()).toBe('exec_9901');
    expect(component.successRate()).toBe(50);
    expect(component.activeExecution()?.steps.length).toBe(4);
  });

  it('should switch active execution', () => {
    component.selectExecution('exec_9902');
    expect(component.activeExecutionId()).toBe('exec_9902');
    expect(component.activeExecution()?.status).toBe('Failed');
  });

  it('should trigger a new demo workflow execution', () => {
    const initialCount = component.executions().length;
    component.triggerDemoExecution();
    expect(component.executions().length).toBe(initialCount + 1);
    expect(component.activeExecution()?.workflowName).toBe('Real-Time Abandoned Cart Recovery');
  });

  it('should replay a failed step and mark execution completed', () => {
    component.selectExecution('exec_9902');
    component.replayStep('st_2');

    expect(component.activeExecution()?.status).toBe('Completed');
    const step2 = component.activeExecution()?.steps.find(s => s.id === 'st_2');
    expect(step2?.status).toBe('Completed');
    expect(step2?.error).toBeUndefined();
  });
});
