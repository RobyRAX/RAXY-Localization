using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.Google;
using UnityEditor.Localization.Reporting;
using UnityEngine;

namespace RAXY.Utility.Localization.Editor
{
    public class LocalizationHelperWindow : EditorWindow
    {
        private readonly List<TablePullEntry> _tableEntries = new();
        private Vector2 _scroll;
        private string _search = string.Empty;
        private bool _isPulling;

        [MenuItem("Tools/RAXY/Localization Helper")]
        public static void Open()
        {
            var window = GetWindow<LocalizationHelperWindow>("Localization Helper");
            window.minSize = new Vector2(620f, 360f);
            window.Show();
        }

        private void OnEnable()
        {
            ScanTables();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawSummary();
            DrawTableList();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new EditorGUI.DisabledScope(_isPulling))
                {
                    if (GUILayout.Button("Scan", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                        ScanTables();

                    if (GUILayout.Button("Pull All", EditorStyles.toolbarButton, GUILayout.Width(88f)))
                        PullAllValidTables();
                }

                GUILayout.Space(8f);
                GUILayout.Label("Search", GUILayout.Width(44f));
                _search = GUILayout.TextField(_search, GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.toolbarTextField);

                if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(54f)))
                    _search = string.Empty;
            }
        }

        private void DrawSummary()
        {
            int extensionCount = _tableEntries.Sum(entry => entry.GoogleExtensions.Count);
            int pullableCount = _tableEntries.Sum(entry => entry.GoogleExtensions.Count(IsPullable));

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("String Table Collections", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                $"Found {_tableEntries.Count} table collections, {extensionCount} Google Sheets extensions, {pullableCount} ready to pull.",
                MessageType.Info);
        }

        private void DrawTableList()
        {
            var entries = GetFilteredEntries();

            if (entries.Count == 0)
            {
                EditorGUILayout.HelpBox("No table collections match the current search.", MessageType.Warning);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (var entry in entries)
            {
                DrawTableEntry(entry);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawTableEntry(TablePullEntry entry)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    entry.IsExpanded = EditorGUILayout.Foldout(entry.IsExpanded, entry.Collection.TableCollectionName, true);

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Select", GUILayout.Width(64f)))
                    {
                        Selection.activeObject = entry.Collection;
                        EditorGUIUtility.PingObject(entry.Collection);
                    }

                    using (new EditorGUI.DisabledScope(_isPulling || !entry.GoogleExtensions.Any(IsPullable)))
                    {
                        if (GUILayout.Button("Pull", GUILayout.Width(64f)))
                            PullTable(entry);
                    }
                }

                EditorGUILayout.LabelField("Path", entry.AssetPath);

                if (!string.IsNullOrEmpty(entry.LastStatus))
                    EditorGUILayout.LabelField("Status", entry.LastStatus);

                if (!entry.IsExpanded)
                    return;

                if (entry.GoogleExtensions.Count == 0)
                {
                    EditorGUILayout.HelpBox("No Google Sheets extension found on this table collection.", MessageType.Warning);
                    return;
                }

                for (int i = 0; i < entry.GoogleExtensions.Count; i++)
                {
                    DrawGoogleExtension(entry, entry.GoogleExtensions[i], i);
                }
            }
        }

        private void DrawGoogleExtension(TablePullEntry entry, GoogleSheetsExtension extension, int index)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"Google Sheets Extension {index + 1}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Spreadsheet Id", string.IsNullOrEmpty(extension.SpreadsheetId) ? "<empty>" : extension.SpreadsheetId);
                EditorGUILayout.LabelField("Sheet Id", extension.SheetId.ToString());
                EditorGUILayout.LabelField("Columns", extension.Columns?.Count.ToString() ?? "0");
                EditorGUILayout.LabelField("Remove Missing Keys", extension.RemoveMissingPulledKeys.ToString());

                string validation = GetValidationMessage(extension);
                if (!string.IsNullOrEmpty(validation))
                    EditorGUILayout.HelpBox(validation, MessageType.Warning);

                using (new EditorGUI.DisabledScope(_isPulling || !IsPullable(extension)))
                {
                    if (GUILayout.Button("Pull This Extension"))
                        PullExtension(entry, extension);
                }
            }
        }

        private List<TablePullEntry> GetFilteredEntries()
        {
            if (string.IsNullOrWhiteSpace(_search))
                return _tableEntries;

            return _tableEntries
                .Where(entry => entry.Collection.TableCollectionName.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                entry.AssetPath.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
        }

        private void ScanTables()
        {
            _tableEntries.Clear();

            foreach (var collection in LocalizationEditorSettings.GetStringTableCollections())
            {
                if (collection == null)
                    continue;

                var googleExtensions = collection.Extensions
                    .OfType<GoogleSheetsExtension>()
                    .ToList();

                _tableEntries.Add(new TablePullEntry
                {
                    Collection = collection,
                    GoogleExtensions = googleExtensions,
                    AssetPath = AssetDatabase.GetAssetPath(collection),
                    IsExpanded = googleExtensions.Count > 0
                });
            }

            _tableEntries.Sort((a, b) => string.Compare(a.Collection.TableCollectionName, b.Collection.TableCollectionName, StringComparison.OrdinalIgnoreCase));
            Repaint();
        }

        private void PullAllValidTables()
        {
            foreach (var entry in _tableEntries)
            {
                PullTable(entry, false);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Repaint();
        }

        private void PullTable(TablePullEntry entry, bool saveAssets = true)
        {
            foreach (var extension in entry.GoogleExtensions)
            {
                if (IsPullable(extension))
                    PullExtension(entry, extension, false);
            }

            if (saveAssets)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Repaint();
            }
        }

        private void PullExtension(TablePullEntry entry, GoogleSheetsExtension extension, bool saveAssets = true)
        {
            _isPulling = true;

            try
            {
                var collection = extension.TargetCollection as StringTableCollection ?? entry.Collection;
                var googleSheets = new GoogleSheets(extension.SheetsServiceProvider)
                {
                    SpreadSheetId = extension.SpreadsheetId
                };

                googleSheets.PullIntoStringTableCollection(
                    extension.SheetId,
                    collection,
                    extension.Columns,
                    extension.RemoveMissingPulledKeys,
                    new ProgressBarReporter(),
                    true);

                entry.LastStatus = $"Pulled at {DateTime.Now:HH:mm:ss}";
                Debug.Log($"[Localization Helper] Pulled `{collection.TableCollectionName}` from Google Sheets.");

                if (saveAssets)
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
            }
            catch (Exception e)
            {
                entry.LastStatus = $"Pull failed: {e.Message}";
                Debug.LogError($"[Localization Helper] Failed to pull `{entry.Collection.TableCollectionName}`: {e}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _isPulling = false;
                Repaint();
            }
        }

        private static bool IsPullable(GoogleSheetsExtension extension)
        {
            return extension != null &&
                   extension.SheetsServiceProvider != null &&
                   !string.IsNullOrWhiteSpace(extension.SpreadsheetId) &&
                   extension.Columns != null &&
                   extension.Columns.Count > 0;
        }

        private static string GetValidationMessage(GoogleSheetsExtension extension)
        {
            if (extension == null)
                return "Extension is null.";

            if (extension.SheetsServiceProvider == null)
                return "Sheets Service Provider is missing.";

            if (string.IsNullOrWhiteSpace(extension.SpreadsheetId))
                return "Spreadsheet Id is empty.";

            if (extension.Columns == null || extension.Columns.Count == 0)
                return "Column mappings are empty.";

            return null;
        }

        [Serializable]
        private class TablePullEntry
        {
            public StringTableCollection Collection;
            public List<GoogleSheetsExtension> GoogleExtensions = new();
            public string AssetPath;
            public string LastStatus;
            public bool IsExpanded;
        }
    }
}
