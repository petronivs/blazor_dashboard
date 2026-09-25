using System.Net;
using System.Text;

namespace BlazorDashboard.Tests.TestSupport;

/// <summary>HttpMessageHandler that records requests and answers them with a delegate.</summary>
internal sealed class StubHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    : HttpMessageHandler
{
    private readonly List<HttpRequestMessage> requests = [];

    public IReadOnlyList<HttpRequestMessage> Requests
    {
        get { lock (requests) { return [.. requests]; } }
    }

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (requests)
        {
            requests.Add(request);
        }
        return respond(request, cancellationToken);
    }
}
