# ADR-003: Cryptographic PAdES Document Vault and MinIO Cold Storage Mirror

## Status
**Accepted & Implemented** (Commit `218df4b`, `3389a23`)

## Context
Commercial agreements, proposals, and logistics manifests require legal non-repudiation, tamper-evidence, and long-term regulatory audit readiness compliant with ISO 32000-1 and eIDAS / Adobe PAdES standards.

## Decision
1. **PAdES-B-LTV Document Vault (`IPersistentDocumentVaultService`)**:
   - ISO 32000-1 byte-range SHA-256 document hashing (`/ByteRange [o1 l1 o2 l2]`).
   - RFC 3161 Time-Stamp Authority (TSA) cryptographic tokens.
   - Database persistence in `compliance.DocumentSealRecords`.
   - Tamper verification: detects any byte alteration outside the signature container.
2. **MinIO / S3 Cold Storage Replication (`IMinioDocumentMirrorService`)**:
   - Automated replication to S3/MinIO compatible object storage.
   - Tenant-isolated buckets (`sgrowing-tenant-{tenantId}`).
   - Storage tiering (`GLACIER_INSTANT_RETRIEVAL`) and SHA-256 pre- and post-transfer verification.

## Consequences
- **Positive**: Strict legal non-repudiation and immutable cold storage disaster recovery.
- **Trade-off**: Requires computation of byte-range hashes on all signed PDFs.
