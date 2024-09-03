using DotNetEnv;
using System.Net.Mail;
using System.Net;

namespace ACAPI.Helper
{
    public static class Mail
    {
        private static readonly string FROM_EMAIL = Env.GetString("MAIL_FROM");
        private static readonly string FROM_PASSWORD = Env.GetString("MAIL_PASSSWORD");

        public static bool Send(string toEmail, string subject, string body)
        {

            //string toEmail = "haikeint@gmail.com";
            //string subject = "Test Email from C#";
            //string body = "This is a test email sent from a C# application.";

            SmtpClient smtpClient = new("smtp.gmail.com", 587)
            {
                Credentials = new NetworkCredential(FROM_EMAIL, FROM_PASSWORD),
                EnableSsl = true
            };

            MailMessage mailMessage = new()
            {
                From = new MailAddress(FROM_EMAIL, "HBPlay Support"),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            mailMessage.To.Add(toEmail);

            try
            {
                smtpClient.Send(mailMessage);
                Console.WriteLine("Email sent successfully!");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to send email. Error: " + ex.Message);
                return false;
            }
        }
    }
}
