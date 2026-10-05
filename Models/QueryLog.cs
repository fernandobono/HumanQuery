using System;

namespace HumanQuery.Models
{
    public class QueryLog
    {
        public Guid Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string HumanQuery { get; set; } = string.Empty;
        public string? GeneratedSql { get; set; }
        public int? ResultCount { get; set; }
        public string? RawDataJson { get; set; }
        public string? HumanizedAnswer { get; set; }
        public long ElapsedMilliseconds { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? DatabaseSchema { get; set; }
        public string? PromptToGenerateSql { get; set; }
        public string? PromptToHumanize { get; set; }
    }
}
