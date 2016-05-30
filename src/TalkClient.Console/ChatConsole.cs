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
        private Communicator _communicator;
        private UserRef _user;
        private UserEventObserver _userEventObserver;
        private Dictionary<string, Tuple<OccupantRef, RoomObserver>> _roomMap =
            new Dictionary<string, Tuple<OccupantRef, RoomObserver>>();
        private string _currentRoomName;

        public async Task RunAsync(Communicator communicator)
        {
            _communicator = communicator;

            WriteLine("[ ChatConsole ]");

            await LoginAsync();
            WriteLine("# logined");

            await EnterRoomAsync("#general");
            WriteLine("# entered #general channel");

            await ChatLoopAsync();
        }

        private async Task LoginAsync()
        {
            var userLogin = _communicator.CreateRef<UserLoginRef>();
            var observer = _communicator.CreateObserver<IUserEventObserver>(this);

            try
            {
                _user = (UserRef)(await userLogin.Login("console", "1234", observer));
                _userEventObserver = (UserEventObserver)observer;
            }
            catch (Exception)
            {
                observer.Dispose();
                throw;
            }
        }

        private async Task ChatLoopAsync()
        {
            OnCommandShowHelp();
            WriteLine("");

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

                        default:
                            WriteLine("Invalid command: " + words[0]);
                            break;
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(_currentRoomName))
                    {
                        WriteLine("Need a room to say");
                    }
                    else
                    {
                        await _roomMap[_currentRoomName].Item1.Say(line);
                    }
                }
            }
        }

        private async Task OnCommandEnterRoom(string[] words)
        {
            try
            {
                await EnterRoomAsync(words[1]);
            }
            catch (Exception e)
            {
                WriteLine("Failed to join: " + e);
            }
        }

        private async Task OnCommandExitFromRoom(string[] words)
        {
            try
            {
                var room = words.Length > 1 ? words[1] : _currentRoomName;
                if (string.IsNullOrEmpty(room))
                {
                    WriteLine("No room to exit.");
                    return;
                }
                await ExitRoomAsync(room);
                if (_roomMap.ContainsKey(_currentRoomName) == false)
                    _currentRoomName = _roomMap.Keys.FirstOrDefault();
            }
            catch (Exception e)
            {
                WriteLine("Failed to leave:" + e);
            }
        }

        private void OnCommandCurrentChannel(string[] words)
        {
            if (words.Length > 1)
            {
                var roomName = words[1];
                if (_roomMap.ContainsKey(roomName))
                {
                    _currentRoomName = roomName;
                    WriteLine($"Current room is changed to {_currentRoomName}");
                }
                else
                {
                    WriteLine($"No room");
                }
            }
            else
            {
                WriteLine($"Current room: {_currentRoomName}");
            }
        }

        private async Task OnCommandInviteUser(string[] words)
        {
            if (string.IsNullOrEmpty(_currentRoomName))
            {
                WriteLine("Need a room to invite");
            }
            else
            {
                var occupant = _roomMap[_currentRoomName].Item1;
                for (var i = 1; i < words.Length; i++)
                {
                    WriteLine("Invite: " + words[i]);
                    try
                    {
                        await occupant.Invite(words[i]);
                    }
                    catch (Exception e)
                    {
                        WriteLine("Failed to invite: " + e);
                    }
                }
            }
        }

        private async Task OnCommandWhisper(string[] words)
        {
            if (words.Length >= 3)
            {
                try
                {
                    var targetUser = words[1];
                    var message = string.Join(" ", words.Skip(2));
                    await _user.Whisper(targetUser, message);
                }
                catch (Exception e)
                {
                    WriteLine("Failed to whisper: " + e);
                }
            }
        }

        private void OnCommandShowHelp()
        {
            WriteLine("Commands:");
            WriteLine("  /e channel   Enter channel                   (alias: /enter /j /join)");
            WriteLine("  /x [channel] Exit from (current) channel     (alias: /exit /l /leave)");
            WriteLine("  /c [channel] Show or set current channel     (alias: /current)");
            WriteLine("  /i user      Invite user                     (alias: /invite)");
            WriteLine("  /w user msg  Whisper to user                 (alias: /whisper)");
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

        private void WriteLine(string str)
        {
            System.Console.WriteLine(str);
        }

        private async Task<RoomInfo> EnterRoomAsync(string name)
        {
            var observer = _communicator.CreateObserver<IRoomObserver>(new RoomConsole(name));
            try
            {
                var ret = await _user.EnterRoom(name, observer);
                _roomMap.Add(name, Tuple.Create((OccupantRef)ret.Item1, (RoomObserver)observer));
                _currentRoomName = name;
                return ret.Item2;
            }
            catch (Exception)
            {
                observer.Dispose();
                throw;
            }
        }

        private async Task ExitRoomAsync(string name)
        {
            Tuple<OccupantRef, RoomObserver> item;
            if (_roomMap.TryGetValue(name, out item) == false)
                throw new Exception("Cannot find room: " + name);

            await _user.ExitFromRoom(name);
            item.Item2.Dispose();
            _roomMap.Remove(name);
        }

        void IUserEventObserver.Invite(string invitorUserId, string roomName)
        {
            WriteLine($"<Invite> [invitorUserId] invites you to {roomName}");
        }

        void IUserEventObserver.Whisper(ChatItem chatItem)
        {
            WriteLine($"<Whisper> {chatItem.UserId}: {chatItem.Message}");
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
                WriteLine($"[{_name}] {userId} Entered");
            }

            public void Exit(string userId)
            {
                WriteLine($"[{_name}] {userId} Exited");
            }

            public void Say(ChatItem chatItem)
            {
                WriteLine($"[{_name}] {chatItem.UserId}: {chatItem.Message}");
            }

            private void WriteLine(string str)
            {
                System.Console.WriteLine(str);
            }
        }
    }
}
