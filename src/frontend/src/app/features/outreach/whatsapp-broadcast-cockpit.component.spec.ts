import { ComponentFixture, TestBed } from '@angular/core/testing';
import { WhatsAppBroadcastCockpitComponent } from './whatsapp-broadcast-cockpit.component';

describe('WhatsAppBroadcastCockpitComponent', () => {
  let component: WhatsAppBroadcastCockpitComponent;
  let fixture: ComponentFixture<WhatsAppBroadcastCockpitComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [WhatsAppBroadcastCockpitComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(WhatsAppBroadcastCockpitComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should initialize with approved templates and draft campaign', () => {
    expect(component).toBeTruthy();
    expect(component.templates().length).toBeGreaterThanOrEqual(2);
    expect(component.campaign().status).toBe('Draft');
    expect(component.campaign().total).toBe(25000);
  });

  it('should dispatch campaign and update delivery metrics', () => {
    component.dispatchCampaign();

    expect(component.campaign().status).toBe('Completed');
    expect(component.campaign().sent).toBe(25000);
    expect(component.campaign().delivered).toBeGreaterThan(24000);
    expect(component.campaign().read).toBeGreaterThan(19000);
    expect(component.deliveredPercent()).toBeGreaterThanOrEqual(95);
  });
});
