using System.Net;
using System.Net.Mail;

namespace KucaMemoServer.Services;

/// <summary>경희대 메일 인증번호를 보낸다.</summary>
public interface IEmailSender
{
    bool IsConfigured { get; }
    Task<bool> SendAsync(string to, string subject, string body, CancellationToken ct);
}

/// <summary>
/// SMTP 로 보낸다. 기본값은 Gmail (smtp.gmail.com:587, STARTTLS) — 팀용 Gmail 계정의 "앱 비밀번호"를 쓴다.
///   Email:User      보내는 Gmail 주소
///   Email:Password  앱 비밀번호 16자리 (저장소에 넣지 말 것. Azure 앱 설정 Email__Password)
///   Email:Host / Email:Port / Email:From  (선택)
/// </summary>
public class SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> log) : IEmailSender
{
    string User => config["Email:User"] ?? "";
    // Google 은 앱 비밀번호를 "abcd efgh ijkl mnop" 처럼 띄어서 보여 준다. 띄어 쓴 채로 넣어도 되게 공백을 뺀다.
    string Password => (config["Email:Password"] ?? "").Replace(" ", "");
    public bool IsConfigured => User.Length > 0 && Password.Length > 0;

    public async Task<bool> SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        if (!IsConfigured)
            return false;
        try
        {
            using var client = new SmtpClient(config["Email:Host"] is { Length: > 0 } h ? h : "smtp.gmail.com",
                int.TryParse(config["Email:Port"], out int port) ? port : 587)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(User, Password),
            };
            string from = config["Email:From"] is { Length: > 0 } f ? f : User;
            using var message = new MailMessage(new MailAddress(from, "KUCA"), new MailAddress(to)) { Subject = subject, Body = body };
            await client.SendMailAsync(message, ct);
            return true;
        }
        catch (Exception e) when (e is SmtpException or InvalidOperationException or FormatException)
        {
            log.LogWarning("인증 메일 보내기 실패: {Message}", e.Message);
            return false;
        }
    }
}
