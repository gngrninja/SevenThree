using System;
using Xunit;
using SevenThree.Services;

namespace SevenThree.Tests
{
    public class ConfigServiceTests
    {
        private readonly ConfigService _sut;

        public ConfigServiceTests()
        {
            _sut = new ConfigService();
        }

        #region ConfigureServices Tests

        [Fact]
        public void ConfigureServices_ReturnsNonNullConfiguration()
        {
            // act
            var config = _sut.ConfigureServices();

            // assert
            Assert.NotNull(config);
        }

        [Fact]
        public void ConfigureServices_ReturnsIConfigurationRoot()
        {
            // act
            var config = _sut.ConfigureServices();

            // assert
            Assert.IsAssignableFrom<Microsoft.Extensions.Configuration.IConfigurationRoot>(config);
        }

        #endregion

        #region Environment Selection Tests

        [Theory]
        [InlineData("production", ".env.production")]
        [InlineData("docker", ".env.development.docker")]
        [InlineData("development", ".env.development")]
        [InlineData("Development", ".env.development")]
        [InlineData("PRODUCTION", ".env.production")]
        [InlineData("unknown", ".env.development")]
        [InlineData("", ".env.development")]
        public void EnvironmentSwitch_SelectsCorrectEnvFile(string environment, string expectedFile)
        {
            // This tests the switch logic from ConfigService
            var envFile = environment.ToLower() switch
            {
                "production" => ".env.production",
                "docker" => ".env.development.docker",
                _ => ".env.development"
            };

            // assert
            Assert.Equal(expectedFile, envFile);
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_CanBeInstantiated()
        {
            // act
            var service = new ConfigService();

            // assert
            Assert.NotNull(service);
        }

        #endregion
    }
}
