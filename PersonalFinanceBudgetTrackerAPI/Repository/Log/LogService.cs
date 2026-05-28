using PersonalFinanceBudgetTrackerAPI.Context;
using PersonalFinanceBudgetTrackerAPI.Models.Dtos.Log;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using PersonalFinanceBudgetTrackerAPI.Models.Entity;

namespace PersonalFinanceBudgetTrackerAPI.Repository.Log
{
    public class LogService : ILogService
    {
        private readonly AppDbContext _db;

        public LogService(AppDbContext db)
        {
            _db = db;
        }

        // ---------------------------------------------------------------
        // GET ALL LOGS (joined with Users for Username)
        // ---------------------------------------------------------------
        public async Task<LogListResult> GetAllLogsAsync()
        {
            try
            {
                var logs = await _db.Logs
                    .AsNoTracking()
                    .LeftJoin(_db.Users,
                        log => log.ActorId,
                        user => user.UserId,
                        (log, user) => new LogResponseDto
                        {
                            LogId = log.LogId,
                            Event = log.Event,
                            EventType = log.EventType,
                            ActorId = log.ActorId,
                            Username = user != null ? user.Username : null,
                            Timestamp = log.Timestamp
                        })
                    .OrderByDescending(l => l.Timestamp)
                    .ToListAsync();

                return new LogListResult
                {
                    Success = true,
                    Message = $"{logs.Count} log(s) retrieved successfully.",
                    Data = logs
                };
            }
            catch (Exception ex)
            {
                return new LogListResult
                {
                    Success = false,
                    Message = $"An error occurred while retrieving logs: {ex.Message}"
                };
            }
        }

        // ---------------------------------------------------------------
        // GET LOG BY ID (joined with Users for Username)
        // ---------------------------------------------------------------
        public async Task<LogResult> GetLogByIdAsync(int logId)
        {
            try
            {
                var log = await _db.Logs
                    .AsNoTracking()
                    .Where(l => l.LogId == logId)
                    .LeftJoin(_db.Users,
                        l => l.ActorId,
                        user => user.UserId,
                        (l, user) => new LogResponseDto
                        {
                            LogId = l.LogId,
                            Event = l.Event,
                            EventType = l.EventType,
                            ActorId = l.ActorId,
                            Username = user != null ? user.Username : null,
                            Timestamp = l.Timestamp
                        })
                    .FirstOrDefaultAsync();

                if (log == null)
                    return new LogResult
                    {
                        Success = false,
                        NotFound = true,
                        Message = $"Log with ID {logId} was not found."
                    };

                return new LogResult
                {
                    Success = true,
                    Message = "Log retrieved successfully.",
                    Data = log
                };
            }
            catch (Exception ex)
            {
                return new LogResult
                {
                    Success = false,
                    NotFound = false,
                    Message = $"An error occurred while retrieving log: {ex.Message}"
                };
            }
        }

        // ---------------------------------------------------------------
        // CREATE LOG
        // ---------------------------------------------------------------
        public async Task<LogResult> CreateLogAsync(CreateLogRequestDto request)
        {
            try
            {
                var log = new Models.Entity.Log
                {
                    Event = request.Event,
                    EventType = request.EventType,
                    ActorId = request.UserId,
                    Timestamp = DateTime.UtcNow
                };

                _db.Logs.Add(log);
                await _db.SaveChangesAsync();

                // Resolve username for response if ActorId is set
                string? username = null;
                if (log.ActorId.HasValue)
                {
                    var user = await _db.Users
                        .AsNoTracking()
                        .Where(u => u.UserId == log.ActorId.Value)
                        .Select(u => u.Username)
                        .FirstOrDefaultAsync();
                    username = user;
                }

                return new LogResult
                {
                    Success = true,
                    Message = "Log entry created successfully.",
                    Data = new LogResponseDto
                    {
                        LogId = log.LogId,
                        Event = log.Event,
                        EventType = log.EventType,
                        ActorId = log.ActorId,
                        Username = username,
                        Timestamp = log.Timestamp
                    }
                };
            }
            catch (Exception ex)
            {
                return new LogResult
                {
                    Success = false,
                    NotFound = false,
                    Message = $"An error occurred while creating log: {ex.Message}"
                };
            }
        }
    }

}
