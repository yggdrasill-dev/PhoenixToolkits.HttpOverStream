using HttpOverStream;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.Features;

namespace PhoenixToolkits.HttpOverStream.Server.AspnetCore;

public class CustomListenerHost : IServer
{
	private static readonly Uri _LocalhostUri = new("http://localhost/");
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
		=> _listener.StartAsync(
			stream => HandleClientStream(stream, application),
			cancellationToken);

	public Task StopAsync(CancellationToken cancellationToken)
	{
		return _listener.StopAsync(cancellationToken);
	}

	public void Dispose()
	{
		GC.SuppressFinalize(this);
	}

	private static async void HandleClientStream<TContext>(Stream stream, IHttpApplication<TContext> application)
		where TContext : notnull
	{
		try
		{
			using (stream)
			{
				var httpCtx = new DefaultHttpContext();

				var requestFeature = httpCtx.Features.Get<IHttpRequestFeature>()!;

				await CreateRequestAsync(
					requestFeature,
					stream,
					CancellationToken.None).ConfigureAwait(false);

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

	private static async Task CreateRequestAsync(IHttpRequestFeature requestFeature, Stream stream, CancellationToken cancellationToken)
	{
		var firstLine = await stream.ReadLineAsync(cancellationToken).ConfigureAwait(false);
		var parts = firstLine.Split(' ');
		//requestFeature.Headers.Host = "localhost";

		requestFeature.Method = parts[0];
		var uri = new Uri(parts[1], UriKind.RelativeOrAbsolute);
		if (!uri.IsAbsoluteUri)
		{
			uri = new Uri(_LocalhostUri, uri);
		}

		requestFeature.Protocol = parts[2];
		for (; ; )
		{
			var line = await stream.ReadLineAsync(cancellationToken).ConfigureAwait(false);
			if (line.Length == 0)
			{
				break;
			}

			(var name, var values) = HttpParser.ParseHeaderNameValues(line);
			requestFeature.Headers.Add(name, new Microsoft.Extensions.Primitives.StringValues(values.ToArray()));
		}

		requestFeature.Scheme = uri.Scheme;
		requestFeature.Path = PathString.FromUriComponent(uri);
		requestFeature.QueryString = QueryString.FromUriComponent(uri).Value ?? string.Empty;
		requestFeature.Body = new BodyStream(stream, requestFeature.Headers.ContentLength);
	}
}
