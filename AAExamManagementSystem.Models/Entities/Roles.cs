namespace AAExamManagementSystem.Models.Entities;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Instructor = "Instructor";
    public const string Student = "Student";
    public const string Applicant = "Applicant";
    public const string Staffer = "Staffer";
    public const string Editor = "Editor";
    public const string OIC = "OIC";
    public const string Guest = "Guest";

    public static readonly string[] All =
    {
        Admin, Instructor, Student, Applicant, Staffer, Editor, OIC, Guest
    };
}
