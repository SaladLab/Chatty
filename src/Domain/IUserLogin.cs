using System.Threading.Tasks;
using Akka.Interfaced;

namespace Domain
{
    public interface IUserLogin : IInterfacedActor
    {
        Task<IUser> Login(string id, string password, IUserEventObserver observer);
    }
}
