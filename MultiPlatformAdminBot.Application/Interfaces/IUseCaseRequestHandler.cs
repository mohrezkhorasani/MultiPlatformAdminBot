using System.Threading.Tasks;

namespace Application.Interfaces
{
    public interface IUseCaseRequestHandler<in TUseCaseRequest, out TUseCaseResponse>
    {
        Task HandleAsync(TUseCaseRequest request, IOutPutPort<TUseCaseResponse> outputPort);
    }
}
