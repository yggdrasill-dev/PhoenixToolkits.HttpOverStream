using HttpOverStream;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.Features;

namespace PhoenixToolkits.HttpOverStream.Server.AspnetCore;

public class CustomListenerHost : IServer
{
	private static Uri _localhostUri = new Uri("http://localhost/");
	private readonly IListen _listener;

	public IFeatureCollection Features { get; }

	public CustomListenerHost(IListen listener)
		: this(new FeatureCollection(), listener)
	{
	}

	public CustomListenerHost(IFeatureCollection featureCollection, IListen listener)
	{
		Features = featureCollection ?? throw new ArgumentNullException(nameof(featureCollection));
		_listener = listener ?? throw new ArgumentNullException(nameof(listener));
	}

	public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
		where TContext : notnull
	{
		return _listener.StartAsync(stream =>
		{
			HandleClientStream(stream, application);
		}, cancellationToken);
	}

	public Task StopAsync(CancellationToken cancellationToken)
	{
		return _listener.StopAsync(cancellationToken);
	}

	public void Dispose()
	{ }

	private async void HandleClientStream<TContext>(Stream stream, IHttpApplication<TContext> application)
				where TContext : notnull
	{
		try
		{
			using (stream)
			{
				var httpCtx = new DefaultHttpContext();

				var rf = httpCtx.Features.Get<IHttpRequestFeature>();

				var requestFeature = await CreateRequestAsync(
					stream,
					CancellationToken.None).ConfigureAwait(false);
				httpCtx.Features.Set(requestFeature);

				var responseFeature = httpCtx.Features.Get<IHttpResponseFeature>()!;

				var ctx = application.CreateContext(httpCtx.Features);

				var body = new MemoryStream();

				httpCtx.Response.Body = body;

				await application
					.ProcessRequestAsync(ctx)
					.ConfigureAwait(false);

				await stream.WriteServerResponseStatusAndHeadersAsync(
					requestFeature.Protocol,
					responseFeature.StatusCode.ToString(),
					responseFeature.ReasonPhrase,
					responseFeature.Headers
						.Select(i => new KeyValuePair<string, IEnumerable<string>>(
							i.Key,
							i.Value)),
					_ => { },
					CancellationToken.None).ConfigureAwait(false);

				body.Position = 0;

				await body.CopyToAsync(stream).ConfigureAwait(false);

				await stream.FlushAsync().ConfigureAwait(false);
			}
		}
		catch (Exception e)
		{
			Console.WriteLine($"[CustomListenerHost]: error handling client stream: {e.Message}");
		}
	}

	private async Task<IHttpRequestFeature> CreateRequestAsync(Stream stream, CancellationToken cancellationToken)
	{
		var firstLine = await stream.ReadLineAsync(cancellationToken).ConfigureAwait(false);
		var parts = firstLine.Split(' ');
		var result = new HttpRequestFeature();
		result.Headers.Host = "localhost";

		result.Method = parts[0];
		var uri = new Uri(parts[1], UriKind.RelativeOrAbsolute);
		if (!uri.IsAbsoluteUri)
		{
			uri = new Uri(_localhostUri, uri);
		}
		result.Protocol = parts[2];
		for (; ; )
		{
			var line = await stream.ReadLineAsync(cancellationToken).ConfigureAwait(false);
			if (line.Length == 0)
			{
				break;
			}
			(var name, var values) = HttpParser.ParseHeaderNameValues(line);
			result.Headers.Add(name, new Microsoft.Extensions.Primitives.StringValues(values.ToArray()));
		}

		result.Scheme = uri.Scheme;
		result.Path = PathString.FromUriComponent(uri);
		result.QueryString = QueryString.FromUriComponent(uri).Value ?? string.Empty;
		result.Body = new BodyStream(stream, result.Headers.ContentLength);
		return result;
	}
}
