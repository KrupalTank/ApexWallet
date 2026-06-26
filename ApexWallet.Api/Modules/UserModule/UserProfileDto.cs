using System;

namespace ApexWallet.Api.Modules.UserModule
{
    public class UserProfileDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string DecryptedIdentityNumber { get; set; } = null!;
        public DateTime AccountCreated { get; set; }
    }
}