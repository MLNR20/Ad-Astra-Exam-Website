using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAExamManagementSystem.Migrations.Postgres
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
                filter: "\"IsDeleted\" = false");

            // Archiving an applicant now archives their account too. Do the same for applicants archived earlier, so
            // their usernames and emails are free like everyone else's.
            migrationBuilder.Sql(
                "UPDATE \"AAExam\".\"AspNetUsers\" SET \"IsDeleted\" = TRUE " +
                "WHERE \"Id\" IN (SELECT \"UserId\" FROM \"AAExam\".\"Applicants\" WHERE \"IsActive\" = FALSE);");
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
                unique: true);
        }
    }
}
