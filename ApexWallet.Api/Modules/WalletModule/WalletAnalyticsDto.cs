namespace ApexWallet.Api.Modules.WalletModule
{
    public class WalletAnalyticsDto
    {
        public decimal TotalDeposited { get; set; }
        public decimal TotalTransferredOut { get; set; }
        public decimal TotalReceivedIn { get; set; }
        public int TotalTransactionCount { get; set; }
        public string MonthName { get; set; } = null!;
    }
}