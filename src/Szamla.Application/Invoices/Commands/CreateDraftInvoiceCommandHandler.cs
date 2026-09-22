using Microsoft.EntityFrameworkCore;
using Szamla.Application.Common.Cqrs;
using Szamla.Application.Common.Exceptions;
using Szamla.Application.Common.Interfaces;
using Szamla.Domain.Common;
using Szamla.Domain.Invoices;

namespace Szamla.Application.Invoices.Commands;

public sealed class CreateDraftInvoiceCommandHandler(IApplicationDbContext dbContext) : ICommandHandler<CreateDraftInvoiceCommand, Guid>
{
    public async Task<Guid> Handle(CreateDraftInvoiceCommand command, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants.SingleOrDefaultAsync(t => t.Id == command.TenantId, cancellationToken)
            ?? throw new NotFoundException("A bérlő nem található.");
        var partner = await dbContext.Partners
            .SingleOrDefaultAsync(p => p.Id == command.PartnerId && p.TenantId == command.TenantId, cancellationToken)
            ?? throw new NotFoundException("A partner nem található.");

        var issuer = new IssuerSnapshot(tenant.Name, tenant.TaxId, tenant.Address, tenant.BankAccount, tenant.IsVatExempt);
        var partnerSnapshot = new PartnerSnapshot(
            partner.Name, partner.IsPrivatePerson, partner.CountryCategory, partner.CountryCode,
            partner.Address, partner.TaxId, partner.EuVatId, partner.Email);

        var lines = command.Lines
            .Select(l => InvoiceLine.Create(l.Description, l.Quantity, l.Unit, new Money(l.NetUnitPriceAmount, command.Currency), l.VatRate.ToDomain()))
            .ToList();

        var invoice = Invoice.CreateDraft(
            command.TenantId, command.IssueDate, command.PerformanceDate, command.PaymentDueDate, command.PaymentMethod,
            command.Currency, issuer, partnerSnapshot, lines,
            exchangeRate: command.ExchangeRate, exchangeRateSource: command.ExchangeRateSource, exchangeRateDate: command.ExchangeRateDate);

        dbContext.Invoices.Add(invoice);
        await dbContext.SaveChangesAsync(cancellationToken);

        return invoice.Id;
    }
}
