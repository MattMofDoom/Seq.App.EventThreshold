using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Timers;
using Lurgle.Dates;
using Lurgle.Dates.Classes;
using Seq.App.EventThreshold.Classes;
using Seq.Apps;
using Seq.Apps.LogEvents;

// ReSharper disable MemberCanBePrivate.Global

namespace Seq.App.EventThreshold
{
    [SeqApp("Event Threshold", AllowReprocessing = false,
        Description =
            "Super-powered Seq event thresholds with start/end times, measuring and suppression intervals, property matching, day of week and day of month inclusion/exclusion, and optional holiday API!")]
    // ReSharper disable once UnusedType.Global
    public class EventThresholdReactor : SeqApp, ISubscribeTo<LogEventData>
    {
        public readonly ThresholdConfig Config = new ThresholdConfig();
        public readonly ThresholdCounters Counters = new ThresholdCounters();
        private Timer _timer;
        public string Description;
        public HandlebarsTemplate DescriptionTemplate;
        public string Message;
        public HandlebarsTemplate MessageTemplate;  // ReSharper disable MemberCanBePrivate.Global

        // ReSharper disable UnusedAutoPropertyAccessor.Global
        // ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
        [SeqAppSetting(
            DisplayName = "Diagnostic logging",
            HelpText = "Send extra diagnostic logging to the stream. Recommended to enable.")]
        public bool Diagnostics { get; set; } = true;

        [SeqAppSetting(
            DisplayName = "Start time",
            HelpText = "The time (H:mm:ss, 24 hour format) to start monitoring.")]
        public string StartTime { get; set; }

        [SeqAppSetting(
            DisplayName = "End time",
            HelpText = "The time (H:mm:ss, 24 hour format) to stop monitoring, up to 24 hours after start time.")]
        public string EndTime { get; set; }

        [SeqAppSetting(
            DisplayName = "Invert threshold (Greater than)",
            HelpText =
                "By default, thresholds are measured as Less Than or Equal To the configured threshold (eg. <=100). Enable this to invert the measurement to Greater Than or Equal To (eg. >=100).",
            InputType = SettingInputType.Checkbox)]
        // ReSharper disable once RedundantDefaultMemberInitializer
        public bool InvertThreshold { get; set; } = false;

        [SeqAppSetting(
            DisplayName = "Threshold",
            HelpText =
                "Threshold for the count of events that must be seen in the configured time. If this threshold is violated, an alert will be raised. Default 100, Minimum 1.",
            InputType = SettingInputType.Integer)]
        public int Threshold { get; set; } = 100;

        [SeqAppSetting(
            DisplayName = "Log event count at end of each interval",
            HelpText =
                "Log the count of events seen at the end of each interval (as a debug log)",
            InputType = SettingInputType.Checkbox)]
        public bool LogEventCount { get; set; } = false;

        [SeqAppSetting(
            DisplayName = "Threshold measuring interval (seconds)",
            HelpText =
                "Time interval for measuring a threshold. If the event count violates the configured Threshold over this interval, an alert will be raised. Default 60, Minimum 1.",
            InputType = SettingInputType.Integer)]
        public int ThresholdInterval { get; set; } = 60;

        [SeqAppSetting(
            DisplayName = "Suppression interval (seconds)",
            HelpText =
                "If an alert has been raised, further alerts will be suppressed for this time. Default 60, Minimum 0.",
            InputType = SettingInputType.Integer)]
        public int SuppressionTime { get; set; } = 60;

        [SeqAppSetting(DisplayName = "Log level for threshold violations",
            HelpText = "Verbose, Debug, Information, Warning, Error, Fatal. Defaults to Error.",
            IsOptional = true)]
        public string ThresholdLogLevel { get; set; }

        [SeqAppSetting(DisplayName = "Priority for threshold violations",
            HelpText = "Optional Priority property to pass for threshold violations, for use with other apps.",
            IsOptional = true)]
        public string Priority { get; set; }

        [SeqAppSetting(DisplayName = "Responders for threshold violations",
            HelpText = "Optional Responders property to pass for threshold violations, for use with other apps.",
            IsOptional = true)]
        public string Responders { get; set; }

        [SeqAppSetting(DisplayName = "Project Key for scheduled logs",
            HelpText = "Optional Project Key property to pass for scheduled logs, for use with other apps.",
            IsOptional = true)]
        public string ProjectKey { get; set; }

        [SeqAppSetting(DisplayName = "Initial Time Estimate for scheduled logs",
            HelpText = "Optional Initial Time Estimate property to pass for scheduled logs, for use with other apps. Jira-type date expression, eg. Ww (weeks) Xd (days) Yh (hours) Zm (minutes).",
            IsOptional = true)]
        public string InitialTimeEstimate { get; set; }

        [SeqAppSetting(DisplayName = "Remaining Time Estimate for scheduled logs",
            HelpText = "Optional Remaining Time Estimate property to pass for scheduled logs, for use with other apps. Jira-type date expression, eg. Ww (weeks) Xd (days) Yh (hours) Zm (minutes).",
            IsOptional = true)]
        public string RemainingTimeEstimate { get; set; }

        [SeqAppSetting(DisplayName = "Due Date for scheduled logs",
            HelpText = "Optional Due Date property to pass for scheduled logs, for use with other apps. Date in yyyy-MM-dd format, or Jira-type date expression, eg. Ww (weeks) Xd (days) Yh (hours) Zm (minutes).",
            IsOptional = true)]
        public string DueDate { get; set; }

        [SeqAppSetting(
            DisplayName = "Days of week",
            HelpText = "Comma-delimited - Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday.",
            IsOptional = true)]
        public string DaysOfWeek { get; set; }

        [SeqAppSetting(
            DisplayName = "Include days of month",
            HelpText =
                "Only run on these days. Comma-delimited - first,last,first weekday,last weekday,first-fourth sunday-saturday,1-31.",
            IsOptional = true)]
        public string IncludeDaysOfMonth { get; set; }

        [SeqAppSetting(
            DisplayName = "Exclude days of month",
            HelpText = "Never run on these days. Comma-delimited - first,last,1-31.",
            IsOptional = true)]
        public string ExcludeDaysOfMonth { get; set; }


        [SeqAppSetting(
            DisplayName = "Property 1 name",
            HelpText =
                "Case insensitive property name (must be a full match). If not configured, the @Message property will be used. An alert will be raised if thresholds are violated.",
            IsOptional = true)]
        public string Property1Name { get; set; }

        [SeqAppSetting(
            DisplayName = "Property 1 match",
            HelpText =
                "Case insensitive text to match - partial match okay. If not configured, ANY text will match. An alert will be raised if thresholds are violated.",
            IsOptional = true)]
        public string TextMatch { get; set; }

        [SeqAppSetting(
            DisplayName = "Property 2 name",
            HelpText =
                "Case insensitive property name (must be a full match). If not configured, this will not be evaluated.",
            IsOptional = true)]
        public string Property2Name { get; set; }

        [SeqAppSetting(
            DisplayName = "Property 2 match",
            HelpText =
                "Case insensitive text to match - partial match okay. If property name is set and this is not configured, ANY text will match.",
            IsOptional = true)]
        public string Property2Match { get; set; }

        [SeqAppSetting(
            DisplayName = "Property 3 name",
            HelpText =
                "Case insensitive property name (must be a full match). If not configured, this will not be evaluated.",
            IsOptional = true)]
        public string Property3Name { get; set; }

        [SeqAppSetting(
            DisplayName = "Property 3 match",
            HelpText =
                "Case insensitive text to match - partial match okay. If property name is set and this is not configured, ANY text will match.",
            IsOptional = true)]
        public string Property3Match { get; set; }

        [SeqAppSetting(
            DisplayName = "Property 4 name",
            HelpText =
                "Case insensitive property name (must be a full match). If not configured, this will not be evaluated.",
            IsOptional = true)]
        public string Property4Name { get; set; }

        [SeqAppSetting(
            DisplayName = "Property 4 match",
            HelpText =
                "Case insensitive text to match - partial match okay. If property name is set and this is not configured, ANY text will match.",
            IsOptional = true)]
        public string Property4Match { get; set; }

        [SeqAppSetting(
            DisplayName = "Alert message",
            HelpText =
                "Event message to raise. Allows tokens for date parts: Day: {d}/{dd}/{ddd}/{dddd}, Month: {M}/{MM}/{MMM}/{MMMM}, Year: {yy}/{yyyy}, or date expressions. These are not case sensitive.")]
        public string AlertMessage { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Alert description",
            HelpText =
                "Optional description associated with the event raised. Allows tokens for date parts: Day: {d}/{dd}/{ddd}/{dddd}, Month: {M}/{MM}/{MMM}/{MMMM}, Year: {yy}/{yyyy}, or date expressions. These are not case sensitive.")]
        public string AlertDescription { get; set; }

        [SeqAppSetting(
            DisplayName = "Include description with log message",
            HelpText =
                "If selected, the configured description will be part of the log message. Otherwise it will only show as a log property, which can be used by other Seq apps.",
            IsOptional = true)]
        public bool? IncludeDescription { get; set; } = false;

        [SeqAppSetting(
            DisplayName = "Use Handlebars templates in message and description",
            HelpText =
                "if selected, the configured message and description will be rendered using Handlebars. Don't select this if you want to render in another app.",
            IsOptional = true)]
        public bool? UseHandlebars { get; set; } = false;

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Alert tags",
            HelpText =
                "Tags for the event, separated by commas.  Allows tokens for date parts: Day: {d}/{dd}/{ddd}/{dddd}, Month: {M}/{MM}/{MMM}/{MMMM}, Year: {yy}/{yyyy}, or date expressions. These are not case sensitive.")]
        public string Tags { get; set; }

        [SeqAppSetting(
            DisplayName = "Include instance name in alert message",
            HelpText = "Prepend the instance name to the alert message.")]
        public bool IncludeApp { get; set; }


        [SeqAppSetting(
            DisplayName = "Holidays - use Holidays API for public holiday detection",
            HelpText = "Connect to the AbstractApi Holidays service to detect public holidays.")]
        public bool UseHolidays { get; set; } = false;

        [SeqAppSetting(
            DisplayName = "Holidays - Retry count",
            HelpText = "Retry count for retrieving the Holidays API. Default 10, minimum 0, maximum 100.",
            InputType = SettingInputType.Integer,
            IsOptional = true)]
        public int RetryCount { get; set; } = 10;

        [SeqAppSetting(
            DisplayName = "Holidays - Country code",
            HelpText = "Two letter country code (eg. AU).",
            IsOptional = true)]
        public string Country { get; set; }

        [SeqAppSetting(
            DisplayName = "Holidays - API key",
            HelpText = "Sign up for an API key at https://www.abstractapi.com/holidays-api and enter it here.",
            IsOptional = true,
            InputType = SettingInputType.Password)]
        public string ApiKey { get; set; }

        [SeqAppSetting(
            DisplayName = "Holidays - match these holiday types",
            HelpText =
                "Comma-delimited list of holiday types (eg. National, Local) - case insensitive, partial match okay.",
            IsOptional = true)]
        public string HolidayMatch { get; set; }

        [SeqAppSetting(
            DisplayName = "Holidays - match these locales",
            HelpText =
                "Holidays are valid if the location matches one of these comma separated values (eg. Australia,New South Wales) - case insensitive, must be a full match.",
            IsOptional = true)]
        public string LocaleMatch { get; set; }

        [SeqAppSetting(
            DisplayName = "Holidays - include weekends",
            HelpText = "Include public holidays that are returned for weekends.")]
        public bool IncludeWeekends { get; set; }

        [SeqAppSetting(
            DisplayName = "Holidays - include Bank Holidays.",
            HelpText = "Include bank holidays")]
        public bool IncludeBank { get; set; }

        [SeqAppSetting(
            DisplayName = "Holidays - test date",
            HelpText = "yyyy-M-d format. Used only for diagnostics - should normally be empty.",
            IsOptional = true)]
        public string TestDate { get; set; }

        [SeqAppSetting(
            DisplayName = "Holidays - proxy address",
            HelpText = "Proxy address for Holidays API.",
            IsOptional = true)]
        public string Proxy { get; set; }

        [SeqAppSetting(
            DisplayName = "Holidays - proxy bypass local addresses",
            HelpText = "Bypass local addresses for proxy.")]
        public bool BypassLocal { get; set; } = true;

        [SeqAppSetting(
            DisplayName = "Holidays - local addresses for proxy bypass",
            HelpText = "Local addresses to bypass, comma separated.",
            IsOptional = true)]
        public string LocalAddresses { get; set; }

        [SeqAppSetting(
            DisplayName = "Holidays - proxy username",
            HelpText = "Username for proxy authentication.",
            IsOptional = true)]
        public string ProxyUser { get; set; }

        [SeqAppSetting(
            DisplayName = "Holidays - proxy password",
            HelpText = "Username for proxy authentication.",
            IsOptional = true,
            InputType = SettingInputType.Password)]


        public string ProxyPass { get; set; }

        public void On(Event<LogEventData> evt)
        {
            if (evt == null) throw new ArgumentNullException(nameof(evt));

            var cannotMatch = false;
            var cannotMatchProperties = new List<string>();
            var properties = 0;
            var matches = 0;

            if (!Counters.IsShowtime) return;
            foreach (var property in Config.Properties)
            {
                properties++;
                if (property.Key.Equals("@Message", StringComparison.OrdinalIgnoreCase))
                {
                    if (PropertyMatch.Matches(evt.Data.RenderedMessage, property.Value)) matches++;
                }
                else
                {
                    var matchedKey = false;

                    //IReadOnlyDictionary ContainsKey is case sensitive, so we need to iterate
                    foreach (var key in evt.Data.Properties)
                        if (key.Key.Equals(property.Key, StringComparison.OrdinalIgnoreCase))
                        {
                            matchedKey = true;
                            if (string.IsNullOrEmpty(property.Value) || !PropertyMatch.Matches(
                                evt.Data.Properties[property.Key].ToString(),
                                property.Value)) continue;
                            matches++;
                            break;
                        }

                    //If one of the configured properties doesn't have a matching property on the event, we won't be able to raise an alert
                    if (matchedKey) continue;
                    cannotMatch = true;
                    cannotMatchProperties.Add(property.Key);
                }
            }

            switch (cannotMatch)
            {
                case true when !Counters.CannotMatchAlerted:
                    LogEvent(LogEventLevel.Debug,
                        "Warning - An event was seen without the properties {PropertyName}, which may impact the ability to alert on failures - further failures will not be logged ...",
                        string.Join(",", cannotMatchProperties.ToArray()));
                    Counters.CannotMatchAlerted = true;
                    break;
                //If all configured properties were present and had matches, log an event
                case false when properties == matches:
                {
                    Counters.EventCount++;
                    break;
                }
            }
        }

        protected override void OnAttached()
        {
            Config.AppName = App.Title;
            LogEvent(LogEventLevel.Debug, "Check {AppName} diagnostic level ({Diagnostics}) ...", App.Title,
                Diagnostics);
            Config.Diagnostics = Diagnostics;

            if (Config.Diagnostics)
                LogEvent(LogEventLevel.Debug, "Check include {AppName} ({IncludeApp}) ...", App.Title, IncludeApp);

            Config.IncludeApp = IncludeApp;
            if (!Config.IncludeApp && Config.Diagnostics)
                LogEvent(LogEventLevel.Debug, "App name {AppName} will not be included in alert message ...",
                    App.Title);
            else if (Config.Diagnostics)
                LogEvent(LogEventLevel.Debug, "App name {AppName} will be included in alert message ...", App.Title);

            if (!DateTime.TryParseExact(StartTime, "H:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None,
                out _))
            {
                if (DateTime.TryParseExact(StartTime, "H:mm", CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out _))
                    Config.StartFormat = "H:mm";
                else
                    LogEvent(LogEventLevel.Debug,
                        "Start Time {StartTime} does  not parse to a valid DateTime - app will exit ...", StartTime);
            }

            if (!DateTime.TryParseExact(EndTime, "H:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None,
                out _))
            {
                if (DateTime.TryParseExact(EndTime, "H:mm", CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out _))
                    Config.EndFormat = "H:mm";
                else
                    LogEvent(LogEventLevel.Debug,
                        "End Time {EndTime} does  not parse to a valid DateTime - app will exit ...", EndTime);
            }

            Config.InvertThreshold = InvertThreshold;
            if (Config.Diagnostics)
                LogEvent(LogEventLevel.Debug, "Invert threshold to Greater Than or Equal To (>=): {InvertThreshold}",
                    Config.InvertThreshold);

            if (Threshold <= 0)
                Threshold = 1;
            Config.Threshold = Threshold;
            if (Config.Diagnostics)
                LogEvent(LogEventLevel.Debug,
                    Config.InvertThreshold
                        ? "Threshold for events is 'Greater Than or Equal To {Threshold}' ..."
                        : "Threshold for events is 'Less Than or Equal To {Threshold}' ...", Config.Threshold);


            Config.LogEventCount = LogEventCount;
            if (Config.Diagnostics)
                LogEvent(LogEventLevel.Debug, "Log events at end of each interval {LogEventCount}", Config.LogEventCount);

            LogEvent(LogEventLevel.Debug,
                "Use Holidays API {UseHolidays}, Country {Country}, Has API key {IsEmpty} ...", UseHolidays, Country,
                !string.IsNullOrEmpty(ApiKey));
            SetHolidays();
            RetrieveHolidays(DateTime.Today, DateTime.UtcNow);

            if (!Config.UseHolidays || Counters.IsUpdating) UtcRollover(DateTime.UtcNow);

            //Enforce minimum threshold interval
            if (ThresholdInterval <= 0)
                ThresholdInterval = 1;
            if (Config.Diagnostics)
                LogEvent(LogEventLevel.Debug, "Convert Threshold Interval {ThresholdInterval} to TimeSpan ...",
                    ThresholdInterval);

            Config.ThresholdInterval = TimeSpan.FromSeconds(ThresholdInterval);
            if (Config.Diagnostics)
                LogEvent(LogEventLevel.Debug, "Parsed Threshold Interval is {Interval} ...",
                    Config.ThresholdInterval.TotalSeconds);

            //Negative values not permitted
            if (SuppressionTime < 0)
                SuppressionTime = 0;
            if (Config.Diagnostics)
                LogEvent(LogEventLevel.Debug, "Convert Suppression {Suppression} to TimeSpan ...", SuppressionTime);

            Config.SuppressionTime = TimeSpan.FromSeconds(SuppressionTime);
            if (Config.Diagnostics)
                LogEvent(LogEventLevel.Debug, "Parsed Suppression is {Suppression} ...", Config.SuppressionTime.TotalSeconds);

            if (Config.Diagnostics)
                LogEvent(LogEventLevel.Debug, "Convert Days of Week {DaysOfWeek} to UTC Days of Week ...", DaysOfWeek);
            Config.DaysOfWeek = Dates.GetUtcDaysOfWeek(DaysOfWeek, StartTime, Config.StartFormat);

            if (Config.Diagnostics)
                LogEvent(LogEventLevel.Debug, "UTC Days of Week {DaysOfWeek} will be used ...", Config.DaysOfWeek.ToArray());

            if (Config.Diagnostics)
                LogEvent(LogEventLevel.Debug, "Validate Include Days of Month {IncludeDays} ...", IncludeDaysOfMonth);

            Config.IncludeDays = Dates.GetUtcDaysOfMonth(IncludeDaysOfMonth, StartTime, Config.StartFormat, DateTime.Now);
            if (Config.IncludeDays.Count > 0)
                LogEvent(LogEventLevel.Debug, "Include UTC Days of Month: {IncludeDays} ...", Config.IncludeDays.ToArray());
            else
                LogEvent(LogEventLevel.Debug, "Include UTC Days of Month: ALL ...");

            if (Config.Diagnostics)
                LogEvent(LogEventLevel.Debug, "Validate Exclude Days of Month {ExcludeDays} ...", ExcludeDaysOfMonth);

            Config.ExcludeDays = Dates.GetUtcDaysOfMonth(ExcludeDaysOfMonth, StartTime, Config.StartFormat, DateTime.Now);
            if (Config.ExcludeDays.Count > 0)
                LogEvent(LogEventLevel.Debug, "Exclude UTC Days of Month: {ExcludeDays} ...", Config.ExcludeDays.ToArray());
            else
                LogEvent(LogEventLevel.Debug, "Exclude UTC Days of Month: NONE ...");

            //Evaluate the properties we will match
            Config.Properties = SetProperties();
            if (Config.Diagnostics)
                LogEvent(LogEventLevel.Debug, "Match criteria will be: {MatchText}",
                    PropertyMatch.MatchConditions(Config.Properties));

            if (UseHandlebars != null) Config.UseHandlebars = (bool)UseHandlebars;
            if (Config.Diagnostics)
                LogEvent(LogEventLevel.Debug,
                    "Use Handlebars to render Log Message and Description: '{UseHandlebars}' ...",
                    Config.UseHandlebars);

            if (Config.Diagnostics)
                LogEvent(LogEventLevel.Debug, "Validate Alert Message '{AlertMessage}' ...", AlertMessage);

            Message = !string.IsNullOrWhiteSpace(AlertMessage)
                ? AlertMessage.Trim()
                : "A threshold violation has occurred!";
            MessageTemplate = Config.UseHandlebars ? new HandlebarsTemplate(Message) : new HandlebarsTemplate("");

            if (Config.Diagnostics)
                LogEvent(LogEventLevel.Debug, "Alert Message '{AlertMessage}' will be used ...", Message);

            if (Config.Diagnostics)
                LogEvent(LogEventLevel.Debug, "Validate Alert Description '{AlertDescription}' ...", AlertDescription);

            Description = !string.IsNullOrWhiteSpace(AlertDescription)
                ? AlertDescription.Trim()
                : "";
            DescriptionTemplate =
                Config.UseHandlebars ? new HandlebarsTemplate(Description) : new HandlebarsTemplate("");

            if (Config.Diagnostics)
                LogEvent(LogEventLevel.Debug, "Alert Description '{AlertDescription}' will be used ...",
                    Description);

            if (IncludeDescription != null)
                Config.IncludeDescription = (bool) IncludeDescription;
            if (Config.Diagnostics)
                LogEvent(LogEventLevel.Debug, "Include Description in Log Message: '{IncludeDescription}' ...",
                    Config.IncludeDescription);

            if (Config.Diagnostics) LogEvent(LogEventLevel.Debug, "Convert Tags '{Tags}' to array ...", Tags);

            Config.Tags = (Tags ?? "")
                .Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .ToArray();
            if (Config.Tags.Length > 0) Config.IsTags = true;

            if (string.IsNullOrWhiteSpace(ThresholdLogLevel)) ThresholdLogLevel = "Error";
            if (!Enum.TryParse(ThresholdLogLevel, out Config.ThresholdLogLevel)) Config.ThresholdLogLevel = LogEventLevel.Error;

            if (!string.IsNullOrEmpty(Priority))
                Config.Priority = Priority;

            if (!string.IsNullOrEmpty(Responders))
            {
                Config.Responders = Responders;
                if (Config.Diagnostics)
                    LogEvent(LogEventLevel.Debug, "Set responder to {Responder}", Config.Responders);
            }

            if (!string.IsNullOrEmpty(ProjectKey))
            {
                if (Config.Diagnostics)
                    LogEvent(LogEventLevel.Debug, "Set Project Key to {Value}", ProjectKey);
                Config.ProjectKey = ProjectKey;
            }

            if (!string.IsNullOrEmpty(InitialTimeEstimate) && DateTokens.ValidDateExpression(InitialTimeEstimate))
            {
                if (Config.Diagnostics)
                    LogEvent(LogEventLevel.Debug, "Set Initial Time Estimate to {Value}",
                        DateTokens.SetValidExpression(InitialTimeEstimate));
                Config.InitialTimeEstimate = DateTokens.SetValidExpression(InitialTimeEstimate);
            }

            if (!string.IsNullOrEmpty(RemainingTimeEstimate) && DateTokens.ValidDateExpression(RemainingTimeEstimate))
            {
                if (Config.Diagnostics)
                    LogEvent(LogEventLevel.Debug, "Set Remaining Time Estimate to {Value}",
                        DateTokens.SetValidExpression(RemainingTimeEstimate));
                Config.RemainingTimeEstimate = DateTokens.SetValidExpression(RemainingTimeEstimate);
            }

            if (!string.IsNullOrEmpty(DueDate) &&
                (DateTokens.ValidDateExpression(DueDate) || DateTokens.ValidDate(DueDate)))
            {
                if (Config.Diagnostics)
                    LogEvent(LogEventLevel.Debug, "Set Due Date to {Value}",
                        DateTokens.ValidDate(DueDate) ? DueDate : DateTokens.SetValidExpression(DueDate));
                Config.DueDate = DateTokens.ValidDate(DueDate) ? DueDate : DateTokens.SetValidExpression(DueDate);
            }

            if (Config.Diagnostics)
                LogEvent(LogEventLevel.Debug,
                    "Log level {LogLevel} will be used for threshold violations on {Instance} ...",
                    Config.ThresholdLogLevel, App.Title);

            if (Config.Diagnostics) LogEvent(LogEventLevel.Debug, "Starting timer ...");

            _timer = new Timer(1000)
            {
                AutoReset = true
            };
            _timer.Elapsed += TimerOnElapsed;
            _timer.Start();
            if (Config.Diagnostics) LogEvent(LogEventLevel.Debug, "Timer started ...");
        }


        private void TimerOnElapsed(object sender, ElapsedEventArgs e)
        {
            var timeNow = DateTime.UtcNow;
            var localDate = DateTime.Today;
            if (!string.IsNullOrEmpty(Config.TestDate))
                localDate = DateTime.ParseExact(Config.TestDate, "yyyy-M-d", CultureInfo.InvariantCulture,
                    DateTimeStyles.None);

            if (Counters.LastDay < localDate) RetrieveHolidays(localDate, timeNow);

            //We can only enter showtime if we're not currently retrying holidays, but existing showtimes will continue to monitor
            if ((!Config.UseHolidays || Counters.IsShowtime || !Counters.IsShowtime && !Counters.IsUpdating) && timeNow >= Counters.StartTime &&
                timeNow < Counters.EndTime)
            {
                if (!Counters.IsShowtime && (!Config.DaysOfWeek.Contains(Counters.StartTime.DayOfWeek) ||
                                    Config.IncludeDays.Count > 0 && !Config.IncludeDays.Contains(Counters.StartTime) ||
                                    Config.ExcludeDays.Contains(Counters.StartTime)))
                {
                    //Log that we have skipped a day due to an exclusion
                    if (!Counters.SkippedShowtime)
                        LogEvent(LogEventLevel.Debug,
                            "Threshold checking will not be performed due to exclusions - Day of Week Excluded {DayOfWeek}, Day Of Month Not Included {IncludeDay}, Day of Month Excluded {ExcludeDay} ...",
                            !Config.DaysOfWeek.Contains(Counters.StartTime.DayOfWeek),
                            Config.IncludeDays.Count > 0 && !Config.IncludeDays.Contains(Counters.StartTime),
                            Config.ExcludeDays.Count > 0 && Config.ExcludeDays.Contains(Counters.StartTime));

                    Counters.SkippedShowtime = true;
                }
                else
                {
                    //Showtime! - Count matched properties in log events
                    if (!Counters.IsShowtime)
                    {
                        LogEvent(LogEventLevel.Debug,
                            "UTC Start Time {Time} ({DayOfWeek}), monitoring for {MatchText} over {Threshold} seconds, until UTC End time {EndTime} ({EndDayOfWeek}) ...",
                            Counters.StartTime.ToShortTimeString(), Counters.StartTime.DayOfWeek,
                            PropertyMatch.MatchConditions(Config.Properties), Config.ThresholdInterval.TotalSeconds,
                            Counters.EndTime.ToShortTimeString(), Counters.EndTime.DayOfWeek);
                        Counters.IsShowtime = true;
                        Counters.LastCheck = timeNow;
                    }

                    var difference = timeNow - Counters.LastCheck;
                    //Check the interval time versus threshold count
                    if (difference.TotalSeconds > Config.ThresholdInterval.TotalSeconds)
                    {
                        if (InvertThreshold && Counters.EventCount >= Config.Threshold ||
                            !InvertThreshold && Counters.EventCount < Config.Threshold)
                        {
                            var suppressDiff = timeNow - Counters.LastLog;
                            if (Counters.IsAlert && suppressDiff.TotalSeconds < Config.SuppressionTime.TotalSeconds) return;

                            //Log event
                            LogThresholdEvent(Config.ThresholdLogLevel,
                                Config.UseHandlebars ? MessageTemplate.Render(Config, Counters) : Message,
                                Config.UseHandlebars ? DescriptionTemplate.Render(Config, Counters) : Description);

                            Counters.LastLog = timeNow;
                            Counters.IsAlert = true;
                        }
                        else
                        {
                            Counters.IsAlert = false;
                        }

                        if (Config.LogEventCount)
                            LogEvent(LogEventLevel.Debug, "Event count after {ThresholdInterval} seconds: {EventCount}",
                                Config.ThresholdInterval.TotalSeconds, Counters.EventCount);

                        //Reset the threshold counter
                        Counters.LastCheck = timeNow;
                        Counters.EventCount = 0;
                    }
                }
            }
            else if (timeNow < Counters.StartTime || timeNow >= Counters.EndTime)
            {
                //Showtime can end even if we're retrieving holidays
                if (Counters.IsShowtime)
                    LogEvent(LogEventLevel.Debug,
                        "UTC End Time {Time} ({DayOfWeek}), no longer monitoring for {MatchText}  ...",
                        Counters.EndTime.ToShortTimeString(), Counters.EndTime.DayOfWeek, PropertyMatch.MatchConditions(Config.Properties));

                //Reset the match counters
                Counters.LastLog = timeNow;
                Counters.LastCheck = timeNow;
                Counters.IsShowtime = false;
                Counters.CannotMatchAlerted = false;
                Counters.SkippedShowtime = false;
            }

            //We can only do UTC rollover if we're not currently retrying holidays and it's not during showtime
            if (Counters.IsShowtime || Config.UseHolidays && Counters.IsUpdating || Counters.StartTime > timeNow ||
                !string.IsNullOrEmpty(Config.TestDate)) return;
            UtcRollover(timeNow);
            //Take the opportunity to refresh include/exclude days to allow for month rollover
            Config.IncludeDays = Dates.GetUtcDaysOfMonth(IncludeDaysOfMonth, StartTime, Config.StartFormat, DateTime.Now);
            if (Config.IncludeDays.Count > 0)
                LogEvent(LogEventLevel.Debug, "Include UTC Days of Month: {IncludeDays} ...",
                    Config.IncludeDays.ToArray());

            Config.ExcludeDays = Dates.GetUtcDaysOfMonth(ExcludeDaysOfMonth, StartTime, Config.StartFormat, DateTime.Now);
            if (Config.ExcludeDays.Count > 0)
                LogEvent(LogEventLevel.Debug, "Exclude UTC Days of Month: {ExcludeDays} ...",
                    Config.ExcludeDays.ToArray());
        }

        /// <summary>
        ///     Create a dictionary of rules for event properties and case-insensitive text match
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, string> SetProperties()
        {
            var properties = new Dictionary<string, string>();

            //Property 1 is mandatory, and will be @Message unless PropertyName is overriden
            var property = GetProperty(1, Property1Name, TextMatch);
            properties.Add(property.Key, property.Value);
            property = GetProperty(2, Property2Name, Property2Match);
            if (!string.IsNullOrEmpty(property.Key)) properties.Add(property.Key, property.Value);

            property = GetProperty(3, Property3Name, Property3Match);
            if (!string.IsNullOrEmpty(property.Key)) properties.Add(property.Key, property.Value);

            property = GetProperty(4, Property4Name, Property4Match);
            if (!string.IsNullOrEmpty(property.Key)) properties.Add(property.Key, property.Value);

            return properties;
        }

        /// <summary>
        ///     Return a property and case-insensitive text match rule
        /// </summary>
        /// <param name="property"></param>
        /// <param name="propertyName"></param>
        /// <param name="propertyMatch"></param>
        /// <returns></returns>
        private KeyValuePair<string, string> GetProperty(int property, string propertyName, string propertyMatch)
        {
            if (Config.Diagnostics)
                LogEvent(LogEventLevel.Debug, "Validate Property {PropertyNo}: '{PropertyNameValue}' ...", property,
                    propertyName);

            var propertyResult = PropertyMatch.GetProperty(property, propertyName, propertyMatch);

            if (!string.IsNullOrEmpty(propertyResult.Key) && Config.Diagnostics)
            {
                if (!string.IsNullOrEmpty(propertyMatch))
                    LogEvent(LogEventLevel.Debug,
                        "Property {PropertyNo} '{PropertyName}' will be used to match '{PropertyMatch}'...", property,
                        propertyResult.Key, propertyResult.Value);
                else
                    LogEvent(LogEventLevel.Debug,
                        "Property {PropertyNo} '{PropertyName}' will be used to match ANY text ...", property,
                        propertyResult.Key);
            }
            else if (Config.Diagnostics)
            {
                LogEvent(LogEventLevel.Debug, "Property {PropertyNo} will not be used to match values ...", property);
            }

            return propertyResult;
        }

        /// <summary>
        ///     Configure Abstract API Holidays for this instance
        /// </summary>
        private void SetHolidays()
        {
            switch (UseHolidays)
            {
                case true when !string.IsNullOrEmpty(Country) && !string.IsNullOrEmpty(ApiKey):
                {
                    if (Config.Diagnostics) LogEvent(LogEventLevel.Debug, "Validate Country {Country}", Country);

                    if (Lurgle.Dates.Holidays.ValidateCountry(Country))
                    {
                        Config.UseHolidays = true;
                        Counters.RetryCount = 10;
                        if (RetryCount >= 0 && RetryCount <= 100)
                            Counters.RetryCount = RetryCount;
                        Config.Country = Country;
                        Config.ApiKey = ApiKey;
                        Config.IncludeWeekends = IncludeWeekends;
                        Config.IncludeBank = IncludeBank;

                        if (string.IsNullOrEmpty(HolidayMatch))
                            Config.HolidayMatch = new List<string>();
                        else
                            Config.HolidayMatch = HolidayMatch.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries)
                                .Select(t => t.Trim()).ToList();

                        if (string.IsNullOrEmpty(LocaleMatch))
                            Config.LocaleMatch = new List<string>();
                        else
                            Config.LocaleMatch = LocaleMatch.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries)
                                .Select(t => t.Trim()).ToList();

                        if (!string.IsNullOrEmpty(Proxy))
                        {
                            Config.UseProxy = true;
                            Config.Proxy = Proxy;
                            Config.BypassLocal = BypassLocal;
                            Config.LocalAddresses = LocalAddresses.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries)
                                .Select(t => t.Trim()).ToArray();
                            Config.ProxyUser = ProxyUser;
                            Config.ProxyPass = ProxyPass;
                        }

                        if (Config.Diagnostics)
                            LogEvent(LogEventLevel.Debug,
                                "Holidays API Enabled: {UseHolidays}, Country {Country}, Use Proxy {UseProxy}, Proxy Address {Proxy}, BypassLocal {BypassLocal}, Authentication {Authentication} ...",
                                Config.UseHolidays, Config.Country,
                                Config.UseProxy, Config.Proxy, Config.BypassLocal,
                                !string.IsNullOrEmpty(ProxyUser) && !string.IsNullOrEmpty(ProxyPass));

                        WebClient.SetConfig(App.Title, Config.UseProxy, Config.Proxy, Config.ProxyUser, Config.ProxyPass, Config.BypassLocal,
                            Config.LocalAddresses);
                    }
                    else
                    {
                        Config.UseHolidays = false;
                        LogEvent(LogEventLevel.Debug,
                            "Holidays API Enabled: {UseHolidays}, Could not parse country {CountryCode} to valid region ...",
                            Config.UseHolidays, Config.Country);
                    }

                    break;
                }
                case true:
                    Config.UseHolidays = false;
                    LogEvent(LogEventLevel.Debug, "Holidays API Enabled: {UseHolidays}, One or more parameters not set",
                        Config.UseHolidays);
                    break;
            }

            Counters.LastDay = DateTime.Today.AddDays(-1);
            Counters.LastError = DateTime.Now.AddDays(-1);
            Counters.LastUpdate = DateTime.Now.AddDays(-1);
            Counters.ErrorCount = 0;
            Config.TestDate = TestDate;
            Config.Holidays = new List<AbstractApiHolidays>();
        }

        /// <summary>
        ///     Update AbstractAPI Holidays for this instance, given local and UTC date
        /// </summary>
        /// <param name="localDate"></param>
        /// <param name="utcDate"></param>
        private void RetrieveHolidays(DateTime localDate, DateTime utcDate)
        {
            if (Config.UseHolidays && (!Counters.IsUpdating || Counters.IsUpdating && (DateTime.Now - Counters.LastUpdate).TotalSeconds > 10 &&
                (DateTime.Now - Counters.LastError).TotalSeconds > 10 && Counters.ErrorCount < Counters.RetryCount))
            {
                Counters.IsUpdating = true;
                if (!string.IsNullOrEmpty(Config.TestDate))
                    localDate = DateTime.ParseExact(Config.TestDate, "yyyy-M-d", CultureInfo.InvariantCulture,
                        DateTimeStyles.None);

                if (Config.Diagnostics)
                    LogEvent(LogEventLevel.Debug,
                        "Retrieve holidays for {Date}, Last Update {lastUpdateDate} {lastUpdateTime} ...",
                        localDate.ToShortDateString(), Counters.LastUpdate.ToShortDateString(),
                        Counters.LastUpdate.ToShortTimeString());

                var holidayUrl = WebClient.GetUrl(Config.ApiKey, Config.Country, localDate);
                if (Config.Diagnostics) LogEvent(LogEventLevel.Debug, "URL used is {url} ...", holidayUrl);

                try
                {
                    Counters.LastUpdate = DateTime.Now;
                    var result = WebClient.GetHolidays(Config.ApiKey, Config.Country, localDate).Result;
                    Config.Holidays = Lurgle.Dates.Holidays.ValidateHolidays(result, Config.HolidayMatch, Config.LocaleMatch, Config.IncludeBank,
                        Config.IncludeWeekends);
                    Counters.LastDay = localDate;
                    Counters.ErrorCount = 0;

                    if (Config.Diagnostics && !string.IsNullOrEmpty(Config.TestDate))
                    {
                        LogEvent(LogEventLevel.Debug,
                            "Test date {testDate} used, raw holidays retrieved {testCount} ...", Config.TestDate,
                            result.Count);
                        foreach (var holiday in result)
                            LogEvent(LogEventLevel.Debug,
                                "Holiday Name: {Name}, Local Name {LocalName}, Start {LocalStart}, Start UTC {Start}, End UTC {End}, Type {Type}, Location string {Location}, Locations parsed {Locations} ...",
                                holiday.Name, holiday.Name_Local, holiday.LocalStart, holiday.UtcStart, holiday.UtcEnd,
                                holiday.Type, holiday.Location, holiday.Locations.ToArray());
                    }

                    LogEvent(LogEventLevel.Debug, "Holidays retrieved and validated {holidayCount} ...",
                        Config.Holidays.Count);
                    foreach (var holiday in Config.Holidays)
                        LogEvent(LogEventLevel.Debug,
                            "Holiday Name: {Name}, Local Name {LocalName}, Start {LocalStart}, Start UTC {Start}, End UTC {End}, Type {Type}, Location string {Location}, Locations parsed {Locations} ...",
                            holiday.Name, holiday.Name_Local, holiday.LocalStart, holiday.UtcStart, holiday.UtcEnd,
                            holiday.Type, holiday.Location, holiday.Locations.ToArray());

                    Counters.IsUpdating = false;
                    if (!Counters.IsShowtime) UtcRollover(utcDate, true);
                }
                catch (Exception ex)
                {
                    Counters.ErrorCount++;
                    LogEvent(LogEventLevel.Debug, ex,
                        "Error {Error} retrieving holidays, public holidays cannot be evaluated (Try {Count} of {retryCount})...",
                        ex.Message, Counters.ErrorCount, Counters.RetryCount);
                    Counters.LastError = DateTime.Now;
                }
            }
            else if (!Config.UseHolidays || Counters.IsUpdating && Counters.ErrorCount >= 10)
            {
                Counters.IsUpdating = false;
                Counters.LastDay = localDate;
                Counters.ErrorCount = 0;
                Config.Holidays = new List<AbstractApiHolidays>();
                if (Config.UseHolidays && !Counters.IsShowtime) UtcRollover(utcDate, true);
            }
        }

        /// <summary>
        ///     Day rollover based on UTC date
        /// </summary>
        /// <param name="utcDate"></param>
        /// <param name="isUpdateHolidays"></param>
        public void UtcRollover(DateTime utcDate, bool isUpdateHolidays = false)
        {
            LogEvent(LogEventLevel.Debug, "UTC Time is currently {UtcTime} ...",
                Config.UseTestOverrideTime
                    ? Config.TestOverrideTime.ToUniversalTime().ToShortTimeString()
                    : DateTime.Now.ToUniversalTime().ToShortTimeString());

            //Day rollover, we need to ensure the next start and end is in the future
            if (!string.IsNullOrEmpty(Config.TestDate))
                Counters.StartTime =
                    Dates.ParseUtcIntervalDate(Config.TestDate, StartTime, timeFormat: Config.StartFormat);
            else if (Config.UseTestOverrideTime)
                Counters.StartTime =
                    Dates.ParseUtcIntervalDate(Config.TestOverrideTime, StartTime, timeFormat: Config.StartFormat);
            else
                Counters.StartTime =
                    Dates.ParseUtcIntervalDate(DateTime.Today, StartTime,
                        timeFormat: Config.StartFormat);

            if (!string.IsNullOrEmpty(Config.TestDate))
                Counters.EndTime = Dates.ParseUtcIntervalDate(Config.TestDate, EndTime, timeFormat: Config.EndFormat);
            else if (Config.UseTestOverrideTime)
                Counters.EndTime =
                    Dates.ParseUtcIntervalDate(Config.TestOverrideTime, EndTime, timeFormat: Config.EndFormat);
            else
                Counters.EndTime =
                    Dates.ParseUtcIntervalDate(DateTime.Today, EndTime,
                        timeFormat: Config.EndFormat);

            //Detect a 24  hour instance and handle it
            if (Counters.EndTime == Counters.StartTime)
            {
                Counters.EndTime = GetNextEnd(GetNextEnd(1) <= Counters.StartTime ? 2 : 1);
                Config.Is24H = true;
            }

            //If there are holidays, account for them
            if (Config.Holidays.Any(holiday =>
                Counters.StartTime >= holiday.UtcStart && Counters.StartTime < holiday.UtcEnd))
            {
                Counters.StartTime = GetNextStart(Config.Holidays.Any(holiday =>
                    GetNextStart(1) >= holiday.UtcStart && GetNextStart(1) < holiday.UtcEnd)
                    ? 2
                    : 1);

                Counters.EndTime = GetNextEnd(GetNextEnd(1) < Counters.StartTime ? 2 : 1);
            }

            //If we updated holidays or this is a 24h instance, don't automatically put start time to the future
            if (!Config.Is24H &&
                (!Config.UseTestOverrideTime && Counters.StartTime < utcDate || Config.UseTestOverrideTime &&
                    Counters.StartTime < Config.TestOverrideTime.ToUniversalTime()) &&
                !isUpdateHolidays)
                Counters.StartTime = GetNextStart(GetNextStart(1) < utcDate ? 2 : 1);

            if (Counters.EndTime < Counters.StartTime)
                Counters.EndTime = GetNextEnd(GetNextEnd(1) < Counters.StartTime ? 2 : 1);

            LogEvent(LogEventLevel.Debug,
                isUpdateHolidays
                    ? "UTC Day Rollover (Holidays Updated), Parse {LocalStart} To Next UTC Start Time {StartTime} ({StartDayOfWeek}), Parse {LocalEnd} to UTC End Time {EndTime} ({EndDayOfWeek})..."
                    : "UTC Day Rollover, Parse {LocalStart} To Next UTC Start Time {StartTime} ({StartDayOfWeek}), Parse {LocalEnd} to UTC End Time {EndTime} ({EndDayOfWeek})...",
                StartTime, Counters.StartTime.ToShortTimeString(), Counters.StartTime.DayOfWeek, EndTime,
                Counters.EndTime.ToShortTimeString(), Counters.EndTime.DayOfWeek);
        }

        public DateTime GetNextStart(int addDays = 0)
        {
            return Dates.ParseUtcIntervalDate(Counters.StartTime.AddDays(addDays), StartTime, Config.StartFormat);
        }

        public DateTime GetNextEnd(int addDays = 0)
        {
            return Dates.ParseUtcIntervalDate(Counters.EndTime.AddDays(addDays), EndTime, Config.EndFormat);
        }
        
        public Showtime GetShowtime()
        {
            return new Showtime(Counters.StartTime, Counters.EndTime);
        }

        /// <summary>
        ///     Output a timeout event that always defines the Message and Description tags for use with other apps
        /// </summary>
        /// <param name="logLevel"></param>
        /// <param name="message"></param>
        /// <param name="description"></param>
        /// <param name="token"></param>
        private void LogThresholdEvent(LogEventLevel logLevel, string message, string description,
            KeyValuePair<string, string>? token = null)
        {
            var include = "{AppName} - ";
            if (!Config.IncludeApp) include = string.Empty;

            Log.ForContext(nameof(Tags),
                    Config.IsTags ? DateTokens.HandleTokens(Config.Tags, token) : new List<string>())
                .ForContext("AppName", Config.AppName)
                .ForContext(nameof(Priority), Config.Priority).ForContext(nameof(Responders), Config.Responders)
                .ForContext(nameof(InitialTimeEstimate), Config.InitialTimeEstimate)
                .ForContext(nameof(RemainingTimeEstimate), Config.RemainingTimeEstimate)
                .ForContext(nameof(ProjectKey), Config.ProjectKey).ForContext(nameof(DueDate), Config.DueDate)
                .ForContext("EventCount", Counters.EventCount).ForContext("Message", DateTokens.HandleTokens(message))
                .ForContext("Description", DateTokens.HandleTokens(description))
                .Write((Serilog.Events.LogEventLevel)logLevel,
                    string.IsNullOrEmpty(description) || !Config.IncludeDescription
                        ? include + "{Message}"
                        : include + "{Message} : {Description}");
        }

        /// <summary>
        ///     Output a log event to Seq stream
        /// </summary>
        /// <param name="logLevel"></param>
        /// <param name="message"></param>
        /// <param name="args"></param>
        private void LogEvent(LogEventLevel logLevel, string message, params object[] args)
        {
            var logArgsList = args.ToList();

            if (Config.IncludeApp) logArgsList.Insert(0, Config.AppName);

            var logArgs = logArgsList.ToArray();

            if (Config.IsTags)
                Log.ForContext(nameof(Tags), Config.Tags).ForContext("AppName", Config.AppName)
                    .ForContext(nameof(Priority), Config.Priority).ForContext(nameof(Responders), Config.Responders)
                    .ForContext(nameof(Counters.EventCount), Counters.EventCount).ForContext(nameof(InitialTimeEstimate), Config.InitialTimeEstimate)
                    .ForContext(nameof(RemainingTimeEstimate), Config.RemainingTimeEstimate)
                    .ForContext(nameof(ProjectKey), Config.ProjectKey).ForContext(nameof(DueDate), Config.DueDate)
                    .Write((Serilog.Events.LogEventLevel)logLevel,
                        Config.IncludeApp ? "[{AppName}] - " + message : message, logArgs);
            else
                Log.ForContext("AppName", Config.AppName).ForContext(nameof(Priority), Config.Priority)
                    .ForContext(nameof(Responders), Config.Responders)
                    .ForContext(nameof(InitialTimeEstimate), Config.InitialTimeEstimate)
                    .ForContext(nameof(RemainingTimeEstimate), Config.RemainingTimeEstimate)
                    .ForContext(nameof(ProjectKey), Config.ProjectKey).ForContext(nameof(DueDate), Config.DueDate)
                    .Write((Serilog.Events.LogEventLevel)logLevel,
                        Config.IncludeApp ? "[{AppName}] - " + message : message, logArgs);
        }

        /// <summary>
        ///     Output an exception log event to Seq stream
        /// </summary>
        /// <param name="logLevel"></param>
        /// <param name="exception"></param>
        /// <param name="message"></param>
        /// <param name="args"></param>
        private void LogEvent(LogEventLevel logLevel, Exception exception, string message, params object[] args)
        {
            var logArgsList = args.ToList();

            if (Config.IncludeApp) logArgsList.Insert(0, Config.AppName);

            var logArgs = logArgsList.ToArray();

            if (Config.IsTags)
                Log.ForContext(nameof(Tags), Config.Tags).ForContext("AppName", Config.AppName)
                    .ForContext(nameof(Priority), Config.Priority).ForContext(nameof(Responders), Config.Responders)
                    .ForContext(nameof(Counters.EventCount), Counters.EventCount).ForContext(nameof(InitialTimeEstimate), Config.InitialTimeEstimate)
                    .ForContext(nameof(RemainingTimeEstimate), Config.RemainingTimeEstimate)
                    .ForContext(nameof(ProjectKey), Config.ProjectKey).ForContext(nameof(DueDate), Config.DueDate)
                    .Write((Serilog.Events.LogEventLevel)logLevel, exception,
                        Config.IncludeApp ? "[{AppName}] - " + message : message, logArgs);
            else
                Log.ForContext("AppName", Config.AppName).ForContext(nameof(Priority), Config.Priority)
                    .ForContext(nameof(Responders), Config.Responders)
                    .ForContext(nameof(InitialTimeEstimate), Config.InitialTimeEstimate)
                    .ForContext(nameof(RemainingTimeEstimate), Config.RemainingTimeEstimate)
                    .ForContext(nameof(ProjectKey), Config.ProjectKey).ForContext(nameof(DueDate), Config.DueDate)
                    .Write((Serilog.Events.LogEventLevel)logLevel, exception,
                        Config.IncludeApp ? "[{AppName}] - " + message : message, logArgs);
        }
    }
}