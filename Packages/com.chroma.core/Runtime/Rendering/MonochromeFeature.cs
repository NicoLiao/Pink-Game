using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Chroma.Core.Rendering
{
    // How the mask pass decides which pixels belong to the Secondary rendering layer.
    public enum MonochromeMaskMode
    {
        World3D, // Secondary renderers, occluded by the camera depth texture.
        World2D, // All renderers in sort order; the topmost decides.
        Overlay  // UI coverage; covered pixels are remapped to primary.
    }

    // Remaps every pixel to the game's monochrome palette, keeping only its value.
    public sealed class MonochromeFeature : ScriptableRendererFeature
    {
        [SerializeField] Material remapMaterial;
        [SerializeField] Shader maskShader;
        [SerializeField] MonochromeMaskMode maskMode = MonochromeMaskMode.World3D;
        [SerializeField, Range(0f, 0.05f)] float depthTolerance = 0.002f;

        MonochromeMaskPass maskPass;
        MonochromeRemapPass remapPass;

        public override void Create()
        {
            maskPass = new MonochromeMaskPass { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
            remapPass = new MonochromeRemapPass { renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing, requiresIntermediateTexture = true };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection) return;
            if (remapMaterial == null || maskShader == null)
            {
                Debug.LogWarning($"MonochromeFeature \"{name}\" is skipped: assign the remap material and mask shader.");
                return;
            }

            maskPass.Setup(maskShader, maskMode, depthTolerance);
            remapPass.Setup(remapMaterial, maskMode);
            renderer.EnqueuePass(maskPass);
            renderer.EnqueuePass(remapPass);
        }
    }

    // Hands the mask texture from the mask pass to the remap pass within one camera.
    sealed class MonochromeMaskData : ContextItem
    {
        public TextureHandle mask;

        public override void Reset() => mask = TextureHandle.nullHandle;
    }

    // Draws the Secondary mask (or UI coverage) into a single-channel texture.
    sealed class MonochromeMaskPass : ScriptableRenderPass
    {
        const string SecondaryLayerName = "Secondary";

        static readonly int MaskId = Shader.PropertyToID("_ChromaSecondaryMask");
        static readonly int SecondaryLayerId = Shader.PropertyToID("_ChromaSecondaryRenderingLayer");
        static readonly int DepthToleranceId = Shader.PropertyToID("_ChromaDepthTolerance");

        static readonly List<ShaderTagId> Tags3D = new() { new("UniversalForward"), new("UniversalForwardOnly"), new("SRPDefaultUnlit") };
        static readonly List<ShaderTagId> Tags2D = new() { new("Universal2D"), new("SRPDefaultUnlit") };
        static readonly List<ShaderTagId> TagsOverlay = new() { new("SRPDefaultUnlit") };

        Shader shader;
        MonochromeMaskMode mode;
        float depthTolerance;
        bool warnedMissingLayer;

        class PassData
        {
            public RendererListHandle renderers;
            public int secondaryLayer;
            public float depthTolerance;
        }

        public void Setup(Shader maskShader, MonochromeMaskMode maskMode, float tolerance)
        {
            shader = maskShader;
            mode = maskMode;
            depthTolerance = tolerance;
            ConfigureInput(mode == MonochromeMaskMode.World3D ? ScriptableRenderPassInput.Depth : ScriptableRenderPassInput.None);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var renderingData = frameData.Get<UniversalRenderingData>();
            var lightData = frameData.Get<UniversalLightData>();

            var maskDesc = renderGraph.GetTextureDesc(resources.activeColorTexture);
            maskDesc.name = "_ChromaSecondaryMask";
            maskDesc.format = GraphicsFormat.R8_UNorm;
            maskDesc.msaaSamples = MSAASamples.None;
            maskDesc.bindTextureMS = false;
            maskDesc.filterMode = FilterMode.Bilinear;
            maskDesc.clearBuffer = true;
            maskDesc.clearColor = Color.clear;

            var maskData = frameData.GetOrCreate<MonochromeMaskData>();
            maskData.mask = renderGraph.CreateTexture(maskDesc);

            int secondaryBit = SecondaryRenderingLayerBit();
            var tags = mode switch
            {
                MonochromeMaskMode.World3D => Tags3D,
                MonochromeMaskMode.World2D => Tags2D,
                _ => TagsOverlay
            };
            var sorting = mode == MonochromeMaskMode.World3D ? cameraData.defaultOpaqueSortFlags : SortingCriteria.CommonTransparent;
            var drawing = RenderingUtils.CreateDrawingSettings(tags, renderingData, cameraData, lightData, sorting);
            drawing.overrideShader = shader;
            drawing.overrideShaderPassIndex = (int)mode;

            uint layerFilter = mode == MonochromeMaskMode.World3D ? (uint)secondaryBit : uint.MaxValue;
            var filtering = new FilteringSettings(RenderQueueRange.all, -1, layerFilter);

            using var builder = renderGraph.AddRasterRenderPass<PassData>("Chroma Monochrome Mask", out var passData, profilingSampler);
            passData.secondaryLayer = secondaryBit;
            passData.depthTolerance = depthTolerance;
            passData.renderers = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, drawing, filtering));

            builder.UseRendererList(passData.renderers);
            if (mode == MonochromeMaskMode.World3D) builder.UseTexture(resources.cameraDepthTexture);
            builder.SetRenderAttachment(maskData.mask, 0, AccessFlags.Write);
            builder.SetGlobalTextureAfterPass(maskData.mask, MaskId);
            builder.AllowGlobalStateModification(true);

            builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
            {
                context.cmd.SetGlobalInteger(SecondaryLayerId, data.secondaryLayer);
                context.cmd.SetGlobalFloat(DepthToleranceId, data.depthTolerance);
                context.cmd.DrawRendererList(data.renderers);
            });
        }

        // Returns the bit of the Secondary rendering layer, or 0 if it isn't defined.
        int SecondaryRenderingLayerBit()
        {
            int index = RenderingLayerMask.NameToRenderingLayer(SecondaryLayerName);
            if (index >= 0) return 1 << index;
            if (!warnedMissingLayer) Debug.LogError($"Rendering layer \"{SecondaryLayerName}\" is not defined; no pixels will use the secondary palette.");
            warnedMissingLayer = true;
            return 0;
        }
    }

    // Rewrites the camera color with the palette, using the mask from the mask pass.
    sealed class MonochromeRemapPass : ScriptableRenderPass
    {
        static readonly Vector4 NoScaleBias = new(1f, 1f, 0f, 0f);

        Material material;
        MonochromeMaskMode mode;

        class CopyData
        {
            public TextureHandle source;
        }

        class RemapData
        {
            public TextureHandle source;
            public Material material;
            public int pass;
        }

        public void Setup(Material remapMaterial, MonochromeMaskMode maskMode)
        {
            material = remapMaterial;
            mode = maskMode;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer || !frameData.Contains<MonochromeMaskData>()) return;
            var mask = frameData.Get<MonochromeMaskData>().mask;

            var copyDesc = renderGraph.GetTextureDesc(resources.activeColorTexture);
            copyDesc.name = "_ChromaMonochromeSource";
            copyDesc.clearBuffer = false;
            var copy = renderGraph.CreateTexture(copyDesc);

            using (var builder = renderGraph.AddRasterRenderPass<CopyData>("Chroma Monochrome Copy", out var copyData, profilingSampler))
            {
                copyData.source = resources.activeColorTexture;
                builder.UseTexture(copyData.source);
                builder.SetRenderAttachment(copy, 0, AccessFlags.Write);
                builder.SetRenderFunc((CopyData data, RasterGraphContext context) =>
                    Blitter.BlitTexture(context.cmd, data.source, NoScaleBias, 0f, false));
            }

            using (var builder = renderGraph.AddRasterRenderPass<RemapData>("Chroma Monochrome Remap", out var remapData, profilingSampler))
            {
                remapData.source = copy;
                remapData.material = material;
                remapData.pass = mode == MonochromeMaskMode.Overlay ? 1 : 0;
                builder.UseTexture(copy);
                builder.UseTexture(mask);
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.Write);
                builder.SetRenderFunc((RemapData data, RasterGraphContext context) =>
                    Blitter.BlitTexture(context.cmd, data.source, NoScaleBias, data.material, data.pass));
            }
        }
    }
}
