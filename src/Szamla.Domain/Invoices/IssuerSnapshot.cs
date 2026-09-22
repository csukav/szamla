namespace Szamla.Domain.Invoices;

/// <summary>
/// A copy of the issuing tenant's company details as they were at issue time, embedded in the
/// invoice. Required for immutability: if the tenant later edits its company profile (address,
/// bank account...), already-issued invoices must keep showing what was true when they were issued.
/// </summary>
public sealed record IssuerSnapshot(
    string Name,
    string TaxId,
    string Address,
    string? BankAccount,
    bool IsVatExempt);
