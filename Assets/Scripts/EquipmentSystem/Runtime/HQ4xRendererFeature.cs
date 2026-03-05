// ============================================================================
// HQ4x Renderer Feature
// ============================================================================
// 
// URP 2D 全屏后处理 Renderer Feature，应用 HQ4x 像素画放大效果
// 适用于 URP 17+ (Unity 6)，使用 RenderGraph API
//
// 使用方法：
// 1. 在 Renderer2D.asset 中添加此 Feature
// 2. 指定 HQ4xFilter Shader
// 3. 提供 hq4x.png LUT 纹理（可放在 Resources 目录自动加载）
// 4. 调整参数以获得最佳效果
// ============================================================================
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace EquipmentSystem
{
    public class HQ4xRendererFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class HQ4xSettings
        {
            [Tooltip("HQ4x Filter Shader")]
            public Shader shader;
            
            [Tooltip("HQ4x 权重查找表 (256x4096)，留空则从 Resources/hq4x 自动加载")]
            public Texture2D lut;
            
            [Tooltip("渲染时机")]
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            
            [Header("像素缩放")]
            [Range(1f, 32f)]
            [Tooltip("像素放大倍数 - 每个源像素占用多少屏幕像素")]
            public float pixelScale = 4f;
            
            [Header("调试")]
            [Tooltip("启用效果")]
            public bool enabled = true;
        }
        
        public HQ4xSettings hq4xSettings = new HQ4xSettings();
        
        private HQ4xRenderPass _renderPass;
        private Material _material;
        
        // Shader 属性 ID
        private static readonly int s_PixelScaleId = Shader.PropertyToID("_PixelScale");
        private static readonly int s_LUTId = Shader.PropertyToID("_LUT");
        
        public override void Create()
        {
            if (hq4xSettings.shader == null)
            {
                hq4xSettings.shader = Shader.Find("EquipmentSystem/HQ4xFilter");
            }
            
            if (hq4xSettings.shader != null)
            {
                _material = CoreUtils.CreateEngineMaterial(hq4xSettings.shader);
            }
            
            // 尝试从 Resources 加载 LUT
            if (hq4xSettings.lut == null)
            {
                hq4xSettings.lut = Resources.Load<Texture2D>("hq4x");
            }
            
            _renderPass = new HQ4xRenderPass(_material);
            _renderPass.renderPassEvent = hq4xSettings.renderPassEvent;
        }
        
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!hq4xSettings.enabled || _material == null)
                return;
            
            // LUT 必须存在
            if (hq4xSettings.lut == null)
            {
                Debug.LogWarning("HQ4xRendererFeature: LUT 纹理未设置，请提供 hq4x.png 或放到 Resources/hq4x.png");
                return;
            }
            
            // 只对 Game 和 SceneView 相机应用
            var cameraType = renderingData.cameraData.cameraType;
            if (cameraType != CameraType.Game && cameraType != CameraType.SceneView)
                return;
            
            // 更新 Material 参数
            _material.SetFloat(s_PixelScaleId, hq4xSettings.pixelScale);
            _material.SetTexture(s_LUTId, hq4xSettings.lut);
            
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
        private class HQ4xRenderPass : ScriptableRenderPass
        {
            private readonly Material _blitMaterial;
            private const string k_PassName = "HQ4x Filter Pass";
            
            public HQ4xRenderPass(Material material)
            {
                _blitMaterial = material;
                // 需要中间纹理以读取当前颜色缓冲
                requiresIntermediateTexture = true;
            }
            
            // PassData 用于 RenderGraph
            private class BlitPassData
            {
                public TextureHandle sourceTexture;
                public Material material;
            }
            
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_blitMaterial == null)
                    return;
                
                // 获取 URP 资源数据
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                
                // 源纹理 = 当前活动颜色纹理
                TextureHandle source = resourceData.activeColorTexture;
                
                // 检查源纹理是否有效
                if (!source.IsValid())
                    return;
                
                // 创建目标纹理（与源相同尺寸）
                var sourceDesc = renderGraph.GetTextureDesc(source);
                sourceDesc.name = "HQ4x_Temp";
                sourceDesc.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(sourceDesc);
                
                // 第一个 Pass：从 source 应用 HQ4x 到 destination
                using (var builder = renderGraph.AddRasterRenderPass<BlitPassData>(k_PassName, out var passData))
                {
                    passData.sourceTexture = source;
                    passData.material = _blitMaterial;
                    
                    // 声明纹理使用
                    builder.UseTexture(source, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                    
                    builder.SetRenderFunc((BlitPassData data, RasterGraphContext context) =>
                    {
                        // 使用 Blitter 绘制全屏四边形，Material 方式
                        // scaleBias = (1,1,0,0) 表示完整采样
                        Blitter.BlitTexture(context.cmd, data.sourceTexture, new Vector4(1, 1, 0, 0), data.material, 0);
                    });
                }
                
                // 第二个 Pass：从 destination 复制回 activeColorTexture
                using (var builder = renderGraph.AddRasterRenderPass<BlitPassData>("HQ4x Copy Back", out var passData2))
                {
                    passData2.sourceTexture = destination;
                    
                    builder.UseTexture(destination, AccessFlags.Read);
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
                    
                    builder.SetRenderFunc((BlitPassData data, RasterGraphContext context) =>
                    {
                        // 简单复制，使用内置 blit shader (mipLevel=0, bilinear=false)
                        Blitter.BlitTexture(context.cmd, data.sourceTexture, new Vector4(1, 1, 0, 0), 0, false);
                    });
                }
            }
        }
    }
}
