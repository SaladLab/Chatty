using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster.Utility;
using Akka.Interfaced;
using Akka.Interfaced.LogFilter;
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
        private IActorRef _clientSession;
        private string _id;
        private UserEventObserver _eventObserver;
        private Dictionary<string, RoomRef> _enteredRoomMap;

        public UserActor(ClusterNodeContext clusterContext, IActorRef clientSession, string id, IUserEventObserver observer)
        {
            _logger = LogManager.GetLogger($"UserActor({id})");
            _clusterContext = clusterContext;
            _clientSession = clientSession;
            _id = id;
            _eventObserver = (UserEventObserver)observer;
            _enteredRoomMap = new Dictionary<string, RoomRef>();
        }

        [MessageHandler]
        private void OnMessage(ActorBoundSessionMessage.SessionTerminated message)
        {
            UnlinkAll();
            Context.Stop(Self);
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

        async Task<List<string>> IUser.GetRoomList()
        {
            var reply = await _clusterContext.RoomTable.Ask<DistributedActorTableMessage<string>.GetIdsReply>(
                new DistributedActorTableMessage<string>.GetIds());
            return reply.Ids?.Select(x => (string)x).ToList();
        }

        async Task<Tuple<IOccupant, RoomInfo>> IUser.EnterRoom(string name, IRoomObserver observer)
        {
            if (_enteredRoomMap.ContainsKey(name))
                throw new ResultException(ResultCodeType.NeedToBeOutOfRoom);

            // Try to get room ref

            var reply = await _clusterContext.RoomTable.Ask<DistributedActorTableMessage<string>.GetOrCreateReply>(
                new DistributedActorTableMessage<string>.GetOrCreate(name, null));
            if (reply.Actor == null)
                throw new ResultException(ResultCodeType.RoomRemoved);

            var room = new RoomRef(reply.Actor, this, null);

            // Let's enter the room !

            var info = await room.Enter(_id, observer);

            // Bind an occupant actor with client session

            var reply2 = await _clientSession.Ask<ActorBoundSessionMessage.BindReply>(
                new ActorBoundSessionMessage.Bind(room.Actor, typeof(IOccupant), _id));

            _enteredRoomMap[name] = room;
            return Tuple.Create((IOccupant)BoundActorRef.Create<OccupantRef>(reply2.ActorId), info);
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

            // Unbind an occupant actor with client session

            _clientSession.Tell(new ActorBoundSessionMessage.Unbind(room.Actor));

            _enteredRoomMap.Remove(name);
        }

        async Task IUser.Whisper(string targetUserId, string message)
        {
            if (targetUserId == _id)
                throw new ResultException(ResultCodeType.UserNotMyself);

            if (_clusterContext.UserTable == null)
                throw new ResultException(ResultCodeType.UserNotOnline);

            var reply = await _clusterContext.UserTable.Ask<DistributedActorTableMessage<string>.GetReply>(
                new DistributedActorTableMessage<string>.Get(targetUserId));
            var targetUser = reply.Actor;
            if (targetUser == null)
                throw new ResultException(ResultCodeType.UserNotOnline);

            var chatItem = new ChatItem
            {
                UserId = _id,
                Time = DateTime.UtcNow,
                Message = message
            };

            var targetUserMessaging = new UserMessasingRef(targetUser);
            targetUserMessaging.WithNoReply().Whisper(chatItem);
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
