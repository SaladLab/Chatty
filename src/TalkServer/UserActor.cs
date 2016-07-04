using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster.Utility;
using Akka.Interfaced;
using Akka.Interfaced.LogFilter;
using Akka.Interfaced.SlimServer;
using Common.Logging;
using Domain;

namespace TalkServer
{
    [Log]
    [ResponsiveException(typeof(ResultException))]
    public class UserActor : InterfacedActor, IUser, IUserMessasing
    {
        private ILog _logger;
        private ClusterNodeContext _clusterContext;
        private ActorBoundChannelRef _channel;
        private string _id;
        private UserEventObserver _eventObserver;
        private Dictionary<string, RoomRef> _enteredRoomMap;

        public UserActor(ClusterNodeContext clusterContext, ActorBoundChannelRef channel,
                         string id, IUserEventObserver observer)
        {
            _logger = LogManager.GetLogger($"UserActor({id})");
            _clusterContext = clusterContext;
            _channel = channel;
            _id = id;
            _eventObserver = (UserEventObserver)observer;
            _enteredRoomMap = new Dictionary<string, RoomRef>();
        }

        protected override void PostStop()
        {
            UnlinkAll();
            base.PostStop();
        }

        private void UnlinkAll()
        {
            foreach (var room in _enteredRoomMap.Values)
                room.WithNoReply().Exit(_id);
            _enteredRoomMap.Clear();
        }

        Task<string> IUser.GetId()
        {
            return Task.FromResult(_id);
        }

        async Task<IList<string>> IUser.GetRoomList()
        {
            try
            {
                var reply = await _clusterContext.RoomTable.GetIds();
                return reply.Ids?.ToList();
            }
            catch (Exception e)
            {
                _logger.Warn($"Failed in querying room list from RoomTable.", e);
                throw new ResultException(ResultCodeType.InternalError);
            }
        }

        async Task<Tuple<IOccupant, RoomInfo>> IUser.EnterRoom(string name, IRoomObserver observer)
        {
            if (_enteredRoomMap.ContainsKey(name))
                throw new ResultException(ResultCodeType.NeedToBeOutOfRoom);

            // Try to get room ref

            IActorRef roomRef;
            try
            {
                var reply = await _clusterContext.RoomTable.GetOrCreate(name, null);
                roomRef = reply.Actor;
            }
            catch (Exception e)
            {
                _logger.Warn($"Failed in querying room from RoomTable. (Name={name})", e);
                throw new ResultException(ResultCodeType.InternalError);
            }

            if (roomRef == null)
                throw new ResultException(ResultCodeType.RoomRemoved);

            var room = roomRef.Cast<RoomRef>().WithRequestWaiter(this);

            // Let's enter the room !

            var info = await room.Enter(_id, observer);

            // Bind an room actor to channel

            BoundActorTarget boundActor = null;
            if (_id.StartsWith("bot"))
            {
                boundActor = await _channel.BindActor(
                    room.CastToIActorRef(), new[] { new TaggedType(typeof(IOccupant), _id) });
            }
            else
            {
                var roomGatewayPath = room.CastToIActorRef().Path.Root / "user" / "RoomGateway";
                var roomGateway = await Context.System.ActorSelection(roomGatewayPath).ResolveOne(TimeSpan.FromSeconds(1));
                boundActor = await roomGateway.Cast<ActorBoundGatewayRef>().OpenChannel(
                    room.CastToIActorRef(), new[] { new TaggedType(typeof(IOccupant), _id) },
                    _id, ActorBindingFlags.OpenThenNotification | ActorBindingFlags.CloseThenNotification);
            }

            if (boundActor == null)
            {
                await room.Exit(_id);
                _logger.Error($"Failed in binding Occupant");
                throw new ResultException(ResultCodeType.InternalError);
            }

            _enteredRoomMap[name] = room;
            return Tuple.Create((IOccupant)boundActor.Cast<OccupantRef>(), info);
        }

        async Task IUser.ExitFromRoom(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ResultException(ResultCodeType.NeedToBeInRoom);

            RoomRef room;
            if (_enteredRoomMap.TryGetValue(name, out room) == false)
                throw new ResultException(ResultCodeType.NeedToBeInRoom);

            // Let's exit from the room !

            await room.Exit(_id);

            // Unbind an room actor from channel

            _channel.WithNoReply().UnbindActor(room.CastToIActorRef());

            _enteredRoomMap.Remove(name);
        }

        async Task IUser.Whisper(string targetUserId, string message)
        {
            if (targetUserId == _id)
                throw new ResultException(ResultCodeType.UserNotMyself);

            // Try to get target user ref

            if (_clusterContext.UserTable == null)
                throw new ResultException(ResultCodeType.UserNotOnline);

            IActorRef targetUser;
            try
            {
                var reply = await _clusterContext.UserTable.Get(targetUserId);
                targetUser = reply.Actor;
            }
            catch (Exception e)
            {
                _logger.Warn($"Failed in querying user from UserTable. (TargetUserId={targetUserId})", e);
                throw new ResultException(ResultCodeType.InternalError);
            }

            if (targetUser == null)
                throw new ResultException(ResultCodeType.UserNotOnline);

            // Whisper to target user

            targetUser.Cast<UserMessasingRef>().WithNoReply().Whisper(new ChatItem
            {
                UserId = _id,
                Time = DateTime.UtcNow,
                Message = message
            });
        }

        Task IUser.CreateBot(string roomName, string botType)
        {
            if (string.IsNullOrEmpty(roomName))
                throw new ResultException(ResultCodeType.ArgumentError);
            if (string.IsNullOrEmpty(botType))
                throw new ResultException(ResultCodeType.ArgumentError);

            RoomRef room;
            if (_enteredRoomMap.TryGetValue(roomName, out room) == false)
                throw new ResultException(ResultCodeType.NeedToBeInRoom);

            // find bot type info

            var type = GetType().Assembly.GetTypes()
                                .Where(t => t.BaseType == typeof(BotPattern))
                                .FirstOrDefault(t => t.Name.ToLower() == botType + "bot");

            if (type == null)
                throw new ResultException(ResultCodeType.ArgumentError);

            // create bot

            if (_clusterContext.BotTable != null)
                _clusterContext.BotTable.Create(new object[] { roomName, type });

            return Task.FromResult(0);
        }

        Task IUserMessasing.Whisper(ChatItem chatItem)
        {
            _eventObserver.Whisper(chatItem);
            return Task.FromResult(0);
        }

        Task IUserMessasing.Invite(string invitorUserId, string roomName)
        {
            _eventObserver.Invite(invitorUserId, roomName);
            return Task.FromResult(0);
        }
    }
}
