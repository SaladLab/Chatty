using System;
using System.Threading.Tasks;
using Domain;

namespace TalkServer
{
    public interface IChatBotService
    {
        Task SayAsync(string message);
        Task<bool> WhisperToAsync(string targetUserId, string message);
        void SetTimer(TimeSpan duration);
        void RemoveTimer();
    }

    public abstract class ChatBotPattern
    {
        public class Context
        {
            public string UserId;
            public string RoomName;
            public IChatBotService Service;
        }

        protected Context _context;

        protected ChatBotPattern(Context context)
        {
            _context = context;
        }

        public virtual Task OnTimer()
        {
            return Task.FromResult(true);
        }

        public virtual Task OnInvite(string invitorUserId, string roomName)
        {
            return Task.FromResult(true);
        }

        public virtual Task OnWhisper(ChatItem chatItem)
        {
            return Task.FromResult(true);
        }

        public virtual Task OnEnter(string userId)
        {
            return Task.FromResult(true);
        }

        public virtual Task OnExit(string userId)
        {
            return Task.FromResult(true);
        }

        public virtual Task OnSay(ChatItem chatItem)
        {
            return Task.FromResult(true);
        }

        protected Task SayAsync(string message)
        {
            return _context.Service.SayAsync(message);
        }

        protected Task<bool> WhisperToAsync(string targetUserId, string message)
        {
            return _context.Service.WhisperToAsync(targetUserId, message);
        }
    }

    public class DummyBot : ChatBotPattern
    {
        public DummyBot(Context context)
            : base(context)
        {
            context.Service.SetTimer(TimeSpan.FromSeconds(5));
        }

        public override Task OnTimer()
        {
            return SayAsync("Now: " + DateTime.Now.ToString());
        }

        public override Task OnInvite(string invitorUserId, string roomName)
        {
            return WhisperToAsync(invitorUserId, "Thanks for invitation but I cannot move.");
        }

        public override Task OnWhisper(ChatItem chatItem)
        {
            return WhisperToAsync(chatItem.UserId, $"Wow you sent a whisper (length={chatItem.Message.Length})");
        }

        public override Task OnEnter(string userId)
        {
            return SayAsync($"Hello {userId}!");
        }

        public override Task OnExit(string userId)
        {
            return SayAsync($"I'll miss {userId}...");
        }

        public override async Task OnSay(ChatItem chatItem)
        {
            if (chatItem.Message.Contains("bot?"))
                await SayAsync($"Yes I'm a bot.");
        }
    }
}
