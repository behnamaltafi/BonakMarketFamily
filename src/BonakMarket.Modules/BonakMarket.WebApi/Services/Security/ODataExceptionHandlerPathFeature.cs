using Microsoft.AspNetCore.Diagnostics;

namespace BonakMarket.Web.Api.Security;

internal class ODataExceptionHandlerPathFeature(Exception error, HttpRequest request) : IExceptionHandlerPathFeature
{
    public Exception Error { get; } = error;
    public string Path { get; } = request?.Path;
}