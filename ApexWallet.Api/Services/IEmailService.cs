using System.Threading.Tasks;

namespace ApexWallet.Api.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string body);
    }
}