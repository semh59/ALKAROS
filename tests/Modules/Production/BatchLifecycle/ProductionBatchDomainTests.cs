using System;
using ALKAROS.Production.BatchLifecycle;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Production.BatchLifecycle.Tests;

public sealed class ProductionBatchDomainTests
{
    [Fact]
    public void CreateWithValidParametersSetsInitialStateCorrectly()
    {
        var id = Guid.NewGuid();
        var recipeVersionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var batch = ProductionBatch.Create(
            id: id,
            batchNumber: "PB-2026-001",
            recipeVersionId: recipeVersionId,
            plannedQuantity: 50.0m,
            portionUnitCode: "portion",
            notes: "Initial run",
            createdAt: now);

        batch.Id.Should().Be(id);
        batch.BatchNumber.Should().Be("PB-2026-001");
        batch.RecipeVersionId.Should().Be(recipeVersionId);
        batch.Status.Should().Be(ProductionBatchStatus.Planned);
        batch.PlannedQuantity.Should().Be(50.0m);
        batch.ActualQuantity.Should().Be(0m);
        batch.PortionUnitCode.Should().Be("portion");
        batch.Notes.Should().Be("Initial run");
        batch.StartedAt.Should().BeNull();
        batch.CompletedAt.Should().BeNull();
        batch.ProducedAt.Should().BeNull();
        batch.CancelledAt.Should().BeNull();
        batch.CancellationReason.Should().BeNull();
        batch.RowVersion.Should().Be(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.0001)]
    public void CreateWithNonPositivePlannedQuantityThrowsInvalidQuantityException(decimal invalidQuantity)
    {
        var act = () => ProductionBatch.Create(
            id: Guid.NewGuid(),
            batchNumber: "PB-INVALID-QTY",
            recipeVersionId: Guid.NewGuid(),
            plannedQuantity: invalidQuantity);

        act.Should().Throw<InvalidProductionBatchQuantityException>()
            .WithMessage($"*{invalidQuantity}*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateWithEmptyBatchNumberThrowsArgumentException(string? invalidNumber)
    {
        var act = () => ProductionBatch.Create(
            id: Guid.NewGuid(),
            batchNumber: invalidNumber!,
            recipeVersionId: Guid.NewGuid(),
            plannedQuantity: 10m);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateWithEmptyRecipeVersionIdThrowsArgumentException()
    {
        var act = () => ProductionBatch.Create(
            id: Guid.NewGuid(),
            batchNumber: "PB-001",
            recipeVersionId: Guid.Empty,
            plannedQuantity: 10m);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateWithEmptyIdThrowsArgumentException()
    {
        var act = () => ProductionBatch.Create(
            id: Guid.Empty,
            batchNumber: "PB-001",
            recipeVersionId: Guid.NewGuid(),
            plannedQuantity: 10m);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void StartFromPlannedTransitionsToInProgress()
    {
        var batch = CreateSampleBatch();
        var startTime = DateTimeOffset.UtcNow;

        batch.Start(startTime);

        batch.Status.Should().Be(ProductionBatchStatus.InProgress);
        batch.StartedAt.Should().Be(startTime);
    }

    [Fact]
    public void StartFromInProgressThrowsInvalidTransitionException()
    {
        var batch = CreateSampleBatch();
        batch.Start();

        var act = () => batch.Start();
        act.Should().Throw<InvalidProductionBatchTransitionException>();
    }

    [Fact]
    public void StartFromCompletedThrowsInvalidTransitionException()
    {
        var batch = CreateSampleBatch();
        batch.Start();
        batch.Complete(50m);

        var act = () => batch.Start();
        act.Should().Throw<InvalidProductionBatchTransitionException>();
    }

    [Fact]
    public void StartFromCancelledThrowsInvalidTransitionException()
    {
        var batch = CreateSampleBatch();
        batch.Cancel("Cancelled before start");

        var act = () => batch.Start();
        act.Should().Throw<InvalidProductionBatchTransitionException>();
    }

    [Fact]
    public void CompleteFromInProgressWithPositiveActualQuantityTransitionsToCompleted()
    {
        var batch = CreateSampleBatch();
        batch.Start();
        var completeTime = DateTimeOffset.UtcNow;

        batch.Complete(48.5m, completeTime);

        batch.Status.Should().Be(ProductionBatchStatus.Completed);
        batch.ActualQuantity.Should().Be(48.5m);
        batch.CompletedAt.Should().Be(completeTime);
        batch.ProducedAt.Should().Be(completeTime);
    }

    [Fact]
    public void CompleteFromPlannedThrowsInvalidTransitionException()
    {
        var batch = CreateSampleBatch();

        var act = () => batch.Complete(50m);
        act.Should().Throw<InvalidProductionBatchTransitionException>();
        batch.Status.Should().Be(ProductionBatchStatus.Planned);
        batch.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void CompleteFromCompletedThrowsInvalidTransitionException()
    {
        var batch = CreateSampleBatch();
        batch.Start();
        batch.Complete(50m);

        var act = () => batch.Complete(50m);
        act.Should().Throw<InvalidProductionBatchTransitionException>();
    }

    [Fact]
    public void CompleteFromCancelledThrowsInvalidTransitionException()
    {
        var batch = CreateSampleBatch();
        batch.Cancel("Cancelled");

        var act = () => batch.Complete(50m);
        act.Should().Throw<InvalidProductionBatchTransitionException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100.5)]
    public void CompleteWithZeroOrNegativeQuantityThrowsInvalidQuantityException(decimal invalidQuantity)
    {
        var batch = CreateSampleBatch();
        batch.Start();

        var act = () => batch.Complete(invalidQuantity);
        act.Should().Throw<InvalidProductionBatchQuantityException>();
        batch.Status.Should().Be(ProductionBatchStatus.InProgress);
    }

    [Fact]
    public void CancelFromPlannedTransitionsToCancelled()
    {
        var batch = CreateSampleBatch();
        var cancelTime = DateTimeOffset.UtcNow;

        batch.Cancel("Ingredient unavailable", cancelTime);

        batch.Status.Should().Be(ProductionBatchStatus.Cancelled);
        batch.CancelledAt.Should().Be(cancelTime);
        batch.CancellationReason.Should().Be("Ingredient unavailable");
    }

    [Fact]
    public void CancelFromInProgressTransitionsToCancelled()
    {
        var batch = CreateSampleBatch();
        batch.Start();
        var cancelTime = DateTimeOffset.UtcNow;

        batch.Cancel("Equipment failure mid-production", cancelTime);

        batch.Status.Should().Be(ProductionBatchStatus.Cancelled);
        batch.CancelledAt.Should().Be(cancelTime);
        batch.CancellationReason.Should().Be("Equipment failure mid-production");
    }

    [Fact]
    public void CancelFromCompletedThrowsInvalidTransitionException()
    {
        var batch = CreateSampleBatch();
        batch.Start();
        batch.Complete(50m);

        var act = () => batch.Cancel("Attempt to cancel completed");
        act.Should().Throw<InvalidProductionBatchTransitionException>();
        batch.Status.Should().Be(ProductionBatchStatus.Completed);
    }

    [Fact]
    public void CancelFromCancelledThrowsInvalidTransitionException()
    {
        var batch = CreateSampleBatch();
        batch.Cancel("First cancellation");

        var act = () => batch.Cancel("Second cancellation");
        act.Should().Throw<InvalidProductionBatchTransitionException>();
    }

    [Theory]
    [InlineData(ProductionBatchStatus.Planned)]
    [InlineData(ProductionBatchStatus.InProgress)]
    [InlineData(ProductionBatchStatus.Completed)]
    [InlineData(ProductionBatchStatus.Cancelled)]
    public void ReassignRecipeVersionOnAnyStatusAlwaysThrowsRecipeVersionImmutableException(ProductionBatchStatus status)
    {
        var recipeVersionId = Guid.NewGuid();
        var batch = CreateSampleBatch(recipeVersionId);

        if (status == ProductionBatchStatus.InProgress)
        {
            batch.Start();
        }
        else if (status == ProductionBatchStatus.Completed)
        {
            batch.Start();
            batch.Complete(50m);
        }
        else if (status == ProductionBatchStatus.Cancelled)
        {
            batch.Cancel("Cancelled");
        }

        var newRecipeVersionId = Guid.NewGuid();
        var act = () => batch.ReassignRecipeVersion(newRecipeVersionId);

        act.Should().Throw<RecipeVersionImmutableException>()
            .Where(e => e.BatchId == batch.Id
                     && e.CurrentRecipeVersionId == recipeVersionId
                     && e.AttemptedRecipeVersionId == newRecipeVersionId);

        batch.RecipeVersionId.Should().Be(recipeVersionId);
    }

    [Fact]
    public void ReconstituteHydratesCorrectly()
    {
        var id = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var dailyMenuItemId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var batch = ProductionBatch.Reconstitute(
            id: id,
            batchNumber: "PB-HYDRATED",
            recipeVersionId: recipeId,
            dailyMenuItemId: dailyMenuItemId,
            status: ProductionBatchStatus.Completed,
            plannedQuantity: 100m,
            actualQuantity: 98m,
            portionUnitCode: "portion",
            destinationLocationId: null,
            startedAt: now.AddMinutes(-30),
            completedAt: now,
            producedAt: now,
            cancelledAt: null,
            cancellationReason: null,
            notes: "Clean reconstitution",
            createdBy: null,
            createdAt: now.AddMinutes(-40),
            updatedAt: now,
            rowVersion: 3);

        batch.Id.Should().Be(id);
        batch.BatchNumber.Should().Be("PB-HYDRATED");
        batch.RecipeVersionId.Should().Be(recipeId);
        batch.DailyMenuItemId.Should().Be(dailyMenuItemId);
        batch.Status.Should().Be(ProductionBatchStatus.Completed);
        batch.PlannedQuantity.Should().Be(100m);
        batch.ActualQuantity.Should().Be(98m);
        batch.RowVersion.Should().Be(3);
    }

    private static ProductionBatch CreateSampleBatch(Guid? recipeVersionId = null)
    {
        return ProductionBatch.Create(
            id: Guid.NewGuid(),
            batchNumber: "PB-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            recipeVersionId: recipeVersionId ?? Guid.NewGuid(),
            plannedQuantity: 50.0m);
    }
}
