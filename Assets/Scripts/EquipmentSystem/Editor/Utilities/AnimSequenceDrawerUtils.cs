using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace EquipmentSystem.Editor
{
    /// <summary>
    /// 序列帧网格绘制工具类
    /// 供 EquipmentAnimSequenceEditor、AnimSequenceEntryDrawer、EquipmentDataEditor 共享使用
    /// </summary>
    public static class AnimSequenceDrawerUtils
    {
        #region 常量配置

        /// <summary>缩略图尺寸</summary>
        public const float THUMB_SIZE = 48f;
        /// <summary>缩略图间距</summary>
        public const float THUMB_SPACING = 2f;
        /// <summary>方向标签宽度</summary>
        public const float FACING_LABEL_WIDTH = 30f;
        /// <summary>添加按钮宽度</summary>
        public const float ADD_BUTTON_WIDTH = 24f;
        /// <summary>单行高度（含间距）</summary>
        public const float ROW_HEIGHT = THUMB_SIZE + THUMB_SPACING;

        /// <summary>四个方向</summary>
        public static readonly CharacterFacing[] Facings = {
            CharacterFacing.SouthEast,
            CharacterFacing.SouthWest,
            CharacterFacing.NorthEast,
            CharacterFacing.NorthWest
        };

        /// <summary>方向标签</summary>
        public static readonly string[] FacingNames = { "SE", "SW", "NE", "NW" };

        #endregion

        #region Sprite 缩略图绘制

        /// <summary>
        /// 绘制 Sprite 缩略图
        /// </summary>
        /// <param name="rect">绘制区域</param>
        /// <param name="sprite">Sprite 对象</param>
        /// <param name="depth">深度模式</param>
        /// <param name="isSelected">是否选中</param>
        public static void DrawSpriteThumbnail(Rect rect, Sprite sprite, FrameDepthMode depth, bool isSelected = false)
        {
            // 1. 背景
            var bgColor = isSelected
                ? new Color(0.3f, 0.5f, 0.8f, 1f)
                : new Color(0.2f, 0.2f, 0.2f, 1f);
            EditorGUI.DrawRect(rect, bgColor);

            // 2. 边框
            var borderColor = isSelected
                ? new Color(0.4f, 0.6f, 0.9f, 1f)
                : new Color(0.3f, 0.3f, 0.3f, 1f);
            DrawRectBorder(rect, borderColor);

            // 3. Sprite 预览
            if (sprite != null && sprite.texture != null)
            {
                var uvRect = GetSpriteUVRect(sprite);
                var innerRect = new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4);
                GUI.DrawTextureWithTexCoords(innerRect, sprite.texture, uvRect);
            }
            else
            {
                // 空帧占位符
                var labelStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    fontSize = 10
                };
                GUI.Label(rect, "空", labelStyle);
            }

            // 4. 深度模式标识
            if (depth == FrameDepthMode.Back)
            {
                var iconRect = new Rect(rect.xMax - 14, rect.yMax - 14, 12, 12);
                var iconStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = Color.yellow },
                    fontSize = 10
                };
                GUI.Label(iconRect, "B", iconStyle);
            }
        }

        /// <summary>
        /// 绘制空的占位缩略图（用于添加新帧）
        /// </summary>
        public static void DrawEmptyThumbnail(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f, 1f));
            DrawRectBorder(rect, new Color(0.25f, 0.25f, 0.25f, 1f), true);

            var labelStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                fontSize = 16
            };
            GUI.Label(rect, "+", labelStyle);
        }

        /// <summary>
        /// 绘制矩形边框
        /// </summary>
        static void DrawRectBorder(Rect rect, Color color, bool dashed = false)
        {
            // 上边
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), color);
            // 下边
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), color);
            // 左边
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), color);
            // 右边
            EditorGUI.DrawRect(new Rect(rect.xMax - 1, rect.y, 1, rect.height), color);
        }

        /// <summary>
        /// 获取 Sprite 在纹理中的 UV 矩形
        /// </summary>
        public static Rect GetSpriteUVRect(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
                return new Rect(0, 0, 1, 1);

            var tex = sprite.texture;
            var rect = sprite.textureRect;
            return new Rect(
                rect.x / tex.width,
                rect.y / tex.height,
                rect.width / tex.width,
                rect.height / tex.height
            );
        }

        #endregion

        /// <summary>每行最大帧数（用于自动换行）</summary>
        public const int FRAMES_PER_ROW = 10;

        #region 序列帧网格

        /// <summary>
        /// 绘制序列帧网格（GUILayout 版本，用于 EditorWindow）
        /// 每个方向按需多行显示，每行最多 6 帧
        /// </summary>
        /// <param name="entry">动画序列条目</param>
        /// <param name="serializedEntry">序列化属性（用于修改数据）</param>
        /// <param name="framesPerRow">每行帧数（默认 6）</param>
        /// <returns>是否有数据变更</returns>
        public static bool DrawDirectionalGridLayout(
            AnimSequenceEntry entry,
            SerializedProperty serializedEntry,
            int framesPerRow = FRAMES_PER_ROW)
        {
            bool changed = false;

            for (int dir = 0; dir < 4; dir++)
            {
                var strip = entry?.GetStrip(Facings[dir]);
                var stripProp = FindStripProperty(serializedEntry, Facings[dir]);

                int frameCount = strip?.frames?.Count ?? 0;
                int rowCount = Mathf.Max(1, Mathf.CeilToInt((frameCount + 1) / (float)framesPerRow)); // +1 为添加槽位

                int frameIndex = 0;
                for (int row = 0; row < rowCount; row++)
                {
                    EditorGUILayout.BeginHorizontal();

                    // 方向标签（只在第一行显示）
                    if (row == 0)
                        EditorGUILayout.LabelField(FacingNames[dir], GUILayout.Width(FACING_LABEL_WIDTH));
                    else
                        GUILayout.Space(FACING_LABEL_WIDTH + 4); // 缩进对齐

                    // 绘制该行的帧
                    for (int col = 0; col < framesPerRow && frameIndex < frameCount; col++, frameIndex++)
                    {
                        var thumbRect = GUILayoutUtility.GetRect(THUMB_SIZE, THUMB_SIZE);
                        var depth = frameIndex < strip.depthModes.Count ? strip.depthModes[frameIndex] : FrameDepthMode.Front;

                        DrawSpriteThumbnail(thumbRect, strip.frames[frameIndex], depth);

                        // 处理帧点击
                        if (Event.current.type == EventType.MouseDown &&
                            thumbRect.Contains(Event.current.mousePosition))
                        {
                            if (Event.current.button == 0)
                            {
                                if (stripProp != null)
                                {
                                    ToggleFrameDepth(stripProp, frameIndex);
                                    changed = true;
                                }
                            }
                            else if (Event.current.button == 1)
                            {
                                if (stripProp != null)
                                {
                                    ShowFrameContextMenu(stripProp, frameIndex);
                                }
                            }
                            Event.current.Use();
                        }

                        // 处理拖拽替换
                        if (stripProp != null)
                        {
                            int idx = frameIndex; // 避免闭包
                            changed |= HandleFrameDragAndDrop(thumbRect, stripProp, idx, false);
                        }
                    }

                    // 最后一行添加槽位
                    if (row == rowCount - 1)
                    {
                        var addRect = GUILayoutUtility.GetRect(THUMB_SIZE, THUMB_SIZE);
                        DrawEmptyThumbnail(addRect);

                        if (stripProp != null || serializedEntry != null)
                        {
                            var targetStripProp = stripProp ?? CreateStripIfNeeded(serializedEntry, Facings[dir]);
                            if (targetStripProp != null)
                            {
                                changed |= HandleFrameDragAndDrop(addRect, targetStripProp, -1, true);
                            }
                        }
                    }

                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                    GUILayout.Space(THUMB_SPACING);
                }
            }

            return changed;
        }

        /// <summary>
        /// 绘制四向序列帧网格（Rect 版本，用于 PropertyDrawer）
        /// 固定 4 行（每方向一行），超出的帧显示省略号
        /// </summary>
        /// <param name="position">绘制区域</param>
        /// <param name="entry">动画序列条目</param>
        /// <param name="serializedEntry">序列化属性</param>
        /// <param name="maxFramesPerRow">每行最大帧数显示（0 = 不限制）</param>
        /// <returns>是否有数据变更</returns>
        public static bool DrawDirectionalGridRect(
            Rect position,
            AnimSequenceEntry entry,
            SerializedProperty serializedEntry,
            int maxFramesPerRow = FRAMES_PER_ROW)
        {
            bool changed = false;
            float y = position.y;

            for (int row = 0; row < 4; row++)
            {
                float x = position.x;

                // 方向标签
                var labelRect = new Rect(x, y, FACING_LABEL_WIDTH, THUMB_SIZE);
                EditorGUI.LabelField(labelRect, FacingNames[row]);
                x += FACING_LABEL_WIDTH + THUMB_SPACING;

                // 获取该方向的 strip
                var strip = entry?.GetStrip(Facings[row]);
                var stripProp = FindStripProperty(serializedEntry, Facings[row]);

                if (strip != null && strip.frames != null)
                {
                    int frameCount = strip.frames.Count;
                    int displayCount = maxFramesPerRow > 0 ? Mathf.Min(frameCount, maxFramesPerRow) : frameCount;

                    for (int i = 0; i < displayCount; i++)
                    {
                        var thumbRect = new Rect(x, y, THUMB_SIZE, THUMB_SIZE);
                        var depth = i < strip.depthModes.Count ? strip.depthModes[i] : FrameDepthMode.Front;

                        DrawSpriteThumbnail(thumbRect, strip.frames[i], depth);

                        // 处理帧点击
                        if (Event.current.type == EventType.MouseDown &&
                            thumbRect.Contains(Event.current.mousePosition))
                        {
                            if (Event.current.button == 0)
                            {
                                if (stripProp != null)
                                {
                                    ToggleFrameDepth(stripProp, i);
                                    changed = true;
                                }
                            }
                            else if (Event.current.button == 1)
                            {
                                if (stripProp != null)
                                {
                                    ShowFrameContextMenu(stripProp, i);
                                }
                            }
                            Event.current.Use();
                        }

                        // 处理拖拽替换
                        if (stripProp != null)
                        {
                            changed |= HandleFrameDragAndDrop(thumbRect, stripProp, i, false);
                        }

                        x += THUMB_SIZE + THUMB_SPACING;
                    }

                    // 如果有更多帧，显示省略号
                    if (maxFramesPerRow > 0 && frameCount > maxFramesPerRow)
                    {
                        var moreRect = new Rect(x, y + THUMB_SIZE / 2 - 8, 30, 16);
                        EditorGUI.LabelField(moreRect, $"+{frameCount - maxFramesPerRow}", EditorStyles.miniLabel);
                        x += 32;
                    }
                }

                // 添加帧的拖放区域
                var addRect = new Rect(x, y, THUMB_SIZE, THUMB_SIZE);
                DrawEmptyThumbnail(addRect);

                if (stripProp != null || serializedEntry != null)
                {
                    var targetStripProp = stripProp ?? CreateStripIfNeeded(serializedEntry, Facings[row]);
                    if (targetStripProp != null)
                    {
                        changed |= HandleFrameDragAndDrop(addRect, targetStripProp, -1, true);
                    }
                }

                y += ROW_HEIGHT;
            }

            return changed;
        }

        /// <summary>
        /// 计算四向网格的高度
        /// </summary>
        public static float GetGridHeight()
        {
            return ROW_HEIGHT * 4;
        }

        #endregion

        #region 拖拽处理

        /// <summary>
        /// 处理帧的拖拽操作
        /// </summary>
        /// <param name="dropRect">拖放区域</param>
        /// <param name="stripProp">strip 的序列化属性</param>
        /// <param name="frameIndex">目标帧索引（-1 表示添加到末尾）</param>
        /// <param name="isAddSlot">是否是添加槽位</param>
        /// <returns>是否有数据变更</returns>
        public static bool HandleFrameDragAndDrop(
            Rect dropRect,
            SerializedProperty stripProp,
            int frameIndex,
            bool isAddSlot)
        {
            var evt = Event.current;

            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)
                return false;

            if (!dropRect.Contains(evt.mousePosition))
                return false;

            // 检查是否有 Sprite
            var sprites = DragAndDrop.objectReferences
                .OfType<Sprite>()
                .OrderBy(s => s.name)
                .ToList();

            if (sprites.Count == 0)
                return false;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();

                var framesProp = stripProp.FindPropertyRelative("frames");
                var depthsProp = stripProp.FindPropertyRelative("depthModes");

                if (isAddSlot)
                {
                    // 添加到末尾
                    foreach (var sprite in sprites)
                    {
                        int newIndex = framesProp.arraySize;
                        framesProp.arraySize++;
                        framesProp.GetArrayElementAtIndex(newIndex).objectReferenceValue = sprite;

                        // 同步 depthModes
                        depthsProp.arraySize = framesProp.arraySize;
                        depthsProp.GetArrayElementAtIndex(newIndex).enumValueIndex = (int)FrameDepthMode.Front;
                    }
                }
                else if (frameIndex >= 0 && frameIndex < framesProp.arraySize)
                {
                    // 替换指定帧
                    framesProp.GetArrayElementAtIndex(frameIndex).objectReferenceValue = sprites[0];
                }

                stripProp.serializedObject.ApplyModifiedProperties();
                evt.Use();
                return true;
            }

            evt.Use();
            return false;
        }

        #endregion

        #region 帧操作

        /// <summary>
        /// 切换帧的深度模式
        /// </summary>
        static void ToggleFrameDepth(SerializedProperty stripProp, int frameIndex)
        {
            var depthsProp = stripProp.FindPropertyRelative("depthModes");
            var framesProp = stripProp.FindPropertyRelative("frames");

            // 确保 depthModes 长度与 frames 一致
            while (depthsProp.arraySize < framesProp.arraySize)
            {
                depthsProp.arraySize++;
                depthsProp.GetArrayElementAtIndex(depthsProp.arraySize - 1).enumValueIndex = (int)FrameDepthMode.Front;
            }

            if (frameIndex < depthsProp.arraySize)
            {
                var depthProp = depthsProp.GetArrayElementAtIndex(frameIndex);
                depthProp.enumValueIndex = depthProp.enumValueIndex == 0 ? 1 : 0;
                stripProp.serializedObject.ApplyModifiedProperties();
            }
        }

        /// <summary>
        /// 显示帧的右键菜单
        /// </summary>
        static void ShowFrameContextMenu(SerializedProperty stripProp, int frameIndex)
        {
            var menu = new GenericMenu();

            // 获取当前帧的 Sprite
            var framesProp = stripProp.FindPropertyRelative("frames");
            Sprite sprite = null;
            if (framesProp != null && frameIndex >= 0 && frameIndex < framesProp.arraySize)
            {
                sprite = framesProp.GetArrayElementAtIndex(frameIndex).objectReferenceValue as Sprite;
            }

            // 定位资源
            if (sprite != null)
            {
                menu.AddItem(new GUIContent("定位资源"), false, () =>
                {
                    EditorGUIUtility.PingObject(sprite);
                    Selection.activeObject = sprite;
                });
                menu.AddSeparator("");
            }

            // 切换深度
            menu.AddItem(new GUIContent("切换深度 (前/后)"), false, () =>
            {
                ToggleFrameDepth(stripProp, frameIndex);
            });

            menu.AddSeparator("");

            // 删除帧
            menu.AddItem(new GUIContent("删除此帧"), false, () =>
            {
                DeleteFrame(stripProp, frameIndex);
            });

            menu.ShowAsContext();
        }

        /// <summary>
        /// 删除帧
        /// </summary>
        static void DeleteFrame(SerializedProperty stripProp, int frameIndex)
        {
            var framesProp = stripProp.FindPropertyRelative("frames");
            var depthsProp = stripProp.FindPropertyRelative("depthModes");

            if (frameIndex >= 0 && frameIndex < framesProp.arraySize)
            {
                // 先将引用设为 null，再删除元素
                framesProp.GetArrayElementAtIndex(frameIndex).objectReferenceValue = null;
                framesProp.DeleteArrayElementAtIndex(frameIndex);

                if (frameIndex < depthsProp.arraySize)
                {
                    depthsProp.DeleteArrayElementAtIndex(frameIndex);
                }

                stripProp.serializedObject.ApplyModifiedProperties();
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 查找指定方向的 strip 属性
        /// </summary>
        public static SerializedProperty FindStripProperty(SerializedProperty entryProp, CharacterFacing facing)
        {
            if (entryProp == null)
                return null;

            var stripsProp = entryProp.FindPropertyRelative("strips");
            if (stripsProp == null)
                return null;

            for (int i = 0; i < stripsProp.arraySize; i++)
            {
                var stripProp = stripsProp.GetArrayElementAtIndex(i);
                var facingProp = stripProp.FindPropertyRelative("facing");
                if (facingProp != null && facingProp.enumValueIndex == (int)facing)
                {
                    return stripProp;
                }
            }

            return null;
        }

        /// <summary>
        /// 如果不存在则创建 strip
        /// </summary>
        public static SerializedProperty CreateStripIfNeeded(SerializedProperty entryProp, CharacterFacing facing)
        {
            if (entryProp == null)
                return null;

            var existing = FindStripProperty(entryProp, facing);
            if (existing != null)
                return existing;

            var stripsProp = entryProp.FindPropertyRelative("strips");
            if (stripsProp == null)
                return null;

            int newIndex = stripsProp.arraySize;
            stripsProp.arraySize++;

            var newStripProp = stripsProp.GetArrayElementAtIndex(newIndex);
            var facingProp = newStripProp.FindPropertyRelative("facing");
            facingProp.enumValueIndex = (int)facing;

            // 初始化空列表
            var framesProp = newStripProp.FindPropertyRelative("frames");
            framesProp.ClearArray();

            var depthsProp = newStripProp.FindPropertyRelative("depthModes");
            depthsProp.ClearArray();

            entryProp.serializedObject.ApplyModifiedProperties();

            return newStripProp;
        }

        #endregion
    }
}
