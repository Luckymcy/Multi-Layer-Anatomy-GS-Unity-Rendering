// SPDX-License-Identifier: MIT
using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GaussianSplatting.Runtime
{
    /// <summary>Desktop Game-view JSON import and version comparison. No file-picker plugin required.</summary>
    [AddComponentMenu("Gaussian Splatting/LUT Comparison Panel")]
    public sealed class LUTComparisonPanel : MonoBehaviour
    {
        public GaussianSplatRenderer rendererTarget;
        public int targetLayer = 7;
        public bool showPanel = true;
        Rect m_Window = new Rect(20, 20, 390, 360);
        Rect m_Browser = new Rect(420, 20, 560, 480);
        Vector2 m_VersionScroll, m_FileScroll;
        string m_Directory, m_LayerText = "7", m_Status = "Import a JSON to compare LUT versions.";
        string[] m_Directories = Array.Empty<string>(), m_Files = Array.Empty<string>();
        bool m_ShowBrowser;

        void OnEnable()
        {
            if (rendererTarget == null) rendererTarget = GetComponent<GaussianSplatRenderer>();
            m_LayerText = targetLayer.ToString();
            m_Directory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrEmpty(m_Directory) || !Directory.Exists(m_Directory))
                m_Directory = Application.persistentDataPath;
        }

        public static string ReadJsonFile(string path)
        {
            var file = new FileInfo(path);
            if (!file.Exists || !string.Equals(file.Extension, ".json", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Select an existing .json file.");
            if (file.Length > 1024 * 1024) throw new ArgumentException("JSON must be smaller than 1 MiB.");
            return File.ReadAllText(file.FullName);
        }

        // Can also be called by a custom UI / platform upload callback.
        public bool ImportJson(string json, string versionName)
        {
            try
            {
                if (rendererTarget == null) throw new ArgumentException("Assign a Gaussian Splat Renderer first.");
                rendererTarget.ImportLearnedLUT(json, versionName, targetLayer);
                m_Status = $"Loaded {versionName} into L{targetLayer}.";
                return true;
            }
            catch (Exception error) { m_Status = error.Message; return false; }
        }

        void OnGUI()
        {
            if (!showPanel) return;
            m_Window = GUILayout.Window(GetInstanceID(), m_Window, DrawPanel, "LUT comparison");
            if (m_ShowBrowser)
                m_Browser = GUILayout.Window(GetInstanceID() + 1, m_Browser, DrawBrowser, "Select LUT JSON");
        }

        void DrawPanel(int id)
        {
            if (rendererTarget == null)
            {
                GUILayout.Label("Assign Renderer Target in the Inspector.");
                GUI.DragWindow(new Rect(0, 0, 10000, 20));
                return;
            }
            int mode = GUILayout.SelectionGrid((int)rendererTarget.CurrentAppearanceMode,
                new[] { "Original Gaussian", "Manual", "Trained" }, 3);
            if (mode != (int)rendererTarget.CurrentAppearanceMode)
                rendererTarget.SetAppearanceMode((GaussianSplatRenderer.AppearanceMode)mode);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Target layer", GUILayout.Width(100));
            m_LayerText = GUILayout.TextField(m_LayerText, GUILayout.Width(65));
            bool validLayer = int.TryParse(m_LayerText, out int layer) && layer >= 0;
            if (validLayer) targetLayer = layer;
            GUILayout.EndHorizontal();
            GUI.enabled = validLayer;
            if (GUILayout.Button("Import JSON...")) { m_ShowBrowser = true; RefreshDirectory(); }
            GUI.enabled = true;
            var active = rendererTarget.ActiveLearnedLUT;
            GUILayout.Label(rendererTarget.CurrentAppearanceMode != GaussianSplatRenderer.AppearanceMode.Trained
                ? "Current: " + rendererTarget.CurrentAppearanceMode
                : active == null ? "No trained LUT: using original Gaussian colors" : $"Current: {active.name} (L{active.layer}, {active.hu.Length} controls)");
            m_VersionScroll = GUILayout.BeginScrollView(m_VersionScroll, GUILayout.Height(135));
            for (int i = 0; i < rendererTarget.LearnedLUTVersions.Count; ++i)
            {
                var version = rendererTarget.LearnedLUTVersions[i];
                DrawSelectionButton(i, $"{i + 1}. {version.name} — L{version.layer}");
            }
            GUILayout.EndScrollView();
            GUILayout.Label(m_Status, GUI.skin.box);
            GUILayout.Label("Imported LUT: full RGB replacement; opacity unchanged.", GUI.skin.label);
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        void DrawSelectionButton(int index, string label)
        {
            if (GUILayout.Button((rendererTarget.CurrentAppearanceMode == GaussianSplatRenderer.AppearanceMode.Trained && rendererTarget.ActiveLearnedLUTIndex == index ? "[Active] " : "") + label))
            {
                try { rendererTarget.SelectLearnedLUT(index); m_Status = "Switched to " + label; }
                catch (Exception error) { m_Status = error.Message; }
            }
        }

        void RefreshDirectory()
        {
            try
            {
                var directory = new DirectoryInfo(m_Directory);
                m_Directories = directory.GetDirectories().OrderBy(p => p.Name).Select(p => p.FullName).ToArray();
                m_Files = directory.GetFiles().Where(p => p.Extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(p => p.Name).Select(p => p.FullName).ToArray();
                m_Directory = directory.FullName;
                m_FileScroll = Vector2.zero;
            }
            catch (Exception error)
            {
                m_Status = error.Message;
                m_Directories = m_Files = Array.Empty<string>();
            }
        }

        void DrawBrowser(int id)
        {
            GUILayout.Label("Folder path (paste a path, then click Open folder):");
            m_Directory = GUILayout.TextField(m_Directory);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Open folder")) RefreshDirectory();
            if (GUILayout.Button("Parent folder"))
            {
                try { m_Directory = Directory.GetParent(m_Directory)?.FullName ?? m_Directory; RefreshDirectory(); }
                catch (Exception error) { m_Status = error.Message; }
            }
            if (GUILayout.Button("Close")) m_ShowBrowser = false;
            GUILayout.EndHorizontal();
            m_FileScroll = GUILayout.BeginScrollView(m_FileScroll, GUILayout.Height(340));
            foreach (string directory in m_Directories)
                if (GUILayout.Button("[Folder] " + Path.GetFileName(directory)))
                { m_Directory = directory; RefreshDirectory(); break; }
            foreach (string path in m_Files)
                if (GUILayout.Button(Path.GetFileName(path)))
                {
                    try
                    {
                        if (ImportJson(ReadJsonFile(path), Path.GetFileNameWithoutExtension(path)))
                            m_ShowBrowser = false;
                    }
                    catch (Exception error) { m_Status = error.Message; }
                    break;
                }
            GUILayout.EndScrollView();
            GUILayout.Label(m_Status, GUI.skin.box);
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }
    }
}
