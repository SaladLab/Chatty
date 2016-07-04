using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
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

        public UserTableWorker(ClusterNodeContext context, Config config)
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
        private ChannelType _channelType;
        private IPEndPoint _listenEndPoint;
        private GatewayRef _gateway;

        public UserWorker(ClusterNodeContext context, Config config)
            : base(context)
        {
            _channelType = (ChannelType)Enum.Parse(typeof(ChannelType), config.GetString("type", "Tcp"));
            _listenEndPoint = new IPEndPoint(IPAddress.Any, config.GetInt("port", 0));
        }

        public override async Task Start()
        {
            // create UserTableContainer

            _userContainer = Context.System.ActorOf(
                Props.Create(() => new DistributedActorTableContainer<string>("User", Context.ClusterActorDiscovery, null, null, InterfacedPoisonPill.Instance)),
                "UserTableContainer");
            Context.UserTableContainer = new DistributedActorTableContainerRef<string>(_userContainer, TimeSpan.FromSeconds(10));

            // create gateway for users to connect to

            if (_listenEndPoint.Port != 0)
            {
                var serializer = PacketSerializer.CreatePacketSerializer();

                var name = "UserGateway";
                var initiator = new GatewayInitiator
                {
                    ListenEndPoint = _listenEndPoint,
                    GatewayLogger = LogManager.GetLogger(name),
                    CreateChannelLogger = (ep, _) => LogManager.GetLogger($"Channel({ep})"),
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

                _gateway = (_channelType == ChannelType.Tcp)
                    ? Context.System.ActorOf(Props.Create(() => new TcpGateway(initiator)), name).Cast<GatewayRef>()
                    : Context.System.ActorOf(Props.Create(() => new UdpGateway(initiator)), name).Cast<GatewayRef>();
                await _gateway.Start();
            }
        }

        public override async Task Stop()
        {
            // stop gateway

            if (_gateway != null)
            {
                await _gateway.Stop();
                await _gateway.CastToIActorRef().GracefulStop(TimeSpan.FromSeconds(10), new Identify(0));
            }

            // stop user container

            await _userContainer.GracefulStop(TimeSpan.FromSeconds(10), PoisonPill.Instance);
        }
    }

    [RoleWorker("RoomTable")]
    public class RoomTableWorker : ClusterRoleWorker
    {
        private IActorRef _roomTable;

        public RoomTableWorker(ClusterNodeContext context, Config config)
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
        }
    }

    [RoleWorker("Room")]
    public class RoomWorker : ClusterRoleWorker
    {
        private IActorRef _roomContainer;
        private ChannelType _channelType;
        private IPEndPoint _listenEndPoint;
        private IPEndPoint _connectEndPoint;
        private GatewayRef _gateway;

        public RoomWorker(ClusterNodeContext context, Config config)
            : base(context)
        {
            _channelType = (ChannelType)Enum.Parse(typeof(ChannelType), config.GetString("type", "Tcp"));
            _listenEndPoint = new IPEndPoint(IPAddress.Any, config.GetInt("port", 0));
            _connectEndPoint = new IPEndPoint(IPAddress.Parse(config.GetString("address", "127.0.0.1")), config.GetInt("port", 0));
        }

        public override async Task Start()
        {
            // create RoomTableContainer

            _roomContainer = Context.System.ActorOf(
                Props.Create(() => new DistributedActorTableContainer<string>(
                    "Room", Context.ClusterActorDiscovery, typeof(RoomActorFactory), new object[] { Context }, InterfacedPoisonPill.Instance)),
                "RoomTableContainer");

            // create a gateway for users to join room

            if (_connectEndPoint.Port != 0)
            {
                var serializer = PacketSerializer.CreatePacketSerializer();

                var name = "RoomGateway";
                var initiator = new GatewayInitiator
                {
                    ListenEndPoint = _listenEndPoint,
                    ConnectEndPoint = _connectEndPoint,
                    GatewayLogger = LogManager.GetLogger(name),
                    TokenRequired = true,
                    TokenTimeout = TimeSpan.FromMinutes(1),
                    CreateChannelLogger = (ep, _) => LogManager.GetLogger($"Channel({ep})"),
                    ConnectionSettings = new TcpConnectionSettings { PacketSerializer = serializer },
                    PacketSerializer = serializer,
                };

                _gateway = (_channelType == ChannelType.Tcp)
                    ? Context.System.ActorOf(Props.Create(() => new TcpGateway(initiator)), name).Cast<GatewayRef>()
                    : Context.System.ActorOf(Props.Create(() => new UdpGateway(initiator)), name).Cast<GatewayRef>();
                await _gateway.Start();
            }
        }

        public override async Task Stop()
        {
            // stop gateway

            await _gateway.Stop();
            await _gateway.CastToIActorRef().GracefulStop(TimeSpan.FromSeconds(10), new Identify(0));

            // stop room container

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

    [RoleWorker("BotTable")]
    public class BotTableWorker : ClusterRoleWorker
    {
        private IActorRef _botTable;

        public BotTableWorker(ClusterNodeContext context, Config config)
            : base(context)
        {
        }

        public override Task Start()
        {
            _botTable = Context.System.ActorOf(
                Props.Create(() => new DistributedActorTable<long>("Bot", Context.ClusterActorDiscovery, typeof(IncrementalIntegerIdGenerator), null)),
                "BotTable");

            return Task.FromResult(true);
        }

        public override async Task Stop()
        {
            await _botTable.GracefulStop(
                TimeSpan.FromMinutes(1),
                new DistributedActorTableMessage<long>.GracefulStop(InterfacedPoisonPill.Instance));
        }
    }

    [RoleWorker("Bot")]
    public class BotWorker : ClusterRoleWorker
    {
        private IActorRef _botContainer;

        public BotWorker(ClusterNodeContext context, Config config)
            : base(context)
        {
        }

        public override Task Start()
        {
            _botContainer = Context.System.ActorOf(
                Props.Create(() => new DistributedActorTableContainer<long>(
                    "Bot", Context.ClusterActorDiscovery, typeof(BotActorFactory), new object[] { Context }, InterfacedPoisonPill.Instance)),
                "BotTableContainer");

            return Task.FromResult(true);
        }

        public override async Task Stop()
        {
            await _botContainer.GracefulStop(TimeSpan.FromSeconds(10), PoisonPill.Instance);
        }
    }

    public class BotActorFactory : IActorFactory
    {
        private ClusterNodeContext _clusterContext;

        public void Initialize(object[] args)
        {
            _clusterContext = (ClusterNodeContext)args[0];
        }

        public IActorRef CreateActor(IActorRefFactory actorRefFactory, object id, object[] args)
        {
            return actorRefFactory.ActorOf(Props.Create(() =>
                new BotActor(_clusterContext, "bot" + (long)id, (string)args[0], (Type)args[1])));
        }
    }
}
