using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAExamManagementSystem.Repository.Migrations
{
    /// <inheritdoc />
    public partial class MoveChoicesAndScheduleToAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChoiceNumber",
                schema: "AAExam",
                table: "Attempts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ExamDate",
                schema: "AAExam",
                table: "Attempts",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ExamTime",
                schema: "AAExam",
                table: "Attempts",
                type: "time",
                nullable: true);

            // Keep each applicant's picks: tag the attempt for that department as their first or second choice.
            migrationBuilder.Sql(
                @"UPDATE a SET a.ChoiceNumber = 1
                  FROM [AAExam].[Attempts] a
                  JOIN [AAExam].[Applicants] ap ON ap.Id = a.ApplicantId
                  WHERE LOWER(LTRIM(RTRIM(a.Section))) = LOWER(LTRIM(RTRIM(ap.FirstChoice)));");

            migrationBuilder.Sql(
                @"UPDATE a SET a.ChoiceNumber = 2
                  FROM [AAExam].[Attempts] a
                  JOIN [AAExam].[Applicants] ap ON ap.Id = a.ApplicantId
                  WHERE a.ChoiceNumber IS NULL
                    AND LOWER(LTRIM(RTRIM(a.Section))) = LOWER(LTRIM(RTRIM(ap.SecondChoice)));");

            migrationBuilder.DropColumn(
                name: "FirstChoice",
                schema: "AAExam",
                table: "Applicants");

            migrationBuilder.DropColumn(
                name: "SecondChoice",
                schema: "AAExam",
                table: "Applicants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FirstChoice",
                schema: "AAExam",
                table: "Applicants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondChoice",
                schema: "AAExam",
                table: "Applicants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.Sql(
                @"UPDATE ap SET ap.FirstChoice = a.Section
                  FROM [AAExam].[Applicants] ap
                  JOIN [AAExam].[Attempts] a ON a.ApplicantId = ap.Id AND a.ChoiceNumber = 1;");

            migrationBuilder.Sql(
                @"UPDATE ap SET ap.SecondChoice = a.Section
                  FROM [AAExam].[Applicants] ap
                  JOIN [AAExam].[Attempts] a ON a.ApplicantId = ap.Id AND a.ChoiceNumber = 2;");

            migrationBuilder.DropColumn(
                name: "ChoiceNumber",
                schema: "AAExam",
                table: "Attempts");

            migrationBuilder.DropColumn(
                name: "ExamDate",
                schema: "AAExam",
                table: "Attempts");

            migrationBuilder.DropColumn(
                name: "ExamTime",
                schema: "AAExam",
                table: "Attempts");
        }
    }
}
