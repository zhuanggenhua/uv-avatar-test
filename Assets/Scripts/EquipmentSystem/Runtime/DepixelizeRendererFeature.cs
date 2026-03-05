// ============================================================================
// Depixelize Renderer Feature
// ============================================================================
// 
// URP 2D 全屏后处理 Renderer Feature，实现 Kopf & Lischinski 2011 论文
// "Depixelizing Pixel Art" 的核心算法
// 适用于 URP 17+ (Unity 6)，使用 RenderGraph API
//
// 核心思想：
// 1. 构建相似性图，判断像素连接性
// 2. 解决对角线交叉歧义（Curves、Sparse Pixels、Islands 启发式）
// 3. 重塑像素单元，使连接的像素共享边
// 4. 拟合平滑曲线并优化
// ============================================================================
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace EquipmentSystem
{
    public class DepixelizeRendererFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class DepixelizeSettings
        {
            [Tooltip("Depixelize Filter Shader")]
            public Shader shader;
            
            [Tooltip("渲染时机")]
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            
            [Header("像素缩放")]
            [Range(1f, 32f)]
            [Tooltip("像素放大倍数 - 每个源像素占用多少屏幕像素")]
            public float pixelScale = 4f;
            
            [Header("算法参数")]
            [Range(0f, 1f)]
            [Tooltip("颜色相似性阈值 - 低于此值认为颜色相似")]
            public float colorThreshold = 0.1176f; // 30/255 from paper
            
            [Range(0f, 1f)]
            [Tooltip("轮廓边缘阈值 - 高于此值认为是轮廓边")]
            public float contourThreshold = 0.392f; // 100/255 from paper
            
            [Range(0f, 2f)]
            [Tooltip("曲线平滑强度")]
            public float smoothness = 1.0f;
            
            [Range(0f, 1f)]
            [Tooltip("抗锯齿强度")]
            public float antialiasing = 0.5f;
            
            [Header("启发式权重")]
            [Range(0f, 10f)]
            [Tooltip("曲线连续性权重")]
            public float curveWeight = 1.0f;
            
            [Range(0f, 10f)]
            [Tooltip("稀疏像素权重")]
            public float sparseWeight = 1.0f;
            
            [Range(0f, 10f)]
            [Tooltip("孤岛避免权重（论文默认5）")]
            public float islandWeight = 5.0f;
            
            [Header("调试")]
            [Tooltip("启用效果")]
            public bool enabled = true;
            
            [Tooltip("调试模式: 0=关闭, 1=显示对角线决策, 2=强制品红色确认shader运行")]
            [Range(0, 2)]
            public int debugMode = 0;
        }
        
        public DepixelizeSettings settings = new DepixelizeSettings();
        
        private DepixelizeRenderPass _renderPass;
        private Material _material;
        
        // Shader 属性 ID
        private static readonly int s_PixelScaleId = Shader.PropertyToID("_PixelScale");
        private static readonly int s_ColorThresholdId = Shader.PropertyToID("_ColorThreshold");
        private static readonly int s_ContourThresholdId = Shader.PropertyToID("_ContourThreshold");
        private static readonly int s_SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int s_AntialiasingId = Shader.PropertyToID("_Antialiasing");
        private static readonly int s_CurveWeightId = Shader.PropertyToID("_CurveWeight");
        private static readonly int s_SparseWeightId = Shader.PropertyToID("_SparseWeight");
        private static readonly int s_IslandWeightId = Shader.PropertyToID("_IslandWeight");
        private static readonly int s_DebugModeId = Shader.PropertyToID("_DebugMode");
        
        public override void Create()
        {
            if (settings.shader == null)
            {
                settings.shader = Shader.Find("EquipmentSystem/DepixelizeFilter");
            }
            
            if (settings.shader != null)
            {
                _material = CoreUtils.CreateEngineMaterial(settings.shader);
            }
            
            _renderPass = new DepixelizeRenderPass(_material);
            _renderPass.renderPassEvent = settings.renderPassEvent;
        }
        
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!settings.enabled || _material == null)
                return;
            
            var cameraType = renderingData.cameraData.cameraType;
            if (cameraType != CameraType.Game && cameraType != CameraType.SceneView)
                return;
            
            // 更新 Material 参数
            _material.SetFloat(s_PixelScaleId, settings.pixelScale);
            _material.SetFloat(s_ColorThresholdId, settings.colorThreshold);
            _material.SetFloat(s_ContourThresholdId, settings.contourThreshold);
            _material.SetFloat(s_SmoothnessId, settings.smoothness);
            _material.SetFloat(s_AntialiasingId, settings.antialiasing);
            _material.SetFloat(s_CurveWeightId, settings.curveWeight);
            _material.SetFloat(s_SparseWeightId, settings.sparseWeight);
            _material.SetFloat(s_IslandWeightId, settings.islandWeight);
            _material.SetFloat(s_DebugModeId, (float)settings.debugMode);
            
            renderer.EnqueuePass(_renderPass);
        }
        
        protected override void Dispose(bool disposing)
        {
            if (_material != null)
            {
                CoreUtils.Destroy(_material);
                _material = null;
            }
        }
        
        // ====================================================================
        // Render Pass (RenderGraph API)
        // ====================================================================
        private class DepixelizeRenderPass : ScriptableRenderPass
        {
            private readonly Material _blitMaterial;
            private const string k_PassName = "Depixelize Filter Pass";
            
            public DepixelizeRenderPass(Material material)
            {
                _blitMaterial = material;
                requiresIntermediateTexture = true;
            }
            
            private class BlitPassData
            {
                public TextureHandle sourceTexture;
                public Material material;
            }
            
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_blitMaterial == null)
                    return;
                
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                TextureHandle source = resourceData.activeColorTexture;
                
                if (!source.IsValid())
                    return;
                
                var sourceDesc = renderGraph.GetTextureDesc(source);
                sourceDesc.name = "Depixelize_Temp";
                sourceDesc.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(sourceDesc);
                
                // Pass 1: 应用 Depixelize 滤镜
                using (var builder = renderGraph.AddRasterRenderPass<BlitPassData>(k_PassName, out var passData))
                {
                    passData.sourceTexture = source;
                    passData.material = _blitMaterial;
                    
                    builder.UseTexture(source, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                    
                    builder.SetRenderFunc((BlitPassData data, RasterGraphContext context) =>
                    {
                        Blitter.BlitTexture(context.cmd, data.sourceTexture, new Vector4(1, 1, 0, 0), data.material, 0);
                    });
                }
                
                // Pass 2: 复制回 activeColorTexture
                using (var builder = renderGraph.AddRasterRenderPass<BlitPassData>("Depixelize Copy Back", out var passData2))
                {
                    passData2.sourceTexture = destination;
                    
                    builder.UseTexture(destination, AccessFlags.Read);
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
                    
                    builder.SetRenderFunc((BlitPassData data, RasterGraphContext context) =>
                    {
                        Blitter.BlitTexture(context.cmd, data.sourceTexture, new Vector4(1, 1, 0, 0), 0, false);
                    });
                }
            }
        }
    }
}
