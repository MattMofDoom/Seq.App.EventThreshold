# Seq.Apps.EventThreshold - Event Threshold for Seq

[![Version](https://img.shields.io/nuget/v/Seq.App.EventThreshold?style=plastic)](https://www.nuget.org/packages/Seq.App.EventThreshold)
[![Downloads](https://img.shields.io/nuget/dt/Seq.App.EventThreshold?style=plastic)](https://www.nuget.org/packages/Seq.App.EventThreshold)
[![License](https://img.shields.io/github/license/MattMofDoom/Seq.App.EventThreshold?style=plastic)](https://github.com/MattMofDoom/Seq.App.EventThreshold/blob/dev/LICENSE)

This app provides an event threshold function for [Seq](https://datalust.co/seq). It will read an input stream and count matching text strings on configured properties, during a configured start and end time. If the event count is Less Than or Equal To (or optionally, Greater Than or Equal To) the threshold, an alert will be raised.

It includes a threshold and suppression interval, which allows you to fine tune the way the threshold works, such as monitoring over 10 seconds or 4 hours for the event count to measure against the threshold, and then not alerting again for 1 hour.

When a threshold violation occurs, it will output the configured text and description back to the stream, which can be used as the basis for a signal 

This is a highly configurable and powerful Seq app. Consider some of the possible usages:

* Monitor a signal for @Message containing "started" between 1am and 3am, and alert if the volume is less than 100 over 30 minutes
* Monitor a signal for @Message containing ANY value and ServerName containing "MYSERVER" between 12am and 12am (24 hour period), and alert if the volume is less than 100 over 30 minutes
* Monitor a signal for ServerName containing "MYSERVER" and Status containing "Succeeded" between 2am and 3am, and alert if the volume is greater than 100 over 10 minutes
* Monitor a signal for @Message containing "started" and ServerName containing "MYSERVER" and JobName containing "Backup" and Component containing "SQL" between 12am and 6am, and alert if the volume is greater than 100 over 1 hour
* Monitor a signal for @Message containing ANY value between 12am and 1am on Monday-Friday, and alert if the volume is greater than 50 over 20 minutes
* Monitor a signal for @Message containing ANY value between 1am and 2am on Monday-Friday excluding public holidays, and alert if the volume is less than 20 over 10 minutes
* Monitor a signal for @Message containing ANY value between 2am and 3am on the first day of the month and alert if the volume is greater than 10 over 20 minutes
* Monitor a signal for @Message containing "stopped" between 3am and 4am on the fourth Friday of the month and alert if the volume is less than 40 over 5 minutes
* Monitor a signal for Status containing "failed" between 4am and 5am on the last weekday of the month and alert if the volume is greater than 90 over 45 minutes
* Monitor a signal for @Message containing ANY value between 5am and 8am on the first day, first weekday, second monday, fifth friday, last weekday, and last day of the month, excluding public holidays, and alert if the volume is less than 1000 over 30 minutes
* Monitor a signal for @Message containing "success" between 8am and 12pm on Monday-Friday, excluding the third monday and last weekday, excluding public holidays, and alert if the volume is greater than 1000 over 2 hours

There are many possible ways to configure Event Threshold! 

Date/time is converted to UTC time internally, so that the start and end times are always handled correctly when considering local timezone and daylight savings. 

Event Threshold includes the optional ability to retrieve public holidays using [AbstractApi's Public Holidays API](https://www.abstractapi.com/holidays-api) which can retrieve your local and national public holidays. 

* You can configure Event Threshold to look for holiday types and locales, so that only (for example) National and Local holidays in Australia or New South Wales will be effective. 
* Events with "Bank Holiday" in the name are excluded by default, but can be enabled
* Weekends are excluded by default, but can be enabled
* Retrieval of holidays occurs once per instance per day, at 12am (local time). If an event monitoring period ("Showtime") is in progress, it will only occur after an event monitoring period has ended. If one is scheduled, it will be delayed until holidays are retrieved.
* The Holidays API free tier limits requests to one per second, so a 10 second retry is configured for up to 10 attempts per instance
* This allows even the free Holidays API pricing tier to be used for most cases. 
* Proxy configuration is included for Seq instances that do not have outbound internet access

Event Threshold shares many common features with [Event Timeout for Seq](https://github.com/MattMofDoom/Seq.App.EventTimeout). You can check my [Blog of Doom](https://MattMofDoom.com) for the latest information!
