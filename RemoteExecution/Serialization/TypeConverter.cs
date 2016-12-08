using Newtonsoft.Json;
using System;
using System.Reflection;
using System.Linq;

namespace RemoteExecution
{
    /// <summary>
    /// Type converter to allow limited NetCore <->NetFX interoperability
    /// There are still plenty of cross-platform scenarios that don't work, but
    /// by stripping out obviously different base/core assembly names from type 
    /// strings, some simple cases can be made to work.
    /// </summary>
    public class TypeConverter : JsonConverter
    {
        // .NET Core-specific core assemblies which don't have NetFX facades
        static string[] SkippedAssemblies = {
            "System.Private.CoreLib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e",
            "System.Private.CoreLib"
        };

        // Specify which types (System.Type) this type converter works on
        public override bool CanConvert(Type objectType)
        {
            if (objectType == null) return false;
            return typeof(Type).GetTypeInfo().IsAssignableFrom(objectType.GetTypeInfo());
        }
        
        // Retrieves a type from its string representation
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            Type ret = null;
            if (reader.TokenType == JsonToken.String)
            {
                ret = Type.GetType(reader.Value?.ToString());
            }
            return ret;
        }

        // Writes a type as a string
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var type = (value as Type).GetTypeInfo();
            string typeString = null;

            // Assume that types in System.Private.CoreLib can be found without their assembly name qualifier (since they are base types)
            // Don't include the assembly name since it changes from Framework-to-Framework
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

        // Helper method to strip skipped assembly names from general string representations of types
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
