using System;
using System.IO;
using System.Drawing;
using System.Linq;
using System.Xml.Linq;
using System.Text.Json;
using System.Collections.Generic;

namespace ImAdjustr.Utility {
    internal class FileReader {
        internal FileReader() { }

        internal static Dictionary<string, dynamic> LoadJsonData(string filename) {
            dynamic ConvertJsonValue(JsonElement element) {
                switch (element.ValueKind) {
                    case JsonValueKind.String:
                        return element.GetString();
                    case JsonValueKind.Number:
                        if (element.TryGetInt32(out int intValue)) return intValue;
                        else if (element.TryGetDouble(out double doubleValue)) return (float)doubleValue;
                        throw new InvalidOperationException("Unexpected number type");
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        return element.GetBoolean();
                    case JsonValueKind.Array:
                        var list = new List<dynamic>();
                        foreach (var item in element.EnumerateArray())
                            list.Add(ConvertJsonValue(item));
                        return list;
                    case JsonValueKind.Object:
                        var dictionary = new Dictionary<string, dynamic>();
                        foreach (var property in element.EnumerateObject())
                            dictionary[property.Name] = ConvertJsonValue(property.Value);
                        return dictionary;
                    case JsonValueKind.Null:
                        return null;
                    default:
                        throw new NotSupportedException($"Unsupported JSON value kind: {element.ValueKind}");
                }
            }

            if (string.IsNullOrEmpty(filename)) throw new ArgumentException("Filename cannot be null or empty", nameof(filename));
            if (!File.Exists(filename)) throw new FileNotFoundException("File not found", filename);
            var parsed = new Dictionary<string, dynamic>();
            try {
                string json = File.ReadAllText(filename);
                using (JsonDocument doc = JsonDocument.Parse(json)) {
                    foreach (var property in doc.RootElement.EnumerateObject()) {
                        try {
                            parsed[property.Name] = ConvertJsonValue(property.Value);
                        }
                        catch (Exception) {
                            Console.WriteLine($"Error parsing property {property.Name}");
                        }
                    }
                    return parsed;
                }
            }
            catch (Exception) {
                Console.WriteLine($"Error reading or parsing {filename}");
                return parsed;
            }
        }

        internal static Dictionary<string, dynamic> LoadXMLConfig(string fileName) {
            // Helper method to parse a Color value from a comma seperated value
            Color ParseColor(string colorString) {
                if (string.IsNullOrEmpty(colorString)) return Color.Empty;
                var parts = colorString.Split(',');
                if (parts.Length == 3 &&
                    int.TryParse(parts[0], out int r) &&
                    int.TryParse(parts[1], out int g) &&
                    int.TryParse(parts[2], out int b)) {
                    return Color.FromArgb(r, g, b);
                }
                return Color.Empty;
            }

            Dictionary<string, dynamic> ParseElement(XElement element) {
                var dict = new Dictionary<string, object>();
                foreach (var child in element.Elements()) {
                    string key = child.Attribute("name")?.Value != null ? child.Attribute("name").Value : child.Name.LocalName;
                    dynamic value;
                    if (child.HasElements) {
                        // If the element has children, it's either a nested object or a list
                        var childElements = child.Elements().Select(e => e.Name.LocalName).ToList();
                        var childElementNames = child.Elements().Select(e => e.Attribute("name")?.Value).ToList();
                        if (childElements.Count > 1) {
                            bool isList = childElements.All(name => name == childElements.First()) && 
                                            childElementNames.All(name => name == childElementNames.First());
                            if (isList) {
                                List<dynamic> list = new List<dynamic> { };
                                foreach (var item in child.Elements()) {
                                    var parsedItem = ParseElement(item);
                                    list.Add(parsedItem);
                                }
                                value = list;
                            }
                            else value = ParseElement(child);
                        }
                        // ...otherwise, treat it as a nested object
                        else value = ParseElement(child);
                    }
                    else {
                        // Base value (string, number, bool, color, csv)
                        string valueString = child.Value;
                        if (valueString.StartsWith("#")) value = ColorTranslator.FromHtml(valueString); // Hex color
                        else if (valueString.Contains(".") && float.TryParse(valueString, out float f)) value = f;
                        else if (int.TryParse(valueString, out int i)) value = i;
                        else if (bool.TryParse(valueString, out bool b)) value = b;
                        else if (valueString.Contains(",")) value = ParseColor(valueString); // Color csv
                        else value = valueString;
                    }
                    dict[key] = value;
                }
                return dict;
            }

            var parsed = new Dictionary<string, object>();
            try {
                XDocument doc = XDocument.Load(fileName);
                XElement rootElement = doc.Root;
                if (rootElement != null)
                    parsed = ParseElement(rootElement);
                else {
                    Console.WriteLine($"No root element found in XML file {fileName}.");
                    return null;
                }
            }
            catch (Exception ex) {
                Console.WriteLine($"Error parsing XML configuration: {ex.Message}");
                return parsed;
            }
            return parsed;
        }
    }
}
