export interface UserDto {
  id: string;
  tenantId: string;
  email: string;
  fullName: string;
  role: string;
  avatarUrl?: string;
}

export interface TenantDto {
  id: string;
  name: string;
  slug: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresAtUtc: string;
  user: UserDto;
  tenant: TenantDto;
}

export interface UserProfileResponse {
  user: UserDto;
  tenant: TenantDto;
  permissions: string[];
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  organizationName: string;
  organizationSlug?: string;
  fullName: string;
  email: string;
  password: string;
}
