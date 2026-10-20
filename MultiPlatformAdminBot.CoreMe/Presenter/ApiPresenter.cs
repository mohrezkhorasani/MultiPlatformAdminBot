using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace MultiPlatformAdminBot.CoreMe.Presenter
{
    public class ApiPresenter<T> : IOutPutPort<GenericResponse<T>>
    {
        public JsonContentResult jsonContentResult { get; }

        public ApiPresenter()
        {
            jsonContentResult = new JsonContentResult();
        }

        public void Handle(GenericResponse<T> response)
        {
            // Telegram expects HTTP 200 OK regardless of success so webhook isn't retried infinitely
            jsonContentResult.StatusCode = 200;
            jsonContentResult.Content = System.Text.Json.JsonSerializer.Serialize(response);
        }
    }
}
