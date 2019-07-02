using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using log4net.Core;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("log4net.AsyncAppender.HttpEndpoint.Tests")]

namespace log4net.AsyncAppender
{
    public abstract class HttpEndpointAsyncAppender : AsyncAppender
    {
        #region Endpoint

        public string Url { get; set; }

        public string Scheme { get; set; }

        public string Host { get; set; }

        public string Path { get; set; }

        public string Port { get; set; }

        public string UserName { get; set; }

        public string Password { get; set; }

        public string Query { get; set; }

        #endregion

        #region Json serialization

        public IEventJsonSerializer EventJsonSerializer { get; set; }

        public Func<LoggingEvent, string> EventJsonSerializerDelegate { get; set; }

        #endregion

        public bool UseDefaultEventJsonSerializerWhenMissing { get; set; }

        public HttpClient HttpClient { get; set; }

        public bool EnsureSuccessStatusCode { get; set; }

        protected Uri _endpoint;

        public HttpEndpointAsyncAppender() : base()
        {
            this.UseDefaultEventJsonSerializerWhenMissing = true;
        }

        #region Setup

        protected override void Configure()
        {
            base.Configure();

            if (this.EventJsonSerializer == null && this.EventJsonSerializerDelegate == null)
            {
                if (this.UseDefaultEventJsonSerializerWhenMissing)
                    this.EventJsonSerializerDelegate = e =>
                        Utf8Json.JsonSerializer.ToJsonString(e, Utf8Json.Resolvers.StandardResolver.CamelCase);
            }
        }

        protected override bool ValidateSelf()
        {
            if (!base.ValidateSelf()) return false;

            try
            {
                if (this.EventJsonSerializer == null &&
                    this.EventJsonSerializerDelegate == null)
                {
                    this.ErrorHandler?.Error("Missing event to json serializer.");
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(this.Url))
                {
                    if (!Uri.TryCreate(this.Url, UriKind.Absolute, out var _))
                    {
                        this.ErrorHandler?.Error($"Invalid url: {this.Url}");
                        return false;
                    }
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(this.Scheme))
                    {
                        this.ErrorHandler?.Error($"Missing schema.");
                        return false;
                    }
                    else if (this.Scheme.ToLower() != Uri.UriSchemeHttp.ToLower() &&
                             this.Scheme.ToLower() != Uri.UriSchemeHttps.ToLower())
                    {
                        this.ErrorHandler?.Error($"Invalid schema: must be either http or https.");
                        return false;
                    }

                    if (string.IsNullOrWhiteSpace(this.Host))
                    {
                        this.ErrorHandler?.Error($"Missing host.");
                        return false;
                    }

                    if (string.IsNullOrWhiteSpace(this.Path))
                    {
                        this.ErrorHandler?.Error($"Missing path.");
                        return false;
                    }

                    if (!string.IsNullOrWhiteSpace(this.Port))
                    {
                        if (!int.TryParse(this.Port, out var _))
                        {
                            this.ErrorHandler?.Error($"Invalid port.");
                            return false;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(this.UserName) &&
                        string.IsNullOrWhiteSpace(this.Password))
                    {
                        this.ErrorHandler?.Error($"UserName without Password.");
                        return false;
                    }

                    if (!string.IsNullOrWhiteSpace(this.Password) &&
                        string.IsNullOrWhiteSpace(this.UserName))
                    {
                        this.ErrorHandler?.Error($"Password without UserName.");
                        return false;
                    }

                    // Try create uri.
                    var uri = CreateEndpoint();

                    if (!Uri.IsWellFormedUriString(uri.ToString(), UriKind.Absolute))
                    {
                        this.ErrorHandler?.Error($"Uri is not well formed: {uri.ToString()}.");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                this.ErrorHandler?.Error("Error during validation", ex);
                return false;
            }
        }

        protected override void Activate()
        {
            if (string.IsNullOrWhiteSpace(this.Url) ||
                !Uri.TryCreate(this.Url, UriKind.Absolute, out _endpoint))
            {
                _endpoint = CreateEndpoint();
            }

            base.Activate();
        }

        #endregion

        protected virtual Uri CreateEndpoint()
        {
            var uriBuilder = new UriBuilder
            {
                Scheme = this.Scheme,
                Host = this.Host,
                Path = this.Path,
                UserName = this.UserName,
                Password = this.Password,
                Query = this.Query
            };

            if (!string.IsNullOrWhiteSpace(this.Port))
            {
                int.TryParse(this.Port, out var port);
                uriBuilder.Port = port;
            }

            return uriBuilder.Uri;
        }

        protected override async Task ProcessAsync(IReadOnlyList<LoggingEvent> events, CancellationToken cancellationToken)
        {
            try
            {
                var requestTask = CreateHttpRequestAsync();
                var contentTask = GetHttpContentAsync(events);

                await Task.WhenAll(requestTask, contentTask).ConfigureAwait(false);

                var request = requestTask.Result;
                request.Content = contentTask.Result;

                // Don't use the cancellation token here: allow the last events to flush.
                var response = await this.HttpClient.SendAsync(request, CancellationToken.None).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    await this.HandleUnsuccessfulResponseAsync(
                        events, request, response, cancellationToken).ConfigureAwait(false);
                }

                if (this.EnsureSuccessStatusCode)
                    response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                this.ErrorHandler?.Error($"Error while processing events", ex);
            }
        }

        protected virtual async Task<HttpRequestMessage> CreateHttpRequestAsync()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);

            if (!string.IsNullOrWhiteSpace(_endpoint.UserInfo))
            {
                await ApplyUserInfoAsync(request, _endpoint.UserInfo).ConfigureAwait(false);
            }

            return request;
        }

        protected virtual Task ApplyUserInfoAsync(HttpRequestMessage request, string userInfo)
        {
            // Assume basic auth.

            var base64UserInfo = Convert.ToBase64String(Encoding.ASCII.GetBytes(userInfo));

            request.Headers.Remove("Authorization");
            request.Headers.Add("Authorization", $"Basic {base64UserInfo}");

            return Task.CompletedTask;
        }

        protected virtual Task HandleUnsuccessfulResponseAsync(
            IReadOnlyList<LoggingEvent> eventsSent,
            HttpRequestMessage request,
            HttpResponseMessage response,
            CancellationToken cancellationToken) => Task.CompletedTask;

        protected virtual string SerializeToJson(LoggingEvent @event)
        {
            return this.EventJsonSerializer != null
                ? this.EventJsonSerializer.SerializeToJson(@event)
                : this.EventJsonSerializerDelegate(@event);
        }

        protected abstract Task<HttpContent> GetHttpContentAsync(IReadOnlyList<LoggingEvent> events);
    }
}
