using System.Net;

namespace ALKAROS.Invoicing.Qnb.Client.Tests;

/// <summary>Same capture-before-disposal pattern as the other draft/real HTTP client test doubles in this codebase.</summary>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<(HttpStatusCode StatusCode, string Body)> _responses = new();
    public List<string?> RequestBodies { get; } = [];

    public FakeHttpMessageHandler Enqueue(HttpStatusCode statusCode, string body)
    {
        _responses.Enqueue((statusCode, body));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
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
