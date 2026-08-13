namespace ALKAROS.Catalog.ProductCatalog;

public enum ProductType
{
    MenuItem = 1,
    Modifier = 2,
    AddOn = 3,
    Packaging = 4,
    ServiceItem = 5,
}

public enum StockMode
{
    Untracked = 1,
    QuantityTracked = 2,
    PortionTracked = 3,
    RecipeDerived = 4,
}

/// <summary>
/// Modifier group selection semantics. Values are defined by the V1-CAT-001
/// task contract (canonical-value-catalog.md section D: selection_type).
/// </summary>
public enum SelectionType
{
    SelectOne = 1,
    SelectMany = 2,
}
