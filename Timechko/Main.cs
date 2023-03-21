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
        var asTimeGuid = TimeGuid.Parse(argument)
            .Map(timeGuid => new TimeGuidBased(timeGuid))
            .Map(result => new DetailedResult(result, Type.TimeGuid));
        var details = kind.Trim() switch
        {
            "DateTime" => asDateTime,
            "UnixTimeSeconds" => ParseLong(argument).Bind(UnixTimeFormat.Seconds.Parse),
            "UnixTimeMilliseconds" => ParseLong(argument).Bind(UnixTimeFormat.Milliseconds.Parse),
            "UnixTimeMicroseconds" => ParseLong(argument).Bind(UnixTimeFormat.Microseconds.Parse),
            "UnixTimeGuess" => ParseLong(argument).Bind(UnixTimeFormat.Formats.Parse),
            "Ticks" => ParseLong(argument).Bind(TicksFormat.Singleton.Parse),
            "TimeGuid" => asTimeGuid,
            "Guess" or _ => asDateTime | asNumeric | asTimeGuid
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
        public override TimeGuid? TimeGuid => Time.TimeGuid.TryCreateMinFromTicks(Ticks);
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
        private static DateTime GregorianCalendarStart => new(1582, 10, 15, 0, 0, 0, DateTimeKind.Utc);

        private readonly Layout layout;

        private TimeGuid(Layout layout)
        {
            this.layout = layout;
        }

        public override string ToString() => layout.ToGuid().ToString("D");
        public long Ticks => layout.Timestamp + GregorianCalendarStart.Ticks;

        public static TimeGuid? TryCreateMinFromTicks(long ticks)
        {
            if (ticks < GregorianCalendarStart.Ticks)
            {
                return null;
            }

            var layout = Layout.Empty;
            if (layout.TrySetTimestamp(ticks - GregorianCalendarStart.Ticks))
            {
                return new TimeGuid(layout);
            }

            return null;
        }

        public static Option<TimeGuid> Parse(string argument)
        {
            if (Guid.TryParse(argument, CultureInfo.InvariantCulture, out var guid))
            {
                var layout = new Layout(guid);
                if (layout.IsTimeGuid)
                {
                    var timeGuid = new TimeGuid(layout);
                    return Option<TimeGuid>.Some(timeGuid);
                }
            }

            return Option<TimeGuid>.None;
        }

        private unsafe struct Layout
        {
            private const int Size = 16;
            private const int FormattedSize = Size * 2;
            private const int BitsInOctet = 8;
            private const long MaxTimestamp = (1L << 60) - 1;
            private const long MinTimestamp = 0;

            public static Layout Empty => new(Guid.Parse("00000000-0000-1000-0000-000000000000"));

            public Layout(Guid guid)
            {
                Span<char> buffer = stackalloc char[FormattedSize];
                guid.TryFormat(buffer, out _, "N");
                for (var i = 0; i < Size; i++)
                {
                    content[i] = byte.Parse(buffer.Slice(2 * i, 2), NumberStyles.HexNumber);
                }
            }

            private fixed byte content[Size];

            public bool IsTimeGuid
            {
                get
                {
                    var versionOctet = content[6];
                    var clearedVersionOctet = versionOctet & 0b1111_0000;
                    return clearedVersionOctet == 0b0001_0000;
                }
            }

            public long Timestamp
            {
                get
                {
                    var value = 0L;
                    var shift = 0;
                    value |= (long)content[3] << ((shift++) * BitsInOctet);
                    value |= (long)content[2] << ((shift++) * BitsInOctet);
                    value |= (long)content[1] << ((shift++) * BitsInOctet);
                    value |= (long)content[0] << ((shift++) * BitsInOctet);

                    value |= (long)content[5] << ((shift++) * BitsInOctet);
                    value |= (long)content[4] << ((shift++) * BitsInOctet);

                    value |= (long)content[7] << ((shift++) * BitsInOctet);
                    value |= (long)(content[6] & 0b0000_1111) << ((shift++) * BitsInOctet);

                    return value;
                }
            }

            public bool TrySetTimestamp(long value)
            {
                if (MinTimestamp <= value && value <= MaxTimestamp)
                {
                    byte Next()
                    {
                        var result = (byte)(value & 0xFF);
                        value >>= 8;
                        return result;
                    }

                    content[3] = Next();
                    content[2] = Next();
                    content[1] = Next();
                    content[0] = Next();

                    content[5] = Next();
                    content[4] = Next();

                    content[7] = Next();
                    content[6] = (byte)(content[6] & 0b1111_0000 | Next());

                    return true;
                }

                return false;
            }

            public readonly Guid ToGuid()
            {
                Span<char> buffer = stackalloc char[FormattedSize];
                for (var i = 0; i < Size; i++)
                {
                    content[i].TryFormat(buffer[(2 * i)..], out _, "x2");
                }

                return Guid.Parse(buffer, CultureInfo.InvariantCulture);
            }
        }
    }

    private readonly struct DetailedResult
    {
        private readonly Result result;
        public Type Type { get; }

        public DetailedResult(Result result, Type type)
        {
            this.result = result;
            Type = type;
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