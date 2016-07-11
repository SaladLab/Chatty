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
        private readonly bool _isInternalAccess;

        public UserLoginActor(ClusterNodeContext clusterContext, ActorBoundChannelRef channel, IPEndPoint clientRemoteEndPoint)
        {
            _logger = LogManager.GetLogger($"UserLoginActor({clientRemoteEndPoint})");
            _clusterContext = clusterContext;
            _channel = channel;
            _isInternalAccess = clientRemoteEndPoint.Address == IPAddress.None;
        }

        async Task<IUser> IUserLogin.Login(string id, string password, IUserEventObserver observer)
        {
            if (string.IsNullOrEmpty(id))
                throw new ResultException(ResultCodeType.ArgumentError);
            if (string.IsNullOrEmpty(password))
                throw new ResultException(ResultCodeType.ArgumentError);

            // check account

            if (_isInternalAccess == false)
            {
                if (id.ToLower().StartsWith("bot") || id.ToLower().StartsWith("test"))
                    throw new ResultException(ResultCodeType.LoginFailedNoUser);

                if (await Authenticator.AuthenticateAsync(id, password) == false)
                    throw new ResultException(ResultCodeType.LoginFailedIncorrectPassword);
            }
            else
            {
                if (id.ToLower().StartsWith("bot") == false && id.ToLower().StartsWith("test") == false)
                {
                    if (await Authenticator.AuthenticateAsync(id, password) == false)
                        throw new ResultException(ResultCodeType.LoginFailedIncorrectPassword);
                }
            }

            // try to create user actor with user-id

            var user = await _clusterContext.UserTable.WithTimeout(TimeSpan.FromSeconds(30)).GetOrCreate(id, new object[] { observer });
            if (user.Actor == null)
                throw new ResultException(ResultCodeType.InternalError);
            if (user.Created == false)
                throw new ResultException(ResultCodeType.LoginFailedAlreadyConnected);

            // bound actor to this channel or new channel on user gateway

            IRequestTarget boundTarget;
            try
            {
                boundTarget = await _channel.BindActorOrOpenChannel(
                    user.Actor, new TaggedType[] { typeof(IUser) },
                    ActorBindingFlags.OpenThenNotification | ActorBindingFlags.CloseThenStop | ActorBindingFlags.StopThenCloseChannel,
                    "UserGateway", null);
            }
            catch (Exception e)
            {
                _logger.Error($"BindActorOrOpenChannel error (UserId={id})", e);
                user.Actor.Tell(InterfacedPoisonPill.Instance);
                throw new ResultException(ResultCodeType.InternalError);
            }

            // once login done, stop this
            Self.Tell(InterfacedPoisonPill.Instance);

            return boundTarget.Cast<UserRef>();
        }
    }
}
