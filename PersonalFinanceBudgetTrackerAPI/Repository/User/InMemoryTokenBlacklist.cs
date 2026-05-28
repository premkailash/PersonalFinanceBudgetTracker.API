using System.Collections.Concurrent;

namespace PersonalFinanceBudgetTrackerAPI.Repository.User
{
    public class InMemoryTokenBlacklist : ITokenBlacklist
    {
        // ---------------------------------------------------------------
        // In-Memory Implementation
        // For production, replace with a Redis-backed implementation
        // ---------------------------------------------------------------

        // Stores userId -> UTC Unix timestamp of invalidation
        private readonly ConcurrentDictionary<int, long> _invalidatedUsers = new();

        public Task InvalidateUserTokensAsync(int userId)
        {
            long invalidatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            _invalidatedUsers[userId] = invalidatedAt;
            return Task.CompletedTask;
        }

        public Task<bool> IsUserInvalidatedAsync(int userId, long tokenIssuedAt)
        {
            if (_invalidatedUsers.TryGetValue(userId, out long invalidatedAt))
            {
                // Token is invalid if it was issued before or at the invalidation time
                return Task.FromResult(tokenIssuedAt <= invalidatedAt);
            }

            return Task.FromResult(false);
        }
    }
}
