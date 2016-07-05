using System;
using System.Threading.Tasks;
using Akka.TestKit.Xunit2;
using Domain;
using Xunit;
using Xunit.Abstractions;

namespace TalkServer
{
    public class RoomActorTest : TestKit, IClassFixture<RedisStorageFixture>, IClassFixture<ClusterContextFixture>
    {
        private ClusterNodeContext _clusterContext;

        public RoomActorTest(ITestOutputHelper output, ClusterContextFixture clusterContext)
            : base(output: output)
        {
            clusterContext.Initialize(Sys);
            _clusterContext = clusterContext.Context;
        }

        private Task<MockClient[]> PrepareLoginedClients(int count) =>
            MockClient.PrepareLoginedClients(_clusterContext, count);

        [Fact]
        public async Task EnterRoom()
        {
            // Arrange
            var clients = await PrepareLoginedClients(1);

            // Act
            var ret = await clients[0].User.EnterRoom("room", clients[0].CreateRoomObserver("room"));
            await ret.Item1.Say("Message");

            // Assert
            Assert.Equal("room", ret.Item2.Name);
            Assert.Equal(new[] { "Say(test0:Message)" },
                         clients[0].RoomLogs["room"].Log);
        }

        [Fact]
        public async Task LeaveRoom()
        {
            // Arrange
            var clients = await PrepareLoginedClients(1);

            // Act
            var ret = await clients[0].User.EnterRoom("room", clients[0].CreateRoomObserver("room"));
            var room = clients[0].Channel.GetBoundActorRef((OccupantRef)ret.Item1);
            await clients[0].User.ExitFromRoom("room");

            // Assert
            Watch(room);
            ExpectTerminated(room);
        }

        [Fact]
        public async Task EnterAndChatAndExit_ObservedByOtherOccupant()
        {
            // Arrange
            var clients = await PrepareLoginedClients(2);

            // Act
            var ret0 = await clients[0].User.EnterRoom("room", clients[0].CreateRoomObserver("room"));
            var ret1 = await clients[1].User.EnterRoom("room", clients[1].CreateRoomObserver("room"));
            await ret1.Item1.Say("Message");
            await clients[1].User.ExitFromRoom("room");

            // Assert
            Assert.Equal(new[] { "Enter(test1)", "Say(test1:Message)", "Exit(test1)" },
                         clients[0].RoomLogs["room"].Log);
        }

        [Fact]
        public async Task Invite_Succeed()
        {
            // Arrange
            var clients = await PrepareLoginedClients(2);

            // Act
            var ret0 = await clients[0].User.EnterRoom("room", clients[0].CreateRoomObserver("room"));
            await ret0.Item1.Invite(clients[1].UserId);
            await Task.Delay(100);

            // Assert
            Assert.Equal(new[] { "Invite(test0, room)" },
                         clients[1].UserLog);
        }

        [Fact]
        public async Task Invite_AlreadyHere_Fail()
        {
            // Arrange
            var clients = await PrepareLoginedClients(2);

            // Act
            var ret0 = await clients[0].User.EnterRoom("room", clients[0].CreateRoomObserver("room"));
            var ret1 = await clients[1].User.EnterRoom("room", clients[1].CreateRoomObserver("room"));
            var exception = await Record.ExceptionAsync(() => ret0.Item1.Invite(clients[1].UserId));

            // Assert
            Assert.IsType<ResultException>(exception);
            Assert.Equal(ResultCodeType.UserAlreadyHere, ((ResultException)exception).ResultCode);
        }
    }
}
