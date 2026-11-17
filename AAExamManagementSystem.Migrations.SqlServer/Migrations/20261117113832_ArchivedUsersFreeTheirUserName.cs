using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAExamManagementSystem.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class ArchivedUsersFreeTheirUserName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UserNameIndex",
                schema: "AAExam",
                table: "AspNetUsers");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "AAExam",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL AND [IsDeleted] = 0");

            // Archiving an applicant now archives their account too. Do the same for applicants archived earlier, so
            // their usernames and emails are free like everyone else's.
            migrationBuilder.Sql(
                "UPDATE [AAExam].[AspNetUsers] SET [IsDeleted] = 1 " +
                "WHERE [Id] IN (SELECT [UserId] FROM [AAExam].[Applicants] WHERE [IsActive] = 0);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UserNameIndex",
                schema: "AAExam",
                table: "AspNetUsers");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "AAExam",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");
        }
    }
}
