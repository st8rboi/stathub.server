using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Stathub.WebApi.Infrastructure.Uploads;

/// <summary>
/// Отклоняет слишком большое тело запроса до разбора multipart: иначе ридер формы
/// отвечает техническим «Request body too large» на английском, а на части конфигураций
/// это превращается в дефолтную 500-страницу «An error occurred while processing your request.».
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RejectTooLargeLogoAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var contentLength = context.HttpContext.Request.ContentLength;

        // null — тело отправлено chunked; тогда срабатывает обычный RequestSizeLimit.
        if (contentLength is null || contentLength <= LogoFile.MaxRequestBytes)
            return;

        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status413PayloadTooLarge,
            Title = "Payload Too Large",
            Detail = "Файл слишком большой. Логотип — PNG, JPEG или WebP не больше 2 МБ."
        })
        {
            StatusCode = StatusCodes.Status413PayloadTooLarge
        };
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
