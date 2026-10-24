using AAExamManagementSystem.Models.Entities;

namespace AAExamManagementSystem.Services;

public static class ExamHeroImage
{
    private const string Default = "~/images/general-knowledge-banner.webp";

    /// <summary>
    /// The hero banner for a department's exam; departments without their own image use the default banner.
    /// </summary>
    public static string For(string? sectionName) => sectionName switch
    {
        Departments.Marketing => "~/images/marketing-hero.jpg",
        Departments.WebDevelopment => "~/images/developer-workstation.png",
        Departments.Managing => "~/images/managing-hero.webp",
        Departments.CustomerSupport => "~/images/customer-support-hero.webp",
        _ => Default
    };
}
