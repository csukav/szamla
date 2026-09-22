namespace Szamla.Application.Common.Constants;

/// <summary>The four MVP roles, per tenant: tulajdonos, adminisztrátor, számlázó, csak olvasó.</summary>
public static class Roles
{
    public const string Owner = "Owner";
    public const string Admin = "Admin";
    public const string Invoicer = "Invoicer";
    public const string ReadOnly = "ReadOnly";

    public static readonly IReadOnlyCollection<string> All = [Owner, Admin, Invoicer, ReadOnly];

    /// <summary>For `[Authorize(Roles = Roles.CanWrite)]` on mutating endpoints — every role except ReadOnly (csak olvasó/könyvelő).</summary>
    public const string CanWrite = $"{Owner},{Admin},{Invoicer}";

    /// <summary>For tenant-configuration endpoints (e.g. invoice series/számlatömb setup) — narrower than CanWrite, since day-to-day invoicing staff shouldn't be creating new series.</summary>
    public const string CanManageSettings = $"{Owner},{Admin}";
}
