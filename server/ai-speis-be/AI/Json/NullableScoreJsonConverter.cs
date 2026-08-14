using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ai_speis_be.AI.Json
{
    /// <summary>
    /// Preserves the distinction between a genuine zero and a score the model
    /// omitted or formatted incorrectly.
    /// </summary>
    public sealed class NullableScoreJsonConverter : JsonConverter<decimal?>
    {
        public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonTokenType.Number && reader.TryGetDecimal(out var number))
            {
                return number;
            }

            if (reader.TokenType == JsonTokenType.String
                && decimal.TryParse(reader.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var textNumber))
            {
                return textNumber;
            }

            return null;
        }

        public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                writer.WriteNumberValue(value.Value);
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}
