using System;

namespace ApexWallet.Api.Modules.WalletModule
{
    public class TransactionHistoryDto
    {
        public int TransactionId { get; set; }
        public int SenderWalletId { get; set; }
        public int ReceiverWalletId { get; set; }
        public decimal Amount { get; set; }
        public string TransactionType { get; set; } = null!;
        public string Status { get; set; } = null!;
        public DateTime Timestamp { get; set; }
        public string Role { get; set; } = null!; // Tells the app if the user is the "Sender" or "Receiver"
    }
}