﻿// ------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by Akka.Interfaced CodeGenerator.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
// ------------------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Akka.Interfaced;
using Akka.Actor;
using ProtoBuf;
using TypeAlias;
using System.ComponentModel;

#region Domain.IOccupant

namespace Domain
{
    [PayloadTable(typeof(IOccupant), PayloadTableKind.Request)]
    public static class IOccupant_PayloadTable
    {
        public static Type[,] GetPayloadTypes()
        {
            return new Type[,] {
                { typeof(GetHistory_Invoke), typeof(GetHistory_Return) },
                { typeof(Invite_Invoke), null },
                { typeof(Say_Invoke), null },
            };
        }

        [ProtoContract, TypeAlias]
        public class GetHistory_Invoke
            : IInterfacedPayload, IAsyncInvokable, IPayloadTagOverridable
        {
            public Type GetInterfaceType()
            {
                return typeof(IOccupant);
            }

            public async Task<IValueGetable> InvokeAsync(object __target)
            {
                var __v = await ((IOccupant)__target).GetHistory();
                return (IValueGetable)(new GetHistory_Return { v = __v });
            }

            void IPayloadTagOverridable.SetTag(object value)
            {
            }
        }

        [ProtoContract, TypeAlias]
        public class GetHistory_Return
            : IInterfacedPayload, IValueGetable
        {
            [ProtoMember(1)] public System.Collections.Generic.IList<Domain.ChatItem> v;

            public Type GetInterfaceType()
            {
                return typeof(IOccupant);
            }

            public object Value
            {
                get { return v; }
            }
        }

        [ProtoContract, TypeAlias]
        public class Invite_Invoke
            : IInterfacedPayload, IAsyncInvokable, IPayloadTagOverridable
        {
            [ProtoMember(1)] public System.String targetUserId;
            [ProtoMember(2)] public System.String senderUserId;

            public Type GetInterfaceType()
            {
                return typeof(IOccupant);
            }

            public async Task<IValueGetable> InvokeAsync(object __target)
            {
                await ((IOccupant)__target).Invite(targetUserId, senderUserId);
                return null;
            }

            void IPayloadTagOverridable.SetTag(object value)
            {
                senderUserId = (System.String)value;
            }
        }

        [ProtoContract, TypeAlias]
        public class Say_Invoke
            : IInterfacedPayload, IAsyncInvokable, IPayloadTagOverridable
        {
            [ProtoMember(1)] public System.String msg;
            [ProtoMember(2)] public System.String senderUserId;

            public Type GetInterfaceType()
            {
                return typeof(IOccupant);
            }

            public async Task<IValueGetable> InvokeAsync(object __target)
            {
                await ((IOccupant)__target).Say(msg, senderUserId);
                return null;
            }

            void IPayloadTagOverridable.SetTag(object value)
            {
                senderUserId = (System.String)value;
            }
        }
    }

    public interface IOccupant_NoReply
    {
        void GetHistory();
        void Invite(System.String targetUserId, System.String senderUserId = null);
        void Say(System.String msg, System.String senderUserId = null);
    }

    public class OccupantRef : InterfacedActorRef, IOccupant, IOccupant_NoReply
    {
        public override Type InterfaceType => typeof(IOccupant);

        public OccupantRef() : base(null)
        {
        }

        public OccupantRef(IRequestTarget target) : base(target)
        {
        }

        public OccupantRef(IRequestTarget target, IRequestWaiter requestWaiter, TimeSpan? timeout = null) : base(target, requestWaiter, timeout)
        {
        }

        public IOccupant_NoReply WithNoReply()
        {
            return this;
        }

        public OccupantRef WithRequestWaiter(IRequestWaiter requestWaiter)
        {
            return new OccupantRef(Target, requestWaiter, Timeout);
        }

        public OccupantRef WithTimeout(TimeSpan? timeout)
        {
            return new OccupantRef(Target, RequestWaiter, timeout);
        }

        public Task<System.Collections.Generic.IList<Domain.ChatItem>> GetHistory()
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IOccupant_PayloadTable.GetHistory_Invoke {  }
            };
            return SendRequestAndReceive<System.Collections.Generic.IList<Domain.ChatItem>>(requestMessage);
        }

        public Task Invite(System.String targetUserId, System.String senderUserId = null)
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IOccupant_PayloadTable.Invite_Invoke { targetUserId = targetUserId, senderUserId = senderUserId }
            };
            return SendRequestAndWait(requestMessage);
        }

        public Task Say(System.String msg, System.String senderUserId = null)
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IOccupant_PayloadTable.Say_Invoke { msg = msg, senderUserId = senderUserId }
            };
            return SendRequestAndWait(requestMessage);
        }

        void IOccupant_NoReply.GetHistory()
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IOccupant_PayloadTable.GetHistory_Invoke {  }
            };
            SendRequest(requestMessage);
        }

        void IOccupant_NoReply.Invite(System.String targetUserId, System.String senderUserId)
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IOccupant_PayloadTable.Invite_Invoke { targetUserId = targetUserId, senderUserId = senderUserId }
            };
            SendRequest(requestMessage);
        }

        void IOccupant_NoReply.Say(System.String msg, System.String senderUserId)
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IOccupant_PayloadTable.Say_Invoke { msg = msg, senderUserId = senderUserId }
            };
            SendRequest(requestMessage);
        }
    }

    [ProtoContract]
    public class SurrogateForIOccupant
    {
        [ProtoMember(1)] public IRequestTarget Target;

        [ProtoConverter]
        public static SurrogateForIOccupant Convert(IOccupant value)
        {
            if (value == null) return null;
            return new SurrogateForIOccupant { Target = ((OccupantRef)value).Target };
        }

        [ProtoConverter]
        public static IOccupant Convert(SurrogateForIOccupant value)
        {
            if (value == null) return null;
            return new OccupantRef(value.Target);
        }
    }

    [AlternativeInterface(typeof(IOccupant))]
    public interface IOccupantSync : IInterfacedActorSync
    {
        System.Collections.Generic.IList<Domain.ChatItem> GetHistory();
        void Invite(System.String targetUserId, System.String senderUserId = null);
        void Say(System.String msg, System.String senderUserId = null);
    }
}

#endregion
#region Domain.IRoom

namespace Domain
{
    [PayloadTable(typeof(IRoom), PayloadTableKind.Request)]
    public static class IRoom_PayloadTable
    {
        public static Type[,] GetPayloadTypes()
        {
            return new Type[,] {
                { typeof(Enter_Invoke), typeof(Enter_Return) },
                { typeof(Exit_Invoke), null },
            };
        }

        [ProtoContract, TypeAlias]
        public class Enter_Invoke
            : IInterfacedPayload, IAsyncInvokable, IPayloadObserverUpdatable
        {
            [ProtoMember(1)] public System.String userId;
            [ProtoMember(2)] public Domain.IRoomObserver observer;

            public Type GetInterfaceType()
            {
                return typeof(IRoom);
            }

            public async Task<IValueGetable> InvokeAsync(object __target)
            {
                var __v = await ((IRoom)__target).Enter(userId, observer);
                return (IValueGetable)(new Enter_Return { v = __v });
            }

            void IPayloadObserverUpdatable.Update(Action<IInterfacedObserver> updater)
            {
                if (observer != null)
                {
                    updater(observer);
                }
            }
        }

        [ProtoContract, TypeAlias]
        public class Enter_Return
            : IInterfacedPayload, IValueGetable
        {
            [ProtoMember(1)] public Domain.RoomInfo v;

            public Type GetInterfaceType()
            {
                return typeof(IRoom);
            }

            public object Value
            {
                get { return v; }
            }
        }

        [ProtoContract, TypeAlias]
        public class Exit_Invoke
            : IInterfacedPayload, IAsyncInvokable
        {
            [ProtoMember(1)] public System.String userId;

            public Type GetInterfaceType()
            {
                return typeof(IRoom);
            }

            public async Task<IValueGetable> InvokeAsync(object __target)
            {
                await ((IRoom)__target).Exit(userId);
                return null;
            }
        }
    }

    public interface IRoom_NoReply
    {
        void Enter(System.String userId, Domain.IRoomObserver observer);
        void Exit(System.String userId);
    }

    public class RoomRef : InterfacedActorRef, IRoom, IRoom_NoReply
    {
        public override Type InterfaceType => typeof(IRoom);

        public RoomRef() : base(null)
        {
        }

        public RoomRef(IRequestTarget target) : base(target)
        {
        }

        public RoomRef(IRequestTarget target, IRequestWaiter requestWaiter, TimeSpan? timeout = null) : base(target, requestWaiter, timeout)
        {
        }

        public IRoom_NoReply WithNoReply()
        {
            return this;
        }

        public RoomRef WithRequestWaiter(IRequestWaiter requestWaiter)
        {
            return new RoomRef(Target, requestWaiter, Timeout);
        }

        public RoomRef WithTimeout(TimeSpan? timeout)
        {
            return new RoomRef(Target, RequestWaiter, timeout);
        }

        public Task<Domain.RoomInfo> Enter(System.String userId, Domain.IRoomObserver observer)
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IRoom_PayloadTable.Enter_Invoke { userId = userId, observer = (RoomObserver)observer }
            };
            return SendRequestAndReceive<Domain.RoomInfo>(requestMessage);
        }

        public Task Exit(System.String userId)
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IRoom_PayloadTable.Exit_Invoke { userId = userId }
            };
            return SendRequestAndWait(requestMessage);
        }

        void IRoom_NoReply.Enter(System.String userId, Domain.IRoomObserver observer)
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IRoom_PayloadTable.Enter_Invoke { userId = userId, observer = (RoomObserver)observer }
            };
            SendRequest(requestMessage);
        }

        void IRoom_NoReply.Exit(System.String userId)
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IRoom_PayloadTable.Exit_Invoke { userId = userId }
            };
            SendRequest(requestMessage);
        }
    }

    [ProtoContract]
    public class SurrogateForIRoom
    {
        [ProtoMember(1)] public IRequestTarget Target;

        [ProtoConverter]
        public static SurrogateForIRoom Convert(IRoom value)
        {
            if (value == null) return null;
            return new SurrogateForIRoom { Target = ((RoomRef)value).Target };
        }

        [ProtoConverter]
        public static IRoom Convert(SurrogateForIRoom value)
        {
            if (value == null) return null;
            return new RoomRef(value.Target);
        }
    }

    [AlternativeInterface(typeof(IRoom))]
    public interface IRoomSync : IInterfacedActorSync
    {
        Domain.RoomInfo Enter(System.String userId, Domain.IRoomObserver observer);
        void Exit(System.String userId);
    }
}

#endregion
#region Domain.IUser

namespace Domain
{
    [PayloadTable(typeof(IUser), PayloadTableKind.Request)]
    public static class IUser_PayloadTable
    {
        public static Type[,] GetPayloadTypes()
        {
            return new Type[,] {
                { typeof(EnterRoom_Invoke), typeof(EnterRoom_Return) },
                { typeof(ExitFromRoom_Invoke), null },
                { typeof(GetId_Invoke), typeof(GetId_Return) },
                { typeof(GetRoomList_Invoke), typeof(GetRoomList_Return) },
                { typeof(Whisper_Invoke), null },
            };
        }

        [ProtoContract, TypeAlias]
        public class EnterRoom_Invoke
            : IInterfacedPayload, IAsyncInvokable, IPayloadObserverUpdatable
        {
            [ProtoMember(1)] public System.String name;
            [ProtoMember(2)] public Domain.IRoomObserver observer;

            public Type GetInterfaceType()
            {
                return typeof(IUser);
            }

            public async Task<IValueGetable> InvokeAsync(object __target)
            {
                var __v = await ((IUser)__target).EnterRoom(name, observer);
                return (IValueGetable)(new EnterRoom_Return { v = __v });
            }

            void IPayloadObserverUpdatable.Update(Action<IInterfacedObserver> updater)
            {
                if (observer != null)
                {
                    updater(observer);
                }
            }
        }

        [ProtoContract, TypeAlias]
        public class EnterRoom_Return
            : IInterfacedPayload, IValueGetable, IPayloadActorRefUpdatable
        {
            [ProtoMember(1)] public System.Tuple<Domain.IOccupant, Domain.RoomInfo> v;

            public Type GetInterfaceType()
            {
                return typeof(IUser);
            }

            public object Value
            {
                get { return v; }
            }

            void IPayloadActorRefUpdatable.Update(Action<object> updater)
            {
                if (v != null)
                {
                    if (v.Item1 != null) updater(v.Item1);
                }
            }
        }

        [ProtoContract, TypeAlias]
        public class ExitFromRoom_Invoke
            : IInterfacedPayload, IAsyncInvokable
        {
            [ProtoMember(1)] public System.String name;

            public Type GetInterfaceType()
            {
                return typeof(IUser);
            }

            public async Task<IValueGetable> InvokeAsync(object __target)
            {
                await ((IUser)__target).ExitFromRoom(name);
                return null;
            }
        }

        [ProtoContract, TypeAlias]
        public class GetId_Invoke
            : IInterfacedPayload, IAsyncInvokable
        {
            public Type GetInterfaceType()
            {
                return typeof(IUser);
            }

            public async Task<IValueGetable> InvokeAsync(object __target)
            {
                var __v = await ((IUser)__target).GetId();
                return (IValueGetable)(new GetId_Return { v = __v });
            }
        }

        [ProtoContract, TypeAlias]
        public class GetId_Return
            : IInterfacedPayload, IValueGetable
        {
            [ProtoMember(1)] public System.String v;

            public Type GetInterfaceType()
            {
                return typeof(IUser);
            }

            public object Value
            {
                get { return v; }
            }
        }

        [ProtoContract, TypeAlias]
        public class GetRoomList_Invoke
            : IInterfacedPayload, IAsyncInvokable
        {
            public Type GetInterfaceType()
            {
                return typeof(IUser);
            }

            public async Task<IValueGetable> InvokeAsync(object __target)
            {
                var __v = await ((IUser)__target).GetRoomList();
                return (IValueGetable)(new GetRoomList_Return { v = __v });
            }
        }

        [ProtoContract, TypeAlias]
        public class GetRoomList_Return
            : IInterfacedPayload, IValueGetable
        {
            [ProtoMember(1)] public System.Collections.Generic.IList<System.String> v;

            public Type GetInterfaceType()
            {
                return typeof(IUser);
            }

            public object Value
            {
                get { return v; }
            }
        }

        [ProtoContract, TypeAlias]
        public class Whisper_Invoke
            : IInterfacedPayload, IAsyncInvokable
        {
            [ProtoMember(1)] public System.String targetUserId;
            [ProtoMember(2)] public System.String message;

            public Type GetInterfaceType()
            {
                return typeof(IUser);
            }

            public async Task<IValueGetable> InvokeAsync(object __target)
            {
                await ((IUser)__target).Whisper(targetUserId, message);
                return null;
            }
        }
    }

    public interface IUser_NoReply
    {
        void EnterRoom(System.String name, Domain.IRoomObserver observer);
        void ExitFromRoom(System.String name);
        void GetId();
        void GetRoomList();
        void Whisper(System.String targetUserId, System.String message);
    }

    public class UserRef : InterfacedActorRef, IUser, IUser_NoReply
    {
        public override Type InterfaceType => typeof(IUser);

        public UserRef() : base(null)
        {
        }

        public UserRef(IRequestTarget target) : base(target)
        {
        }

        public UserRef(IRequestTarget target, IRequestWaiter requestWaiter, TimeSpan? timeout = null) : base(target, requestWaiter, timeout)
        {
        }

        public IUser_NoReply WithNoReply()
        {
            return this;
        }

        public UserRef WithRequestWaiter(IRequestWaiter requestWaiter)
        {
            return new UserRef(Target, requestWaiter, Timeout);
        }

        public UserRef WithTimeout(TimeSpan? timeout)
        {
            return new UserRef(Target, RequestWaiter, timeout);
        }

        public Task<System.Tuple<Domain.IOccupant, Domain.RoomInfo>> EnterRoom(System.String name, Domain.IRoomObserver observer)
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IUser_PayloadTable.EnterRoom_Invoke { name = name, observer = (RoomObserver)observer }
            };
            return SendRequestAndReceive<System.Tuple<Domain.IOccupant, Domain.RoomInfo>>(requestMessage);
        }

        public Task ExitFromRoom(System.String name)
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IUser_PayloadTable.ExitFromRoom_Invoke { name = name }
            };
            return SendRequestAndWait(requestMessage);
        }

        public Task<System.String> GetId()
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IUser_PayloadTable.GetId_Invoke {  }
            };
            return SendRequestAndReceive<System.String>(requestMessage);
        }

        public Task<System.Collections.Generic.IList<System.String>> GetRoomList()
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IUser_PayloadTable.GetRoomList_Invoke {  }
            };
            return SendRequestAndReceive<System.Collections.Generic.IList<System.String>>(requestMessage);
        }

        public Task Whisper(System.String targetUserId, System.String message)
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IUser_PayloadTable.Whisper_Invoke { targetUserId = targetUserId, message = message }
            };
            return SendRequestAndWait(requestMessage);
        }

        void IUser_NoReply.EnterRoom(System.String name, Domain.IRoomObserver observer)
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IUser_PayloadTable.EnterRoom_Invoke { name = name, observer = (RoomObserver)observer }
            };
            SendRequest(requestMessage);
        }

        void IUser_NoReply.ExitFromRoom(System.String name)
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IUser_PayloadTable.ExitFromRoom_Invoke { name = name }
            };
            SendRequest(requestMessage);
        }

        void IUser_NoReply.GetId()
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IUser_PayloadTable.GetId_Invoke {  }
            };
            SendRequest(requestMessage);
        }

        void IUser_NoReply.GetRoomList()
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IUser_PayloadTable.GetRoomList_Invoke {  }
            };
            SendRequest(requestMessage);
        }

        void IUser_NoReply.Whisper(System.String targetUserId, System.String message)
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IUser_PayloadTable.Whisper_Invoke { targetUserId = targetUserId, message = message }
            };
            SendRequest(requestMessage);
        }
    }

    [ProtoContract]
    public class SurrogateForIUser
    {
        [ProtoMember(1)] public IRequestTarget Target;

        [ProtoConverter]
        public static SurrogateForIUser Convert(IUser value)
        {
            if (value == null) return null;
            return new SurrogateForIUser { Target = ((UserRef)value).Target };
        }

        [ProtoConverter]
        public static IUser Convert(SurrogateForIUser value)
        {
            if (value == null) return null;
            return new UserRef(value.Target);
        }
    }

    [AlternativeInterface(typeof(IUser))]
    public interface IUserSync : IInterfacedActorSync
    {
        System.Tuple<Domain.IOccupant, Domain.RoomInfo> EnterRoom(System.String name, Domain.IRoomObserver observer);
        void ExitFromRoom(System.String name);
        System.String GetId();
        System.Collections.Generic.IList<System.String> GetRoomList();
        void Whisper(System.String targetUserId, System.String message);
    }
}

#endregion
#region Domain.IUserLogin

namespace Domain
{
    [PayloadTable(typeof(IUserLogin), PayloadTableKind.Request)]
    public static class IUserLogin_PayloadTable
    {
        public static Type[,] GetPayloadTypes()
        {
            return new Type[,] {
                { typeof(Login_Invoke), typeof(Login_Return) },
            };
        }

        [ProtoContract, TypeAlias]
        public class Login_Invoke
            : IInterfacedPayload, IAsyncInvokable, IPayloadObserverUpdatable
        {
            [ProtoMember(1)] public System.String id;
            [ProtoMember(2)] public System.String password;
            [ProtoMember(3)] public Domain.IUserEventObserver observer;

            public Type GetInterfaceType()
            {
                return typeof(IUserLogin);
            }

            public async Task<IValueGetable> InvokeAsync(object __target)
            {
                var __v = await ((IUserLogin)__target).Login(id, password, observer);
                return (IValueGetable)(new Login_Return { v = __v });
            }

            void IPayloadObserverUpdatable.Update(Action<IInterfacedObserver> updater)
            {
                if (observer != null)
                {
                    updater(observer);
                }
            }
        }

        [ProtoContract, TypeAlias]
        public class Login_Return
            : IInterfacedPayload, IValueGetable, IPayloadActorRefUpdatable
        {
            [ProtoMember(1)] public Domain.IUser v;

            public Type GetInterfaceType()
            {
                return typeof(IUserLogin);
            }

            public object Value
            {
                get { return v; }
            }

            void IPayloadActorRefUpdatable.Update(Action<object> updater)
            {
                if (v != null)
                {
                    updater(v); 
                }
            }
        }
    }

    public interface IUserLogin_NoReply
    {
        void Login(System.String id, System.String password, Domain.IUserEventObserver observer);
    }

    public class UserLoginRef : InterfacedActorRef, IUserLogin, IUserLogin_NoReply
    {
        public override Type InterfaceType => typeof(IUserLogin);

        public UserLoginRef() : base(null)
        {
        }

        public UserLoginRef(IRequestTarget target) : base(target)
        {
        }

        public UserLoginRef(IRequestTarget target, IRequestWaiter requestWaiter, TimeSpan? timeout = null) : base(target, requestWaiter, timeout)
        {
        }

        public IUserLogin_NoReply WithNoReply()
        {
            return this;
        }

        public UserLoginRef WithRequestWaiter(IRequestWaiter requestWaiter)
        {
            return new UserLoginRef(Target, requestWaiter, Timeout);
        }

        public UserLoginRef WithTimeout(TimeSpan? timeout)
        {
            return new UserLoginRef(Target, RequestWaiter, timeout);
        }

        public Task<Domain.IUser> Login(System.String id, System.String password, Domain.IUserEventObserver observer)
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IUserLogin_PayloadTable.Login_Invoke { id = id, password = password, observer = (UserEventObserver)observer }
            };
            return SendRequestAndReceive<Domain.IUser>(requestMessage);
        }

        void IUserLogin_NoReply.Login(System.String id, System.String password, Domain.IUserEventObserver observer)
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IUserLogin_PayloadTable.Login_Invoke { id = id, password = password, observer = (UserEventObserver)observer }
            };
            SendRequest(requestMessage);
        }
    }

    [ProtoContract]
    public class SurrogateForIUserLogin
    {
        [ProtoMember(1)] public IRequestTarget Target;

        [ProtoConverter]
        public static SurrogateForIUserLogin Convert(IUserLogin value)
        {
            if (value == null) return null;
            return new SurrogateForIUserLogin { Target = ((UserLoginRef)value).Target };
        }

        [ProtoConverter]
        public static IUserLogin Convert(SurrogateForIUserLogin value)
        {
            if (value == null) return null;
            return new UserLoginRef(value.Target);
        }
    }

    [AlternativeInterface(typeof(IUserLogin))]
    public interface IUserLoginSync : IInterfacedActorSync
    {
        Domain.IUser Login(System.String id, System.String password, Domain.IUserEventObserver observer);
    }
}

#endregion
#region Domain.IUserMessasing

namespace Domain
{
    [PayloadTable(typeof(IUserMessasing), PayloadTableKind.Request)]
    public static class IUserMessasing_PayloadTable
    {
        public static Type[,] GetPayloadTypes()
        {
            return new Type[,] {
                { typeof(Invite_Invoke), null },
                { typeof(Whisper_Invoke), null },
            };
        }

        [ProtoContract, TypeAlias]
        public class Invite_Invoke
            : IInterfacedPayload, IAsyncInvokable
        {
            [ProtoMember(1)] public System.String invitorUserId;
            [ProtoMember(2)] public System.String roomName;

            public Type GetInterfaceType()
            {
                return typeof(IUserMessasing);
            }

            public async Task<IValueGetable> InvokeAsync(object __target)
            {
                await ((IUserMessasing)__target).Invite(invitorUserId, roomName);
                return null;
            }
        }

        [ProtoContract, TypeAlias]
        public class Whisper_Invoke
            : IInterfacedPayload, IAsyncInvokable
        {
            [ProtoMember(1)] public Domain.ChatItem chatItem;

            public Type GetInterfaceType()
            {
                return typeof(IUserMessasing);
            }

            public async Task<IValueGetable> InvokeAsync(object __target)
            {
                await ((IUserMessasing)__target).Whisper(chatItem);
                return null;
            }
        }
    }

    public interface IUserMessasing_NoReply
    {
        void Invite(System.String invitorUserId, System.String roomName);
        void Whisper(Domain.ChatItem chatItem);
    }

    public class UserMessasingRef : InterfacedActorRef, IUserMessasing, IUserMessasing_NoReply
    {
        public override Type InterfaceType => typeof(IUserMessasing);

        public UserMessasingRef() : base(null)
        {
        }

        public UserMessasingRef(IRequestTarget target) : base(target)
        {
        }

        public UserMessasingRef(IRequestTarget target, IRequestWaiter requestWaiter, TimeSpan? timeout = null) : base(target, requestWaiter, timeout)
        {
        }

        public IUserMessasing_NoReply WithNoReply()
        {
            return this;
        }

        public UserMessasingRef WithRequestWaiter(IRequestWaiter requestWaiter)
        {
            return new UserMessasingRef(Target, requestWaiter, Timeout);
        }

        public UserMessasingRef WithTimeout(TimeSpan? timeout)
        {
            return new UserMessasingRef(Target, RequestWaiter, timeout);
        }

        public Task Invite(System.String invitorUserId, System.String roomName)
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IUserMessasing_PayloadTable.Invite_Invoke { invitorUserId = invitorUserId, roomName = roomName }
            };
            return SendRequestAndWait(requestMessage);
        }

        public Task Whisper(Domain.ChatItem chatItem)
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IUserMessasing_PayloadTable.Whisper_Invoke { chatItem = chatItem }
            };
            return SendRequestAndWait(requestMessage);
        }

        void IUserMessasing_NoReply.Invite(System.String invitorUserId, System.String roomName)
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IUserMessasing_PayloadTable.Invite_Invoke { invitorUserId = invitorUserId, roomName = roomName }
            };
            SendRequest(requestMessage);
        }

        void IUserMessasing_NoReply.Whisper(Domain.ChatItem chatItem)
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IUserMessasing_PayloadTable.Whisper_Invoke { chatItem = chatItem }
            };
            SendRequest(requestMessage);
        }
    }

    [ProtoContract]
    public class SurrogateForIUserMessasing
    {
        [ProtoMember(1)] public IRequestTarget Target;

        [ProtoConverter]
        public static SurrogateForIUserMessasing Convert(IUserMessasing value)
        {
            if (value == null) return null;
            return new SurrogateForIUserMessasing { Target = ((UserMessasingRef)value).Target };
        }

        [ProtoConverter]
        public static IUserMessasing Convert(SurrogateForIUserMessasing value)
        {
            if (value == null) return null;
            return new UserMessasingRef(value.Target);
        }
    }

    [AlternativeInterface(typeof(IUserMessasing))]
    public interface IUserMessasingSync : IInterfacedActorSync
    {
        void Invite(System.String invitorUserId, System.String roomName);
        void Whisper(Domain.ChatItem chatItem);
    }
}

#endregion
#region Domain.IRoomObserver

namespace Domain
{
    [PayloadTable(typeof(IRoomObserver), PayloadTableKind.Notification)]
    public static class IRoomObserver_PayloadTable
    {
        public static Type[] GetPayloadTypes()
        {
            return new Type[] {
                typeof(Enter_Invoke),
                typeof(Exit_Invoke),
                typeof(Say_Invoke),
            };
        }

        [ProtoContract, TypeAlias]
        public class Enter_Invoke : IInterfacedPayload, IInvokable
        {
            [ProtoMember(1)] public System.String userId;

            public Type GetInterfaceType()
            {
                return typeof(IRoomObserver);
            }

            public void Invoke(object __target)
            {
                ((IRoomObserver)__target).Enter(userId);
            }
        }

        [ProtoContract, TypeAlias]
        public class Exit_Invoke : IInterfacedPayload, IInvokable
        {
            [ProtoMember(1)] public System.String userId;

            public Type GetInterfaceType()
            {
                return typeof(IRoomObserver);
            }

            public void Invoke(object __target)
            {
                ((IRoomObserver)__target).Exit(userId);
            }
        }

        [ProtoContract, TypeAlias]
        public class Say_Invoke : IInterfacedPayload, IInvokable
        {
            [ProtoMember(1)] public Domain.ChatItem chatItem;

            public Type GetInterfaceType()
            {
                return typeof(IRoomObserver);
            }

            public void Invoke(object __target)
            {
                ((IRoomObserver)__target).Say(chatItem);
            }
        }
    }

    public class RoomObserver : InterfacedObserver, IRoomObserver
    {
        public RoomObserver()
            : base(null, 0)
        {
        }

        public RoomObserver(INotificationChannel channel, int observerId = 0)
            : base(channel, observerId)
        {
        }

        public void Enter(System.String userId)
        {
            var payload = new IRoomObserver_PayloadTable.Enter_Invoke { userId = userId };
            Notify(payload);
        }

        public void Exit(System.String userId)
        {
            var payload = new IRoomObserver_PayloadTable.Exit_Invoke { userId = userId };
            Notify(payload);
        }

        public void Say(Domain.ChatItem chatItem)
        {
            var payload = new IRoomObserver_PayloadTable.Say_Invoke { chatItem = chatItem };
            Notify(payload);
        }
    }

    [ProtoContract]
    public class SurrogateForIRoomObserver
    {
        [ProtoMember(1)] public INotificationChannel Channel;
        [ProtoMember(2)] public int ObserverId;

        [ProtoConverter]
        public static SurrogateForIRoomObserver Convert(IRoomObserver value)
        {
            if (value == null) return null;
            var o = (RoomObserver)value;
            return new SurrogateForIRoomObserver { Channel = o.Channel, ObserverId = o.ObserverId };
        }

        [ProtoConverter]
        public static IRoomObserver Convert(SurrogateForIRoomObserver value)
        {
            if (value == null) return null;
            return new RoomObserver(value.Channel, value.ObserverId);
        }
    }

    [AlternativeInterface(typeof(IRoomObserver))]
    public interface IRoomObserverAsync : IInterfacedObserverSync
    {
        Task Enter(System.String userId);
        Task Exit(System.String userId);
        Task Say(Domain.ChatItem chatItem);
    }
}

#endregion
#region Domain.IUserEventObserver

namespace Domain
{
    [PayloadTable(typeof(IUserEventObserver), PayloadTableKind.Notification)]
    public static class IUserEventObserver_PayloadTable
    {
        public static Type[] GetPayloadTypes()
        {
            return new Type[] {
                typeof(Invite_Invoke),
                typeof(Whisper_Invoke),
            };
        }

        [ProtoContract, TypeAlias]
        public class Invite_Invoke : IInterfacedPayload, IInvokable
        {
            [ProtoMember(1)] public System.String invitorUserId;
            [ProtoMember(2)] public System.String roomName;

            public Type GetInterfaceType()
            {
                return typeof(IUserEventObserver);
            }

            public void Invoke(object __target)
            {
                ((IUserEventObserver)__target).Invite(invitorUserId, roomName);
            }
        }

        [ProtoContract, TypeAlias]
        public class Whisper_Invoke : IInterfacedPayload, IInvokable
        {
            [ProtoMember(1)] public Domain.ChatItem chatItem;

            public Type GetInterfaceType()
            {
                return typeof(IUserEventObserver);
            }

            public void Invoke(object __target)
            {
                ((IUserEventObserver)__target).Whisper(chatItem);
            }
        }
    }

    public class UserEventObserver : InterfacedObserver, IUserEventObserver
    {
        public UserEventObserver()
            : base(null, 0)
        {
        }

        public UserEventObserver(INotificationChannel channel, int observerId = 0)
            : base(channel, observerId)
        {
        }

        public void Invite(System.String invitorUserId, System.String roomName)
        {
            var payload = new IUserEventObserver_PayloadTable.Invite_Invoke { invitorUserId = invitorUserId, roomName = roomName };
            Notify(payload);
        }

        public void Whisper(Domain.ChatItem chatItem)
        {
            var payload = new IUserEventObserver_PayloadTable.Whisper_Invoke { chatItem = chatItem };
            Notify(payload);
        }
    }

    [ProtoContract]
    public class SurrogateForIUserEventObserver
    {
        [ProtoMember(1)] public INotificationChannel Channel;
        [ProtoMember(2)] public int ObserverId;

        [ProtoConverter]
        public static SurrogateForIUserEventObserver Convert(IUserEventObserver value)
        {
            if (value == null) return null;
            var o = (UserEventObserver)value;
            return new SurrogateForIUserEventObserver { Channel = o.Channel, ObserverId = o.ObserverId };
        }

        [ProtoConverter]
        public static IUserEventObserver Convert(SurrogateForIUserEventObserver value)
        {
            if (value == null) return null;
            return new UserEventObserver(value.Channel, value.ObserverId);
        }
    }

    [AlternativeInterface(typeof(IUserEventObserver))]
    public interface IUserEventObserverAsync : IInterfacedObserverSync
    {
        Task Invite(System.String invitorUserId, System.String roomName);
        Task Whisper(Domain.ChatItem chatItem);
    }
}

#endregion
