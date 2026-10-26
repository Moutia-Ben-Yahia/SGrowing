import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap, catchError, of } from 'rxjs';
import { AuthResponse, LoginRequest, RegisterRequest, TenantDto, UserDto, UserProfileResponse } from '../models/auth.models';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private http = inject(HttpClient);
  private baseUrl = 'http://localhost:5000/api/auth';

  public currentUser = signal<UserDto | null>(null);
  public currentTenant = signal<TenantDto | null>(null);
  public accessToken = signal<string | null>(null);
  public permissions = signal<string[]>([]);

  public isAuthenticated = computed(() => !!this.accessToken());

  constructor() {
    this.loadSession();
  }

  public login(req: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.baseUrl}/login`, req).pipe(
      tap(res => this.handleAuthSuccess(res))
    );
  }

  public register(req: RegisterRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.baseUrl}/register`, req).pipe(
      tap(res => this.handleAuthSuccess(res))
    );
  }

  public refreshToken(): Observable<AuthResponse | null> {
    const refreshToken = localStorage.getItem('sgrowing_refresh_token');
    if (!refreshToken) {
      this.logout();
      return of(null);
    }

    return this.http.post<AuthResponse>(`${this.baseUrl}/refresh`, { refreshToken }).pipe(
      tap(res => this.handleAuthSuccess(res)),
      catchError(() => {
        this.logout();
        return of(null);
      })
    );
  }

  public logout(): void {
    localStorage.removeItem('sgrowing_access_token');
    localStorage.removeItem('sgrowing_refresh_token');
    localStorage.removeItem('sgrowing_user');
    localStorage.removeItem('sgrowing_tenant');

    this.accessToken.set(null);
    this.currentUser.set(null);
    this.currentTenant.set(null);
    this.permissions.set([]);
  }

  private handleAuthSuccess(res: AuthResponse): void {
    this.accessToken.set(res.accessToken);
    this.currentUser.set(res.user);
    this.currentTenant.set(res.tenant);

    localStorage.setItem('sgrowing_access_token', res.accessToken);
    localStorage.setItem('sgrowing_refresh_token', res.refreshToken);
    localStorage.setItem('sgrowing_user', JSON.stringify(res.user));
    localStorage.setItem('sgrowing_tenant', JSON.stringify(res.tenant));
  }

  private loadSession(): void {
    const token = localStorage.getItem('sgrowing_access_token');
    const userJson = localStorage.getItem('sgrowing_user');
    const tenantJson = localStorage.getItem('sgrowing_tenant');

    if (token && userJson && tenantJson) {
      try {
        this.accessToken.set(token);
        this.currentUser.set(JSON.parse(userJson));
        this.currentTenant.set(JSON.parse(tenantJson));
      } catch {
        this.logout();
      }
    }
  }
}
