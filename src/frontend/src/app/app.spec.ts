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
});
