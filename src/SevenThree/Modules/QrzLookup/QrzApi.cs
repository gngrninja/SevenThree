
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
using SevenThree.Database;
using System.Linq;

namespace SevenThree.Modules
{
    public class QrzApi
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _config;
        private string _baseUrl;
        private string _sessionCheckUrl;
        private XmlServices _xmlService;        
        private string _apiKey;              
        private readonly SevenThreeContext _db;
        private ApiData _qrzApiData;

        public QrzApi(IServiceProvider services)
        {
            _config     = services.GetRequiredService<IConfiguration>();            
            _logger     = services.GetRequiredService<ILogger<QrzApi>>();
            _xmlService = services.GetRequiredService<XmlServices>();
            _db         = services.GetRequiredService<SevenThreeContext>();                    

            _qrzApiData = _db.ApiData.Where(a => a.AppName == "QRZ").FirstOrDefault();

            if (_qrzApiData == null) 
            {
                throw new ApplicationException("Unable to get QRZ api data, cannot continue!");
            }

            _baseUrl = _qrzApiData.ApiBaseUrl;
            _apiKey  = _qrzApiData.ApiKey;

            this.GetCallInfo("kf7ign");
        }

        public async Task GetKey()
        {            
            Cred creds = null;
            string result = string.Empty;
            try
            {
                creds            = _db.Cred.FirstOrDefault();                                                       
                _sessionCheckUrl = $"{_baseUrl}/?s={_apiKey};callsign=kf7ign";
                
                using (var client = new HttpClient())
                {
                    var fullUrl  = $"{_baseUrl}/?username={creds.User};password={creds.Pass};agent=q5.0";
                    var response = await client.GetAsync(fullUrl);
                    result       = await response.Content.ReadAsStringAsync();
                }
                _logger.LogInformation($"{result}");
            }            
            catch
            {
                _logger.LogError("Error updating QRZ Api key!");
            }
            finally 
            {                                
                creds = null;
            }
            var qrzApi = ConvertResultToXml(result);           
            if(!string.IsNullOrEmpty(qrzApi.Session.Key))
            {
                _qrzApiData.ApiKey = qrzApi.Session.Key;
                await _db.SaveChangesAsync();
                _apiKey = _qrzApiData.ApiKey;
                _logger.LogInformation("Updated QRZ Api Key!");
            } 
            else 
            {
                if (!string.IsNullOrEmpty(qrzApi.Session.Error))
                {
                    _logger.LogError($"{qrzApi.Session.Error}");
                }
            }
        }

        public async Task<QrzApiXml.QRZDatabase> GetDxccInfo(string dxcc)
        {                        
            QrzApiXml.QRZDatabase xmlResult = ConvertResultToXml(await QrzApiRequest(dxcc, "dxcc"));       
            if (!string.IsNullOrEmpty(xmlResult.Session.Error))
            {
                switch (xmlResult.Session.Error)
                {
                    case "Session Timeout":
                    {
                        _logger.LogInformation("QRZ Api Key needs to be refreshed... attempting to update");
                        await GetKey();  
                        break;
                    }
                    case "Invalid session key":
                    {
                        _logger.LogInformation("QRZ Api Key needs to be refreshed... attempting to update");
                        await GetKey();  
                        break;
                    }
                }              
                xmlResult = ConvertResultToXml(await QrzApiRequest(dxcc, "dxcc"));                
            }
            else if (!string.IsNullOrEmpty(xmlResult.Session.Error) && !xmlResult.Session.Error.Contains("Not found:"))
            {
                _logger.LogError($"Error accessing QRZ Api -> [{xmlResult.Session.Error}]!");                
            }
            return xmlResult;
        }

        public async Task<QrzApiXml.QRZDatabase> GetCallInfo(string callsign)
        {            
            var xmlResult = ConvertResultToXml(await QrzApiRequest(callsign, "callsign"));            
            if (!string.IsNullOrEmpty(xmlResult.Session.Error))
            {
                switch (xmlResult.Session.Error)
                {
                    case "Session Timeout":
                    {
                        _logger.LogInformation("QRZ Api Key needs to be refreshed... attempting to update");
                        await GetKey();  
                        break;
                    }
                    case "Invalid session key":
                    {
                        _logger.LogInformation("QRZ Api Key needs to be refreshed... attempting to update");
                        await GetKey();  
                        break;
                    }
                }              
                xmlResult = ConvertResultToXml(await QrzApiRequest(callsign, "callsign"));                
            }
            else if (!string.IsNullOrEmpty(xmlResult.Session.Error) && !xmlResult.Session.Error.Contains("Not found:"))
            {
                _logger.LogError($"Error accessing QRZ Api -> [{xmlResult.Session.Error}]!");                
            }
            return xmlResult;            
        }  

        private async Task<string> QrzApiRequest(string lookup, string type)
        {
            string result = null;
            using (var client = new HttpClient())
            {
                HttpResponseMessage response = null;
                try
                {
                    response = await client.GetAsync($"{_baseUrl}/?s={_apiKey};{type}={lookup}");
                    result = await response.Content.ReadAsStringAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error accessing QRZ Api -> [{ex.Message}]");
                }
            }
            _logger.LogInformation(result);
            return result;
        }

        private static QrzApiXml.QRZDatabase ConvertResultToXml(string result)
        {
            QrzApiXml.QRZDatabase xmlResult;
            var byteArray = Encoding.UTF8.GetBytes(result);
            using (var sr = new StreamReader(new MemoryStream(byteArray)))
            {
                xmlResult = new XmlServices().GetQrzResultFromString(sr);
            }

            return xmlResult;
        }                                   
    }
}

//System.Console.WriteLine("user");                     
//_userName = Console.ReadLine();
//System.Console.WriteLine("password");
/* 
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