using System;

// ReSharper disable UnusedMember.Global

namespace Seq.App.EventThreshold.Classes
{
    public class ThresholdCounters
    {
        public bool CannotMatchAlerted { get; set; }
        public DateTime EndTime { get; set; }
        public int EventCount { get; set; }
        public int ErrorCount { get; set; }
        public bool IsUpdating { get; set; }
        public DateTime LastCheck { get; set; }
        public DateTime LastDay { get; set; }
        public DateTime LastError { get; set; }
        public DateTime LastLog { get; set; }
        public int LastMatched { get; set; }
        public DateTime LastMatchLog { get; set; }
        public DateTime LastUpdate { get; set; }
        public int RetryCount { get; set; }
        public bool SkippedShowtime { get; set; }
        public DateTime StartTime { get; set; }
        public bool IsAlert { get; set; }
        public bool IsShowtime { get; set; }
    }
}