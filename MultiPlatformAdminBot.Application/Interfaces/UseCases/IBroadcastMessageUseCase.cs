using Application.DTOs;

namespace Application.Interfaces.UseCases
{
    public interface IBroadcastMessageUseCase : IUseCaseRequestHandler<BroadcastMessageRequest, GenericResponse<BaseResponseResponse>>
    {
        Task HandleAlbumAsync(System.Collections.Generic.IEnumerable<BroadcastMessageRequest> requests, IOutPutPort<GenericResponse<BaseResponseResponse>> outputPort);

        /// <summary>Manually retry a failed delivery. Returns true when the resend succeeded.</summary>
        Task<bool> RetryDeliveryAsync(long deliveryId);

        /// <summary>Edits a previously broadcast message across all destination channels.</summary>
        Task HandleEditMessageAsync(BroadcastMessageRequest request);
    }
}
