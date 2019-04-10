using System;
using System.Collections.Generic;
using Xunit;
using System.Linq;

namespace log4net.Elasticsearch.Async.Tests
{
    public class AppenderSettingsTests
    {
        [Fact]
        public void Settings_are_not_valid_when_created_with_empty_connection_string()
        {
            var settings = new AppenderSettings(string.Empty);
            Assert.False(settings.AreValid());
        }

        [Fact]
        public void Settings_are_not_valid_when_created_with_null_connection_string()
        {
            var settings = new AppenderSettings(null as string);
            Assert.False(settings.AreValid());
        }

        [Fact]
        public void Settings_are_not_valid_when_created_with_empty_dictionary()
        {
            var settings = new AppenderSettings(new Dictionary<string, string>());
            Assert.False(settings.AreValid());
        }

        [Fact]
        public void Settings_are_valid_when_created_with_connection_string()
        {
            var connectionString = "Scheme=https;Server=myServer.com;Index=anIndex";
            var settings = new AppenderSettings(connectionString);
            Assert.True(settings.AreValid());
        }

        [Fact]
        public void Settings_are_valid_when_created_with_dictionary()
        {
            var dictionary = new Dictionary<string, string>
            {
                ["Scheme"] = "https",
                ["Server"] = "myServer.com",
                ["Index"] = "anIndex"
            };

            var settings = new AppenderSettings(dictionary);
            Assert.True(settings.AreValid());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Schema=https")]
        [InlineData("Server=myServer.com")]
        [InlineData("Schema=https;Server=myServer.com")]
        [InlineData("Schema=https;Index=anIndex")]
        public void Partial_connection_string_yields_invalid_uri(string connectionString)
        {
            var settings = new AppenderSettings(connectionString);
            Assert.False(settings.AreValid());
            Assert.ThrowsAny<Exception>(() => settings.Uri);
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("Schema")]
        public void Invalid_connection_string_yields_invalid_uri(string connectionString)
        {
            Assert.ThrowsAny<Exception>(() => new AppenderSettings(connectionString));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Schema=https")]
        [InlineData("Server=myServer.com")]
        [InlineData("Schema=https", "Server=myServer.com")]
        [InlineData("Schema=https", "Index=anIndex")]
        public void Partial_dictionary_yields_invalid_uri(params string[] @params)
        {
            Dictionary<string, string> dictionary = null;

            if (@params != null)
            {
                dictionary = new Dictionary<string, string>();
                foreach (var param in @params)
                {
                    var tokens = param.Split('=');
                    var key = tokens.First();
                    var value = tokens.Skip(1).FirstOrDefault();
                    dictionary[key] = value;
                }
            }

            var settings = new AppenderSettings(dictionary);
            Assert.False(settings.AreValid());
            Assert.ThrowsAny<Exception>(() => settings.Uri);
        }

        [Theory]
        [InlineData("https", "test", "pwd", "myServer.com", "80", "anIndex", "routing123", 10, true)]
        [InlineData("https", "test", "pwd", "myServer.com", "80", "anIndex", "routing123", 10, false)]
        [InlineData("https", "test", "pwd", "myServer.com", "80", "anIndex", "routing123", 1, true)]
        [InlineData("https", "test", "pwd", "myServer.com", "80", "anIndex", "routing123", 1, false)]
        [InlineData("https", "test", "pwd", "myServer.com", "80", "anIndex", null, 10, true)]
        public void Settings_are_initialized_correctly_with_connection_string(
            string scheme, string user, string password, string server, string port, string index, string routing, int bufferSize, bool rolling)
        {
            var connectionString = $"Scheme={scheme};User={user};Pwd={password};Server={server};Port={port};Index={index};Routing={routing};BufferSize={bufferSize};Rolling={rolling}";

            var settings = new AppenderSettings(connectionString);
            Assert.True(settings.AreValid());
            Assert.NotNull(settings.Uri);
            
            if (!string.IsNullOrEmpty(user) || !string.IsNullOrEmpty(password))
                Assert.NotNull(settings.Uri.UserInfo);

            Assert.Equal(scheme, settings.Scheme);
            Assert.Equal(user, settings.User);
            Assert.Equal(password, settings.Password);
            Assert.Equal(server, settings.Server);
            Assert.Equal(port, settings.Port);
            Assert.Equal(rolling, settings.IsRollingIndex);

            if (settings.IsRollingIndex)
            {
                Assert.NotEqual(index, settings.Index);
                Assert.Contains(index, settings.Index);
            }
            else
                Assert.Equal(index, settings.Index);

            if (string.IsNullOrEmpty(routing))
                Assert.True(string.IsNullOrEmpty(settings.Routing));
            else
                Assert.Contains(routing, settings.Routing);

            if (bufferSize > 1)
                Assert.False(string.IsNullOrEmpty(settings.Bulk));
            else
                Assert.True(string.IsNullOrEmpty(settings.Bulk));
        }
    }
}
