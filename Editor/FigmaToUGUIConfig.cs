using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEditor;
using FigmaClient;

namespace FigmaImporter.Editor
{
    
    [Serializable]
    public class FigmaPrefabResult
    {
        public string file_key;
        public string api_token;
        public string node_name;
        public string raw_content_path;
        public string prefab_hierarchy_path;

        public bool Valid
        {
            get
            {
                if (string.IsNullOrEmpty(file_key)) return false;
                if (string.IsNullOrEmpty(api_token)) return false;
                if (string.IsNullOrEmpty(raw_content_path) || !File.Exists(raw_content_path)) return false;
                if (string.IsNullOrEmpty(prefab_hierarchy_path) || !File.Exists(prefab_hierarchy_path)) return false;
                return true;
            }
        }
    }
    
    [CustomEditor(typeof(FigmaToUGUIConfig))]
    public class FigmaToUGUIConfigInspector : UnityEditor.Editor
    {
        private FigmaToUGUIConfig config => (FigmaToUGUIConfig)target;
        private bool isGenerating;
        private HashSet<int> selectedIndices = new HashSet<int>();

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            
            if (config.FigmaPages == null || config.FigmaPages.Length == 0) return;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Generate Prefab", EditorStyles.boldLabel);

            if (isGenerating) EditorGUILayout.HelpBox("A prefab generation is already in progress. Please wait…", MessageType.Warning);

            // ── 重新渲染 FigmaPages，每项带 checkbox ──
            for (int i = 0; i < config.FigmaPages.Length; i++)
            {
                GUI.enabled = !isGenerating;
                bool sel = selectedIndices.Contains(i);
                bool newSel = EditorGUILayout.Toggle(config.FigmaPages[i]?.name ?? "(empty)", sel, GUILayout.Width(18));
                if (newSel != sel)
                {
                    if (newSel) selectedIndices.Add(i);
                    else selectedIndices.Remove(i);
                }
                GUI.enabled = true;
            }

            EditorGUILayout.Space(5);

            var canGenerate = !isGenerating && selectedIndices.Count > 0;
            GUI.enabled = canGenerate;
            GUI.backgroundColor = canGenerate ? new UnityEngine.Color(0.4f, 0.8f, 0.4f) : UnityEngine.Color.white;
            if (GUILayout.Button("Generate Selected Pages", GUILayout.Height(36)))
            {
                var toGenerate = new List<FigmaPrefabResult>();
                foreach (var idx in selectedIndices)
                {
                    try
                    {
                        var ta = config.FigmaPages[idx];
                        var rr = JsonUtility.FromJson<FigmaPrefabResult>(ta.text);
                        toGenerate.Add(rr);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[FigmaToUGUI] Failed to parse '{idx}': {ex.Message}, skipped.");
                    }
                }
                GenerateSelected(toGenerate);
            }
            GUI.backgroundColor = UnityEngine.Color.white;
            GUI.enabled = true;
        }

        private async void GenerateSelected(List<FigmaPrefabResult> results)
        {
            if (isGenerating || results.Count == 0) return;

            isGenerating = true;
            Repaint();

            int succeeded = 0, failed = 0;

            try
            {
                for (int i = 0; i < results.Count; i++)
                {
                    var r = results[i];
                    try
                    {
                        EditorUtility.DisplayProgressBar("Generating Prefabs",  $"({i + 1}/{results.Count}) {r.node_name}…",(float)i / results.Count);

                        string raw = File.ReadAllText(r.raw_content_path);
                        string hierarchy = File.ReadAllText(r.prefab_hierarchy_path);
                        await FigmaToUGUIConvertor.ConvertFigmaToUGUIPrefab(config, r.file_key, r.api_token, raw, hierarchy);
                        succeeded++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        Debug.LogError($"[FigmaToUGUI] Failed '{r.node_name}': {ex.Message}\n{ex.StackTrace}");
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                isGenerating = false;
                Repaint();
                var msg = $"Generated {succeeded} prefab(s) successfully. \nFailed: {failed} prefabs (See Console for details)";
                EditorUtility.DisplayDialog("Generation Complete", msg, "OK");
            }
        }
    }
    
    [CreateAssetMenu(menuName = "FigmaToUGUI/Config")]
    public class FigmaToUGUIConfig : ScriptableObject
    {
        [Serializable]
        public class FigmaFontItem
        {
            public string Name;
            public int Weight;
            [Header("字体粗细, 如 Regular，SemiBold等")]
            public string Style;
            public TMP_FontAsset Font;
        }

        [SerializeField] private List<FigmaFontItem> FontItems;
        [SerializeField] public string MaterialSaveFolder;
        [SerializeField] public string SpriteSaveFolder;
        [SerializeField] public string PrefabSaveFolder = "Assets/Prefabs";
        
        [SerializeField] public TextAsset[] FigmaPages;
        
        public TMP_FontAsset GetFont(Node textNode)
        {
            Style style = textNode.style;
            
            var fontItem = FontItems.Find(item =>
            {
                if (item.Weight != style.fontWeight) return false;
                if (!item.Name.Equals(style.fontFamily, StringComparison.InvariantCultureIgnoreCase)) return false;
                if (string.IsNullOrEmpty(style.fontPostScriptName) && string.IsNullOrEmpty(item.Style)) return true;
                return style.fontPostScriptName.Contains(item.Style, StringComparison.InvariantCultureIgnoreCase);
            });
            if (fontItem == null)
            {
                Debug.LogError($"[Figma Importer] cannot find TMP Font Asset for node {textNode.name} (id:{textNode.id})");
            }
            return fontItem?.Font;
        }
    }

}
