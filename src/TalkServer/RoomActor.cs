using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster.Utility;
using Akka.Interfaced;
using Akka.Interfaced.LogFilter;
using Common.Logging;
using Newtonsoft.Json;
using Domain;

namespace TalkServer
{
    [Log]
    [ResponsiveException(typeof(ResultException))]
    public class RoomActor : InterfacedActor, IRoom, IOccupant
    {
        private class UserData
        {
            public DateTime EnterTime;
            public RoomObserver Observer;
        }

        private ILog _logger;
        private ClusterNodeContext _clusterContext;
        private string _name;
        private Dictionary<string, UserData> _userMap;
        private bool _removed;
        private ImmutableList<ChatItem> _history;
        private static readonly int HistoryMax = 100;

        public RoomActor(ClusterNodeContext clusterContext, string name)
        {
            _logger = LogManager.GetLogger(string.Format("RoomActor({0})", name));
            _clusterContext = clusterContext;
            _name = name;
            _userMap = new Dictionary<string, UserData>();
        }

        private void NotifyToAllObservers(Action<RoomObserver> notifyAction)
        {
            foreach (var item in _userMap)
            {
                if (item.Value.Observer != null)
                    notifyAction(item.Value.Observer);
            }
        }

        protected override async Task OnStart(bool restarted)
        {
            await LoadAsync();
        }

        protected override async Task OnGracefulStop()
        {
            await SaveAsync();
        }

        private async Task LoadAsync()
        {
            List<ChatItem> history = null;

            var data = await RedisStorage.Db.HashGetAsync("RoomHistory", _name);
            if (data.HasValue)
            {
                try
                {
                    history = JsonConvert.DeserializeObject<List<ChatItem>>(data.ToString());
                }
                catch (Exception e)
                {
                    _logger.ErrorFormat("Error occured in loading room({0})", e, _name);
                }
            }

            _history = (history ?? new List<ChatItem>()).ToImmutableList();
        }

        private async Task SaveAsync()
        {
            try
            {
                var historyJson = JsonConvert.SerializeObject(_history);
                await RedisStorage.Db.HashSetAsync("RoomHistory", _name, historyJson);
            }
            catch (Exception e)
            {
                _logger.ErrorFormat("Error occured in saving room({0})", e, _name);
            }
        }

        async Task<RoomInfo> IRoom.Enter(string userId, IRoomObserver observer)
        {
            if (_removed)
                throw new ResultException(ResultCodeType.RoomRemoved);

            if (_userMap.ContainsKey(userId))
                throw new ResultException(ResultCodeType.NeedToBeOutOfRoom);

            NotifyToAllObservers(o => o.Enter(userId));

            _userMap[userId] = new UserData
            {
                EnterTime = DateTime.UtcNow,
                Observer = (RoomObserver)observer
            };

            return new RoomInfo
            {
                Name = _name,
                Users = _userMap.Keys.ToList(),
                History = _history
            };
        }

        async Task IRoom.Exit(string userId)
        {
            if (_userMap.ContainsKey(userId) == false)
                throw new ResultException(ResultCodeType.NeedToBeInRoom);

            _userMap.Remove(userId);

            NotifyToAllObservers(o => o.Exit(userId));

            if (_userMap.Count == 0)
            {
                // Because Enter could be invoked between this Exit and PoisonPill,
                // _removed is used for preventing user from entering the room which is stopping.

                _removed = true;

                Self.Tell(InterfacedPoisonPill.Instance);
            }
        }

        async Task IOccupant.Say(string msg, string senderUserId)
        {
            if (_userMap.ContainsKey(senderUserId) == false)
                throw new ResultException(ResultCodeType.NeedToBeInRoom);

            var chatItem = new ChatItem { Time = DateTime.UtcNow, UserId = senderUserId, Message = msg };
            _history = _history.Add(chatItem);
            if (_history.Count > HistoryMax)
                _history = _history.RemoveRange(0, _history.Count - HistoryMax);

            NotifyToAllObservers(o => o.Say(chatItem));
        }

        Task<IList<ChatItem>> IOccupant.GetHistory()
        {
            return Task.FromResult((IList<ChatItem>)_history);
        }

        async Task IOccupant.Invite(string targetUserId, string senderUserId)
        {
            if (targetUserId == senderUserId)
                throw new ResultException(ResultCodeType.UserNotMyself);

            if (_userMap.ContainsKey(targetUserId))
                throw new ResultException(ResultCodeType.UserAlreadyHere);

            var reply = await _clusterContext.UserTable.Ask<DistributedActorTableMessage<string>.GetReply>(
                new DistributedActorTableMessage<string>.Get(targetUserId));
            var targetUser = reply.Actor;
            if (targetUser == null)
                throw new ResultException(ResultCodeType.UserNotOnline);

            targetUser.Cast<UserMessasingRef>().WithNoReply().Invite(senderUserId, _name);
        }
    }
}
