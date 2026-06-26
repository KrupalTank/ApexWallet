namespace ApexWallet.Api.Modules.UserModule
{
    public class BeneficiarySearchResultDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public int WalletId { get; set; } 
    }
}