namespace ApexWallet.Api.Modules.Authentication
{
    public class RegisterDto
    {
        public required string FullName { get; set; }
        public required string Email { get; set; }
        public required string Password { get; set; }
        public required string IdentityNumber { get; set; } // This will be AES-encrypted
    }
}