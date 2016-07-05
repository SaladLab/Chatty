using System;
using System.Threading.Tasks;
using Akka.TestKit.Xunit2;
using Domain;
using Xunit;
using Xunit.Abstractions;

namespace TalkServer
{
    public class UserActorTest : TestKit, IClassFixture<RedisStorageFixture>, IClassFixture<ClusterContextFixture>
    {
        private ClusterNodeContext _clusterContext;

        public UserActorTest(ITestOutputHelper output, ClusterContextFixture clusterContext)
            : base(output: output)
        {
            clusterContext.Initialize(Sys);
            _clusterContext = clusterContext.Context;
        }

        private Task<MockClient[]> PrepareLoginedClients(int count) =>
            MockClient.PrepareLoginedClients(_clusterContext, count);

        [Fact]
        public async Task Whisper_Succeed()
        {
            // Arrange
            var clients = await PrepareLoginedClients(2);

            // Act
            await clients[0].User.Whisper(clients[1].UserId, "Message");
            await Task.Delay(100);

            // Assert
            Assert.Equal(new[] { $"Whisper({clients[0].UserId}, Message)" }, clients[1].UserLog);
        }

        [Fact]
        public async Task Whisper_WrongTarget_Fail()
        {
            // Arrange
            var clients = await PrepareLoginedClients(1);

            // Act
            var exception = await Record.ExceptionAsync(() => clients[0].User.Whisper("None", "Message"));

            // Assert
            Assert.IsType<ResultException>(exception);
            Assert.Equal(ResultCodeType.UserNotOnline, ((ResultException)exception).ResultCode);
        }
    }
}
