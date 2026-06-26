using ApexWallet.Api.Database.Models;
using ApexWallet.Api.Services;
using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ApexWallet.Api.Modules.WalletModule
{
    [Authorize] // 🔒 Lock down this entire controller to authenticated users only
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    //[Route("api/[controller]")]
    [ServiceFilter(typeof(ApexWallet.Api.Filters.AccountStatusCheckFilter))] // 📌 LOCK SWITCH INSTALLED
    public class WalletController : ControllerBase
    {
        private readonly ApexWalletDbContext _context;
        private readonly IValidator<TransferDto> _validator;
        private readonly FluentValidation.IValidator<DepositDto> _depositValidator; // 📌 Add this
        private readonly IEmailService _emailService; // 📌 Add this
        public WalletController(ApexWalletDbContext context, IValidator<TransferDto> validator, FluentValidation.IValidator<DepositDto> depositValidator, IEmailService emailService)
        {
            _context = context;
            _validator = validator;
            _depositValidator = depositValidator;
            _emailService = emailService;
        }

        [HttpPost("transfer")]
        public async Task<IActionResult> Transfer([FromBody] TransferDto dto)
        {
            Response.Headers.Append("Sunset", "Thu, 31 Dec 2026 23:59:59 GMT");
            Response.Headers.Append("Link", "<https://localhost:7284/api/v2/wallet/transfer>; rel='successor-version'");
            //throw new InvalidOperationException("Simulated system database crash during fund transfer processing.");
            var validationResult = await _validator.ValidateAsync(dto);
            if (!validationResult.IsValid)
            {
                return BadRequest(validationResult.Errors);
            }
            // 1. Extract the Sender's User ID dynamically from the JWT claims payload
            var senderUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(senderUserIdClaim) || !int.TryParse(senderUserIdClaim, out int senderUserId))
            {
                return Unauthorized(new { message = "Unauthorized or invalid token claims." });
            }

            // 2. Defensive Validation Check: Prevent transferring to oneself
            if (senderUserId == dto.ReceiverUserId)
            {
                return BadRequest(new { message = "You cannot transfer funds to your own wallet account." });
            }

            // Defensive Validation Check: Ensure amount is positive
            if (dto.Amount <= 0)
            {
                return BadRequest(new { message = "Transfer amount must be greater than zero." });
            }

            // 3. Fetch Sender Wallet matching exact lowercase schema properties
            var senderWallet = await _context.Wallets.FirstOrDefaultAsync(w => w.Userid == senderUserId);
            if (senderWallet == null)
            {
                return BadRequest(new { message = "Sender wallet account not found." });
            }

            // Check for sufficient funds
            if (senderWallet.Balance < dto.Amount)
            {
                return BadRequest(new { message = "Insufficient funds to execute this transfer transaction." });
            }

            // 4. Fetch Receiver Wallet
            var receiverWallet = await _context.Wallets.FirstOrDefaultAsync(w => w.Userid == dto.ReceiverUserId);
            if (receiverWallet == null)
            {
                return BadRequest(new { message = "Recipient account not found." });
            }

            // 5. Atomic Transaction execution: Deduct and Credit balances
            senderWallet.Balance -= dto.Amount;
            senderWallet.Updatedat = DateTime.Now;

            receiverWallet.Balance += dto.Amount;
            receiverWallet.Updatedat = DateTime.Now;

            // 6. Generate an audit trail log inside your Transactions table
            var transactionHistory = new Transaction
            {
                Senderwalletid = senderWallet.Walletid,
                Receiverwalletid = receiverWallet.Walletid,
                Amount = dto.Amount,
                Transactiontype = "Transfer",
                Status = "Success",
                Timestamp = DateTime.Now
            };

            _context.Transactions.Add(transactionHistory);

            // Persist all balance shifts atomically to PostgreSQL
            await _context.SaveChangesAsync();

            // --- SEND TRANSACTION EMAILS ---
            try
            {
                // Fetch user details safely matching exact lowercase schema fields
                var senderUser = await _context.Users.FindAsync(senderWallet.Userid);
                var receiverWalletInfo = await _context.Wallets.FindAsync(dto.ReceiverUserId);
                var receiverUser = receiverWalletInfo != null ? await _context.Users.FindAsync(receiverWalletInfo.Userid) : null;

                if (senderUser != null && receiverUser != null)
                {
                    // Email to Sender (A)
                    string senderBody = $"<h3>Debit Alert!</h3><p>Dear {senderUser.Fullname},</p><p>An amount of <b>{dto.Amount} INR</b> has been transferred from your wallet to {receiverUser.Fullname}.</p><p>Thank you for using ApexWallet.</p>";
                    await _emailService.SendEmailAsync(senderUser.Email, "Transaction Alert: Debit", senderBody);

                    // Email to Receiver (B)
                    string receiverBody = $"<h3>Credit Alert!</h3><p>Dear {receiverUser.Fullname},</p><p>You have received <b>{dto.Amount} INR</b> in your wallet from {senderUser.Fullname}.</p>";
                    await _emailService.SendEmailAsync(receiverUser.Email, "Transaction Alert: Credit", receiverBody);
                }
            }
            catch (Exception ex)
            {
                // Background safety catch: prevents email network issues from rolling back a successful transfer
                Serilog.Log.Warning("Transfer processed, but notification dispatch failed: " + ex.Message);
            }


            return Ok(new
            {
                message = "Transfer transaction completed successfully.",
                newBalance = senderWallet.Balance
            });
        }

        [HttpGet("balance")]
        public async Task<IActionResult> GetBalance()
        {
            // 1. Extract the current User ID dynamically from the JWT claims payload
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { message = "Invalid account verification token." });
            }

            // 2. Query the wallet matching exact lowercase properties
            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.Userid == userId);
            if (wallet == null)
            {
                return NotFound(new { message = "Wallet account profile not found." });
            }

            // 3. Return a clean, targeted dashboard payload
            return Ok(new
            {
                walletId = wallet.Walletid,
                balance = wallet.Balance,
                currency = wallet.Currency,
                lastUpdated = wallet.Updatedat
            });
        }

        [HttpGet("transactions")]
        public async Task<IActionResult> GetTransactionHistory([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            // 1. Identify the logged-in user from their JWT token
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            // 2. Locate the user's wallet record
            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.Userid == userId);
            if (wallet == null) return NotFound(new { message = "Wallet profile not found." });

            // 3. Set up boundary rules for date filters
            DateTime startBoundary;
            DateTime endBoundary;

            if (startDate.HasValue && endDate.HasValue)
            {
                // User-provided filters: Include the full days by setting start to 00:00:00 and end to 23:59:59
                startBoundary = startDate.Value.Date;
                endBoundary = endDate.Value.Date.AddDays(1).AddTicks(-1);
            }
            else
            {
                // Default View: Automatically filter for the past 7 days
                startBoundary = DateTime.Now.Date.AddDays(-7);
                endBoundary = DateTime.Now.Date.AddDays(1).AddTicks(-1);
            }

            // 4. Query PostgreSQL using date ranges and map roles cleanly
            var transactions = await _context.Transactions
                .Where(t => (t.Senderwalletid == wallet.Walletid || t.Receiverwalletid == wallet.Walletid)
                         && t.Timestamp >= startBoundary
                         && t.Timestamp <= endBoundary)
                .OrderByDescending(t => t.Timestamp)
                .Select(t => new
                {
                    t.Transactionid,
                    t.Senderwalletid,
                    t.Receiverwalletid,
                    t.Amount,
                    t.Transactiontype,
                    t.Status,
                    t.Timestamp,
                    Role = t.Senderwalletid == wallet.Walletid ? "Sender" : "Receiver"
                })
                .ToListAsync();

            return Ok(transactions);
        }

        [HttpPost("deposit")]
        public async Task<IActionResult> Deposit([FromBody] DepositDto dto)
        {
            // 1. Run pipeline validation check instantly
            var validationResult = await _depositValidator.ValidateAsync(dto);
            if (!validationResult.IsValid) return BadRequest(validationResult.Errors);

            // 2. Identify who is executing the deposit from JWT claims
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { message = "Invalid account verification token." });
            }

            // 3. Query their wallet profile matching exact lowercase schema
            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.Userid == userId);
            if (wallet == null) return NotFound(new { message = "Wallet profile not found." });

            // 4. Update Balance
            wallet.Balance += dto.Amount;
            wallet.Updatedat = DateTime.Now;

            // 5. Generate Audit Trail Item in Transactions table
            var auditTrail = new Transaction
            {
                Senderwalletid = null, // No sender since money is entering from an external bank node
                Receiverwalletid = wallet.Walletid,
                Amount = dto.Amount,
                Transactiontype = "Deposit",
                Status = "Success",
                Timestamp = DateTime.Now
            };

            _context.Transactions.Add(auditTrail);
            await _context.SaveChangesAsync();

            // --- SEND DEPOSIT EMAIL ---
            try
            {
                var userRecord = await _context.Users.FindAsync(wallet.Userid);
                if (userRecord != null)
                {
                    string depositBody = $"<h3>Deposit Confirmed!</h3><p>Dear {userRecord.Fullname},</p><p>Your wallet has been successfully funded with <b>{dto.Amount} INR</b>.</p><p>Your updated balance is <b>{wallet.Balance} INR</b>.</p>";
                    await _emailService.SendEmailAsync(userRecord.Email, "Account Alert: Wallet Funded", depositBody);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning("Deposit processed, but notification dispatch failed: " + ex.Message);
            }

            return Ok(new { message = "Deposit processed successfully.", currentBalance = wallet.Balance });
        }

        [HttpGet("analytics")]
        public async Task<IActionResult> GetMonthlyAnalytics()
        {
            // 1. Identify the logged-in user from their JWT token
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { message = "Invalid account verification token." });
            }

            // 2. Locate the user's wallet record matching lowercased schema
            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.Userid == userId);
            if (wallet == null) return NotFound(new { message = "Wallet profile not found." });

            // 3. Define the time window boundaries (First day of current month)
            var now = DateTime.Now;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);

            // 4. Fetch all transactions involving this wallet within this month
            var monthlyTransactions = await _context.Transactions
                .Where(t => (t.Senderwalletid == wallet.Walletid || t.Receiverwalletid == wallet.Walletid)
                         && t.Timestamp >= startOfMonth && t.Status == "Success")
                .ToListAsync();

            // 5. Run in-memory aggregations using conditional LINQ sums
            var totalDeposited = monthlyTransactions
                .Where(t => t.Transactiontype == "Deposit" && t.Receiverwalletid == wallet.Walletid)
                .Sum(t => t.Amount);

            var totalTransferredOut = monthlyTransactions
                .Where(t => t.Transactiontype == "Transfer" && t.Senderwalletid == wallet.Walletid)
                .Sum(t => t.Amount);

            var totalReceivedIn = monthlyTransactions
                .Where(t => t.Transactiontype == "Transfer" && t.Receiverwalletid == wallet.Walletid)
                .Sum(t => t.Amount);

            // 6. Map everything cleanly to our analytics dashboard DTO
            var analyticsSummary = new WalletAnalyticsDto
            {
                TotalDeposited = totalDeposited,
                TotalTransferredOut = totalTransferredOut,
                TotalReceivedIn = totalReceivedIn,
                TotalTransactionCount = monthlyTransactions.Count,
                MonthName = now.ToString("MMMM yyyy") // e.g., "June 2026"
            };

            return Ok(analyticsSummary);
        }

    }
}