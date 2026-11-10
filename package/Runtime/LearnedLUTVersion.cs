// SPDX-License-Identifier: MIT
using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GaussianSplatting.Runtime
{
    /// <summary>One explicitly assigned anatomical layer; RGB values are linear light.</summary>
    [Serializable]
    public sealed class LearnedLUTVersion
    {
        public string name;
        public int layer = 7;
        public float[] hu;
        public Color[] linearRGB;

        public static LearnedLUTVersion Parse(string json, string name, int layer)
        {
            if (layer < 0) throw new ArgumentException("Target layer must be nonnegative.");
            if (string.IsNullOrWhiteSpace(json) || json.Length > 1024 * 1024)
                throw new ArgumentException("Expected a JSON file smaller than 1 MiB.");
            JObject root;
            using (var reader = new JsonTextReader(new StringReader(json.TrimStart('\uFEFF'))))
            {
                reader.MaxDepth = 16;
                root = JObject.Load(reader, new JsonLoadSettings
                { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new ArgumentException("Unexpected content after JSON object.");
            }
            if (root["layer"] != null &&
                (root["layer"].Type != JTokenType.Integer || (int)root["layer"] != layer))
                throw new ArgumentException("JSON layer does not match the selected target layer.");
            var huRows = root["hu"] as JArray;
            var rgbRows = root["final_linear_rgb"] as JArray;
            if (huRows == null || rgbRows == null || huRows.Count != 1 || rgbRows.Count != 1)
                throw new ArgumentException("Expected one LUT row in hu and final_linear_rgb. Multi-tissue files must be exported separately.");
            var positions = huRows[0] as JArray;
            var colors = rgbRows[0] as JArray;
            if (positions == null || colors == null || positions.Count < 2 || positions.Count > 1024 || positions.Count != colors.Count)
                throw new ArgumentException("HU and RGB must contain the same number of controls (2–1024).");
            var result = new LearnedLUTVersion
            { name = name, layer = layer, hu = new float[positions.Count], linearRGB = new Color[positions.Count] };
            for (int i = 0; i < positions.Count; i++)
            {
                result.hu[i] = Number(positions[i]);
                if (i > 0 && result.hu[i] <= result.hu[i - 1])
                    throw new ArgumentException("HU control positions must be strictly increasing.");
                var rgb = colors[i] as JArray;
                if (rgb == null || rgb.Count != 3) throw new ArgumentException("Each control must have exactly three RGB values.");
                var color = new Color(Number(rgb[0]), Number(rgb[1]), Number(rgb[2]), 1);
                if (color.r < 0 || color.r > 1 || color.g < 0 || color.g > 1 || color.b < 0 || color.b > 1)
                    throw new ArgumentException("Linear RGB values must be within [0, 1].");
                result.linearRGB[i] = color;
            }
            return result;
        }

        static float Number(JToken value)
        {
            if (value == null || (value.Type != JTokenType.Float && value.Type != JTokenType.Integer))
                throw new ArgumentException("Expected a numeric value.");
            float number = (float)value;
            if (float.IsNaN(number) || float.IsInfinity(number)) throw new ArgumentException("All numbers must be finite.");
            return number;
        }

        public Color Evaluate(float value)
        {
            if (value <= hu[0]) return linearRGB[0];
            int last = hu.Length - 1;
            if (value >= hu[last]) return linearRGB[last];
            int low = 0, high = last;
            while (high - low > 1)
            {
                int middle = (low + high) / 2;
                if (hu[middle] <= value) low = middle; else high = middle;
            }
            return Color.LerpUnclamped(linearRGB[low], linearRGB[high], (value - hu[low]) / (hu[high] - hu[low]));
        }
    }
}
