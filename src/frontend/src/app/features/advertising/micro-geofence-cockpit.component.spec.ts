// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { MicroGeofenceCockpitComponent } from './micro-geofence-cockpit.component';

describe('MicroGeofenceCockpitComponent', () => {
  it('should initialize with active polygons, dwells, and attributed revenue', () => {
    const comp = new MicroGeofenceCockpitComponent();
    expect(comp.activePolygonsCount()).toBe(2);
    expect(comp.dailyAudienceDwells()).toBe(1481);
    expect(comp.footfallLiftPct()).toBe(48.2);
    expect(comp.totalAttributedRevenue()).toBe(85750);
    expect(comp.polygons().length).toBe(2);
    expect(comp.events().length).toBe(1);
  });

  it('should define new geofence polygon and increment active count', () => {
    const comp = new MicroGeofenceCockpitComponent();
    const initialPolys = comp.activePolygonsCount();
    comp.defineNewPolygon();

    expect(comp.activePolygonsCount()).toBe(initialPolys + 1);
    expect(comp.polygons()[initialPolys].venueName).toBe('Javits Center NYC');
    expect(comp.polygons()[initialPolys].venueType).toBe('CONVENTION_CENTER');
  });
});
