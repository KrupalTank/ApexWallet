using System;
using System.Collections.Generic;

namespace ApexWallet.Api.Database.Models;

public partial class User
{
    public int Userid { get; set; }

    public string Fullname { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Passwordhash { get; set; } = null!;

    public string Identitynumberencrypted { get; set; } = null!;

    public DateTime Createdat { get; set; }

    // 📌 Tracks the lifecycle state of the ledger account (e.g., "Active", "Locked")
    public string AccountStatus { get; set; } = "Active";

    public virtual Wallet? Wallet { get; set; }
}
