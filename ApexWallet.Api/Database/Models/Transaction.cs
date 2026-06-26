using System;
using System.Collections.Generic;

namespace ApexWallet.Api.Database.Models;

public partial class Transaction
{
    public int Transactionid { get; set; }

    public int? Senderwalletid { get; set; }

    public int? Receiverwalletid { get; set; }

    public decimal Amount { get; set; }

    public string Transactiontype { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime Timestamp { get; set; }

    public virtual Wallet? Receiverwallet { get; set; }

    public virtual Wallet? Senderwallet { get; set; }
}
