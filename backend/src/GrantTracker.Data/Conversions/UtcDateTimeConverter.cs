using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GrantTracker.Data.Conversions;

public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc),
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

public sealed class UtcNullableDateTimeConverter() : ValueConverter<DateTime?, DateTime?>(
    value => value.HasValue
        ? value.Value.Kind == DateTimeKind.Local ? value.Value.ToUniversalTime() : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        : value,
    value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : value);

public sealed class DateOnlyConverter() : ValueConverter<DateOnly, string>(
    value => value.ToString("yyyy-MM-dd"),
    value => DateOnly.Parse(value));
