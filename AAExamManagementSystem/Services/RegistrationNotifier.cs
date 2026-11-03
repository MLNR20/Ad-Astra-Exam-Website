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

    /// <summary>The SMTP login. Gmail uses the sender address; relays such as Brevo have their own login.</summary>
    public string? Username { get; set; }

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
        var htmlBody = BuildHtmlBody(firstName, lastName, studentNo, academicYear, exams);

        // Sent in the background so a slow or failing mail server never holds up the signup page.
        _ = Task.Run(async () =>
        {
            try
            {
                await SendAsync(email, subject, body, htmlBody);
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

    private const string HeaderGreen = "#1f5a35";

    /// <summary>The styled HTML version of <see cref="BuildBody"/>: a green header, a greeting, and the details as label and value rows.</summary>
    public static string BuildHtmlBody(string firstName, string lastName, string studentNo, string academicYear, IReadOnlyList<string> exams)
    {
        static string Encode(string value) => WebUtility.HtmlEncode(value);

        var applicantName = Encode($"{firstName} {lastName}".Trim());

        static string Row(string label, string value) =>
            "<tr>" +
            $"<td style=\"padding:8px 0;width:150px;font-weight:bold;color:#666666;vertical-align:top;\">{label}</td>" +
            $"<td style=\"padding:8px 0;color:#333333;vertical-align:top;\">{value}</td>" +
            "</tr>";

        var details = new StringBuilder();
        details.Append(Row("Applicant", applicantName));
        details.Append(Row("Student No.", Encode(studentNo)));
        details.Append(Row("Academic Year", Encode(academicYear)));

        if (exams.Count == 0)
        {
            details.Append(Row("Exam Choice", "None selected"));
        }
        else
        {
            for (var i = 0; i < exams.Count; i++)
            {
                details.Append(Row(exams.Count == 1 ? "Exam Choice" : $"Choice {i + 1}", Encode(exams[i])));
            }
        }

        const string paragraph = "margin:0 0 16px;font-size:16px;line-height:1.6;color:#333333;";

        return $"""
            <!DOCTYPE html>
            <html>
            <body style="margin:0;padding:0;background-color:#f4f5f9;font-family:'Segoe UI',Helvetica,Arial,sans-serif;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:#f4f5f9;padding:24px 0;">
            <tr><td align="center">
              <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="width:100%;max-width:600px;">
                <tr><td style="background-color:{HeaderGreen};padding:32px 40px;border-radius:8px 8px 0 0;">
                  <div style="font-size:24px;font-weight:bold;color:#ffffff;">Welcome to Ad Astra</div>
                  <div style="margin-top:8px;font-size:14px;color:#d6e6dc;">Entrance Exam Registration</div>
                </td></tr>
                <tr><td style="background-color:#ffffff;padding:32px 40px;">
                  <p style="{paragraph}">Hi <strong>{applicantName}</strong>,</p>
                  <p style="{paragraph}">Glad to have you! Before we begin your journey, we invite you to take our entrance exam to gauge your interests and your specialties.</p>
                  <p style="margin:0 0 8px;font-size:16px;font-weight:bold;color:#333333;">Exam instructions</p>
                  <ul style="margin:0 0 24px;padding-left:20px;font-size:15px;line-height:1.7;color:#333333;">
                    <li>Each exam may take up to 90 minutes.</li>
                    <li>You can go back to a previous page and change your answers.</li>
                    <li>Once you submit the final page, your answers are final and cannot be changed.</li>
                    <li>Each exam can only be taken once.</li>
                    <li>Don't share your exam password with anyone.</li>
                  </ul>
                  <p style="margin:0 0 8px;font-size:16px;font-weight:bold;color:#333333;">Your registration details</p>
                  <table role="presentation" cellpadding="0" cellspacing="0" style="width:100%;font-size:16px;">{details}</table>
                </td></tr>
                <tr><td style="padding:20px 40px;text-align:center;font-size:12px;color:#888888;">
                  This is an automated notification. Please do not reply to this email.
                </td></tr>
              </table>
            </td></tr>
            </table>
            </body>
            </html>
            """;
    }

    private async Task SendAsync(string recipient, string subject, string body, string htmlBody)
    {
        // Google shows app passwords in groups of four separated by spaces; the spaces are not part of it.
        var password = new string(_settings.Password!.Where(c => !char.IsWhiteSpace(c)).ToArray());

        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(string.IsNullOrWhiteSpace(_settings.Username) ? _settings.From : _settings.Username.Trim(), password),
            Timeout = 15_000,
        };

        using var message = new MailMessage(new MailAddress(_settings.From, "Ad Astra Recruitment"), new MailAddress(recipient))
        {
            Subject = subject,
            Body = body,
            IsBodyHtml = false,
        };
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(htmlBody, null, "text/html"));

        await client.SendMailAsync(message);
    }
}
