using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using log4net.Core;
using System.Linq;
using Utf8Json;
using Utf8Json.Resolvers;

namespace log4net.Elasticsearch.Async.IntegrationTests
{
    public class EventJsonSerializer : IEventJsonSerializer
    {
        public string SerializeToJson(LoggingEvent loggingEvent)
        {
            var properties = loggingEvent.Properties;
            if (properties == null) properties = new Util.PropertiesDictionary();
            properties["@timestamp"] = loggingEvent.TimeStamp.ToUniversalTime().ToString("O");

            var projection = new
            {
                loggingEvent.LoggerName,
                loggingEvent.Domain,
                loggingEvent.Identity,
                loggingEvent.ThreadName,
                loggingEvent.UserName,
                TimeStamp = loggingEvent.TimeStamp.ToUniversalTime().ToString("O"),
                Exception = loggingEvent.ExceptionObject ?? new object(),
                Message = loggingEvent.RenderedMessage,
                Fix = loggingEvent.Fix.ToString(),
                Environment.MachineName,
                Level = loggingEvent.Level?.DisplayName,
                MessageObject = loggingEvent.MessageObject ?? new object(),
                loggingEvent.LocationInformation?.ClassName,
                loggingEvent.LocationInformation?.FileName,
                loggingEvent.LocationInformation?.LineNumber,
                loggingEvent.LocationInformation?.FullInfo,
                loggingEvent.LocationInformation?.MethodName,
                Properties = properties
            };

            var json = JsonSerializer.ToJsonString(projection, StandardResolver.CamelCase);
            return json;
        }
    }
}
