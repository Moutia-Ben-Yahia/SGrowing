import '@angular/compiler';
import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { AuthService } from './auth.service';
import { AuthResponse } from '../models/auth.models';

describe('AuthService', () => {
  let service: AuthService;
  let mockHttp: any;
  let injector: Injector;

  const mockUser = {
    id: 'user-001',
    email: 'operator@sgrowing.io',
    fullName: 'Jane Doe',
    role: 'Admin'
  };

  const mockTenant = {
    id: 'tenant-001',
    name: 'Main Store',
    slug: 'main-store'
  };

  const mockAuthResponse: AuthResponse = {
    accessToken: 'mock-access-token-xyz',
    refreshToken: 'mock-refresh-token-abc',
    user: mockUser as any,
    tenant: mockTenant as any
  };

  beforeEach(() => {
    localStorage.clear();

    mockHttp = {
      get: vi.fn(),
      post: vi.fn(),
      put: vi.fn(),
      delete: vi.fn()
    };

    injector = Injector.create({
      providers: [
        { provide: HttpClient, useValue: mockHttp }
      ]
    });

    service = runInInjectionContext(injector, () => new AuthService());
  });

  afterEach(() => {
    localStorage.clear();
    vi.restoreAllMocks();
  });

  it('should initialize with null state when localStorage is empty', () => {
    expect(service.currentUser()).toBeNull();
    expect(service.currentTenant()).toBeNull();
    expect(service.accessToken()).toBeNull();
    expect(service.isAuthenticated()).toBe(false);
  });

  it('should restore state from localStorage upon initialization if keys exist', () => {
    localStorage.setItem('sgrowing_access_token', 'stored-token-111');
    localStorage.setItem('sgrowing_user', JSON.stringify(mockUser));
    localStorage.setItem('sgrowing_tenant', JSON.stringify(mockTenant));

    const restoredService = runInInjectionContext(injector, () => new AuthService());

    expect(restoredService.accessToken()).toBe('stored-token-111');
    expect(restoredService.currentUser()).toEqual(mockUser);
    expect(restoredService.currentTenant()).toEqual(mockTenant);
    expect(restoredService.isAuthenticated()).toBe(true);
  });

  it('should execute login, update signals and save session to localStorage', () => {
    mockHttp.post.mockReturnValue(of(mockAuthResponse));

    let responseReceived: AuthResponse | undefined;
    service.login({ email: 'operator@sgrowing.io', password: 'password123' }).subscribe(res => {
      responseReceived = res;
    });

    expect(responseReceived).toEqual(mockAuthResponse);
    expect(service.accessToken()).toBe('mock-access-token-xyz');
    expect(service.currentUser()).toEqual(mockUser);
    expect(service.currentTenant()).toEqual(mockTenant);
    expect(service.isAuthenticated()).toBe(true);

    expect(localStorage.getItem('sgrowing_access_token')).toBe('mock-access-token-xyz');
    expect(localStorage.getItem('sgrowing_refresh_token')).toBe('mock-refresh-token-abc');
  });

  it('should execute register, update signals and save session to localStorage', () => {
    mockHttp.post.mockReturnValue(of(mockAuthResponse));

    let responseReceived: AuthResponse | undefined;
    service.register({ email: 'new@sgrowing.io', password: 'password123', fullName: 'Jane Doe', tenantName: 'Store' }).subscribe(res => {
      responseReceived = res;
    });

    expect(responseReceived).toEqual(mockAuthResponse);
    expect(service.accessToken()).toBe('mock-access-token-xyz');
    expect(service.isAuthenticated()).toBe(true);
  });

  it('should refresh token when valid refresh token exists in localStorage', () => {
    localStorage.setItem('sgrowing_refresh_token', 'active-refresh-token');
    mockHttp.post.mockReturnValue(of(mockAuthResponse));

    let responseReceived: AuthResponse | null | undefined;
    service.refreshToken().subscribe(res => {
      responseReceived = res;
    });

    expect(responseReceived).toEqual(mockAuthResponse);
    expect(mockHttp.post).toHaveBeenCalledWith('http://localhost:5000/api/auth/refresh', {
      refreshToken: 'active-refresh-token'
    });
    expect(service.accessToken()).toBe('mock-access-token-xyz');
  });

  it('should logout and return null when refreshToken is called without token in localStorage', () => {
    localStorage.removeItem('sgrowing_refresh_token');

    let responseReceived: AuthResponse | null | undefined;
    service.refreshToken().subscribe(res => {
      responseReceived = res;
    });

    expect(responseReceived).toBeNull();
    expect(mockHttp.post).not.toHaveBeenCalled();
    expect(service.isAuthenticated()).toBe(false);
  });

  it('should logout and return null when refreshToken endpoint returns an error', () => {
    localStorage.setItem('sgrowing_refresh_token', 'expired-token');
    mockHttp.post.mockReturnValue(throwError(() => new Error('Token expired')));

    let responseReceived: AuthResponse | null | undefined;
    service.refreshToken().subscribe(res => {
      responseReceived = res;
    });

    expect(responseReceived).toBeNull();
    expect(service.accessToken()).toBeNull();
    expect(service.isAuthenticated()).toBe(false);
  });

  it('should clear all credentials and signals on logout', () => {
    mockHttp.post.mockReturnValue(of(mockAuthResponse));
    service.login({ email: 'test@example.com', password: 'pwd' }).subscribe();

    expect(service.isAuthenticated()).toBe(true);

    service.logout();

    expect(service.accessToken()).toBeNull();
    expect(service.currentUser()).toBeNull();
    expect(service.currentTenant()).toBeNull();
    expect(service.permissions()).toEqual([]);
    expect(service.isAuthenticated()).toBe(false);

    expect(localStorage.getItem('sgrowing_access_token')).toBeNull();
    expect(localStorage.getItem('sgrowing_refresh_token')).toBeNull();
  });
});
