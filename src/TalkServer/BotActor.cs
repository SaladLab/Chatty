using System;
using System.Net;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Interfaced;
using Akka.Interfaced.SlimServer;
using Common.Logging;
using Domain;

namespace TalkServer
{
    public class BotActor : InterfacedActor, IActorBoundChannelSync, IUserEventObserverAsync, IRoomObserverAsync, IBotService
    {
        private readonly ILog _log;
        private readonly ClusterNodeContext _clusterContext;
        private BotPattern.Context _patternContext;
        private BotPattern _pattern;
        private UserRef _user;
        private OccupantRef _occupant;
        private ICancelable _timerCancelable;

        internal class StartMessage
        {
            public string UserId;
            public string RoomName;
            public Type PatternType;
        }

        internal class TimerMessage
        {
        }

        public BotActor(ClusterNodeContext clusterContext, string name, string roomName, Type patternType)
        {
            _log = LogManager.GetLogger($"Bot({name})");
            _clusterContext = clusterContext;
            Self.Tell(new StartMessage { UserId = name, RoomName = roomName, PatternType = patternType });
        }

        protected override void PostStop()
        {
            ((IBotService)this).RemoveTimer();

            if (_user != null)
                _user.CastToIActorRef().Tell(InterfacedPoisonPill.Instance);

            base.PostStop();
        }

        [MessageHandler, Reentrant]
        private async Task Handle(StartMessage m)
        {
            if (_user != null)
                throw new InvalidOperationException("Already started");

            // create bot pattern

            _patternContext = new BotPattern.Context
            {
                UserId = m.UserId,
                RoomName = m.RoomName,
                Service = this
            };
            _pattern = (BotPattern)Activator.CreateInstance(m.PatternType, new object[] { _patternContext });

            // login by itself

            var userLogin = Context.ActorOf(Props.Create(() => new UserLoginActor(_clusterContext, Self.Cast<ActorBoundChannelRef>(), new IPEndPoint(IPAddress.None, 0))))
                                   .Cast<UserLoginRef>().WithRequestWaiter(this);
            await userLogin.Login(m.UserId, m.UserId, CreateObserver<IUserEventObserver>());

            // enter room

            await _user.EnterRoom(m.RoomName, CreateObserver<IRoomObserver>());

            // start bot

            await _pattern.OnStart();
        }

        [MessageHandler]
        private Task Handle(TimerMessage m)
        {
            return (_pattern != null) ? _pattern.OnTimer() : Task.FromResult(true);
        }

        void IActorBoundChannelSync.SetTag(object tag)
        {
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

        async Task IBotService.SayAsync(string message)
        {
            if (_occupant != null)
                await _occupant.Say(message, _patternContext.UserId);
        }

        async Task<bool> IBotService.WhisperToAsync(string targetUserId, string message)
        {
            if (_clusterContext.UserTable == null)
                return false;

            IActorRef targetUser;
            try
            {
                var reply = await _clusterContext.UserTable.Get(targetUserId);
                targetUser = reply.Actor;
            }
            catch (Exception)
            {
                return false;
            }

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

        void IBotService.SetTimer(TimeSpan duration)
        {
            ((IBotService)this).RemoveTimer();
            _timerCancelable = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(duration, duration, Self, new TimerMessage(), Self);
        }

        void IBotService.RemoveTimer()
        {
            if (_timerCancelable != null)
            {
                _timerCancelable.Cancel();
                _timerCancelable = null;
            }
        }

        void IBotService.Kill()
        {
            Self.Tell(InterfacedPoisonPill.Instance);
        }
    }
}
