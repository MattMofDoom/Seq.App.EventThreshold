using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Timers;
using Seq.App.EventThreshold.Classes;
using Seq.Apps;
using Seq.Apps.LogEvents;

namespace Seq.App.EventThreshold
{
    [SeqApp("Event Threshold", AllowReprocessing = false,
        Description =
            "Super-powered Seq event thresholds with start/end times, measuring and suppression intervals, property matching, day of week and day of month inclusion/exclusion, and optional holiday API!")]
    // ReSharper disable once UnusedType.Global
    public class EventThresholdReactor : SeqApp, ISubscribeTo<LogEventData>
    {
        private string _alertDescription;
        private string _alertMessage;
        private string _apiKey;
        private bool _bypassLocal;
        private bool _cannotMatchAlerted;
        private string _country;
        private List<DayOfWeek> _daysOfWeek;
        private bool _diagnostics;
        private string _endFormat = "H:mm:ss";
        private DateTime _endTime;
        private int _errorCount;
        private List<int> _excludeDays;
        private List<string> _holidayMatch;
        private bool _includeApp;
        private bool _includeBank;
        private List<int> _includeDays;
        private bool _includeWeekends;
        private bool _invertThreshold;

        private bool _is24H;
        private bool _isTags;
        private bool _isUpdating;
        private DateTime _lastCheck;
        private DateTime _lastDay;
        private DateTime _lastError;
        private DateTime _lastLog;
        private DateTime _lastUpdate;
        private string[] _localAddresses;

        private List<string> _localeMatch;
        private bool _logEventCount;

        private string _priority;
        private Dictionary<string, string> _properties;
        private string _proxy;
        private string _proxyPass;
        private string _proxyUser;
        private string _responders;
        private int _retryCount;
        private bool _skippedShowtime;
        private string _startFormat = "H:mm:ss";
        private DateTime _startTime;
        private TimeSpan _suppressionTime;
        private string[] _tags;
        private string _testDate;
        private int _threshold;
        private TimeSpan _thresholdInterval;
        private LogEventLevel _thresholdLogLevel;
        private Timer _timer;
        private bool _useHolidays;
        private bool _useProxy;
        public int EventCount;
        public List<AbstractApiHolidays> Holidays;
        public bool IsAlert;
        public bool IsShowtime;
        public DateTime TestOverrideTime = DateTime.Now;
        public bool UseTestOverrideTime; // ReSharper disable MemberCanBePrivate.Global
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
            HelpText = "Event message to raise.")]
        public string AlertMessage { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Alert description",
            HelpText = "Optional description associated with the event raised.")]
        public string AlertDescription { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Alert tags",
            HelpText = "Tags for the event, separated by commas.")]
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

            if (!IsShowtime) return;
            foreach (var property in _properties)
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
                            if (string.IsNullOrEmpty(property.Value) || !PropertyMatch.Matches(evt.Data.Properties[property.Key].ToString(),
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
                case true when !_cannotMatchAlerted:
                    LogEvent(LogEventLevel.Debug,
                        "Warning - An event was seen without the properties {PropertyName}, which may impact the ability to alert on failures - further failures will not be logged ...",
                        string.Join(",", cannotMatchProperties.ToArray()));
                    _cannotMatchAlerted = true;
                    break;
                //If all configured properties were present and had matches, log an event
                case false when properties == matches:
                {
                    EventCount++;
                    break;
                }
            }
        }

        protected override void OnAttached()
        {
            LogEvent(LogEventLevel.Debug, "Check {AppName} diagnostic level ({Diagnostics}) ...", App.Title,
                Diagnostics);
            _diagnostics = Diagnostics;

            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Check include {AppName} ({IncludeApp}) ...", App.Title, IncludeApp);

            _includeApp = IncludeApp;
            if (!_includeApp && _diagnostics)
                LogEvent(LogEventLevel.Debug, "App name {AppName} will not be included in alert message ...",
                    App.Title);
            else if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "App name {AppName} will be included in alert message ...", App.Title);

            if (!DateTime.TryParseExact(StartTime, "H:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None,
                out _))
            {
                if (DateTime.TryParseExact(StartTime, "H:mm", CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out _))
                    _startFormat = "H:mm";
                else
                    LogEvent(LogEventLevel.Debug,
                        "Start Time {StartTime} does  not parse to a valid DateTime - app will exit ...", StartTime);
            }

            if (!DateTime.TryParseExact(EndTime, "H:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None,
                out _))
            {
                if (DateTime.TryParseExact(EndTime, "H:mm", CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out _))
                    _endFormat = "H:mm";
                else
                    LogEvent(LogEventLevel.Debug,
                        "End Time {EndTime} does  not parse to a valid DateTime - app will exit ...", EndTime);
            }

            _invertThreshold = InvertThreshold;
            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Invert threshold to Greater Than or Equal To (>=): {InvertThreshold}",
                    _invertThreshold);

            if (Threshold <= 0)
                Threshold = 1;
            _threshold = Threshold;
            if (_diagnostics)
                LogEvent(LogEventLevel.Debug,
                    _invertThreshold
                        ? "Threshold for events is 'Greater Than or Equal To {Threshold}' ..."
                        : "Threshold for events is 'Less Than or Equal To {Threshold}' ...", _threshold);


            _logEventCount = LogEventCount;
            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Log events at end of each interval {LogEventCount}", _logEventCount);

            LogEvent(LogEventLevel.Debug,
                "Use Holidays API {UseHolidays}, Country {Country}, Has API key {IsEmpty} ...", UseHolidays, Country,
                !string.IsNullOrEmpty(ApiKey));
            SetHolidays();
            RetrieveHolidays(DateTime.Today, DateTime.UtcNow);

            if (!_useHolidays || _isUpdating) UtcRollover(DateTime.UtcNow);

            //Enforce minimum threshold interval
            if (ThresholdInterval <= 0)
                ThresholdInterval = 1;
            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Convert Threshold Interval {ThresholdInterval} to TimeSpan ...",
                    ThresholdInterval);

            _thresholdInterval = TimeSpan.FromSeconds(ThresholdInterval);
            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Parsed Threshold Interval is {Interval} ...",
                    _thresholdInterval.TotalSeconds);

            //Negative values not permitted
            if (SuppressionTime < 0)
                SuppressionTime = 0;
            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Convert Suppression {Suppression} to TimeSpan ...", SuppressionTime);

            _suppressionTime = TimeSpan.FromSeconds(SuppressionTime);
            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Parsed Suppression is {Suppression} ...", _suppressionTime.TotalSeconds);

            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Convert Days of Week {DaysOfWeek} to UTC Days of Week ...", DaysOfWeek);
            _daysOfWeek = Dates.GetDaysOfWeek(DaysOfWeek, StartTime, _startFormat);

            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "UTC Days of Week {DaysOfWeek} will be used ...", _daysOfWeek.ToArray());

            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Validate Include Days of Month {IncludeDays} ...", IncludeDaysOfMonth);

            _includeDays = Dates.GetDaysOfMonth(IncludeDaysOfMonth, StartTime, _startFormat);
            if (_includeDays.Count > 0)
                LogEvent(LogEventLevel.Debug, "Include UTC Days of Month: {IncludeDays} ...", _includeDays.ToArray());
            else
                LogEvent(LogEventLevel.Debug, "Include UTC Days of Month: ALL ...");

            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Validate Exclude Days of Month {ExcludeDays} ...", ExcludeDaysOfMonth);

            _excludeDays = Dates.GetDaysOfMonth(ExcludeDaysOfMonth, StartTime, _startFormat);
            if (_excludeDays.Count > 0)
                LogEvent(LogEventLevel.Debug, "Exclude UTC Days of Month: {ExcludeDays} ...", _excludeDays.ToArray());
            else
                LogEvent(LogEventLevel.Debug, "Exclude UTC Days of Month: NONE ...");

            //Evaluate the properties we will match
            _properties = SetProperties();
            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Match criteria will be: {MatchText}",
                    PropertyMatch.MatchConditions(_properties));

            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Validate Alert Message '{AlertMessage}' ...", AlertMessage);

            _alertMessage = string.IsNullOrWhiteSpace(AlertMessage)
                ? "A threshold violation has occurred!"
                : AlertMessage.Trim();
            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Alert Message '{AlertMessage}' will be used ...", _alertMessage);

            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Validate Alert Description '{AlertDescription}' ...", AlertDescription);

            _alertDescription = string.IsNullOrWhiteSpace(AlertDescription)
                ? ""
                : AlertDescription.Trim();
            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Alert Description '{AlertDescription}' will be used ...",
                    _alertDescription);

            if (_diagnostics) LogEvent(LogEventLevel.Debug, "Convert Tags '{Tags}' to array ...", Tags);

            _tags = (Tags ?? "")
                .Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .ToArray();
            if (_tags.Length > 0) _isTags = true;

            if (string.IsNullOrWhiteSpace(ThresholdLogLevel)) ThresholdLogLevel = "Error";
            if (!Enum.TryParse(ThresholdLogLevel, out _thresholdLogLevel)) _thresholdLogLevel = LogEventLevel.Error;

            if (!string.IsNullOrEmpty(Priority))
                _priority = Priority;

            if (!string.IsNullOrEmpty(Responders))
                _responders = Responders;

            if (_diagnostics)
                LogEvent(LogEventLevel.Debug,
                    "Log level {LogLevel} will be used for threshold violations on {Instance} ...",
                    _thresholdLogLevel, App.Title);

            if (_diagnostics) LogEvent(LogEventLevel.Debug, "Starting timer ...");

            _timer = new Timer(1000)
            {
                AutoReset = true
            };
            _timer.Elapsed += TimerOnElapsed;
            _timer.Start();
            if (_diagnostics) LogEvent(LogEventLevel.Debug, "Timer started ...");
        }


        private void TimerOnElapsed(object sender, ElapsedEventArgs e)
        {
            var timeNow = DateTime.UtcNow;
            var localDate = DateTime.Today;
            if (!string.IsNullOrEmpty(_testDate))
                localDate = DateTime.ParseExact(_testDate, "yyyy-M-d", CultureInfo.InvariantCulture,
                    DateTimeStyles.None);

            if (_lastDay < localDate) RetrieveHolidays(localDate, timeNow);

            //We can only enter showtime if we're not currently retrying holidays, but existing showtimes will continue to monitor
            if ((!_useHolidays || IsShowtime || !IsShowtime && !_isUpdating) && timeNow >= _startTime &&
                timeNow < _endTime)
            {
                if (!IsShowtime && (!_daysOfWeek.Contains(_startTime.DayOfWeek) ||
                                    _includeDays.Count > 0 && !_includeDays.Contains(_startTime.Day) ||
                                    _excludeDays.Contains(_startTime.Day)))
                {
                    //Log that we have skipped a day due to an exclusion
                    if (!_skippedShowtime)
                        LogEvent(LogEventLevel.Debug,
                            "Threshold checking will not be performed due to exclusions - Day of Week Excluded {DayOfWeek}, Day Of Month Not Included {IncludeDay}, Day of Month Excluded {ExcludeDay} ...",
                            !_daysOfWeek.Contains(_startTime.DayOfWeek),
                            _includeDays.Count > 0 && !_includeDays.Contains(_startTime.Day),
                            _excludeDays.Count > 0 && _excludeDays.Contains(_startTime.Day));

                    _skippedShowtime = true;
                }
                else
                {
                    //Showtime! - Count matched properties in log events
                    if (!IsShowtime)
                    {
                        LogEvent(LogEventLevel.Debug,
                            "UTC Start Time {Time} ({DayOfWeek}), monitoring for {MatchText} over {Threshold} seconds, until UTC End time {EndTime} ({EndDayOfWeek}) ...",
                            _startTime.ToShortTimeString(), _startTime.DayOfWeek,
                            PropertyMatch.MatchConditions(_properties), _thresholdInterval.TotalSeconds,
                            _endTime.ToShortTimeString(), _endTime.DayOfWeek);
                        IsShowtime = true;
                        _lastCheck = timeNow;
                    }

                    var difference = timeNow - _lastCheck;
                    //Check the interval time versus threshold count
                    if (difference.TotalSeconds > _thresholdInterval.TotalSeconds)
                    {
                        if (InvertThreshold && EventCount >= _threshold ||
                            !InvertThreshold && EventCount < _threshold)
                        {
                            var suppressDiff = timeNow - _lastLog;
                            if (IsAlert && suppressDiff.TotalSeconds < _suppressionTime.TotalSeconds) return;

                            //Log event
                                LogEvent(_thresholdLogLevel,
                                    string.IsNullOrEmpty(_alertDescription) ? "{Message}" : "{Message} : {Description}",
                                    _alertMessage, _alertDescription);

                                _lastLog = timeNow;
                                IsAlert = true;
                        }
                        else
                        {
                            IsAlert = false;
                        }

                        if (_logEventCount)
                            LogEvent(LogEventLevel.Debug, "Event count after {ThresholdInterval} seconds: {EventCount}",
                                _thresholdInterval.TotalSeconds, EventCount);

                        //Reset the threshold counter
                        _lastCheck = timeNow;
                        EventCount = 0;
                    }
                }
            }
            else if (timeNow < _startTime || timeNow >= _endTime)
            {
                //Showtime can end even if we're retrieving holidays
                if (IsShowtime)
                    LogEvent(LogEventLevel.Debug,
                        "UTC End Time {Time} ({DayOfWeek}), no longer monitoring for {MatchText}  ...",
                        _endTime.ToShortTimeString(), _endTime.DayOfWeek, PropertyMatch.MatchConditions(_properties));

                //Reset the match counters
                _lastLog = timeNow;
                _lastCheck = timeNow;
                IsShowtime = false;
                _cannotMatchAlerted = false;
                _skippedShowtime = false;
            }

            //We can only do UTC rollover if we're not currently retrying holidays and it's not during showtime
            if (IsShowtime || _useHolidays && _isUpdating || _startTime > timeNow ||
                !string.IsNullOrEmpty(_testDate)) return;
            UtcRollover(timeNow);
            //Take the opportunity to refresh include/exclude days to allow for month rollover
            _includeDays = Dates.GetDaysOfMonth(IncludeDaysOfMonth, StartTime, _startFormat);
            if (_includeDays.Count > 0)
                LogEvent(LogEventLevel.Debug, "Include UTC Days of Month: {includedays} ...",
                    _includeDays.ToArray());

            _excludeDays = Dates.GetDaysOfMonth(ExcludeDaysOfMonth, StartTime, _startFormat);
            if (_excludeDays.Count > 0)
                LogEvent(LogEventLevel.Debug, "Exclude UTC Days of Month: {excludedays} ...",
                    _excludeDays.ToArray());
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
            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Validate Property {PropertyNo}: '{PropertyNameValue}' ...", property,
                    propertyName);

            var propertyResult = PropertyMatch.GetProperty(property, propertyName, propertyMatch);

            if (!string.IsNullOrEmpty(propertyResult.Key) && _diagnostics)
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
            else if (_diagnostics)
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
                    if (_diagnostics) LogEvent(LogEventLevel.Debug, "Validate Country {Country}", Country);

                    if (Classes.Holidays.ValidateCountry(Country))
                    {
                        _useHolidays = true;
                        _retryCount = 10;
                        if (RetryCount >= 0 && RetryCount <= 100)
                            _retryCount = RetryCount;
                        _country = Country;
                        _apiKey = ApiKey;
                        _includeWeekends = IncludeWeekends;
                        _includeBank = IncludeBank;

                        if (string.IsNullOrEmpty(HolidayMatch))
                            _holidayMatch = new List<string>();
                        else
                            _holidayMatch = HolidayMatch.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries)
                                .Select(t => t.Trim()).ToList();

                        if (string.IsNullOrEmpty(LocaleMatch))
                            _localeMatch = new List<string>();
                        else
                            _localeMatch = LocaleMatch.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries)
                                .Select(t => t.Trim()).ToList();

                        if (!string.IsNullOrEmpty(Proxy))
                        {
                            _useProxy = true;
                            _proxy = Proxy;
                            _bypassLocal = BypassLocal;
                            _localAddresses = LocalAddresses.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries)
                                .Select(t => t.Trim()).ToArray();
                            _proxyUser = ProxyUser;
                            _proxyPass = ProxyPass;
                        }

                        if (_diagnostics)
                            LogEvent(LogEventLevel.Debug,
                                "Holidays API Enabled: {UseHolidays}, Country {Country}, Use Proxy {UseProxy}, Proxy Address {Proxy}, BypassLocal {BypassLocal}, Authentication {Authentication} ...",
                                _useHolidays, _country,
                                _useProxy, _proxy, _bypassLocal,
                                !string.IsNullOrEmpty(ProxyUser) && !string.IsNullOrEmpty(ProxyPass));

                        WebClient.SetFlurlConfig(App.Title, _useProxy, _proxy, _proxyUser, _proxyPass, _bypassLocal,
                            _localAddresses);
                    }
                    else
                    {
                        _useHolidays = false;
                        LogEvent(LogEventLevel.Debug,
                            "Holidays API Enabled: {UseHolidays}, Could not parse country {CountryCode} to valid region ...",
                            _useHolidays, _country);
                    }

                    break;
                }
                case true:
                    _useHolidays = false;
                    LogEvent(LogEventLevel.Debug, "Holidays API Enabled: {UseHolidays}, One or more parameters not set",
                        _useHolidays);
                    break;
            }

            _lastDay = DateTime.Today.AddDays(-1);
            _lastError = DateTime.Now.AddDays(-1);
            _lastUpdate = DateTime.Now.AddDays(-1);
            _errorCount = 0;
            _testDate = TestDate;
            Holidays = new List<AbstractApiHolidays>();
        }

        /// <summary>
        ///     Update AbstractAPI Holidays for this instance, given local and UTC date
        /// </summary>
        /// <param name="localDate"></param>
        /// <param name="utcDate"></param>
        private void RetrieveHolidays(DateTime localDate, DateTime utcDate)
        {
            if (_useHolidays && (!_isUpdating || _isUpdating && (DateTime.Now - _lastUpdate).TotalSeconds > 10 &&
                (DateTime.Now - _lastError).TotalSeconds > 10 && _errorCount < _retryCount))
            {
                _isUpdating = true;
                if (!string.IsNullOrEmpty(_testDate))
                    localDate = DateTime.ParseExact(_testDate, "yyyy-M-d", CultureInfo.InvariantCulture,
                        DateTimeStyles.None);

                if (_diagnostics)
                    LogEvent(LogEventLevel.Debug,
                        "Retrieve holidays for {Date}, Last Update {lastUpdateDate} {lastUpdateTime} ...",
                        localDate.ToShortDateString(), _lastUpdate.ToShortDateString(),
                        _lastUpdate.ToShortTimeString());

                var holidayUrl = WebClient.GetUrl(_apiKey, _country, localDate);
                if (_diagnostics) LogEvent(LogEventLevel.Debug, "URL used is {url} ...", holidayUrl);

                try
                {
                    _lastUpdate = DateTime.Now;
                    var result = WebClient.GetHolidays(_apiKey, _country, localDate).Result;
                    Holidays = Classes.Holidays.ValidateHolidays(result, _holidayMatch, _localeMatch, _includeBank,
                        _includeWeekends);
                    _lastDay = localDate;
                    _errorCount = 0;

                    if (_diagnostics && !string.IsNullOrEmpty(_testDate))
                    {
                        LogEvent(LogEventLevel.Debug,
                            "Test date {testDate} used, raw holidays retrieved {testCount} ...", _testDate,
                            result.Count);
                        foreach (var holiday in result)
                            LogEvent(LogEventLevel.Debug,
                                "Holiday Name: {Name}, Local Name {LocalName}, Start {LocalStart}, Start UTC {Start}, End UTC {End}, Type {Type}, Location string {Location}, Locations parsed {Locations} ...",
                                holiday.Name, holiday.Name_Local, holiday.LocalStart, holiday.UtcStart, holiday.UtcEnd,
                                holiday.Type, holiday.Location, holiday.Locations.ToArray());
                    }

                    LogEvent(LogEventLevel.Debug, "Holidays retrieved and validated {holidayCount} ...",
                        Holidays.Count);
                    foreach (var holiday in Holidays)
                        LogEvent(LogEventLevel.Debug,
                            "Holiday Name: {Name}, Local Name {LocalName}, Start {LocalStart}, Start UTC {Start}, End UTC {End}, Type {Type}, Location string {Location}, Locations parsed {Locations} ...",
                            holiday.Name, holiday.Name_Local, holiday.LocalStart, holiday.UtcStart, holiday.UtcEnd,
                            holiday.Type, holiday.Location, holiday.Locations.ToArray());

                    _isUpdating = false;
                    if (!IsShowtime) UtcRollover(utcDate, true);
                }
                catch (Exception ex)
                {
                    _errorCount++;
                    LogEvent(LogEventLevel.Debug, ex,
                        "Error {Error} retrieving holidays, public holidays cannot be evaluated (Try {Count} of {retryCount})...",
                        ex.Message, _errorCount, _retryCount);
                    _lastError = DateTime.Now;
                }
            }
            else if (!_useHolidays || _isUpdating && _errorCount >= 10)
            {
                _isUpdating = false;
                _lastDay = localDate;
                _errorCount = 0;
                Holidays = new List<AbstractApiHolidays>();
                if (_useHolidays && !IsShowtime) UtcRollover(utcDate, true);
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
                UseTestOverrideTime
                    ? TestOverrideTime.ToUniversalTime().ToShortTimeString()
                    : DateTime.Now.ToUniversalTime().ToShortTimeString());

            //Day rollover, we need to ensure the next start and end is in the future
            if (!string.IsNullOrEmpty(_testDate))
                _startTime = DateTime.ParseExact(_testDate + " " + StartTime, "yyyy-M-d " + _startFormat,
                    CultureInfo.InvariantCulture, DateTimeStyles.None).ToUniversalTime();
            else if (UseTestOverrideTime)
                _startTime = DateTime
                    .ParseExact(TestOverrideTime.ToString("yyyy-M-d") + " " + StartTime, "yyyy-M-d " + _startFormat,
                        CultureInfo.InvariantCulture, DateTimeStyles.None)
                    .ToUniversalTime();
            else
                _startTime = DateTime
                    .ParseExact(StartTime, _startFormat, CultureInfo.InvariantCulture, DateTimeStyles.None)
                    .ToUniversalTime();

            if (!string.IsNullOrEmpty(_testDate))
                _endTime = DateTime.ParseExact(_testDate + " " + EndTime, "yyyy-M-d " + _endFormat,
                    CultureInfo.InvariantCulture, DateTimeStyles.None).ToUniversalTime();
            else if (UseTestOverrideTime)
                _endTime = DateTime.ParseExact(TestOverrideTime.ToString("yyyy-M-d") + " " + EndTime,
                    "yyyy-M-d " + _endFormat,
                    CultureInfo.InvariantCulture, DateTimeStyles.None).ToUniversalTime();
            else
                _endTime = DateTime.ParseExact(EndTime, _endFormat, CultureInfo.InvariantCulture, DateTimeStyles.None)
                    .ToUniversalTime();

            //Detect a 24  hour instance and handle it
            if (_endTime == _startTime)
            {
                _endTime = _endTime.AddDays(1);
                _is24H = true;
            }

            //If there are holidays, account for them
            if (Holidays.Any(holiday => _startTime >= holiday.UtcStart && _startTime < holiday.UtcEnd))
            {
                _startTime = _startTime.AddDays(Holidays.Any(holiday =>
                    _startTime.AddDays(1) >= holiday.UtcStart && _startTime.AddDays(1) < holiday.UtcEnd)
                    ? 2
                    : 1);
                _endTime = _endTime.AddDays(_endTime.AddDays(1) < _startTime ? 2 : 1);
            }

            //If we updated holidays or this is a 24h instance, don't automatically put start time to the future
            if (!_is24H &&
                (!UseTestOverrideTime && _startTime < utcDate ||
                 UseTestOverrideTime && _startTime < TestOverrideTime.ToUniversalTime()) &&
                !isUpdateHolidays) _startTime = _startTime.AddDays(1);

            if (_endTime < _startTime)
                _endTime = _endTime.AddDays(_endTime.AddDays(1) < _startTime ? 2 : 1);

            LogEvent(LogEventLevel.Debug,
                isUpdateHolidays
                    ? "UTC Day Rollover (Holidays Updated), Parse {LocalStart} To Next UTC Start Time {StartTime} ({StartDayOfWeek}), Parse {LocalEnd} to UTC End Time {EndTime} ({EndDayOfWeek})..."
                    : "UTC Day Rollover, Parse {LocalStart} To Next UTC Start Time {StartTime} ({StartDayOfWeek}), Parse {LocalEnd} to UTC End Time {EndTime} ({EndDayOfWeek})...",
                StartTime, _startTime.ToShortTimeString(), _startTime.DayOfWeek, EndTime,
                _endTime.ToShortTimeString(), _endTime.DayOfWeek);
        }

        public Showtime GetShowtime()
        {
            return new Showtime(_startTime, _endTime);
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

            if (_includeApp)
            {
                message = "[{AppName}] -" + message;
                logArgsList.Insert(0, App.Title);
            }

            var logArgs = logArgsList.ToArray();

            if (_isTags)
                Log.ForContext(nameof(Tags), _tags).ForContext("AppName", App.Title)
                    .ForContext(nameof(Priority), _priority).ForContext(nameof(Responders), _responders)
                    .ForContext(nameof(EventCount), EventCount)
                    .Write((Serilog.Events.LogEventLevel) logLevel, message, logArgs);
            else
                Log.ForContext("AppName", App.Title).ForContext(nameof(Priority), _priority)
                    .ForContext(nameof(Responders), _responders).ForContext(nameof(EventCount), EventCount)
                    .Write((Serilog.Events.LogEventLevel) logLevel, message, logArgs);
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

            if (_includeApp)
            {
                message = "[{AppName}] -" + message;
                logArgsList.Insert(0, App.Title);
            }

            var logArgs = logArgsList.ToArray();

            if (_isTags)
                Log.ForContext(nameof(Tags), _tags).ForContext("AppName", App.Title)
                    .ForContext(nameof(Priority), _priority).ForContext(nameof(Responders), _responders)
                    .ForContext(nameof(EventCount), EventCount)
                    .Write((Serilog.Events.LogEventLevel) logLevel, exception, message, logArgs);
            else
                Log.ForContext("AppName", App.Title).ForContext(nameof(Priority), _priority)
                    .ForContext(nameof(Responders), _responders).ForContext(nameof(EventCount), EventCount)
                    .Write((Serilog.Events.LogEventLevel) logLevel, exception, message, logArgs);
        }
    }
}