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

    /// <summary>Who gets the registration email; falls back to <see cref="From"/>.</summary>
    public string? Recipient { get; set; }

    /// <summary>The Gmail app password. Read from the "ad-astra-exam-password" setting (see .env).</summary>
    public string? Password { get; set; }
}

public interface IRegistrationNotifier
{
    /// <summary>Emails the recruitment inbox about a new registration. Never throws; failures are logged.</summary>
    void NotifyRegistration(string applicantName, string studentNo, string academicYear, IReadOnlyList<string> exams);
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

    public void NotifyRegistration(string applicantName, string studentNo, string academicYear, IReadOnlyList<string> exams)
    {
        if (string.IsNullOrWhiteSpace(_settings.Password))
        {
            _logger.LogWarning("Registration email skipped: no 'ad-astra-exam-password' is configured.");
            return;
        }

        var subject = BuildSubject(applicantName);
        var body = BuildBody(applicantName, studentNo, academicYear, exams);

        // Sent in the background so a slow or failing mail server never holds up the signup page.
        _ = Task.Run(async () =>
        {
            try
            {
                await SendAsync(subject, body);
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
        return $"New applicant registration: {singleLineName}";
    }

    public static string BuildBody(string applicantName, string studentNo, string academicYear, IReadOnlyList<string> exams)
    {
        var body = new StringBuilder();
        body.AppendLine("A new applicant has registered.");
        body.AppendLine();
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

    private async Task SendAsync(string subject, string body)
    {
        var recipient = string.IsNullOrWhiteSpace(_settings.Recipient) ? _settings.From : _settings.Recipient;

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
