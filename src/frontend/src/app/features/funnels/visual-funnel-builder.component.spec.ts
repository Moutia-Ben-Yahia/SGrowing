// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { VisualFunnelBuilderComponent } from './visual-funnel-builder.component';
import { TranslationService } from '../../core/services/translation.service';

describe('VisualFunnelBuilderComponent', () => {
  let component: VisualFunnelBuilderComponent;

  beforeEach(() => {
    component = new VisualFunnelBuilderComponent(new TranslationService());
  });

  it('should initialize with 3 default blocks and 2 MAB variants', () => {
    expect(component.blocks().length).toBe(3);
    expect(component.variants().length).toBe(2);
    expect(component.leadingVariant().id).toBe('var_2');
  });

  it('should add a new block to the funnel', () => {
    component.addBlock('HeroBanner');
    expect(component.blocks().length).toBe(4);
    expect(component.blocks()[3].type).toBe('HeroBanner');
  });

  it('should remove a block by id', () => {
    component.removeBlock('blk_1');
    expect(component.blocks().length).toBe(2);
    expect(component.blocks().find(b => b.id === 'blk_1')).toBeUndefined();
  });

  it('should reorder blocks upwards and downwards', () => {
    component.moveBlock(0, 1);
    expect(component.blocks()[0].id).toBe('blk_2');
    expect(component.blocks()[1].id).toBe('blk_1');
  });

  it('should simulate traffic burst and recalculate weights', () => {
    const initialWeight = component.variants().find(v => v.id === 'var_2')!.weight;
    component.simulateTrafficBurst();
    const newWeight = component.variants().find(v => v.id === 'var_2')!.weight;

    expect(component.confidenceScore()).toBeGreaterThanOrEqual(99.0);
    expect(newWeight).toBeGreaterThan(0.60);
  });
});
