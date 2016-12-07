using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace RemoteExecution
{
    /// <summary>
    /// A helper class for serializing and deserializing types to/from JSON
    /// for transport over named pipes
    /// </summary>
    internal static class SerializationHelper
    {
        private static bool initialized = false;
        
        private static JsonSerializerSettings GetJsonSettings() =>
            new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Objects,
                NullValueHandling = NullValueHandling.Ignore,
                ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
                PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                Converters = new List<JsonConverter>() {
                    // Use a custom type converter to massage type names in order
                    // to allow limited cross-platform (NetFX <-> NetCore) interoperability
                    new TypeConverter()
                }
            };

        public static string Serialize(object o)
        {
            if (!initialized)
            {
                JsonConvert.DefaultSettings = SerializationHelper.GetJsonSettings;
                initialized = true;
            }

            var json = JsonConvert.SerializeObject(o);
            return TypeConverter.CleanSerializedString(json);
        }

        public static object Deserialize(string s)
        {
            if (!initialized)
            {
                JsonConvert.DefaultSettings = SerializationHelper.GetJsonSettings;
                initialized = true;
            }

            try
            {
                return JsonConvert.DeserializeObject(s);
            }
            catch (JsonException exc)
            {
                throw new ArgumentException("Invalid json string", exc);
            }
        }

        public static T Deserialize<T>(string s)
        {
            if (!initialized)
            {
                JsonConvert.DefaultSettings = SerializationHelper.GetJsonSettings;
                initialized = true;
            }

            try
            {
                return JsonConvert.DeserializeObject<T>(s);
            }
            catch (JsonException exc)
            {
                throw new ArgumentException("Invalid json string", exc);
            }
        }

        // A wrapper around Convert.ChangeType to handle more cases known to be interesting
        // with Json.Net serialization.
        // http://www.newtonsoft.com/json/help/html/SerializationGuide.htm
        internal static object GetObjectAsType(object obj, Type type)
        {
            if (obj == null) return null;

            if (type == null) return obj;

            if (obj is JArray)
            {
                return ((JArray)obj).ToObject(type);
            }

            if (obj is JObject)
            {
                return ((JObject)obj).ToObject(type);
            }

            if (obj is string && type == typeof(Guid))
            {
                return new Guid(obj as string);
            }

            if (obj is string && type == typeof(byte[]))
            {
                return Convert.FromBase64String(obj as string);
            }

            if (obj is string && typeof(DateTime).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
            {
                return DateTime.ParseExact(obj as string, "YYYY-MM-DDTHH:mmZ", null);
            }

            if (obj is string && typeof(Type).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
            {
                return Type.GetType(obj as string);
            }


            return Convert.ChangeType(obj, type);
        }
    }
}
