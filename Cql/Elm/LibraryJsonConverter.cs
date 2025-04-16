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
            // Write the library object
            writer.WriteStartObject();
            writer.WritePropertyName("library");

            // Add the type property
            writer.WriteStartObject();
            writer.WriteString("type", Library.LibraryNodeProperty);


            var newOptions = new JsonSerializerOptions()
            {
                TypeInfoResolver = options.TypeInfoResolver,
                MaxDepth = options.MaxDepth,
                PropertyNamingPolicy = options.PropertyNamingPolicy,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };

            //newOptions.Converters.Remove(this);

            var converters = options.Converters
                .Where(c => c is not LibraryJsonConverter)
                .ToArray();

            foreach (var converter in converters)
            {
                newOptions.Converters.Add(converter);
            }

            var properties = typeof(Library).GetProperties();

            foreach (var property in properties)
            {         
                if (property.Name == "Name" || property.Name == "Version" || property.Name == "NameAndVersion")
                    continue;

                var propertyValue = property.GetValue(library);
                var propertyName = char.ToUpper(property.Name[0]) + property.Name.Substring(1);

                if (propertyValue is not null)
                {
                    var jsonElement = JsonSerializer.SerializeToElement(propertyValue, property.PropertyType, newOptions);
                    writer.WritePropertyName(property.Name);

                    if (jsonElement.ValueKind == JsonValueKind.Object)
                    {
                        writer.WriteStartObject();

                        bool hasType = jsonElement.EnumerateObject().Any(p =>
                            p.NameEquals("type") ||
                            (options.PropertyNamingPolicy?.ConvertName("type") == p.Name));


                        if (!hasType)
                        {
                            if (propertyValue is VersionedIdentifier)
                            {
                                writer.WriteString("type", "VersionedIdentifier");
                            }
                            else
                                writer.WriteString("type", $"{Library.LibraryNodeProperty}${propertyName}");
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
