using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Linq;
using Domain;
using Newtonsoft.Json.Linq;

namespace TalkServer
{
    public interface IBotService
    {
        Task SayAsync(string message);
        Task<bool> WhisperToAsync(string targetUserId, string message);
        void SetTimer(TimeSpan duration);
        void RemoveTimer();
        void Kill();
    }

    public abstract class BotPattern
    {
        public class Context
        {
            public string UserId;
            public string RoomName;
            public IBotService Service;
        }

        protected Context _context;

        protected BotPattern(Context context)
        {
            _context = context;
        }

        public virtual Task OnStart()
        {
            return Task.FromResult(true);
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

    public class ClockBot : BotPattern
    {
        public ClockBot(Context context)
            : base(context)
        {
            context.Service.SetTimer(TimeSpan.FromSeconds(10));
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
            if (chatItem.Message.ToLower() == "kill")
                _context.Service.Kill();

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
            if (chatItem.UserId.StartsWith("bot"))
                return;

            if (chatItem.Message.Contains("bot?"))
                await SayAsync($"Yes I'm a bot.");
        }
    }

    public class WikipediaBot : BotPattern
    {
        public WikipediaBot(Context context)
            : base(context)
        {
        }

        public override Task OnWhisper(ChatItem chatItem)
        {
            if (chatItem.Message.ToLower() == "kill")
                _context.Service.Kill();

            return Task.FromResult(0);
        }

        public override async Task OnSay(ChatItem chatItem)
        {
            if (chatItem.UserId.StartsWith("bot"))
                return;

            if (chatItem.Message.StartsWith("?"))
            {
                var query = chatItem.Message.Substring(1).Trim();
                if (query.Length > 0)
                {
                    try
                    {
                        using (var client = new HttpClient())
                        {
                            var ret = await client.GetStringAsync("https://en.wikipedia.org/w/api.php?action=query&format=json&prop=pageterms&titles=" + Uri.EscapeUriString(query));
                            var json = JObject.Parse(ret);
                            var answer = json.Descendants().FirstOrDefault(t => t.Type == JTokenType.Property && ((JProperty)t).Name == "description");
                            if (answer != null)
                            {
                                var desc = ((JArray)((JProperty)answer).Value)[0];
                                await SayAsync($"{query}: {desc}");
                            }
                            else
                            {
                                await SayAsync($"{query}: Not found");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        await SayAsync($"{query}: Error {e.Message}");
                    }
                }
            }
        }
    }

    public class GameBot : BotPattern
    {
        private int[] _numbers;

        public GameBot(Context context)
            : base(context)
        {
        }

        private Task NewGameAsync()
        {
            CreateNumbers(3);
            return _context.Service.SayAsync($"Let's do Bulls and Cows! Length={_numbers.Length}");
        }

        private void CreateNumbers(int count)
        {
            var rand = new Random();
            _numbers = Enumerable.Range(1, 9).OrderBy(i => rand.Next()).Take(count).ToArray();
        }

        public override Task OnWhisper(ChatItem chatItem)
        {
            if (chatItem.Message.ToLower() == "kill")
                _context.Service.Kill();

            return Task.FromResult(0);
        }

        public override Task OnStart()
        {
            return NewGameAsync();
        }

        public override Task OnEnter(string userId)
        {
            return SayAsync($"Hello {userId}! Play game with me!");
        }

        public override async Task OnSay(ChatItem chatItem)
        {
            if (chatItem.UserId.StartsWith("bot"))
                return;

            if (chatItem.Message.Length == _numbers.Length && chatItem.Message.All(c => char.IsDigit(c)))
            {
                var inputs = chatItem.Message.Select(c => Convert.ToInt32(new string(c, 1))).ToArray();
                var bulls = 0;
                var cows = 0;
                for (int i = 0; i < inputs.Length; i++)
                {
                    for (int j = 0; j < _numbers.Length; j++)
                    {
                        if (inputs[i] == _numbers[j])
                        {
                            if (i == j)
                                bulls += 1;
                            else
                                cows += 1;
                        }
                    }
                }
                if (bulls < _numbers.Length)
                {
                    await SayAsync($"{chatItem.UserId}: {bulls} bull and {cows} cows.");
                }
                else
                {
                    await SayAsync($"{chatItem.UserId}: Correct! You Win!");
                    await NewGameAsync();
                }
            }
        }
    }
}
