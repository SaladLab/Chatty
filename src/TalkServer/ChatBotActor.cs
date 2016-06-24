﻿using System;
using System.Net;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster.Utility;
using Akka.Interfaced;
using Common.Logging;
using Domain;

namespace TalkServer
{
    /*
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

    public class ChatBotActor : InterfacedActor, IExtendedInterface<IUserEventObserver, IRoomObserver>
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

            var userLogin = Context.ActorOf(Props.Create(() => new UserLoginActor(_clusterContext, Self, new IPEndPoint(IPAddress.Loopback, 0))))
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
    */
}
