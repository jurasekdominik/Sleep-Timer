#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "$0")/.." && pwd)"
check_dir="$(mktemp -d /tmp/sleep-timer-checks.XXXXXX)"
trap 'rm -rf "$check_dir"' EXIT
cat > "$check_dir/Checks.csproj" <<'PROJECT'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>
  <ItemGroup>
    <Compile Include="Program.cs" />
    <Compile Include="$(SleepTimerRoot)/src/Countdown.cs" />
    <Compile Include="$(SleepTimerRoot)/src/TimerDial.cs" />
    <Compile Include="$(SleepTimerRoot)/src/QuickTimers.cs" />
    <Compile Include="$(SleepTimerRoot)/src/WheelInput.cs" />
    <Compile Include="$(SleepTimerRoot)/src/AnimationTiming.cs" />
    <Compile Include="$(SleepTimerRoot)/src/PowerAction.cs" />
  </ItemGroup>
</Project>
PROJECT
cat > "$check_dir/Program.cs" <<'CHECKS'
using System;
using SleepTimer;
using System.Linq;
class Checks
{
    static int checks;
    static void Equal(double actual, double expected, string name)
    {
        if (Math.Abs(actual - expected) > 0.000001) throw new Exception(name + ": " + actual + " != " + expected);
        checks++;
    }
    static void Main()
    {
        foreach (double seconds in new[] { 0d, 1, 60, 1800, 3599, 3600, 3600.1, 3601, 5400, 7200, 9000, 43200, 86400 })
        {
            var dial = TimerDial.For(seconds);
            Equal(dial.Seconds, 3600, "always one hour");
            Equal(dial.Ticks, 60, "always minute ticks");
            Equal((dial.ExtraHours + dial.Fraction(seconds)) * 3600, seconds, "hours and arc reconstruct countdown");
        }
        Equal(TimerDial.For(9000).ExtraHours, 2, "2h30 extra marks");
        Equal(TimerDial.For(9000).Fraction(9000), .5, "2h30 half ring");
        Equal(TimerDial.For(7200).ExtraHours, 1, "exact 2h keeps one additional hour");
        Equal(TimerDial.For(7200).Fraction(7200), 1, "exact 2h full ring");
        Equal(TimerDial.For(7201).ExtraHours, 2, "before hourly rollover");
        Equal(TimerDial.For(7199).ExtraHours, 1, "after hourly rollover");
        Equal(TimerDial.For(7199).Fraction(7199), 3599d / 3600, "rollover refills ring");
        Equal(TimerDial.For(-1).Fraction(-1), 0, "expired arc");
        Equal(TimerDial.For(86400).ExtraHours, 23, "24h represented by 23 marks and full ring");
        double now = 0;
        var timer = new Countdown(7200, () => now);
        timer.Start(timer.Duration);
        now = 1800;
        Equal(TimerDial.For(timer.Remaining).ExtraHours, 1, "running hour marks");
        Equal(TimerDial.For(timer.Remaining).Fraction(timer.Remaining), .5, "running minute position");
        timer.Pause(); now += 60;
        Equal(timer.Remaining, 5400, "paused position");
        timer.Resume(); now += 60;
        Equal(timer.Remaining, 5340, "resumed position");
        timer.Adjust(-86400);
        Equal(timer.Remaining, 10, "safe minimum on scroll");
        var presets = new QuickTimers("180,15,180,60,0,1441,bad,90");
        if (!presets.All.SequenceEqual(new[] {15,30,60,90,120,180,240})) throw new Exception("preset ordering and parsing"); checks++;
        if (!presets.Custom.SequenceEqual(new[] {15,90,180})) throw new Exception("custom deduplication"); checks++;
        if (presets.Add(30) || presets.Add(180) || presets.Add(0) || presets.Add(1441)) throw new Exception("duplicates and bounds"); checks++;
        if (!presets.Add(1440) || !presets.Add(1)) throw new Exception("valid bounds"); checks++;
        var restored = new QuickTimers(presets.Save());
        if (!restored.All.SequenceEqual(presets.All)) throw new Exception("save/reload"); checks++;
        if (!presets.Remove(180) || presets.All.Contains(180) || presets.Remove(30)) throw new Exception("custom removal preserves defaults"); checks++;
        foreach (string invalid in new[] {"", "1.5", "-1", "0", "1441", "9999999999", "hello", "1,5", "1.5h", "1,5h", "1h 30m", "1:30", "90m", "0h", "25h", "+2", "-2h", "9999999999h"})
        { if (QuickTimers.TryMinutes(invalid, out _)) throw new Exception("invalid input accepted"); checks++; }
        foreach (var input in new[] { ("1", 1), ("90", 90), ("1440", 1440), ("1h", 60), ("2h", 120), ("5h", 300), ("24h", 1440), (" 2H ", 120) })
        {
            if (!QuickTimers.TryMinutes(input.Item1, out int minutes)) throw new Exception("valid timer rejected: " + input.Item1);
            Equal(minutes, input.Item2, "whole-minute and whole-hour parsing");
        }
        foreach (var command in new[] { (false, false, "/s /t 0"), (false, true, "/s /f /t 0"),
            (true, false, "/r /t 0"), (true, true, "/r /f /t 0") })
        {
            if (PowerAction.ShutdownArguments(command.Item1, command.Item2) != command.Item3)
                throw new Exception("Power action/force arguments do not match the selected action");
            checks++;
        }
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague");
        var fall = new DateTimeOffset(2026, 10, 25, 1, 30, 0, TimeSpan.FromHours(2));
        Equal((TimeText.NextOccurrence(fall, 210, zone) - fall).TotalHours, 3, "fall clock transition");
        var spring = new DateTimeOffset(2026, 3, 29, 1, 30, 0, TimeSpan.FromHours(1));
        Equal((TimeText.NextOccurrence(spring, 210, zone) - spring).TotalHours, 1, "spring clock transition");
        var gap = TimeText.NextOccurrence(spring, 150, zone);
        Equal(gap.Hour * 60 + gap.Minute, 180, "nonexistent time advances to first valid minute");
        Equal((gap - spring).TotalMinutes, 30, "nonexistent time elapsed duration");
        Equal((TimeText.NextOccurrence(fall, 150, zone) - fall).TotalHours, 1, "first repeated time");
        var repeated = new DateTimeOffset(2026, 10, 25, 2, 45, 0, TimeSpan.FromHours(2));
        Equal((TimeText.NextOccurrence(repeated, 150, zone) - repeated).TotalMinutes, 45, "second repeated time when first has passed");
        var normal = new DateTimeOffset(2026, 10, 15, 23, 30, 0, TimeSpan.FromHours(2));
        Equal((TimeText.NextOccurrence(normal, 30, zone) - normal).TotalHours, 1, "next-day scheduling");
        Equal(TimeText.DefaultClock(spring, 3600, zone), 210, "default target crosses spring clock change");
        Equal(TimeText.DefaultClock(fall, 7200, zone), 150, "default target crosses fall clock change");
        var round = new DateTimeOffset(2026, 10, 15, 12, 0, 45, TimeSpan.Zero);
        Equal(TimeText.DefaultClock(round, 1800, TimeZoneInfo.Utc), 755, "round seconds up to five-minute boundary");
        Equal(TimeText.DefaultClock(round.AddSeconds(-45), 1800, TimeZoneInfo.Utc), 750, "exact five-minute boundary stays unchanged");
        Equal(TimeText.DefaultClock(new DateTimeOffset(2026, 10, 15, 23, 29, 59, TimeSpan.Zero).AddMilliseconds(999), 1800, TimeZoneInfo.Utc), 0, "round across midnight");
        if (QuickTimers.Label(60) != "1 hour" || QuickTimers.Label(120) != "2 hours" || QuickTimers.Label(90) != "1 h 30 min") throw new Exception("duration labels"); checks++;
        now = 0;
        timer = new Countdown(7200, () => now);
        timer.Start(timer.Duration);
        timer.Adjust(-5400);
        var editedDial = TimerDial.For(timer.Remaining);
        Equal(editedDial.ExtraHours, 0, "manual decrease removes hour marks");
        Equal(editedDial.Fraction(timer.Remaining), .5, "manual decrease moves minute arc");
        now += 60;
        Equal(editedDial.Seconds, 3600, "normal countdown keeps hourly dial");
        timer.Adjust(7200);
        editedDial = TimerDial.For(timer.Remaining);
        Equal(editedDial.ExtraHours, 2, "manual increase adds hour marks");
        timer.Pause();
        timer.Adjust(43200);
        Equal(TimerDial.For(timer.Remaining).ExtraHours, 14, "paused editing adds hour marks");
        timer.Reset(); timer.SetDuration(180 * 60); timer.Start(timer.Duration);
        Equal(timer.Remaining, 10800, "quick timer replaces active countdown");
        Equal((int)timer.State, (int)TimerState.Running, "quick timer runs immediately");
        foreach (double left in new[] {0d, 10, 60, 3600})
        {
            Equal((int)TimerStatus.Tone(TimerState.Idle, left), (int)TimerTone.Idle, "idle state overrides warning");
            Equal((int)TimerStatus.Tone(TimerState.Paused, left), (int)TimerTone.Paused, "paused state overrides warning");
        }
        Equal((int)TimerStatus.Tone(TimerState.Running, 61), (int)TimerTone.Running, "normal running state");
        Equal((int)TimerStatus.Tone(TimerState.Running, 60), (int)TimerTone.Warning, "warning boundary");
        Equal((int)TimerStatus.Tone(TimerState.Running, 11), (int)TimerTone.Warning, "warning before urgent");
        Equal((int)TimerStatus.Tone(TimerState.Running, 10), (int)TimerTone.Urgent, "urgent boundary");
        Equal(TimerStatus.Complete(1), 0, "new timer completion");
        Equal(TimerStatus.Complete(.625), 37, "overall completion percentage");
        Equal(TimerStatus.Complete(0), 100, "finished completion");
        Equal(TimerStatus.Complete(1.5), 0, "bounded completion");
        Equal(TimerStatus.Complete(-.5), 100, "bounded expiration completion");
        Equal(TimerDial.For(9000).HourFill, 2.5, "2h30 last hour half faded");
        Equal(TimerDial.For(10800).HourFill, 3, "3h all hour segments full");
        Equal(TimerDial.For(7200).HourFill, 2, "2h two segments full at rollover");
        Equal(TimerDial.For(5400).HourFill, 1.5, "1h30 second hour half faded");
        Equal(TimerDial.For(3600).HourFill, 1, "first hour included");
        Equal(TimerDial.For(1800).HourFill, .5, "first hour half faded");
        Equal(TimerDial.For(86400).HourFill, 24, "24h fills inner circle");
        Equal(TimerDial.For(0).HourFill, 0, "expired hour blocks");
        if (TimerDial.For(7201).HourFill <= TimerDial.For(7200).HourFill || TimerDial.For(7200).HourFill <= TimerDial.For(7199).HourFill)
            throw new Exception("hour fading must decrease continuously across rollover");
        checks++;
        Equal(TimerDial.For(1800.25).Fraction(1800.25), 1800.25 / 3600, "subsecond arc progress");
        if (TimerDial.For(7200.25).HourFill <= TimerDial.For(7200.1).HourFill)
            throw new Exception("hour fading needs continuous subsecond progress");
        checks++;
        Equal(TimerDial.ArcFraction(3600), 1, "exact hour draws full ring");
        Equal(TimerDial.ArcFraction(3630), 30d / 3600, "forward animation wraps through top");
        Equal(TimerDial.ArcFraction(3570), 3570d / 3600, "backward animation wraps through top");
        Equal(TimerDial.ArcFraction(10830), 30d / 3600, "later revolutions wrap identically");
        Equal(TimerDial.ArcFraction(0), 0, "expired animated arc is empty");
        Equal(AnimationTiming.Normalize(600), 600, "default animation duration");
        Equal(AnimationTiming.Normalize(0), 0, "instant animation supported");
        Equal(AnimationTiming.Normalize(-1), 600, "invalid persisted duration falls back");
        Equal(AnimationTiming.Normalize(10001), 600, "oversized persisted duration falls back");
        foreach (string invalid in new[] { "", "-1", "1.5", "10001", "fast", "999999999999" })
        { if (AnimationTiming.TryParse(invalid, out _)) throw new Exception("invalid animation duration accepted"); checks++; }
        foreach (string valid in new[] { "0", "120", "600", "10000" })
        { if (!AnimationTiming.TryParse(valid, out _)) throw new Exception("valid animation duration rejected"); checks++; }
        var stop = new HourStop();
        Equal(stop.Adjust(3300, 900, 0), 300, "coarse wheel snaps to first hour");
        Equal(stop.Adjust(3600, 300, 399), 0, "hour hold lasts 400 ms");
        Equal(stop.Adjust(3600, 300, 400), 300, "hour hold ends at 400 ms");
        stop.Reset();
        Equal(stop.Adjust(3900, -1800, 0), -300, "descending hour snap");
        Equal(stop.Adjust(3600, 60, 1), 60, "reversal bypasses hour hold");
        stop.Reset();
        Equal(stop.Adjust(3300, 86400, 0), 300, "burst stops at first crossed hour");
        Equal(stop.Adjust(3600, 86400, 400), 3600, "continued burst stops at next hour");
        stop.Reset();
        Equal(stop.Adjust(3300, 300, 0), 300, "exact landing starts hold");
        Equal(stop.Adjust(3599.6, 60, 399), 0, "countdown keeps ticking during hold");
        Equal(stop.Adjust(3599.6, 60, 400), 60, "countdown drift does not retrigger same stop");
        stop.Reset();
        Equal(stop.Adjust(3600, -60, 0), -60, "scroll away from initial whole hour");
        Equal(stop.Adjust(60, -300, 500), -300, "zero is not an hour stop");
        var wheel = new WheelInput();
        for (int i = 0; i < 7; i++) Equal(wheel.Add(15, 300), 0, "partial wheel delta");
        Equal(wheel.Add(15, 300), 300, "eight small deltas make one notch");
        Equal(wheel.Add(1200, 300), 3000, "high-resolution burst preserves travel");
        Equal(wheel.Add(int.MaxValue, 3600), Countdown.MaxSet, "positive burst bounded");
        Equal(wheel.Add(int.MinValue, 3600), -Countdown.MaxSet, "negative burst bounded");
        wheel = new WheelInput(); wheel.Add(60, 300);
        Equal(wheel.Add(-120, 300), -300, "direction reversal immediate");
        wheel.Add(60, 300);
        Equal(wheel.Add(60, 60), 0, "modifier change resets partial notch");
        Equal(wheel.Add(60, 60), 60, "fine wheel after modifier change");
        wheel = new WheelInput(); double travel = 0;
        for (int i = 0; i < 100000; i++) travel += wheel.Add(12, 300);
        Equal(travel, 3000000, "sustained precision-wheel stress");
        Console.WriteLine(checks + " checks passed.");
    }
}
CHECKS
dotnet run --project "$check_dir/Checks.csproj" -c Release --property:SleepTimerRoot="$repo_root"
