using ApexWallet.Api.Database.Models;
using ApexWallet.Api.Security;
using ApexWallet.Api.Services;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ApexWallet.Api.Modules.UserModule
{
    [Authorize] // Lock down profile data to token owners
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    //[Route("api/[controller]")]

    public class UserController : ControllerBase
    {
        private readonly ApexWalletDbContext _context;
        private readonly ICryptoService _cryptoService;
        private readonly FluentValidation.IValidator<UpdateProfileDto> _profileValidator; // 📌 Add this
        private readonly IEmailService _emailService; // 📌 Add this
        public UserController(
            ApexWalletDbContext context,
            ICryptoService cryptoService,
            FluentValidation.IValidator<UpdateProfileDto> profileValidator, IEmailService emailService) // 📌 Inject this
        {
            _context = context;
            _cryptoService = cryptoService;
            _profileValidator = profileValidator;
            _emailService = emailService;
        }

        [HttpPut("update")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            // 1. Run FluentValidation pipeline checks instantly
            var validationResult = await _profileValidator.ValidateAsync(dto);
            if (!validationResult.IsValid) return BadRequest(validationResult.Errors);

            // 2. Extract the authenticated user's ID from the JWT token
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { message = "Invalid account verification token." });
            }

            // 3. Query the user record from PostgreSQL using lowercased property names
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Userid == userId);
            if (user == null) return NotFound(new { message = "User record not found." });

            string oldEmail = user.Email;
            bool emailChanged = oldEmail != dto.Email;
            // 4. Defensive check: If the email is being changed, ensure it's not already taken by someone else
            if (user.Email != dto.Email)
            {
                bool emailExists = await _context.Users.AnyAsync(u => u.Email == dto.Email && u.Userid != userId);
                if (emailExists)
                {
                    return BadRequest(new { message = "This email address is already registered to another account." });
                }
            }

            // 5. Update the entity values
            user.Fullname = dto.FullName;
            user.Email = dto.Email;

            // 6. Save modifications back to PostgreSQL
            await _context.SaveChangesAsync();

            try
            {
                string body = $"<h3>Security Notification</h3><p>Dear {user.Fullname},</p><p>Your ApexWallet account profile details were recently updated.</p>";

                if (emailChanged)
                {
                    body += $"<p>Your account email address was changed from <b>{oldEmail}</b> to <b>{dto.Email}</b>.</p>";
                    // Fire to both old and new addresses as requested
                    await _emailService.SendEmailAsync(oldEmail, "Security Alert: Email Address Updated", body);
                    await _emailService.SendEmailAsync(dto.Email, "Security Alert: Email Address Updated", body);
                }
                else
                {
                    body += "<p>Your profile name information has been modified.</p>";
                    await _emailService.SendEmailAsync(user.Email, "Security Alert: Profile Updated", body);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning("Profile updated, but notification dispatch failed: " + ex.Message);
            }

            return Ok(new { message = "Profile details updated successfully.", email = user.Email, fullName = user.Fullname });
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            // 1. Identify who is asking based on their secure token claims
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { message = "Invalid account verification token." });
            }

            // 2. Query the user details from PostgreSQL using lowercased property names
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Userid == userId);
            if (user == null)
            {
                return NotFound(new { message = "User profile records not found." });
            }

            // 3. Cryptographic Inversion: Decrypt the AES string back to plain text for the user
            string plainIdentity = "Decryption Failed";
            try
            {
                plainIdentity = _cryptoService.Decrypt(user.Identitynumberencrypted);
            }
            catch
            {
                // Safety catch block if database contents were tampered with manually
            }

            // 4. Map data to your clean presentation DTO
            var profileDetails = new UserProfileDto
            {
                UserId = user.Userid,
                FullName = user.Fullname,
                Email = user.Email,
                DecryptedIdentityNumber = plainIdentity,
                AccountCreated = user.Createdat
            };

            return Ok(profileDetails);
        }
        //[HttpGet("search")]
        [HttpGet("lookup/search")]
        public async Task<IActionResult> SearchBeneficiaries([FromQuery] string query)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(query) || query.Length < 3)
            {
                return BadRequest(new { message = "Search query must be at least 3 characters long." });
            }

            var wildCardPattern = $"%{query}%";

            // 📌 Query combining Users and Wallets matching exact lowercase properties
            var matchedUsers = await _context.Users
                .Where(u => u.Userid != currentUserId && EF.Functions.ILike(u.Fullname, wildCardPattern))
                .Join(_context.Wallets,
                      user => user.Userid,
                      wallet => wallet.Userid,
                      (user, wallet) => new BeneficiarySearchResultDto
                      {
                          UserId = user.Userid,
                          WalletId = wallet.Walletid, // 📌 Fetch the correct ledger target ID!
                          FullName = user.Fullname,
                          Email = user.Email
                      })
                .Take(10)
                .ToListAsync();

            return Ok(matchedUsers);
        }

        // 📌 GET /api/v1/user/lookup/locked-list
        [HttpGet("lookup/locked-list")]
        public async Task<IActionResult> GetLockedAccounts()
        {
            // Fetches all users where AccountStatus matches Locked
            var lockedUsers = await _context.Users
                .Where(u => u.AccountStatus == "Locked")
                .Select(u => new { u.Userid, u.Fullname, u.Email, u.AccountStatus })
                .ToListAsync();

            return Ok(lockedUsers);
        }

        // 📌 POST /api/v1/user/lookup/reactivate/{id}
        [HttpPost("lookup/reactivate/{id}")]
        public async Task<IActionResult> ReactivateAccount(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            // Reset status back to default operational state
            user.AccountStatus = "Active";
            await _context.SaveChangesAsync();

            try
            {
                string body = $"<h3>🔓 Account Reactivated</h3><p>Dear {user.Fullname},</p><p>Your ApexWallet account has been successfully verified and reactivated by an administrator. All wallet operations are now active.</p>";
                await _emailService.SendEmailAsync(user.Email, "Account Update: Wallet Reactivated", body);
            }
            catch (Exception ex) { Serilog.Log.Warning("Email notification failed: " + ex.Message); }

            return Ok(new { message = "Account reactivated." });
        }
    }
}