export interface DynamicFormFieldDto {
  fieldKey: string;
  label: string;
  fieldType: string; // text, email, phone, select, rating, textarea
  isRequired: boolean;
  options: string[];
  conditionalParentKey?: string;
  conditionalExpectedValue?: string;
}

export interface DynamicFormDefinitionDto {
  id: string;
  tenantId: string;
  title: string;
  slug: string;
  description: string;
  fieldsSchemaJson: string;
  submitAction: string;
  actionPayload: string;
  submissionCount: number;
  isPublished: boolean;
  createdAtUtc: string;
}

export interface DynamicFormSubmissionDto {
  id: string;
  tenantId: string;
  formId: string;
  respondentContactId?: string;
  respondentIp: string;
  answersJson: string;
  completedInSeconds: number;
  submittedAtUtc: string;
}

export interface CryptographicPdfRecordDto {
  id: string;
  tenantId: string;
  documentTitle: string;
  documentContent: string;
  originalSha256: string;
  sealedSha256: string;
  signaturePlacementJson: string;
  auditTrailHashChainJson: string;
  status: string; // Draft, PendingSignatures, Sealed
  sealedAtUtc?: string;
  createdAtUtc: string;
}
