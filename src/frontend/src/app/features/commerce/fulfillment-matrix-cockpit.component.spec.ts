import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FulfillmentMatrixCockpitComponent } from './fulfillment-matrix-cockpit.component';

describe('FulfillmentMatrixCockpitComponent', () => {
  let component: FulfillmentMatrixCockpitComponent;
  let fixture: ComponentFixture<FulfillmentMatrixCockpitComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FulfillmentMatrixCockpitComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(FulfillmentMatrixCockpitComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should initialize with 4 regional hubs and computed stock', () => {
    expect(component).toBeTruthy();
    expect(component.warehouses().length).toBe(4);
    expect(component.totalStockUnits()).toBe(18500 + 14200 + 22000 + 16800);
    expect(component.plans().length).toBe(2);
  });

  it('should simulate a new order allocation and append to plans', () => {
    const initialPlansCount = component.plans().length;
    component.planNewOrder();

    expect(component.plans().length).toBe(initialPlansCount + 1);
    expect(component.plans()[0].assignedWarehouse).toContain('Reno');
    expect(component.plans()[0].isSplit).toBe(false);
  });
});
