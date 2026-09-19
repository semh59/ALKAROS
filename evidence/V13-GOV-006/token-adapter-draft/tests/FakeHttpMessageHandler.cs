using System.Net;

namespace ALKAROS.Payments.Token.Draft.Tests;

/// <summary>
/// A scripted <see cref="HttpMessageHandler"/> — no network, no real Token
/// terminal. Each test wires exact responses (copied verbatim from the real
/// Postman collection's saved examples) keyed by request path, to prove the
/// client's parsing/serialization matches Token's own documented schema.
/// This is schema-conformance testing, NOT acceptance evidence of real
/// terminal behavior — see README.md.
/// </summary>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<(HttpStatusCode StatusCode, string Body)> _responses = new();
    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>
    /// The request body read as a string BEFORE the caller's `using`
    /// disposes the request/content (reading `Requests[i].Content` after
    /// the call returns throws <see cref="ObjectDisposedException"/> —
    /// every client method here disposes its request with `using`).
    /// </summary>
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
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        };
        return response;
    }
}
