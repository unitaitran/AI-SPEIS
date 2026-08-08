using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ai_speis_be.AI.Json
{
    // A malformed score should not discard an otherwise usable rubric response.
    public sealed class LenientDecimalJsonConverter : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number && reader.TryGetDecimal(out var number)) return number;
            if (reader.TokenType == JsonTokenType.String
                && decimal.TryParse(reader.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var textNumber))
            {
                return textNumber;
            }
            return 0m;
        }

        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) => writer.WriteNumberValue(value);
    }
}
