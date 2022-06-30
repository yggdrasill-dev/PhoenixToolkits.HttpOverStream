using HttpOverStream;
using PhoenixToolkits.HttpOverStream.Server.AspnetCore;

namespace Microsoft.AspNetCore.Builder;

public static class WebHostBuilderExtensions
{
	public static IWebHostBuilder UseHttpOverStreamServer(this IWebHostBuilder builder, IListen listener)
		=> builder.UseServer(new CustomListenerHost(listener));
}
