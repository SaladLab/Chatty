using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Interfaced.SlimSocket.Client;
using Domain;
using Akka.Interfaced;

namespace TalkClient.Console
{
    internal class ChatConsole : IUserEventObserver
    {
        private Communicator _comm;
        private CancellationTokenSource _channelCloseCts;
        private UserRef _user;
        private UserEventObserver _userEventObserver;

        private class RoomItem
        {
            public OccupantRef Occupant;
            public RoomObserver Observer;
            public IChannel Channel;
        }

        private Dictionary<string, RoomItem> _roomMap = new Dictionary<string, RoomItem>();
        private string _currentRoomName;

        public async Task RunAsync(Communicator commnuicator, string userId, string password)
        {
            _comm = commnuicator;

            var channel = _comm.CreateChannel();

            ConsoleUtil.Out("[ Chatty.Console ]");
            ConsoleUtil.Out("");

            ConsoleUtil.Out("Try to connect...");
            try
            {
                await channel.ConnectAsync();
            }
            catch (Exception e)
            {
                System.Console.WriteLine("Failed to connect: " + e.Message);
                return;
            }

            ConsoleUtil.Out("Connected!");
            ConsoleUtil.Out("");

            OnCommandShowHelp();
            ConsoleUtil.Out("");

            try
            {
                if (await LoginAsync(userId, password) == false)
                    return;

                await OnCommandEnterRoom("", "#general");
                await ChatLoopAsync();
            }
            catch (TaskCanceledException)
            {
                System.Console.WriteLine("Disconnected!");
                return;
            }
        }

        private async Task<bool> LoginAsync(string userId, string password)
        {
            var userLogin = _comm.Channels[0].CreateRef<UserLoginRef>();
            var observer = _comm.ObserverRegistry.Create<IUserEventObserver>(this);

            try
            {
                _user = (UserRef)(await userLogin.Login(userId, password, observer));
                if (_user.IsChannelConnected() == false)
                    await _user.ConnectChannelAsync();
                _userEventObserver = (UserEventObserver)observer;
                _comm.Channels.Last().StateChanged += (_, state) => { if (state == ChannelStateType.Closed) _channelCloseCts?.Cancel(); };
                ConsoleUtil.Sys($"{userId} logined.");
                return true;
            }
            catch (Exception e)
            {
                _comm.ObserverRegistry.Remove(observer);
                ConsoleUtil.Err($"Failed to login {userId} with " + e);
                return false;
            }
        }

        private async Task ChatLoopAsync()
        {
            while (true)
            {
                var line = await ReadLineAsync();
                var trimmedLine = line.Trim();

                if (trimmedLine.StartsWith("/"))
                {
                    var words = trimmedLine.Split();
                    switch (words[0].ToLower())
                    {
                        case "/e":
                        case "/enter":
                        case "/j":
                        case "/join":
                            await OnCommandEnterRoom(words);
                            break;

                        case "/x":
                        case "/exit":
                        case "/l":
                        case "/leave":
                            await OnCommandExitFromRoom(words);
                            break;

                        case "/c":
                        case "/current":
                            OnCommandCurrentChannel(words);
                            break;

                        case "/i":
                        case "/invite":
                            await OnCommandInviteUser(words);
                            break;

                        case "/w":
                        case "/whisper":
                            await OnCommandWhisper(words);
                            break;

                        case "/b":
                        case "/bot":
                            await OnCommandBot(words);
                            break;

                        case "/?":
                        case "/h":
                        case "/help":
                            OnCommandShowHelp();
                            break;

                        case "/q":
                        case "/quit":
                            ConsoleUtil.Sys("Bye!");
                            return;

                        default:
                            ConsoleUtil.Err("Invalid command: " + words[0]);
                            break;
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(_currentRoomName))
                    {
                        ConsoleUtil.Err("Need a room to say");
                    }
                    else
                    {
                        try
                        {
                            await _roomMap[_currentRoomName].Occupant.Say(line);
                        }
                        catch (RequestChannelException)
                        {
                            ConsoleUtil.Err("Failed to say because channel is closed. Leave this room.");
                            await OnCommandExitFromRoom();
                        }
                        catch (Exception e)
                        {
                            ConsoleUtil.Err("Failed to say: " + e);
                        }
                    }
                }
            }
        }

        private async Task OnCommandEnterRoom(params string[] words)
        {
            try
            {
                var name = words[1];
                var ret = await EnterRoomAsync(name);
                ConsoleUtil.Sys($"entered {name}");

                if (ret.Item1.History != null)
                {
                    foreach (var chatItem in ret.Item1.History.Skip(Math.Max(0, ret.Item1.History.Count() - 5)))
                        ConsoleUtil.Out($"[{name}] {chatItem.UserId}: {chatItem.Message}");
                }

                ret.Item2.GetEventDispatcher().Pending = false;
            }
            catch (Exception e)
            {
                ConsoleUtil.Err("Failed to join: " + e);
            }
        }

        private async Task OnCommandExitFromRoom(params string[] words)
        {
            try
            {
                var room = words.Length > 1 ? words[1] : _currentRoomName;
                if (string.IsNullOrEmpty(room))
                {
                    ConsoleUtil.Err("No room to exit.");
                    return;
                }
                await ExitRoomAsync(room);
                ConsoleUtil.Sys($"exited from {room}");
                if (_roomMap.ContainsKey(_currentRoomName) == false)
                    _currentRoomName = _roomMap.Keys.FirstOrDefault();
            }
            catch (Exception e)
            {
                ConsoleUtil.Err("Failed to leave:" + e);
            }
        }

        private void OnCommandCurrentChannel(params string[] words)
        {
            if (words.Length > 1)
            {
                var roomName = words[1];
                if (_roomMap.ContainsKey(roomName))
                {
                    _currentRoomName = roomName;
                    ConsoleUtil.Sys($"Current room is changed to {_currentRoomName}");
                }
                else
                {
                    ConsoleUtil.Err($"No room");
                }
            }
            else
            {
                ConsoleUtil.Sys($"Current room: {_currentRoomName}");
            }
        }

        private async Task OnCommandInviteUser(params string[] words)
        {
            if (string.IsNullOrEmpty(_currentRoomName))
            {
                ConsoleUtil.Err("Need a room to invite");
            }
            else
            {
                var occupant = _roomMap[_currentRoomName].Occupant;
                for (var i = 1; i < words.Length; i++)
                {
                    try
                    {
                        await occupant.Invite(words[i]);
                        ConsoleUtil.Sys("Invite: " + words[i]);
                    }
                    catch (Exception e)
                    {
                        ConsoleUtil.Err("Failed to invite: " + e);
                    }
                }
            }
        }

        private async Task OnCommandWhisper(params string[] words)
        {
            if (words.Length >= 3)
            {
                try
                {
                    var targetUser = words[1];
                    var message = string.Join(" ", words.Skip(2));
                    await _user.Whisper(targetUser, message);
                    ConsoleUtil.Sys($"Whisper to {targetUser}");
                }
                catch (Exception e)
                {
                    ConsoleUtil.Err("Failed to whisper: " + e);
                }
            }
            else
            {
                ConsoleUtil.Sys("Not enough parameters");
            }
        }

        private async Task OnCommandBot(params string[] words)
        {
            if (string.IsNullOrEmpty(_currentRoomName))
            {
                ConsoleUtil.Err("Need a room to create a bot");
                return;
            }

            if (words.Length >= 2)
            {
                try
                {
                    var botType = words[1];
                    await _user.CreateBot(_currentRoomName, botType);
                }
                catch (Exception e)
                {
                    ConsoleUtil.Err("Failed to create a bot: " + e);
                }
            }
            else
            {
                ConsoleUtil.Sys("Not enough parameters");
            }
        }

        private void OnCommandShowHelp()
        {
            ConsoleUtil.Out("Commands:");
            ConsoleUtil.Out("  /e channel   Enter channel                   (alias: /enter /j /join)");
            ConsoleUtil.Out("  /x [channel] Exit from (current) channel     (alias: /exit /l /leave)");
            ConsoleUtil.Out("  /c [channel] Show or set current channel     (alias: /current)");
            ConsoleUtil.Out("  /i user      Invite user                     (alias: /invite)");
            ConsoleUtil.Out("  /w user msg  Whisper to user                 (alias: /whisper)");
            ConsoleUtil.Out("  /b type      Create bot (of type)            (alias: /bot)");
            ConsoleUtil.Out("  /q           Quit                            (alias: /quit)");
        }

        private Task<string> ReadLineAsync()
        {
            // When use plain "System.Console.ReadLine" in async loop,
            // it stops TcpConnection from receiving data so that
            // we cannot read any chat message while reading console.
            // To avoid this problem we read console in another thread in ThreadPool.
            var tcs = new TaskCompletionSource<string>();
            _channelCloseCts = new CancellationTokenSource();
            _channelCloseCts.Token.Register(() => tcs.TrySetCanceled());
            ThreadPool.QueueUserWorkItem(_ => { tcs.TrySetResult(System.Console.ReadLine()); });
            return tcs.Task;
        }

        private async Task<Tuple<RoomInfo, IRoomObserver>> EnterRoomAsync(string name)
        {
            var observer = _comm.ObserverRegistry.Create<IRoomObserver>(new RoomConsole(name));
            observer.GetEventDispatcher().Pending = true;
            observer.GetEventDispatcher().KeepingOrder = true;
            try
            {
                var ret = await _user.EnterRoom(name, observer);
                var occupant = (OccupantRef)ret.Item1;
                if (occupant.IsChannelConnected() == false)
                    await occupant.ConnectChannelAsync();
                _roomMap.Add(name, new RoomItem
                {
                    Occupant = occupant,
                    Observer = (RoomObserver)observer,
                    Channel = (IChannel)occupant.RequestWaiter
                });
                _currentRoomName = name;
                return Tuple.Create(ret.Item2, observer);
            }
            catch (Exception)
            {
                _comm.ObserverRegistry.Remove(observer);
                throw;
            }
        }

        private async Task ExitRoomAsync(string name)
        {
            RoomItem item;
            if (_roomMap.TryGetValue(name, out item) == false)
                throw new Exception("Cannot find room: " + name);

            await _user.ExitFromRoom(name);

            _comm.ObserverRegistry.Remove(item.Observer);
            _roomMap.Remove(name);

            if (item.Channel != _comm.Channels[0])
                item.Channel.Close();
        }

        void IUserEventObserver.Invite(string invitorUserId, string roomName)
        {
            ConsoleUtil.Sys($"<Invite> {invitorUserId} invites you to {roomName}");
        }

        void IUserEventObserver.Whisper(ChatItem chatItem)
        {
            ConsoleUtil.Sys($"<Whisper> {chatItem.UserId}: {chatItem.Message}");
        }

        private class RoomConsole : IRoomObserver
        {
            private string _name;

            public RoomConsole(string name)
            {
                _name = name;
            }

            public void Enter(string userId)
            {
                ConsoleUtil.Out($"[{_name}] {userId} Entered");
            }

            public void Exit(string userId)
            {
                ConsoleUtil.Out($"[{_name}] {userId} Exited");
            }

            public void Say(ChatItem chatItem)
            {
                ConsoleUtil.Out($"[{_name}] {chatItem.UserId}: {chatItem.Message}");
            }
        }
    }
}
