using System.Threading.Tasks;

namespace Application.Interfaces
{
    public interface IOutPutPort<in TUseCaseResponse>
    {
        void Handle(TUseCaseResponse response);
    }
}
