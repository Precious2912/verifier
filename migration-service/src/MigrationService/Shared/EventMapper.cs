using EventStore.Core.Events;
using MigrationService.Crud;

namespace MigrationService.Shared;

public static class EventMapper
{
    public static (string StreamKey, object Event)? Map(CrudTransaction t)
    {
        return t.Type switch
        {
            "Debit" => (t.DebitAccount, new AccountDebited(
                Reference: t.Reference,
                DebitAccount: t.DebitAccount,
                CreditAccount: t.CreditAccount,
                Amount: t.Amount,
                OccurredAt: t.CreatedAt)),

            "Credit" => (t.CreditAccount, new AccountCredited(
                Reference: t.Reference,
                DebitAccount: t.DebitAccount,
                CreditAccount: t.CreditAccount,
                Amount: t.Amount,
                OccurredAt: t.CreatedAt)),

            _ => null
        };
    }

    // public static (string StreamKey, object Event) MapAccount(CrudAccount a) =>
    //     (a.AccountNumber, new AccountCreated(a.AccountNumber, a.AccountName, a.CreatedAt));
}