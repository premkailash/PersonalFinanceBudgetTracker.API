using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Log;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Transaction;
using PersonalFinanceBudgetTrackerAPI.Models.Entity;
using PersonalFinanceBudgetTrackerAPI.Repository.Transaction;
using PersonalFinanceBudgetTrackerAPI.Repository.Log;

namespace PersonalFinanceBudgetTrackerAPI.Controllers
{
    [ApiController]
    [Route("api/transactions")]    
    public class TransactionsController : ControllerBase
    {
        private readonly ITransactionService _transactionService;
        private readonly ITransactionImportService _importService;
        private readonly ILogService _logService;

        public TransactionsController(
            ITransactionService transactionService,
            ITransactionImportService importService,
            ILogService logService)
        {
            _transactionService = transactionService;
            _importService = importService;
            _logService = logService;
        }

        // ---------------------------------------------------------------
        // GET /api/transactions?account_id={id}&from={date}&to={date}
        // Filtered transaction list for the logged-in user.
        // Financial data must be fetched and displayed within 30 seconds.
        // ---------------------------------------------------------------
        [HttpGet]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> GetTransactions(
            [FromQuery(Name = "account_id")] int accountId,
            [FromQuery(Name = "from")] DateTime from,
            [FromQuery(Name = "to")] DateTime to)
        {
            int callerId = GetCallerId();

            if (callerId == 0)
                return Unauthorized(new { message = "Invalid token. User ID claim is missing." });

            if (to < from)
                return BadRequest(new { message = "The 'to' date must be on or after the 'from' date." });

            // 30-second timeout as required
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            try
            {
                var result = await _transactionService
                    .GetTransactionsAsync(accountId, from, to, callerId)
                    .WaitAsync(cts.Token);

                if (!result.Success)
                    return result.NotFound
                        ? NotFound(new { message = result.Message })
                        : Forbid();

                return Ok(result.Data);
            }
            catch (OperationCanceledException)
            {
                return StatusCode(504, new
                {
                    message = "Request timed out. Financial data must be fetched within 30 seconds."
                });
            }
        }

        // ---------------------------------------------------------------
        // GET /api/transactions/{id}
        // Get a single transaction by ID
        // ---------------------------------------------------------------
        [HttpGet("{id:int}")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> GetTransactionById(int id)
        {
            int callerId = GetCallerId();

            if (callerId == 0)
                return Unauthorized(new { message = "Invalid token. User ID claim is missing." });

            var result = await _transactionService.GetTransactionByIdAsync(id, callerId);

            if (!result.Success)
                return result.NotFound
                    ? NotFound(new { message = result.Message })
                    : Forbid();

            return Ok(result.Data);
        }

        // ---------------------------------------------------------------
        // POST /api/transactions
        // Create a new transaction, update budget, write audit log
        // ---------------------------------------------------------------
        [HttpPost]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> CreateTransaction(
            [FromBody] CreateTransactionRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            int callerId = GetCallerId();

            if (callerId == 0)
                return Unauthorized(new { message = "Invalid token. User ID claim is missing." });

            var result = await _transactionService.CreateTransactionAsync(request, callerId);

            if (!result.Success)
                return result.NotFound
                    ? NotFound(new { message = result.Message })
                    : BadRequest(new { message = result.Message });

            // Audit log
            await _logService.CreateLogAsync(new CreateLogRequestDto
            {
                Event = $"For User {callerId} Transaction {result.Data!.TransactionId} created",
                EventType = "Transaction Created",
                UserId = callerId
            });

            return CreatedAtAction(
                nameof(GetTransactionById),
                new { id = result.Data.TransactionId },
                new { message = result.Message, data = result.Data });
        }

        // ---------------------------------------------------------------
        // PUT /api/transactions/{id}
        // Update transaction, reverse old budget amount, apply new amount
        // ---------------------------------------------------------------
        [HttpPut("{id:int}")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> UpdateTransaction(
            int id,
            [FromBody] UpdateTransactionRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (request.TransactionId != id)
                return BadRequest(new
                {
                    message = "TransactionId in the request body does not match the route parameter."
                });

            int callerId = GetCallerId();

            if (callerId == 0)
                return Unauthorized(new { message = "Invalid token. User ID claim is missing." });

            var result = await _transactionService.UpdateTransactionAsync(request, callerId);

            if (!result.Success)
            {
                if (result.NotFound) return NotFound(new { message = result.Message });
                return Forbid();
            }

            // Audit log
            await _logService.CreateLogAsync(new CreateLogRequestDto
            {
                Event = $"For User {callerId} Transaction {id} updated",
                EventType = "Transaction Updated",
                UserId = callerId
            });

            return Ok(new { message = result.Message, data = result.Data });
        }

        // ---------------------------------------------------------------
        // DELETE /api/transactions/{id}
        // Delete transaction, reverse budget amount, write audit log
        // ---------------------------------------------------------------
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> DeleteTransaction(int id)
        {
            int callerId = GetCallerId();

            if (callerId == 0)
                return Unauthorized(new { message = "Invalid token. User ID claim is missing." });

            var result = await _transactionService.DeleteTransactionAsync(id, callerId);

            if (!result.Success)
                return result.NotFound
                    ? NotFound(new { message = result.Message })
                    : Forbid();

            // Audit log
            await _logService.CreateLogAsync(new CreateLogRequestDto
            {
                Event = $"For User {callerId} Transaction {id} deleted",
                EventType = "Transaction Deleted",
                UserId = callerId
            });

            return Ok(new { message = result.Message });
        }

        // ---------------------------------------------------------------
        // POST /api/transactions/import
        // Internal endpoint — invoked by Lambda / Batch Job only.
        // NOT exposed to Role=Admin or Role=User.
        // Secured via API Key header (X-Import-Key) checked by middleware.
        // ---------------------------------------------------------------
        [HttpPost("import")]
        [AllowAnonymous]            // JWT not used — Lambda passes API key instead
        [ApiKeyAuthorize]           // Custom attribute validates X-Import-Key header
        public async Task<IActionResult> ImportTransactions()
        {
            var result = await _importService.ImportAllLinkedAccountsAsync();

            if (!result.Success)
                return StatusCode(500, new { message = result.Message });

            return Ok(new { message = result.Message, data = result.Data });
        }

        // ---------------------------------------------------------------
        // Helper
        // ---------------------------------------------------------------
        private int GetCallerId()
        {
            var claim = User.FindFirst("userId")?.Value;
            return claim != null ? int.Parse(claim) : 0;
        }
    }

}
