using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAExamManagementSystem.Repository.Migrations
{
    /// <inheritdoc />
    public partial class SectionsHaveManyDepartments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DepartmentSection",
                schema: "AAExam",
                columns: table => new
                {
                    DepartmentsId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SectionsId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentSection", x => new { x.DepartmentsId, x.SectionsId });
                    table.ForeignKey(
                        name: "FK_DepartmentSection_Departments_DepartmentsId",
                        column: x => x.DepartmentsId,
                        principalSchema: "AAExam",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DepartmentSection_Sections_SectionsId",
                        column: x => x.SectionsId,
                        principalSchema: "AAExam",
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentSection_SectionsId",
                schema: "AAExam",
                table: "DepartmentSection",
                column: "SectionsId");

            // Keep every existing section-to-department assignment before the old column goes.
            migrationBuilder.Sql(
                @"INSERT INTO [AAExam].[DepartmentSection] ([DepartmentsId], [SectionsId])
                  SELECT [DepartmentId], [Id] FROM [AAExam].[Sections] WHERE [DepartmentId] IS NOT NULL");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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

            // A section can only keep one department again, so keep the first of its departments.
            migrationBuilder.Sql(
                @"UPDATE s SET s.[DepartmentId] =
                    (SELECT TOP 1 ds.[DepartmentsId] FROM [AAExam].[DepartmentSection] ds WHERE ds.[SectionsId] = s.[Id] ORDER BY ds.[DepartmentsId])
                  FROM [AAExam].[Sections] s");

            migrationBuilder.DropTable(
                name: "DepartmentSection",
                schema: "AAExam");
        }
    }
}
