using System;
using ProtoBuf;
using TypeAlias;

namespace Domain
{
    public enum ResultCodeType
    {
        None = 0,
        ArgumentError = 1,
        InternalError = 2,
        LoginFailedNoUser = 10,
        LoginFailedIncorrectPassword,
        LoginFailedAlreadyConnected,
        NeedToBeInRoom = 20,
        NeedToBeOutOfRoom,
        RoomRemoved,
        UserNotMyself,
        UserNotOnline,
        UserAlreadyHere
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
