using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace EquipmentSystem.Editor
{
    /// <summary>
    /// 装备动画序列的公用工具方法
    /// </summary>
    public static class EquipmentAnimSequenceTools
    {
        /// <summary>
        /// 从 Texture2D 获取所有子 Sprite，并按行列顺序排序
        /// </summary>
        public static List<Sprite> GetSpritesFromTexture(Texture2D tex)
        {
            var sprites = new List<Sprite>();
            if (tex == null)
                return sprites;

            string path = AssetDatabase.GetAssetPath(tex);
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);

            foreach (var asset in allAssets)
            {
                if (asset is Sprite sprite)
                    sprites.Add(sprite);
            }

            // 按行列排序：先按行（从上到下），再按列（从左到右）
            sprites.Sort((a, b) =>
            {
                int rowA = Mathf.FloorToInt((tex.height - a.rect.y - a.rect.height) / a.rect.height);
                int rowB = Mathf.FloorToInt((tex.height - b.rect.y - b.rect.height) / b.rect.height);
                if (rowA != rowB) return rowA.CompareTo(rowB);
                return a.rect.x.CompareTo(b.rect.x);
            });

            return sprites;
        }

        /// <summary>
        /// 分析 Sprite 列表的行列布局
        /// </summary>
        public static void AnalyzeSpriteLayout(List<Sprite> sprites, Texture2D tex, out int rowCount, out int framesPerRow)
        {
            rowCount = 0;
            framesPerRow = 0;

            if (sprites == null || sprites.Count == 0 || tex == null)
                return;

            // 统计每一行的帧数
            var rowToCount = new Dictionary<int, int>();
            foreach (var sprite in sprites)
            {
                int row = Mathf.FloorToInt((tex.height - sprite.rect.y - sprite.rect.height) / sprite.rect.height);
                if (!rowToCount.ContainsKey(row))
                    rowToCount[row] = 0;
                rowToCount[row]++;
            }

            rowCount = rowToCount.Count;
            if (rowCount > 0)
            {
                // 采用每行的最大帧数，避免最后一行不满导致平均数偏小
                int maxPerRow = 0;
                foreach (var kv in rowToCount)
                    maxPerRow = Mathf.Max(maxPerRow, kv.Value);
                framesPerRow = maxPerRow;
            }
        }

        /// <summary>
        /// 根据 Sprite 列表填充动画条目的方向帧条
        /// </summary>
        /// <param name="anim">目标动画条目</param>
        /// <param name="sprites">已排序的 Sprite 列表</param>
        /// <param name="tex">原始贴图（用于计算布局）</param>
        /// <param name="dirCount">方向数（1=单向SE，4=四向）</param>
        /// <param name="manualRowCount">手动行数（0 表示自动）</param>
        /// <param name="manualFramesPerRow">手动每行帧数（0 表示自动）</param>
        public static void FillStripsFromSprites(AnimSequenceEntry anim, List<Sprite> sprites, Texture2D tex, int dirCount, int manualRowCount = 0, int manualFramesPerRow = 0)
        {
            if (anim == null || sprites == null || sprites.Count == 0)
                return;

            // 单向模式 + 未指定手动布局：将所有帧平铺到一个方向 strip
            if (dirCount == 1 && manualRowCount <= 0 && manualFramesPerRow <= 0)
            {
                anim.strips.Clear();

                var strip = new DirectionalStrip { facing = CharacterFacing.SouthEast };
                foreach (var s in sprites)
                {
                    if (s != null)
                        strip.frames.Add(s);
                }

                anim.strips.Add(strip);
                return;
            }

            // 先根据 rect.y 计算“从上到下”的行索引：
            //  1. 收集所有不同的 y（底部像素坐标），按照从大到小排序 -> 上行在前
            //  2. 为每个 y 分配一个 rowIndex（0 = 顶行，1 = 第二行 ...）
            var uniqueY = new List<int>();
            foreach (var s in sprites)
            {
                if (s == null) continue;
                int y = Mathf.RoundToInt(s.rect.y);
                if (!uniqueY.Contains(y))
                    uniqueY.Add(y);
            }

            if (uniqueY.Count == 0)
                return;

            uniqueY.Sort();          // 从下到上
            uniqueY.Reverse();       // 从上到下

            var yToRowIndex = new Dictionary<int, int>();
            for (int i = 0; i < uniqueY.Count; i++)
                yToRowIndex[uniqueY[i]] = i;   // 行号：0=最上面一行

            // 构建“按行索引分组”的字典
            var rowDict = new SortedDictionary<int, List<Sprite>>();
            foreach (var s in sprites)
            {
                if (s == null) continue;
                int y = Mathf.RoundToInt(s.rect.y);
                if (!yToRowIndex.TryGetValue(y, out int mappedRow))
                    continue;

                if (!rowDict.TryGetValue(mappedRow, out var list))
                {
                    list = new List<Sprite>();
                    rowDict[mappedRow] = list;
                }
                list.Add(s);
            }

            if (rowDict.Count == 0)
                return;

            // 方向映射：按行顺序（从上到下）依次映射到 SE, SW, NE, NW
            CharacterFacing[] facings = dirCount == 4
                ? new[] { CharacterFacing.SouthEast, CharacterFacing.SouthWest, CharacterFacing.NorthEast, CharacterFacing.NorthWest }
                : new[] { CharacterFacing.SouthEast };

            // 允许手动限制“使用前多少行”
            int availableRows = rowDict.Count;      // 实际存在的行数
            int useRows = manualRowCount > 0 ? Mathf.Min(manualRowCount, availableRows) : availableRows;
            useRows = Mathf.Min(useRows, dirCount, facings.Length);

            anim.strips.Clear();

            int rowIndex = 0;
            foreach (var kv in rowDict)
            {
                if (rowIndex >= useRows)
                    break;

                var strip = new DirectionalStrip { facing = facings[rowIndex] };
                var rowSprites = kv.Value;

                // 确保按 x 坐标排序（保险起见）
                rowSprites.Sort((a, b) => a.rect.x.CompareTo(b.rect.x));

                // 默认使用整行；若设置了手动“每行帧数”，则进行裁剪
                int count = rowSprites.Count;
                if (manualFramesPerRow > 0)
                    count = Mathf.Min(count, manualFramesPerRow);

                for (int i = 0; i < count; i++)
                {
                    if (rowSprites[i] != null)
                        strip.frames.Add(rowSprites[i]);
                }

                anim.strips.Add(strip);
                rowIndex++;
            }
        }

        /// <summary>
        /// 为装备添加动画条目（从 Spritesheet 生成）
        /// </summary>
        /// <param name="data">目标装备数据</param>
        /// <param name="animType">动画类型</param>
        /// <param name="spritesheet">Spritesheet 贴图</param>
        /// <param name="dirCount">方向数</param>
        /// <param name="overwrite">是否覆盖已有动画</param>
        /// <returns>是否成功</returns>
        public static bool AddAnimationFromSpritesheet(
            EquipmentRenderData data,
            AnimationTypeItem animType,
            Texture2D spritesheet,
            int dirCount,
            bool overwrite,
            int manualRowCount = 0,
            int manualFramesPerRow = 0)
        {
            if (data == null || animType == null)
                return false;

            if (data.animSequences == null)
                data.animSequences = new List<AnimSequenceEntry>();

            // 检查是否已存在
            int existingIndex = data.animSequences.FindIndex(a => a != null && a.animationType == animType);
            if (existingIndex >= 0 && !overwrite)
                return false;

            // 获取 sprites
            var sprites = GetSpritesFromTexture(spritesheet);
            if (sprites.Count == 0 && spritesheet != null)
            {
                Debug.LogWarning($"Spritesheet [{spritesheet.name}] 没有子 Sprite，请确认已切片");
                return false;
            }

            Undo.RecordObject(data, "Add/Update Animation");

            AnimSequenceEntry anim;
            if (existingIndex >= 0)
            {
                // 覆盖已有
                anim = data.animSequences[existingIndex];
                anim.strips.Clear();
            }
            else
            {
                // 新建
                anim = new AnimSequenceEntry
                {
                    animationType = animType,
                    strips = new List<DirectionalStrip>()
                };
                data.animSequences.Add(anim);
            }

            // 填充帧
            if (sprites.Count > 0)
                FillStripsFromSprites(anim, sprites, spritesheet, dirCount, manualRowCount, manualFramesPerRow);
            else
            {
                // 无 spritesheet，创建空 strips
                CharacterFacing[] facings = dirCount == 4
                    ? new[] { CharacterFacing.SouthEast, CharacterFacing.SouthWest, CharacterFacing.NorthEast, CharacterFacing.NorthWest }
                    : new[] { CharacterFacing.SouthEast };

                foreach (var facing in facings)
                    anim.strips.Add(new DirectionalStrip { facing = facing });
            }

            EditorUtility.SetDirty(data);
            return true;
        }

        /// <summary>
        /// 添加空动画条目
        /// </summary>
        public static bool AddEmptyAnimation(EquipmentRenderData data, AnimationTypeItem animType)
        {
            if (data == null || animType == null)
                return false;

            if (data.animSequences == null)
                data.animSequences = new List<AnimSequenceEntry>();

            if (data.animSequences.Exists(a => a != null && a.animationType == animType))
                return false;

            Undo.RecordObject(data, "Add Empty Animation");

            data.animSequences.Add(new AnimSequenceEntry
            {
                animationType = animType,
                strips = new List<DirectionalStrip>()
            });

            EditorUtility.SetDirty(data);
            return true;
        }
    }
}
