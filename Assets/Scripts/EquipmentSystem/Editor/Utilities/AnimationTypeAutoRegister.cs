using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EquipmentSystem.Editor
{
    /// <summary>
    /// 动画类型自动注册工具
    /// - 提供静态方法：手动扫描所有 AnimationTypeItem 并注册到 AnimationTypeDatabase
    /// - 提供 AssetPostprocessor：在导入 AnimationTypeItem 资产时自动注册
    /// </summary>
    public class AnimationTypeAutoRegister : AssetPostprocessor
    {
        /// <summary>
        /// 手动扫描项目中所有 AnimationTypeItem，并注册到第一个 AnimationTypeDatabase
        /// （供 AnimationTypeDatabase Inspector 按钮调用）
        /// </summary>
        public static void ScanAndRegisterAll()
        {
            var db = FindDatabase(logIfNotFound: true);
            if (db == null)
                return;

            // 先清理数据库中的 null 引用
            var items = db.Items;
            int removed = 0;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (items[i] == null)
                {
                    items.RemoveAt(i);
                    removed++;
                }
            }
            if (removed > 0)
                db.RebuildCache();

            // 扫描并注册新的动画类型
            string[] typeGuids = AssetDatabase.FindAssets("t:AnimationTypeItem");
            int added = 0;

            foreach (var guid in typeGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<AnimationTypeItem>(path);
                if (item == null)
                    continue;

                if (!db.Contains(item))
                {
                    db.EditorAddItem(item);
                    added++;
                }
            }

            if (added > 0 || removed > 0)
            {
                EditorUtility.SetDirty(db);
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"[AnimationTypeAutoRegister] 手动扫描完成，新增 {added} 个，清理 {removed} 个空引用。");
        }

        /// <summary>
        /// 监听资产变更：
        /// - 导入 AnimationTypeItem 时自动注册到数据库
        /// - 删除 AnimationTypeItem 时自动从数据库中移除空引用
        /// </summary>
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            AnimationTypeDatabase db = null;
            int added = 0;
            int removed = 0;

            // 处理导入：自动注册新的 AnimationTypeItem
            if (importedAssets != null && importedAssets.Length > 0)
            {
                var newItems = new List<AnimationTypeItem>();
                foreach (var path in importedAssets)
                {
                    var item = AssetDatabase.LoadAssetAtPath<AnimationTypeItem>(path);
                    if (item != null)
                        newItems.Add(item);
                }

                if (newItems.Count > 0)
                {
                    db ??= FindDatabase(logIfNotFound: false);
                    if (db != null)
                    {
                        foreach (var item in newItems)
                        {
                            if (!db.Contains(item))
                            {
                                db.EditorAddItem(item);
                                added++;
                            }
                        }
                    }
                }
            }

            // 处理删除：清理数据库中变成 null 的引用
            if (deletedAssets != null && deletedAssets.Length > 0)
            {
                db ??= FindDatabase(logIfNotFound: false);
                if (db != null)
                {
                    var items = db.Items;
                    for (int i = items.Count - 1; i >= 0; i--)
                    {
                        if (items[i] == null)
                        {
                            items.RemoveAt(i);
                            removed++;
                        }
                    }

                    if (removed > 0)
                        db.RebuildCache();
                }
            }

            if (db != null && (added > 0 || removed > 0))
            {
                EditorUtility.SetDirty(db);
                AssetDatabase.SaveAssets();

                if (added > 0)
                    Debug.Log($"[AnimationTypeAutoRegister] 自动注册 {added} 个动画类型。");
                if (removed > 0)
                    Debug.Log($"[AnimationTypeAutoRegister] 自动移除 {removed} 个已删除的动画类型引用。");
            }
        }

        /// <summary>
        /// 查找一个 AnimationTypeDatabase 资源（当前实现：取项目中找到的第一个）
        /// </summary>
        static AnimationTypeDatabase FindDatabase(bool logIfNotFound)
        {
            string[] guids = AssetDatabase.FindAssets("t:AnimationTypeDatabase");
            if (guids == null || guids.Length == 0)
            {
                if (logIfNotFound)
                    Debug.LogWarning("[AnimationTypeAutoRegister] 未找到 AnimationTypeDatabase 资源，请先创建数据库资产。");
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var db = AssetDatabase.LoadAssetAtPath<AnimationTypeDatabase>(path);
            if (db == null && logIfNotFound)
            {
                Debug.LogWarning($"[AnimationTypeAutoRegister] 无法加载 AnimationTypeDatabase: {path}");
            }

            return db;
        }
    }
}
