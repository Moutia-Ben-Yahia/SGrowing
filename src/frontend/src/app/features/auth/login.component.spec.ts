// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { createEnvironmentInjector } from '@angular/core';
import { LoginComponent } from './login.component';
import { AuthService } from '../../core/services/auth.service';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';

describe('LoginComponent', () => {
  let component: LoginComponent;
  let authServiceMock: any;
  let routerMock: any;

  beforeEach(() => {
    authServiceMock = {
      login: vi.fn().mockReturnValue(of({ token: 'jwt-mock-token-xyz' })),
      register: vi.fn().mockReturnValue(of({ token: 'jwt-registered-token' }))
    };

    routerMock = {
      navigate: vi.fn()
    };

    const injector = createEnvironmentInjector([
      { provide: AuthService, useValue: authServiceMock },
      { provide: Router, useValue: routerMock }
    ]);

    injector.runInContext(() => {
      component = new LoginComponent();
    });
  });

  it('should initialize with login mode and empty input state', () => {
    expect(component).toBeTruthy();
    expect(component.mode()).toBe('login');
    expect(component.email).toBe('');
    expect(component.password).toBe('');
    expect(component.isLoading()).toBe(false);
    expect(component.errorMessage()).toBeNull();
  });

  it('should toggle between login and register modes', () => {
    component.mode.set('register');
    expect(component.mode()).toBe('register');

    component.mode.set('login');
    expect(component.mode()).toBe('login');
  });

  it('should prefill demo admin credentials when triggered', () => {
    component.fillDemoAdmin();

    expect(component.email).toBe('admin@sgrowing.io');
    expect(component.password).toBe('SecurePassword999!');
  });

  it('should authenticate user and navigate to root on successful login', () => {
    component.email = 'operator@sgrowing.io';
    component.password = 'ValidPassword123!';

    component.handleLogin();

    expect(authServiceMock.login).toHaveBeenCalledWith({
      email: 'operator@sgrowing.io',
      password: 'ValidPassword123!'
    });
    expect(component.isLoading()).toBe(false);
    expect(routerMock.navigate).toHaveBeenCalledWith(['/']);
  });

  it('should display error message on failed login attempt', () => {
    authServiceMock.login.mockReturnValue(
      throwError(() => ({ error: { error: 'Invalid email or password' } }))
    );

    component.email = 'wrong@sgrowing.io';
    component.password = 'WrongPass';

    component.handleLogin();

    expect(component.isLoading()).toBe(false);
    expect(component.errorMessage()).toBe('Invalid email or password');
    expect(routerMock.navigate).not.toHaveBeenCalled();
  });

  it('should create new enterprise organization on registration', () => {
    component.mode.set('register');
    component.orgName = 'Carthage AI Corp';
    component.fullName = 'Leila Trabelsi';
    component.email = 'leila@carthage.tn';
    component.password = 'MasterPass999!';

    component.handleRegister();

    expect(authServiceMock.register).toHaveBeenCalledWith({
      organizationName: 'Carthage AI Corp',
      fullName: 'Leila Trabelsi',
      email: 'leila@carthage.tn',
      password: 'MasterPass999!'
    });
    expect(routerMock.navigate).toHaveBeenCalledWith(['/']);
  });
});
