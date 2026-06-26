namespace ApexWallet.Api.Modules.UserModule
{
    public class UpdateProfileDto
    {
        public required string FullName { get; set; }
        public required string Email { get; set; }
    }
}