using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GLORIOUSSYSTEM.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRefreshTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RefreshTokenExpiresAt",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefreshTokenHash",
                table: "Users",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_RefreshTokenHash",
                table: "Users",
                column: "RefreshTokenHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_RefreshTokenHash",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RefreshTokenExpiresAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RefreshTokenHash",
                table: "Users");
        }
    }
}
