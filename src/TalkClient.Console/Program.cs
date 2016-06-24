using System;
using System.Net;
using Akka.Interfaced.SlimSocket;
using Akka.Interfaced.SlimSocket.Client;
using Common.Logging;
using TypeAlias;

namespace TalkClient.Console
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            // create channel

            var channelFactory = ChannelFactoryBuilder.Build<DomainProtobufSerializer>(
                endPoint: new IPEndPoint(IPAddress.Loopback, 9001),
                createChannelLogger: () => LogManager.GetLogger("Channel"));
            channelFactory.Type = channelType;
            var channel = channelFactory.Create();

            /*
            var serializer = new PacketSerializer(
                new PacketSerializerBase.Data(
                    new ProtoBufMessageSerializer(PacketSerializer.CreateTypeModel()),
                    new TypeAliasTable()));

            var communicator = new Communicator(LogManager.GetLogger("Communicator"),
                                                new IPEndPoint(IPAddress.Loopback, 9001),
                                                _ => new TcpConnection(serializer, LogManager.GetLogger("Connection")));
            communicator.Start();

            var userId = args.Length > 0 ? args[0] : "console";
            var password = args.Length > 1 ? args[1] : userId;

            var driver = new ChatConsole();
            driver.RunAsync(communicator, userId, password).Wait();
            */
        }
    }
}
