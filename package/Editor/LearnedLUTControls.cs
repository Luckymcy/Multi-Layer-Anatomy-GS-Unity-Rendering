// SPDX-License-Identifier: MIT
using System;
using System.IO;
using GaussianSplatting.Runtime;
using UnityEditor;
using UnityEngine;

namespace GaussianSplatting.Editor
{
    /// <summary>Shared Inspector controls; imported values persist with the scene/prefab.</summary>
    internal static class LearnedLUTControls
    {
        static int s_TargetLayer = 7;
        static string s_Status;

        public static void Draw(GaussianSplatRenderer renderer)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Learned LUT / JSON versions", EditorStyles.boldLabel);
            s_TargetLayer = EditorGUILayout.IntField("Import into layer", s_TargetLayer);
            if (GUILayout.Button("Import LUT JSON..."))
            {
                string path = EditorUtility.OpenFilePanel("Import learned RGB LUT", "", "json");
                if (!string.IsNullOrEmpty(path))
                {
                    try
                    {
                        string json = LUTComparisonPanel.ReadJsonFile(path);
                        // Validate before recording an import. Renderer validates the asset as well.
                        LearnedLUTVersion.Parse(json, Path.GetFileNameWithoutExtension(path), s_TargetLayer);
                        Undo.RecordObject(renderer, "Import Learned LUT");
                        renderer.ImportLearnedLUT(json, Path.GetFileNameWithoutExtension(path), s_TargetLayer);
                        EditorUtility.SetDirty(renderer);
                        s_Status = "Imported " + Path.GetFileName(path);
                    }
                    catch (Exception error) { s_Status = "Import failed: " + error.Message; }
                }
            }
            var versions = renderer.LearnedLUTVersions;
            var names = new string[versions.Count + 1];
            names[0] = "Original / manual";
            for (int i = 0; i < versions.Count; ++i) names[i + 1] = $"{i + 1}. {versions[i].name} (L{versions[i].layer})";
            int selection = EditorGUILayout.Popup("Displayed version", renderer.ActiveLearnedLUTIndex + 1, names) - 1;
            if (selection != renderer.ActiveLearnedLUTIndex)
            {
                try
                {
                    Undo.RecordObject(renderer, "Switch Learned LUT");
                    renderer.SelectLearnedLUT(selection);
                    EditorUtility.SetDirty(renderer);
                    s_Status = null;
                }
                catch (Exception error) { s_Status = error.Message; }
            }
            if (GUILayout.Button("Add / select Game-view LUT panel"))
            {
                var panel = renderer.GetComponent<LUTComparisonPanel>();
                if (panel == null) panel = Undo.AddComponent<LUTComparisonPanel>(renderer.gameObject);
                Undo.RecordObject(panel, "Configure LUT Comparison Panel");
                panel.rendererTarget = renderer;
                panel.targetLayer = s_TargetLayer;
                panel.showPanel = true;
                EditorUtility.SetDirty(panel);
                Selection.activeGameObject = renderer.gameObject;
            }
            if (!string.IsNullOrEmpty(s_Status)) EditorGUILayout.HelpBox(s_Status, MessageType.Info);
            EditorGUILayout.HelpBox(
                "Imported versions fully replace RGB on the selected layer using raw HU; old SH lighting and HU mix/detail are bypassed. " +
                "Opacity and other layers are preserved. Choose Original / manual to restore the existing settings. Save the scene to retain imported versions.", MessageType.Info);
        }
    }
}
