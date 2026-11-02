import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { FlowInteractiveCockpitComponent } from './flow-interactive-cockpit.component';

describe('FlowInteractiveCockpitComponent', () => {
  let component: FlowInteractiveCockpitComponent;
  let fixture: ComponentFixture<FlowInteractiveCockpitComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FlowInteractiveCockpitComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(FlowInteractiveCockpitComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create component and render initial flow screens', () => {
    expect(component).toBeTruthy();
    expect(component.flows().length).toBeGreaterThan(0);
    expect(component.selectedFlow()).toBeTruthy();
    expect(component.activeScreenId()).toBe('SCREEN_LEAD_INFO');
  });

  it('should advance from lead info screen to objective screen', () => {
    component.companyInput = 'Hyperion Systems';
    component.goToNextScreen();

    expect(component.activeScreenId()).toBe('SCREEN_USE_CASE');
  });

  it('should submit terminal screen and show success confirmation', () => {
    component.goToNextScreen();
    expect(component.activeScreenId()).toBe('SCREEN_USE_CASE');

    component.submitFlow();
    expect(component.activeScreenId()).toBe('SUCCESS_SCREEN');
  });
});
