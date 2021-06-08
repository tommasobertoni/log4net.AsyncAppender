using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using log4net.Core;

namespace log4net.AsyncAppender
{
    public abstract class HttpEndpointAsyncAppender : AsyncAppender
    {
        protected Uri? _endpoint;

        public HttpEndpointAsyncAppender() : base()
        {
            UseDefaultEventJsonSerializerWhenMissing = true;
        }

        public bool UseDefaultEventJsonSerializerWhenMissing { get; set; }

        public HttpClient? HttpClient { get; set; }

        public bool EnsureSuccessStatusCode { get; set; }

        #region Endpoint

        public string? Url { get; set; }

        public string? Scheme { get; set; }

        public string? Host { get; set; }

        public string? Path { get; set; }

        public string? Port { get; set; }

        public string? UserName { get; set; }

        public string? Password { get; set; }

        public string? Query { get; set; }

        #endregion

        #region Json serialization

        public IEventJsonSerializer? EventJsonSerializer { get; set; }

        public Func<LoggingEvent, string>? EventJsonSerializerDelegate { get; set; }

        #endregion

        #region Setup

        protected override void Configure()
        {
            base.Configure();

            if (EventJsonSerializer == null && EventJsonSerializerDelegate == null)
            {
                if (UseDefaultEventJsonSerializerWhenMissing)
                    EventJsonSerializerDelegate = e =>
                        Utf8Json.JsonSerializer.ToJsonString(e, Utf8Json.Resolvers.StandardResolver.CamelCase);
            }

            if (HttpClient == null)
            {
                HttpClient = NewHttpClient();
            }
        }

        protected virtual HttpClient NewHttpClient()
        {
            return new HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip,
                AllowAutoRedirect = true,
            });
        }

        protected override bool ValidateSelf()
        {
            if (!base.ValidateSelf()) return false;

            try
            {
                if (EventJsonSerializer == null &&
                    EventJsonSerializerDelegate == null)
                {
                    ErrorHandler?.Error("Missing event to json serializer.");
                    return false;
                }

                if (HttpClient == null)
                {
                    ErrorHandler?.Error("Missing HttpClient");
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(Url))
                {
                    if (!Uri.TryCreate(Url, UriKind.Absolute, out _))
                    {
                        ErrorHandler?.Error($"Invalid url: {Url}");
                        return false;
                    }
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(Scheme))
                    {
                        ErrorHandler?.Error($"Missing scheme.");
                        return false;
                    }
                    else if (Scheme!.ToLower() != Uri.UriSchemeHttp.ToLower() &&
                             Scheme!.ToLower() != Uri.UriSchemeHttps.ToLower())
                    {
                        ErrorHandler?.Error($"Invalid schema: must be either http or https.");
                        return false;
                    }

                    if (string.IsNullOrWhiteSpace(Host))
                    {
                        ErrorHandler?.Error($"Missing host.");
                        return false;
                    }

                    if (!string.IsNullOrWhiteSpace(Port))
                    {
                        if (!int.TryParse(Port, out _))
                        {
                            ErrorHandler?.Error($"Invalid port.");
                            return false;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(UserName) &&
                        string.IsNullOrWhiteSpace(Password))
                    {
                        ErrorHandler?.Error($"UserName without Password.");
                        return false;
                    }

                    if (!string.IsNullOrWhiteSpace(Password) &&
                        string.IsNullOrWhiteSpace(UserName))
                    {
                        ErrorHandler?.Error($"Password without UserName.");
                        return false;
                    }

                    // Try create uri.
                    var uri = CreateEndpoint();

                    if (!Uri.IsWellFormedUriString(uri.ToString(), UriKind.Absolute))
                    {
                        ErrorHandler?.Error($"Uri is not well formed: {uri}.");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                ErrorHandler?.Error("Error during validation", ex);
                return false;
            }
        }

        protected override void Activate()
        {
            base.Activate();
            _endpoint = CreateEndpoint();
        }

        #endregion

        protected virtual Uri CreateEndpoint()
        {
            if (!string.IsNullOrWhiteSpace(Url))
            {
                if (Uri.TryCreate(Url, UriKind.Absolute, out var endpoint))
                    return endpoint;
            }

            var uriBuilder = new UriBuilder
            {
                Scheme = Scheme,
                Host = Host,
                Path = Path,
                UserName = UserName,
                Password = Password,
                Query = Query
            };

            if (!string.IsNullOrWhiteSpace(Port))
            {
                int.TryParse(Port, out var port);
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
                var response = await HttpClient!.SendAsync(request, CancellationToken.None).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    await HandleUnsuccessfulResponseAsync(
                        events, request, response, cancellationToken).ConfigureAwait(false);
                }

                if (EnsureSuccessStatusCode)
                    response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                ErrorHandler?.Error($"Error while processing events", ex);
            }
        }

        protected virtual async Task<HttpRequestMessage> CreateHttpRequestAsync()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);

            if (!string.IsNullOrWhiteSpace(_endpoint!.UserInfo))
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
            return EventJsonSerializer is not null
                ? EventJsonSerializer.SerializeToJson(@event)
                : EventJsonSerializerDelegate is not null
                ? EventJsonSerializerDelegate(@event)
                : @event.ToString();
        }

        protected abstract Task<HttpContent> GetHttpContentAsync(IReadOnlyList<LoggingEvent> events);
    }
}
