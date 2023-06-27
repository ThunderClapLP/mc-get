using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MCGet
{
    public static class JsonHelpers
    {
        public static JsonElement? GetOrNull(this JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out JsonElement value))
            {
                return value;
            }
            return null;
        }

        public static bool Exists(this JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out _);
        }
    }
}
