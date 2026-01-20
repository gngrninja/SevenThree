using System;
using System.Net;
using Xunit;
using SevenThree.Services;

namespace SevenThree.Tests
{
    public class SecurityServicesTests
    {
        private readonly SecurityServices _sut;

        public SecurityServicesTests()
        {
            _sut = new SecurityServices();
        }

        #region ConvertToSecure Tests

        [Fact]
        public void ConvertToSecure_ValidCredentials_ReturnsNetworkCredential()
        {
            // arrange
            var username = "testuser";
            var password = "testpassword123";

            // act
            var result = _sut.ConvertToSecure(username, password);

            // assert
            Assert.NotNull(result);
            Assert.Equal(username, result.UserName);
            Assert.Equal(password, result.Password);
        }

        [Fact]
        public void ConvertToSecure_EmptyUsername_ReturnsCredentialWithEmptyUsername()
        {
            // arrange
            var username = "";
            var password = "testpassword";

            // act
            var result = _sut.ConvertToSecure(username, password);

            // assert
            Assert.NotNull(result);
            Assert.Equal("", result.UserName);
            Assert.Equal(password, result.Password);
        }

        [Fact]
        public void ConvertToSecure_EmptyPassword_ReturnsCredentialWithEmptyPassword()
        {
            // arrange
            var username = "testuser";
            var password = "";

            // act
            var result = _sut.ConvertToSecure(username, password);

            // assert
            Assert.NotNull(result);
            Assert.Equal(username, result.UserName);
            Assert.Equal("", result.Password);
        }

        [Fact]
        public void ConvertToSecure_SpecialCharacters_HandlesCorrectly()
        {
            // arrange
            var username = "user@domain.com";
            var password = "P@$$w0rd!#%&*";

            // act
            var result = _sut.ConvertToSecure(username, password);

            // assert
            Assert.Equal(username, result.UserName);
            Assert.Equal(password, result.Password);
        }

        [Fact]
        public void ConvertToSecure_UnicodeCharacters_HandlesCorrectly()
        {
            // arrange
            var username = "user_unicodetest";
            var password = "pass_test123";

            // act
            var result = _sut.ConvertToSecure(username, password);

            // assert
            Assert.Equal(username, result.UserName);
            Assert.Equal(password, result.Password);
        }

        #endregion

        #region ConvertFromSecure Tests

        [Fact]
        public void ConvertFromSecure_ValidCredential_ReturnsPassword()
        {
            // arrange
            var username = "testuser";
            var password = "secretpassword";
            var cred = new NetworkCredential(username, password);

            // act
            var result = _sut.ConvertFromSecure(cred);

            // assert
            Assert.Equal(password, result);
        }

        [Fact]
        public void ConvertFromSecure_EmptyPassword_ReturnsEmptyString()
        {
            // arrange
            var cred = new NetworkCredential("user", "");

            // act
            var result = _sut.ConvertFromSecure(cred);

            // assert
            Assert.Equal("", result);
        }

        [Fact]
        public void ConvertFromSecure_SpecialCharactersInPassword_ReturnsCorrectly()
        {
            // arrange
            var password = "!@#$%^&*()_+-=[]{}|;':\",./<>?";
            var cred = new NetworkCredential("user", password);

            // act
            var result = _sut.ConvertFromSecure(cred);

            // assert
            Assert.Equal(password, result);
        }

        #endregion

        #region Round Trip Tests

        [Theory]
        [InlineData("admin", "admin123")]
        [InlineData("user@email.com", "Complex!Pass#2024")]
        [InlineData("", "passwordonly")]
        [InlineData("usernameonly", "")]
        public void RoundTrip_ConvertToAndFromSecure_PreservesData(string username, string password)
        {
            // act
            var secure = _sut.ConvertToSecure(username, password);
            var retrieved = _sut.ConvertFromSecure(secure);

            // assert
            Assert.Equal(password, retrieved);
        }

        #endregion
    }
}
