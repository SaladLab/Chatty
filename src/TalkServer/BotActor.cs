using System;
using System.Net;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Interfaced;
using Akka.Interfaced.SlimServer;
using Common.Logging;
using Domain;

namespace TalkServer
{
    public class BotActor : InterfacedActor, IUserEventObserverAsync, IRoomObserverAsync, IBotService
    {
        private readonly ILog _log;
        private readonly ClusterNodeContext _clusterContext;
        private ActorBoundChannelRef _channel;
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
            _channel = Context.InterfacedActorOf(() => new ActorBoundDummyChannel()).Cast<ActorBoundChannelRef>();

            Self.Tell(new StartMessage { UserId = name, RoomName = roomName, PatternType = patternType });
        }

        protected override void PostStop()
        {
            ((IBotService)this).RemoveTimer();

            if (_user != null)
                _user.CastToIActorRef().Tell(InterfacedPoisonPill.Instance);

            _channel.WithNoReply().Close();

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

            await LoginUser(m.UserId);
            if (_user == null)
                throw new InvalidOperationException();

            // enter room

            var roomInfo = await _user.EnterRoom(m.RoomName, CreateObserver<IRoomObserver>());
            _occupant = ((OccupantRef)roomInfo.Item1).WithRequestWaiter(this);

            // start bot

            await _pattern.OnStart();
        }

        private async Task LoginUser(string userId)
        {
            IActorRef user;
            try
            {
                var observer = CreateObserver<IUserEventObserver>();
                user = Context.System.ActorOf(
                    Props.Create(() => new UserActor(_clusterContext, userId, observer)),
                    "user_" + userId);
            }
            catch (Exception e)
            {
                _log.Error("Failed to create user.", e);
                return;
            }

            var registered = false;
            for (int i = 0; i < 10; i++)
            {
                var reply = await _clusterContext.UserTableContainer.Add(userId, user);
                if (reply.Added)
                {
                    registered = true;
                    break;
                }
                await Task.Delay(200);
            }

            if (registered == false)
            {
                _log.Error("Failed to register user.");
                user.Tell(InterfacedPoisonPill.Instance);
                return;
            }

            await _channel.BindActor(user, new TaggedType[] { typeof(IUser) }, ActorBindingFlags.OpenThenNotification);
            _user = user.Cast<UserRef>().WithRequestWaiter(this);
        }

        [MessageHandler]
        private Task Handle(TimerMessage m)
        {
            return (_pattern != null) ? _pattern.OnTimer() : Task.FromResult(true);
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
