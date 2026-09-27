using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HouseManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class EffectiveDatedPricingVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServicePricingVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    PricingMode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    BasePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TimeBillingUnit = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    TimeUnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MinimumBillableDurationMinutes = table.Column<int>(type: "int", nullable: true),
                    BillingIncrementMinutes = table.Column<int>(type: "int", nullable: true),
                    TimeRoundingPolicy = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    OvertimeThresholdMinutes = table.Column<int>(type: "int", nullable: true),
                    OvertimeUnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServicePricingVersions", x => x.Id);
                    table.CheckConstraint("CK_ServicePricingVersions_BasePrice", "[BasePrice] IS NULL OR [BasePrice] > 0");
                    table.CheckConstraint("CK_ServicePricingVersions_BillingIncrement", "[BillingIncrementMinutes] IS NULL OR [BillingIncrementMinutes] > 0");
                    table.CheckConstraint("CK_ServicePricingVersions_EffectiveWindow", "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]");
                    table.CheckConstraint("CK_ServicePricingVersions_MinimumDuration", "[MinimumBillableDurationMinutes] IS NULL OR [MinimumBillableDurationMinutes] > 0");
                    table.CheckConstraint("CK_ServicePricingVersions_Overtime", "([OvertimeThresholdMinutes] IS NULL AND [OvertimeUnitPrice] IS NULL) OR ([OvertimeThresholdMinutes] IS NOT NULL AND [OvertimeUnitPrice] IS NOT NULL AND [OvertimeThresholdMinutes] >= [MinimumBillableDurationMinutes] AND [OvertimeUnitPrice] > 0)");
                    table.CheckConstraint("CK_ServicePricingVersions_PricingMode", "[PricingMode] IN ('Fixed', 'PerUnit', 'TimeBased')");
                    table.CheckConstraint("CK_ServicePricingVersions_Status", "[Status] IN ('Draft', 'Published')");
                    table.CheckConstraint("CK_ServicePricingVersions_TimeUnitPrice", "[TimeUnitPrice] IS NULL OR [TimeUnitPrice] > 0");
                    table.ForeignKey(
                        name: "FK_ServicePricingVersions_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ServicePricingVersionUnits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServicePricingVersionId = table.Column<int>(type: "int", nullable: false),
                    UnitName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServicePricingVersionUnits", x => x.Id);
                    table.CheckConstraint("CK_ServicePricingVersionUnits_UnitPrice", "[UnitPrice] > 0");
                    table.ForeignKey(
                        name: "FK_ServicePricingVersionUnits_ServicePricingVersions_ServicePricingVersionId",
                        column: x => x.ServicePricingVersionId,
                        principalTable: "ServicePricingVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServicePricingVersions_ServiceId_EffectiveFrom",
                table: "ServicePricingVersions",
                columns: new[] { "ServiceId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_ServicePricingVersions_ServiceId_OpenPublished",
                table: "ServicePricingVersions",
                column: "ServiceId",
                unique: true,
                filter: "[Status] = 'Published' AND [EffectiveTo] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ServicePricingVersionUnits_ServicePricingVersionId_UnitName",
                table: "ServicePricingVersionUnits",
                columns: new[] { "ServicePricingVersionId", "UnitName" },
                unique: true);

            // Migrate existing (unversioned) pricing definitions into one open-ended, published
            // pricing version per service, effective from the earliest representable instant so
            // no existing booking snapshot is reinterpreted differently.
            migrationBuilder.Sql(@"
INSERT INTO [ServicePricingVersions] ([ServiceId], [Status], [PricingMode], [EffectiveFrom], [EffectiveTo], [BasePrice], [CreatedAt], [PublishedAt])
SELECT [Id], 'Published', [PricingMode], '0001-01-01T00:00:00.0000000+00:00', NULL,
       CASE WHEN [PricingMode] = 'Fixed' THEN [BasePrice] ELSE NULL END,
       SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET()
FROM [Services];

UPDATE v
SET v.[TimeBillingUnit] = p.[BillingUnit],
    v.[TimeUnitPrice] = p.[UnitPrice],
    v.[MinimumBillableDurationMinutes] = p.[MinimumBillableDurationMinutes],
    v.[BillingIncrementMinutes] = p.[BillingIncrementMinutes],
    v.[TimeRoundingPolicy] = p.[RoundingPolicy],
    v.[OvertimeThresholdMinutes] = p.[OvertimeThresholdMinutes],
    v.[OvertimeUnitPrice] = p.[OvertimeUnitPrice]
FROM [ServicePricingVersions] v
INNER JOIN [ServiceTimePricingPolicies] p ON p.[ServiceId] = v.[ServiceId]
WHERE v.[PricingMode] = 'TimeBased';

INSERT INTO [ServicePricingVersionUnits] ([ServicePricingVersionId], [UnitName], [UnitPrice])
SELECT v.[Id], r.[UnitName], r.[UnitPrice]
FROM [ServicePriceRules] r
INNER JOIN [ServicePricingVersions] v ON v.[ServiceId] = r.[ServiceId] AND v.[PricingMode] = 'PerUnit';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServicePricingVersionUnits");

            migrationBuilder.DropTable(
                name: "ServicePricingVersions");
        }
    }
}
