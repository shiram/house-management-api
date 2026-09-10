using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HouseManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class ServiceTimePricingPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceTimePricingPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    BillingUnit = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MinimumBillableDurationMinutes = table.Column<int>(type: "int", nullable: false),
                    BillingIncrementMinutes = table.Column<int>(type: "int", nullable: false),
                    RoundingPolicy = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    OvertimeThresholdMinutes = table.Column<int>(type: "int", nullable: true),
                    OvertimeUnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceTimePricingPolicies", x => x.Id);
                    table.CheckConstraint("CK_ServiceTimePricingPolicies_BillingIncrement", "[BillingIncrementMinutes] > 0");
                    table.CheckConstraint("CK_ServiceTimePricingPolicies_BillingUnit", "[BillingUnit] IN ('Minute', 'Hour', 'Day')");
                    table.CheckConstraint("CK_ServiceTimePricingPolicies_MinimumDuration", "[MinimumBillableDurationMinutes] > 0");
                    table.CheckConstraint("CK_ServiceTimePricingPolicies_Overtime", "([OvertimeThresholdMinutes] IS NULL AND [OvertimeUnitPrice] IS NULL) OR ([OvertimeThresholdMinutes] IS NOT NULL AND [OvertimeUnitPrice] IS NOT NULL AND [OvertimeThresholdMinutes] >= [MinimumBillableDurationMinutes] AND [OvertimeUnitPrice] > 0)");
                    table.CheckConstraint("CK_ServiceTimePricingPolicies_RoundingPolicy", "[RoundingPolicy] IN ('None', 'Up', 'Down', 'Nearest')");
                    table.CheckConstraint("CK_ServiceTimePricingPolicies_UnitPrice", "[UnitPrice] > 0");
                    table.ForeignKey(
                        name: "FK_ServiceTimePricingPolicies_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTimePricingPolicies_ServiceId",
                table: "ServiceTimePricingPolicies",
                column: "ServiceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceTimePricingPolicies");
        }
    }
}
