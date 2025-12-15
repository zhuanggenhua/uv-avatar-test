// ============================================================================
// xBRZ Renderer Feature
// ============================================================================
// 
// URP 2D 全屏后处理 Renderer Feature，应用 xBRZ-like 像素画边缘平滑效果
// 适用于 URP 17+ (Unity 6)，使用 RenderGraph API
//
// 使用方法：
// 1. 在 Renderer2D.asset 中添加此 Feature
// 2. 指定 xBRZFilter Shader 或 Material
// 3. 调整参数以获得最佳效果
// ============================================================================
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace EquipmentSystem
{
    public class xBRZRendererFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class xBRZSettings
        {
            [Tooltip("xBRZ Filter Shader")]
            public Shader shader;
            
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
        
        public xBRZSettings xbrzSettings = new xBRZSettings();
        
        private xBRZRenderPass _renderPass;
        private Material _material;
        
        // Shader 属性 ID
        private static readonly int s_PixelScaleId = Shader.PropertyToID("_PixelScale");
        
        public override void Create()
        {
            if (xbrzSettings.shader == null)
            {
                xbrzSettings.shader = Shader.Find("EquipmentSystem/xBRZFilter");
            }
            
            if (xbrzSettings.shader != null)
            {
                _material = CoreUtils.CreateEngineMaterial(xbrzSettings.shader);
            }
            
            _renderPass = new xBRZRenderPass(_material);
            _renderPass.renderPassEvent = xbrzSettings.renderPassEvent;
        }
        
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!xbrzSettings.enabled || _material == null)
                return;
            
            // 只对 Game 和 SceneView 相机应用
            var cameraType = renderingData.cameraData.cameraType;
            if (cameraType != CameraType.Game && cameraType != CameraType.SceneView)
                return;
            
            // 更新 Material 参数
            _material.SetFloat(s_PixelScaleId, xbrzSettings.pixelScale);
            
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
        private class xBRZRenderPass : ScriptableRenderPass
        {
            private readonly Material _blitMaterial;
            private const string k_PassName = "xBRZ Filter Pass";
            
            public xBRZRenderPass(Material material)
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
                sourceDesc.name = "xBRZ_Temp";
                sourceDesc.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(sourceDesc);
                
                // 第一个 Pass：从 source 应用 xBRZ 到 destination
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
                        Blitter.BlitTexture(context.cmd, new Vector4(1, 1, 0, 0), data.material, 0);
                    });
                }
                
                // 第二个 Pass：从 destination 复制回 activeColorTexture
                using (var builder = renderGraph.AddRasterRenderPass<BlitPassData>("xBRZ Copy Back", out var passData2))
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
