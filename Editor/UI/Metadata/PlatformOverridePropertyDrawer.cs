using System;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Tables;
using UnityEngine.Pool;

namespace UnityEditor.Localization.UI
{
    [CustomPropertyDrawer(typeof(PlatformOverride))]
    class PlatformOverridePropertyDrawer : PropertyDrawer
    {
        class Styles
        {
            public static readonly GUIContent entry = new GUIContent("Table Entry");
            public static readonly GUIContent none = new GUIContent("None");
            public static readonly GUIContent reference = new GUIContent("Reference");
            public static readonly GUIContent tableCollection = new GUIContent("Table Collection");
        }

        ReorderableList m_PlatformOverridesList;
        LocalizationTableCollection m_Collection;
        Type m_TableType;

        static readonly GUIContent kPlatformOverrides = EditorGUIUtility.TrTextContent("Platform Overrides");
        static readonly int[] s_PlatformValues;

        static PlatformOverridePropertyDrawer()
        {
            s_PlatformValues = Enum.GetValues(typeof(RuntimePlatform)) as int[];
        }

        void Init(SerializedProperty property)
        {
            if (m_PlatformOverridesList == null)
            {
                m_PlatformOverridesList = new ReorderableList(property.serializedObject, property.FindPropertyRelative("m_PlatformOverrides"))
                {
                    drawElementCallback = DrawListElement,
                    drawHeaderCallback = DrawListHeader,
                    onAddDropdownCallback = DrawAddDropdown,
                    elementHeightCallback = GetHeight
                };

                var target = m_PlatformOverridesList.serializedProperty.serializedObject.targetObject;
                SharedTableData sharedTableData = target as SharedTableData ?? (target as LocalizationTable)?.SharedData;
                Debug.Assert(sharedTableData != null, "Shared Table Data is null. Platform Override can only be used on a Shared Table Entry.");
                m_Collection = LocalizationEditorSettings.GetCollectionForSharedTableData(sharedTableData);
                m_TableType = m_Collection.GetType() == typeof(StringTableCollection) ? typeof(StringTable) : typeof(AssetTable);
            }
        }

        void DrawListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            rect.height = EditorGUIUtility.singleLineHeight;

            rect.xMin += 12; // Small indent so the foldout arrow does not cover the dragger.
            var element = m_PlatformOverridesList.serializedProperty.GetArrayElementAtIndex(index);
            var platform = (RuntimePlatform)element.FindPropertyRelative("platform").intValue;

            EditorGUI.LabelField(rect, EditorGUIUtility.TrTempContent(platform.ToString()), EditorStyles.boldLabel);
            rect.MoveToNextLine();

            var overrideTypeProperty = element.FindPropertyRelative("entryOverrideType");
            EditorGUI.PropertyField(rect, overrideTypeProperty);
            rect.MoveToNextLine();

            var overrideType = (EntryOverrideType)overrideTypeProperty.intValue;
            switch (overrideType)
            {
                case EntryOverrideType.Table:
                    DoTableGUI(rect, element);
                    break;
                case EntryOverrideType.Entry:
                    DoEntryGUI(rect, element);
                    break;
                case EntryOverrideType.TableAndEntry:
                    DoTableAndEntryGUI(rect, element);
                    break;
            }
        }

        static string GetTableLabel(TableReference tableReference)
        {
            if (tableReference.ReferenceType == TableReference.Type.Guid)
            {
                var guid = TableReference.StringFromGuid(tableReference.TableCollectionNameGuid);
                var collection = LocalizationEditorSettings.Instance.TableCollectionCache.FindCollectionFromDependencyGuid(guid);
                if (collection != null)
                    return collection.TableCollectionName;
                else
                    return "Missing Collection: " + guid;
            }
            else if (tableReference.ReferenceType == TableReference.Type.Name && !string.IsNullOrEmpty(tableReference.TableCollectionName))
            {
                return tableReference.TableCollectionName;
            }
            return "None";
        }

        static string GetEntryLabel(TableEntryReference entryReference, SharedTableData sharedTableData)
        {
            return entryReference.ReferenceType != TableEntryReference.Type.Empty ? entryReference.ResolveKeyName(sharedTableData) : "None";
        }

        void DoTableGUI(Rect rect, SerializedProperty property)
        {
            var tableRef = new SerializedTableReference(property.FindPropertyRelative("tableReference"));
            var valueLabel = new GUIContent(GetTableLabel(tableRef.Reference));

            var dropDownPosition = EditorGUI.PrefixLabel(rect, Styles.tableCollection);
            if (EditorGUI.DropdownButton(dropDownPosition, valueLabel, FocusType.Passive))
            {
                var treeSelection = new TableTreeView(m_TableType, sel =>
                {
                    tableRef.Reference = sel != null ? sel.TableCollectionNameReference : default(TableReference);
                    var entryRef = new SerializedTableEntryReference(property.FindPropertyRelative("tableEntryReference"));
                    entryRef.Reference = SharedTableData.EmptyId;
                    property.serializedObject.ApplyModifiedProperties();
                });
                PopupWindow.Show(dropDownPosition, new TreeViewPopupWindow(treeSelection) { Width = dropDownPosition.width });
            }
        }

        void DoEntryGUI(Rect rect, SerializedProperty property)
        {
            var entryRef = new SerializedTableEntryReference(property.FindPropertyRelative("tableEntryReference"));
            var valueLabel = new GUIContent(GetEntryLabel(entryRef.Reference, m_Collection.SharedData));

            var dropDownPosition = EditorGUI.PrefixLabel(rect, Styles.entry);
            if (EditorGUI.DropdownButton(dropDownPosition, valueLabel, FocusType.Passive))
            {
                Type assetType;
                if (m_TableType == typeof(AssetTable))
                {
                    var assetTableCollection = m_Collection as AssetTableCollection;
                    assetType = assetTableCollection.GetEntryAssetType(entryRef.Reference);
                }
                else
                {
                    assetType = typeof(string);
                }

                var treeSelection = new EntryTreeView(assetType, m_Collection, (c, e) =>
                {
                    entryRef.Reference = e != null ? e.Id : SharedTableData.EmptyId;
                    var tableRef = new SerializedTableReference(property.FindPropertyRelative("tableReference"));
                    tableRef.Reference = c != null ? c.TableCollectionNameReference : default(TableReference);
                    property.serializedObject.ApplyModifiedProperties();
                });
                PopupWindow.Show(dropDownPosition, new TreeViewPopupWindow(treeSelection) { Width = dropDownPosition.width });
            }
        }

        void DoTableAndEntryGUI(Rect rect, SerializedProperty property)
        {
            var tableRef = new SerializedTableReference(property.FindPropertyRelative("tableReference"));
            var entryRef = new SerializedTableEntryReference(property.FindPropertyRelative("tableEntryReference"));
            GUIContent valueLabel;
            if (tableRef.Reference.ReferenceType != TableReference.Type.Empty && entryRef.Reference.ReferenceType != TableEntryReference.Type.Empty)
            {
                LocalizationTableCollection collection = null;
                if (m_TableType == typeof(StringTable))
                    collection = LocalizationEditorSettings.GetStringTableCollection(tableRef.Reference);
                else
                    collection = LocalizationEditorSettings.GetAssetTableCollection(tableRef.Reference);
                valueLabel = new GUIContent($"{GetTableLabel(tableRef.Reference)}/{GetEntryLabel(entryRef.Reference, collection?.SharedData)}");
            }
            else
            {
                valueLabel = Styles.none;
            }

            var dropDownPosition = EditorGUI.PrefixLabel(rect, Styles.reference);
            if (EditorGUI.DropdownButton(dropDownPosition, valueLabel, FocusType.Passive))
            {
                Type assetType;
                if (m_TableType == typeof(AssetTable))
                {
                    var assetTableCollection = m_Collection as AssetTableCollection;
                    assetType = assetTableCollection.GetEntryAssetType(entryRef.Reference);
                }
                else
                {
                    assetType = typeof(string);
                }

                var treeSelection = new TableEntryTreeView(assetType, (c, e) =>
                {
                    entryRef.Reference = e != null ? e.Id : SharedTableData.EmptyId;
                    tableRef.Reference = c != null ? c.TableCollectionNameReference : default(TableReference);
                    property.serializedObject.ApplyModifiedProperties();
                });
                PopupWindow.Show(dropDownPosition, new TreeViewPopupWindow(treeSelection) { Width = dropDownPosition.width });
            }
        }

        void DrawListHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, kPlatformOverrides);
        }

        void DrawAddDropdown(Rect buttonRect, ReorderableList list)
        {
            using (HashSetPool<int>.Get(out var hashSet))
            {
                for (int i = 0; i < m_PlatformOverridesList.serializedProperty.arraySize; ++i)
                {
                    hashSet.Add(m_PlatformOverridesList.serializedProperty.GetArrayElementAtIndex(i).FindPropertyRelative("platform").intValue);
                }

                var menu = new GenericMenu();
                foreach (var platform in s_PlatformValues)
                {
                    if (!hashSet.Contains(platform))
                    {
                        var label = EditorGUIUtility.TrTextContent(((RuntimePlatform)platform).ToString());
                        menu.AddItem(label, false, AddPlatform, platform);
                    }
                }
                menu.DropDown(buttonRect);
            }
        }

        float GetHeight(int index)
        {
            var element = m_PlatformOverridesList.serializedProperty.GetArrayElementAtIndex(index);
            var overrideTypeProperty = element.FindPropertyRelative("entryOverrideType");
            var overrideType = (EntryOverrideType)overrideTypeProperty.intValue;

            float singleLine = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            return overrideType == EntryOverrideType.None ? singleLine * 2 : singleLine * 3;
        }

        void AddPlatform(object platform)
        {
            var item = m_PlatformOverridesList.serializedProperty.AddArrayElement();
            item.FindPropertyRelative("platform").intValue = (int)platform;
            m_PlatformOverridesList.serializedProperty.serializedObject.ApplyModifiedProperties();
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            Init(property);
            m_PlatformOverridesList.DoList(position);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            Init(property);
            return m_PlatformOverridesList.GetHeight();
        }
    }
}