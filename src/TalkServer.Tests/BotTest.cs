using System;
using System.Threading.Tasks;
using System.Linq;
using Akka.TestKit.Xunit2;
using Xunit;
using Xunit.Abstractions;

namespace TalkServer
{
    public class BotTest : TestKit, IClassFixture<RedisStorageFixture>, IClassFixture<ClusterContextFixture>
    {
        private ClusterNodeContext _clusterContext;

        public BotTest(ITestOutputHelper output, ClusterContextFixture clusterContext)
            : base(output: output)
        {
            clusterContext.Initialize(Sys);
            _clusterContext = clusterContext.Context;
        }

        private Task<MockClient[]> PrepareLoginedClients(int count) =>
            MockClient.PrepareLoginedClients(_clusterContext, count);

        [Fact]
        public async Task CreateBot()
        {
            // Arrange
            var clients = await PrepareLoginedClients(1);

            // Act
            var ret = await clients[0].User.EnterRoom("room", clients[0].CreateRoomObserver("room"));
            await clients[0].User.CreateBot("room", "clock");
            await Task.Delay(100);
            await ret.Item1.Say("bot?");
            await Task.Delay(100);

            // Assert
            Assert.NotNull(clients[0].RoomLogs["room"].Log.FirstOrDefault(s => s.Contains("Yes I'm a bot.")));
        }

        [Fact]
        public async Task KillBot()
        {
            // Arrange
            var clients = await PrepareLoginedClients(1);

            // Act
            var ret = await clients[0].User.EnterRoom("room", clients[0].CreateRoomObserver("room"));
            await clients[0].User.CreateBot("room", "clock");
            await Task.Delay(100);
            var bot = await _clusterContext.BotTable.Get((await _clusterContext.BotTable.GetIds()).Ids[0]);
            await clients[0].User.Whisper("bot" + bot.Id, "kill");

            // Assert
            Watch(bot.Actor);
            ExpectTerminated(bot.Actor);
        }
    }
}
