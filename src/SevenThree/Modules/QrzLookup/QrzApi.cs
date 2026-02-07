using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
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
        private readonly XmlServices _xmlService;
        private readonly string _baseUrl;
        private readonly string _username;
        private readonly string _password;
        private string _apiKey;

        public bool IsConfigured { get; private set; }

        public QrzApi(IServiceProvider services)
        {
            _config = services.GetRequiredService<IConfiguration>();
            _logger = services.GetRequiredService<ILogger<QrzApi>>();
            _xmlService = services.GetRequiredService<XmlServices>();

            // Get credentials from environment variables
            _username = _config["QRZ_Username"];
            _password = _config["QRZ_Password"];
            _baseUrl = _config["QRZ_ApiUrl"] ?? "https://xmldata.qrz.com/xml/current/";

            if (string.IsNullOrEmpty(_username) || string.IsNullOrEmpty(_password))
            {
                _logger.LogWarning("QRZ API credentials not configured. Set SEVENTHREE_QRZ_Username and SEVENTHREE_QRZ_Password environment variables.");
                IsConfigured = false;
                return;
            }

            IsConfigured = true;
            _logger.LogInformation("QRZ API configured from environment variables.");
        }

        public async Task GetKey()
        {
            if (!IsConfigured) return;

            string result = string.Empty;
            try
            {
                using (var client = new HttpClient())
                {
                    var fullUrl = $"{_baseUrl}?username={_username};password={_password};agent=q5.0";
                    var response = await client.GetAsync(fullUrl);
                    result = await response.Content.ReadAsStringAsync();
                }
                _logger.LogDebug("QRZ key refresh response received");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating QRZ API key!");
                return;
            }

            var qrzApi = ConvertResultToXml(result);
            if (!string.IsNullOrEmpty(qrzApi.Session.Key))
            {
                _apiKey = qrzApi.Session.Key;
                _logger.LogInformation("QRZ API session key updated.");
            }
            else if (!string.IsNullOrEmpty(qrzApi.Session.Error))
            {
                _logger.LogError($"QRZ API error: {qrzApi.Session.Error}");
            }
        }

        public async Task<QrzApiXml.QRZDatabase> GetDxccInfo(string dxcc)
        {
            if (!IsConfigured)
            {
                return new QrzApiXml.QRZDatabase { Session = new QrzApiXml.Session { Error = "QRZ API not configured" } };
            }

            // Get session key if not set
            if (string.IsNullOrEmpty(_apiKey))
            {
                await GetKey();
            }

            var xmlResult = ConvertResultToXml(await QrzApiRequest(dxcc, "dxcc"));

            if (!string.IsNullOrEmpty(xmlResult.Session.Error))
            {
                if (xmlResult.Session.Error == "Session Timeout" || xmlResult.Session.Error == "Invalid session key")
                {
                    _logger.LogInformation("QRZ API session expired, refreshing...");
                    await GetKey();
                    xmlResult = ConvertResultToXml(await QrzApiRequest(dxcc, "dxcc"));
                }
                else if (!xmlResult.Session.Error.Contains("Not found:"))
                {
                    _logger.LogError($"QRZ API error: {xmlResult.Session.Error}");
                }
            }

            return xmlResult;
        }

        public async Task<QrzApiXml.QRZDatabase> GetCallInfo(string callsign)
        {
            if (!IsConfigured)
            {
                return new QrzApiXml.QRZDatabase { Session = new QrzApiXml.Session { Error = "QRZ API not configured" } };
            }

            // Get session key if not set
            if (string.IsNullOrEmpty(_apiKey))
            {
                await GetKey();
            }

            var xmlResult = ConvertResultToXml(await QrzApiRequest(callsign, "callsign"));

            if (!string.IsNullOrEmpty(xmlResult.Session.Error))
            {
                if (xmlResult.Session.Error == "Session Timeout" || xmlResult.Session.Error == "Invalid session key")
                {
                    _logger.LogInformation("QRZ API session expired, refreshing...");
                    await GetKey();
                    xmlResult = ConvertResultToXml(await QrzApiRequest(callsign, "callsign"));
                }
                else if (!xmlResult.Session.Error.Contains("Not found:"))
                {
                    _logger.LogError($"QRZ API error: {xmlResult.Session.Error}");
                }
            }

            return xmlResult;
        }

        private async Task<string> QrzApiRequest(string lookup, string type)
        {
            string result = string.Empty;
            using (var client = new HttpClient())
            {
                try
                {
                    var response = await client.GetAsync($"{_baseUrl}?s={_apiKey};{type}={lookup}");
                    result = await response.Content.ReadAsStringAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error making QRZ API request");
                }
            }
            return result;
        }

        internal static QrzApiXml.QRZDatabase ConvertResultToXml(string result)
        {
            if (string.IsNullOrEmpty(result))
            {
                return new QrzApiXml.QRZDatabase { Session = new QrzApiXml.Session { Error = "Empty response from QRZ API" } };
            }

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
