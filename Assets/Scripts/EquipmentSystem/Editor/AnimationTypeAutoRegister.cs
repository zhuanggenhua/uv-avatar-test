using UnityEngine;
using UnityEditor;
using EquipmentSystem.Data;
using System.Linq;

namespace EquipmentSystem.Editor
{
    /// <summary>
    /// 自动将新建的 AnimationTypeItem 注册到 Database
    /// </summary>
    public class AnimationTypeAutoRegister : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(
            string[] importedAssets, 
            string[] deletedAssets, 
            string[] movedAssets, 
            string[] movedFromAssetPaths)
        {
            // 查找导入的 AnimationTypeItem
            foreach (string path in importedAssets)
            {
                if (!path.EndsWith(".asset")) continue;
                
                var item = AssetDatabase.LoadAssetAtPath<AnimationTypeItem>(path);
                if (item == null) continue;
                
                // 查找 Database 并自动注册
                AutoRegisterToDatabase(item);
            }
        }
        
        static void AutoRegisterToDatabase(AnimationTypeItem item)
        {
            // 查找项目中的 AnimationTypeDatabase
            var guids = AssetDatabase.FindAssets("t:AnimationTypeDatabase");
            if (guids.Length == 0) return;
            
            // 注册到第一个找到的 Database
            string dbPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            var database = AssetDatabase.LoadAssetAtPath<AnimationTypeDatabase>(dbPath);
            
            if (database != null && !database.Contains(item))
            {
                database.EditorAddItem(item);
                EditorUtility.SetDirty(database);
                AssetDatabase.SaveAssets();
                Debug.Log($"[AnimationType] 已自动注册 '{item.name}' 到 {database.name}");
            }
        }
    }
    
    /// <summary>
    /// Database 编辑器扩展
    /// </summary>
    [CustomEditor(typeof(AnimationTypeDatabase))]
    public class AnimationTypeDatabaseEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var db = (AnimationTypeDatabase)target;
            
            EditorGUILayout.HelpBox(
                "动画类型数据库\n" +
                "新建的 AnimationTypeItem 会自动注册到这里\n" +
                "也可以手动拖拽添加", MessageType.Info);
            
            EditorGUILayout.Space();
            
            // 显示统计
            EditorGUILayout.LabelField($"已注册类型: {db.Count} 个", EditorStyles.boldLabel);
            
            EditorGUILayout.Space();
            
            // 扫描并添加所有未注册的
            if (GUILayout.Button("扫描并注册所有 AnimationTypeItem"))
            {
                ScanAndRegisterAll(db);
            }
            
            EditorGUILayout.Space();
            
            // 默认绘制
            DrawDefaultInspector();
        }
        
        void ScanAndRegisterAll(AnimationTypeDatabase db)
        {
            var guids = AssetDatabase.FindAssets("t:AnimationTypeItem");
            int added = 0;
            
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<AnimationTypeItem>(path);
                
                if (item != null && !db.Contains(item))
                {
                    db.EditorAddItem(item);
                    added++;
                }
            }
            
            if (added > 0)
            {
                EditorUtility.SetDirty(db);
                AssetDatabase.SaveAssets();
                Debug.Log($"[AnimationType] 已注册 {added} 个新类型");
            }
            else
            {
                Debug.Log("[AnimationType] 所有类型已注册，无需添加");
            }
        }
    }
}
