// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { FunnelCanvasBuilderComponent } from './funnel-canvas-builder.component';

describe('FunnelCanvasBuilderComponent', () => {
  let component: FunnelCanvasBuilderComponent;

  beforeEach(() => {
    component = new FunnelCanvasBuilderComponent();
  });

  it('should initialize with default canvas elements and desktop viewport', () => {
    expect(component).toBeTruthy();
    expect(component.elements().length).toBe(3);
    expect(component.selectedViewport()).toBe('desktop');
  });

  it('should switch viewports smoothly', () => {
    component.selectedViewport.set('mobile');
    expect(component.selectedViewport()).toBe('mobile');

    component.selectedViewport.set('tablet');
    expect(component.selectedViewport()).toBe('tablet');
  });

  it('should add and remove component blocks on canvas', () => {
    const initial = component.elements().length;
    component.addElement('TestimonialGrid', 'Client Reviews', '99.9% customer satisfaction');

    expect(component.elements().length).toBe(initial + 1);

    component.removeElement(component.elements()[component.elements().length - 1].id);
    expect(component.elements().length).toBe(initial);
  });

  it('should compile and export semantic HTML', () => {
    component.compileAndExportHtml();

    expect(component.showExportModal()).toBe(true);
    expect(component.exportedHtml()).toContain('funnel-root');
    expect(component.exportedHtml()).toContain('Scale Revenue');
  });
});
