using Application.DTOs;
using System.Threading.Tasks;

namespace Application.Interfaces.UseCases
{
    public interface IAlbumAggregatorService
    {
        Task ProcessRequestAsync(BroadcastMessageRequest request);
    }
}
