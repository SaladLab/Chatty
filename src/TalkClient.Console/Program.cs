using System.Net;
using Akka.Interfaced.SlimSocket;
using Akka.Interfaced.SlimSocket.Client;
using Common.Logging;

namespace TalkClient.Console
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var channelFactory = new ChannelFactory
            {
                Type = ChannelType.Tcp,
                ConnectEndPoint = new IPEndPoint(IPAddress.Loopback, 9001),
                CreateChannelLogger = () => LogManager.GetLogger("Connection"),
                PacketSerializer = PacketSerializer.CreatePacketSerializer()
            };

            var channel = channelFactory.Create();
            var userId = args.Length > 0 ? args[0] : "console";
            var password = args.Length > 1 ? args[1] : userId;

            var driver = new ChatConsole();
            driver.RunAsync(channel, userId, password).Wait();
        }
    }
}
