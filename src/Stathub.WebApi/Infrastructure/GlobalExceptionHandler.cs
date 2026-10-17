using Microsoft.AspNetCore.Diagnostics;
using Stathub.Shared.Exceptions;

namespace Stathub.WebApi.Infrastructure;

internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            DomainException => (StatusCodes.Status400BadRequest, "Bad Request"),
            NotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            // Слишком большое тело запроса (логотип) — отвечаем понятным текстом,
            // а не дефолтным «An error occurred while processing your request.».
            BadHttpRequestException => (StatusCodes.Status413PayloadTooLarge, "Payload Too Large"),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
        };

        var detail = statusCode == StatusCodes.Status500InternalServerError
            ? "Внутренняя ошибка сервера. Попробуйте ещё раз; если повторяется — сообщите администратору."
            : exception.Message;

        if (statusCode == StatusCodes.Status500InternalServerError)
            logger.LogError(
                exception,
                "Необработанное исключение: {Method} {Path}",
                httpContext.Request.Method,
                httpContext.Request.Path);

        httpContext.Response.StatusCode = statusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = statusCode,
                Title = title,
                Detail = detail
            }
        });
    }
}
