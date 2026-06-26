using System;
using System.Collections.Generic;

namespace ApexWallet.Api.Database.Models;

public partial class Wallet
{
    public int Walletid { get; set; }

    public int Userid { get; set; }

    public decimal Balance { get; set; }

    public string Currency { get; set; } = null!;

    public DateTime Updatedat { get; set; }

    public virtual ICollection<Transaction> TransactionReceiverwallets { get; set; } = new List<Transaction>();

    public virtual ICollection<Transaction> TransactionSenderwallets { get; set; } = new List<Transaction>();

    public virtual User User { get; set; } = null!;
}
