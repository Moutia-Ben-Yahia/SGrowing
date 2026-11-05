using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGrowing.Api.Migrations
{
    /// <inheritdoc />
    public partial class EnterpriseSwarmStreamingAndDocumentVault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "compliance");

            migrationBuilder.CreateTable(
                name: "DocumentSealRecords",
                schema: "compliance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sha256Hash = table.Column<string>(type: "text", nullable: false),
                    SignatureFormat = table.Column<string>(type: "text", nullable: false),
                    CertificateIssuer = table.Column<string>(type: "text", nullable: false),
                    CertificateThumbprintSha256 = table.Column<string>(type: "text", nullable: false),
                    VerificationToken = table.Column<string>(type: "text", nullable: false),
                    SignerEmail = table.Column<string>(type: "text", nullable: false),
                    SignerIpAddress = table.Column<string>(type: "text", nullable: false),
                    SealedPdfUrl = table.Column<string>(type: "text", nullable: false),
                    HasRfc3161Timestamp = table.Column<bool>(type: "boolean", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsTampered = table.Column<bool>(type: "boolean", nullable: false),
                    AuditChecksum = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentSealRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSealRecords_TenantId_DocumentId",
                schema: "compliance",
                table: "DocumentSealRecords",
                columns: new[] { "TenantId", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSealRecords_VerificationToken",
                schema: "compliance",
                table: "DocumentSealRecords",
                column: "VerificationToken");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentSealRecords",
                schema: "compliance");
        }
    }
}
