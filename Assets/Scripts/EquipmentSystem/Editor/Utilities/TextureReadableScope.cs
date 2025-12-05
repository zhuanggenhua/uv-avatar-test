using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EquipmentSystem.Editor
{
    /// <summary>
    /// 纹理临时可读管理器
    /// 
    /// 功能：
    /// 1. 临时修改纹理的导入设置，使其可读
    /// 2. 执行需要读取纹理的操作
    /// 3. 自动恢复原始导入设置
    /// 
    /// 使用场景：
    /// - 读取精灵表的像素颜色
    /// - 自动检测身体部位
    /// - 保存帧数据时获取颜色信息
    /// 
    /// 安全特性：
    /// - 支持异常处理，确保设置总是被恢复
    /// - 支持编辑器重启后的恢复机制
    /// - 避免纹理设置被永久修改
    /// </summary>
    public static class TextureReadableScope
    {
        const string PREF_PENDING_RESTORE_PATH = "TextureReadableScope_PendingRestorePath";
        
        /// <summary>
        /// 编辑器启动时的安全恢复机制
        /// 
        /// 功能：检查并恢复上次编辑器会话中未正确恢复的纹理设置
        /// 场景：编辑器崩溃或强制退出后，确保纹理设置不会被永久修改
        /// 
        /// 使用EditorPrefs存储需要恢复的纹理路径列表
        /// </summary>
        [InitializeOnLoadMethod]
        static void CheckAndRestoreTextureSettings()
        {
            RestorePendingTexture();
        }
        
        /// <summary>
        /// 记录当前正在处理的纹理路径
        /// 用于在编辑器异常退出时恢复设置
        /// </summary>
        static readonly HashSet<string> activeScopes = new HashSet<string>();
        
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
        /// 在临时可读作用域内执行操作
        /// 
        /// 执行流程：
        /// 1. 检查纹理是否已经可读，如果是则直接执行
        /// 2. 如果不可读，临时修改导入设置
        /// 3. 执行用户操作
        /// 4. 恢复原始设置（即使发生异常也会恢复）
        /// 
        /// 注意：这个操作可能会触发纹理重新导入，有一定性能开销
        /// </summary>
        /// <param name="texture">目标纹理</param>
        /// <param name="action">需要纹理可读的操作</param>
        /// <returns>是否成功执行（纹理为null时返回false）</returns>
        public static bool Execute(Texture2D texture, System.Action<Texture2D> action)
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
