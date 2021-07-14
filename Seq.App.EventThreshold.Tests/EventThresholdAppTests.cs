using System;
using System.Collections.Generic;
using System.Threading;
using Seq.App.EventThreshold.Classes;
using Seq.App.EventThreshold.Tests.Support;
using Xunit;
using Xunit.Abstractions;

namespace Seq.App.EventThreshold.Tests
{
    public class EventThresholdAppTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public EventThresholdAppTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }
        
        [Fact]
        public void AppThresholds()
        {
            var app = Some.Reactor(DateTime.Today.ToString("H:mm:ss"),
                DateTime.Today.ToString("H:mm:ss"), 1, 1, 2, false, "Hello");
            app.Attach(TestAppHost.Instance);
            //Wait for showtime
            Thread.Sleep(2000);
            Assert.True(app.IsShowtime);
            _testOutputHelper.WriteLine("Event Count: {0}", app.EventCount);
            Assert.True(app.EventCount == 0);
            //Log an event and validate that we are still in showtime and matching events
            var evt = Some.LogEvent();
            app.On(evt);
            app.On(evt);
            app.On(evt);
            Assert.True(app.EventCount > app.Threshold);
            //Still in showtime and still matching events
            Thread.Sleep(2000);
            _testOutputHelper.WriteLine("Event Count: {0}", app.EventCount);
            Assert.True(app.EventCount == 0);
            app.On(evt);
            Assert.True(app.IsShowtime);
            Assert.True(app.EventCount <= app.Threshold);
        }

        [Fact]
        public void AppStartsDuringShowTime()
        {
            var start = DateTime.Now.AddHours(-1);
            var end = DateTime.Now.AddHours(1);

            var app = Some.Reactor(start.ToString("H:mm:ss"), end.ToString("H:mm:ss"), 1, 59, 1);
            app.Attach(TestAppHost.Instance);
            var showTime = app.GetShowtime();
            _testOutputHelper.WriteLine("Current UTC: " + DateTime.Now.ToUniversalTime().ToString("F"));
            _testOutputHelper.WriteLine("ShowTime: " + showTime.Start.ToString("F") + " to " + showTime.End.ToString("F"));
            _testOutputHelper.WriteLine("Expect Start: " + start.AddDays(1).ToUniversalTime().ToString("F") + " to " + end.AddDays(1).ToUniversalTime().ToString("F"));
            Assert.True(showTime.Start.ToString("F") == start.AddDays(1).ToUniversalTime().ToString("F"));
            Assert.True(showTime.End.ToString("F") == end.AddDays(1).ToUniversalTime().ToString("F"));
        }

        [Fact]
        public void AppStartsBeforeShowTime()
        {
            var start = DateTime.Now.AddHours(1);
            var end = DateTime.Now.AddHours(2);

            var app = Some.Reactor(start.ToString("H:mm:ss"), end.ToString("H:mm:ss"), 1, 59, 1);
            app.Attach(TestAppHost.Instance);
            var showTime = app.GetShowtime();
            _testOutputHelper.WriteLine("Current UTC: " + DateTime.Now.ToUniversalTime().ToString("F"));
            _testOutputHelper.WriteLine("ShowTime: " + showTime.Start.ToString("F") + " to " + showTime.End.ToString("F"));
            _testOutputHelper.WriteLine("Expect Start: " + start.ToUniversalTime().ToString("F") + " to " + end.ToUniversalTime().ToString("F"));
            Assert.True(showTime.Start.ToString("F") == start.ToUniversalTime().ToString("F"));
            Assert.True(showTime.End.ToString("F") == end.ToUniversalTime().ToString("F"));
        }

        [Fact]
        public void AppStartsAfterShowTime()
        {
            var start = DateTime.Now.AddHours(-2);
            var end = DateTime.Now.AddHours(-1);

            var app = Some.Reactor(start.ToString("H:mm:ss"), end.ToString("H:mm:ss"), 1, 59, 1);
            app.Attach(TestAppHost.Instance);
            var showTime = app.GetShowtime();
            _testOutputHelper.WriteLine("Current UTC: " + DateTime.Now.ToUniversalTime().ToString("F"));
            _testOutputHelper.WriteLine("ShowTime: " + showTime.Start.ToString("F") + " to " + showTime.End.ToString("F"));
            _testOutputHelper.WriteLine("Expect Start: " + start.AddDays(1).ToUniversalTime().ToString("F") + " to " + end.AddDays(1).ToUniversalTime().ToString("F"));
            Assert.True(showTime.Start.ToString("F") == start.AddDays(1).ToUniversalTime().ToString("F"));
            Assert.True(showTime.End.ToString("F") == end.AddDays(1).ToUniversalTime().ToString("F"));
        }

        [Fact]
        public void RolloverWithHoliday()
        {
            var start = DateTime.Now.AddHours(1);
            var end = DateTime.Now.AddHours(2);
            var holiday = new AbstractApiHolidays("Threshold Day", "", "AU", "", "AU", "Australia - New South Wales",
                "Local holiday", start.ToString("MM/dd/yyyy"), start.Year.ToString(),
                start.Month.ToString(), start.Day.ToString(), start.DayOfWeek.ToString());

            var app = Some.Reactor(start.ToString("H:mm:ss"), end.ToString("H:mm:ss"), 1, 59, 1);
            app.Attach(TestAppHost.Instance);
            app.Holidays = new List<AbstractApiHolidays> {holiday};
            //Handle edge condition for holiday
            if (start.AddHours(1).Day > holiday.LocalStart.Day)
            {
                app.Holidays.Add(new AbstractApiHolidays("Threshold Day", "", "AU", "", "AU", "Australia - New South Wales",
                    "Local holiday", start.AddDays(1).ToString("MM/dd/yyyy"), start.AddDays(1).Year.ToString(),
                    start.AddDays(1).Month.ToString(), start.AddDays(1).Day.ToString(), start.AddDays(1).DayOfWeek.ToString()));
            }
            app.UtcRollover(DateTime.Now.ToUniversalTime(), true);
            var showTime = app.GetShowtime();
            _testOutputHelper.WriteLine("Holiday Local: " + holiday.LocalStart.ToString("F") );
            _testOutputHelper.WriteLine("Current UTC: " + DateTime.Now.ToUniversalTime().ToString("F"));
            _testOutputHelper.WriteLine("Current Local: " + DateTime.Now.ToString("F"));
            _testOutputHelper.WriteLine("ShowTime: " + showTime.Start.ToString("F") + " to " + showTime.End.ToString("F"));
            _testOutputHelper.WriteLine("ShowTime Local: " + showTime.Start.ToLocalTime().ToString("F") + " to " + showTime.End.ToLocalTime().ToString("F"));
            _testOutputHelper.WriteLine("Expect Start: " + start.AddDays(1).ToUniversalTime().ToString("F") + " to " + end.AddDays(1).ToUniversalTime().ToString("F"));
            _testOutputHelper.WriteLine("Expect Start Local: " + start.AddDays(1).ToLocalTime().ToString("F") + " to " + end.AddDays(1).ToLocalTime().ToString("F"));
            Assert.True(showTime.Start.ToString("F") == start.AddDays(1).ToUniversalTime().ToString("F"));
            Assert.True(showTime.End.ToString("F") == end.AddDays(1).ToUniversalTime().ToString("F"));
        }
        
        [Fact]
        public void RolloverWithoutHoliday()
        {
            var start = DateTime.Now.AddHours(-1);
            var end = DateTime.Now.AddSeconds(-1);
            var app = Some.Reactor(start.ToString("H:mm:ss"), end.ToString("H:mm:ss"), 1, 59, 1);

            app.Attach(TestAppHost.Instance);
            app.UtcRollover(DateTime.Now.ToUniversalTime());
            var showTime = app.GetShowtime();
            Assert.True(app.Holidays.Count == 0);
            _testOutputHelper.WriteLine("Current UTC: " + DateTime.Now.ToUniversalTime().ToString("F"));
            _testOutputHelper.WriteLine("ShowTime: " + showTime.Start.ToString("F") + " to " + showTime.End.ToString("F"));
            _testOutputHelper.WriteLine("Expect Start: " + start.AddDays(1).ToUniversalTime().ToString("F") + " to " + end.AddDays(1).ToUniversalTime().ToString("F"));
            Assert.True(showTime.Start.ToString("F") == start.AddDays(1).ToUniversalTime().ToString("F"));
            Assert.True(showTime.End.ToString("F") == end.AddDays(1).ToUniversalTime().ToString("F"));
        }

        [Fact]
        public void HolidaysMatch()
        {
            var holiday = new AbstractApiHolidays("Threshold Day", "", "AU", "", "AU", "Australia - New South Wales",
                "Local holiday", DateTime.Today.ToString("MM/dd/yyyy"), DateTime.Today.Year.ToString(),
                DateTime.Today.Month.ToString(), DateTime.Today.Day.ToString(), DateTime.Today.DayOfWeek.ToString());

            Assert.True(Holidays.ValidateHolidays(new List<AbstractApiHolidays> {holiday},
                new List<string> {"National", "Local"}, new List<string> {"Australia", "New South Wales"}, false,
                false).Count > 0);
        }

        [Fact]
        public void DatesExpressed()
        {
            _testOutputHelper.WriteLine(string.Join(",",Dates.GetDaysOfMonth("first,last,first weekday,last weekday,first monday", "12:00", "H:mm").ToArray()));
            Assert.True(Dates.GetDaysOfMonth("first,last,first weekday,last weekday,first monday", "12:00", "H:mm").Count > 0);
        }

        [Fact]
        public void PropertyMatched()
        {
            Assert.True(PropertyMatch.Matches("A Matchable Event", "matchable"));
        }
    }
}
