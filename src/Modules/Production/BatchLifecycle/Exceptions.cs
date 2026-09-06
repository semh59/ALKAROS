using System;

namespace ALKAROS.Production.BatchLifecycle;

public class ProductionBatchException : Exception
{
    public ProductionBatchException(string message) : base(message) { }
    public ProductionBatchException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class ProductionBatchNotFoundException : ProductionBatchException
{
    public Guid BatchId { get; }

    public ProductionBatchNotFoundException(Guid batchId)
        : base($"Production batch with ID '{batchId}' was not found.")
    {
        BatchId = batchId;
    }

    public ProductionBatchNotFoundException(string identifier)
        : base($"Production batch '{identifier}' was not found.")
    {
    }
}

public sealed class InvalidProductionBatchTransitionException : ProductionBatchException
{
    public ProductionBatchTransition Transition { get; }

    public InvalidProductionBatchTransitionException(string message) : base(message) { }

    public InvalidProductionBatchTransitionException(ProductionBatchStatus from, ProductionBatchStatus to)
        : base($"Illegal production batch transition from '{from}' to '{to}'.")
    {
        Transition = new ProductionBatchTransition(from, to);
    }
}

public readonly record struct ProductionBatchTransition(ProductionBatchStatus From, ProductionBatchStatus To);

public sealed class RecipeVersionImmutableException : ProductionBatchException
{
    public Guid BatchId { get; }
    public Guid CurrentRecipeVersionId { get; }
    public Guid AttemptedRecipeVersionId { get; }

    public RecipeVersionImmutableException(Guid batchId, Guid currentRecipeVersionId, Guid attemptedRecipeVersionId)
        : base($"RecipeVersionId on production batch '{batchId}' is immutable and cannot be modified. Current: '{currentRecipeVersionId}', Attempted: '{attemptedRecipeVersionId}'.")
    {
        BatchId = batchId;
        CurrentRecipeVersionId = currentRecipeVersionId;
        AttemptedRecipeVersionId = attemptedRecipeVersionId;
    }

    public RecipeVersionImmutableException(string message) : base(message) { }
}

public sealed class InvalidProductionBatchQuantityException : ProductionBatchException
{
    public InvalidProductionBatchQuantityException(string message) : base(message) { }
}

public sealed class ProductionBatchDuplicateNumberException : ProductionBatchException
{
    public string BatchNumber { get; }

    public ProductionBatchDuplicateNumberException(string batchNumber)
        : base($"A production batch with number '{batchNumber}' already exists.")
    {
        BatchNumber = batchNumber;
    }
}

public sealed class ProductionBatchConcurrencyException : ProductionBatchException
{
    public Guid BatchId { get; }
    public int ExpectedVersion { get; }

    public ProductionBatchConcurrencyException(Guid batchId, int expectedVersion)
        : base($"Production batch '{batchId}' was modified concurrently (expected version {expectedVersion}).")
    {
        BatchId = batchId;
        ExpectedVersion = expectedVersion;
    }
}
