using FluentAssertions;
using Xunit;

namespace ALKAROS.Security.DataProtectionRetention.Tests;

public sealed class RetentionCoverageVerifierTests
{
    [Fact]
    public void EveryDataCategoryHasADisposalMatrixEntry()
    {
        var report = RetentionCoverageVerifier.Verify();

        report.IsComplete.Should().BeTrue();
        report.UnmappedCategories.Should().BeEmpty();
    }

    [Fact]
    public void EveryEnumValueIsPresentInBothMatrixDictionaries()
    {
        foreach (var category in Enum.GetValues<DataCategory>())
        {
            DisposalMatrix.Actions.Should().ContainKey(category);
            DisposalMatrix.RetentionPeriods.Should().ContainKey(category);
        }
    }

    [Fact]
    public void ActionForAnUnmappedCategoryThrows()
    {
        var unmapped = (DataCategory)999;

        Assert.Throws<RetentionCategoryUnmappedException>(() => DisposalMatrix.ActionFor(unmapped));
        Assert.Throws<RetentionCategoryUnmappedException>(() => DisposalMatrix.RetentionPeriodFor(unmapped));
    }
}
