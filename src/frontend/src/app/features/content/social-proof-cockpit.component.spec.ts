// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { SocialProofCockpitComponent } from './social-proof-cockpit.component';

describe('SocialProofCockpitComponent (Horizon 62)', () => {
  let component: SocialProofCockpitComponent;

  beforeEach(() => {
    localStorage.clear();
    component = new SocialProofCockpitComponent();
  });

  it('should initialize with approved customer testimonials and ratings', () => {
    expect(component).toBeTruthy();
    expect(component.items().length).toBeGreaterThanOrEqual(2);
    expect(component.approvedCount()).toBe(2);
    expect(component.videoCount()).toBeGreaterThanOrEqual(1);
  });

  it('should request and add new customer testimonial', () => {
    const initialCount = component.items().length;
    component.requestNewTestimonial();
    expect(component.items().length).toBe(initialCount + 1);
    expect(component.items()[0].name).toBe('Elena Rostova');
  });

  it('should publish wall of love widget snippet', () => {
    component.publishWidget();
    expect(component.widgetSnippet()).toContain('Published');
    expect(component.widgetSnippet()).toContain('Schema.org');
  });

  it('should compute avgRating and handle empty list fallback safely', () => {
    expect(component.avgRating()).toBe('5.0');

    component.items.set([]);
    expect(component.avgRating()).toBe(0);
    expect(component.approvedCount()).toBe(0);
    expect(component.videoCount()).toBe(0);
  });

  it('should update approvedCount and avgRating when non-approved or 4-star items are added', () => {
    component.items.update(list => [
      {
        id: 'tst_custom',
        name: 'Alex Johnson',
        title: 'CTO',
        company: 'CloudFlow Inc',
        domain: 'cloudflow.io',
        format: 'TextQuote',
        stars: 4,
        soundbite: 'Solid platform.',
        fullQuote: 'Solid platform for scaling.',
        sentiment: 0.85,
        isApproved: false
      },
      ...list
    ]);

    expect(component.approvedCount()).toBe(2);
    expect(component.items().length).toBe(3);
    expect(component.avgRating()).toBe('4.7');
  });
});
