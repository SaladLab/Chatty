using System;
using System.Net;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Interfaced;
using Common.Logging;
using Domain;

namespace TalkServer
{
    public static class ChatBotMessage
    {
        public class Start
        {
            public string UserId;
            public string RoomName;
        }

        public class Stop
        {
        }
    }

    public class ChatBotActor : InterfacedActor, IUserEventObserver, IRoomObserver
    {
        private readonly ILog _log;
        private readonly ClusterNodeContext _clusterContext;
        private string _userId;
        private UserRef _user;
        private OccupantRef _occupant;
        private bool _stopped;

        public ChatBotActor(ClusterNodeContext clusterContext, string name)
        {
            _log = LogManager.GetLogger($"Bot({name})");
            _clusterContext = clusterContext;
        }

        [MessageHandler, Reentrant]
        private async Task Handle(ChatBotMessage.Start m)
        {
            if (_user != null)
                throw new InvalidOperationException("Already started");

            _userId = m.UserId;

            // login by itself

            var userLoginActor = Context.ActorOf(Props.Create(
                () => new UserLoginActor(_clusterContext, Self, new IPEndPoint(IPAddress.Loopback, 0))));
            var userLogin = new UserLoginRef(userLoginActor, this, null);
            await userLogin.Login(_userId, m.UserId, CreateObserver<IUserEventObserver>());

            // enter room

            await _user.EnterRoom(m.RoomName, CreateObserver<IRoomObserver>());

            // chat !

            while (_stopped == false)
            {
                await _occupant.Say(DateTime.Now.ToString(), _userId);
                await Task.Delay(1000);
            }

            // outro

            await _user.ExitFromRoom(m.RoomName);

            _user.Actor.Tell(new ActorBoundSessionMessage.SessionTerminated());

            Context.Stop(Self);
        }

        [MessageHandler]
        private void Handle(ChatBotMessage.Stop m)
        {
            _stopped = true;
        }

        [MessageHandler]
        private void Handle(ActorBoundSessionMessage.Bind m)
        {
            if (m.InterfaceType == typeof(IUser))
            {
                _user = new UserRef(m.Actor, this, null);
                Sender.Tell(new ActorBoundSessionMessage.BindReply(1));
                return;
            }

            if (m.InterfaceType == typeof(IOccupant))
            {
                _occupant = new OccupantRef(m.Actor, this, null);
                Sender.Tell(new ActorBoundSessionMessage.BindReply(1));
                return;
            }

            _log.ErrorFormat("Unexpected bind type. (InterfaceType={0}, Actor={1})",
                             m.InterfaceType?.FullName, m.Actor);
        }

        void IUserEventObserver.Whisper(ChatItem chatItem)
        {
        }

        void IUserEventObserver.Invite(string invitorUserId, string roomName)
        {
        }

        void IRoomObserver.Enter(string userId)
        {
        }

        void IRoomObserver.Exit(string userId)
        {
        }

        void IRoomObserver.Say(ChatItem chatItem)
        {
        }
    }
}
