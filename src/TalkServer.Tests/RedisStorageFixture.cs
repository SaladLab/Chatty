using System;
using System.Configuration;

namespace TalkServer
{
    public sealed class RedisStorageFixture : IDisposable
    {
        public RedisStorageFixture()
        {
            var cstr = ConfigurationManager.ConnectionStrings["Redis"].ConnectionString;
            RedisStorage.Instance = new RedisStorage(cstr + ",allowAdmin=true");
            RedisStorage.Db.KeyDelete("Accounts");
            RedisStorage.Db.KeyDelete("RoomHistory");
        }

        public void Dispose()
        {
            RedisStorage.Instance = null;
        }
    }
}
