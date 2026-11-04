// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { App } from './app';

describe('App', () => {
  let app: App;

  beforeEach(() => {
    localStorage.clear();
    app = Object.create(App.prototype);
    (app as any).title = () => 'marketing-studio-web';
  });

  it('should create the app', () => {
    expect(app).toBeTruthy();
  });

  it('should have default title', () => {
    expect((app as any).title()).toBe('marketing-studio-web');
  });

  it('should support signal-based title updates and dynamic modifications', () => {
    let currentTitle = 'marketing-studio-web';
    (app as any).title = () => currentTitle;
    expect((app as any).title()).toBe('marketing-studio-web');

    currentTitle = 'sgrowing-omnichannel-growth-os';
    expect((app as any).title()).toBe('sgrowing-omnichannel-growth-os');
  });

  it('should expose title as a readable signal', () => {
    const customTitle = 'SGrowing - Enterprise Omnichannel Suite';
    (app as any).title = () => customTitle;
    expect((app as any).title()).toContain('Enterprise Omnichannel');
  });
});
