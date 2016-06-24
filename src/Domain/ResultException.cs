using System;
using System.Threading.Tasks;
using Akka.Interfaced;
using ProtoBuf;
using TypeAlias;

namespace Domain
{
    public enum ResultCodeType
    {
        None = 0,
        ArgumentError = 1,
        InternalError = 2,
        LoginFailedNoUser = 3,
        LoginFailedIncorrectPassword = 4,
        LoginFailedAlreadyConnected = 5,
        NeedToBeInRoom = 6,
        NeedToBeOutOfRoom = 7,
        RoomRemoved = 8,
        UserNotMyself = 9,
        UserNotOnline = 10,
        UserAlreadyHere = 11
    }

    [ProtoContract, TypeAlias]
    public class ResultException : Exception
    {
        [ProtoMember(1)] public ResultCodeType ResultCode;

        public ResultException()
        {
        }

        public ResultException(ResultCodeType resultCode)
        {
            ResultCode = resultCode;
        }

        public override string ToString()
        {
            return string.Format("ResultException({0})", ResultCode);
        }
    }
}
