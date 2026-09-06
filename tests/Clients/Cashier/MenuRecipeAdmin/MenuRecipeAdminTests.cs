using FluentAssertions;
using Xunit;

namespace ALKAROS.Clients.Cashier.MenuRecipeAdmin.Tests;

public sealed class MenuRecipeAdminTests
{
    private readonly MenuRecipeAdminEngine _engine = new();
    private readonly MenuRecipeAdminOperator _adminUser;
    private readonly MenuRecipeAdminOperator _unauthorizedUser;

    private readonly Guid _stockItemIdMeat = Guid.NewGuid();
    private readonly Guid _stockItemIdMilk = Guid.NewGuid();
    private readonly Guid _stockItemIdEgg = Guid.NewGuid();

    public MenuRecipeAdminTests()
    {
        _adminUser = new MenuRecipeAdminOperator(
            OperatorId: Guid.NewGuid(),
            FullName: "Yönetici Ali",
            Permissions: new HashSet<string> { MenuRecipePermissions.MenuAdmin, MenuRecipePermissions.RecipeAdmin });

        _unauthorizedUser = new MenuRecipeAdminOperator(
            OperatorId: Guid.NewGuid(),
            FullName: "Garson Mehmet",
            Permissions: new HashSet<string>());

        var stockItems = new List<StockItemUnitInfo>
        {
            new(_stockItemIdMeat, "STK-ET-01", "Kıyma", "kg", MenuRecipeAdminEngine.DimMass),
            new(_stockItemIdMilk, "STK-SUT-01", "Süt", "l", MenuRecipeAdminEngine.DimVolume),
            new(_stockItemIdEgg, "STK-YMR-01", "Yumurta", "adet", MenuRecipeAdminEngine.DimCount)
        };

        _engine.LoadRecipes(Array.Empty<RecipeView>(), stockItems);
    }

    [Fact]
    public void CannotEditActiveOrReferencedRecipeVersionSurfacesClearTurkishError()
    {
        _engine.SetOperator(_adminUser);

        var recipeId = Guid.NewGuid();
        var activeVersionId = Guid.NewGuid();
        var referencedDraftId = Guid.NewGuid();

        var activeVersion = new RecipeVersionViewModel(
            Id: activeVersionId,
            RecipeId: recipeId,
            VersionNumber: 1,
            Status: "Active",
            IsReferencedInProductionOrMenu: false,
            Ingredients: new List<RecipeIngredientViewModel>
            {
                new(Guid.NewGuid(), _stockItemIdMeat, "Kıyma", 0.200m, "kg", MenuRecipeAdminEngine.DimMass, 5.0m, 1)
            },
            ChangeReason: "İlk onaylı sürüm",
            CreatedAt: DateTimeOffset.UtcNow.AddDays(-10));

        var referencedDraft = new RecipeVersionViewModel(
            Id: referencedDraftId,
            RecipeId: recipeId,
            VersionNumber: 2,
            Status: "Draft",
            IsReferencedInProductionOrMenu: true, // Referenced in production batch!
            Ingredients: new List<RecipeIngredientViewModel>
            {
                new(Guid.NewGuid(), _stockItemIdMeat, "Kıyma", 0.250m, "kg", MenuRecipeAdminEngine.DimMass, 5.0m, 1)
            },
            ChangeReason: "Porsiyon revizyonu",
            CreatedAt: DateTimeOffset.UtcNow.AddDays(-1));

        var recipe = new RecipeView(
            Id: recipeId,
            Code: "RCP-KOFTE",
            Name: "Izgara Köfte",
            Description: "Geleneksel ızgara köfte",
            YieldUnitCode: "porsiyon",
            YieldQuantity: 1.0m,
            ActiveVersion: activeVersion,
            Versions: new[] { activeVersion, referencedDraft });

        _engine.LoadRecipes(new[] { recipe }, null!);

        // Attempt 1: Try editing Active version
        var editActiveCmd = new EditRecipeVersionCommand(
            RecipeVersionId: activeVersionId,
            Ingredients: new List<RecipeIngredientInput>
            {
                new(_stockItemIdMeat, "Kıyma", 0.300m, "kg", MenuRecipeAdminEngine.DimMass, 5.0m, 1)
            });

        var editActiveResult = _engine.EditRecipeVersion(editActiveCmd);
        editActiveResult.Success.Should().BeFalse();
        editActiveResult.ErrorMessage.Should().Be("Aktif veya referans verilmiş tarif versiyonları değiştirilemez; yeni bir versiyon oluşturulmalıdır.");
        _engine.LastErrorMessage.Should().Be("Aktif veya referans verilmiş tarif versiyonları değiştirilemez; yeni bir versiyon oluşturulmalıdır.");

        // Attempt 2: Try editing Draft version that is referenced
        var editRefCmd = new EditRecipeVersionCommand(
            RecipeVersionId: referencedDraftId,
            Ingredients: new List<RecipeIngredientInput>
            {
                new(_stockItemIdMeat, "Kıyma", 0.280m, "kg", MenuRecipeAdminEngine.DimMass, 5.0m, 1)
            });

        var editRefResult = _engine.EditRecipeVersion(editRefCmd);
        editRefResult.Success.Should().BeFalse();
        editRefResult.ErrorMessage.Should().Be("Aktif veya referans verilmiş tarif versiyonları değiştirilemez; yeni bir versiyon oluşturulmalıdır.");
    }

    [Fact]
    public void EditableDraftVersionAllowsModifications()
    {
        _engine.SetOperator(_adminUser);

        var recipeId = Guid.NewGuid();
        var draftVersionId = Guid.NewGuid();

        var draftVersion = new RecipeVersionViewModel(
            Id: draftVersionId,
            RecipeId: recipeId,
            VersionNumber: 1,
            Status: "Draft",
            IsReferencedInProductionOrMenu: false,
            Ingredients: new List<RecipeIngredientViewModel>
            {
                new(Guid.NewGuid(), _stockItemIdMeat, "Kıyma", 0.200m, "kg", MenuRecipeAdminEngine.DimMass, 5.0m, 1)
            },
            ChangeReason: "Taslak çalışma",
            CreatedAt: DateTimeOffset.UtcNow);

        var recipe = new RecipeView(
            Id: recipeId,
            Code: "RCP-KOFTE",
            Name: "Izgara Köfte",
            Description: "Geleneksel ızgara köfte",
            YieldUnitCode: "porsiyon",
            YieldQuantity: 1.0m,
            ActiveVersion: null,
            Versions: new[] { draftVersion });

        _engine.LoadRecipes(new[] { recipe }, null!);

        var editCmd = new EditRecipeVersionCommand(
            RecipeVersionId: draftVersionId,
            Ingredients: new List<RecipeIngredientInput>
            {
                new(_stockItemIdMeat, "Kıyma", 0.250m, "kg", MenuRecipeAdminEngine.DimMass, 6.0m, 1)
            });

        var result = _engine.EditRecipeVersion(editCmd);
        result.Success.Should().BeTrue();
        result.Value!.Ingredients[0].Quantity.Should().Be(0.250m);
        result.Value.Ingredients[0].LossPercentage.Should().Be(6.0m);
    }

    [Fact]
    public void IngredientDimensionMismatchSurfacesClearDimensionError()
    {
        _engine.SetOperator(_adminUser);

        // STK-ET-01 is Mass (kg). Input attempts to set Volume (l).
        var invalidIngredient = new RecipeIngredientInput(
            IngredientItemId: _stockItemIdMeat,
            IngredientName: "Kıyma",
            Quantity: 0.500m,
            UnitCode: "l",
            UnitDimension: MenuRecipeAdminEngine.DimVolume,
            LossPercentage: 0.0m,
            SortOrder: 1);

        var result = _engine.ValidateIngredient(invalidIngredient);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("UNIT_DIMENSION_MISMATCH");
        result.ErrorMessage.Should().Contain("Birim boyutu uyumsuz");
        result.ErrorMessage.Should().Contain("Volume");
        result.ErrorMessage.Should().Contain("Mass");
    }

    [Fact]
    public void InvalidLossPercentageSurfacesExplicitLossError()
    {
        _engine.SetOperator(_adminUser);

        // Loss percentage negative (-5%)
        var negativeLoss = new RecipeIngredientInput(
            IngredientItemId: _stockItemIdMeat,
            IngredientName: "Kıyma",
            Quantity: 0.500m,
            UnitCode: "kg",
            UnitDimension: MenuRecipeAdminEngine.DimMass,
            LossPercentage: -5.0m,
            SortOrder: 1);

        var resultNegative = _engine.ValidateIngredient(negativeLoss);
        resultNegative.Success.Should().BeFalse();
        resultNegative.ErrorCode.Should().Be("INVALID_LOSS_PERCENTAGE");
        resultNegative.ErrorMessage.Should().Contain("Fire oranı %0 ile %99.99 arasında olmalıdır");

        // Loss percentage >= 100% (100%)
        var excessiveLoss = negativeLoss with { LossPercentage = 100.0m };
        var resultExcessive = _engine.ValidateIngredient(excessiveLoss);
        resultExcessive.Success.Should().BeFalse();
        resultExcessive.ErrorCode.Should().Be("INVALID_LOSS_PERCENTAGE");
    }

    [Fact]
    public void ZeroOrNegativeQuantitySurfacesQuantityError()
    {
        _engine.SetOperator(_adminUser);

        var zeroQty = new RecipeIngredientInput(
            IngredientItemId: _stockItemIdMeat,
            IngredientName: "Kıyma",
            Quantity: 0m,
            UnitCode: "kg",
            UnitDimension: MenuRecipeAdminEngine.DimMass,
            LossPercentage: 0m,
            SortOrder: 1);

        var result = _engine.ValidateIngredient(zeroQty);
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("QUANTITY_MUST_BE_POSITIVE");
        result.ErrorMessage.Should().Contain("Miktar sıfırdan büyük olmalıdır");
    }

    [Fact]
    public void UnauthorizedOperatorCannotAdministerMenus()
    {
        _engine.SetOperator(_unauthorizedUser);

        var createMenuCmd = new CreateStaticMenuCommand("MNU-LUNCH", "Öğle Menüsü", "Hızlı öğle menüsü");
        var menuResult = _engine.CreateStaticMenu(createMenuCmd);

        menuResult.Success.Should().BeFalse();
        menuResult.ErrorCode.Should().Be("PERMISSION_DENIED");
        menuResult.ErrorMessage.Should().Be("Menü yönetimi için yetkiniz bulunmamaktadır.");

        var createDailyCmd = new CreateDailyMenuCommand(DateOnly.FromDateTime(DateTime.Today), "Öğle");
        var dailyResult = _engine.CreateDailyMenu(createDailyCmd);

        dailyResult.Success.Should().BeFalse();
        dailyResult.ErrorCode.Should().Be("PERMISSION_DENIED");
        dailyResult.ErrorMessage.Should().Be("Menü yönetimi için yetkiniz bulunmamaktadır.");
    }

    [Fact]
    public void UnauthorizedOperatorCannotAdministerRecipes()
    {
        _engine.SetOperator(_unauthorizedUser);

        var createDraftCmd = new CreateRecipeDraftCommand(
            RecipeId: Guid.NewGuid(),
            ChangeReason: "İzinsiz taslak",
            Ingredients: new List<RecipeIngredientInput>());

        var result = _engine.CreateRecipeDraft(createDraftCmd);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("PERMISSION_DENIED");
        result.ErrorMessage.Should().Be("Reçete yönetimi için yetkiniz bulunmamaktadır.");
    }

    [Fact]
    public void StaticMenuCompositionAndPriceUpdatesOperateAccurately()
    {
        _engine.SetOperator(_adminUser);

        var createCmd = new CreateStaticMenuCommand("MNU-ANA", "Ana Menü", "Ana restoran menüsü");
        var createResult = _engine.CreateStaticMenu(createCmd);
        createResult.Success.Should().BeTrue();
        var menuId = createResult.Value!.Id;

        var productId = Guid.NewGuid();
        var addItemCmd = new AddStaticMenuItemCommand(
            MenuId: menuId,
            ProductId: productId,
            ProductName: "Mercimek Çorbası",
            Price: 90.00m,
            Category: "Çorbalar",
            DisplayOrder: 1);

        var addResult = _engine.AddStaticMenuItem(addItemCmd);
        addResult.Success.Should().BeTrue();
        addResult.Value!.Items.Should().HaveCount(1);
        var itemId = addResult.Value.Items[0].Id;

        // Update item price & availability
        var updateCmd = new UpdateStaticMenuItemCommand(
            MenuId: menuId,
            ItemId: itemId,
            Price: 95.00m,
            DisplayOrder: 1,
            IsAvailable: false);

        var updateResult = _engine.UpdateStaticMenuItem(updateCmd);
        updateResult.Success.Should().BeTrue();
        var updatedItem = updateResult.Value!.Items[0];
        updatedItem.Price.Should().Be(95.00m);
        updatedItem.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void DailyMenuLifecycleAndPortionSetupOperatesAccurately()
    {
        _engine.SetOperator(_adminUser);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var createCmd = new CreateDailyMenuCommand(today, "Öğle");
        var createResult = _engine.CreateDailyMenu(createCmd);
        createResult.Success.Should().BeTrue();
        var dailyMenuId = createResult.Value!.Id;

        // Try publishing empty menu -> fails
        var publishEmptyResult = _engine.PublishDailyMenu(new PublishDailyMenuCommand(dailyMenuId));
        publishEmptyResult.Success.Should().BeFalse();
        publishEmptyResult.ErrorMessage.Should().Be("Günlük menüde en az bir ürün bulunmalıdır.");

        // Add daily item with planned portions
        var addItemCmd = new AddDailyMenuItemCommand(
            DailyMenuId: dailyMenuId,
            ProductId: Guid.NewGuid(),
            ProductName: "Karnıyarık",
            RecipeVersionId: Guid.NewGuid(),
            RecipeVersionNumber: 1,
            PlannedPortions: 50,
            Price: 180.00m);

        var addResult = _engine.AddDailyMenuItem(addItemCmd);
        addResult.Success.Should().BeTrue();
        addResult.Value!.Items[0].RemainingPortions.Should().Be(50);

        // Publish daily menu
        var publishResult = _engine.PublishDailyMenu(new PublishDailyMenuCommand(dailyMenuId));
        publishResult.Success.Should().BeTrue();
        publishResult.Value!.Status.Should().Be("Published");

        // Adding item to published menu is rejected
        var addAfterPublishResult = _engine.AddDailyMenuItem(addItemCmd);
        addAfterPublishResult.Success.Should().BeFalse();
        addAfterPublishResult.ErrorMessage.Should().Be("Yayınlanmış veya kapanmış günlük menüye yeni ürün eklenemez.");
    }

    [Fact]
    public void RecipeDraftCreationAndActivationManagesVersionsProperly()
    {
        _engine.SetOperator(_adminUser);

        var recipeId = Guid.NewGuid();
        var recipe = new RecipeView(
            Id: recipeId,
            Code: "RCP-TAVUK",
            Name: "Tavuk Şiş",
            Description: "Marine edilmiş tavuk şiş",
            YieldUnitCode: "porsiyon",
            YieldQuantity: 1.0m,
            ActiveVersion: null,
            Versions: Array.Empty<RecipeVersionViewModel>());

        _engine.LoadRecipes(new[] { recipe }, null!);

        // Create version 1 (Draft)
        var createDraftCmd = new CreateRecipeDraftCommand(
            RecipeId: recipeId,
            ChangeReason: "İlk reçete tanımı",
            Ingredients: new List<RecipeIngredientInput>
            {
                new(_stockItemIdMeat, "Tavuk Göğsü", 0.250m, "kg", MenuRecipeAdminEngine.DimMass, 3.0m, 1)
            });

        var draftResult = _engine.CreateRecipeDraft(createDraftCmd);
        draftResult.Success.Should().BeTrue();
        draftResult.Value!.VersionNumber.Should().Be(1);
        draftResult.Value.Status.Should().Be("Draft");

        // Activate version 1
        var activateCmd = new ActivateRecipeVersionCommand(draftResult.Value.Id);
        var activateResult = _engine.ActivateRecipeVersion(activateCmd);
        activateResult.Success.Should().BeTrue();
        activateResult.Value!.Status.Should().Be("Active");

        var loadedRecipe = _engine.GetRecipe(recipeId);
        loadedRecipe.Should().NotBeNull();
        loadedRecipe!.ActiveVersion.Should().NotBeNull();
        loadedRecipe.ActiveVersion!.VersionNumber.Should().Be(1);

        // Create version 2 (branch from version 1)
        var createDraft2Cmd = new CreateRecipeDraftCommand(
            RecipeId: recipeId,
            ChangeReason: "Baharat oranı revizyonu",
            Ingredients: new List<RecipeIngredientInput>
            {
                new(_stockItemIdMeat, "Tavuk Göğsü", 0.240m, "kg", MenuRecipeAdminEngine.DimMass, 2.5m, 1)
            });

        var draft2Result = _engine.CreateRecipeDraft(createDraft2Cmd);
        draft2Result.Success.Should().BeTrue();
        draft2Result.Value!.VersionNumber.Should().Be(2);

        // Activate version 2 -> version 1 becomes Archived
        var activate2Result = _engine.ActivateRecipeVersion(new ActivateRecipeVersionCommand(draft2Result.Value.Id));
        activate2Result.Success.Should().BeTrue();
        activate2Result.Value!.Status.Should().Be("Active");

        var reloadedRecipe = _engine.GetRecipe(recipeId);
        reloadedRecipe!.ActiveVersion!.VersionNumber.Should().Be(2);
        var v1 = reloadedRecipe.Versions.First(v => v.VersionNumber == 1);
        v1.Status.Should().Be("Archived");
    }

    [Fact]
    public void ServerRoundtripReloadPreservesAllDataWithoutMutation()
    {
        _engine.SetOperator(_adminUser);

        var menu = new StaticMenuView(
            Id: Guid.NewGuid(),
            Code: "MNU-TEST",
            Name: "Test Menü",
            Description: "Açıklama",
            IsActive: true,
            Items: new List<StaticMenuItemViewModel>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), "Test Ürün", 120.00m, "Kategori 1", 1, true)
            });

        var reloadedMenu = _engine.ReloadStaticMenuFromServer(menu);
        reloadedMenu.Should().BeEquivalentTo(menu);

        var dailyMenu = new DailyMenuView(
            Id: Guid.NewGuid(),
            BusinessDate: new DateOnly(2026, 9, 6),
            MealPeriod: "Akşam",
            Status: "Published",
            Items: new List<DailyMenuItemViewModel>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), "Günlük Yemek", Guid.NewGuid(), 1, 40, 150.00m, 40, false)
            });

        var reloadedDailyMenu = _engine.ReloadDailyMenuFromServer(dailyMenu);
        reloadedDailyMenu.Should().BeEquivalentTo(dailyMenu);
    }

    [Fact]
    public void OperationsUiSatisfiesV0Cmp005AccessibilityCriteria()
    {
        var elements = new[]
        {
            "btn-create-menu",
            "btn-add-menu-item",
            "btn-create-daily-menu",
            "btn-publish-daily-menu",
            "btn-create-recipe-draft",
            "btn-activate-recipe-version"
        };

        foreach (var el in elements)
        {
            var meta = MenuRecipeAdminEngine.GetAccessibilityMetadata(el);
            meta.MinTargetSizePx.Should().BeGreaterOrEqualTo(44, "WCAG 2.2 AA target size must be at least 44px");
            meta.AriaLabel.Should().NotBeNullOrWhiteSpace("Every interactive element must have an accessible Turkish label");
            meta.IsKeyboardFocusable.Should().BeTrue("Interactive operations UI controls must be keyboard operable");
            meta.HighContrastCompliance.Should().BeTrue("Operations UI must satisfy non-text and text contrast");
        }
    }
}
