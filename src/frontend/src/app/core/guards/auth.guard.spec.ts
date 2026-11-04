import '@angular/compiler';
import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { Router } from '@angular/router';
import { authGuard } from './auth.guard';
import { AuthService } from '../services/auth.service';

describe('authGuard', () => {
  let mockRouter: any;
  let mockAuthService: any;
  let injector: Injector;

  beforeEach(() => {
    localStorage.clear();

    mockRouter = {
      navigate: vi.fn().mockResolvedValue(true)
    };

    mockAuthService = {
      isAuthenticated: vi.fn().mockReturnValue(false)
    };

    injector = Injector.create({
      providers: [
        { provide: Router, useValue: mockRouter },
        { provide: AuthService, useValue: mockAuthService }
      ]
    });
  });

  afterEach(() => {
    localStorage.clear();
    vi.restoreAllMocks();
  });

  it('should allow activation when authService.isAuthenticated() is true', () => {
    mockAuthService.isAuthenticated.mockReturnValue(true);

    const result = runInInjectionContext(injector, () => (authGuard as any)({}, {}));

    expect(result).toBe(true);
    expect(mockRouter.navigate).not.toHaveBeenCalled();
  });

  it('should allow activation when local storage contains access token even if authService is not initialized', () => {
    mockAuthService.isAuthenticated.mockReturnValue(false);
    localStorage.setItem('sgrowing_access_token', 'valid-jwt-token-sample');

    const result = runInInjectionContext(injector, () => (authGuard as any)({}, {}));

    expect(result).toBe(true);
    expect(mockRouter.navigate).not.toHaveBeenCalled();
  });

  it('should redirect to /login and reject activation when unauthenticated and no token present', () => {
    mockAuthService.isAuthenticated.mockReturnValue(false);

    const result = runInInjectionContext(injector, () => (authGuard as any)({}, {}));

    expect(result).toBe(false);
    expect(mockRouter.navigate).toHaveBeenCalledWith(['/login']);
  });

  it('should navigate to login and return false after token removal', () => {
    localStorage.setItem('sgrowing_access_token', 'temp-token');
    localStorage.removeItem('sgrowing_access_token');
    mockAuthService.isAuthenticated.mockReturnValue(false);

    const result = runInInjectionContext(injector, () => (authGuard as any)({}, {}));

    expect(result).toBe(false);
    expect(mockRouter.navigate).toHaveBeenCalledWith(['/login']);
  });

  it('should return true when both isAuthenticated and local storage token are valid', () => {
    mockAuthService.isAuthenticated.mockReturnValue(true);
    localStorage.setItem('sgrowing_access_token', 'dual-valid-token');

    const result = runInInjectionContext(injector, () => (authGuard as any)({}, {}));

    expect(result).toBe(true);
    expect(mockRouter.navigate).not.toHaveBeenCalled();
  });
});
