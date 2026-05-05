namespace PaymentEDA.Contracts.Enums;

public enum PaymentStatus
{
    Initiated,
    Authorized,
    Captured,
    Settled,
    Failed,
    Cancelled,
    Refunded
}

public enum PaymentMethod
{
    CreditCard,
    DebitCard,
    BankTransfer,
    DigitalWallet
}

public enum Currency
{
    TRY,
    USD,
    EUR,
    GBP
}
