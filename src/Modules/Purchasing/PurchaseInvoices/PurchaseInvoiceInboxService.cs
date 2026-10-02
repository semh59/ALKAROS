using ALKAROS.Purchasing.OrdersAndReceipts;

namespace ALKAROS.Purchasing.PurchaseInvoices;

public sealed record InboxDocument(string Ettn, string DocumentNo, long SequenceNo);

/// <summary>A logged-in view of the supplier invoice inbox; disposing it closes the remote session.</summary>
public interface IPurchaseInvoiceInboxSession : IAsyncDisposable
{
    /// <summary>The documents after <paramref name="afterSequenceNo"/>, oldest first (the remote side returns them in pages).</summary>
    Task<IReadOnlyList<InboxDocument>> ListAfterAsync(long afterSequenceNo, CancellationToken ct = default);

    Task<string> DownloadXmlAsync(string ettn, CancellationToken ct = default);
}

public interface IPurchaseInvoiceInboxSource
{
    /// <exception cref="PurchaseInvoiceSourceNotConfiguredException">No credentials are stored.</exception>
    /// <exception cref="PurchaseInvoiceSourceUnavailableException">The remote side could not be reached or refused the login.</exception>
    Task<IPurchaseInvoiceInboxSession> OpenAsync(CancellationToken ct = default);
}

public sealed class PurchaseInvoiceSourceNotConfiguredException : PurchasingException
{
    public PurchaseInvoiceSourceNotConfiguredException() : base("The e-invoice inbox credentials are not configured.") { }
}

public sealed class PurchaseInvoiceSourceUnavailableException : PurchasingException
{
    public PurchaseInvoiceSourceUnavailableException(string message, Exception? inner = null) : base(message, inner!) { }
}

/// <param name="Listed">Documents the inbox offered this time.</param>
/// <param name="Imported">New drafts created.</param>
/// <param name="Duplicates">Already imported (same ETTN).</param>
/// <param name="Skipped">Not importable as a purchase invoice (return, malformed, incomplete).</param>
/// <param name="StoppedEarly">A transient download failure stopped the run; the next run resumes at that document.</param>
public sealed record InboxFetchResult(int Listed, int Imported, int Duplicates, int Skipped, bool StoppedEarly);

public interface IPurchaseInvoiceInboxService
{
    Task<InboxFetchResult> FetchAsync(string importedBy, CancellationToken ct = default);
}

public sealed class PurchaseInvoiceInboxService : IPurchaseInvoiceInboxService
{
    private const int MaxPages = 10;

    private readonly IPurchaseInvoiceInboxSource _source;
    private readonly IPurchaseInvoiceService _invoices;
    private readonly IPurchaseInvoiceRepository _repository;

    public PurchaseInvoiceInboxService(IPurchaseInvoiceInboxSource source, IPurchaseInvoiceService invoices, IPurchaseInvoiceRepository repository)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _invoices = invoices ?? throw new ArgumentNullException(nameof(invoices));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<InboxFetchResult> FetchAsync(string importedBy, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(importedBy);

        await using var session = await _source.OpenAsync(ct).ConfigureAwait(false);
        var cursor = await _repository.GetInboxCursorAsync(PurchaseInvoiceSources.QnbInbox, ct).ConfigureAwait(false);
        int listed = 0, imported = 0, duplicates = 0, skipped = 0;
        var stoppedEarly = false;

        for (var page = 0; page < MaxPages && !stoppedEarly; page++)
        {
            var documents = await session.ListAfterAsync(cursor, ct).ConfigureAwait(false);
            var fresh = documents.OrderBy(d => d.SequenceNo).ToArray();
            if (fresh.Length == 0)
                break;

            foreach (var document in fresh)
            {
                listed++;
                try
                {
                    var xml = await session.DownloadXmlAsync(document.Ettn, ct).ConfigureAwait(false);
                    await _invoices.ImportAsync(xml, importedBy, PurchaseInvoiceSources.QnbInbox, ct).ConfigureAwait(false);
                    imported++;
                }
                catch (DuplicatePurchaseInvoiceException)
                {
                    duplicates++;
                }
                catch (Exception ex) when (ex is InvalidPurchaseInvoiceException or UnsupportedPurchaseDocumentException)
                {
                    skipped++;
                }
                catch (PurchaseInvoiceSourceUnavailableException)
                {
                    stoppedEarly = true;
                    break;
                }

                cursor = document.SequenceNo;
                await _repository.SaveInboxCursorAsync(PurchaseInvoiceSources.QnbInbox, cursor, ct).ConfigureAwait(false);
            }
        }

        return new InboxFetchResult(listed, imported, duplicates, skipped, stoppedEarly);
    }
}
