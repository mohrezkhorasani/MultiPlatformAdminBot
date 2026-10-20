using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace MultiPlatformAdminBot.CoreMe.Presenter
{
    public class JsonContentResult : IActionResult
    {
        public int StatusCode { get; set; } = 200;
        public string Content { get; set; }

        public async Task ExecuteResultAsync(ActionContext context)
        {
            var response = context.HttpContext.Response;
            response.StatusCode = StatusCode;
            response.ContentType = "application/json";
            
            if (!string.IsNullOrEmpty(Content))
            {
                await response.WriteAsync(Content);
            }
        }
    }
}
