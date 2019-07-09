using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace SevenThree.Services
{
    public class ConfigService
    {
        public IConfigurationRoot ConfigureServices()
        {
            // create the configuration
            var _builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile(path: "config.json");  

            // build the configuration and assign to _config          
            return _builder.Build();
        }
    }
}