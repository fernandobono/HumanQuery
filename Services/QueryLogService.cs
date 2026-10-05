using HumanQuery.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace HumanQuery.Services
{
    public class QueryLogService
    {
        private readonly ConcurrentQueue<QueryLog> _logs = new();
        private const int MaxLogs = 100; // Mantener solo las últimas 100 consultas

        public void AddLog(QueryLog log)
        {
            _logs.Enqueue(log);

            // Mantener solo las últimas MaxLogs consultas
            while (_logs.Count > MaxLogs)
            {
                _logs.TryDequeue(out _);
            }
        }

        public List<QueryLog> GetAllLogs()
        {
            return _logs.OrderByDescending(l => l.Timestamp).ToList();
        }

        public QueryLog? GetLogById(Guid id)
        {
            return _logs.FirstOrDefault(l => l.Id == id);
        }

        public void ClearLogs()
        {
            _logs.Clear();
        }

        public int GetLogCount()
        {
            return _logs.Count;
        }
    }
}
