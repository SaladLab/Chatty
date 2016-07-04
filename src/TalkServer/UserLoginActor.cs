using System;
using System.Net;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Interfaced;
using Akka.Interfaced.LogFilter;
using Akka.Interfaced.SlimServer;
using Common.Logging;
using Domain;

namespace TalkServer
{
    [Log]
    [ResponsiveException(typeof(ResultException))]
    public class UserLoginActor : InterfacedActor, IUserLogin
    {
        private readonly ILog _logger;
        private readonly ClusterNodeContext _clusterContext;
        private readonly ActorBoundChannelRef _channel;
        private readonly bool _isBot;

        public UserLoginActor(ClusterNodeContext clusterContext, ActorBoundChannelRef channel, IPEndPoint clientRemoteEndPoint)
        {
            _logger = LogManager.GetLogger($"UserLoginActor({clientRemoteEndPoint})");
            _clusterContext = clusterContext;
            _channel = channel;
            _isBot = clientRemoteEndPoint.Address == IPAddress.None;
        }

        async Task<IUser> IUserLogin.Login(string id, string password, IUserEventObserver observer)
        {
            if (string.IsNullOrEmpty(id))
                throw new ResultException(ResultCodeType.ArgumentError);
            if (string.IsNullOrEmpty(password))
                throw new ResultException(ResultCodeType.ArgumentError);

            // Check account

            if (_isBot == false)
            {
                if (id.ToLower().StartsWith("bot"))
                    throw new ResultException(ResultCodeType.LoginFailedIncorrectPassword);

                if (await Authenticator.AuthenticateAsync(id, password) == false)
                    throw new ResultException(ResultCodeType.LoginFailedIncorrectPassword);
            }

            // Make UserActor

            IActorRef user;
            try
            {
                user = Context.System.ActorOf(
                    Props.Create(() => new UserActor(_clusterContext, _channel, id, observer)),
                    "user_" + id);
            }
            catch (Exception)
            {
                throw new ResultException(ResultCodeType.LoginFailedAlreadyConnected);
            }

            // Register User in UserTable

            var registered = false;
            for (int i = 0; i < 10; i++)
            {
                var reply = await _clusterContext.UserTableContainer.Add(id, user);
                if (reply.Added)
                {
                    registered = true;
                    break;
                }
                await Task.Delay(200);
            }
            if (registered == false)
            {
                user.Tell(InterfacedPoisonPill.Instance);
                throw new ResultException(ResultCodeType.LoginFailedAlreadyConnected);
            }

            // Bind user actor to channel, which makes client to communicate with this actor.

            var boundActor = await _channel.BindActor(
                user.Cast<UserRef>(),
                ActorBindingFlags.CloseThenStop | ActorBindingFlags.StopThenCloseChannel);
            if (boundActor == null)
            {
                user.Tell(InterfacedPoisonPill.Instance);
                _logger.Error($"Failed in binding UserActor({id}");
                throw new ResultException(ResultCodeType.InternalError);
            }

            // After login successfully, stop this
            Self.Tell(InterfacedPoisonPill.Instance);

            return boundActor.Cast<UserRef>();
        }
    }
}
