using ALKAROS.Cash.Contracts;
using ALKAROS.Cash.TenderHandler;
using ALKAROS.Host.Composition.Errors;
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
using ALKAROS.Purchasing.Suppliers;
using ALKAROS.Recipes.CostSnapshots;
using ALKAROS.Recipes.Versioning;
using ALKAROS.Reconciliation.CaseFoundation;
using ALKAROS.Reporting.V1Operations;
using ALKAROS.Security.SecretRotation;
using ALKAROS.Settings.TypedSettings;
using Microsoft.AspNetCore.Http;
using Npgsql;
using System.Runtime.CompilerServices;
using Xunit;

namespace ALKAROS.Host.Errors.Tests;

/// <summary>
/// V1-RMD-431: every status, code and Turkish message the thirteen per-area MapError switches and the DualScreen
/// default returned before the shared handler (generated from their last source), resolved through the one
/// <see cref="ApiExceptionHandler"/>. A changed code or message fails here before any client sees it.
/// </summary>
public sealed class ApiErrorCatalogTests
{
    public static TheoryData<string?, Exception, int, string, string> FormerMappings => new()
    {
        { "AuthorizationDecisions", Uninitialized<AuthorizationDecisionUnauthorizedException>(), 401, "UNAUTHORIZED", "Yönetici ya da vardiya sorumlusu oturumu gerekiyor." },
        { "AuthorizationDecisions", Uninitialized<AuthorizationDeniedException>(), 403, "FORBIDDEN", "Bu ekran için rapor görüntüleme izni gerekiyor." },
        { "AuthorizationDecisions", Uninitialized<AuthorizationGrantAlreadyResolvedException>(), 409, "ALREADY_RESOLVED", "Bu talep başka biri tarafından zaten yanıtlandı." },
        { "AuthorizationDecisions", Uninitialized<AuthorizationSelfApprovalException>(), 403, "SELF_APPROVAL_NOT_ALLOWED", "Kendi talebinizi onaylayamaz ya da reddedemezsiniz." },
        { "AuthorizationDecisions", Uninitialized<BehaviouralTighteningAlreadyClearedException>(), 409, "ALREADY_CLEARED", "Bu kısıtlama zaten kaldırılmış." },
        { "AuthorizationDecisions", Uninitialized<BehaviouralTighteningSelfClearException>(), 403, "SELF_APPROVAL_NOT_ALLOWED", "Kendinize uygulanan kısıtlamayı kaldıramazsınız." },
        { "AuthorizationDecisions", Uninitialized<DelegatorLacksPermissionException>(), 403, "DELEGATOR_LACKS_PERMISSION", "Sahip olmadığınız bir yetkiyi devredemezsiniz." },
        { "AuthorizationDecisions", Uninitialized<DelegationGranteeNotFoundException>(), 404, "GRANTEE_NOT_FOUND", "Yetki devredilecek kullanıcı bulunamadı." },
        { "AuthorizationDecisions", Uninitialized<BadHttpRequestException>(), 400, "VALIDATION_FAILED", "İstek doğrulanamadı." },
        { "AuthorizationDecisions", Uninitialized<ArgumentException>(), 400, "VALIDATION_FAILED", "İstek doğrulanamadı." },
        { "AuthorizationDecisions", Uninitialized<NpgsqlException>(), 503, "DATABASE_UNAVAILABLE", "Karar kaydedilemedi." },
        { "CatalogManagement", Uninitialized<CatalogUnauthorizedException>(), 401, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş." },
        { "CatalogManagement", Uninitialized<AuthorizationDeniedException>(), 403, "FORBIDDEN", "Katalog yönetimi izni gerekiyor." },
        { "CatalogManagement", Pg("23505", "products_sku_key"), 409, "DUPLICATE_SKU", "Bu stok koduyla (SKU) bir ürün zaten var." },
        { "CatalogManagement", Pg("23505", null), 409, "DUPLICATE_RESOURCE", "Bu katalog kaydı zaten var." },
        { "CatalogManagement", Pg("23P01", null), 409, "OVERLAPPING_EFFECTIVE_PRICE", "Bu fiyatın geçerlilik aralığı mevcut bir aralıkla çakışıyor." },
        { "CatalogManagement", Pg("23503", null), 400, "REFERENCE_NOT_FOUND", "Başvurulan katalog kaydı bulunamadı." },
        { "CatalogManagement", Pg("23514", null), 400, "VALIDATION_FAILED", "İstek bir veri kısıtını ihlal ediyor." },
        { "CatalogManagement", Uninitialized<ArgumentException>(), 400, "VALIDATION_FAILED", "İstek doğrulanamadı." },
        { "CatalogManagement", Uninitialized<BadHttpRequestException>(), 400, "VALIDATION_FAILED", "İstek doğrulanamadı." },
        { "CatalogManagement", Uninitialized<InvalidOperationException>(), 409, "CONCURRENCY_CONFLICT", "Katalog kaydı başka bir işlem tarafından değiştirildi." },
        { "CatalogManagement", Uninitialized<NpgsqlException>(), 503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı." },
        { "CustomerCreditTerms", Uninitialized<CustomerCreditUnauthorizedException>(), 401, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş." },
        { "CustomerCreditTerms", Uninitialized<AuthorizationDeniedException>(), 403, "FORBIDDEN", "Kredi limitini yalnız yönetici değiştirebilir." },
        { "CustomerCreditTerms", Uninitialized<CustomerCreditCustomerNotFoundException>(), 404, "NOT_FOUND", "Müşteri bulunamadı veya anonimleştirilmiş." },
        { "CustomerCreditTerms", Uninitialized<ArgumentException>(), 400, "VALIDATION_FAILED", "Kredi limiti 0 veya üstü (en çok iki ondalık), vade 1-365 gün olmalı." },
        { "CustomerCreditTerms", Uninitialized<BadHttpRequestException>(), 400, "VALIDATION_FAILED", "Kredi limiti 0 veya üstü (en çok iki ondalık), vade 1-365 gün olmalı." },
        { "CustomerCreditTerms", new NpgsqlException("sample"), 503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı." },
        { "MenuManagement", Uninitialized<MenuManagementUnauthorizedException>(), 401, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş." },
        { "MenuManagement", Uninitialized<AuthorizationDeniedException>(), 403, "FORBIDDEN", "Menü yönetimi izni gerekiyor." },
        { "MenuManagement", Uninitialized<MenuNotFoundException>(), 404, "NOT_FOUND", "İstenen kayıt bulunamadı." },
        { "MenuManagement", Uninitialized<MenuItemNotFoundException>(), 404, "NOT_FOUND", "İstenen kayıt bulunamadı." },
        { "MenuManagement", Uninitialized<DailyMenuNotFoundException>(), 404, "NOT_FOUND", "İstenen kayıt bulunamadı." },
        { "MenuManagement", Uninitialized<ALKAROS.Menu.DailyMenuLifecycle.DailyMenuItemNotFoundException>(), 404, "NOT_FOUND", "İstenen kayıt bulunamadı." },
        { "MenuManagement", Uninitialized<CatalogProductNotFoundException>(), 404, "NOT_FOUND", "İstenen kayıt bulunamadı." },
        { "MenuManagement", Uninitialized<DuplicateMenuCodeException>(), 409, "DUPLICATE_CODE", "Bu kodla bir menü zaten var." },
        { "MenuManagement", Uninitialized<DuplicateMenuItemProductException>(), 409, "DUPLICATE_PRODUCT", "Bu ürün menüye zaten eklendi." },
        { "MenuManagement", Uninitialized<DuplicateDailyMenuItemException>(), 409, "DUPLICATE_PRODUCT", "Bu ürün menüye zaten eklendi." },
        { "MenuManagement", Uninitialized<DuplicateDailyMenuBusinessDateException>(), 409, "DUPLICATE_BUSINESS_DATE", "Bu tarih için zaten bir günlük menü var." },
        { "MenuManagement", Uninitialized<DailyMenuClosedException>(), 409, "DAILY_MENU_CLOSED", "Günlük menü kapatıldığı için değiştirilemez." },
        { "MenuManagement", Uninitialized<InvalidDailyMenuTransitionException>(), 409, "INVALID_TRANSITION", "Günlük menü bu durumda bu işlemi kabul etmiyor." },
        { "MenuManagement", Uninitialized<InvalidMenuCommandException>(), 400, "VALIDATION_FAILED", "İstek doğrulanamadı." },
        { "MenuManagement", Uninitialized<InvalidDailyMenuOperationException>(), 400, "VALIDATION_FAILED", "İstek doğrulanamadı." },
        { "MenuManagement", Pg("23505", null), 409, "DUPLICATE_RESOURCE", "Aynı kimlikte bir kayıt zaten var." },
        { "MenuManagement", Pg("23503", null), 400, "REFERENCE_NOT_FOUND", "Başvurulan bir kayıt mevcut değil." },
        { "MenuManagement", Pg("23514", null), 400, "VALIDATION_FAILED", "İstek bir veri kısıtını ihlal ediyor." },
        { "MenuManagement", Uninitialized<ArgumentException>(), 400, "VALIDATION_FAILED", "İstek doğrulanamadı." },
        { "MenuManagement", Uninitialized<BadHttpRequestException>(), 400, "VALIDATION_FAILED", "İstek doğrulanamadı." },
        { "MenuManagement", Uninitialized<NpgsqlException>(), 503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı." },
        { "Observability", Uninitialized<ObservabilityUnauthorizedException>(), 401, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş." },
        { "Observability", Uninitialized<AuthorizationDeniedException>(), 403, "FORBIDDEN", "Gözlemlenebilirlik yönetimi için yeterli izin yok." },
        { "Observability", Uninitialized<AlertNotFoundException>(), 404, "NOT_FOUND", "İstenen alarm bulunamadı." },
        { "Observability", Uninitialized<ALKAROS.Host.Experience.Observability.HealthCheckNotFoundException>(), 404, "NOT_FOUND", "İstenen sağlık kontrolü bulunamadı." },
        { "Observability", Uninitialized<InvalidAlertStateException>(), 409, "INVALID_OPERATION", "Bu işlem alarmın şu anki durumuyla uyumlu değil." },
        { "Observability", Uninitialized<AlertConcurrencyException>(), 409, "CONCURRENCY_CONFLICT", "Alarm başka bir işlem tarafından değiştirildi." },
        { "Observability", Uninitialized<UnapprovedRetentionPolicyException>(), 400, "VALIDATION_FAILED", "Onaylanmamış bir saklama politikası kimliği kullanıldı." },
        { "Observability", Pg("23505", null), 409, "DUPLICATE_RESOURCE", "Aynı kimlikte bir kayıt zaten var." },
        { "Observability", Uninitialized<ArgumentException>(), 400, "VALIDATION_FAILED", "İstek doğrulanamadı." },
        { "Observability", Uninitialized<BadHttpRequestException>(), 400, "VALIDATION_FAILED", "İstek doğrulanamadı." },
        { "Observability", Uninitialized<NpgsqlException>(), 503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı." },
        { "ProductionManagement", Uninitialized<ProductionManagementUnauthorizedException>(), 401, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş." },
        { "ProductionManagement", Uninitialized<AuthorizationDeniedException>(), 403, "FORBIDDEN", "Üretim yönetimi izni gerekiyor." },
        { "ProductionManagement", Uninitialized<ProductionBatchNotFoundException>(), 404, "NOT_FOUND", "İstenen üretim partisi bulunamadı." },
        { "ProductionManagement", Uninitialized<ProductionBatchDuplicateNumberException>(), 409, "DUPLICATE_BATCH_NUMBER", "Bu numarayla bir üretim partisi zaten var." },
        { "ProductionManagement", Uninitialized<InvalidProductionBatchTransitionException>(), 409, "INVALID_TRANSITION", "Üretim partisi bu durumda bu işlemi kabul etmiyor." },
        { "ProductionManagement", Uninitialized<ALKAROS.Production.BatchLifecycle.RecipeVersionImmutableException>(), 409, "RECIPE_VERSION_IMMUTABLE", "Reçete sürümü bu partide değiştirilemez." },
        { "ProductionManagement", Uninitialized<ProductionBatchConcurrencyException>(), 409, "CONCURRENCY_CONFLICT", "Üretim partisi başka bir işlem tarafından değiştirildi." },
        { "ProductionManagement", Uninitialized<InvalidProductionBatchQuantityException>(), 400, "VALIDATION_FAILED", "İstek doğrulanamadı." },
        { "ProductionManagement", Uninitialized<InsufficientProductionStockException>(), 409, "INSUFFICIENT_STOCK", "Reçete bileşenleri için yeterli stok yok." },
        { "ProductionManagement", Uninitialized<ProductionBatchUnitMismatchException>(), 409, "BATCH_UNIT_MISMATCH", "Partinin birimi reçetenin verim birimine çevrilemiyor; partiyi reçetenin verim biriminde açın." },
        { "ProductionManagement", Uninitialized<InvalidProductionStockEffectException>(), 400, "VALIDATION_FAILED", "İstek doğrulanamadı." },
        { "ProductionManagement", Pg("23505", null), 409, "DUPLICATE_RESOURCE", "Aynı kimlikte bir kayıt zaten var." },
        { "ProductionManagement", Pg("23503", null), 400, "REFERENCE_NOT_FOUND", "Başvurulan bir kayıt mevcut değil." },
        { "ProductionManagement", Pg("23514", null), 400, "VALIDATION_FAILED", "İstek bir veri kısıtını ihlal ediyor." },
        { "ProductionManagement", Uninitialized<ArgumentException>(), 400, "VALIDATION_FAILED", "İstek doğrulanamadı." },
        { "ProductionManagement", Uninitialized<BadHttpRequestException>(), 400, "VALIDATION_FAILED", "İstek doğrulanamadı." },
        { "ProductionManagement", Uninitialized<NpgsqlException>(), 503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı." },
        { "PurchasingManagement", Uninitialized<PurchasingManagementUnauthorizedException>(), 401, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş." },
        { "PurchasingManagement", Uninitialized<AuthorizationDeniedException>(), 403, "FORBIDDEN", "Satın alma yönetimi izni gerekiyor." },
        { "PurchasingManagement", Uninitialized<SupplierNotFoundException>(), 404, "NOT_FOUND", "İstenen kayıt bulunamadı." },
        { "PurchasingManagement", Uninitialized<PurchaseOrderNotFoundException>(), 404, "NOT_FOUND", "İstenen kayıt bulunamadı." },
        { "PurchasingManagement", Uninitialized<GoodsReceiptNotFoundException>(), 404, "NOT_FOUND", "İstenen kayıt bulunamadı." },
        { "PurchasingManagement", Uninitialized<DuplicateSupplierCodeException>(), 409, "DUPLICATE_CODE", "Bu kodla bir tedarikçi zaten var." },
        { "PurchasingManagement", Uninitialized<DuplicateSupplierTaxNumberException>(), 409, "DUPLICATE_TAX_NUMBER", "Bu vergi numarasıyla bir tedarikçi zaten var." },
        { "PurchasingManagement", Uninitialized<DuplicateGoodsReceiptException>(), 409, "DUPLICATE_RECEIPT_NUMBER", "Bu numarayla bir mal kabul fişi zaten var." },
        { "PurchasingManagement", Uninitialized<InactiveSupplierException>(), 409, "SUPPLIER_INACTIVE", "Tedarikçi pasif; sipariş kabul edemez." },
        { "PurchasingManagement", Uninitialized<PurchaseOrderStatusException>(), 409, "INVALID_STATUS", "Sipariş bu durumda bu işlemi kabul etmiyor." },
        { "PurchasingManagement", Uninitialized<VarianceReasonRequiredException>(), 400, "VARIANCE_REASON_REQUIRED", "Sipariş edilenden farklı miktar için gerekçe zorunlu." },
        { "PurchasingManagement", Uninitialized<OverReceiptApprovalRequiredException>(), 409, "APPROVAL_REQUIRED", "Tolerans üstü fazla teslimat için yönetici onayı gerekiyor." },
        { "PurchasingManagement", Uninitialized<InvalidSupplierDataException>(), 400, "VALIDATION_FAILED", "İstek doğrulanamadı." },
        { "PurchasingManagement", Uninitialized<InvalidPurchaseOrderException>(), 400, "VALIDATION_FAILED", "İstek doğrulanamadı." },
        { "PurchasingManagement", Uninitialized<InvalidGoodsReceiptException>(), 400, "VALIDATION_FAILED", "İstek doğrulanamadı." },
        { "PurchasingManagement", Uninitialized<SupplierAccessDeniedException>(), 403, "FORBIDDEN", "Bu tedarikçi verisine erişim izniniz yok." },
        { "PurchasingManagement", Pg("23505", null), 409, "DUPLICATE_RESOURCE", "Aynı kimlikte bir kayıt zaten var." },
        { "PurchasingManagement", Pg("23503", null), 400, "REFERENCE_NOT_FOUND", "Başvurulan bir kayıt mevcut değil." },
        { "PurchasingManagement", Pg("23514", null), 400, "VALIDATION_FAILED", "İstek bir veri kısıtını ihlal ediyor." },
        { "PurchasingManagement", Uninitialized<ArgumentException>(), 400, "VALIDATION_FAILED", "İstek doğrulanamadı." },
        { "PurchasingManagement", Uninitialized<BadHttpRequestException>(), 400, "VALIDATION_FAILED", "İstek doğrulanamadı." },
        { "PurchasingManagement", Uninitialized<NpgsqlException>(), 503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı." },
        { "RecipeCostSnapshots", Uninitialized<RecipeCostSnapshotUnauthorizedException>(), 401, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş." },
        { "RecipeCostSnapshots", Uninitialized<AuthorizationDeniedException>(), 403, "FORBIDDEN", "Reçete maliyeti yönetimi izni gerekiyor." },
        { "RecipeCostSnapshots", Uninitialized<RecipeVersionNotFoundException>(), 404, "NOT_FOUND", "İstenen reçete sürümü bulunamadı." },
        { "RecipeCostSnapshots", Uninitialized<DuplicateCostSnapshotException>(), 409, "DUPLICATE_RESOURCE", "Bu tarihte bir maliyet anlık görüntüsü zaten var." },
        { "RecipeCostSnapshots", Uninitialized<MissingCostBasisException>(), 400, "MISSING_COST_BASIS", "Bir malzeme için maliyet verisi bulunamadı." },
        { "RecipeCostSnapshots", Uninitialized<MissingStockUnitMappingException>(), 400, "MISSING_STOCK_UNIT_MAPPING", "Bir malzeme için stok takip birimi belirtilmedi." },
        { "RecipeCostSnapshots", Uninitialized<InvalidCostSnapshotException>(), 400, "VALIDATION_FAILED", "İstek doğrulanamadı." },
        { "RecipeCostSnapshots", Pg("23505", null), 409, "DUPLICATE_RESOURCE", "Aynı kimlikte bir kayıt zaten var." },
        { "RecipeCostSnapshots", Pg("23503", null), 400, "REFERENCE_NOT_FOUND", "Başvurulan bir kayıt mevcut değil." },
        { "RecipeCostSnapshots", Uninitialized<ArgumentException>(), 400, "VALIDATION_FAILED", "İstek doğrulanamadı." },
        { "RecipeCostSnapshots", Uninitialized<NpgsqlException>(), 503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı." },
        { "ReconciliationCases", Uninitialized<ReconciliationCaseUnauthorizedException>(), 401, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş." },
        { "ReconciliationCases", Uninitialized<AuthorizationDeniedException>(), 403, "FORBIDDEN", "Mutabakat vakası yönetimi için yeterli izin yok." },
        { "ReconciliationCases", Uninitialized<CaseNotFoundException>(), 404, "NOT_FOUND", "İstenen mutabakat vakası bulunamadı." },
        { "ReconciliationCases", Uninitialized<InvalidCaseStatusTransitionException>(), 409, "INVALID_OPERATION", "Bu durum geçişi şu anki vaka durumuyla uyumlu değil." },
        { "ReconciliationCases", Uninitialized<ReconciliationConcurrencyException>(), 409, "CONCURRENCY_CONFLICT", "Vaka başka bir işlem tarafından değiştirildi." },
        { "ReconciliationCases", Pg("23505", null), 409, "DUPLICATE_RESOURCE", "Aynı kimlikte bir kayıt zaten var." },
        { "ReconciliationCases", Uninitialized<ArgumentException>(), 400, "VALIDATION_FAILED", "İstek doğrulanamadı." },
        { "ReconciliationCases", Uninitialized<BadHttpRequestException>(), 400, "VALIDATION_FAILED", "İstek doğrulanamadı." },
        { "ReconciliationCases", Uninitialized<NpgsqlException>(), 503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı." },
        { "EndOfDay", Uninitialized<EndOfDayUnauthorizedException>(), 401, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş." },
        { "EndOfDay", Uninitialized<AuthorizationDeniedException>(), 403, "FORBIDDEN", "Gün sonu raporu/işlemi için yeterli izin yok." },
        { "EndOfDay", Uninitialized<BusinessDayNotFoundException>(), 404, "NOT_FOUND", "İstenen iş günü bulunamadı." },
        { "EndOfDay", Uninitialized<BusinessDayAlreadyOpenException>(), 409, "DUPLICATE_RESOURCE", "Bu tarih için iş günü zaten açık." },
        { "EndOfDay", Uninitialized<InvalidBusinessDayOperationException>(), 409, "INVALID_OPERATION", "Bu işlem şu anki gün durumuyla uyumlu değil." },
        { "EndOfDay", Pg("23505", null), 409, "DUPLICATE_RESOURCE", "Aynı kimlikte bir kayıt zaten var." },
        { "EndOfDay", Uninitialized<ArgumentException>(), 400, "VALIDATION_FAILED", "İstek doğrulanamadı." },
        { "EndOfDay", Uninitialized<BadHttpRequestException>(), 400, "VALIDATION_FAILED", "İstek doğrulanamadı." },
        { "EndOfDay", Uninitialized<NpgsqlException>(), 503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı." },
        { "RoleManagement", Uninitialized<RoleManagementUnauthorizedException>(), 401, "UNAUTHORIZED", "Yönetici ya da vardiya sorumlusu oturumu gerekiyor." },
        { "RoleManagement", Uninitialized<AuthorizationDeniedException>(), 403, "FORBIDDEN", "Rol ya da yetki yönetimi izni gerekiyor." },
        { "RoleManagement", Uninitialized<InvalidOperationException>(), 409, "ROLE_MANAGEMENT_CONFLICT", "Bu rol, yetki ya da kullanıcı adı zaten var veya belirtilen yetki bulunamadı." },
        { "RoleManagement", Uninitialized<BadHttpRequestException>(), 400, "VALIDATION_FAILED", "İstek doğrulanamadı." },
        { "RoleManagement", Uninitialized<ArgumentException>(), 400, "VALIDATION_FAILED", "İstek doğrulanamadı." },
        { "RoleManagement", Uninitialized<NpgsqlException>(), 503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı." },
        { "SecurityAdministration", Uninitialized<SecurityAdministrationUnauthorizedException>(), 401, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş." },
        { "SecurityAdministration", Uninitialized<AuthorizationDeniedException>(), 403, "FORBIDDEN", "Güvenlik yönetimi için yeterli izin yok." },
        { "SecurityAdministration", Uninitialized<SecretRotationConflictException>(), 409, "INVALID_OPERATION", "Bu işlem sürümün mevcut durumuyla uyumlu değil." },
        { "SecurityAdministration", Uninitialized<SecretRotationConcurrencyException>(), 409, "CONCURRENCY_CONFLICT", "Sürüm kaydı başka bir işlem tarafından değiştirildi." },
        { "SecurityAdministration", Uninitialized<ArgumentException>(), 400, "VALIDATION_FAILED", "İstek doğrulanamadı." },
        { "SecurityAdministration", Uninitialized<BadHttpRequestException>(), 400, "VALIDATION_FAILED", "İstek doğrulanamadı." },
        { "SecurityAdministration", Uninitialized<NpgsqlException>(), 503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı." },
        { "SettingsManagement", Uninitialized<SettingsManagementUnauthorizedException>(), 401, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş." },
        { "SettingsManagement", Uninitialized<AuthorizationDeniedException>(), 403, "FORBIDDEN", "Ayar yönetimi izni gerekiyor." },
        { "SettingsManagement", Uninitialized<SettingNotFoundException>(), 404, "NOT_FOUND", "İstenen ayar bulunamadı." },
        { "SettingsManagement", Uninitialized<SecretSettingsStorageBanException>(), 400, "SECRET_KEY_BANNED", "Bu anahtar gizli bilgi deposu kuralını ihlal ediyor." },
        { "SettingsManagement", Uninitialized<SettingTypeValidationException>(), 400, "VALIDATION_FAILED", "Değer, ayarın türüyle uyuşmuyor." },
        { "SettingsManagement", Uninitialized<SettingConcurrencyException>(), 409, "CONCURRENCY_CONFLICT", "Ayar başka bir işlem tarafından değiştirildi." },
        { "SettingsManagement", Uninitialized<DuplicateSettingKeyException>(), 409, "DUPLICATE_RESOURCE", "Bu anahtarla bir ayar zaten var." },
        { "SettingsManagement", Pg("23505", null), 409, "DUPLICATE_RESOURCE", "Aynı kimlikte bir kayıt zaten var." },
        { "SettingsManagement", Pg("23503", null), 400, "REFERENCE_NOT_FOUND", "Başvurulan bir kayıt mevcut değil." },
        { "SettingsManagement", Pg("23514", null), 400, "VALIDATION_FAILED", "İstek bir veri kısıtını ihlal ediyor." },
        { "SettingsManagement", Uninitialized<ArgumentException>(), 400, "VALIDATION_FAILED", "İstek doğrulanamadı." },
        { "SettingsManagement", Uninitialized<BadHttpRequestException>(), 400, "VALIDATION_FAILED", "İstek doğrulanamadı." },
        { "SettingsManagement", Uninitialized<NpgsqlException>(), 503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı." },
        { null, Uninitialized<DualScreenUnauthorizedException>(), 401, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş." },
        { null, Uninitialized<DualScreenForbiddenException>(), 403, "FORBIDDEN", "Bu işlem için yetkiniz yok." },
        { null, Uninitialized<AuthorizationDeniedException>(), 403, "FORBIDDEN", "Bu işlem için yetkiniz yok." },
        { null, Uninitialized<DualScreenNotFoundException>(), 404, "NOT_FOUND", "İstenen kayıt bulunamadı." },
        { null, Uninitialized<DualScreenConflictException>(), 409, "CONCURRENT_MODIFICATION", "Kayıt başka bir işlem tarafından değiştirildi." },
        { null, Uninitialized<SubmitOrderIdempotencyConflictException>(), 409, "IDEMPOTENCY_CONFLICT", "İşlem anahtarı farklı bir istekle kullanılmış." },
        { null, Uninitialized<OrderNotFoundException>(), 404, "ORDER_NOT_FOUND", "Sipariş bulunamadı." },
        { null, Uninitialized<CashSessionNotFoundException>(), 404, "CASH_SESSION_NOT_FOUND", "Kasa oturumu bulunamadı." },
        { null, Uninitialized<ActiveCashSessionExistsException>(), 409, "ACTIVE_CASH_SESSION_EXISTS", "Bu terminalde zaten açık bir kasa oturumu var." },
        { null, Uninitialized<InvalidCashSessionStateException>(), 409, "INVALID_CASH_SESSION_STATE", "Kasa oturumu bu işlem için uygun durumda değil." },
        { null, Uninitialized<CashVarianceThresholdExceededException>(), 409, "CASH_VARIANCE_THRESHOLD_EXCEEDED", "Fark tolerans sınırını aşıyor; süpervizör onayı gerekiyor." },
        { null, Uninitialized<NegativeCashAmountException>(), 400, "VALIDATION_FAILED", "Tutar negatif olamaz." },
        { null, Uninitialized<CashTenderBillNotFoundException>(), 404, "BILL_NOT_FOUND", "Hesap bulunamadı." },
        { null, Uninitialized<ClosedCashSessionException>(), 409, "CLOSED_CASH_SESSION", "Kasa oturumu açık değil." },
        { null, Uninitialized<InsufficientCashTenderException>(), 400, "INSUFFICIENT_CASH_TENDER", "Verilen tutar hesaplanan tutarı karşılamıyor." },
        { null, Uninitialized<CashTenderIdempotencyKeyReusedException>(), 409, "TENDER_IDEMPOTENCY_KEY_REUSED", "İşlem kimliği başka bir tahsilat için zaten kullanılmış." },
        { null, Uninitialized<CashTenderUnsettledPaymentExistsException>(), 409, "TENDER_UNSETTLED_PAYMENT_EXISTS", "Bu hesapta çözülmemiş bir kart ödemesi var; nakit almadan önce kart ödemesinin sonucu netleştirilmeli." },
        { null, Uninitialized<BillNotPayableException>(), 409, "TENDER_BILL_NOT_PAYABLE", "Bu hesap iptal edilmiş; tahsilat alınamaz. Hesabı yenileyin." },
        { null, Uninitialized<OverAllocationException>(), 409, "OVER_ALLOCATION", "İstenen tutar hesabın kalan bakiyesini aşıyor." },
        { null, Uninitialized<ArgumentException>(), 400, "VALIDATION_FAILED", "İstek doğrulanamadı." },
        { null, Uninitialized<BadHttpRequestException>(), 400, "VALIDATION_FAILED", "İstek doğrulanamadı." },
        { null, Pg("XX000", null), 503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı." },
        { null, new TimeoutException("sample"), 500, "INTERNAL_ERROR", "İşlem tamamlanamadı." },
    };

    [Theory]
    [MemberData(nameof(FormerMappings))]
    public void TheSharedHandlerKeepsEveryFormerStatusCodeAndMessage(
        string? area, Exception exception, int status, string code, string message)
    {
        var entered = area is null ? [] : new[] { Area(area) };

        var mapped = ApiExceptionHandler.Resolve(entered, exception);

        Assert.Equal((status, code, message), mapped);
    }

    [Fact]
    public void AnExceptionAnAreaDoesNotCatchFallsThroughToTheHostDefault()
    {
        var mapped = ApiExceptionHandler.Resolve(
            [ApiErrorCatalog.CatalogManagement], Uninitialized<DualScreenNotFoundException>());

        Assert.Equal((404, "NOT_FOUND", "İstenen kayıt bulunamadı."), mapped);
    }

    [Fact]
    public void AnAreaWithNoAnswerPassesTheExceptionToTheNextAreaOut()
    {
        // A caught type with no matching arm used to be rethrown to the next handler out.
        var inner = new ApiErrorArea([typeof(InvalidOperationException)], _ => null);

        var mapped = ApiExceptionHandler.Resolve(
            [ApiErrorCatalog.CatalogManagement, inner], new InvalidOperationException("sample"));

        Assert.Equal((409, "CONCURRENCY_CONFLICT", "Katalog kaydı başka bir işlem tarafından değiştirildi."), mapped);
    }

    [Fact]
    public void TheInnermostAreaThatCatchesTheExceptionAnswers()
    {
        var mapped = ApiExceptionHandler.Resolve(
            [ApiErrorCatalog.CatalogManagement, ApiErrorCatalog.MenuManagement], Uninitialized<AuthorizationDeniedException>());

        Assert.Equal((403, "FORBIDDEN", "Menü yönetimi izni gerekiyor."), mapped);
    }

    private static ApiErrorArea Area(string name)
        => (ApiErrorArea)typeof(ApiErrorCatalog).GetField(name)!.GetValue(null)!;

    private static T Uninitialized<T>()
        where T : Exception
        => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

    private static PostgresException Pg(string sqlState, string? constraintName)
        => new(
            "sample", "ERROR", "ERROR", sqlState,
            constraintName: constraintName);
}
