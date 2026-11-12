// SPDX-License-Identifier: MIT
using System.Linq;
using GaussianSplatting.Runtime;
using UnityEditor;
using UnityEngine;

namespace GaussianSplatting.Editor
{
    internal static class AppearanceControls
    {
        static bool s_Advanced;
        static readonly string[] Modes = { "Original Gaussian / 原始高斯颜色", "Manual / 手工配色", "Trained / 训练外观" };

        public static void Draw(Object[] targets)
        {
            var renderer = targets[0] as GaussianSplatRenderer;
            if (renderer == null) return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Appearance / 外观模式", EditorStyles.boldLabel);
            EditorGUI.showMixedValue = targets.OfType<GaussianSplatRenderer>().Any(r => r.CurrentAppearanceMode != renderer.CurrentAppearanceMode);
            EditorGUI.BeginChangeCheck();
            int mode = EditorGUILayout.Popup("Display mode", (int)renderer.CurrentAppearanceMode, Modes);
            if (EditorGUI.EndChangeCheck())
                foreach (var item in targets.OfType<GaussianSplatRenderer>())
                {
                    Undo.RecordObject(item, "Switch Appearance Mode");
                    item.SetAppearanceMode((GaussianSplatRenderer.AppearanceMode)mode);
                    EditorUtility.SetDirty(item);
                }
            EditorGUI.showMixedValue = false;
            if (targets.Length > 1)
            {
                EditorGUILayout.HelpBox("Select one renderer to edit its palette or trained versions.", MessageType.Info);
                return;
            }
            switch (renderer.CurrentAppearanceMode)
            {
                case GaussianSplatRenderer.AppearanceMode.OriginalGaussian:
                    EditorGUILayout.HelpBox("Uses the model's stored Gaussian color and view-dependent SH. Manual colors and LUTs are bypassed.", MessageType.Info);
                    break;
                case GaussianSplatRenderer.AppearanceMode.Manual:
                    DrawManual(renderer);
                    break;
                case GaussianSplatRenderer.AppearanceMode.Trained:
                    LearnedLUTControls.Draw(renderer);
                    EditorGUILayout.HelpBox("Supported: learned LUT JSON. Independent Gaussian RGB import is not connected yet. Unselected layers use their original Gaussian colors.", MessageType.Info);
                    break;
            }
        }

        static void DrawManual(GaussianSplatRenderer renderer)
        {
            if (!renderer.HasValidAsset) return;
            int count = renderer.asset.layerInfo.Keys.Max() + 1;
            renderer.EnsureLayerAppearanceCount(count);
            renderer.EnsureHUTransferSettingsCount(count);
            foreach (int layer in renderer.asset.layerInfo.Keys.OrderBy(id => id))
            {
                var appearance = renderer.m_LayerAppearances[layer];
                EditorGUI.BeginChangeCheck();
                appearance.color = EditorGUILayout.ColorField($"L{layer} Base Color", appearance.color);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(renderer, "Change Manual Layer Color");
                    renderer.m_LayerAppearances[layer] = appearance;
                    renderer.UploadLayerAppearanceData();
                    EditorUtility.SetDirty(renderer);
                }
            }
            s_Advanced = EditorGUILayout.Foldout(s_Advanced, "Advanced manual settings / 手工微调", true);
            if (!s_Advanced) return;
            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();
            bool preserve = EditorGUILayout.Toggle("Preserve SH luminance", renderer.m_HUPreserveLighting);
            bool enabled = EditorGUILayout.Toggle("Enable HU mapping", renderer.m_EnableHUMapping);
            float mix = renderer.m_HUColorStrength, minimum = renderer.m_HULUTMin, maximum = renderer.m_HULUTMax;
            using (new EditorGUI.DisabledScope(!enabled || !renderer.asset.hasHU))
            {
                mix = EditorGUILayout.Slider("Base / HU mix", mix, 0, 1);
                minimum = EditorGUILayout.FloatField("LUT HU min", minimum);
                maximum = EditorGUILayout.FloatField("LUT HU max", maximum);
            }
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(renderer, "Change Manual HU Mapping");
                renderer.m_HUPreserveLighting = preserve;
                renderer.m_EnableHUMapping = enabled;
                renderer.m_HUColorStrength = mix;
                renderer.m_HULUTMin = minimum;
                renderer.m_HULUTMax = maximum;
                renderer.UploadHULUT();
                EditorUtility.SetDirty(renderer);
            }
            if (!renderer.asset.hasHU) EditorGUILayout.HelpBox("Asset has no raw HU. Base color is used.", MessageType.Warning);
            if (maximum <= minimum) EditorGUILayout.HelpBox("HU max must exceed HU min.", MessageType.Warning);
            foreach (int layer in renderer.asset.layerInfo.Keys.OrderBy(id => id))
            {
                EditorGUILayout.LabelField($"L{layer}", EditorStyles.boldLabel);
                var appearance = renderer.m_LayerAppearances[layer];
                EditorGUI.BeginChangeCheck();
                appearance.brightness = EditorGUILayout.Slider("Brightness", appearance.brightness, 0, 3);
                appearance.opacity = EditorGUILayout.Slider("Layer opacity (all modes)", appearance.opacity, 0, 2);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(renderer, "Change Layer Adjustment");
                    renderer.m_LayerAppearances[layer] = appearance;
                    renderer.UploadLayerAppearanceData();
                    EditorUtility.SetDirty(renderer);
                }
                using (new EditorGUI.DisabledScope(!enabled || !renderer.asset.hasHU))
                {
                    var settings = renderer.m_HUTransferSettings[layer];
                    EditorGUI.BeginChangeCheck();
                    settings.lowHU = EditorGUILayout.FloatField("Low HU", settings.lowHU);
                    settings.centerHU = EditorGUILayout.FloatField("Center HU", settings.centerHU);
                    settings.highHU = EditorGUILayout.FloatField("High HU", settings.highHU);
                    settings.lowColor = EditorGUILayout.ColorField("Low color", settings.lowColor);
                    settings.centerColor = EditorGUILayout.ColorField("Center color", settings.centerColor);
                    settings.highColor = EditorGUILayout.ColorField("High color", settings.highColor);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(renderer, "Change Manual HU Controls");
                        renderer.m_HUTransferSettings[layer] = settings;
                        renderer.UploadHULUT();
                        EditorUtility.SetDirty(renderer);
                    }
                    if (!(settings.lowHU < settings.centerHU && settings.centerHU < settings.highHU))
                        EditorGUILayout.HelpBox("Expected Low HU < Center HU < High HU.", MessageType.Warning);
                }
            }
            if (GUILayout.Button("Reset manual HU defaults"))
            {
                Undo.RecordObject(renderer, "Reset Manual HU");
                renderer.ResetHUTransferSettings(count);
                EditorUtility.SetDirty(renderer);
            }
            EditorGUI.indentLevel--;
        }
    }
}
