using System.Net;
using System.Security;

namespace SevenThree.Services
{
    public class SecurityServices
    {
        public SecurityServices()
        {
        
        }

        public NetworkCredential ConvertToSecure(string username, string password)
        {
            return new NetworkCredential(username, password);
        }

        public string ConvertFromSecure(NetworkCredential cred)
        {
            return new NetworkCredential(cred.UserName, cred.Password).Password;
        }
    }
}