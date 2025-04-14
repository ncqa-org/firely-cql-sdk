using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hl7.Cql.Elm
{
    internal class VersionedIdentifierConverter : JsonConverter<VersionedIdentifier>
    {
        public override VersionedIdentifier? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotImplementedException("This converter only supports writing.");
        }
        
        public override void Write(Utf8JsonWriter writer, VersionedIdentifier value, JsonSerializerOptions options)
        {
            writer.WritePropertyName("identifier");
            writer.WriteStartObject();
            writer.WriteString("type", value.GetType().Name);

            var newOptions = new JsonSerializerOptions(options);
            newOptions.Converters.Remove(
                newOptions.Converters.First(c => c is VersionedIdentifierConverter) 
            );

            writer.WriteString("type", value.GetType().Name);
            writer.WriteString("id", value.id);
            writer.WriteString("system", value.system);
            writer.WriteString("version", value.version);

            writer.WriteEndObject();
        }

    }
}
