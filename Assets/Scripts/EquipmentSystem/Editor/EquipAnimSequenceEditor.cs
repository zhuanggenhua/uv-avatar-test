using UnityEngine;
using UnityEditor;
using EquipmentSystem.Data;
using System.Collections.Generic;

namespace EquipmentSystem.Editor
{
    /// <summary>
    /// EquipAnimSetAsset 的自定义 Inspector
    /// 提供快捷操作：添加动画、从 Spritesheet 填充等
    /// </summary>
    [CustomEditor(typeof(EquipAnimSetAsset))]
    public class EquipAnimSetEditor : UnityEditor.Editor
    {
        EquipAnimSetAsset _asset;
        
        // 快捷填充
        Texture2D _sourceTexture;
        AnimationTypeItem _selectedAnimType;
        AnimationTypeDatabase _animDatabase;
        int _framesPerRow = 8;
        int _rowCount = 4;
        
        // 折叠状态
        Dictionary<int, bool> _animFoldouts = new Dictionary<int, bool>();
        
        void OnEnable()
        {
            _asset = (EquipAnimSetAsset)target;
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            // 基础信息
            EditorGUILayout.LabelField("基础信息", EditorStyles.boldLabel);
            
            var setIdProp = serializedObject.FindProperty("setId");
            var descProp = serializedObject.FindProperty("description");
            
            if (setIdProp != null)
                EditorGUILayout.PropertyField(setIdProp, new GUIContent("动画集 ID"));
            if (descProp != null)
                EditorGUILayout.PropertyField(descProp, new GUIContent("描述"));
            
            EditorGUILayout.Space(10);
            
            // 快捷添加动画
            DrawQuickAddSection();
            
            EditorGUILayout.Space(10);
            
            // 动画列表
            EditorGUILayout.LabelField($"动画列表 ({_asset.animations.Count} 个)", EditorStyles.boldLabel);
            DrawAnimationsList();
            
            serializedObject.ApplyModifiedProperties();
        }
        
        void DrawQuickAddSection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("快捷添加动画", EditorStyles.boldLabel);
            
            _sourceTexture = (Texture2D)EditorGUILayout.ObjectField("Spritesheet", _sourceTexture, typeof(Texture2D), false);
            
            // 动画类型数据库
            _animDatabase = (AnimationTypeDatabase)EditorGUILayout.ObjectField(
                "动画数据库", _animDatabase, typeof(AnimationTypeDatabase), false);
            
            // 动画类型选择
            _selectedAnimType = (AnimationTypeItem)EditorGUILayout.ObjectField(
                "动画类型", _selectedAnimType, typeof(AnimationTypeItem), false);
            
            EditorGUILayout.BeginHorizontal();
            _framesPerRow = EditorGUILayout.IntField("每行帧数", _framesPerRow);
            _rowCount = EditorGUILayout.IntField("行数", _rowCount);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("添加 4 向动画"))
            {
                AddAnimationFromSpritesheet(4);
            }
            if (GUILayout.Button("添加单向动画 (SE)"))
            {
                AddAnimationFromSpritesheet(1);
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("添加空动画"))
            {
                AddEmptyAnimation();
            }
            if (GUILayout.Button("常用模板"))
            {
                ShowTemplateMenu();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.HelpBox(
                "4 向：行 0=SE, 1=SW, 2=NE, 3=NW\n" +
                "常用模板：一次性添加 Idle/Walk/Attack/Die 等空动画",
                MessageType.Info);
            
            EditorGUILayout.EndVertical();
        }
        
        void DrawAnimationsList()
        {
            var animsProp = serializedObject.FindProperty("animations");
            
            for (int i = 0; i < animsProp.arraySize; i++)
            {
                var animProp = animsProp.GetArrayElementAtIndex(i);
                var animTypeProp = animProp.FindPropertyRelative("animationType");
                var stripsProp = animProp.FindPropertyRelative("strips");
                
                // 折叠状态
                if (!_animFoldouts.ContainsKey(i))
                    _animFoldouts[i] = false;
                
                EditorGUILayout.BeginVertical("helpbox");
                
                // 标题行
                EditorGUILayout.BeginHorizontal();
                _animFoldouts[i] = EditorGUILayout.Foldout(_animFoldouts[i], "", true);
                
                // 动画类型选择（优先）
                EditorGUILayout.PropertyField(animTypeProp, GUIContent.none, GUILayout.Width(120));
                
                // 状态显示
                int stripCount = stripsProp.arraySize;
                int totalFrames = 0;
                for (int j = 0; j < stripCount; j++)
                {
                    var framesProp = stripsProp.GetArrayElementAtIndex(j).FindPropertyRelative("frames");
                    totalFrames += framesProp.arraySize;
                }
                GUILayout.Label($"{stripCount}向 / {totalFrames}帧", EditorStyles.miniLabel, GUILayout.Width(80));
                
                // 删除按钮
                string displayName = animTypeProp.objectReferenceValue != null 
                    ? (animTypeProp.objectReferenceValue as AnimationTypeItem)?.name 
                    : "(未设置)";
                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    if (EditorUtility.DisplayDialog("确认", $"确定删除动画 [{displayName}]？", "确定", "取消"))
                    {
                        animsProp.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }
                EditorGUILayout.EndHorizontal();
                
                // 展开时显示 strips
                if (_animFoldouts[i])
                {
                    EditorGUI.indentLevel++;
                    DrawStripsForAnimation(stripsProp);
                    EditorGUI.indentLevel--;
                }
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
            
            if (animsProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox("暂无动画，请使用上方工具添加", MessageType.Info);
            }
        }
        
        void DrawStripsForAnimation(SerializedProperty stripsProp)
        {
            for (int j = 0; j < stripsProp.arraySize; j++)
            {
                var stripProp = stripsProp.GetArrayElementAtIndex(j);
                var facingProp = stripProp.FindPropertyRelative("facing");
                var framesProp = stripProp.FindPropertyRelative("frames");
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(facingProp, GUIContent.none, GUILayout.Width(80));
                GUILayout.Label($"({framesProp.arraySize} 帧)", GUILayout.Width(50));
                
                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    stripsProp.DeleteArrayElementAtIndex(j);
                    break;
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(framesProp, new GUIContent("帧序列"), true);
                EditorGUI.indentLevel--;
            }
            
            // 添加 strip 按钮
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ 添加方向", GUILayout.Width(80)))
            {
                stripsProp.arraySize++;
            }
            EditorGUILayout.EndHorizontal();
        }
        
        void AddAnimationFromSpritesheet(int dirCount)
        {
            if (_selectedAnimType == null)
            {
                EditorUtility.DisplayDialog("错误", "请先选择动画类型", "确定");
                return;
            }
            
            // 检查是否已存在
            if (_asset.HasAnimation(_selectedAnimType))
            {
                EditorUtility.DisplayDialog("错误", $"动画 [{_selectedAnimType.name}] 已存在", "确定");
                return;
            }
            
            Undo.RecordObject(_asset, "Add Animation");
            
            var anim = new AnimSequenceEntry
            {
                animationType = _selectedAnimType,
                strips = new List<DirectionalStrip>()
            };
            
            // 如果有 Spritesheet，从中填充
            if (_sourceTexture != null)
            {
                var sprites = GetSpritesFromTexture(_sourceTexture);
                if (sprites.Count > 0)
                {
                    FillStripsFromSprites(anim, sprites, dirCount);
                }
            }
            else
            {
                // 添加空 strips
                var facings = dirCount == 4 
                    ? new[] { CharacterFacing.SouthEast, CharacterFacing.SouthWest, CharacterFacing.NorthEast, CharacterFacing.NorthWest }
                    : new[] { CharacterFacing.SouthEast };
                
                foreach (var facing in facings)
                {
                    anim.strips.Add(new DirectionalStrip { facing = facing });
                }
            }
            
            _asset.animations.Add(anim);
            EditorUtility.SetDirty(_asset);
            
            Debug.Log($"[EquipAnimSet] 已添加动画: {_selectedAnimType.name}");
        }
        
        void AddEmptyAnimation()
        {
            if (_selectedAnimType == null)
            {
                EditorUtility.DisplayDialog("错误", "请先选择动画类型", "确定");
                return;
            }
            
            if (_asset.HasAnimation(_selectedAnimType))
            {
                EditorUtility.DisplayDialog("错误", $"动画 [{_selectedAnimType.name}] 已存在", "确定");
                return;
            }
            
            Undo.RecordObject(_asset, "Add Empty Animation");
            
            _asset.animations.Add(new AnimSequenceEntry
            {
                animationType = _selectedAnimType,
                strips = new List<DirectionalStrip>()
            });
            
            EditorUtility.SetDirty(_asset);
        }
        
        void ShowTemplateMenu()
        {
            var menu = new GenericMenu();
            
            // 从数据库获取动画类型
            if (_animDatabase != null && _animDatabase.Count > 0)
            {
                var allTypes = _animDatabase.ItemsReadOnly;
                menu.AddItem(new GUIContent("添加所有动画类型"), false, () => ApplyTemplateFromDatabase());
                menu.AddSeparator("");
                
                foreach (var type in allTypes)
                {
                    if (type != null)
                    {
                        var t = type; // 闭包捕获
                        menu.AddItem(new GUIContent($"添加: {type.name}"), false, () => AddSingleAnimationType(t));
                    }
                }
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("请先指定动画数据库"));
            }
            
            menu.ShowAsContext();
        }
        
        void ApplyTemplateFromDatabase()
        {
            if (_animDatabase == null) return;
            
            Undo.RecordObject(_asset, "Apply Template");
            
            int added = 0;
            foreach (var type in _animDatabase.ItemsReadOnly)
            {
                if (type != null && !_asset.HasAnimation(type))
                {
                    _asset.animations.Add(new AnimSequenceEntry
                    {
                        animationType = type,
                        strips = new List<DirectionalStrip>()
                    });
                    added++;
                }
            }
            
            EditorUtility.SetDirty(_asset);
            Debug.Log($"[EquipAnimSet] 已添加 {added} 个空动画");
        }
        
        void AddSingleAnimationType(AnimationTypeItem type)
        {
            if (_asset.HasAnimation(type))
            {
                EditorUtility.DisplayDialog("错误", $"动画 [{type.name}] 已存在", "确定");
                return;
            }
            
            Undo.RecordObject(_asset, "Add Animation Type");
            
            _asset.animations.Add(new AnimSequenceEntry
            {
                animationType = type,
                strips = new List<DirectionalStrip>()
            });
            
            EditorUtility.SetDirty(_asset);
        }
        
        List<Sprite> GetSpritesFromTexture(Texture2D tex)
        {
            string path = AssetDatabase.GetAssetPath(tex);
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            var sprites = new List<Sprite>();
            
            foreach (var asset in allAssets)
            {
                if (asset is Sprite sprite)
                    sprites.Add(sprite);
            }
            
            // 按位置排序
            sprites.Sort((a, b) =>
            {
                int rowA = Mathf.FloorToInt((tex.height - a.rect.y - a.rect.height) / a.rect.height);
                int rowB = Mathf.FloorToInt((tex.height - b.rect.y - b.rect.height) / b.rect.height);
                if (rowA != rowB) return rowA.CompareTo(rowB);
                return a.rect.x.CompareTo(b.rect.x);
            });
            
            return sprites;
        }
        
        void FillStripsFromSprites(AnimSequenceEntry anim, List<Sprite> sprites, int dirCount)
        {
            var facings = dirCount == 4
                ? new[] { CharacterFacing.SouthEast, CharacterFacing.SouthWest, CharacterFacing.NorthEast, CharacterFacing.NorthWest }
                : new[] { CharacterFacing.SouthEast };
            
            int spritesPerRow = sprites.Count / Mathf.Min(_rowCount, dirCount);
            if (spritesPerRow <= 0) spritesPerRow = sprites.Count;
            
            for (int row = 0; row < Mathf.Min(_rowCount, dirCount); row++)
            {
                var strip = new DirectionalStrip { facing = facings[row] };
                int frameCount = Mathf.Min(_framesPerRow, spritesPerRow);
                
                for (int col = 0; col < frameCount; col++)
                {
                    int idx = row * spritesPerRow + col;
                    if (idx < sprites.Count)
                        strip.frames.Add(sprites[idx]);
                }
                
                anim.strips.Add(strip);
            }
        }
    }
}
