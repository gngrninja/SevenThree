
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Net;
using SevenThree.Models;
using SevenThree.Services;
using System.Text;
using System.IO;

namespace SevenThree.Modules
{
    public class QrzApi
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _config;
        private string _baseUrl;
        private string _sessionCheckUrl;
        private string _userName;
        private string _password;
        private XmlServices _xmlService;
        private QrzApiXml.QRZDatabase _qrzApi;
        private string _apiKey;
        private readonly SecurityServices _secure;
        private readonly NetworkCredential _creds;

        public QrzApi(IServiceProvider services)
        {
            _config = services.GetRequiredService<IConfiguration>();
            _secure = services.GetRequiredService<SecurityServices>();
            _logger = services.GetRequiredService<ILogger<QrzApi>>();
            _xmlService = services.GetRequiredService<XmlServices>();
                    
            _baseUrl = "https://xmldata.qrz.com/xml/current";
            System.Console.WriteLine("user");
            //_userName = Console.ReadLine();
            
            /* 
            System.Console.WriteLine("password");
            while (true)
            {
                var key = System.Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter)
                {
                    break;
                }
                _password += key.KeyChar;
            }
            */
            
            _creds = _secure.ConvertToSecure(username: _userName, password: _password);

            _password = null;
            _userName = null;


            _apiKey = this.GetKey().Result;
            _sessionCheckUrl = $"{_baseUrl}/?s={_apiKey};callsign=kf7ign";
        }

        public async Task<String> GetKey()
        {
            string result = string.Empty;
            using (var client = new HttpClient())
            {
                var fullUrl = $"{_baseUrl}/?username={_creds.UserName};password={_secure.ConvertFromSecure(_creds)};agent=q5.0";
                var response = client.GetAsync(fullUrl).Result;
                result = response.Content.ReadAsStringAsync().Result;
            }
            _logger.LogInformation($"{result}");
            var byteArray = Encoding.UTF8.GetBytes(result);
            using (var sr = new StreamReader(new MemoryStream(byteArray)))
            {
                _qrzApi = new XmlServices().GetQrzResultFromString(sr);
            }  

            if(!string.IsNullOrEmpty(_qrzApi.Session.Key))
            {
                return _qrzApi.Session.Key;
            } 
            else 
            {
                if (!string.IsNullOrEmpty(_qrzApi.Session.Error))
                {
                    _logger.LogError($"(_qrzApi.Session.Error)");
                    throw new Exception($"No key returned!");
                }
                throw new Exception("qrz api error");
            }
        }

        public async Task<QrzApiXml.QRZDatabase> GetCallInfo(string callsign)
        {
            string result = string.Empty;
            using (var client = new HttpClient())
            {
                var response = client.GetAsync($"{_baseUrl}/?s={_apiKey};callsign={callsign}").Result;
                result = response.Content.ReadAsStringAsync().Result;
            }

            _logger.LogInformation(result);

            var byteArray = Encoding.UTF8.GetBytes(result);
            using (var sr = new StreamReader(new MemoryStream(byteArray)))
            {
                _qrzApi = new XmlServices().GetQrzResultFromString(sr);
            }  

            return _qrzApi;
        }
    }
}