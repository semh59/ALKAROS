// V1-RMD-431: generated from the thirteen per-area MapError switches it replaces; each arm is unchanged.
using ALKAROS.Cash.Contracts;
using ALKAROS.Cash.TenderHandler;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Experience.Authorization;
using ALKAROS.Host.Experience.Catalog;
using ALKAROS.Host.Experience.CustomerCredit;
using ALKAROS.Host.Experience.Menu;
using ALKAROS.Host.Experience.Observability;
using ALKAROS.Host.Experience.Production;
using ALKAROS.Host.Experience.Purchasing;
using ALKAROS.Host.Experience.Recipes;
using ALKAROS.Host.Experience.Reconciliation;
using ALKAROS.Host.Experience.Reporting;
using ALKAROS.Host.Experience.Roles;
using ALKAROS.Host.Experience.SecurityAdministration;
using ALKAROS.Host.Experience.Settings;
using ALKAROS.Identity.Authorization.Behavioural;
using ALKAROS.Identity.Authorization.Grants;
using ALKAROS.Identity.Authorization;
using ALKAROS.Menu.CounterProjection;
using ALKAROS.Menu.DailyMenuLifecycle;
using ALKAROS.Menu.StaticMenu;
using ALKAROS.Observability.AlertFoundation;
using ALKAROS.Observability.Foundation;
using ALKAROS.Orders.SubmitOrder;
using ALKAROS.Payments.Allocations.Persistence;
using ALKAROS.Production.BatchLifecycle;
using ALKAROS.Production.StockEffects;
using ALKAROS.Purchasing.OrdersAndReceipts;
using ALKAROS.Purchasing.PurchaseInvoices;
using ALKAROS.Purchasing.Suppliers;
using ALKAROS.Recipes.CostSnapshots;
using ALKAROS.Recipes.Versioning;
using ALKAROS.Reconciliation.CaseFoundation;
using ALKAROS.Reporting.V1Operations;
using ALKAROS.Security.SecretRotation;
using ALKAROS.Settings.TypedSettings;
using Microsoft.AspNetCore.Http;
using Npgsql;

namespace ALKAROS.Host.Composition.Errors;

/// <summary>
/// V1-RMD-431: the one table of Host API error answers. Each area lists the exception types its endpoint filter
/// answers for and, in order, the status, code and Turkish message for each; the arms are the former per-area
/// MapError switches, unchanged, so every client keeps the codes and messages it already shows.
/// <see cref="Default"/> is the former DualScreen mapping for everything no area answers.
/// </summary>
public static class ApiErrorCatalog
{
    /// <summary>Authorization/AuthorizationDecisionEndpoints.</summary>
    public static readonly ApiErrorArea AuthorizationDecisions = new(
        [
            typeof(AuthorizationDecisionUnauthorizedException),
            typeof(AuthorizationDeniedException),
            typeof(AuthorizationGrantAlreadyResolvedException),
            typeof(AuthorizationSelfApprovalException),
            typeof(BehaviouralTighteningAlreadyClearedException),
            typeof(BehaviouralTighteningSelfClearException),
            typeof(DelegatorLacksPermissionException),
            typeof(DelegationGranteeNotFoundException),
            typeof(BadHttpRequestException),
            typeof(ArgumentException),
            typeof(NpgsqlException),
        ],
        exception => exception switch
        {
            AuthorizationDecisionUnauthorizedException =>
                (StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Yönetici ya da vardiya sorumlusu oturumu gerekiyor."),
            AuthorizationDeniedException =>
                (StatusCodes.Status403Forbidden, "FORBIDDEN", "Bu ekran için rapor görüntüleme izni gerekiyor."),
            AuthorizationGrantAlreadyResolvedException =>
                (StatusCodes.Status409Conflict, "ALREADY_RESOLVED", "Bu talep başka biri tarafından zaten yanıtlandı."),
            AuthorizationSelfApprovalException =>
                (StatusCodes.Status403Forbidden, "SELF_APPROVAL_NOT_ALLOWED", "Kendi talebinizi onaylayamaz ya da reddedemezsiniz."),
            BehaviouralTighteningAlreadyClearedException =>
                (StatusCodes.Status409Conflict, "ALREADY_CLEARED", "Bu kısıtlama zaten kaldırılmış."),
            BehaviouralTighteningSelfClearException =>
                (StatusCodes.Status403Forbidden, "SELF_APPROVAL_NOT_ALLOWED", "Kendinize uygulanan kısıtlamayı kaldıramazsınız."),
            DelegatorLacksPermissionException =>
                (StatusCodes.Status403Forbidden, "DELEGATOR_LACKS_PERMISSION", "Sahip olmadığınız bir yetkiyi devredemezsiniz."),
            DelegationGranteeNotFoundException =>
                (StatusCodes.Status404NotFound, "GRANTEE_NOT_FOUND", "Yetki devredilecek kullanıcı bulunamadı."),
            BadHttpRequestException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            ArgumentException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            NpgsqlException =>
                (StatusCodes.Status503ServiceUnavailable, "DATABASE_UNAVAILABLE", "Karar kaydedilemedi."),
            _ => null,
        });

    /// <summary>Catalog/CatalogManagementEndpoints.</summary>
    public static readonly ApiErrorArea CatalogManagement = new(
        [
            typeof(CatalogUnauthorizedException),
            typeof(AuthorizationDeniedException),
            typeof(PostgresException),
            typeof(NpgsqlException),
            typeof(BadHttpRequestException),
            typeof(ArgumentException),
            typeof(InvalidOperationException),
        ],
        exception => exception switch
        {
            CatalogUnauthorizedException =>
                (StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
            AuthorizationDeniedException =>
                (StatusCodes.Status403Forbidden, "FORBIDDEN", "Katalog yönetimi izni gerekiyor."),
            PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "products_sku_key" } =>
                (StatusCodes.Status409Conflict, "DUPLICATE_SKU", "Bu stok koduyla (SKU) bir ürün zaten var."),
            PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } =>
                (StatusCodes.Status409Conflict, "DUPLICATE_RESOURCE", "Bu katalog kaydı zaten var."),
            PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation } =>
                (StatusCodes.Status409Conflict, "OVERLAPPING_EFFECTIVE_PRICE", "Bu fiyatın geçerlilik aralığı mevcut bir aralıkla çakışıyor."),
            PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } =>
                (StatusCodes.Status400BadRequest, "REFERENCE_NOT_FOUND", "Başvurulan katalog kaydı bulunamadı."),
            PostgresException { SqlState: PostgresErrorCodes.CheckViolation or PostgresErrorCodes.NumericValueOutOfRange } =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek bir veri kısıtını ihlal ediyor."),
            ArgumentException or BadHttpRequestException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            InvalidOperationException =>
                (StatusCodes.Status409Conflict, "CONCURRENCY_CONFLICT", "Katalog kaydı başka bir işlem tarafından değiştirildi."),
            NpgsqlException =>
                (StatusCodes.Status503ServiceUnavailable, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
            _ => null,
        });

    /// <summary>CustomerCredit/CustomerCreditTermsEndpoints.</summary>
    public static readonly ApiErrorArea CustomerCreditTerms = new(
        [
            typeof(CustomerCreditUnauthorizedException),
            typeof(AuthorizationDeniedException),
            typeof(CustomerCreditCustomerNotFoundException),
            typeof(ArgumentException),
            typeof(BadHttpRequestException),
            typeof(NpgsqlException),
        ],
        exception => exception switch
        {
            CustomerCreditUnauthorizedException =>
                (StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
            AuthorizationDeniedException =>
                (StatusCodes.Status403Forbidden, "FORBIDDEN", "Kredi limitini yalnız yönetici değiştirebilir."),
            CustomerCreditCustomerNotFoundException =>
                (StatusCodes.Status404NotFound, "NOT_FOUND", "Müşteri bulunamadı veya anonimleştirilmiş."),
            ArgumentException or BadHttpRequestException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED",
                    "Kredi limiti 0 veya üstü (en çok iki ondalık), vade 1-365 gün olmalı."),
            _ =>
                (StatusCodes.Status503ServiceUnavailable, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
        });

    /// <summary>Menu/MenuManagementEndpoints.</summary>
    public static readonly ApiErrorArea MenuManagement = new(
        [
            typeof(MenuManagementUnauthorizedException),
            typeof(AuthorizationDeniedException),
            typeof(MenuException),
            typeof(DailyMenuException),
            typeof(PostgresException),
            typeof(NpgsqlException),
            typeof(BadHttpRequestException),
            typeof(ArgumentException),
        ],
        exception => exception switch
        {
            MenuManagementUnauthorizedException =>
                (StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
            AuthorizationDeniedException =>
                (StatusCodes.Status403Forbidden, "FORBIDDEN", "Menü yönetimi izni gerekiyor."),
            MenuNotFoundException or MenuItemNotFoundException
                or DailyMenuNotFoundException or ALKAROS.Menu.DailyMenuLifecycle.DailyMenuItemNotFoundException
                or CatalogProductNotFoundException =>
                (StatusCodes.Status404NotFound, "NOT_FOUND", "İstenen kayıt bulunamadı."),
            DuplicateMenuCodeException =>
                (StatusCodes.Status409Conflict, "DUPLICATE_CODE", "Bu kodla bir menü zaten var."),
            DuplicateMenuItemProductException or DuplicateDailyMenuItemException =>
                (StatusCodes.Status409Conflict, "DUPLICATE_PRODUCT", "Bu ürün menüye zaten eklendi."),
            DuplicateDailyMenuBusinessDateException =>
                (StatusCodes.Status409Conflict, "DUPLICATE_BUSINESS_DATE", "Bu tarih için zaten bir günlük menü var."),
            DailyMenuClosedException =>
                (StatusCodes.Status409Conflict, "DAILY_MENU_CLOSED", "Günlük menü kapatıldığı için değiştirilemez."),
            InvalidDailyMenuTransitionException =>
                (StatusCodes.Status409Conflict, "INVALID_TRANSITION", "Günlük menü bu durumda bu işlemi kabul etmiyor."),
            InvalidMenuCommandException or InvalidDailyMenuOperationException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } =>
                (StatusCodes.Status409Conflict, "DUPLICATE_RESOURCE", "Aynı kimlikte bir kayıt zaten var."),
            PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } =>
                (StatusCodes.Status400BadRequest, "REFERENCE_NOT_FOUND", "Başvurulan bir kayıt mevcut değil."),
            PostgresException { SqlState: PostgresErrorCodes.CheckViolation or PostgresErrorCodes.NumericValueOutOfRange } =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek bir veri kısıtını ihlal ediyor."),
            ArgumentException or BadHttpRequestException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            NpgsqlException =>
                (StatusCodes.Status503ServiceUnavailable, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
            _ => null,
        });

    /// <summary>Observability/ObservabilityEndpoints.</summary>
    public static readonly ApiErrorArea Observability = new(
        [
            typeof(ObservabilityUnauthorizedException),
            typeof(AuthorizationDeniedException),
            typeof(ALKAROS.Host.Experience.Observability.HealthCheckNotFoundException),
            typeof(AlertException),
            typeof(ObservabilityException),
            typeof(PostgresException),
            typeof(NpgsqlException),
            typeof(ArgumentException),
            typeof(BadHttpRequestException),
        ],
        exception => exception switch
        {
            ObservabilityUnauthorizedException =>
                (StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
            AuthorizationDeniedException =>
                (StatusCodes.Status403Forbidden, "FORBIDDEN", "Gözlemlenebilirlik yönetimi için yeterli izin yok."),
            AlertNotFoundException =>
                (StatusCodes.Status404NotFound, "NOT_FOUND", "İstenen alarm bulunamadı."),
            ALKAROS.Host.Experience.Observability.HealthCheckNotFoundException =>
                (StatusCodes.Status404NotFound, "NOT_FOUND", "İstenen sağlık kontrolü bulunamadı."),
            InvalidAlertStateException =>
                (StatusCodes.Status409Conflict, "INVALID_OPERATION", "Bu işlem alarmın şu anki durumuyla uyumlu değil."),
            AlertConcurrencyException =>
                (StatusCodes.Status409Conflict, "CONCURRENCY_CONFLICT", "Alarm başka bir işlem tarafından değiştirildi."),
            UnapprovedRetentionPolicyException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "Onaylanmamış bir saklama politikası kimliği kullanıldı."),
            PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } =>
                (StatusCodes.Status409Conflict, "DUPLICATE_RESOURCE", "Aynı kimlikte bir kayıt zaten var."),
            ArgumentException or BadHttpRequestException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            NpgsqlException =>
                (StatusCodes.Status503ServiceUnavailable, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
            _ => null,
        });

    /// <summary>Production/ProductionManagementEndpoints.</summary>
    public static readonly ApiErrorArea ProductionManagement = new(
        [
            typeof(ProductionManagementUnauthorizedException),
            typeof(AuthorizationDeniedException),
            typeof(ProductionBatchException),
            typeof(ProductionStockEffectException),
            typeof(PostgresException),
            typeof(NpgsqlException),
            typeof(BadHttpRequestException),
            typeof(ArgumentException),
        ],
        exception => exception switch
        {
            ProductionManagementUnauthorizedException =>
                (StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
            AuthorizationDeniedException =>
                (StatusCodes.Status403Forbidden, "FORBIDDEN", "Üretim yönetimi izni gerekiyor."),
            ProductionBatchNotFoundException =>
                (StatusCodes.Status404NotFound, "NOT_FOUND", "İstenen üretim partisi bulunamadı."),
            ProductionBatchDuplicateNumberException =>
                (StatusCodes.Status409Conflict, "DUPLICATE_BATCH_NUMBER", "Bu numarayla bir üretim partisi zaten var."),
            InvalidProductionBatchTransitionException =>
                (StatusCodes.Status409Conflict, "INVALID_TRANSITION", "Üretim partisi bu durumda bu işlemi kabul etmiyor."),
            ALKAROS.Production.BatchLifecycle.RecipeVersionImmutableException =>
                (StatusCodes.Status409Conflict, "RECIPE_VERSION_IMMUTABLE", "Reçete sürümü bu partide değiştirilemez."),
            ProductionBatchConcurrencyException =>
                (StatusCodes.Status409Conflict, "CONCURRENCY_CONFLICT", "Üretim partisi başka bir işlem tarafından değiştirildi."),
            InvalidProductionBatchQuantityException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            InsufficientProductionStockException =>
                (StatusCodes.Status409Conflict, "INSUFFICIENT_STOCK", "Reçete bileşenleri için yeterli stok yok."),
            ProductionBatchUnitMismatchException =>
                (StatusCodes.Status409Conflict, "BATCH_UNIT_MISMATCH", "Partinin birimi reçetenin verim birimine çevrilemiyor; partiyi reçetenin verim biriminde açın."),
            InvalidProductionStockEffectException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } =>
                (StatusCodes.Status409Conflict, "DUPLICATE_RESOURCE", "Aynı kimlikte bir kayıt zaten var."),
            PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } =>
                (StatusCodes.Status400BadRequest, "REFERENCE_NOT_FOUND", "Başvurulan bir kayıt mevcut değil."),
            PostgresException { SqlState: PostgresErrorCodes.CheckViolation or PostgresErrorCodes.NumericValueOutOfRange } =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek bir veri kısıtını ihlal ediyor."),
            ArgumentException or BadHttpRequestException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            NpgsqlException =>
                (StatusCodes.Status503ServiceUnavailable, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
            _ => null,
        });

    /// <summary>Purchasing/PurchasingManagementEndpoints.</summary>
    public static readonly ApiErrorArea PurchasingManagement = new(
        [
            typeof(PurchasingManagementUnauthorizedException),
            typeof(AuthorizationDeniedException),
            typeof(SupplierException),
            typeof(PurchasingException),
            typeof(PostgresException),
            typeof(NpgsqlException),
            typeof(BadHttpRequestException),
            typeof(ArgumentException),
        ],
        exception => exception switch
        {
            PurchasingManagementUnauthorizedException =>
                (StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
            AuthorizationDeniedException =>
                (StatusCodes.Status403Forbidden, "FORBIDDEN", "Satın alma yönetimi izni gerekiyor."),
            SupplierNotFoundException or PurchaseOrderNotFoundException or GoodsReceiptNotFoundException or PurchaseInvoiceNotFoundException =>
                (StatusCodes.Status404NotFound, "NOT_FOUND", "İstenen kayıt bulunamadı."),
            DuplicatePurchaseInvoiceException =>
                (StatusCodes.Status409Conflict, "DUPLICATE_INVOICE", "Bu fatura (ETTN) daha önce içeri alınmış."),
            PurchaseInvoiceNotReadyException =>
                (StatusCodes.Status409Conflict, "INVOICE_NOT_READY", "Fatura onaya hazır değil: tüm satırlar eşleştirilmeli ve tedarikçi kayıtlı olmalı."),
            PurchaseInvoiceStatusException =>
                (StatusCodes.Status409Conflict, "INVALID_STATUS", "Fatura bu durumda bu işlemi kabul etmiyor."),
            UnsupportedPurchaseDocumentException =>
                (StatusCodes.Status400BadRequest, "UNSUPPORTED_DOCUMENT", "Yalnız alış faturaları içeri alınabilir; iade ve diğer belgeler desteklenmiyor."),
            InvalidPurchaseInvoiceException =>
                (StatusCodes.Status400BadRequest, "INVALID_INVOICE", "Fatura verisi okunamadı, geçersiz veya eksik."),
            DuplicateSupplierCodeException =>
                (StatusCodes.Status409Conflict, "DUPLICATE_CODE", "Bu kodla bir tedarikçi zaten var."),
            DuplicateSupplierTaxNumberException =>
                (StatusCodes.Status409Conflict, "DUPLICATE_TAX_NUMBER", "Bu vergi numarasıyla bir tedarikçi zaten var."),
            DuplicateGoodsReceiptException =>
                (StatusCodes.Status409Conflict, "DUPLICATE_RECEIPT_NUMBER", "Bu numarayla bir mal kabul fişi zaten var."),
            InactiveSupplierException =>
                (StatusCodes.Status409Conflict, "SUPPLIER_INACTIVE", "Tedarikçi pasif; sipariş kabul edemez."),
            PurchaseOrderStatusException =>
                (StatusCodes.Status409Conflict, "INVALID_STATUS", "Sipariş bu durumda bu işlemi kabul etmiyor."),
            VarianceReasonRequiredException =>
                (StatusCodes.Status400BadRequest, "VARIANCE_REASON_REQUIRED", "Sipariş edilenden farklı miktar için gerekçe zorunlu."),
            OverReceiptApprovalRequiredException =>
                (StatusCodes.Status409Conflict, "APPROVAL_REQUIRED", "Tolerans üstü fazla teslimat için yönetici onayı gerekiyor."),
            InvalidSupplierDataException or InvalidPurchaseOrderException or InvalidGoodsReceiptException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            SupplierAccessDeniedException =>
                (StatusCodes.Status403Forbidden, "FORBIDDEN", "Bu tedarikçi verisine erişim izniniz yok."),
            PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } =>
                (StatusCodes.Status409Conflict, "DUPLICATE_RESOURCE", "Aynı kimlikte bir kayıt zaten var."),
            PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } =>
                (StatusCodes.Status400BadRequest, "REFERENCE_NOT_FOUND", "Başvurulan bir kayıt mevcut değil."),
            PostgresException { SqlState: PostgresErrorCodes.CheckViolation or PostgresErrorCodes.NumericValueOutOfRange } =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek bir veri kısıtını ihlal ediyor."),
            ArgumentException or BadHttpRequestException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            NpgsqlException =>
                (StatusCodes.Status503ServiceUnavailable, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
            _ => null,
        });

    /// <summary>Recipes/RecipeCostSnapshotEndpoints.</summary>
    public static readonly ApiErrorArea RecipeCostSnapshots = new(
        [
            typeof(RecipeCostSnapshotUnauthorizedException),
            typeof(AuthorizationDeniedException),
            typeof(RecipeCostSnapshotException),
            typeof(PostgresException),
            typeof(NpgsqlException),
            typeof(ArgumentException),
        ],
        exception => exception switch
        {
            RecipeCostSnapshotUnauthorizedException =>
                (StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
            AuthorizationDeniedException =>
                (StatusCodes.Status403Forbidden, "FORBIDDEN", "Reçete maliyeti yönetimi izni gerekiyor."),
            RecipeVersionNotFoundException =>
                (StatusCodes.Status404NotFound, "NOT_FOUND", "İstenen reçete sürümü bulunamadı."),
            DuplicateCostSnapshotException =>
                (StatusCodes.Status409Conflict, "DUPLICATE_RESOURCE", "Bu tarihte bir maliyet anlık görüntüsü zaten var."),
            MissingCostBasisException =>
                (StatusCodes.Status400BadRequest, "MISSING_COST_BASIS", "Bir malzeme için maliyet verisi bulunamadı."),
            MissingStockUnitMappingException =>
                (StatusCodes.Status400BadRequest, "MISSING_STOCK_UNIT_MAPPING", "Bir malzeme için stok takip birimi belirtilmedi."),
            InvalidCostSnapshotException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } =>
                (StatusCodes.Status409Conflict, "DUPLICATE_RESOURCE", "Aynı kimlikte bir kayıt zaten var."),
            PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } =>
                (StatusCodes.Status400BadRequest, "REFERENCE_NOT_FOUND", "Başvurulan bir kayıt mevcut değil."),
            ArgumentException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            NpgsqlException =>
                (StatusCodes.Status503ServiceUnavailable, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
            _ => null,
        });

    /// <summary>Reconciliation/ReconciliationCaseEndpoints.</summary>
    public static readonly ApiErrorArea ReconciliationCases = new(
        [
            typeof(ReconciliationCaseUnauthorizedException),
            typeof(AuthorizationDeniedException),
            typeof(ReconciliationException),
            typeof(PostgresException),
            typeof(NpgsqlException),
            typeof(ArgumentException),
            typeof(BadHttpRequestException),
        ],
        exception => exception switch
        {
            ReconciliationCaseUnauthorizedException =>
                (StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
            AuthorizationDeniedException =>
                (StatusCodes.Status403Forbidden, "FORBIDDEN", "Mutabakat vakası yönetimi için yeterli izin yok."),
            CaseNotFoundException =>
                (StatusCodes.Status404NotFound, "NOT_FOUND", "İstenen mutabakat vakası bulunamadı."),
            InvalidCaseStatusTransitionException =>
                (StatusCodes.Status409Conflict, "INVALID_OPERATION", "Bu durum geçişi şu anki vaka durumuyla uyumlu değil."),
            ReconciliationConcurrencyException =>
                (StatusCodes.Status409Conflict, "CONCURRENCY_CONFLICT", "Vaka başka bir işlem tarafından değiştirildi."),
            PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } =>
                (StatusCodes.Status409Conflict, "DUPLICATE_RESOURCE", "Aynı kimlikte bir kayıt zaten var."),
            ArgumentException or BadHttpRequestException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            NpgsqlException =>
                (StatusCodes.Status503ServiceUnavailable, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
            _ => null,
        });

    /// <summary>Reporting/EndOfDayEndpoints.</summary>
    public static readonly ApiErrorArea EndOfDay = new(
        [
            typeof(EndOfDayUnauthorizedException),
            typeof(AuthorizationDeniedException),
            typeof(ReportingException),
            typeof(PostgresException),
            typeof(NpgsqlException),
            typeof(ArgumentException),
            typeof(BadHttpRequestException),
        ],
        exception => exception switch
        {
            EndOfDayUnauthorizedException =>
                (StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
            AuthorizationDeniedException =>
                (StatusCodes.Status403Forbidden, "FORBIDDEN", "Gün sonu raporu/işlemi için yeterli izin yok."),
            BusinessDayNotFoundException =>
                (StatusCodes.Status404NotFound, "NOT_FOUND", "İstenen iş günü bulunamadı."),
            BusinessDayAlreadyOpenException =>
                (StatusCodes.Status409Conflict, "DUPLICATE_RESOURCE", "Bu tarih için iş günü zaten açık."),
            InvalidBusinessDayOperationException =>
                (StatusCodes.Status409Conflict, "INVALID_OPERATION", "Bu işlem şu anki gün durumuyla uyumlu değil."),
            PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } =>
                (StatusCodes.Status409Conflict, "DUPLICATE_RESOURCE", "Aynı kimlikte bir kayıt zaten var."),
            ArgumentException or BadHttpRequestException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            NpgsqlException =>
                (StatusCodes.Status503ServiceUnavailable, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
            _ => null,
        });

    /// <summary>Roles/RoleManagementEndpoints.</summary>
    public static readonly ApiErrorArea RoleManagement = new(
        [
            typeof(RoleManagementUnauthorizedException),
            typeof(AuthorizationDeniedException),
            typeof(InvalidOperationException),
            typeof(BadHttpRequestException),
            typeof(ArgumentException),
            typeof(NpgsqlException),
        ],
        exception => exception switch
        {
            RoleManagementUnauthorizedException =>
                (StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Yönetici ya da vardiya sorumlusu oturumu gerekiyor."),
            AuthorizationDeniedException =>
                (StatusCodes.Status403Forbidden, "FORBIDDEN", "Rol ya da yetki yönetimi izni gerekiyor."),
            // V1-RMD-426: the exception's own text is English and names internal codes (UI_STYLE_GUIDE); the
            // cases behind it are an existing role, permission or username, or a missing permission code.
            InvalidOperationException =>
                (StatusCodes.Status409Conflict, "ROLE_MANAGEMENT_CONFLICT",
                    "Bu rol, yetki ya da kullanıcı adı zaten var veya belirtilen yetki bulunamadı."),
            BadHttpRequestException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            ArgumentException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            NpgsqlException =>
                (StatusCodes.Status503ServiceUnavailable, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
            _ => null,
        });

    /// <summary>SecurityAdministration/SecurityAdministrationEndpoints.</summary>
    public static readonly ApiErrorArea SecurityAdministration = new(
        [
            typeof(SecurityAdministrationUnauthorizedException),
            typeof(AuthorizationDeniedException),
            typeof(SecretRotationConflictException),
            typeof(SecretRotationConcurrencyException),
            typeof(PostgresException),
            typeof(NpgsqlException),
            typeof(ArgumentException),
            typeof(BadHttpRequestException),
        ],
        exception => exception switch
        {
            SecurityAdministrationUnauthorizedException =>
                (StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
            AuthorizationDeniedException =>
                (StatusCodes.Status403Forbidden, "FORBIDDEN", "Güvenlik yönetimi için yeterli izin yok."),
            SecretRotationConflictException =>
                (StatusCodes.Status409Conflict, "INVALID_OPERATION", "Bu işlem sürümün mevcut durumuyla uyumlu değil."),
            SecretRotationConcurrencyException =>
                (StatusCodes.Status409Conflict, "CONCURRENCY_CONFLICT", "Sürüm kaydı başka bir işlem tarafından değiştirildi."),
            ArgumentException or BadHttpRequestException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            NpgsqlException =>
                (StatusCodes.Status503ServiceUnavailable, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
            _ => null,
        });

    /// <summary>Settings/SettingsManagementEndpoints.</summary>
    public static readonly ApiErrorArea SettingsManagement = new(
        [
            typeof(SettingsManagementUnauthorizedException),
            typeof(AuthorizationDeniedException),
            typeof(SettingsException),
            typeof(PostgresException),
            typeof(NpgsqlException),
            typeof(BadHttpRequestException),
            typeof(ArgumentException),
        ],
        exception => exception switch
        {
            SettingsManagementUnauthorizedException =>
                (StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
            AuthorizationDeniedException =>
                (StatusCodes.Status403Forbidden, "FORBIDDEN", "Ayar yönetimi izni gerekiyor."),
            SettingNotFoundException =>
                (StatusCodes.Status404NotFound, "NOT_FOUND", "İstenen ayar bulunamadı."),
            SecretSettingsStorageBanException =>
                (StatusCodes.Status400BadRequest, "SECRET_KEY_BANNED", "Bu anahtar gizli bilgi deposu kuralını ihlal ediyor."),
            SettingTypeValidationException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "Değer, ayarın türüyle uyuşmuyor."),
            SettingConcurrencyException =>
                (StatusCodes.Status409Conflict, "CONCURRENCY_CONFLICT", "Ayar başka bir işlem tarafından değiştirildi."),
            DuplicateSettingKeyException =>
                (StatusCodes.Status409Conflict, "DUPLICATE_RESOURCE", "Bu anahtarla bir ayar zaten var."),
            PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } =>
                (StatusCodes.Status409Conflict, "DUPLICATE_RESOURCE", "Aynı kimlikte bir kayıt zaten var."),
            PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } =>
                (StatusCodes.Status400BadRequest, "REFERENCE_NOT_FOUND", "Başvurulan bir kayıt mevcut değil."),
            PostgresException { SqlState: PostgresErrorCodes.CheckViolation or PostgresErrorCodes.NumericValueOutOfRange } =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek bir veri kısıtını ihlal ediyor."),
            ArgumentException or BadHttpRequestException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            NpgsqlException =>
                (StatusCodes.Status503ServiceUnavailable, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
            _ => null,
        });

    /// <summary>The Host default for exceptions no area answers (formerly DualScreenApplication.WriteErrorAsync).</summary>
    public static (int Status, string Code, string Message) Default(Exception exception)
        => exception switch
        {
            DualScreenUnauthorizedException => (401, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
            DualScreenForbiddenException => (403, "FORBIDDEN", "Bu işlem için yetkiniz yok."),
            AuthorizationDeniedException => (403, "FORBIDDEN", "Bu işlem için yetkiniz yok."),
            DualScreenNotFoundException => (404, "NOT_FOUND", "İstenen kayıt bulunamadı."),
            DualScreenConflictException => (409, "CONCURRENT_MODIFICATION", "Kayıt başka bir işlem tarafından değiştirildi."),
            SubmitOrderIdempotencyConflictException => (409, "IDEMPOTENCY_CONFLICT", "İşlem anahtarı farklı bir istekle kullanılmış."),
            OrderNotFoundException => (404, "ORDER_NOT_FOUND", "Sipariş bulunamadı."),
            // V13-CSH-004: Cash/CashTender domain exceptions, most specific
            // first (CashVarianceThresholdExceededException/
            // ActiveCashSessionExistsException/InvalidCashSessionStateException
            // all derive from CashSessionException, so they must precede it).
            CashSessionNotFoundException => (404, "CASH_SESSION_NOT_FOUND", "Kasa oturumu bulunamadı."),
            ActiveCashSessionExistsException => (409, "ACTIVE_CASH_SESSION_EXISTS", "Bu terminalde zaten açık bir kasa oturumu var."),
            InvalidCashSessionStateException => (409, "INVALID_CASH_SESSION_STATE", "Kasa oturumu bu işlem için uygun durumda değil."),
            CashVarianceThresholdExceededException => (409, "CASH_VARIANCE_THRESHOLD_EXCEEDED", "Fark tolerans sınırını aşıyor; süpervizör onayı gerekiyor."),
            NegativeCashAmountException => (400, "VALIDATION_FAILED", "Tutar negatif olamaz."),
            CashSessionException => (400, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            CashTenderBillNotFoundException => (404, "BILL_NOT_FOUND", "Hesap bulunamadı."),
            ClosedCashSessionException => (409, "CLOSED_CASH_SESSION", "Kasa oturumu açık değil."),
            InsufficientCashTenderException => (400, "INSUFFICIENT_CASH_TENDER", "Verilen tutar hesaplanan tutarı karşılamıyor."),
            // V1-RMD-409 (V1-RMD-393 F-04, F-07) and V1-RMD-415 (F-12): the same codes the card/EFT tender route
            // already returns.
            CashTenderIdempotencyKeyReusedException => (409, "TENDER_IDEMPOTENCY_KEY_REUSED", "İşlem kimliği başka bir tahsilat için zaten kullanılmış."),
            CashTenderUnsettledPaymentExistsException => (409, "TENDER_UNSETTLED_PAYMENT_EXISTS", "Bu hesapta çözülmemiş bir kart ödemesi var; nakit almadan önce kart ödemesinin sonucu netleştirilmeli."),
            BillNotPayableException => (409, "TENDER_BILL_NOT_PAYABLE", "Bu hesap iptal edilmiş; tahsilat alınamaz. Hesabı yenileyin."),
            CashTenderException => (400, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            // V13-CSH-004: OverAllocationException (V13-ALC-001) is reachable
            // through the cash-tender endpoint - both this handler's own
            // fail-fast check and AllocateAsync's deeper, lock-guarded one
            // can throw it when the requested amount exceeds what the bill
            // actually has left.
            OverAllocationException => (409, "OVER_ALLOCATION", "İstenen tutar hesabın kalan bakiyesini aşıyor."),
            ArgumentException or BadHttpRequestException => (400, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            PostgresException => (503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
            _ => (500, "INTERNAL_ERROR", "İşlem tamamlanamadı."),
        };
}
