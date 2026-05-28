using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kotirauha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MembershipProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InviteId",
                table: "Memberships",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JoinedVia",
                table: "Memberships",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_InviteId",
                table: "Memberships",
                column: "InviteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Memberships_InviteId",
                table: "Memberships");

            migrationBuilder.DropColumn(
                name: "InviteId",
                table: "Memberships");

            migrationBuilder.DropColumn(
                name: "JoinedVia",
                table: "Memberships");
        }
    }
}
