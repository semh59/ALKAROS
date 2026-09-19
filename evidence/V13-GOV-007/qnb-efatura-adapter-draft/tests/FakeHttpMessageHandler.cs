using System.Net;

namespace ALKAROS.Invoicing.Qnb.Draft.Tests;

/// <summary>
/// Copied from `V13-GOV-006`'s own `FakeHttpMessageHandler`
/// (`evidence/V13-GOV-006/token-adapter-draft/tests/`) — same
/// request-body-capture-before-disposal fix already discovered there.
/// No network, no real QNB test tenant.
/// </summary>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<(HttpStatusCode StatusCode, string Body)> _responses = new();
    public List<HttpRequestMessage> Requests { get; } = [];
    public List<string?> RequestBodies { get; } = [];

    public FakeHttpMessageHandler Enqueue(HttpStatusCode statusCode, string body)
    {
        _responses.Enqueue((statusCode, body));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        RequestBodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));

        if (_responses.Count == 0)
            throw new InvalidOperationException("FakeHttpMessageHandler'a beklenenden fazla istek geldi.");

        var (statusCode, body) = _responses.Dequeue();
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "text/xml"),
        };
    }
}
