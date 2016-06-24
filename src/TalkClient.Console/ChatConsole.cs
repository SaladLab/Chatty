using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Interfaced.SlimSocket.Client;
using Domain;

namespace TalkClient.Console
{
    internal class ChatConsole : IUserEventObserver
    {
        private IChannel _channel;
        private UserRef _user;
        private UserEventObserver _userEventObserver;
        private Dictionary<string, Tuple<OccupantRef, RoomObserver>> _roomMap =
            new Dictionary<string, Tuple<OccupantRef, RoomObserver>>();
        private string _currentRoomName;

        public async Task RunAsync(IChannel channel, string userId, string password)
        {
            _channel = channel;

            ConsoleUtil.Out("[ Chatty.Console ]");
            ConsoleUtil.Out("");

            OnCommandShowHelp();
            ConsoleUtil.Out("");

            if (await LoginAsync(userId, password) == false)
                return;

            await OnCommandEnterRoom("", "#general");
            await ChatLoopAsync();
        }

        private async Task<bool> LoginAsync(string userId, string password)
        {
            var userLogin = _channel.CreateRef<UserLoginRef>();
            var observer = _channel.CreateObserver<IUserEventObserver>(this);

            try
            {
                _user = (UserRef)(await userLogin.Login(userId, password, observer));
                _userEventObserver = (UserEventObserver)observer;
                ConsoleUtil.Sys($"{userId} logined.");
                return true;
            }
            catch (Exception e)
            {
                _channel.RemoveObserver(observer);
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
                        await _roomMap[_currentRoomName].Item1.Say(line);
                    }
                }
            }
        }

        private async Task OnCommandEnterRoom(params string[] words)
        {
            try
            {
                var name = words[1];
                var info = await EnterRoomAsync(name);
                ConsoleUtil.Sys($"entered {name}");

                if (info.History != null)
                {
                    foreach (var chatItem in info.History.Skip(Math.Max(0, info.History.Count() - 5)))
                        ConsoleUtil.Out($"[{name}] {chatItem.UserId}: {chatItem.Message}");
                }
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
                var occupant = _roomMap[_currentRoomName].Item1;
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

        private void OnCommandShowHelp()
        {
            ConsoleUtil.Out("Commands:");
            ConsoleUtil.Out("  /e channel   Enter channel                   (alias: /enter /j /join)");
            ConsoleUtil.Out("  /x [channel] Exit from (current) channel     (alias: /exit /l /leave)");
            ConsoleUtil.Out("  /c [channel] Show or set current channel     (alias: /current)");
            ConsoleUtil.Out("  /i user      Invite user                     (alias: /invite)");
            ConsoleUtil.Out("  /w user msg  Whisper to user                 (alias: /whisper)");
            ConsoleUtil.Out("  /q           Quit                            (alias: /quit)");
        }

        private Task<string> ReadLineAsync()
        {
            // When use plain "System.Console.ReadLine" in async loop,
            // it stops TcpConnection from receiving data so that
            // we cannot read any chat message while reading console.
            // To avoid this problem we read console in another thread in ThreadPool.
            var tcs = new TaskCompletionSource<string>();
            ThreadPool.QueueUserWorkItem(_ => { tcs.SetResult(System.Console.ReadLine()); });
            return tcs.Task;
        }

        private async Task<RoomInfo> EnterRoomAsync(string name)
        {
            var observer = _channel.CreateObserver<IRoomObserver>(new RoomConsole(name));
            try
            {
                var ret = await _user.EnterRoom(name, observer);
                _roomMap.Add(name, Tuple.Create((OccupantRef)ret.Item1, (RoomObserver)observer));
                _currentRoomName = name;
                return ret.Item2;
            }
            catch (Exception)
            {
                _channel.RemoveObserver(observer);
                throw;
            }
        }

        private async Task ExitRoomAsync(string name)
        {
            Tuple<OccupantRef, RoomObserver> item;
            if (_roomMap.TryGetValue(name, out item) == false)
                throw new Exception("Cannot find room: " + name);

            await _user.ExitFromRoom(name);
            _channel.RemoveObserver(item.Item2);
            _roomMap.Remove(name);
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
