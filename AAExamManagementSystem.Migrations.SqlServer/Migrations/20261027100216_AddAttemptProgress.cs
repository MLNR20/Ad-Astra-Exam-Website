using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAExamManagementSystem.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddAttemptProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProgressJson",
                schema: "AAExam",
                table: "Attempts",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProgressJson",
                schema: "AAExam",
                table: "Attempts");
        }
    }
}
