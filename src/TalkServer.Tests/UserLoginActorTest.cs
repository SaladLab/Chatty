using System;
using System.Threading.Tasks;
using Akka.TestKit.Xunit2;
using Domain;
using Xunit;
using Xunit.Abstractions;

namespace TalkServer
{
    public class UserLoginActorTest : TestKit, IClassFixture<RedisStorageFixture>, IClassFixture<ClusterContextFixture>
    {
        private ClusterNodeContext _clusterContext;
        private MockClient _client;

        public UserLoginActorTest(ITestOutputHelper output, ClusterContextFixture clusterContext)
            : base(output: output)
        {
            clusterContext.Initialize(Sys);
            _clusterContext = clusterContext.Context;
            _client = new MockClient(_clusterContext);
        }

        [Fact]
        public async Task UserLogin_NewUser_Succeed()
        {
            var user = await _client.LoginAsync("account", "1234");
            Assert.NotNull(user);
        }

        [Fact]
        public async Task UserLogin_ExistingUser_Succeed()
        {
            var user = await _client.LoginAsync("account", "1234");

            _client.ChannelRef.WithNoReply().Close();
            await Task.Delay(100);

            _client = new MockClient(_clusterContext);
            var user2 = await _client.LoginAsync("account", "1234");
            Assert.NotNull(user2);
        }

        [Fact]
        public async Task UserLogin_ExistingUser_WrongPassword_Fail()
        {
            var user = await _client.LoginAsync("account", "1234");

            _client.ChannelRef.WithNoReply().Close();

            _client = new MockClient(_clusterContext);
            var exception = await Record.ExceptionAsync(() => _client.LoginAsync("account", "12345"));
            Assert.IsType<ResultException>(exception);
            Assert.Equal(ResultCodeType.LoginFailedIncorrectPassword, ((ResultException)exception).ResultCode);
        }
    }
}
