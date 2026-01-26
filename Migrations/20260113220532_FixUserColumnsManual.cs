using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PROJFACILITY.IA.Migrations
{
    /// <inheritdoc />
    public partial class FixUserColumnsManual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Messages",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_UserId_AgentName",
                table: "Messages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Documents",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "AgentName",
                table: "Messages");

            migrationBuilder.RenameTable(
                name: "Messages",
                newName: "ChatMessages");

            migrationBuilder.RenameTable(
                name: "Documents",
                newName: "KnowledgeDocuments");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastResetDate",
                table: "Users",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<long>(
                name: "UsedTokensCurrentMonth",
                table: "Users",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "AgentId",
                table: "ChatMessages",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChatMessages",
                table: "ChatMessages",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_KnowledgeDocuments",
                table: "KnowledgeDocuments",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_KnowledgeDocuments",
                table: "KnowledgeDocuments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ChatMessages",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "LastResetDate",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UsedTokensCurrentMonth",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AgentId",
                table: "ChatMessages");

            migrationBuilder.RenameTable(
                name: "KnowledgeDocuments",
                newName: "Documents");

            migrationBuilder.RenameTable(
                name: "ChatMessages",
                newName: "Messages");

            migrationBuilder.AddColumn<string>(
                name: "AgentName",
                table: "Messages",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Documents",
                table: "Documents",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Messages",
                table: "Messages",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_UserId_AgentName",
                table: "Messages",
                columns: new[] { "UserId", "AgentName" });
        }
    }
}
