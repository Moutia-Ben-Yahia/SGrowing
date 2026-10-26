import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const token = authService.accessToken();
  const tenant = authService.currentTenant();

  let headers = req.headers;

  if (token) {
    headers = headers.set('Authorization', `Bearer ${token}`);
  }

  if (tenant?.id) {
    headers = headers.set('X-Tenant-Id', tenant.id);
  }

  const cloned = req.clone({ headers });
  return next(cloned);
};
