using ALKAROS.Invoicing.Qnb.Client;
using ALKAROS.Invoicing.Qnb.CredentialRegistration;
using ALKAROS.Purchasing.PurchaseInvoices;

namespace ALKAROS.Host.Experience.Purchasing;

/// <summary>The supplier invoice inbox backed by QNB eSolutions, using the credentials saved on the QNB settings screen.</summary>
public sealed class QnbPurchaseInvoiceSource : IPurchaseInvoiceInboxSource
{
    private const string DefaultUserServiceUrl = "https://erpefaturatest1.qnbesolutions.com.tr/efatura/ws/userService";

    private readonly IQnbCredentialStore _credentials;
    private readonly IHttpClientFactory _httpClients;

    public QnbPurchaseInvoiceSource(IQnbCredentialStore credentials, IHttpClientFactory httpClients)
    {
        _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        _httpClients = httpClients ?? throw new ArgumentNullException(nameof(httpClients));
    }

    public async Task<IPurchaseInvoiceInboxSession> OpenAsync(CancellationToken ct = default)
    {
        var status = await _credentials.GetStatusAsync(ct).ConfigureAwait(false);
        var password = status.Configured ? await _credentials.ResolvePasswordAsync(ct).ConfigureAwait(false) : null;
        if (status.UserId is null || status.VergiTcKimlikNo is null || password is null)
            throw new PurchaseInvoiceSourceNotConfiguredException();

        var url = Environment.GetEnvironmentVariable("ALKAROS_QNB_USER_SERVICE_URL") is { Length: > 0 } configured ? configured : DefaultUserServiceUrl;
        var client = new QnbSoapClient(_httpClients.CreateClient(), url);
        try
        {
            await client.LoginAsync(status.UserId, password, cancellationToken: ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is QnbApiException or HttpRequestException or TaskCanceledException)
        {
            throw new PurchaseInvoiceSourceUnavailableException("The e-invoice inbox login failed.", ex);
        }

        return new Session(client, status.VergiTcKimlikNo);
    }

    private sealed class Session : IPurchaseInvoiceInboxSession
    {
        private readonly QnbSoapClient _client;
        private readonly string _taxNumber;

        public Session(QnbSoapClient client, string taxNumber)
        {
            _client = client;
            _taxNumber = taxNumber;
        }

        public async Task<IReadOnlyList<InboxDocument>> ListAfterAsync(long afterSequenceNo, CancellationToken ct = default)
        {
            try
            {
                var documents = await _client.ListIncomingInvoicesAsync(_taxNumber, afterSequenceNo, ct).ConfigureAwait(false);
                return documents.Select(d => new InboxDocument(d.Ettn, d.DocumentNo, d.SequenceNo)).ToArray();
            }
            catch (Exception ex) when (ex is QnbApiException or HttpRequestException or TaskCanceledException)
            {
                throw new PurchaseInvoiceSourceUnavailableException("The e-invoice inbox could not be listed.", ex);
            }
        }

        public async Task<string> DownloadXmlAsync(string ettn, CancellationToken ct = default)
        {
            try
            {
                return await _client.DownloadIncomingInvoiceXmlAsync(_taxNumber, ettn, ct).ConfigureAwait(false);
            }
            catch (QnbApiException ex)
            {
                // QNB answered but would not hand this document over: skipping it keeps one bad document from blocking the rest.
                throw new InvalidPurchaseInvoiceException("QNB would not deliver the document.", ex);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                throw new PurchaseInvoiceSourceUnavailableException("The e-invoice could not be downloaded.", ex);
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await _client.LogoutAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is QnbApiException or HttpRequestException or TaskCanceledException)
            {
                // The session expires on QNB's side; a failed logout must not hide the fetch result.
            }
        }
    }
}
