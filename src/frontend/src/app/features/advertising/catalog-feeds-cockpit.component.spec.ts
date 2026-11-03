// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { CatalogFeedsCockpitComponent } from './catalog-feeds-cockpit.component';

describe('CatalogFeedsCockpitComponent', () => {
  it('initializes with seed feeds and products with badges', () => {
    const comp = new CatalogFeedsCockpitComponent();
    expect(comp.feeds().length).toBe(3);
    expect(comp.products().length).toBe(3);
    expect(comp.totalBadges()).toBe(4);
  });

  it('adds dynamic promo badge to catalog product', () => {
    const comp = new CatalogFeedsCockpitComponent();
    const p1 = comp.products()[0];
    const initialBadgeCount = p1.badges.length;

    comp.addPromoBadge(p1);
    expect(comp.products()[0].badges.length).toBe(initialBadgeCount + 1);
    expect(comp.products()[0].badges).toContain('Q4 Enterprise Special');
    expect(comp.totalBadges()).toBe(5);
  });

  it('syncs all feeds and updates timestamp', () => {
    const comp = new CatalogFeedsCockpitComponent();
    const oldTimestamp = comp.feeds()[0].lastGeneratedAt;
    comp.syncAllFeeds();

    expect(comp.feeds()[0].lastGeneratedAt).not.toBe(oldTimestamp);
  });
});
