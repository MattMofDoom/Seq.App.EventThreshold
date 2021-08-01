using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Lurgle.Dates;
using Lurgle.Dates.Classes;
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
            Thread.Sleep(2000);
            Assert.True(app.IsAlert);
            Assert.True(app.EventCount == 0);
            //Log an event and validate that we are still in showtime and matching events
            var evt = Some.LogEvent();
            app.On(evt);
            app.On(evt);
            app.On(evt);
            var isAlert = true;
            for (var i = 1; i < 1001; i++)
            {
                Thread.Sleep(1);
                if (app.IsAlert) continue;
                isAlert = false;
                break;
            }

            Assert.False(isAlert);

            //Still in showtime and still matching events
            Thread.Sleep(2000);
            Assert.True(app.IsShowtime);
            Assert.True(app.IsAlert);
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
            _testOutputHelper.WriteLine("ShowTime: " + showTime.Start.ToString("F") + " to " +
                                        showTime.End.ToString("F"));
            _testOutputHelper.WriteLine("Expect Start: " + start.AddDays(1).ToUniversalTime().ToString("F") + " to " +
                                        end.AddDays(1).ToUniversalTime().ToString("F"));
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
            _testOutputHelper.WriteLine("ShowTime: " + showTime.Start.ToString("F") + " to " +
                                        showTime.End.ToString("F"));
            _testOutputHelper.WriteLine("Expect Start: " + start.ToUniversalTime().ToString("F") + " to " +
                                        end.ToUniversalTime().ToString("F"));
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
            _testOutputHelper.WriteLine("ShowTime: " + showTime.Start.ToString("F") + " to " +
                                        showTime.End.ToString("F"));
            _testOutputHelper.WriteLine("Expect Start: " + start.AddDays(1).ToUniversalTime().ToString("F") + " to " +
                                        end.AddDays(1).ToUniversalTime().ToString("F"));
            Assert.True(showTime.Start.ToString("F") == start.AddDays(1).ToUniversalTime().ToString("F"));
            Assert.True(showTime.End.ToString("F") == end.AddDays(1).ToUniversalTime().ToString("F"));
        }

        [Fact]
        public void RolloverWithHoliday()
        {
            var start = DateTime.ParseExact(DateTime.Today.ToString("yyyy-MM-dd") + " 00:01:00", "yyyy-MM-dd H:mm:ss",
                CultureInfo.InvariantCulture, DateTimeStyles.None);
            var end = DateTime.ParseExact(start.AddHours(1).ToString("yyyy-MM-dd H:mm:ss"), "yyyy-MM-dd H:mm:ss",
                CultureInfo.InvariantCulture, DateTimeStyles.None);

            var app = Some.Reactor(start.ToString("H:mm:ss"), end.ToString("H:mm:ss"), 1, 59, 1);
            app.TestOverrideTime = start;
            app.UseTestOverrideTime = true;
            app.Attach(TestAppHost.Instance);

            for (var i = 0; i < 169; i++)
            {
                if (i > 0)
                {
                    app.TestOverrideTime = app.TestOverrideTime.AddHours(1);

                    if (i % 24 == 0)
                    {
                        start = start.AddDays(1);
                        end = start.AddHours(1);
                    }
                }

                var holiday = new AbstractApiHolidays("Threshold Day", "", "AU", "", "AU",
                    "Australia - New South Wales",
                    "Local holiday", start.ToString("MM/dd/yyyy"), start.Year.ToString(),
                    start.Month.ToString(), start.Day.ToString(), start.DayOfWeek.ToString());
                app.Holidays = new List<AbstractApiHolidays> {holiday};

                app.UtcRollover(app.TestOverrideTime.ToUniversalTime(), true);
                var showTime = app.GetShowtime();
                _testOutputHelper.WriteLine("Time: {0:F}, Next ShowTime: {1:F}, Matches {2}",
                    app.TestOverrideTime.ToUniversalTime(), showTime.Start.ToUniversalTime(),
                    showTime.Start.ToString("F") == start.AddDays(1).ToUniversalTime().ToString("F"));

                Assert.True(showTime.Start.ToString("F") == start.AddDays(1).ToUniversalTime().ToString("F"));
                Assert.True(showTime.End.ToString("F") == end.AddDays(1).ToUniversalTime().ToString("F"));
            }
        }

        [Fact]
        public void RolloverWithoutHoliday()
        {
            var start = DateTime.ParseExact(DateTime.Today.ToString("yyyy-MM-dd") + " 00:01:00", "yyyy-MM-dd H:mm:ss",
                CultureInfo.InvariantCulture, DateTimeStyles.None);
            var end = DateTime.ParseExact(start.AddHours(1).ToString("yyyy-MM-dd H:mm:ss"), "yyyy-MM-dd H:mm:ss",
                CultureInfo.InvariantCulture, DateTimeStyles.None);

            var app = Some.Reactor(start.ToString("H:mm:ss"), end.ToString("H:mm:ss"), 1, 59, 1);
            app.TestOverrideTime = start;
            app.UseTestOverrideTime = true;
            app.Attach(TestAppHost.Instance);

            for (var i = 0; i < 169; i++)
            {
                if (i > 0)
                {
                    app.TestOverrideTime = app.TestOverrideTime.AddHours(1);

                    if (i % 24 == 0)
                    {
                        start = start.AddDays(1);
                        end = start.AddHours(1);
                    }
                }

                app.Holidays = new List<AbstractApiHolidays>();

                app.UtcRollover(app.TestOverrideTime.ToUniversalTime());
                var showTime = app.GetShowtime();

                if (start < app.TestOverrideTime)
                {
                    _testOutputHelper.WriteLine("Time: {0:F}, Next ShowTime: {1:F}, Matches {2}",
                        app.TestOverrideTime.ToUniversalTime(), showTime.Start.ToUniversalTime(),
                        showTime.Start.ToString("F") == start.AddDays(1).ToUniversalTime().ToString("F"));
                    Assert.True(showTime.Start.ToString("F") == start.AddDays(1).ToUniversalTime().ToString("F"));
                    Assert.True(showTime.End.ToString("F") == end.AddDays(1).ToUniversalTime().ToString("F"));
                }
                else
                {
                    _testOutputHelper.WriteLine("Time: {0:F}, Next ShowTime: {1:F}, Matches {2}",
                        app.TestOverrideTime.ToUniversalTime(), showTime.Start.ToUniversalTime(),
                        showTime.Start.ToString("F") == start.ToUniversalTime().ToString("F"));
                    Assert.True(showTime.Start.ToString("F") == start.ToUniversalTime().ToString("F"));
                    Assert.True(showTime.End.ToString("F") == end.ToUniversalTime().ToString("F"));
                }
            }
        }

        [Fact]
        public void HolidaysMatch()
        {
            var holiday = new AbstractApiHolidays("Threshold Day", "", "AU", "", "AU", "Australia - New South Wales",
                "Local holiday", DateTime.Today.ToString("MM/dd/yyyy"), DateTime.Today.Year.ToString(),
                DateTime.Today.Month.ToString(), DateTime.Today.Day.ToString(), DateTime.Today.DayOfWeek.ToString());

            Assert.True(Holidays.ValidateHolidays(new List<AbstractApiHolidays> {holiday},
                new List<string> {"National", "Local"}, new List<string> {"Australia", "New South Wales"}, false,
                true).Count > 0);
        }

        [Fact]
        public void DatesExpressed()
        {
            _testOutputHelper.WriteLine(string.Join(",",
                Dates.GetDaysOfMonth("first,last,first weekday,last weekday,first monday", "12:00", "H:mm",
                    DateTime.Now).ToArray()));
            Assert.True(Dates
                .GetDaysOfMonth("first,last,first weekday,last weekday,first monday", "12:00", "H:mm", DateTime.Now)
                .Count > 0);
        }

        [Fact]
        public void PropertyMatched()
        {
            Assert.True(PropertyMatch.Matches("A Matchable Event", "matchable"));
        }
    }
}