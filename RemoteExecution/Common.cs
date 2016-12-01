using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RemoteExecution
{
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
    }
}
