
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Net;


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

        public QrzApi(IServiceProvider services)
        {
            _config = services.GetRequiredService<IConfiguration>();
            _baseUrl = "https://xmldata.qrz.com/xml/current";
            _sessionCheckUrl = $"https://xmldata.qrz.com/xml/current/?s={_config["QrzApiKey"]};callsign=kf7ign";
            System.Console.WriteLine("user");
            _userName = Console.ReadLine();
            System.Console.WriteLine("pass");
            _password = Console.ReadLine();
            _logger = services.GetRequiredService<ILogger<QrzApi>>();
            this.GetKey();
        }

        public async Task<String> GetKey()
        {
            string result = string.Empty;
            using (var client = new HttpClient())
            {
                var fullUrl = $"{_baseUrl}/?username={_userName};password={_password};agent=q5.0";
                var response = client.GetAsync(fullUrl).Result;
                result = response.Content.ReadAsStringAsync().Result;
            }
            System.Console.WriteLine($"{result}");
            return result;
        }

        public async Task<string> CheckSession(string url)
        {
            string result = string.Empty;
            return result;
        }
    }
}