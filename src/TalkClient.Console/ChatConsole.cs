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
        private Dictionary<string, OccupantRef> _occupantMap = new Dictionary<string, OccupantRef>();
        private string _currentRoomName;

        public async Task RunAsync(Communicator communicator)
        {
            _communicator = communicator;

            WriteLine("[ ChatConsole ]");

            _user = await LoginAsync();
            WriteLine("# logined");

            await EnterRoomAsync("#general");
            WriteLine("# entered #general channel");

            await ChatLoopAsync();
        }

        private async Task<UserRef> LoginAsync()
        {
            var userLogin = _communicator.CreateRef<UserLoginRef>();
            var observer = _communicator.CreateObserver<IUserEventObserver>(this);

            try
            {
                return (UserRef)(await userLogin.Login("console", "1234", observer));
            }
            catch (Exception)
            {
                observer.Dispose();
                throw;
            }
        }

        private async Task ChatLoopAsync()
        {
            ShowCommandHelp();
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
                            try
                            {
                                await EnterRoomAsync(words[1]);
                            }
                            catch (Exception e)
                            {
                                WriteLine("Failed to join: " + e);
                            }
                            break;

                        case "/x":
                        case "/exit":
                        case "/l":
                        case "/leave":
                            try
                            {
                                var leaveRoom = words.Length > 1 ? words[1] : _currentRoomName;
                                await ExitRoomAsync(leaveRoom);
                                _occupantMap.Remove(leaveRoom);
                                if (_occupantMap.ContainsKey(_currentRoomName) == false)
                                    _currentRoomName = _occupantMap.Keys.FirstOrDefault();
                            }
                            catch (Exception e)
                            {
                                WriteLine("Failed to leave:" + e);
                            }
                            break;

                        case "/c":
                        case "/current":
                            if (words.Length > 1)
                            {
                                var roomName = words[1];
                                if (_occupantMap.ContainsKey(roomName))
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
                            break;

                        case "/i":
                        case "/invite":
                            if (string.IsNullOrEmpty(_currentRoomName))
                            {
                                WriteLine("Need a room to invite");
                            }
                            else
                            {
                                var occupant = _occupantMap[_currentRoomName];
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
                            break;

                        case "/w":
                        case "/whisper":
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
                            break;

                        case "/?":
                        case "/h":
                        case "/help":
                            ShowCommandHelp();
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
                        await _occupantMap[_currentRoomName].Say(line);
                    }
                }
            }
        }

        private void ShowCommandHelp()
        {
            WriteLine("Commands:");
            WriteLine("  /e channel   Enter channel                   (alias: /enter /j /join)");
            WriteLine("  /x [channel] Leave (current) channel         (alias: /exit /l /leave)");
            WriteLine("  /c [channel] Show or change current channel  (alias: /current)");
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
                _occupantMap.Add(name, (OccupantRef)ret.Item1);
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
            await _user.ExitFromRoom(name);
            // TODO: Remove Observer
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
