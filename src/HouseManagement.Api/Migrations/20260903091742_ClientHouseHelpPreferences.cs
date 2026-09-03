using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HouseManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class ClientHouseHelpPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientHouseHelpPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClientId = table.Column<int>(type: "int", nullable: false),
                    HouseHelpId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientHouseHelpPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientHouseHelpPreferences_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ClientHouseHelpPreferences_HouseHelps_HouseHelpId",
                        column: x => x.HouseHelpId,
                        principalTable: "HouseHelps",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientHouseHelpPreferences_ClientId_HouseHelpId",
                table: "ClientHouseHelpPreferences",
                columns: new[] { "ClientId", "HouseHelpId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientHouseHelpPreferences_HouseHelpId",
                table: "ClientHouseHelpPreferences",
                column: "HouseHelpId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientHouseHelpPreferences");
        }
    }
}
