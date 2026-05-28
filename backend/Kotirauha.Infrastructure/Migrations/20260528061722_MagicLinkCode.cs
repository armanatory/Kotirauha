using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kotirauha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MagicLinkCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Attempts",
                table: "MagicLinkTokens",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CodeHash",
                table: "MagicLinkTokens",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_MagicLinkTokens_Email",
                table: "MagicLinkTokens",
                column: "Email");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MagicLinkTokens_Email",
                table: "MagicLinkTokens");

            migrationBuilder.DropColumn(
                name: "Attempts",
                table: "MagicLinkTokens");

            migrationBuilder.DropColumn(
                name: "CodeHash",
                table: "MagicLinkTokens");
        }
    }
}
