using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PROJFACILITY.IA.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentIdToChat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreatorId",
                table: "Agents",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatorId",
                table: "Agents");
        }
    }
}
