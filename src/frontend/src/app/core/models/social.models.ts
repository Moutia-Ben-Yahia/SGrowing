export enum SocialPlatform {
  Facebook = 1,
  Instagram = 2,
  Threads = 3,
  X = 4,
  LinkedIn = 5,
  Telegram = 6,
  TikTok = 7,
  YouTube = 8
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

export enum SocialRuleMatchType {
  Exact = 1,
  Contains = 2,
  Regex = 3,
  IntentSentiment = 4
}

export enum CommentProcessingStatus {
  NoMatch = 0,
  RepliedPublicly = 1,
  DmDelivered = 2,
  DuplicateSkipped = 3,
  RateLimitThrottled = 4,
  Failed = 5
}

export interface SocialCommentRuleDto {
  id: string;
  tenantId: string;
  name: string;
  platform?: SocialPlatform | null;
  matchType: SocialRuleMatchType;
  keywords: string[];
  regexPattern?: string | null;
  publicReplyTemplates: string[];
  privateDmTemplate: string;
  discountPrefix: string;
  discountPercent: number;
  baseCheckoutUrl: string;
  utmSource: string;
  utmMedium: string;
  utmCampaign: string;
  minJitterSeconds: number;
  maxJitterSeconds: number;
  maxDmsPerHour: number;
  maxDmsPerMinute: number;
  isEnabled: boolean;
  priority: number;
  createdAtUtc?: string;
  updatedAtUtc?: string;
}

export interface IncomingSocialCommentDto {
  tenantId: string;
  platform: string;
  postId: string;
  commentId: string;
  commenterId: string;
  commenterUsername: string;
  commentText: string;
  postTitle?: string;
  postUrl?: string;
  timestampUtc?: string;
}

export interface TwoPhaseReactionResultDto {
  logId: string;
  tenantId: string;
  platform: SocialPlatform;
  postId: string;
  commentId: string;
  commenterUsername: string;
  matched: boolean;
  matchedRuleId?: string | null;
  matchedRuleName?: string | null;
  matchedKeyword?: string | null;
  status: CommentProcessingStatus;
  publicReply?: string | null;
  privateDm?: string | null;
  couponCode?: string | null;
  checkoutUrl?: string | null;
  jitterDelaySeconds: number;
  processedAtUtc: string;
  message?: string | null;
}

export interface CommentProcessingLogDto {
  id: string;
  tenantId: string;
  platform: SocialPlatform;
  postId: string;
  commentId: string;
  commenterId: string;
  commenterUsername: string;
  commentText: string;
  matchedRuleId?: string | null;
  matchedRuleName?: string | null;
  matchedKeyword?: string | null;
  status: CommentProcessingStatus;
  publicReply?: string | null;
  privateDm?: string | null;
  couponCode?: string | null;
  checkoutUrl?: string | null;
  jitterSeconds: number;
  processedAtUtc: string;
  hasClicked: boolean;
  hasConverted: boolean;
  orderValue?: number | null;
}

export interface PlatformFunnelBreakdownDto {
  platform: string;
  comments: number;
  matched: number;
  dmsSent: number;
  clicks: number;
  conversions: number;
  revenue: number;
}

export interface SocialCommentFunnelMetricsDto {
  totalComments: number;
  matchedIntent: number;
  publicRepliesSent: number;
  dmsDelivered: number;
  duplicateSkipped: number;
  rateLimited: number;
  linkClicks: number;
  conversions: number;
  totalRevenue: number;
  matchRatePercentage: number;
  clickThroughRatePercentage: number;
  conversionRatePercentage: number;
  byPlatform: Record<string, PlatformFunnelBreakdownDto>;
  recentLogs: CommentProcessingLogDto[];
}
