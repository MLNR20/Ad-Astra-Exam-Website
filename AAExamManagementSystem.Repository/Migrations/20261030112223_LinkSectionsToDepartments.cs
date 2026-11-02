using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAExamManagementSystem.Repository.Migrations
{
    /// <inheritdoc />
    public partial class LinkSectionsToDepartments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DepartmentId",
                schema: "AAExam",
                table: "Sections",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sections_DepartmentId",
                schema: "AAExam",
                table: "Sections",
                column: "DepartmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Sections_Departments_DepartmentId",
                schema: "AAExam",
                table: "Sections",
                column: "DepartmentId",
                principalSchema: "AAExam",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sections_Departments_DepartmentId",
                schema: "AAExam",
                table: "Sections");

            migrationBuilder.DropIndex(
                name: "IX_Sections_DepartmentId",
                schema: "AAExam",
                table: "Sections");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                schema: "AAExam",
                table: "Sections");
        }
    }
}
