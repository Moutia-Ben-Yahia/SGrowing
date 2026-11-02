import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FunnelCanvasBuilderComponent } from './funnel-canvas-builder.component';

describe('FunnelCanvasBuilderComponent', () => {
  let component: FunnelCanvasBuilderComponent;
  let fixture: ComponentFixture<FunnelCanvasBuilderComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FunnelCanvasBuilderComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(FunnelCanvasBuilderComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
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
