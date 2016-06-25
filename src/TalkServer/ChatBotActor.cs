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
            public Type PatternType;
        }

        public class Stop
        {
        }

        internal class Timer
        {
        }
    }

    public class ChatBotActor : InterfacedActor, IActorBoundChannelSync, IUserEventObserverAsync, IRoomObserverAsync, IChatBotService
    {
        private readonly ILog _log;
        private readonly ClusterNodeContext _clusterContext;
        private ChatBotPattern.Context _patternContext;
        private ChatBotPattern _pattern;
        private UserRef _user;
        private OccupantRef _occupant;
        private ICancelable _timerCancelable;
        private bool _stopped;

        public ChatBotActor(ClusterNodeContext clusterContext, string name)
        {
            _log = LogManager.GetLogger($"Bot({name})");
            _clusterContext = clusterContext;
        }

        protected override void PostStop()
        {
            ((IChatBotService)this).RemoveTimer();

            if (_user != null)
                _user.CastToIActorRef().Tell(InterfacedPoisonPill.Instance);

            base.PostStop();
        }

        [MessageHandler, Reentrant]
        private async Task Handle(ChatBotMessage.Start m)
        {
            if (_user != null)
                throw new InvalidOperationException("Already started");

            // create bot pattern

            _patternContext = new ChatBotPattern.Context
            {
                UserId = m.UserId,
                RoomName = m.RoomName,
                Service = this
            };
            _pattern = (ChatBotPattern)Activator.CreateInstance(m.PatternType, new object[] { _patternContext });

            // login by itself

            var userLogin = Context.ActorOf(Props.Create(() => new UserLoginActor(_clusterContext, Self.Cast<ActorBoundChannelRef>(), new IPEndPoint(IPAddress.Loopback, 0))))
                                   .Cast<UserLoginRef>().WithRequestWaiter(this);
            await userLogin.Login(m.UserId, m.UserId, CreateObserver<IUserEventObserver>());

            // enter room

            await _user.EnterRoom(m.RoomName, CreateObserver<IRoomObserver>());
        }

        [MessageHandler]
        private void Handle(ChatBotMessage.Stop m)
        {
            Self.Tell(InterfacedPoisonPill.Instance);
        }

        [MessageHandler]
        private Task Handle(ChatBotMessage.Timer m)
        {
            return (_pattern != null) ? _pattern.OnTimer() : Task.FromResult(true);
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
            _stopped = true;
        }

        Task IUserEventObserverAsync.Invite(string invitorUserId, string roomName)
        {
            return (_pattern != null) ? _pattern.OnInvite(invitorUserId, roomName) : Task.FromResult(true);
        }

        Task IUserEventObserverAsync.Whisper(ChatItem chatItem)
        {
            return (_pattern != null) ? _pattern.OnWhisper(chatItem) : Task.FromResult(true);
        }

        Task IRoomObserverAsync.Enter(string userId)
        {
            return (_pattern != null) ? _pattern.OnEnter(userId) : Task.FromResult(true);
        }

        Task IRoomObserverAsync.Exit(string userId)
        {
            return (_pattern != null) ? _pattern.OnExit(userId) : Task.FromResult(true);
        }

        Task IRoomObserverAsync.Say(ChatItem chatItem)
        {
            return (_pattern != null && chatItem.UserId != _patternContext.UserId) ? _pattern.OnSay(chatItem) : Task.FromResult(true);
        }

        async Task IChatBotService.SayAsync(string message)
        {
            if (_occupant != null)
                await _occupant.Say(message, _patternContext.UserId);
        }

        async Task<bool> IChatBotService.WhisperToAsync(string targetUserId, string message)
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
                UserId = _patternContext.UserId,
                Time = DateTime.UtcNow,
                Message = message
            });
            return true;
        }

        void IChatBotService.SetTimer(TimeSpan duration)
        {
            ((IChatBotService)this).RemoveTimer();
            _timerCancelable = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(duration, duration, Self, new ChatBotMessage.Timer(), Self);
        }

        void IChatBotService.RemoveTimer()
        {
            if (_timerCancelable != null)
            {
                _timerCancelable.Cancel();
                _timerCancelable = null;
            }
        }
    }
}
