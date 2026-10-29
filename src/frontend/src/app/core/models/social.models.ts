export enum SocialPlatform {
  Facebook = 1,
  Instagram = 2,
  Threads = 3,
  X = 4,
  LinkedIn = 5,
  Telegram = 6,
  TikTok = 7
}

export enum SocialPostStatus {
  Draft = 1,
  Scheduled = 2,
  Published = 3,
  Failed = 4
}

export interface SocialChannelMetadataDto {
  platform: string;
  id: number;
  charLimit: number;
  supportsMedia: boolean;
  icon: string;
}

export interface SocialAccountDto {
  id: string;
  tenantId: string;
  platform: SocialPlatform;
  accountName: string;
  externalAccountId: string;
  isActive: boolean;
  tokenExpiresAtUtc?: string;
  refreshTokenRequired?: boolean;
}

export interface SocialPostDto {
  id: string;
  tenantId: string;
  content: string;
  mediaUrls: string[];
  scheduledAtUtc?: string;
  publishedAtUtc?: string;
  status: SocialPostStatus;
  targetPlatforms: SocialPlatform[];
  createdAtUtc: string;
}

export interface DeflectCommentResultDto {
  deflected: boolean;
  replyText?: string;
}

export interface PlatformAdaptedPostDto {
  platform: SocialPlatform;
  formattedContent: string;
  recommendedHashtags: string[];
  characterCount: number;
  maxCharacterLimit: number;
  isWithinLimit: boolean;
  recommendedAspectRatio: string;
  validationWarning?: string | null;
}

export interface ViralHookSuggestionDto {
  hookText: string;
  angle: string;
  estimatedViralityScore: number;
}

export interface AccountTokenHealthDto {
  accountId: string;
  tenantId: string;
  platform: SocialPlatform;
  accountName: string;
  tokenExpiresAtUtc?: string;
  refreshTokenRequired: boolean;
  isExpired: boolean;
  daysUntilExpiration: number;
  statusSummary: string;
}

export interface TokenHealthAuditReportDto {
  tenantId: string;
  auditedAtUtc: string;
  totalAccountsAudited: number;
  healthyAccountsCount: number;
  expiringOrRequiredRefreshCount: number;
  accounts: AccountTokenHealthDto[];
}

export interface CharacterLimitValidationResultDto {
  platform: SocialPlatform;
  maxLimit: number;
  actualLength: number;
  isValid: boolean;
  remainingCharacters: number;
}
