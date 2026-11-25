using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OutdoorRental.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConcurrencyVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "Version",
                table: "RentalLocations",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "Version",
                table: "EquipmentUnits",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "Version",
                table: "Equipment",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "RentalLocations");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "EquipmentUnits");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Equipment");
        }
    }
}
