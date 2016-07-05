using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Interfaced;
using Akka.Interfaced.SlimServer;
using Akka.Interfaced.TestKit;
using Akka.TestKit;
using Domain;

namespace TalkServer
{
    public class MockClient : IUserEventObserver
    {
        private ClusterNodeContext _clusterContext;
        private UserEventObserver _userEventObserver;

        public TestActorBoundChannel Channel { get; private set; }
        public ActorBoundChannelRef ChannelRef { get; private set; }
        public UserLoginRef UserLogin { get; private set; }
        public string UserId { get; private set; }
        public UserRef User { get; private set; }
        public List<string> UserLog { get; private set; } = new List<string>();
        public Dictionary<string, RoomObserverLog> RoomLogs { get; private set; } = new Dictionary<string, RoomObserverLog>();

        public class RoomObserverLog : IRoomObserver
        {
            public List<string> Log = new List<string>();

            void IRoomObserver.Enter(string userId)
            {
                Log.Add($"Enter({userId})");
            }

            void IRoomObserver.Exit(string userId)
            {
                Log.Add($"Exit({userId})");
            }

            void IRoomObserver.Say(ChatItem chatItem)
            {
                Log.Add($"Say({chatItem.UserId}:{chatItem.Message})");
            }
        }

        public MockClient(ClusterNodeContext clusterContex)
        {
            _clusterContext = clusterContex;

            var channel = new TestActorRef<TestActorBoundChannel>(
                _clusterContext.System,
                 Props.Create(() => new TestActorBoundChannel(CreateInitialActor)));
            Channel = channel.UnderlyingActor;
            ChannelRef = channel.Cast<ActorBoundChannelRef>();

            UserLogin = Channel.CreateRef<UserLoginRef>();
        }

        private Tuple<IActorRef, TaggedType[], ActorBindingFlags>[] CreateInitialActor(IActorContext context) =>
            new[]
            {
                Tuple.Create(
                    context.ActorOf(Props.Create(() =>
                        new UserLoginActor(_clusterContext, context.Self.Cast<ActorBoundChannelRef>(), new IPEndPoint(IPAddress.None, 0)))),
                    new TaggedType[] { typeof(IUserLogin) },
                    (ActorBindingFlags)0)
            };

        public async Task<UserRef> LoginAsync(string id, string password)
        {
            if (User != null)
                throw new InvalidOperationException("Already logined");

            _userEventObserver = (UserEventObserver)Channel.CreateObserver<IUserEventObserver>(this);

            var ret = await UserLogin.Login(id, password, _userEventObserver);
            User = (UserRef)ret;
            UserId = id;
            return User;
        }

        public IRoomObserver CreateRoomObserver(string roomName)
        {
            var log = new RoomObserverLog();
            RoomLogs[roomName] = log;
            return Channel.CreateObserver((IRoomObserver)log);
        }

        void IUserEventObserver.Invite(string invitorUserId, string roomName)
        {
            UserLog.Add($"Invite({invitorUserId}, {roomName})");
        }

        void IUserEventObserver.Whisper(ChatItem chatItem)
        {
            UserLog.Add($"Whisper({chatItem.UserId}, {chatItem.Message})");
        }

        public static async Task<MockClient[]> PrepareLoginedClients(ClusterNodeContext context, int count)
        {
            var clients = new List<MockClient>();
            for (var i = 0; i < count; i++)
            {
                var client = new MockClient(context);
                await client.LoginAsync("test" + i, "1234");
                clients.Add(client);
            }
            return clients.ToArray();
        }
    }
}
