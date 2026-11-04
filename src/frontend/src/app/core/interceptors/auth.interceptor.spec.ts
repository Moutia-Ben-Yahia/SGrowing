import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext, signal } from '@angular/core';
import { HttpRequest, HttpHeaders } from '@angular/common/http';
import { of } from 'rxjs';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from '../services/auth.service';

describe('authInterceptor', () => {
  let mockAuthService: any;
  let injector: Injector;

  beforeEach(() => {
    mockAuthService = {
      accessToken: signal<string | null>(null),
      currentTenant: signal<any>(null)
    };

    injector = Injector.create({
      providers: [
        { provide: AuthService, useValue: mockAuthService }
      ]
    });
  });

  it('should not alter headers when no token or tenant is present', () => {
    const req = new HttpRequest('GET', '/api/test');
    let forwardedReq: HttpRequest<any> | undefined;
    const next = vi.fn().mockImplementation((r: HttpRequest<any>) => {
      forwardedReq = r;
      return of({ status: 200 });
    });

    runInInjectionContext(injector, () => authInterceptor(req, next));

    expect(next).toHaveBeenCalledTimes(1);
    expect(forwardedReq).toBeDefined();
    expect(forwardedReq?.headers.has('Authorization')).toBe(false);
    expect(forwardedReq?.headers.has('X-Tenant-Id')).toBe(false);
  });

  it('should attach Authorization Bearer header when token is available', () => {
    mockAuthService.accessToken.set('jwt-test-token-123');

    const req = new HttpRequest('GET', '/api/test');
    let forwardedReq: HttpRequest<any> | undefined;
    const next = vi.fn().mockImplementation((r: HttpRequest<any>) => {
      forwardedReq = r;
      return of({ status: 200 });
    });

    runInInjectionContext(injector, () => authInterceptor(req, next));

    expect(next).toHaveBeenCalledTimes(1);
    expect(forwardedReq?.headers.get('Authorization')).toBe('Bearer jwt-test-token-123');
    expect(forwardedReq?.headers.has('X-Tenant-Id')).toBe(false);
  });

  it('should attach X-Tenant-Id header when tenant id is available', () => {
    mockAuthService.currentTenant.set({ id: 'tenant-456', name: 'Acme Corp' });

    const req = new HttpRequest('GET', '/api/test');
    let forwardedReq: HttpRequest<any> | undefined;
    const next = vi.fn().mockImplementation((r: HttpRequest<any>) => {
      forwardedReq = r;
      return of({ status: 200 });
    });

    runInInjectionContext(injector, () => authInterceptor(req, next));

    expect(next).toHaveBeenCalledTimes(1);
    expect(forwardedReq?.headers.has('Authorization')).toBe(false);
    expect(forwardedReq?.headers.get('X-Tenant-Id')).toBe('tenant-456');
  });

  it('should attach both Authorization and X-Tenant-Id headers when both exist', () => {
    mockAuthService.accessToken.set('jwt-token-789');
    mockAuthService.currentTenant.set({ id: 'tenant-999', name: 'Global Enterprise' });

    const req = new HttpRequest('POST', '/api/orders', { orderId: 1 });
    let forwardedReq: HttpRequest<any> | undefined;
    const next = vi.fn().mockImplementation((r: HttpRequest<any>) => {
      forwardedReq = r;
      return of({ status: 201 });
    });

    runInInjectionContext(injector, () => authInterceptor(req, next));

    expect(next).toHaveBeenCalledTimes(1);
    expect(forwardedReq?.headers.get('Authorization')).toBe('Bearer jwt-token-789');
    expect(forwardedReq?.headers.get('X-Tenant-Id')).toBe('tenant-999');
  });

  it('should preserve existing request custom headers while injecting authorization', () => {
    mockAuthService.accessToken.set('token-abc');

    const req = new HttpRequest('GET', '/api/data', null, {
      headers: new HttpHeaders().set('X-Correlation-Id', 'corr-123')
    });
    let forwardedReq: HttpRequest<any> | undefined;
    const next = vi.fn().mockImplementation((r: HttpRequest<any>) => {
      forwardedReq = r;
      return of({ status: 200 });
    });

    runInInjectionContext(injector, () => authInterceptor(req, next));

    expect(next).toHaveBeenCalledTimes(1);
    expect(forwardedReq?.headers.get('Authorization')).toBe('Bearer token-abc');
    expect(forwardedReq?.headers.get('X-Correlation-Id')).toBe('corr-123');
  });
});
