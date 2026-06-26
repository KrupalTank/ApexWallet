using ApexWallet.Api.Database.Models;
using ApexWallet.Api.Security;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ApexWallet.Api.Services;

namespace ApexWallet.Api.Modules.Authentication
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    //[Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApexWalletDbContext _context;
        private readonly ICryptoService _cryptoService;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService; // 📌 Add this
        public AuthController(ApexWalletDbContext context, ICryptoService cryptoService, IConfiguration configuration, IEmailService emailService)
        {
            _context = context;
            _cryptoService = cryptoService;
            _configuration = configuration;
            _emailService = emailService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            // 1. Validation check: Ensure the email isn't already taken
            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
            {
                return BadRequest(new { message = "Email is already registered." });
            }

            // 2. Hash the password using BCrypt
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            // 3. Encrypt sensitive fields using our custom AES service
            string encryptedId = _cryptoService.Encrypt(dto.IdentityNumber);

            // 4. Map DTO to Database Entity
            var newUser = new User
            {
                Fullname = dto.FullName,
                Email = dto.Email,
                Passwordhash = passwordHash,
                Identitynumberencrypted = encryptedId,
                Createdat = DateTime.Now
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // 5. Automatically spin up a default wallet for the new user
            var newWallet = new Wallet
            {
                Userid = newUser.Userid,
                Balance = 0.0000m,
                Currency = "INR",
                Updatedat = DateTime.Now
            };

            _context.Wallets.Add(newWallet);
            await _context.SaveChangesAsync();

            // --- SEND WELCOME EMAIL EVENT ---
            try
            {
                string welcomeBody = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; padding: 20px; border: 1px solid #eee;'>
                <h2 style='color: #003366;'>Welcome to ApexWallet! 🚀</h2>
                <p>Dear {newUser.Fullname},</p>
                <p>Thank you for creating an account with ApexWallet. Your enterprise-grade digital wallet has been successfully provisioned and is ready for use.</p>
                <p><b>Your Account Details:</b></p>
                <ul>
                    <li><b>Registered Email:</b> {newUser.Email}</li>
                    <li><b>Account Status:</b> Active</li>
                </ul>
                <hr style='border: none; border-top: 1px solid #eee;' />
                <p style='font-size: 12px; color: #777;'>This is an automated system security notification. Please do not reply directly to this email.</p>
            </div>";

                await _emailService.SendEmailAsync(newUser.Email, "Welcome to ApexWallet - Account Activated", welcomeBody);
            }
            catch (Exception ex)
            {
                // Background safety catch prevents network drops from crashing the registration success response
                Serilog.Log.Warning("User account created, but welcome notification dispatch failed: " + ex.Message);
            }


            return Ok(new { message = "User registered successfully and wallet initialized." });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.Passwordhash))
            {
                return Unauthorized(new { message = "Invalid email or password." });
            }

            // Generate Token
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Userid.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("FullName", user.Fullname)
            };

            var token = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(double.Parse(jwtSettings["DurationInMinutes"]!)),
                Issuer = jwtSettings["Issuer"],
                Audience = jwtSettings["Audience"],
                SigningCredentials = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var createdToken = tokenHandler.CreateToken(token);

            return Ok(new
            {
                message = "Login successful.",
                token = tokenHandler.WriteToken(createdToken)
            });
        }

        [Authorize] // 🔒 Lock this action to logged-in users only!
        [HttpPost("changepassword")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            // 1. Extract the secure caller ID from token claims
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { message = "Invalid verification credentials." });
            }

            // 2. Query user records using exact lowercased schema property mappings
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Userid == userId);
            if (user == null) return NotFound(new { message = "User records not found." });

            // 3. Verification step: Ensure old password matches current database hash
            bool isOldPasswordValid = BCrypt.Net.BCrypt.Verify(dto.OldPassword, user.Passwordhash);
            if (!isOldPasswordValid)
            {
                return BadRequest(new { message = "The old password you supplied is incorrect." });
            }

            // 4. Prevention rule: Do not let new password match the old one
            if (dto.OldPassword == dto.NewPassword)
            {
                return BadRequest(new { message = "Your new password cannot match your current password." });
            }

            // 5. Hash and persist the new password securely
            user.Passwordhash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            await _context.SaveChangesAsync();

            // --- SEND PASSWORD MODIFICATION EMAIL ---
            try
            {
                string passwordAlertBody = $"<h3>Security Alert: Password Changed</h3><p>Dear {user.Fullname},</p><p>The password for your ApexWallet account was successfully changed just now.</p><p>If you did not authorize this change, please lock down your account profile immediately.</p>";
                await _emailService.SendEmailAsync(user.Email, "Security Alert: Password Modified", passwordAlertBody);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning("Password modified, but notification dispatch failed: " + ex.Message);
            }

            return Ok(new { message = "Account password updated successfully." });
        }

        [HttpPost("lock-account")]
        [Authorize] // Requires a valid logged-in JWT token
        public async Task<IActionResult> EmergencyLockAccount()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            // Flip state to Locked
            user.AccountStatus = "Locked";
            await _context.SaveChangesAsync();

            // Send a security email confirming the freeze action
            try
            {
                string body = $"<h3>🚨 Account Frozen Successfully</h3><p>Dear {user.Fullname},</p><p>As requested, your ApexWallet account has been placed into a <b>Locked Security State</b>.</p><p>All deposit, transfer, and update workflows have been blocked. Please reach out to your system administrator to verify your identity and reactivate your account.</p>";
                await _emailService.SendEmailAsync(user.Email, "SECURITY ALERT: Wallet Frozen", body);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning("Account locked, but notification failed: " + ex.Message);
            }

            return Ok(new { message = "Account successfully frozen." });
        }
    }
}