namespace ApexWallet.Api.Modules.WalletModule
{
    public class TransferDto
    {
        public int ReceiverUserId { get; set; }
        public decimal Amount { get; set; }
    }
}