using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kotirauha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BuildingResourceLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BuildingId",
                table: "ResourceLinks",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceLinks_BuildingId",
                table: "ResourceLinks",
                column: "BuildingId");

            migrationBuilder.AddForeignKey(
                name: "FK_ResourceLinks_Buildings_BuildingId",
                table: "ResourceLinks",
                column: "BuildingId",
                principalTable: "Buildings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ResourceLinks_Buildings_BuildingId",
                table: "ResourceLinks");

            migrationBuilder.DropIndex(
                name: "IX_ResourceLinks_BuildingId",
                table: "ResourceLinks");

            migrationBuilder.DropColumn(
                name: "BuildingId",
                table: "ResourceLinks");
        }
    }
}
