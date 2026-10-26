export enum SocialPlatform {
  Facebook = 1,
  Instagram = 2,
  Threads = 3,
  X = 4,
  LinkedIn = 5,
  Telegram = 6
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
