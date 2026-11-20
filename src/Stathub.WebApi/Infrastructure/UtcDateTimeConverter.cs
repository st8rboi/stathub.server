using System.Text.Json;
using System.Text.Json.Serialization;

namespace Stathub.WebApi.Infrastructure;

/// <summary>
/// Все даты API — UTC. Значение со смещением («+05:00») переводится в UTC, значение без зоны
/// считается уже UTC. Без этого DateTime с Kind=Unspecified доходил до Npgsql, который не пишет
/// его в timestamptz, и запрос падал с 500 — в том числе весь батч событий судьи.
/// Применяется и к <see cref="Nullable{DateTime}"/>.
/// </summary>
internal sealed class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetDateTime();

        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    // Запись как у стандартного сериализатора: формат ответов API не меняется.
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
