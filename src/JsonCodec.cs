using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ConverterAqui {
    // Retorna os mesmos dicionários/listas usados pela interface, sem System.Web.
    static class JsonCodec {
        public static string Serialize(object value) { return JsonSerializer.Serialize(value); }
        public static T Deserialize<T>(string value) {
            using (JsonDocument document = JsonDocument.Parse(value, new JsonDocumentOptions { MaxDepth = 128 })) {
                return (T)ConvertElement(document.RootElement);
            }
        }
        static object ConvertElement(JsonElement value) {
            switch (value.ValueKind) {
                case JsonValueKind.Object:
                    var map = new Dictionary<string, object>();
                    foreach (var property in value.EnumerateObject()) map[property.Name] = ConvertElement(property.Value);
                    return map;
                case JsonValueKind.Array:
                    var list = new List<object>();
                    foreach (var item in value.EnumerateArray()) list.Add(ConvertElement(item));
                    return list.ToArray();
                case JsonValueKind.String: return value.GetString();
                case JsonValueKind.Number: return value.TryGetInt64(out long number) ? (object)number : value.GetDouble();
                case JsonValueKind.True: return true;
                case JsonValueKind.False: return false;
                default: return null;
            }
        }
    }
}
