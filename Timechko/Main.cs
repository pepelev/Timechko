using System;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices.JavaScript;

return;

public static partial class Time
{
    private static (long Min, long Max) TicksBounds => (0, DateTime.MaxValue.Ticks);

    private static (long Min, long Max) UnixTicksBounds => (
        Min: (DateTime.UnixEpoch.Ticks - TicksBounds.Min),
        Max: (TicksBounds.Min - DateTime.UnixEpoch.Ticks)
    );

    private static (long Min, long Max) UnixTimeBounds => (
        Min: UnixTicksBounds.Min / TimeSpan.TicksPerSecond,
        Max: UnixTicksBounds.Max / TimeSpan.TicksPerSecond
    );

    private static (long Min, long Max) UnixTimeMillisecondsBounds => (
        Min: UnixTicksBounds.Min / TimeSpan.TicksPerMillisecond,
        Max: UnixTicksBounds.Max / TimeSpan.TicksPerMillisecond
    );

    private static (long Min, long Max) UnixTimeMicrosecondsBounds => (
        Min: UnixTicksBounds.Min / TimeSpan.TicksPerMicrosecond,
        Max: UnixTicksBounds.Max / TimeSpan.TicksPerMicrosecond
    );

    private abstract class Result
    {
        public abstract long Ticks { get; }
        public abstract TimeGuid? TimeGuid { get; }
    }

    private sealed class TicksBased : Result
    {
        public TicksBased(long ticks)
        {
            Ticks = ticks;
        }

        public override long Ticks { get; }
        public override TimeGuid? TimeGuid => Time.TimeGuid.TryCreateMinFromLong(Ticks);
    }

    private sealed class TimeGuidBased : Result
    {
        private readonly TimeGuid source;

        public TimeGuidBased(TimeGuid source)
        {
            this.source = source;
        }

        public override long Ticks => source.Ticks;
        public override TimeGuid? TimeGuid => source;
    }

    private readonly struct TimeGuid
    {
        private readonly Guid value;

        public TimeGuid(Guid value)
        {
            this.value = value;
        }

        public long Ticks => throw new NotImplementedException();

        public static TimeGuid? TryCreateMinFromLong(long ticks)
        {
            
        }
    }

    private readonly struct DetailedResult
    {
        private readonly Result result;

        public DetailedResult(Result result)
        {
            this.result = result;
        }

        public long Ticks => result.Ticks;
        private long UnixTicks => (Ticks - DateTime.UnixEpoch.Ticks);
        public long UnixTime => UnixTicks / TimeSpan.TicksPerSecond;
        public long UnixTimeMilliseconds => UnixTicks / TimeSpan.TicksPerMillisecond;
        public long UnixTimeMicroseconds => UnixTicks / TimeSpan.TicksPerMicrosecond;
        public DateTime DateTime => new(Ticks, DateTimeKind.Utc);
        public Guid? TimeGuid => result.TimeGuid;
    }

    [JSExport]
    internal static string Now() => Print(
        new DateTime(
            DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond,
            DateTimeKind.Utc
        )
    );

    [JSExport]
    internal static string Parse(string argument, string kind) => kind switch
    {
        "DateTime" => DateTimeToTicks(argument),
        "UnixTime" => UnixTimeToTicks(argument),
        "Ticks" => TicksToTicks(argument),
        "Guess" or _ => GuessToTicks(argument)
    };

    private static string DateTimeToTicks(string argument)
    {
        var success = DateTime.TryParse(
            argument,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
            out var result
        );
        return success
            ? ToJson("DateTime", result.Ticks)
            : UnknownToJson();
    }

    private static string UnixTimeToTicks(string argument)
    {
        var success = long.TryParse(
            argument,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var result
        );
        return success
            ? ToJson("UnixTime", (DateTime.UnixEpoch + new TimeSpan(result * TimeSpan.TicksPerSecond)).Ticks)
            : UnknownToJson();
    }

    private static string TicksToTicks(string argument)
    {
        var success = long.TryParse(
            argument,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var result
        );
        return success && result >= 0
            ? ToJson("Ticks", result)
            : UnknownToJson();
    }

    private static string GuessToTicks(string argument)
    {
        var success = long.TryParse(
            argument,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var result
        );
        if (!success)
        {
            return DateTimeToTicks(argument);
        }

        var minUnixTime = (DateTime.MinValue - DateTime.UnixEpoch).Ticks / TimeSpan.TicksPerSecond;
        var maxUnixTime = (DateTime.MaxValue - DateTime.UnixEpoch).Ticks / TimeSpan.TicksPerSecond;
        if (minUnixTime <= result && result <= maxUnixTime)
        {
            return UnixTimeToTicks(argument);
        }

        return TicksToTicks(argument);
    }

    private static string ToJson(string type, long ticks)
    {
        var dateTime = new DateTime(ticks, DateTimeKind.Utc);
        var unixTime = (dateTime - DateTime.UnixEpoch).Ticks / TimeSpan.TicksPerSecond;
        return ToJson(
            type: type,
            dateTime: Print(dateTime),
            unixTime: unixTime.ToString(CultureInfo.InvariantCulture),
            ticks: ticks.ToString(CultureInfo.InvariantCulture),
            timeGuid: "93E9A3DC-E331-4CC1-BAA6-5A012F9D06C8" // todo
        );
    }

    private static string UnknownToJson() => "null";

    private static string ToJson(string type, string dateTime, string unixTime, string ticks, string timeGuid) =>
        @$"{{""type"":""{type}"",""dateTime"":""{dateTime}"",""unixTime"":""{unixTime}"",""ticks"":""{ticks}"",""timeGuid"":""{timeGuid}}}";

    private static string Print(DateTime value)
    {
        var result = value.ToString("O", CultureInfo.InvariantCulture);
        if (result.EndsWith("T00:00:00.0000000Z"))
        {
            return result.Replace("T00:00:00.0000000Z", "Z");
        }
        if (result.EndsWith(".0000000Z"))
        {
            return result.Replace(".0000000Z", "Z");
        }

        if (result.EndsWith("0000Z"))
        {
            return result.Replace("0000Z", "Z");
        }

        return result;
    }
}