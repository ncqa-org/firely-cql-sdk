using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hl7.Cql.Elm
{
    internal class DefConverter<T> : JsonConverter<T[]> where T : class
    {
        public override T[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (JsonDocument.TryParseValue(ref reader, out var doc))
            {
                var root = doc.RootElement;
                if (root.TryGetProperty("def", out var defElement))
                {
                    var array = new T[defElement.GetArrayLength()];
                    int i = 0;
                    foreach (var ele in defElement.EnumerateArray())
                    {
                        var json = ele.GetRawText();
                        var t = JsonSerializer.Deserialize<T>(json, options)!;
                        array[i] = t;
                        i++;
                    }
                    return array;
                }
                else return null;
            }
            return null;
        }

        public override void Write(Utf8JsonWriter writer, T[] value, JsonSerializerOptions options)
        {           

            writer.WriteStartObject();
            writer.WritePropertyName("def");
            writer.WriteStartArray();

            foreach (var item in value)
            {
                var element = JsonSerializer.SerializeToElement(item, options);

                writer.WriteStartObject();

                bool hasTypeProperty = element.EnumerateObject()
                                  .Any(p => p.NameEquals("type"));

                if (!hasTypeProperty)
                {
                    writer.WriteString("type", item.GetType().Name);
                }

                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.NameEquals("mediaType"))
                    {
                        continue;
                    }
                    if (prop.NameEquals("expression") && prop.Value.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "Tuple")
                    {
                        writer.WritePropertyName(prop.Name);
                        WriteExpressionWithType(writer, prop.Value);
                        continue;
                    }

                    prop.WriteTo(writer);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        private void WriteExpressionWithType(Utf8JsonWriter writer, JsonElement expressionElement)
        {
            writer.WriteStartArray();
            foreach (var tupleElement in expressionElement.EnumerateArray())
            {
                writer.WriteStartObject();
                foreach (var tupleProp in tupleElement.EnumerateObject())
                {
                    if (tupleProp.NameEquals("type"))
                    {
                        writer.WriteString("type", tupleElement.GetProperty("type").GetString());
                    }
                    else
                    {
                        writer.WritePropertyName(tupleProp.Name);
                        tupleProp.WriteTo(writer);
                    }
                }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
    }
}
