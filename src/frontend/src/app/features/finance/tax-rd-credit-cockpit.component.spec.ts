// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { TaxRdCreditCockpitComponent } from './tax-rd-credit-cockpit.component';

describe('TaxRdCreditCockpitComponent', () => {
  it('should initialize with qualified R&D projects and QRE totals', () => {
    const comp = new TaxRdCreditCockpitComponent();
    expect(comp.qualifiedProjectsCount()).toBe(2);
    expect(comp.totalQre()).toBe(3050000);
    expect(comp.totalTaxCredit()).toBe(305000);
    expect(comp.projects().length).toBe(2);
    expect(comp.claims().length).toBe(1);
  });

  it('should qualify new R&D project and update QRE total', () => {
    const comp = new TaxRdCreditCockpitComponent();
    const initialProjects = comp.qualifiedProjectsCount();
    const initialQre = comp.totalQre();
    comp.qualifyNewProject();

    expect(comp.qualifiedProjectsCount()).toBe(initialProjects + 1);
    expect(comp.totalQre()).toBe(initialQre + 930000);
    expect(comp.projects()[initialProjects].fourPartTestStatus).toBe('FULLY_QUALIFIED');
  });
});
