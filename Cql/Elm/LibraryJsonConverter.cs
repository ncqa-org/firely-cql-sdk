#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hl7.Cql.Elm
{
    internal class LibraryJsonConverter : JsonConverter<Library>
    {
        public override Library? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (JsonDocument.TryParseValue(ref reader, out var doc))
            {
                var root = doc.RootElement;
                if (root.TryGetProperty("library", out var libraryElement))
                {
                    var libJson = libraryElement.GetRawText();
                    var converters = options.Converters
                        .Except(options.Converters.OfType<LibraryJsonConverter>())
                        .ToArray();
                    var newOptions = new JsonSerializerOptions
                    {
                        TypeInfoResolver = options.TypeInfoResolver,
                        MaxDepth = options.MaxDepth,
                    };
                    foreach (var converter in converters)
                    {
                        newOptions.Converters.Add(converter);
                    }
                    var lib = JsonSerializer.Deserialize<Library>(libJson, newOptions);
                    return lib;
                }
                else return null;
            }
            else return null;
        }

        public override void Write(Utf8JsonWriter writer, Library library, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("library");

            writer.WriteStartObject();
            writer.WriteString("type", Library.LibraryNodeProperty);

            var converters = options.Converters
                .Where(c => c is not LibraryJsonConverter)
                .ToArray();

            var newOptions = new JsonSerializerOptions
            {
                TypeInfoResolver = options.TypeInfoResolver,
                MaxDepth = options.MaxDepth,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };
            foreach (var converter in converters)
            {
                newOptions.Converters.Add(converter);
            }

            var properties = typeof(Library).GetProperties()
                .Where(p => p.Name != "Name" && p.Name != "Version" && p.Name != "NameAndVersion")
                .ToArray();
            foreach (var property in properties)
            {
                var value = property.GetValue(library);
                if (value is not null)
                {
                    var jsonElement = JsonSerializer.SerializeToElement(value, property.PropertyType, newOptions);
                    writer.WritePropertyName(property.Name);

                    if (jsonElement.ValueKind == JsonValueKind.Object)
                    {
                        writer.WriteStartObject();

                        bool hasType = jsonElement.EnumerateObject().Any(p =>
                            p.NameEquals("type") ||
                            (options.PropertyNamingPolicy?.ConvertName("type") == p.Name));

                        if (!hasType)
                        {
                            if (value is VersionedIdentifier)
                            {
                                writer.WriteString("type", "VersionedIdentifier");
                            }
                            else
                                writer.WriteString("type", $"{Library.LibraryNodeProperty}${property.Name}");
                        }

                        foreach (var prop in jsonElement.EnumerateObject())
                        {
                            prop.WriteTo(writer);
                        }

                        writer.WriteEndObject();
                    }
                    else
                    {
                        jsonElement.WriteTo(writer);
                    }
                }
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }
    }
}
