using BocaAPI.Models.DTO;

namespace BocaAPI.Interfaces
{
    public interface IEmail
    {
        Task Send(  string content, string subject);

       
    }
}
