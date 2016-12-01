using Newtonsoft.Json;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RemoteExecution
{
    public class TypeConverter : JsonConverter
    {
        static string[] SkippedAssemblies = {
            "System.Private.CoreLib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e",
            "System.Private.CoreLib"
        };

        public override bool CanConvert(Type objectType)
        {
            if (objectType == null) return false;
            return typeof(Type).GetTypeInfo().IsAssignableFrom(objectType.GetTypeInfo());
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            Type ret = null;
            if (reader.TokenType == JsonToken.String)
            {
                ret = Type.GetType(reader.Value?.ToString());
            }
            return ret;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var type = (value as Type).GetTypeInfo();
            string typeString = null;
            if (SkippedAssemblies.Contains(type.Assembly.FullName, StringComparer.OrdinalIgnoreCase))
            {
                typeString = type.FullName;
            }
            else
            {
                typeString = type.AssemblyQualifiedName;
            }

            // Even if FullName is used, assembly qualified names can show up in generic parameters.
            // This subsequent scrub of the string removes those.
            // Using this approach, the previous decision to use FullName or AssemblyQualifiedName
            // doesn't actually change anything, but I'm leaving it for the moment since it's the
            // more obvious approach.
            typeString = CleanSerializedString(typeString);

            writer.WriteValue(typeString);
        }

        public static string CleanSerializedString(string serializedString)
        {
            foreach (var assmName in SkippedAssemblies)
            {
                serializedString = serializedString.Replace($", {assmName}", string.Empty);
            }

            return serializedString;
        }
    }
}
