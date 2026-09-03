using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HouseManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class HouseHelpProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Bio",
                table: "HouseHelps",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmergencyContactName",
                table: "HouseHelps",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmergencyContactPhone",
                table: "HouseHelps",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Languages",
                table: "HouseHelps",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NationalIdLast4",
                table: "HouseHelps",
                type: "nvarchar(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfileImageContentType",
                table: "HouseHelps",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ProfileImageSizeBytes",
                table: "HouseHelps",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfileImageStorageKey",
                table: "HouseHelps",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProfileImageUpdatedAt",
                table: "HouseHelps",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationStatus",
                table: "HouseHelps",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Unverified");

            migrationBuilder.AddColumn<int>(
                name: "YearsOfExperience",
                table: "HouseHelps",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HouseHelps_VerificationStatus",
                table: "HouseHelps",
                column: "VerificationStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HouseHelps_VerificationStatus",
                table: "HouseHelps");

            migrationBuilder.DropColumn(
                name: "Bio",
                table: "HouseHelps");

            migrationBuilder.DropColumn(
                name: "EmergencyContactName",
                table: "HouseHelps");

            migrationBuilder.DropColumn(
                name: "EmergencyContactPhone",
                table: "HouseHelps");

            migrationBuilder.DropColumn(
                name: "Languages",
                table: "HouseHelps");

            migrationBuilder.DropColumn(
                name: "NationalIdLast4",
                table: "HouseHelps");

            migrationBuilder.DropColumn(
                name: "ProfileImageContentType",
                table: "HouseHelps");

            migrationBuilder.DropColumn(
                name: "ProfileImageSizeBytes",
                table: "HouseHelps");

            migrationBuilder.DropColumn(
                name: "ProfileImageStorageKey",
                table: "HouseHelps");

            migrationBuilder.DropColumn(
                name: "ProfileImageUpdatedAt",
                table: "HouseHelps");

            migrationBuilder.DropColumn(
                name: "VerificationStatus",
                table: "HouseHelps");

            migrationBuilder.DropColumn(
                name: "YearsOfExperience",
                table: "HouseHelps");
        }
    }
}
