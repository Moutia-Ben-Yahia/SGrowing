export interface DigitalAssetDto {
  id: string;
  tenantId: string;
  title: string;
  fileName: string;
  storagePath: string;
  mimeType: string;
  fileSizeBytes: number;
  maxAllowedDownloads: number;
  expiryHours: number;
  createdAtUtc: string;
}

export interface DigitalDownloadTokenDto {
  id: string;
  tenantId: string;
  token: string;
  assetId: string;
  orderId?: string;
  contactId?: string;
  downloadCount: number;
  maxAllowedDownloads: number;
  expiresAtUtc: string;
  isRevoked: boolean;
  createdAtUtc: string;
}

export interface ProductLicenseKeyDto {
  id: string;
  tenantId: string;
  licenseKey: string;
  orderId?: string;
  productSku: string;
  maxSeats: number;
  activatedSeats: number;
  status: string;
  expiresAtUtc?: string;
  createdAtUtc: string;
}

export interface PimVariantMatrixDto {
  id: string;
  tenantId: string;
  baseSku: string;
  variantSku: string;
  attributesJson: string;
  price: number;
  stockQuantity: number;
  createdAtUtc: string;
}
