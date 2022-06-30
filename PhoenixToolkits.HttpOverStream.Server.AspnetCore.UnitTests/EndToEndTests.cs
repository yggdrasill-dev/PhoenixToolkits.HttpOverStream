using System.Net.Http.Json;
using System.Reflection;
using HttpOverStream.Client;
using HttpOverStream.NamedPipe;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace PhoenixToolkits.HttpOverStream.Server.AspnetCore.UnitTests;

public class PersonMessage
{
	public string Name { get; set; } = default!;
}

public class WelcomeMessage
{
	public string Text { get; set; } = default!;
}

[Route("api/e2e-tests")]
public class EndToEndApiController : ControllerBase
{
	[HttpGet("hello-world")]
	public string HelloWorld()
	{
		return "Hello World";
	}

	[HttpPost("hello")]
	public WelcomeMessage Hello([FromBody] PersonMessage person)
	{
		return new WelcomeMessage { Text = $"Hello {person.Name}" };
	}
}

[TestClass]
public class EndToEndTests
{
	[TestMethod]
	public async Task TestHelloWorld()
	{
		var builder = WebApplication.CreateBuilder();

		builder.Services
			.AddControllers()
			.AddApplicationPart(Assembly.GetExecutingAssembly());

		builder.WebHost
			.UseHttpOverStreamServer(new NamedPipeListener("test-core-get"));

		var host = builder.Build();

		host.MapControllers();

		await host.StartAsync();

		var client = new HttpClient(new DialMessageHandler(new NamedPipeDialer("test-core-get")));
		var result = await client.GetStringAsync("http://localhost/api/e2e-tests/hello-world");

		Assert.AreEqual("Hello World", result);

		await host.StopAsync();
	}

	[TestMethod]
	public async Task TestHelloPost()
	{
		var builder = WebApplication.CreateBuilder();

		builder.Services
			.AddControllers()
			.AddApplicationPart(Assembly.GetExecutingAssembly());

		builder.WebHost
			.UseHttpOverStreamServer(new NamedPipeListener("test-core-post"));

		var host = builder.Build();

		host.MapControllers();

		await host.StartAsync();

		var client = new HttpClient(new DialMessageHandler(new NamedPipeDialer("test-core-post")));
		var result = await client.PostAsJsonAsync(
			"http://localhost/api/e2e-tests/hello",
			new PersonMessage { Name = "Test" });

		var wlcMsg = await result.Content.ReadFromJsonAsync<WelcomeMessage>();

		Assert.AreEqual("Hello Test", wlcMsg?.Text);

		await host.StopAsync();
	}
}
