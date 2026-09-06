namespace ALKAROS.Clients.Cashier.MenuRecipeAdmin;

/// <summary>
/// Domain controller and state engine for Menu and Recipe administration UI (V11-UI-001, PDF:I.21.1-I.21.4, V0-CMP-005).
/// Enforces immutability of referenced/active recipe versions, unit dimension compatibility,
/// permissions, Turkish localization, and operations UI accessibility criteria.
/// </summary>
public sealed class MenuRecipeAdminEngine
{
    public const string DimMass = "Mass";
    public const string DimVolume = "Volume";
    public const string DimCount = "Count";

    private MenuRecipeAdminOperator? _currentOperator;
    private readonly Dictionary<Guid, StaticMenuView> _staticMenus = new();
    private readonly Dictionary<Guid, DailyMenuView> _dailyMenus = new();
    private readonly Dictionary<Guid, RecipeView> _recipes = new();
    private readonly Dictionary<Guid, StockItemUnitInfo> _stockItems = new();
    private string? _lastErrorMessage;

    public MenuRecipeAdminOperator? CurrentOperator => _currentOperator;
    public string? LastErrorMessage => _lastErrorMessage;

    public void SetOperator(MenuRecipeAdminOperator op)
    {
        _currentOperator = op;
        _lastErrorMessage = null;
    }

    public void ClearError()
    {
        _lastErrorMessage = null;
    }

    #region Static Menu Management

    public void LoadStaticMenus(IEnumerable<StaticMenuView> menus)
    {
        _staticMenus.Clear();
        if (menus != null)
        {
            foreach (var menu in menus)
                _staticMenus[menu.Id] = menu;
        }
    }

    public StaticMenuView? GetStaticMenu(Guid id) =>
        _staticMenus.TryGetValue(id, out var menu) ? menu : null;

    public IReadOnlyList<StaticMenuView> GetAllStaticMenus() =>
        _staticMenus.Values.OrderBy(m => m.Code).ToList().AsReadOnly();

    public AdminOperationResult<StaticMenuView> CreateStaticMenu(CreateStaticMenuCommand cmd)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (_currentOperator == null || !_currentOperator.HasPermission(MenuRecipePermissions.MenuAdmin))
        {
            _lastErrorMessage = "Menü yönetimi için yetkiniz bulunmamaktadır.";
            return AdminOperationResult.Fail<StaticMenuView>(_lastErrorMessage, "PERMISSION_DENIED");
        }

        if (string.IsNullOrWhiteSpace(cmd.Code))
        {
            _lastErrorMessage = "Menü kodu zorunludur.";
            return AdminOperationResult.Fail<StaticMenuView>(_lastErrorMessage, "CODE_REQUIRED");
        }

        if (string.IsNullOrWhiteSpace(cmd.Name))
        {
            _lastErrorMessage = "Menü adı zorunludur.";
            return AdminOperationResult.Fail<StaticMenuView>(_lastErrorMessage, "NAME_REQUIRED");
        }

        if (_staticMenus.Values.Any(m => string.Equals(m.Code, cmd.Code.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            _lastErrorMessage = $"'{cmd.Code}' kodlu menü zaten mevcuttur.";
            return AdminOperationResult.Fail<StaticMenuView>(_lastErrorMessage, "DUPLICATE_CODE");
        }

        var newMenu = new StaticMenuView(
            Id: Guid.NewGuid(),
            Code: cmd.Code.Trim(),
            Name: cmd.Name.Trim(),
            Description: cmd.Description?.Trim(),
            IsActive: true,
            Items: Array.Empty<StaticMenuItemViewModel>());

        _staticMenus[newMenu.Id] = newMenu;
        _lastErrorMessage = null;
        return AdminOperationResult.Ok<StaticMenuView>(newMenu);
    }

    public AdminOperationResult<StaticMenuView> AddStaticMenuItem(AddStaticMenuItemCommand cmd)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (_currentOperator == null || !_currentOperator.HasPermission(MenuRecipePermissions.MenuAdmin))
        {
            _lastErrorMessage = "Menü yönetimi için yetkiniz bulunmamaktadır.";
            return AdminOperationResult.Fail<StaticMenuView>(_lastErrorMessage, "PERMISSION_DENIED");
        }

        if (!_staticMenus.TryGetValue(cmd.MenuId, out var menu))
        {
            _lastErrorMessage = "Belirtilen menü bulunamadı.";
            return AdminOperationResult.Fail<StaticMenuView>(_lastErrorMessage, "MENU_NOT_FOUND");
        }

        if (string.IsNullOrWhiteSpace(cmd.ProductName))
        {
            _lastErrorMessage = "Ürün adı zorunludur.";
            return AdminOperationResult.Fail<StaticMenuView>(_lastErrorMessage, "PRODUCT_NAME_REQUIRED");
        }

        if (cmd.Price < 0)
        {
            _lastErrorMessage = "Fiyat negatif olamaz.";
            return AdminOperationResult.Fail<StaticMenuView>(_lastErrorMessage, "PRICE_NEGATIVE");
        }

        var newItem = new StaticMenuItemViewModel(
            Id: Guid.NewGuid(),
            ProductId: cmd.ProductId,
            ProductName: cmd.ProductName.Trim(),
            Price: cmd.Price,
            Category: cmd.Category?.Trim(),
            DisplayOrder: cmd.DisplayOrder,
            IsAvailable: true);

        var updatedItems = menu.Items.Append(newItem).OrderBy(i => i.DisplayOrder).ToList();
        var updatedMenu = menu with { Items = updatedItems };
        _staticMenus[updatedMenu.Id] = updatedMenu;

        _lastErrorMessage = null;
        return AdminOperationResult.Ok<StaticMenuView>(updatedMenu);
    }

    public AdminOperationResult<StaticMenuView> UpdateStaticMenuItem(UpdateStaticMenuItemCommand cmd)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (_currentOperator == null || !_currentOperator.HasPermission(MenuRecipePermissions.MenuAdmin))
        {
            _lastErrorMessage = "Menü yönetimi için yetkiniz bulunmamaktadır.";
            return AdminOperationResult.Fail<StaticMenuView>(_lastErrorMessage, "PERMISSION_DENIED");
        }

        if (!_staticMenus.TryGetValue(cmd.MenuId, out var menu))
        {
            _lastErrorMessage = "Belirtilen menü bulunamadı.";
            return AdminOperationResult.Fail<StaticMenuView>(_lastErrorMessage, "MENU_NOT_FOUND");
        }

        var existingItem = menu.Items.FirstOrDefault(i => i.Id == cmd.ItemId);
        if (existingItem == null)
        {
            _lastErrorMessage = "Menü öğesi bulunamadı.";
            return AdminOperationResult.Fail<StaticMenuView>(_lastErrorMessage, "ITEM_NOT_FOUND");
        }

        if (cmd.Price < 0)
        {
            _lastErrorMessage = "Fiyat negatif olamaz.";
            return AdminOperationResult.Fail<StaticMenuView>(_lastErrorMessage, "PRICE_NEGATIVE");
        }

        var updatedItem = existingItem with
        {
            Price = cmd.Price,
            DisplayOrder = cmd.DisplayOrder,
            IsAvailable = cmd.IsAvailable
        };

        var updatedItems = menu.Items.Select(i => i.Id == cmd.ItemId ? updatedItem : i).OrderBy(i => i.DisplayOrder).ToList();
        var updatedMenu = menu with { Items = updatedItems };
        _staticMenus[updatedMenu.Id] = updatedMenu;

        _lastErrorMessage = null;
        return AdminOperationResult.Ok<StaticMenuView>(updatedMenu);
    }

    #endregion

    #region Daily Menu Management

    public void LoadDailyMenus(IEnumerable<DailyMenuView> menus)
    {
        _dailyMenus.Clear();
        if (menus != null)
        {
            foreach (var menu in menus)
                _dailyMenus[menu.Id] = menu;
        }
    }

    public DailyMenuView? GetDailyMenu(Guid id) =>
        _dailyMenus.TryGetValue(id, out var menu) ? menu : null;

    public IReadOnlyList<DailyMenuView> GetAllDailyMenus() =>
        _dailyMenus.Values.OrderByDescending(m => m.BusinessDate).ToList().AsReadOnly();

    public AdminOperationResult<DailyMenuView> CreateDailyMenu(CreateDailyMenuCommand cmd)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (_currentOperator == null || !_currentOperator.HasPermission(MenuRecipePermissions.MenuAdmin))
        {
            _lastErrorMessage = "Menü yönetimi için yetkiniz bulunmamaktadır.";
            return AdminOperationResult.Fail<DailyMenuView>(_lastErrorMessage, "PERMISSION_DENIED");
        }

        if (string.IsNullOrWhiteSpace(cmd.MealPeriod))
        {
            _lastErrorMessage = "Öğün periyodu zorunludur.";
            return AdminOperationResult.Fail<DailyMenuView>(_lastErrorMessage, "MEAL_PERIOD_REQUIRED");
        }

        var existing = _dailyMenus.Values.FirstOrDefault(m =>
            m.BusinessDate == cmd.BusinessDate &&
            string.Equals(m.MealPeriod, cmd.MealPeriod.Trim(), StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            _lastErrorMessage = $"{cmd.BusinessDate:yyyy-MM-dd} tarihi ve '{cmd.MealPeriod}' periyodu için günlük menü zaten mevcuttur.";
            return AdminOperationResult.Fail<DailyMenuView>(_lastErrorMessage, "DUPLICATE_DAILY_MENU");
        }

        var newMenu = new DailyMenuView(
            Id: Guid.NewGuid(),
            BusinessDate: cmd.BusinessDate,
            MealPeriod: cmd.MealPeriod.Trim(),
            Status: "Draft",
            Items: Array.Empty<DailyMenuItemViewModel>());

        _dailyMenus[newMenu.Id] = newMenu;
        _lastErrorMessage = null;
        return AdminOperationResult.Ok<DailyMenuView>(newMenu);
    }

    public AdminOperationResult<DailyMenuView> AddDailyMenuItem(AddDailyMenuItemCommand cmd)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (_currentOperator == null || !_currentOperator.HasPermission(MenuRecipePermissions.MenuAdmin))
        {
            _lastErrorMessage = "Menü yönetimi için yetkiniz bulunmamaktadır.";
            return AdminOperationResult.Fail<DailyMenuView>(_lastErrorMessage, "PERMISSION_DENIED");
        }

        if (!_dailyMenus.TryGetValue(cmd.DailyMenuId, out var menu))
        {
            _lastErrorMessage = "Günlük menü bulunamadı.";
            return AdminOperationResult.Fail<DailyMenuView>(_lastErrorMessage, "DAILY_MENU_NOT_FOUND");
        }

        if (!string.Equals(menu.Status, "Draft", StringComparison.OrdinalIgnoreCase))
        {
            _lastErrorMessage = "Yayınlanmış veya kapanmış günlük menüye yeni ürün eklenemez.";
            return AdminOperationResult.Fail<DailyMenuView>(_lastErrorMessage, "NOT_IN_DRAFT");
        }

        if (cmd.PlannedPortions <= 0)
        {
            _lastErrorMessage = "Planlanan porsiyon sıfırdan büyük olmalıdır.";
            return AdminOperationResult.Fail<DailyMenuView>(_lastErrorMessage, "PORTIONS_INVALID");
        }

        if (cmd.Price < 0)
        {
            _lastErrorMessage = "Fiyat negatif olamaz.";
            return AdminOperationResult.Fail<DailyMenuView>(_lastErrorMessage, "PRICE_NEGATIVE");
        }

        var newItem = new DailyMenuItemViewModel(
            Id: Guid.NewGuid(),
            ProductId: cmd.ProductId,
            ProductName: cmd.ProductName.Trim(),
            RecipeVersionId: cmd.RecipeVersionId,
            RecipeVersionNumber: cmd.RecipeVersionNumber,
            PlannedPortions: cmd.PlannedPortions,
            Price: cmd.Price,
            RemainingPortions: cmd.PlannedPortions,
            IsSoldOut: false);

        var updatedMenu = menu with { Items = menu.Items.Append(newItem).ToList() };
        _dailyMenus[updatedMenu.Id] = updatedMenu;

        _lastErrorMessage = null;
        return AdminOperationResult.Ok<DailyMenuView>(updatedMenu);
    }

    public AdminOperationResult<DailyMenuView> PublishDailyMenu(PublishDailyMenuCommand cmd)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (_currentOperator == null || !_currentOperator.HasPermission(MenuRecipePermissions.MenuAdmin))
        {
            _lastErrorMessage = "Menü yönetimi için yetkiniz bulunmamaktadır.";
            return AdminOperationResult.Fail<DailyMenuView>(_lastErrorMessage, "PERMISSION_DENIED");
        }

        if (!_dailyMenus.TryGetValue(cmd.DailyMenuId, out var menu))
        {
            _lastErrorMessage = "Günlük menü bulunamadı.";
            return AdminOperationResult.Fail<DailyMenuView>(_lastErrorMessage, "DAILY_MENU_NOT_FOUND");
        }

        if (menu.Items.Count == 0)
        {
            _lastErrorMessage = "Günlük menüde en az bir ürün bulunmalıdır.";
            return AdminOperationResult.Fail<DailyMenuView>(_lastErrorMessage, "MENU_EMPTY");
        }

        var updatedMenu = menu with { Status = "Published" };
        _dailyMenus[updatedMenu.Id] = updatedMenu;

        _lastErrorMessage = null;
        return AdminOperationResult.Ok<DailyMenuView>(updatedMenu);
    }

    #endregion

    #region Recipe and Versioning Management

    public void LoadRecipes(IEnumerable<RecipeView> recipes, IEnumerable<StockItemUnitInfo>? stockItems = null)
    {
        _recipes.Clear();
        if (recipes != null)
        {
            foreach (var r in recipes)
                _recipes[r.Id] = r;
        }

        if (stockItems != null)
        {
            _stockItems.Clear();
            foreach (var s in stockItems)
                _stockItems[s.StockItemId] = s;
        }
    }

    public RecipeView? GetRecipe(Guid id) =>
        _recipes.TryGetValue(id, out var recipe) ? recipe : null;

    public IReadOnlyList<RecipeView> GetAllRecipes() =>
        _recipes.Values.OrderBy(r => r.Code).ToList().AsReadOnly();

    public AdminOperationResult ValidateIngredient(RecipeIngredientInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!_stockItems.TryGetValue(input.IngredientItemId, out var stockItem))
        {
            return AdminOperationResult.Fail("Stok kartı bulunamadı.", "STOCK_ITEM_NOT_FOUND");
        }

        if (input.Quantity <= 0)
        {
            return AdminOperationResult.Fail("Miktar sıfırdan büyük olmalıdır.", "QUANTITY_MUST_BE_POSITIVE");
        }

        if (input.LossPercentage < 0 || input.LossPercentage >= 100)
        {
            return AdminOperationResult.Fail(
                "Fire oranı geçersiz: Fire oranı %0 ile %99.99 arasında olmalıdır.",
                "INVALID_LOSS_PERCENTAGE");
        }

        // Dimension mismatch check (e.g. Mass vs Volume)
        if (!string.Equals(input.UnitDimension.Trim(), stockItem.UnitDimension.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return AdminOperationResult.Fail(
                $"Birim boyutu uyumsuz: Malzeme birim boyutu '{input.UnitDimension}' ile stok kartı birim boyutu '{stockItem.UnitDimension}' eşleşmelidir.",
                "UNIT_DIMENSION_MISMATCH");
        }

        return AdminOperationResult.Ok();
    }

    public AdminOperationResult<RecipeVersionViewModel> CreateRecipeDraft(CreateRecipeDraftCommand cmd)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (_currentOperator == null || !_currentOperator.HasPermission(MenuRecipePermissions.RecipeAdmin))
        {
            _lastErrorMessage = "Reçete yönetimi için yetkiniz bulunmamaktadır.";
            return AdminOperationResult.Fail<RecipeVersionViewModel>(_lastErrorMessage, "PERMISSION_DENIED");
        }

        if (!_recipes.TryGetValue(cmd.RecipeId, out var recipe))
        {
            _lastErrorMessage = "Reçete bulunamadı.";
            return AdminOperationResult.Fail<RecipeVersionViewModel>(_lastErrorMessage, "RECIPE_NOT_FOUND");
        }

        if (recipe.Versions.Count > 0 && string.IsNullOrWhiteSpace(cmd.ChangeReason))
        {
            _lastErrorMessage = "Yeni tarif versiyonu için değişiklik gerekçesi girilmesi zorunludur.";
            return AdminOperationResult.Fail<RecipeVersionViewModel>(_lastErrorMessage, "CHANGE_REASON_REQUIRED");
        }

        if (cmd.Ingredients == null || cmd.Ingredients.Count == 0)
        {
            _lastErrorMessage = "Tarif en az bir malzeme içermelidir.";
            return AdminOperationResult.Fail<RecipeVersionViewModel>(_lastErrorMessage, "INGREDIENTS_EMPTY");
        }

        // Validate all ingredients
        foreach (var ingredient in cmd.Ingredients)
        {
            var valResult = ValidateIngredient(ingredient);
            if (!valResult.Success)
            {
                _lastErrorMessage = valResult.ErrorMessage;
                return AdminOperationResult.Fail<RecipeVersionViewModel>(valResult.ErrorMessage!, valResult.ErrorCode);
            }
        }

        int nextVersionNumber = recipe.Versions.Count > 0
            ? recipe.Versions.Max(v => v.VersionNumber) + 1
            : 1;

        var newVersionId = Guid.NewGuid();
        var ingredientViewModels = cmd.Ingredients.Select(i => new RecipeIngredientViewModel(
            Id: Guid.NewGuid(),
            IngredientItemId: i.IngredientItemId,
            IngredientName: i.IngredientName,
            Quantity: i.Quantity,
            UnitCode: i.UnitCode,
            UnitDimension: i.UnitDimension,
            LossPercentage: i.LossPercentage,
            SortOrder: i.SortOrder)).ToList();

        var newVersion = new RecipeVersionViewModel(
            Id: newVersionId,
            RecipeId: recipe.Id,
            VersionNumber: nextVersionNumber,
            Status: "Draft",
            IsReferencedInProductionOrMenu: false,
            Ingredients: ingredientViewModels,
            ChangeReason: cmd.ChangeReason?.Trim(),
            CreatedAt: DateTimeOffset.UtcNow);

        var updatedVersions = recipe.Versions.Append(newVersion).ToList();
        var updatedRecipe = recipe with { Versions = updatedVersions };
        _recipes[updatedRecipe.Id] = updatedRecipe;

        _lastErrorMessage = null;
        return AdminOperationResult.Ok<RecipeVersionViewModel>(newVersion);
    }

    /// <summary>
    /// Edits a recipe version. Invariant: referenced or active versions CANNOT be modified.
    /// Acceptance Evidence #1: "UI başvurulan sürümü düzenleyemez".
    /// </summary>
    public AdminOperationResult<RecipeVersionViewModel> EditRecipeVersion(EditRecipeVersionCommand cmd)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (_currentOperator == null || !_currentOperator.HasPermission(MenuRecipePermissions.RecipeAdmin))
        {
            _lastErrorMessage = "Reçete yönetimi için yetkiniz bulunmamaktadır.";
            return AdminOperationResult.Fail<RecipeVersionViewModel>(_lastErrorMessage, "PERMISSION_DENIED");
        }

        RecipeView? targetRecipe = null;
        RecipeVersionViewModel? targetVersion = null;

        foreach (var r in _recipes.Values)
        {
            var v = r.Versions.FirstOrDefault(ver => ver.Id == cmd.RecipeVersionId);
            if (v != null)
            {
                targetRecipe = r;
                targetVersion = v;
                break;
            }
        }

        if (targetRecipe == null || targetVersion == null)
        {
            _lastErrorMessage = "Tarif versiyonu bulunamadı.";
            return AdminOperationResult.Fail<RecipeVersionViewModel>(_lastErrorMessage, "VERSION_NOT_FOUND");
        }

        // CRITICAL INVARIANT: Cannot edit if not Draft, or if referenced in production or menu
        if (!string.Equals(targetVersion.Status, "Draft", StringComparison.OrdinalIgnoreCase) ||
            targetVersion.IsReferencedInProductionOrMenu)
        {
            _lastErrorMessage = "Aktif veya referans verilmiş tarif versiyonları değiştirilemez; yeni bir versiyon oluşturulmalıdır.";
            return AdminOperationResult.Fail<RecipeVersionViewModel>(_lastErrorMessage, "IMMUTABLE_VERSION");
        }

        if (cmd.Ingredients == null || cmd.Ingredients.Count == 0)
        {
            _lastErrorMessage = "Tarif en az bir malzeme içermelidir.";
            return AdminOperationResult.Fail<RecipeVersionViewModel>(_lastErrorMessage, "INGREDIENTS_EMPTY");
        }

        foreach (var ingredient in cmd.Ingredients)
        {
            var valResult = ValidateIngredient(ingredient);
            if (!valResult.Success)
            {
                _lastErrorMessage = valResult.ErrorMessage;
                return AdminOperationResult.Fail<RecipeVersionViewModel>(valResult.ErrorMessage!, valResult.ErrorCode);
            }
        }

        var ingredientViewModels = cmd.Ingredients.Select(i => new RecipeIngredientViewModel(
            Id: Guid.NewGuid(),
            IngredientItemId: i.IngredientItemId,
            IngredientName: i.IngredientName,
            Quantity: i.Quantity,
            UnitCode: i.UnitCode,
            UnitDimension: i.UnitDimension,
            LossPercentage: i.LossPercentage,
            SortOrder: i.SortOrder)).ToList();

        var updatedVersion = targetVersion with
        {
            Ingredients = ingredientViewModels
        };

        var updatedVersions = targetRecipe.Versions
            .Select(v => v.Id == updatedVersion.Id ? updatedVersion : v)
            .ToList();

        var updatedRecipe = targetRecipe with { Versions = updatedVersions };
        _recipes[updatedRecipe.Id] = updatedRecipe;

        _lastErrorMessage = null;
        return AdminOperationResult.Ok<RecipeVersionViewModel>(updatedVersion);
    }

    public AdminOperationResult<RecipeVersionViewModel> ActivateRecipeVersion(ActivateRecipeVersionCommand cmd)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (_currentOperator == null || !_currentOperator.HasPermission(MenuRecipePermissions.RecipeAdmin))
        {
            _lastErrorMessage = "Reçete yönetimi için yetkiniz bulunmamaktadır.";
            return AdminOperationResult.Fail<RecipeVersionViewModel>(_lastErrorMessage, "PERMISSION_DENIED");
        }

        RecipeView? targetRecipe = null;
        RecipeVersionViewModel? targetVersion = null;

        foreach (var r in _recipes.Values)
        {
            var v = r.Versions.FirstOrDefault(ver => ver.Id == cmd.RecipeVersionId);
            if (v != null)
            {
                targetRecipe = r;
                targetVersion = v;
                break;
            }
        }

        if (targetRecipe == null || targetVersion == null)
        {
            _lastErrorMessage = "Tarif versiyonu bulunamadı.";
            return AdminOperationResult.Fail<RecipeVersionViewModel>(_lastErrorMessage, "VERSION_NOT_FOUND");
        }

        if (!string.Equals(targetVersion.Status, "Draft", StringComparison.OrdinalIgnoreCase))
        {
            _lastErrorMessage = "Yalnızca taslak (Draft) durumundaki tarif versiyonları aktif edilebilir.";
            return AdminOperationResult.Fail<RecipeVersionViewModel>(_lastErrorMessage, "ONLY_DRAFT_CAN_BE_ACTIVATED");
        }

        // Archive previously active versions and set this to Active
        var newActiveVersion = targetVersion with { Status = "Active" };
        var updatedVersions = targetRecipe.Versions.Select(v =>
        {
            if (v.Id == targetVersion.Id) return newActiveVersion;
            if (string.Equals(v.Status, "Active", StringComparison.OrdinalIgnoreCase)) return v with { Status = "Archived" };
            return v;
        }).ToList();

        var updatedRecipe = targetRecipe with
        {
            ActiveVersion = newActiveVersion,
            Versions = updatedVersions
        };

        _recipes[updatedRecipe.Id] = updatedRecipe;
        _lastErrorMessage = null;
        return AdminOperationResult.Ok<RecipeVersionViewModel>(newActiveVersion);
    }

    #endregion

    #region Server Roundtrip & Reload

    /// <summary>
    /// Reloads static menu record from server snapshot without mutation.
    /// Acceptance Evidence #1: "kaydedilen veriler sunucudan aynı şekilde yeniden yüklenir".
    /// </summary>
    public StaticMenuView ReloadStaticMenuFromServer(StaticMenuView serverSnapshot)
    {
        ArgumentNullException.ThrowIfNull(serverSnapshot);
        _staticMenus[serverSnapshot.Id] = serverSnapshot;
        return _staticMenus[serverSnapshot.Id];
    }

    /// <summary>
    /// Reloads daily menu record from server snapshot without mutation.
    /// Acceptance Evidence #1: "kaydedilen veriler sunucudan aynı şekilde yeniden yüklenir".
    /// </summary>
    public DailyMenuView ReloadDailyMenuFromServer(DailyMenuView serverSnapshot)
    {
        ArgumentNullException.ThrowIfNull(serverSnapshot);
        _dailyMenus[serverSnapshot.Id] = serverSnapshot;
        return _dailyMenus[serverSnapshot.Id];
    }

    /// <summary>
    /// Reloads recipe record from server snapshot without mutation.
    /// Acceptance Evidence #1: "kaydedilen veriler sunucudan aynı şekilde yeniden yüklenir".
    /// </summary>
    public RecipeView ReloadRecipeFromServer(RecipeView serverSnapshot)
    {
        ArgumentNullException.ThrowIfNull(serverSnapshot);
        _recipes[serverSnapshot.Id] = serverSnapshot;
        return _recipes[serverSnapshot.Id];
    }

    #endregion

    #region Operations UI Accessibility Compliance (V0-CMP-005, WCAG 2.2 AA)

    /// <summary>
    /// Retrieves accessibility metadata for an interactive UI element adhering to V0-CMP-005:
    /// minimum target size 44px (2.5.8), explicit Turkish labeling (3.3.2), keyboard focusability (2.1.1).
    /// </summary>
    public static AccessibilityUiMetadata GetAccessibilityMetadata(string elementId)
    {
        return elementId switch
        {
            "btn-create-menu" => new AccessibilityUiMetadata(
                elementId,
                AriaLabel: "Yeni statik menü oluştur",
                Role: "button",
                IsKeyboardFocusable: true,
                MinTargetSizePx: 48,
                HelpText: "Yeni bir statik menü tanımı başlatır.",
                HighContrastCompliance: true),

            "btn-add-menu-item" => new AccessibilityUiMetadata(
                elementId,
                AriaLabel: "Menüye ürün kalemi ekle",
                Role: "button",
                IsKeyboardFocusable: true,
                MinTargetSizePx: 44,
                HelpText: "Seçili menüye yeni katalog ürünü ve fiyat ekler.",
                HighContrastCompliance: true),

            "btn-create-daily-menu" => new AccessibilityUiMetadata(
                elementId,
                AriaLabel: "Günlük menü oluştur",
                Role: "button",
                IsKeyboardFocusable: true,
                MinTargetSizePx: 48,
                HelpText: "Belirtilen tarih ve öğün için günlük menü planı başlatır.",
                HighContrastCompliance: true),

            "btn-publish-daily-menu" => new AccessibilityUiMetadata(
                elementId,
                AriaLabel: "Günlük menüyü yayınla",
                Role: "button",
                IsKeyboardFocusable: true,
                MinTargetSizePx: 44,
                HelpText: "Porsiyon sayaçlarını kilitleyerek günlük menüyü servise açar.",
                HighContrastCompliance: true),

            "btn-create-recipe-draft" => new AccessibilityUiMetadata(
                elementId,
                AriaLabel: "Yeni reçete taslak versiyonu oluştur",
                Role: "button",
                IsKeyboardFocusable: true,
                MinTargetSizePx: 48,
                HelpText: "Mevcut reçeteden dallanan yeni bir değişmez taslak versiyon oluşturur.",
                HighContrastCompliance: true),

            "btn-activate-recipe-version" => new AccessibilityUiMetadata(
                elementId,
                AriaLabel: "Reçete versiyonunu üretime al ve aktif et",
                Role: "button",
                IsKeyboardFocusable: true,
                MinTargetSizePx: 44,
                HelpText: "Taslak reçeteyi üretime yetkili aktif sürüm yapar.",
                HighContrastCompliance: true),

            _ => new AccessibilityUiMetadata(
                elementId,
                AriaLabel: elementId,
                Role: "region",
                IsKeyboardFocusable: false,
                MinTargetSizePx: 44,
                HelpText: null,
                HighContrastCompliance: true)
        };
    }

    #endregion
}
