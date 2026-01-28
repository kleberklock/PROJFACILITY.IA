using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PROJFACILITY.IA.Migrations
{
    /// <inheritdoc />
    public partial class UpdateKnowledgeTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UploadedAt",
                table: "KnowledgeDocuments",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "AgentName",
                table: "KnowledgeDocuments",
                newName: "VectorIdPrefix");

            migrationBuilder.AddColumn<string>(
                name: "FileType",
                table: "KnowledgeDocuments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Tag",
                table: "KnowledgeDocuments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileType",
                table: "KnowledgeDocuments");

            migrationBuilder.DropColumn(
                name: "Tag",
                table: "KnowledgeDocuments");

            migrationBuilder.RenameColumn(
                name: "VectorIdPrefix",
                table: "KnowledgeDocuments",
                newName: "AgentName");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "KnowledgeDocuments",
                newName: "UploadedAt");
        }
    }
}
