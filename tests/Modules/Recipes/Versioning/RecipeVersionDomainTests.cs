using FluentAssertions;
using Xunit;

namespace ALKAROS.Recipes.Versioning.Tests;

public sealed class RecipeVersionDomainTests
{
    private static readonly Guid SampleRecipeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SampleIngredientId1 = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid SampleIngredientId2 = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void CreateDraftWithValidParametersSetsInitialProperties()
    {
        var draft = RecipeVersion.CreateDraft(
            recipeId: SampleRecipeId,
            versionNumber: 1,
            yieldQuantity: 4m,
            yieldUnitCode: "portion",
            preparationMinutes: 25,
            instructions: "Cook with passion");

        draft.RecipeId.Should().Be(SampleRecipeId);
        draft.VersionNumber.Should().Be(1);
        draft.Status.Should().Be(RecipeVersionStatus.Draft);
        draft.IsLocked.Should().BeFalse();
        draft.YieldQuantity.Should().Be(4m);
        draft.YieldUnitCode.Should().Be("portion");
        draft.PreparationMinutes.Should().Be(25);
        draft.Instructions.Should().Be("Cook with passion");
        draft.Ingredients.Should().BeEmpty();
    }

    [Fact]
    public void AddIngredientToDraftAddsIngredientCorrectly()
    {
        var draft = RecipeVersion.CreateDraft(SampleRecipeId, 1, 1m, "portion");

        draft.AddIngredient(SampleIngredientId1, 250m, "g", lossPercentage: 5.0m, sortOrder: 1, notes: "Diced");

        draft.Ingredients.Should().HaveCount(1);
        var ing = draft.Ingredients[0];
        ing.IngredientItemId.Should().Be(SampleIngredientId1);
        ing.Quantity.Should().Be(250m);
        ing.UnitCode.Should().Be("g");
        ing.LossPercentage.Should().Be(5.0m);
        ing.SortOrder.Should().Be(1);
        ing.Notes.Should().Be("Diced");
    }

    [Fact]
    public void AddIngredientDuplicateIngredientItemThrowsInvalidRecipeVersionException()
    {
        var draft = RecipeVersion.CreateDraft(SampleRecipeId, 1, 1m, "portion");
        draft.AddIngredient(SampleIngredientId1, 100m, "g");

        var act = () => draft.AddIngredient(SampleIngredientId1, 50m, "g");

        act.Should().Throw<InvalidRecipeVersionException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public void RemoveIngredientFromDraftRemovesIngredient()
    {
        var draft = RecipeVersion.CreateDraft(SampleRecipeId, 1, 1m, "portion");
        draft.AddIngredient(SampleIngredientId1, 100m, "g");
        draft.AddIngredient(SampleIngredientId2, 200m, "ml");

        draft.RemoveIngredient(SampleIngredientId1);

        draft.Ingredients.Should().HaveCount(1);
        draft.Ingredients[0].IngredientItemId.Should().Be(SampleIngredientId2);
    }

    [Fact]
    public void UpdateIngredientInDraftUpdatesValues()
    {
        var draft = RecipeVersion.CreateDraft(SampleRecipeId, 1, 1m, "portion");
        draft.AddIngredient(SampleIngredientId1, 100m, "g");

        draft.UpdateIngredient(SampleIngredientId1, 150m, "g", lossPercentage: 2.5m, sortOrder: 2, notes: "Updated notes");

        var updated = draft.Ingredients[0];
        updated.Quantity.Should().Be(150m);
        updated.LossPercentage.Should().Be(2.5m);
        updated.SortOrder.Should().Be(2);
        updated.Notes.Should().Be("Updated notes");
    }

    [Fact]
    public void UpdateYieldAndPreparationInDraftUpdatesValues()
    {
        var draft = RecipeVersion.CreateDraft(SampleRecipeId, 1, 1m, "portion", preparationMinutes: 10);

        draft.UpdateYieldAndPreparation(10m, "piece", 35, "New instructions");

        draft.YieldQuantity.Should().Be(10m);
        draft.YieldUnitCode.Should().Be("piece");
        draft.PreparationMinutes.Should().Be(35);
        draft.Instructions.Should().Be("New instructions");
    }

    [Fact]
    public void ActivateDraftWithIngredientsSetsActiveAndIsLocked()
    {
        var draft = RecipeVersion.CreateDraft(SampleRecipeId, 1, 1m, "portion");
        draft.AddIngredient(SampleIngredientId1, 100m, "g");
        var now = DateTimeOffset.UtcNow;

        draft.Activate(now);

        draft.Status.Should().Be(RecipeVersionStatus.Active);
        draft.IsLocked.Should().BeTrue();
        draft.EffectiveFrom.Should().Be(now);
        draft.ActivatedAt.Should().Be(now);
    }

    [Fact]
    public void ActivateDraftWithZeroIngredientsThrowsInvalidRecipeVersionException()
    {
        var draft = RecipeVersion.CreateDraft(SampleRecipeId, 1, 1m, "portion");

        var act = () => draft.Activate(DateTimeOffset.UtcNow);

        act.Should().Throw<InvalidRecipeVersionException>()
            .WithMessage("*zero ingredients*");
    }

    [Fact]
    public void ArchiveActiveVersionSetsArchivedAndEffectiveTo()
    {
        var draft = RecipeVersion.CreateDraft(SampleRecipeId, 1, 1m, "portion");
        draft.AddIngredient(SampleIngredientId1, 100m, "g");
        var activatedAt = DateTimeOffset.UtcNow.AddDays(-1);
        draft.Activate(activatedAt);

        var archivedAt = DateTimeOffset.UtcNow;
        draft.Archive(archivedAt);

        draft.Status.Should().Be(RecipeVersionStatus.Archived);
        draft.IsLocked.Should().BeTrue();
        draft.EffectiveTo.Should().Be(archivedAt);
    }

    [Fact]
    public void ArchiveDraftVersionThrowsInvalidRecipeVersionException()
    {
        var draft = RecipeVersion.CreateDraft(SampleRecipeId, 1, 1m, "portion");

        var act = () => draft.Archive(DateTimeOffset.UtcNow);

        act.Should().Throw<InvalidRecipeVersionException>()
            .WithMessage("*Only Active versions can be archived*");
    }

    [Fact]
    public void DeprecateSetsDeprecatedAndIsLocked()
    {
        var draft = RecipeVersion.CreateDraft(SampleRecipeId, 1, 1m, "portion");

        draft.Deprecate();

        draft.Status.Should().Be(RecipeVersionStatus.Deprecated);
        draft.IsLocked.Should().BeTrue();
    }

    [Fact]
    public void AddIngredientToActiveVersionThrowsRecipeVersionImmutableException()
    {
        var version = RecipeVersion.CreateDraft(SampleRecipeId, 1, 1m, "portion");
        version.AddIngredient(SampleIngredientId1, 100m, "g");
        version.Activate(DateTimeOffset.UtcNow);

        var act = () => version.AddIngredient(SampleIngredientId2, 50m, "ml");

        act.Should().Throw<RecipeVersionImmutableException>()
            .WithMessage("*is immutable*");
    }

    [Fact]
    public void RemoveIngredientFromActiveVersionThrowsRecipeVersionImmutableException()
    {
        var version = RecipeVersion.CreateDraft(SampleRecipeId, 1, 1m, "portion");
        version.AddIngredient(SampleIngredientId1, 100m, "g");
        version.Activate(DateTimeOffset.UtcNow);

        var act = () => version.RemoveIngredient(SampleIngredientId1);

        act.Should().Throw<RecipeVersionImmutableException>()
            .WithMessage("*is immutable*");
    }

    [Fact]
    public void UpdateYieldInActiveVersionThrowsRecipeVersionImmutableException()
    {
        var version = RecipeVersion.CreateDraft(SampleRecipeId, 1, 1m, "portion");
        version.AddIngredient(SampleIngredientId1, 100m, "g");
        version.Activate(DateTimeOffset.UtcNow);

        var act = () => version.UpdateYieldAndPreparation(2m, "portion", 20, null);

        act.Should().Throw<RecipeVersionImmutableException>()
            .WithMessage("*is immutable*");
    }

    [Fact]
    public void AddIngredientToLockedVersionThrowsRecipeVersionImmutableException()
    {
        var draft = RecipeVersion.CreateDraft(SampleRecipeId, 1, 1m, "portion");
        draft.AddIngredient(SampleIngredientId1, 100m, "g");
        draft.LockForOperationalUse();

        var act = () => draft.AddIngredient(SampleIngredientId2, 50m, "ml");

        act.Should().Throw<RecipeVersionImmutableException>()
            .WithMessage("*is immutable*");
    }

    [Fact]
    public void CloneAsDraftIncrementsVersionNumberAndClonesIngredients()
    {
        var v1 = RecipeVersion.CreateDraft(SampleRecipeId, 1, 2m, "portion", preparationMinutes: 15, instructions: "Step 1");
        v1.AddIngredient(SampleIngredientId1, 100m, "g", lossPercentage: 5m, sortOrder: 1, notes: "Salt");
        v1.Activate(DateTimeOffset.UtcNow);

        var v2 = v1.CloneAsDraft(nextVersionNumber: 2);

        v2.Id.Should().NotBe(v1.Id);
        v2.RecipeId.Should().Be(SampleRecipeId);
        v2.VersionNumber.Should().Be(2);
        v2.Status.Should().Be(RecipeVersionStatus.Draft);
        v2.IsLocked.Should().BeFalse();
        v2.YieldQuantity.Should().Be(2m);
        v2.PreparationMinutes.Should().Be(15);
        v2.Instructions.Should().Be("Step 1");
        v2.Ingredients.Should().HaveCount(1);
        v2.Ingredients[0].Id.Should().NotBe(v1.Ingredients[0].Id);
        v2.Ingredients[0].RecipeVersionId.Should().Be(v2.Id);
        v2.Ingredients[0].IngredientItemId.Should().Be(SampleIngredientId1);
        v2.Ingredients[0].Quantity.Should().Be(100m);
    }

    [Fact]
    public void CloneAsDraftMutatingNewDraftDoesNotMutateOriginalHistoricalVersion()
    {
        // Acceptance criteria:
        // "Başvurulan bir RecipeVersion değiştirilemez veya silinemez; yeni sürüm eski production girişlerini korur."
        var v1 = RecipeVersion.CreateDraft(SampleRecipeId, 1, 1m, "portion");
        v1.AddIngredient(SampleIngredientId1, 100m, "g");
        v1.Activate(DateTimeOffset.UtcNow);

        var v2 = v1.CloneAsDraft(nextVersionNumber: 2);

        // Edit new draft: add second ingredient and modify first ingredient
        v2.AddIngredient(SampleIngredientId2, 50m, "ml");
        v2.UpdateIngredient(SampleIngredientId1, 300m, "g");

        // Verify v1 historical record remains completely intact and unaffected
        v1.Ingredients.Should().HaveCount(1);
        v1.Ingredients[0].Quantity.Should().Be(100m);
        v1.Status.Should().Be(RecipeVersionStatus.Active);
        v1.IsLocked.Should().BeTrue();

        // Verify v2 draft has new changes
        v2.Ingredients.Should().HaveCount(2);
        v2.Ingredients.First(i => i.IngredientItemId == SampleIngredientId1).Quantity.Should().Be(300m);
        v2.Ingredients.First(i => i.IngredientItemId == SampleIngredientId2).Quantity.Should().Be(50m);
        v2.Status.Should().Be(RecipeVersionStatus.Draft);
    }
}
