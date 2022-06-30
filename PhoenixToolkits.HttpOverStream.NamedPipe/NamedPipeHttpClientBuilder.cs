﻿using System.Security.Principal;
using HttpOverStream.Client;
using HttpOverStream.Logging;

namespace HttpOverStream.NamedPipe;

public class NamedPipeHttpClientBuilder
{
	private string _pipeName;
	private ILoggerHttpOverStream? _logger;
	private DelegatingHandler? _outerHandler;
	private TimeSpan? _perRequestTimeout;
	private Version? _httpVersion;
	private TimeSpan? _namedPipeConnectionTimeout;
	private TokenImpersonationLevel _impersonationLevel = TokenImpersonationLevel.Identification;

	public NamedPipeHttpClientBuilder(string pipeName)
	{
		_pipeName = pipeName;
	}

	public NamedPipeHttpClientBuilder WithLogger(ILoggerHttpOverStream logger)
	{
		_logger = logger;
		return this;
	}

	public NamedPipeHttpClientBuilder WithDelegatingHandler(DelegatingHandler outerHandler)
	{
		_outerHandler = outerHandler;
		return this;
	}

	public NamedPipeHttpClientBuilder WithPerRequestTimeout(TimeSpan perRequestTimeout)
	{
		_perRequestTimeout = perRequestTimeout;
		return this;
	}

	public NamedPipeHttpClientBuilder WithHttpVersion(Version httpVersion)
	{
		_httpVersion = httpVersion;
		return this;
	}

	public NamedPipeHttpClientBuilder WithNamedPipeConnectionTimeout(TimeSpan timeSpan)
	{
		_namedPipeConnectionTimeout = timeSpan;
		return this;
	}

	public NamedPipeHttpClientBuilder WithImpersonationLevel(TokenImpersonationLevel impersonationLevel)
	{
		_impersonationLevel = impersonationLevel;
		return this;
	}

	public HttpClient Build()
	{
		var connectionTimeout = 0;
		if (_namedPipeConnectionTimeout.HasValue)
		{
			connectionTimeout = (int)_namedPipeConnectionTimeout.Value.TotalMilliseconds;
		}

		var dialer = new NamedPipeDialer(_pipeName, ".", System.IO.Pipes.PipeOptions.Asynchronous, connectionTimeout, _impersonationLevel);

		var innerHandler = new DialMessageHandler(dialer, _logger, _httpVersion);
		HttpClient httpClient;
		if (_outerHandler != null)
		{
			_outerHandler.InnerHandler = innerHandler;
			httpClient = new HttpClient(_outerHandler);
		}
		else
		{
			httpClient = new HttpClient(innerHandler);
		}

		httpClient.BaseAddress = new Uri("http://localhost");
		if (_perRequestTimeout != null)
		{
			httpClient.Timeout = _perRequestTimeout.Value;
		}

		return httpClient;
	}
}
