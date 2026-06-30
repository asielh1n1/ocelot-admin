using Ocelot.Errors;

namespace ApiGateway.Util
{
    public class UnauthorizedError : Error
    {
        public UnauthorizedError(string message) : base(message, OcelotErrorCode.UnauthorizedError, 403)
        {
        }
    }

    public class HttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }

        public HttpContextAccessor(HttpContext? httpContext)
        {
            HttpContext = httpContext;
        }
    }
}
