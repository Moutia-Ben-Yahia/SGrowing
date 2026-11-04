// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { PipelineVelocityCockpitComponent } from './pipeline-velocity-cockpit.component';

describe('PipelineVelocityCockpitComponent', () => {
  it('should initialize with stages and calculate metrics', () => {
    const comp = new PipelineVelocityCockpitComponent();
    expect(comp.stages().length).toBe(5);
    expect(comp.totalDeals()).toBe(133);
    expect(comp.bottleneckCount()).toBe(3);
    expect(comp.dailyVelocity()).toBeGreaterThan(0);
  });

  it('should select a stage', () => {
    const comp = new PipelineVelocityCockpitComponent();
    const first = comp.stages()[0];
    comp.selectStage(first);
    expect(comp.selectedStage()?.stageId).toBe('stg_discovery');
  });

  it('should accelerate bottleneck stage, reducing dwell days and clearing bottleneck status', () => {
    const comp = new PipelineVelocityCockpitComponent();
    const initialBottlenecks = comp.bottleneckCount();

    comp.accelerateStage('stg_security');

    expect(comp.bottleneckCount()).toBe(initialBottlenecks - 1);
    const updated = comp.stages().find(s => s.stageId === 'stg_security');
    expect(updated?.isBottleneck).toBe(false);
    expect(updated?.averageDwellDays).toBe(21.0); // 28.0 - 7.0
  });

  it('should return 0 dailyVelocity and 0 totalCycleDays when stages list is empty', () => {
    const comp = new PipelineVelocityCockpitComponent();
    comp.stages.set([]);
    expect(comp.totalDeals()).toBe(0);
    expect(comp.totalCycleDays()).toBe(0);
    expect(comp.dailyVelocity()).toBe(0);
    expect(comp.bottleneckCount()).toBe(0);
  });

  it('should update selectedStage when it matches the accelerated stageId', () => {
    const comp = new PipelineVelocityCockpitComponent();
    const security = comp.stages().find(s => s.stageId === 'stg_security')!;
    comp.selectStage(security);
    expect(comp.selectedStage()?.isBottleneck).toBe(true);

    comp.accelerateStage('stg_security');
    expect(comp.selectedStage()?.isBottleneck).toBe(false);
    expect(comp.selectedStage()?.averageDwellDays).toBe(21.0);
  });
});
