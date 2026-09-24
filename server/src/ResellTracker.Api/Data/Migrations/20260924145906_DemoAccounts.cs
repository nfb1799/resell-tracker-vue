using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResellTracker.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DemoAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DemoExpiresAt",
                table: "AspNetUsers",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDemo",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_DemoExpiresAt",
                table: "AspNetUsers",
                column: "DemoExpiresAt",
                filter: "[IsDemo] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_DemoExpiresAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DemoExpiresAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsDemo",
                table: "AspNetUsers");
        }
    }
}
