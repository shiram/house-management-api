using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HouseManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceFeesSurchargesAndPublicHolidays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PublicHolidays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicHolidays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServiceFees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AdjustmentType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceFees", x => x.Id);
                    table.CheckConstraint("CK_ServiceFees_AdjustmentType", "[AdjustmentType] IN ('Percentage', 'FixedAmount')");
                    table.CheckConstraint("CK_ServiceFees_Amount", "([AdjustmentType] = 'FixedAmount' AND [Amount] > 0) OR ([AdjustmentType] = 'Percentage' AND [Amount] > 0 AND [Amount] <= 100)");
                    table.ForeignKey(
                        name: "FK_ServiceFees_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ServiceSurcharges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TriggerType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    AdjustmentType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AfterHoursStartMinutes = table.Column<int>(type: "int", nullable: true),
                    AfterHoursEndMinutes = table.Column<int>(type: "int", nullable: true),
                    UrgentLeadTimeMinutes = table.Column<int>(type: "int", nullable: true),
                    LocationMatch = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceSurcharges", x => x.Id);
                    table.CheckConstraint("CK_ServiceSurcharges_AdjustmentType", "[AdjustmentType] IN ('Percentage', 'FixedAmount')");
                    table.CheckConstraint("CK_ServiceSurcharges_Amount", "([AdjustmentType] = 'FixedAmount' AND [Amount] > 0) OR ([AdjustmentType] = 'Percentage' AND [Amount] > 0 AND [Amount] <= 100)");
                    table.CheckConstraint("CK_ServiceSurcharges_TriggerFields", "([TriggerType] = 'AfterHours' AND [AfterHoursStartMinutes] BETWEEN 0 AND 1439 AND [AfterHoursEndMinutes] BETWEEN 0 AND 1439 AND [UrgentLeadTimeMinutes] IS NULL AND [LocationMatch] IS NULL) OR ([TriggerType] = 'Urgent' AND [UrgentLeadTimeMinutes] > 0 AND [AfterHoursStartMinutes] IS NULL AND [AfterHoursEndMinutes] IS NULL AND [LocationMatch] IS NULL) OR ([TriggerType] = 'Location' AND [LocationMatch] IS NOT NULL AND [AfterHoursStartMinutes] IS NULL AND [AfterHoursEndMinutes] IS NULL AND [UrgentLeadTimeMinutes] IS NULL) OR ([TriggerType] IN ('Weekend', 'Holiday') AND [AfterHoursStartMinutes] IS NULL AND [AfterHoursEndMinutes] IS NULL AND [UrgentLeadTimeMinutes] IS NULL AND [LocationMatch] IS NULL)");
                    table.CheckConstraint("CK_ServiceSurcharges_TriggerType", "[TriggerType] IN ('Weekend', 'Holiday', 'AfterHours', 'Urgent', 'Location')");
                    table.ForeignKey(
                        name: "FK_ServiceSurcharges_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PublicHolidays_Date",
                table: "PublicHolidays",
                column: "Date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceFees_ServiceId_IsActive",
                table: "ServiceFees",
                columns: new[] { "ServiceId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceSurcharges_ServiceId_IsActive",
                table: "ServiceSurcharges",
                columns: new[] { "ServiceId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PublicHolidays");

            migrationBuilder.DropTable(
                name: "ServiceFees");

            migrationBuilder.DropTable(
                name: "ServiceSurcharges");
        }
    }
}
