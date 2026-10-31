using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGrowing.Api.Migrations
{
    /// <inheritdoc />
    public partial class SpatialWarehouseIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_WarehouseRecords_TenantId_IsActive_Latitude_Longitude",
                table: "WarehouseRecords",
                columns: new[] { "TenantId", "IsActive", "Latitude", "Longitude" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WarehouseRecords_TenantId_IsActive_Latitude_Longitude",
                table: "WarehouseRecords");
        }
    }
}
