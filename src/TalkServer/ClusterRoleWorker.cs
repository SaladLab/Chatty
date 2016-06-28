using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration.Hocon;
using Akka.Cluster.Utility;
using Akka.Interfaced;
using Akka.Interfaced.SlimServer;
using Akka.Interfaced.SlimSocket;
using Akka.Interfaced.SlimSocket.Server;
using Common.Logging;
using Domain;

namespace TalkServer
{
    public abstract class ClusterRoleWorker
    {
        public ClusterNodeContext Context { get; }

        public ClusterRoleWorker(ClusterNodeContext context)
        {
            Context = context;
        }

        public abstract Task Start();
        public abstract Task Stop();
    }

    [AttributeUsage(System.AttributeTargets.Class)]
    public class RoleWorkerAttribute : Attribute
    {
        public string Role { get; set; }

        public RoleWorkerAttribute(string role)
        {
            Role = role;
        }
    }

    [RoleWorker("UserTable")]
    public class UserTableWorker : ClusterRoleWorker
    {
        private IActorRef _userTable;

        public UserTableWorker(ClusterNodeContext context, HoconObject config)
            : base(context)
        {
        }

        public override Task Start()
        {
            _userTable = Context.System.ActorOf(
                Props.Create(() => new DistributedActorTable<string>("User", Context.ClusterActorDiscovery, null, null)),
                "UserTable");
            return Task.FromResult(true);
        }

        public override async Task Stop()
        {
            await _userTable.GracefulStop(
                TimeSpan.FromMinutes(1),
                new DistributedActorTableMessage<string>.GracefulStop(InterfacedPoisonPill.Instance));
        }
    }

    [RoleWorker("User")]
    public class UserWorker : ClusterRoleWorker
    {
        private IActorRef _userContainer;
        private List<ChannelType> _channelTypes;
        private IPEndPoint _listenEndPoint;
        private List<GatewayRef> _gateways;

        public UserWorker(ClusterNodeContext context, HoconObject config)
            : base(context)
        {
            _channelTypes = new List<ChannelType> { ChannelType.Tcp, ChannelType.Udp };
            _listenEndPoint = new IPEndPoint(IPAddress.Any, config.GetKey("port")?.GetInt() ?? 0);
        }

        public override async Task Start()
        {
            // create UserTableContainer

            _userContainer = Context.System.ActorOf(
                Props.Create(() => new DistributedActorTableContainer<string>("User", Context.ClusterActorDiscovery, null, null)),
                "UserTableContainer");
            Context.UserTableContainer = _userContainer;

            // create gateway for users to connect to

            _gateways = new List<GatewayRef>();
            if (_listenEndPoint.Port != 0)
            {
                var serializer = PacketSerializer.CreatePacketSerializer();

                foreach (var channelType in _channelTypes)
                {
                    var name = $"UserGateway({channelType})";
                    var initiator = new GatewayInitiator
                    {
                        ListenEndPoint = _listenEndPoint,
                        GatewayLogger = LogManager.GetLogger(name),
                        CreateChannelLogger = (ep, _) => LogManager.GetLogger($"Channel({ep}"),
                        ConnectionSettings = new TcpConnectionSettings { PacketSerializer = serializer },
                        PacketSerializer = serializer,
                        CreateInitialActors = (context, connection) => new[]
                        {
                        Tuple.Create(
                            context.ActorOf(Props.Create(() =>
                                new UserLoginActor(Context, context.Self.Cast<ActorBoundChannelRef>(), GatewayInitiator.GetRemoteEndPoint(connection)))),
                            new TaggedType[] { typeof(IUserLogin) },
                            (ActorBindingFlags)0)
                    }
                    };

                    var gateway = (channelType == ChannelType.Tcp)
                        ? Context.System.ActorOf(Props.Create(() => new TcpGateway(initiator)), name).Cast<GatewayRef>()
                        : Context.System.ActorOf(Props.Create(() => new UdpGateway(initiator)), name).Cast<GatewayRef>();

                    await gateway.Start();

                    _gateways.Add(gateway);
                }
            }
        }

        public override async Task Stop()
        {
            // stop gateways

            foreach (var gateway in _gateways)
            {
                await gateway.Stop();
                await gateway.CastToIActorRef().GracefulStop(TimeSpan.FromSeconds(10), new Identify(0));
            }

            // stop user container

            await _userContainer.GracefulStop(TimeSpan.FromSeconds(10), PoisonPill.Instance);
        }
    }

    [RoleWorker("RoomTable")]
    public class RoomTableWorker : ClusterRoleWorker
    {
        private IActorRef _roomTable;

        public RoomTableWorker(ClusterNodeContext context, HoconObject config)
            : base(context)
        {
        }

        public override Task Start()
        {
            _roomTable = Context.System.ActorOf(
                Props.Create(() => new DistributedActorTable<string>("Room", Context.ClusterActorDiscovery, null, null)),
                "RoomTable");
            return Task.FromResult(true);
        }

        public override async Task Stop()
        {
            await _roomTable.GracefulStop(
                TimeSpan.FromMinutes(1),
                new DistributedActorTableMessage<string>.GracefulStop(InterfacedPoisonPill.Instance));
            _roomTable = null;
        }
    }

    [RoleWorker("Room")]
    public class RoomWorker : ClusterRoleWorker
    {
        private IActorRef _roomContainer;

        public RoomWorker(ClusterNodeContext context, HoconObject config)
            : base(context)
        {
        }

        public override Task Start()
        {
            // create RoomTableContainer

            _roomContainer = Context.System.ActorOf(
                Props.Create(() => new DistributedActorTableContainer<string>(
                    "Room", Context.ClusterActorDiscovery, typeof(RoomActorFactory), new object[] { Context })),
                "RoomTableContainer");
            Context.RoomTableContainer = _roomContainer;

            return Task.FromResult(true);
        }

        public override async Task Stop()
        {
            await _roomContainer.GracefulStop(TimeSpan.FromSeconds(10), PoisonPill.Instance);
        }
    }

    public class RoomActorFactory : IActorFactory
    {
        private ClusterNodeContext _clusterContext;

        public void Initialize(object[] args)
        {
            _clusterContext = (ClusterNodeContext)args[0];
        }

        public IActorRef CreateActor(IActorRefFactory actorRefFactory, object id, object[] args)
        {
            return actorRefFactory.ActorOf(Props.Create(() => new RoomActor(_clusterContext, (string)id)));
        }
    }

    [RoleWorker("Bot")]
    public class BotWorker : ClusterRoleWorker
    {
        private IActorRef _botCommander;

        public BotWorker(ClusterNodeContext context, HoconObject config)
            : base(context)
        {
        }

        public override Task Start()
        {
            // create commander actor

            _botCommander = Context.System.ActorOf(
                Props.Create(() => new ChatBotCommanderActor(Context)), "ChatBotCommander");
            _botCommander.Tell(new ChatBotCommanderMessage.Start());

            return Task.FromResult(true);
        }

        public override async Task Stop()
        {
            await _botCommander.GracefulStop(TimeSpan.FromSeconds(10), new ChatBotCommanderMessage.Stop());
        }
    }
}
