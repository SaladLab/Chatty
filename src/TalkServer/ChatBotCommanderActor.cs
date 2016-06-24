using System;
using System.Collections.Generic;
using Akka.Actor;
using Akka.Interfaced;
using Common.Logging;

namespace TalkServer
{
    public class ChatBotCommanderMessage
    {
        public class Start
        {
        }

        public class Stop
        {
        }
    }

    public class ChatBotCommanderActor : InterfacedActor
    {
        private ILog _logger = LogManager.GetLogger("ChatBotCommander");
        private ClusterNodeContext _clusterContext;
        private HashSet<IActorRef> _botSet = new HashSet<IActorRef>();
        private bool _isStopped;

        public ChatBotCommanderActor(ClusterNodeContext clusterContext)
        {
            _clusterContext = clusterContext;
        }

        [MessageHandler]
        private void Handle(ChatBotCommanderMessage.Start m)
        {
            // waits for a while until system is fully initialized.

            if (_clusterContext.UserTable == null ||
                _clusterContext.RoomTable == null)
            {
                Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(1), Self, m, Self);
                return;
            }

            // make one bot

            var chatBot = Context.ActorOf(Props.Create(() => new ChatBotActor(_clusterContext, "bot1")));
            chatBot.Tell(new ChatBotMessage.Start { UserId = "bot1", RoomName = "#bot" });
            Context.Watch(chatBot);
            _botSet.Add(chatBot);
        }

        [MessageHandler]
        private void Handle(ChatBotCommanderMessage.Stop m)
        {
            if (_isStopped)
                return;

            _logger.Info("Stop");
            _isStopped = true;

            // stop all running bots

            if (_botSet.Count > 0)
            {
                Context.ActorSelection("*").Tell(new ChatBotMessage.Stop());
            }
            else
            {
                Self.Tell(InterfacedPoisonPill.Instance);
            }
        }

        [MessageHandler]
        private void Handle(Terminated m)
        {
            _botSet.Remove(m.ActorRef);

            if (_isStopped && _botSet.Count == 0)
                Self.Tell(InterfacedPoisonPill.Instance);
        }
    }
}
