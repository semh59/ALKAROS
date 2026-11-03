using System.Data.Common;
using ALKAROS.Reporting.BusinessDayTotals;

namespace ALKAROS.Reporting.V1Operations.Tests;

/// <summary>The figures the service stores at close; fixed so the store's own behaviour is under test.</summary>
internal sealed class FixedBusinessDayTotalsReader : IBusinessDayTotalsReader
{
    public FixedBusinessDayTotalsReader(BusinessDayFigures? figures = null)
    {
        Figures = figures ?? Empty;
    }

    public static BusinessDayFigures Empty { get; } = new(
        0m, 0m, 0, 0, 0, new OpenItems(0, 0, 0), [], [], [], new CashReconciliationFigure(0, 0m, 0m, 0m, 0, 0));

    public BusinessDayFigures Figures { get; }

    public Task<BusinessDayFigures> ReadAsync(DateOnly businessDate, CancellationToken cancellationToken = default)
        => Task.FromResult(Figures);

    public Task<BusinessDayFigures> ReadAsync(
        DateOnly businessDate, DbConnection connection, DbTransaction? transaction, CancellationToken cancellationToken = default)
        => Task.FromResult(Figures);
}
