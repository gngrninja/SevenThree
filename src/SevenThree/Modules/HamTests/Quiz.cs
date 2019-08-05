using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SevenThree.Database;

namespace SevenThree.Modules
{
    public class QuizTool
    {
        private readonly ILogger _logger;
        private readonly SevenThreeContext _db;
        private bool _isActive = false;
        private ulong _discordServer;
        private ulong _id;

        public bool IsActive
        {
            get
            {
                return _isActive;
            }
            set
            {
                _isActive = value;
            }
        }

        public ulong Id
        {
            get
            {
                return _id;
            }
            set
            {
                _id = value;
            }
        }

        public QuizTool(IServiceProvider services)
        {
            _logger = services.GetRequiredService<ILogger<CallAssociation>>();
            _db = services.GetRequiredService<SevenThreeContext>();
        }

        public void SetServer(ulong discordServer)
        {
            _discordServer = discordServer;
            this.Id = _discordServer;
        }

        public void SetActive()
        {
            IsActive = true;
        }
    }
}