using System;
using UnityEditor;
using UnityEngine;

namespace EquipmentSystem.Editor
{
    /// <summary>
    /// 纹理可读性管理工具 - 临时启用纹理可读并自动恢复
    /// 支持域重载时的自动恢复
    /// </summary>
    public static class TextureReadableScope
    {
        const string PREF_PENDING_RESTORE_PATH = "TextureReadableScope_PendingRestorePath";
        
        /// <summary>
        /// 在编辑器初始化时检查并恢复上次未正确恢复的纹理设置
        /// </summary>
        [InitializeOnLoadMethod]
        static void Initialize()
        {
            RestorePendingTexture();
        }
        
        /// <summary>
        /// 恢复上次可能未正确恢复的纹理可读设置
        /// </summary>
        public static void RestorePendingTexture()
        {
            string pendingPath = EditorPrefs.GetString(PREF_PENDING_RESTORE_PATH, "");
            if (string.IsNullOrEmpty(pendingPath)) return;
            
            var importer = AssetImporter.GetAtPath(pendingPath) as TextureImporter;
            if (importer != null && importer.isReadable)
            {
                importer.isReadable = false;
                importer.SaveAndReimport();
                Debug.Log($"[TextureReadableScope] 已恢复纹理可读设置: {pendingPath}");
            }
            EditorPrefs.DeleteKey(PREF_PENDING_RESTORE_PATH);
        }
        
        /// <summary>
        /// 确保纹理可读后执行操作，完成后自动恢复原始设置
        /// </summary>
        /// <param name="texture">目标纹理</param>
        /// <param name="action">要执行的操作，参数为可读的纹理</param>
        /// <returns>操作是否成功执行</returns>
        public static bool Execute(Texture2D texture, Action<Texture2D> action)
        {
            if (texture == null) return false;
            
            // 已经可读，直接执行
            if (texture.isReadable)
            {
                action?.Invoke(texture);
                return true;
            }
            
            // 获取纹理路径和导入器
            var path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path)) return false;
            
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return false;
            
            // 保存原始状态
            bool originalIsReadable = importer.isReadable;
            bool needRestore = !originalIsReadable;
            
            try
            {
                if (needRestore)
                {
                    // 记录待恢复路径，防止域重载时丢失
                    EditorPrefs.SetString(PREF_PENDING_RESTORE_PATH, path);
                    
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                }
                
                // 重新加载纹理
                var readableTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (readableTexture != null && readableTexture.isReadable)
                {
                    action?.Invoke(readableTexture);
                }
                else
                {
                    return false;
                }
            }
            finally
            {
                if (needRestore)
                {
                    // 重新获取 importer 确保使用最新引用
                    var restoreImporter = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (restoreImporter != null && restoreImporter.isReadable != originalIsReadable)
                    {
                        restoreImporter.isReadable = originalIsReadable;
                        restoreImporter.SaveAndReimport();
                    }
                    
                    // 清除待恢复标记
                    EditorPrefs.DeleteKey(PREF_PENDING_RESTORE_PATH);
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// 简化版本 - 直接执行无参操作
        /// </summary>
        public static bool Execute(Texture2D texture, Action action)
        {
            return Execute(texture, _ => action?.Invoke());
        }
    }
}
