﻿using System;

namespace Seq.App.EventThreshold.Classes
{
    public class Showtime
    {
        public DateTime Start { get; }
        public DateTime End { get; }

        public Showtime(DateTime start, DateTime end)
        {
            Start = start;
            End = end;
        }
    }
}
