namespace ALKAROS.Purchasing.Suppliers;

public abstract class SupplierException : Exception
{
    protected SupplierException(string message) : base(message) { }
    protected SupplierException(string message, Exception inner) : base(message, inner) { }
}

public sealed class SupplierNotFoundException : SupplierException
{
    public SupplierNotFoundException(Guid id) : base($"Supplier '{id}' was not found.") { }
    public SupplierNotFoundException(string code) : base($"Supplier with code '{code}' was not found.") { }
}

public sealed class DuplicateSupplierCodeException : SupplierException
{
    public DuplicateSupplierCodeException(string code) : base($"Supplier with code '{code}' already exists.") { }
}

public sealed class DuplicateSupplierTaxNumberException : SupplierException
{
    public DuplicateSupplierTaxNumberException(string taxNumber) : base($"Supplier with tax number '{taxNumber}' already exists.") { }
}

public sealed class InactiveSupplierException : SupplierException
{
    public InactiveSupplierException(string message) : base(message) { }
}

public sealed class InvalidSupplierDataException : SupplierException
{
    public InvalidSupplierDataException(string message) : base(message) { }
}

public sealed class SupplierAccessDeniedException : SupplierException
{
    public SupplierAccessDeniedException(string message) : base(message) { }
}
