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
            var userId = args.Length > 0 ? args[0] : "console";
            var password = args.Length > 1 ? args[1] : userId;

            var driver = new ChatConsole();
            driver.RunAsync(CreateCommunicator(), userId, password).Wait();
        }

        private static Communicator CreateCommunicator()
        {
            var communicator = new Communicator();
            var channelFactory = communicator.ChannelFactory;
            channelFactory.Type = ChannelType.Tcp;
            channelFactory.ConnectEndPoint = new IPEndPoint(IPAddress.Loopback, 9001);
            channelFactory.CreateChannelLogger = () => LogManager.GetLogger("Channel");
            channelFactory.PacketSerializer = PacketSerializer.CreatePacketSerializer();
            return communicator;
        }
    }
}
