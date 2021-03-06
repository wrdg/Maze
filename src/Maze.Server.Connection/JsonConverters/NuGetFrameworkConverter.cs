using System;
using Newtonsoft.Json;
using NuGet.Frameworks;

namespace Maze.Server.Connection.JsonConverters
{
    public class NuGetFrameworkConverter : JsonConverter<NuGetFramework>
    {
        public override void WriteJson(JsonWriter writer, NuGetFramework value, JsonSerializer serializer)
        {
            if (value == null)
                writer.WriteNull();
            else
                serializer.Serialize(writer, value.ToString());
        }

        public override NuGetFramework ReadJson(JsonReader reader, Type objectType, NuGetFramework existingValue, bool hasExistingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            return NuGetFramework.Parse(serializer.Deserialize<string>(reader));
        }
    }
}