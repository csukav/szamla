namespace Szamla.Application.Common.Constants;

/// <summary>The four MVP roles, per tenant: tulajdonos, adminisztrátor, számlázó, csak olvasó.</summary>
public static class Roles
{
    public const string Owner = "Owner";
    public const string Admin = "Admin";
    public const string Invoicer = "Invoicer";
    public const string ReadOnly = "ReadOnly";

    public static readonly IReadOnlyCollection<string> All = [Owner, Admin, Invoicer, ReadOnly];
}
