using System;
using System.Net;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster.Utility;
using Akka.Interfaced;
using Akka.Interfaced.SlimServer;
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

    public class ChatBotActor : InterfacedActor, IActorBoundChannelSync, IExtendedInterface<IUserEventObserver, IRoomObserver>
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

            var userLogin = Context.ActorOf(Props.Create(() => new UserLoginActor(_clusterContext, Self.Cast<ActorBoundChannelRef>(), new IPEndPoint(IPAddress.Loopback, 0))))
                                   .Cast<UserLoginRef>().WithRequestWaiter(this);
            await userLogin.Login(_userId, m.UserId, CreateObserver<IUserEventObserver>());

            // enter room

            await _user.EnterRoom(m.RoomName, CreateObserver<IRoomObserver>());

            // chat !

            while (_stopped == false)
            {
                await SayAsync(DateTime.Now.ToString());
                await Task.Delay(5000);
            }

            // outro

            await _user.ExitFromRoom(m.RoomName);

            _user.CastToIActorRef().Tell(InterfacedPoisonPill.Instance);
            Self.Tell(InterfacedPoisonPill.Instance);
        }

        [MessageHandler]
        private void Handle(ChatBotMessage.Stop m)
        {
            _stopped = true;
        }

        InterfacedActorRef IActorBoundChannelSync.BindActor(InterfacedActorRef actor, ActorBindingFlags bindingFlags)
        {
            var targetActor = ((AkkaActorTarget)actor.Target).Actor;

            var boundActor = ((IActorBoundChannelSync)this).BindActor(targetActor, new TaggedType[] { actor.InterfaceType }, bindingFlags);
            if (boundActor == null)
                return null;

            var actorRef = (InterfacedActorRef)Activator.CreateInstance(actor.GetType());
            InterfacedActorRefModifier.SetTarget(actorRef, boundActor);
            return actorRef;
        }

        BoundActorTarget IActorBoundChannelSync.BindActor(IActorRef actor, TaggedType[] types, ActorBindingFlags bindingFlags)
        {
            // this actor doesn't work as a normal channel.
            // it just hooks binding event and save those actors to use later.

            if (types[0].Type == typeof(IUser))
            {
                _user = actor.Cast<UserRef>().WithRequestWaiter(this);
                return new BoundActorTarget(0);
            }

            if (types[0].Type == typeof(IOccupant))
            {
                _occupant = actor.Cast<OccupantRef>().WithRequestWaiter(this);
                return new BoundActorTarget(0);
            }

            _log.ErrorFormat("Unexpected bind type. (InterfaceType={0}, Actor={1})",
                             types[0].Type.FullName, actor);
            return null;
        }

        bool IActorBoundChannelSync.UnbindActor(IActorRef actor)
        {
            return true;
        }

        bool IActorBoundChannelSync.BindType(IActorRef actor, TaggedType[] types)
        {
            return true;
        }

        bool IActorBoundChannelSync.UnbindType(IActorRef actor, Type[] types)
        {
            return true;
        }

        void IActorBoundChannelSync.Close()
        {
            Self.Tell(InterfacedPoisonPill.Instance);
        }

        private async Task SayAsync(string message)
        {
            if (_occupant != null)
                await _occupant.Say(message, _userId);
        }

        private async Task<bool> WhisperToAsync(string targetUserId, string message)
        {
            if (_clusterContext.UserTable == null)
                return false;

            var reply = await _clusterContext.UserTable.Ask<DistributedActorTableMessage<string>.GetReply>(
                new DistributedActorTableMessage<string>.Get(targetUserId));
            var targetUser = reply.Actor;
            if (targetUser == null)
                return false;

            targetUser.Cast<UserMessasingRef>().WithNoReply().Whisper(new ChatItem
            {
                UserId = _userId,
                Time = DateTime.UtcNow,
                Message = message
            });
            return true;
        }

        [ExtendedHandler]
        private Task Whisper(ChatItem chatItem)
        {
            return WhisperToAsync(chatItem.UserId, $"Wow you sent a whisper (length={chatItem.Message.Length})");
        }

        [ExtendedHandler]
        private Task Invite(string invitorUserId, string roomName)
        {
            return WhisperToAsync(invitorUserId, "Thanks for invitation but I cannot move.");
        }

        [ExtendedHandler]
        private Task Enter(string userId)
        {
            return SayAsync($"Hello {userId}!");
        }

        [ExtendedHandler]
        private Task Exit(string userId)
        {
            return SayAsync($"I'll miss {userId}...");
        }

        [ExtendedHandler]
        private async Task Say(ChatItem chatItem)
        {
            if (chatItem.UserId != _userId && chatItem.Message.Contains("bot?"))
                await SayAsync($"Yes I'm a bot.");
        }
    }
}
