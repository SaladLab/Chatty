using System;
using System.Collections.Generic;
using ProtoBuf;

namespace Domain
{
    [ProtoContract]
    public class RoomInfo
    {
        [ProtoMember(1)] public string Name;
        [ProtoMember(2)] public IList<string> Users;
        [ProtoMember(3)] public IList<ChatItem> History;
    }
}
