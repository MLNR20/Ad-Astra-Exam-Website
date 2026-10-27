using AAExamManagementSystem.Services;

namespace AAExamManagementSystem.Tests;

public class RegistrationEmailTests
{
    [Fact]
    public void BuildBody_ListsTheApplicantAndEveryExamChoice()
    {
        var body = SmtpRegistrationNotifier.BuildBody("Ana", "Reyes", "2026-0042", "2026-2027", new[] { "Web Development", "Managing" });

        Assert.Contains("Applicant: Ana Reyes", body);
        Assert.Contains("Student No.: 2026-0042", body);
        Assert.Contains("Exam choices:", body);
        Assert.Contains("1. Web Development", body);
        Assert.Contains("2. Managing", body);
    }

    [Fact]
    public void BuildBody_WithOneExam_UsesTheSingularHeading()
    {
        var body = SmtpRegistrationNotifier.BuildBody("Ana", "Reyes", "1", "2026-2027", new[] { "Managing" });

        Assert.Contains("Exam choice:", body);
        Assert.DoesNotContain("Exam choices:", body);
    }

    [Fact]
    public void BuildBody_WelcomesTheApplicantAndExplainsTheExam()
    {
        var body = SmtpRegistrationNotifier.BuildBody("Ana", "Reyes", "1", "2026-2027", new[] { "Managing" });

        Assert.StartsWith("Welcome to Ad Astra!", body);
        Assert.Contains("Glad to have you, Ana Reyes!", body);
        Assert.Contains("entrance exam", body);
        Assert.Contains("90 minutes", body);
        Assert.Contains("change your answers", body);
        Assert.Contains("Don't share your exam password", body);
    }

    [Fact]
    public void BuildSubject_NamesTheApplicantOnASingleLine()
    {
        var subject = SmtpRegistrationNotifier.BuildSubject("Ana\r\nBcc: someone@example.com");

        Assert.DoesNotContain('\n', subject);
        Assert.DoesNotContain('\r', subject);
        Assert.StartsWith("New applicant registration: Ana", subject);
    }

    [Fact]
    public void EnvFileParse_ReadsKeysQuotesAndComments()
    {
        var values = EnvFileConfiguration.Parse(new[]
        {
            "# a comment",
            "",
            "ad-astra-exam-password=\"abcd efgh ijkl mnop\"",
            "export PLAIN=value",
            "SINGLE='quoted'",
            "WITH_EQUALS=a=b",
            "not a pair",
        });

        Assert.Equal("abcd efgh ijkl mnop", values["ad-astra-exam-password"]);
        Assert.Equal("value", values["PLAIN"]);
        Assert.Equal("quoted", values["SINGLE"]);
        Assert.Equal("a=b", values["WITH_EQUALS"]);
        Assert.Equal(4, values.Count);
    }
}
