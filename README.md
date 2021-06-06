# log4net.AsyncAppender

[![Nuget](https://img.shields.io/nuget/vpre/log4net.AsyncAppender)](https://www.nuget.org/packages/log4net.AsyncAppender)
[![netstandard2.0](https://img.shields.io/badge/netstandard-2.0-blue)](https://docs.microsoft.com/en-us/dotnet/standard/net-standard#net-implementation-support)
<br/>
[![CI](https://img.shields.io/github/workflow/status/tommasobertoni/log4net.AsyncAppender/CI/main)](https://github.com/tommasobertoni/log4net.AsyncAppender/actions?query=workflow%3ACI+branch%3Amain)
[![Coverage](https://img.shields.io/coveralls/github/tommasobertoni/log4net.AsyncAppender/main)](https://coveralls.io/github/tommasobertoni/log4net.AsyncAppender?branch=main)
<br/>
[![License MIT](https://img.shields.io/badge/license-MIT-green)](LICENSE)

## Packages

- **log4net.AsyncAppender**: abstract appender that uses concurrent collections and tasks to enable asynchronous and concurrent batch processing of `LoggingEvent`s

- **log4net.AsyncAppender.HttpEndpoint**: abstract appender built on top of `log4net.AsyncAppender`, sends the `LoggingEvent`s to a specified http uri

- **log4net.AsyncAppender.ElasticSearch**: appender built on top of `log4net.AsyncAppender.HttpEndpoint`, sends the `LoggingEvent`s to an ElasticSearch http uri using the specified ELK configuration
