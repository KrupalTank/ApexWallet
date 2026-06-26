using System.Security.Claims;
using System.Threading.Tasks;
using ApexWallet.Api.Database.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace ApexWallet.Api.Filters
{
    public class AccountStatusCheckFilter : IAsyncActionFilter
    {
        private readonly ApexWalletDbContext _context;

        public AccountStatusCheckFilter(ApexWalletDbContext context)
        {
            _context = context;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Extract user from the active JWT claim context identity block
            var userIdClaim = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int userId))
            {
                // Pull status directly out of the database cache line
                var userStatus = await _context.Users
                    .Where(u => u.Userid == userId)
                    .Select(u => u.AccountStatus)
                    .FirstOrDefaultAsync();

                if (userStatus == "Locked")
                {
                    // ⚠️ SECURITY REJECTION: Instantly drop the connection with an HTTP 403 Forbidden!
                    context.Result = new ObjectResult(new { message = "Access Denied: Your account has been securely locked. Please contact system support." })
                    {
                        StatusCode = 403
                    };
                    return; // Short-circuit pipeline execution path
                }
            }

            await next(); // Allow request execution if user status is clear
        }
    }
}