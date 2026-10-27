using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;

namespace AAExamManagementSystem.Services;

public class EmailSettings
{
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public string From { get; set; } = "adastra.recruitment.test@gmail.com";

    /// <summary>The Gmail app password. Read from the "ad-astra-exam-password" setting (see .env).</summary>
    public string? Password { get; set; }
}

public interface IRegistrationNotifier
{
    /// <summary>Emails the applicant who just registered, at the address they signed up with. Never throws; failures are logged.</summary>
    void NotifyRegistration(string firstName, string lastName, string email, string studentNo, string academicYear, IReadOnlyList<string> exams);
}

public class SmtpRegistrationNotifier : IRegistrationNotifier
{
    private readonly EmailSettings _settings;
    private readonly ILogger<SmtpRegistrationNotifier> _logger;

    public SmtpRegistrationNotifier(IOptions<EmailSettings> settings, ILogger<SmtpRegistrationNotifier> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public void NotifyRegistration(string firstName, string lastName, string email, string studentNo, string academicYear, IReadOnlyList<string> exams)
    {
        var applicantName = $"{firstName} {lastName}".Trim();

        if (string.IsNullOrWhiteSpace(_settings.Password))
        {
            _logger.LogWarning("Registration email skipped: no 'ad-astra-exam-password' is configured.");
            return;
        }

        var subject = BuildSubject(applicantName);
        var body = BuildBody(firstName, lastName, studentNo, academicYear, exams);

        // Sent in the background so a slow or failing mail server never holds up the signup page.
        _ = Task.Run(async () =>
        {
            try
            {
                await SendAsync(email, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send the registration email for {Applicant}.", applicantName);
            }
        });
    }

    public static string BuildSubject(string applicantName)
    {
        var singleLineName = string.Join(' ', applicantName.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
        return $"Welcome to Ad Astra, {singleLineName}";
    }

    public static string BuildBody(string firstName, string lastName, string studentNo, string academicYear, IReadOnlyList<string> exams)
    {
        var applicantName = $"{firstName} {lastName}".Trim();

        var body = new StringBuilder();
        body.AppendLine("Welcome to Ad Astra!");
        body.AppendLine();
        body.AppendLine($"Glad to have you, {applicantName}! Before we begin your journey, we invite you to take our entrance exam to gauge your interests and your specialties.");
        body.AppendLine();
        body.AppendLine("Exam instructions:");
        body.AppendLine("  - Each exam may take up to 90 minutes.");
        body.AppendLine("  - You can go back to a previous page and change your answers.");
        body.AppendLine("  - Once you submit the final page, your answers are final and cannot be changed.");
        body.AppendLine("  - Each exam can only be taken once.");
        body.AppendLine("  - Don't share your exam password with anyone.");
        body.AppendLine();
        body.AppendLine("Your registration details:");
        body.AppendLine($"Applicant: {applicantName}");
        body.AppendLine($"Student No.: {studentNo}");
        body.AppendLine($"Academic year: {academicYear}");
        body.AppendLine();

        if (exams.Count == 0)
        {
            body.AppendLine("Exam choice: none selected");
        }
        else
        {
            body.AppendLine(exams.Count == 1 ? "Exam choice:" : "Exam choices:");
            for (var i = 0; i < exams.Count; i++)
            {
                body.AppendLine($"  {i + 1}. {exams[i]}");
            }
        }

        return body.ToString();
    }

    private async Task SendAsync(string recipient, string subject, string body)
    {
        // Google shows app passwords in groups of four separated by spaces; the spaces are not part of it.
        var password = new string(_settings.Password!.Where(c => !char.IsWhiteSpace(c)).ToArray());

        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(_settings.From, password),
            Timeout = 15_000,
        };

        using var message = new MailMessage(new MailAddress(_settings.From, "Ad Astra Recruitment"), new MailAddress(recipient))
        {
            Subject = subject,
            Body = body,
            IsBodyHtml = false,
        };

        await client.SendMailAsync(message);
    }
}
