using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.JavaScript;

return;

public static partial class Time
{
    private static IEnumerable<IDateTimeFormat> NumericFormats { get; } =
        UnixTimeFormat.Formats.Append<IDateTimeFormat>(TicksFormat.Singleton);

    private static (long Min, long Max) TicksBounds => (0, DateTime.MaxValue.Ticks);

    private static (long Min, long Max) UnixTicksBounds => (
        Min: TicksBounds.Min - DateTime.UnixEpoch.Ticks,
        Max: TicksBounds.Max - DateTime.UnixEpoch.Ticks
    );

    [JSExport]
    internal static string Now() => Print(
        new DateTime(
            DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond,
            DateTimeKind.Utc
        )
    );

    [JSExport]
    internal static string Parse(string argument, string kind)
    {
        var asDateTime = ParseDateTime(argument)
            .Map(dateTime => new TicksBased(dateTime.Ticks))
            .Map(result => new DetailedResult(result, Type.DateTime));
        var asNumeric = ParseLong(argument).Bind(NumericFormats.Parse);
        var details = kind.Trim() switch
        {
            "DateTime" => asDateTime,
            "UnixTimeSeconds" => ParseLong(argument).Bind(UnixTimeFormat.Seconds.Parse),
            "UnixTimeMilliseconds" => ParseLong(argument).Bind(UnixTimeFormat.Milliseconds.Parse),
            "UnixTimeMicroseconds" => ParseLong(argument).Bind(UnixTimeFormat.Microseconds.Parse),
            "UnixTimeGuess" => ParseLong(argument).Bind(UnixTimeFormat.Formats.Parse),
            "Ticks" => ParseLong(argument).Bind(TicksFormat.Singleton.Parse),
            "TimeGuid" => Option<DetailedResult>.None, // todo
            "Guess" or _ => asDateTime | asNumeric
        };

        return details
            .Map(result => result.ToJson())
            .Or("null");
    }

    private static Option<DetailedResult> Parse(this IDateTimeFormat formats, long parts) =>
        Parse(new[] { formats }, parts);

    private static Option<DetailedResult> Parse(this IEnumerable<IDateTimeFormat> formats, long parts)
    {
        var heuristic = new DateTime(2023, 01, 01, 00, 00, 00, DateTimeKind.Utc);
        var parsed = formats
            .Select(format => (Parsed: format.ToDateTime(parts), format.Type))
            .Where(pair => pair.Parsed.HasValue)
            .Select(pair => (pair.Parsed.Value, pair.Type))
            .OrderBy(pair => Math.Abs((pair.Value - heuristic).Ticks))
            .Select(pair => new DetailedResult(new TicksBased(pair.Value.Ticks), pair.Type))
            .Take(1)
            .ToArray();

        return parsed.Length > 0
            ? Option<DetailedResult>.Some(parsed[0])
            : Option<DetailedResult>.None;
    }
    
    private static Option<DateTime> ParseDateTime(string argument)
    {
        var success = DateTime.TryParse(
            argument,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
            out var result
        );
        return success
            ? Option<DateTime>.Some(result)
            : Option<DateTime>.None;
    }

    private static Option<long> ParseLong(string argument)
    {
        var success = long.TryParse(
            argument,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var result
        );
        return success
            ? Option<long>.Some(result)
            : Option<long>.None;
    }

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
            type,
            Print(dateTime),
            unixTime.ToString(CultureInfo.InvariantCulture),
            ticks.ToString(CultureInfo.InvariantCulture),
            "93E9A3DC-E331-4CC1-BAA6-5A012F9D06C8" // todo
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

    private static bool Between<T>(this T value, (T Min, T Max) bounds) where T : IComparisonOperators<T, T, bool> =>
        bounds.Min <= value && value <= bounds.Max;

    private interface IDateTimeFormat
    {
        Type Type { get; }
        Option<DateTime> ToDateTime(long parts);
    }

    private sealed class TicksFormat : IDateTimeFormat
    {
        public static TicksFormat Singleton { get; } = new();

        public Type Type => Type.Ticks;

        public Option<DateTime> ToDateTime(long ticks) => ticks.Between(TicksBounds)
            ? Option<DateTime>.Some(new DateTime(ticks, DateTimeKind.Utc))
            : Option<DateTime>.None;
    }

    private sealed class UnixTimeFormat : IDateTimeFormat
    {
        public Type Type { get; }
        private readonly TimeSpan fraction;

        private UnixTimeFormat(TimeSpan fraction, Type type)
        {
            Type = type;
            this.fraction = fraction;
        }

        private (long Min, long Max) Bounds => (
            Min: UnixTicksBounds.Min / fraction.Ticks,
            Max: UnixTicksBounds.Max / fraction.Ticks
        );

        public static UnixTimeFormat Seconds => new(
            new TimeSpan(TimeSpan.TicksPerSecond),
            Type.UnixTimeSeconds
        );

        public static UnixTimeFormat Milliseconds => new(
            new TimeSpan(TimeSpan.TicksPerMillisecond),
            Type.UnixTimeMilliseconds
        );

        public static UnixTimeFormat Microseconds => new(
            new TimeSpan(TimeSpan.TicksPerMicrosecond),
            Type.UnixTimeMicroseconds
        );

        public static IReadOnlyCollection<UnixTimeFormat> Formats { get; } = new[]
        {
            Seconds,
            Milliseconds,
            Microseconds
        };

        public Option<DateTime> ToDateTime(long parts)
        {
            if (parts.Between(Bounds))
            {
                return Option<DateTime>.Some(
                    DateTime.UnixEpoch + parts * fraction
                );
            }

            return Option<DateTime>.None;
        }
    }

    private readonly struct Option<T>
    {
        public bool HasValue { get; }
        private readonly T value;

        private Option(T value, bool hasValue)
        {
            this.value = value;
            HasValue = hasValue;
        }

        public T Value => HasValue
            ? value
            : throw new Exception();

        public Option<TNew> Map<TNew>(Func<T, TNew> map) => HasValue
            ? Option<TNew>.Some(map(value))
            : Option<TNew>.None;

        public Option<TNew> Bind<TNew>(Func<T, Option<TNew>> map) => HasValue
            ? map(value)
            : Option<TNew>.None;

        public Option<T> Filter(Func<T, bool> filter) => HasValue && filter(value)
            ? this
            : None;

        public T Or(T fallback) => HasValue
            ? value
            : fallback;

        public static Option<T> Some(T value) => new(value, true);

        public static Option<T> None => new(
#pragma warning disable CS8604
            default,
#pragma warning restore CS8604
            false
        );

        public static Option<T> operator |(Option<T> a, Option<T> b) => a.HasValue
            ? a
            : b;
    }

    // todo
    private enum TypeIn
    {
        Guess,
        UnixTimeGuess,
        UnixTimeSeconds,
        UnixTimeMilliseconds,
        UnixTimeMicroseconds,
        Ticks,
        DateTime,
        TimeGuid
    }

    private enum Type
    {
        UnixTimeSeconds,
        UnixTimeMilliseconds,
        UnixTimeMicroseconds,
        Ticks,
        DateTime,
        TimeGuid
    }

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

        public override string ToString() => value.ToString("D");

        public long Ticks => throw new NotImplementedException();

        public static TimeGuid? TryCreateMinFromLong(long ticks)
        {
            return null; // todo
        }
    }

    private readonly struct DetailedResult
    {
        private readonly Result result;
        public Type Type { get; }

        public DetailedResult(Result result, Type type)
        {
            this.result = result;
            this.Type = type;
        }

        public long Ticks => result.Ticks;
        private long UnixTicks => Ticks - DateTime.UnixEpoch.Ticks;
        public long UnixTimeSeconds => UnixTicks / TimeSpan.TicksPerSecond;
        public long UnixTimeMilliseconds => UnixTicks / TimeSpan.TicksPerMillisecond;
        public long UnixTimeMicroseconds => UnixTicks / TimeSpan.TicksPerMicrosecond;
        public DateTime DateTime => new(Ticks, DateTimeKind.Utc);
        public TimeGuid? TimeGuid => result.TimeGuid;

        public string ToJson() => $$"""
        {
            "type":"{{Type:G}}",
            "dateTime":"{{Print(DateTime)}}",
            "unixTimeSeconds":"{{UnixTimeSeconds.ToString(CultureInfo.InvariantCulture)}}",
            "unixTimeMilliseconds":"{{UnixTimeMilliseconds.ToString(CultureInfo.InvariantCulture)}}",
            "unixTimeMicroseconds":"{{UnixTimeMicroseconds.ToString(CultureInfo.InvariantCulture)}}",
            "ticks":"{{Ticks.ToString(CultureInfo.InvariantCulture)}}",
            "timeGuid":"{{TimeGuid}}"
        }
        """;
    }
}