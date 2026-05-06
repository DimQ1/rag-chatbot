using rag_chatbot_api.Models;

namespace rag_chatbot_api.Services;

public interface ITokenService
{
    string CreateToken(AppUser user);
}
