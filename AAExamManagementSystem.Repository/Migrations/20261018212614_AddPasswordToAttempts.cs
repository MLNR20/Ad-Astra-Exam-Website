using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAExamManagementSystem.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordToAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Password",
                schema: "AAExam",
                table: "Attempts",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Attempts_Password",
                schema: "AAExam",
                table: "Attempts",
                column: "Password",
                unique: true,
                filter: "[Password] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Attempts_Password",
                schema: "AAExam",
                table: "Attempts");

            migrationBuilder.DropColumn(
                name: "Password",
                schema: "AAExam",
                table: "Attempts");
        }
    }
}
