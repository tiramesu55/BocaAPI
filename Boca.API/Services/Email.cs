using BocaAPI.Interfaces;
using BocaAPI.Models;
using Microsoft.Extensions.Options;
using System.Net.Mail;

namespace BocaAPI.Services
{
    public class Email:ServiceBase, IEmail
    {
        
        private readonly EmailConfig _settings;
        public Email(ILogger<BocaService> logger, IOptions<EmailConfig> options):base(logger)
        {
            _settings = options.Value;

        }
        public async Task  Send(   string content, string subject)
        {
            string host = _settings.SmtpServer;
            //do not email if smtp not set
            if (string.IsNullOrEmpty(host))
                return;
            string fromAddr = _settings.From;
            int port = _settings.Port;
            string To = _settings.To;
            
            MailMessage mailMessage = new MailMessage();
            //see if to contains commas and parse accordingly
            To.Split(',').ToList().ForEach(x => mailMessage.To.Add(x.Trim()));

            mailMessage.From =new MailAddress( fromAddr);
            mailMessage.Subject = subject;
            mailMessage.Body = content;
            mailMessage.BodyEncoding = System.Text.Encoding.ASCII;
            mailMessage.SubjectEncoding = System.Text.Encoding.ASCII;
            using SmtpClient client = new SmtpClient( host, port );
            try
            {
                client.EnableSsl = false;
                await client.SendMailAsync(mailMessage);

            }
            catch (Exception ex)
            {
                if(ex.InnerException != null)
                {
                    _logger.LogCritical(ex.InnerException.Message, ex.InnerException);
                }
                    else
                        {
                            _logger.LogCritical(ex.Message, ex);
                        }
               
            }
           

        }
    }
}
