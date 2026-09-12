namespace ALKAROS.Settings.GarsonFeatureToggles;

using ALKAROS.Settings.TypedSettings;

/// <summary>
/// V1-SET-004 (Semih, 2026-09-12: wants the ability to turn optional
/// features off entirely, per business) — per-deployment on/off for every
/// optional Garson feature. ALKAROS is single-tenant per deployment (one Host process
/// per business, confirmed — no shared "business_id" table anywhere),
/// so "turn a feature off for a business" means "turn it off for this
/// whole Host". Mirrors <see cref="ReservationStation.ReservationStationSetting"/>'s
/// exact pattern (a <see cref="SettingScope.Global"/> <see cref="SettingDataType.Toggle"/>,
/// self-registering the first time it's asked) but consolidated into one
/// shared enum + helper instead of one near-identical file per feature —
/// ten features would otherwise mean ten copies of the same twelve lines.
///
/// Deliberately opt-OUT, not opt-IN: every one of these features already
/// ships active for every deployment today, so the default is
/// <c>true</c> (behaves exactly as before this task) — unlike
/// <c>ReservationStationSetting</c>'s own opt-in default (a genuinely new
/// capability most deployments don't want yet). A business that wants a
/// feature off has an operator explicitly flip it, once, at setup.
/// </summary>
public enum GarsonFeature
{
    /// <summary>V1-WTR-025: OrderItem.CourseNumber, FireRound course-splitting, the /fire-course endpoint, the course picker in the product sheet.</summary>
    CourseManagement,
    /// <summary>V1-WTR-022: OrderItem.SeatId, the seat picker in the product sheet.</summary>
    SeatAssignment,
    /// <summary>V1-WTR-012: PersonalCompBudgetEscalationResolver's own daily/per-item caps that let a waiter comp without manager escalation.</summary>
    PersonalCompBudget,
    /// <summary>V1-WTR-013: the shift-handoff context note (notifications.serving_handoff_notes, /handoff-note/pop).</summary>
    ShiftHandoffNotes,
    /// <summary>V1-WTR-014: the real-time call-for-help signal to manager/supervisor (HelpRequestHub).</summary>
    HelpRequest,
    /// <summary>V1-WTR-018: the guest-facing read-only live bill reachable from the table's own QR code (DualScreenStore.GetLiveBillAsync, /api/v1/qr/bill).</summary>
    GuestLiveBill,
    /// <summary>V1-WTR-020: the voluntary tip line on the bill (BillAdjustment.CreateTip, POST .../bills/{billId}/tip).</summary>
    VoluntaryTip,
    /// <summary>V1-WTR-021: the self-view-only shift summary (ShiftSummaryStore, GET /my-shift-summary).</summary>
    ShiftSummary,
    /// <summary>V1-WTR-015: party size (kuver) captured on a table's first round (Order.PartySize).</summary>
    PartySize,
}

public static class GarsonFeatureToggles
{
    private const string ModuleOwner = "waiter";

    private static readonly IReadOnlyDictionary<GarsonFeature, (string Key, string Description)> Definitions =
        new Dictionary<GarsonFeature, (string, string)>
        {
            [GarsonFeature.CourseManagement] = (
                "garson.course_management_enabled",
                "Kurs sıralama/beklet/ateşle özelliği (V1-WTR-025). Kapatılırsa yeni " +
                "kalemler kurs numarası taşımaz ve /fire-course uç noktası reddedilir."),
            [GarsonFeature.SeatAssignment] = (
                "garson.seat_assignment_enabled",
                "Sipariş kalemini bir koltuğa atama (V1-WTR-022). Kapatılırsa kat planı " +
                "olsa bile hiçbir kalem bir koltuğa atanmaz."),
            [GarsonFeature.PersonalCompBudget] = (
                "garson.personal_comp_budget_enabled",
                "Garsonun kendi günlük ikram bütçesi dahilinde yönetici onayı olmadan " +
                "ikram yapabilmesi (V1-WTR-012). Kapatılırsa her ikram yönetici onayı gerektirir."),
            [GarsonFeature.ShiftHandoffNotes] = (
                "garson.shift_handoff_notes_enabled",
                "Masa devrinde bağlam notu bırakma (V1-WTR-013)."),
            [GarsonFeature.HelpRequest] = (
                "garson.help_request_enabled",
                "Garsonun yöneticiye gerçek zamanlı yardım çağrısı gönderebilmesi (V1-WTR-014)."),
            [GarsonFeature.GuestLiveBill] = (
                "garson.guest_live_bill_enabled",
                "Misafirin masasının QR koduyla kendi adisyonunu salt-okunur görebilmesi (V1-WTR-018)."),
            [GarsonFeature.VoluntaryTip] = (
                "garson.voluntary_tip_enabled",
                "Hesapta gönüllü bahşiş satırı (V1-WTR-020). Kapatma, yalnız bu " +
                "isteğe bağlı satırı gizler — hiçbir zaman zorunlu bir servis " +
                "ücretine dönüştürülemez (2026-01-30 tarihli mevzuat, bkz. görev notu)."),
            [GarsonFeature.ShiftSummary] = (
                "garson.shift_summary_enabled",
                "Garsonun kendi vardiya özetini görebilmesi (V1-WTR-021)."),
            [GarsonFeature.PartySize] = (
                "garson.party_size_enabled",
                "Masanın ilk turunda kişi sayısı (kuver) sorulması (V1-WTR-015)."),
        };

    public static string KeyFor(GarsonFeature feature) => Definitions[feature].Key;

    // V1-SET-004: PostgresSettingsRepository.RegisterSettingAsync is a plain
    // check-then-insert (a SELECT, then an INSERT in a later statement), not
    // an atomic upsert — two requests racing to register the SAME never-yet-
    // registered key for the first time (the very first help request /
    // table-draft / etc. this deployment ever handles) can both pass the
    // check, and the loser's INSERT then fails the *database's* unique
    // constraint directly, not the repository's own app-level
    // DuplicateSettingKeyException the catch below expects — an unhandled
    // exception the caller never asked for. A process-wide lock serializes
    // first-registration within this Host; it does nothing to (and is not
    // needed for) the ordinary read path once every key already exists.
    private static readonly SemaphoreSlim RegistrationLock = new(1, 1);

    /// <summary>
    /// Registers every feature's setting (default <c>true</c>) if it does
    /// not already exist. Safe to call repeatedly — an already-registered
    /// key is a no-op, never resets an operator's chosen value.
    /// </summary>
    public static async Task EnsureAllRegisteredAsync(ISettingsService settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await RegistrationLock.WaitAsync(cancellationToken);
        try
        {
            foreach (var (key, description) in Definitions.Values)
            {
                try
                {
                    await settings.RegisterSettingAsync(
                        new RegisterSettingRequest(
                            key,
                            "true",
                            SettingDataType.Toggle,
                            SettingScope.Global,
                            ModuleOwner,
                            Description: description),
                        cancellationToken);
                }
                catch (DuplicateSettingKeyException)
                {
                    // Already registered — an earlier boot, or a previous
                    // deployment of this same database. Leave the operator's
                    // chosen value alone.
                }
            }
        }
        finally
        {
            RegistrationLock.Release();
        }
    }

    /// <summary>
    /// Whether this deployment has <paramref name="feature"/> turned on.
    /// Registers every feature's setting (all at their default, on) the
    /// first time any one of them is asked, so no separate startup hook
    /// is needed — same self-registering shape as
    /// <c>ReservationStationSetting.IsEnabledAsync</c>.
    /// </summary>
    public static async Task<bool> IsEnabledAsync(
        ISettingsService settings, GarsonFeature feature, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var key = KeyFor(feature);
        var record = await settings.GetRecordAsync(key, cancellationToken);
        if (record is null)
        {
            await EnsureAllRegisteredAsync(settings, cancellationToken);
            return true;
        }

        return await settings.GetValueOrDefaultAsync(key, true, cancellationToken);
    }

    /// <summary>
    /// Every feature's current on/off value in one round trip — the shape
    /// a client-facing "which features are active" endpoint needs, so it
    /// does not issue nine separate lookups per request.
    /// </summary>
    public static async Task<IReadOnlyDictionary<GarsonFeature, bool>> GetAllAsync(
        ISettingsService settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var result = new Dictionary<GarsonFeature, bool>();
        foreach (var feature in Definitions.Keys)
        {
            result[feature] = await IsEnabledAsync(settings, feature, cancellationToken);
        }

        return result;
    }
}

/// <summary>
/// V1-SET-004: a caller reached an endpoint whose whole feature is turned
/// off for this deployment (e.g. <c>/fire-course</c> when
/// <see cref="GarsonFeature.CourseManagement"/> is off). Distinct from
/// <c>AuthorizationDeniedException</c> — this is not a permission
/// problem, the action is simply not offered here at all.
/// </summary>
public sealed class GarsonFeatureDisabledException : Exception
{
    public GarsonFeatureDisabledException(GarsonFeature feature)
        : base($"Feature '{feature}' is disabled for this deployment.")
    {
        Feature = feature;
    }

    public GarsonFeature Feature { get; }
}
