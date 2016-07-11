using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Aim.ClusterNode;
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
    [ClusterRole("UserTable")]
    public class UserTableWorker : ClusterRoleWorker
    {
        private ClusterNodeContext _context;
        private IActorRef _userTable;

        public UserTableWorker(ClusterNodeContext context, Config config)
        {
            _context = context;
        }

        public override Task Start()
        {
            _userTable = _context.System.ActorOf(
                Props.Create(() => new DistributedActorTable<string>("User", _context.ClusterActorDiscovery, null, null)),
                "UserTable");
            return Task.CompletedTask;
        }

        public override async Task Stop()
        {
            await _userTable.GracefulStop(
                TimeSpan.FromMinutes(1),
                new DistributedActorTableMessage<string>.GracefulStop(InterfacedPoisonPill.Instance));
        }
    }

    [ClusterRole("User")]
    public class UserWorker : ClusterRoleWorker
    {
        private ClusterNodeContext _context;
        private IActorRef _userContainer;
        private ChannelType _channelType;
        private IPEndPoint _listenEndPoint;
        private IPEndPoint _connectEndPoint;
        private GatewayRef _gateway;

        public UserWorker(ClusterNodeContext context, Config config)
        {
            _context = context;
            _channelType = (ChannelType)Enum.Parse(typeof(ChannelType), config.GetString("type", "Tcp"));
            _listenEndPoint = new IPEndPoint(IPAddress.Any, config.GetInt("port", 0));

            var connectAddress = config.GetString("connect-address");
            var connectPort = config.GetInt("connect-port", _listenEndPoint.Port);
            _connectEndPoint = new IPEndPoint(connectAddress != null ? IPAddress.Parse(connectAddress) : IPAddress.Loopback, connectPort);
        }

        public override async Task Start()
        {
            // create UserTableContainer

            _userContainer = _context.System.ActorOf(
                Props.Create(() => new DistributedActorTableContainer<string>(
                    "User", _context.ClusterActorDiscovery, typeof(UserActorFactory), new object[] { _context }, InterfacedPoisonPill.Instance)),
                "UserTableContainer");
            _context.UserTableContainer = new DistributedActorTableContainerRef<string>(_userContainer);

            // create gateway for users to connect to

            if (_listenEndPoint.Port != 0)
            {
                var serializer = PacketSerializer.CreatePacketSerializer();

                var name = "UserGateway";
                var initiator = new GatewayInitiator
                {
                    ListenEndPoint = _listenEndPoint,
                    ConnectEndPoint = _connectEndPoint,
                    TokenRequired = true,
                    GatewayLogger = LogManager.GetLogger(name),
                    CreateChannelLogger = (ep, _) => LogManager.GetLogger($"Channel({ep})"),
                    ConnectionSettings = new TcpConnectionSettings { PacketSerializer = serializer },
                    PacketSerializer = serializer,
                };

                _gateway = (_channelType == ChannelType.Tcp)
                    ? _context.System.ActorOf(Props.Create(() => new TcpGateway(initiator)), name).Cast<GatewayRef>()
                    : _context.System.ActorOf(Props.Create(() => new UdpGateway(initiator)), name).Cast<GatewayRef>();
                await _gateway.Start();
            }
        }

        public override async Task Stop()
        {
            // stop gateway

            if (_gateway != null)
            {
                await _gateway.CastToIActorRef().GracefulStop(
                    TimeSpan.FromSeconds(10),
                    InterfacedMessageBuilder.Request<IGateway>(x => x.Stop()));
            }

            // stop user container

            await _userContainer.GracefulStop(TimeSpan.FromSeconds(10), PoisonPill.Instance);
        }
    }

    public class UserActorFactory : IActorFactory
    {
        private ClusterNodeContext _clusterContext;

        public void Initialize(object[] args)
        {
            _clusterContext = (ClusterNodeContext)args[0];
        }

        public IActorRef CreateActor(IActorRefFactory actorRefFactory, object id, object[] args)
        {
            return actorRefFactory.ActorOf(Props.Create(() => new UserActor(_clusterContext, (string)id, (IUserEventObserver)args[0])));
        }
    }

    [ClusterRole("UserLogin")]
    public class UserLoginWorker : ClusterRoleWorker
    {
        private ClusterNodeContext _context;
        private ChannelType _channelType;
        private IPEndPoint _listenEndPoint;
        private GatewayRef _gateway;

        public UserLoginWorker(ClusterNodeContext context, Config config)
        {
            _context = context;
            _channelType = (ChannelType)Enum.Parse(typeof(ChannelType), config.GetString("type", "Tcp"));
            _listenEndPoint = new IPEndPoint(IPAddress.Any, config.GetInt("port", 0));
        }

        public override async Task Start()
        {
            var serializer = PacketSerializer.CreatePacketSerializer();

            var name = "UserLoginGateway";
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
                            new UserLoginActor(_context, context.Self.Cast<ActorBoundChannelRef>(), GatewayInitiator.GetRemoteEndPoint(connection)))),
                        new TaggedType[] { typeof(IUserLogin) },
                        ActorBindingFlags.CloseThenStop | ActorBindingFlags.StopThenCloseChannel)
                }
            };

            _gateway = (_channelType == ChannelType.Tcp)
                ? _context.System.ActorOf(Props.Create(() => new TcpGateway(initiator)), name).Cast<GatewayRef>()
                : _context.System.ActorOf(Props.Create(() => new UdpGateway(initiator)), name).Cast<GatewayRef>();
            await _gateway.Start();
        }

        public override async Task Stop()
        {
            await _gateway.CastToIActorRef().GracefulStop(
                TimeSpan.FromSeconds(10),
                InterfacedMessageBuilder.Request<IGateway>(x => x.Stop()));
        }
    }

    [ClusterRole("RoomTable")]
    public class RoomTableWorker : ClusterRoleWorker
    {
        private ClusterNodeContext _context;
        private IActorRef _roomTable;

        public RoomTableWorker(ClusterNodeContext context, Config config)
        {
            _context = context;
        }

        public override Task Start()
        {
            _roomTable = _context.System.ActorOf(
                Props.Create(() => new DistributedActorTable<string>("Room", _context.ClusterActorDiscovery, null, null)),
                "RoomTable");
            return Task.CompletedTask;
        }

        public override async Task Stop()
        {
            await _roomTable.GracefulStop(
                TimeSpan.FromMinutes(1),
                new DistributedActorTableMessage<string>.GracefulStop(InterfacedPoisonPill.Instance));
        }
    }

    [ClusterRole("Room")]
    public class RoomWorker : ClusterRoleWorker
    {
        private ClusterNodeContext _context;
        private IActorRef _roomContainer;
        private ChannelType _channelType;
        private IPEndPoint _listenEndPoint;
        private IPEndPoint _connectEndPoint;
        private GatewayRef _gateway;

        public RoomWorker(ClusterNodeContext context, Config config)
        {
            _context = context;
            _channelType = (ChannelType)Enum.Parse(typeof(ChannelType), config.GetString("type", "Tcp"));
            _listenEndPoint = new IPEndPoint(IPAddress.Any, config.GetInt("port", 0));
            _connectEndPoint = new IPEndPoint(IPAddress.Parse(config.GetString("address", "127.0.0.1")), config.GetInt("port", 0));
        }

        public override async Task Start()
        {
            // create RoomTableContainer

            _roomContainer = _context.System.ActorOf(
                Props.Create(() => new DistributedActorTableContainer<string>(
                    "Room", _context.ClusterActorDiscovery, typeof(RoomActorFactory), new object[] { _context }, InterfacedPoisonPill.Instance)),
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
                    CreateChannelLogger = (ep, _) => LogManager.GetLogger($"Channel({ep})"),
                    ConnectionSettings = new TcpConnectionSettings { PacketSerializer = serializer },
                    PacketSerializer = serializer,
                };

                _gateway = (_channelType == ChannelType.Tcp)
                    ? _context.System.ActorOf(Props.Create(() => new TcpGateway(initiator)), name).Cast<GatewayRef>()
                    : _context.System.ActorOf(Props.Create(() => new UdpGateway(initiator)), name).Cast<GatewayRef>();
                await _gateway.Start();
            }
        }

        public override async Task Stop()
        {
            // stop gateway

            await _gateway.CastToIActorRef().GracefulStop(
                TimeSpan.FromSeconds(10),
                InterfacedMessageBuilder.Request<IGateway>(x => x.Stop()));

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

    [ClusterRole("BotTable")]
    public class BotTableWorker : ClusterRoleWorker
    {
        private ClusterNodeContext _context;
        private IActorRef _botTable;

        public BotTableWorker(ClusterNodeContext context, Config config)
        {
            _context = context;
        }

        public override Task Start()
        {
            _botTable = _context.System.ActorOf(
                Props.Create(() => new DistributedActorTable<long>("Bot", _context.ClusterActorDiscovery, typeof(IncrementalIntegerIdGenerator), null)),
                "BotTable");

            return Task.CompletedTask;
        }

        public override async Task Stop()
        {
            await _botTable.GracefulStop(
                TimeSpan.FromMinutes(1),
                new DistributedActorTableMessage<long>.GracefulStop(InterfacedPoisonPill.Instance));
        }
    }

    [ClusterRole("Bot")]
    public class BotWorker : ClusterRoleWorker
    {
        private ClusterNodeContext _context;
        private IActorRef _botContainer;

        public BotWorker(ClusterNodeContext context, Config config)
        {
            _context = context;
        }

        public override Task Start()
        {
            _botContainer = _context.System.ActorOf(
                Props.Create(() => new DistributedActorTableContainer<long>(
                    "Bot", _context.ClusterActorDiscovery, typeof(BotActorFactory), new object[] { _context }, InterfacedPoisonPill.Instance)),
                "BotTableContainer");

            return Task.CompletedTask;
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
