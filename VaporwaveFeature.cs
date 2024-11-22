using System;
using System.Threading;
using Unity.Mathematics;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
/*******************************************************
 * 项目来源: 开源项目 [Vaporwave](https://github.com/itorr/vaporwave?tab=readme-ov-file)
 * 
 * Project Source: Open-source project [Vaporwave](https://github.com/itorr/vaporwave?tab=readme-ov-file)
 *******************************************************/

public class VaporwaveFeature : ScriptableRendererFeature
{
    public static class ConvolutionKernels
    {
        public enum KernelType
        {
            [InspectorName("右倾")]
            RightTilt,

            [InspectorName("左倾")]
            LeftTilt,

            [InspectorName("桑拿")]
            Sauna,

            [InspectorName("浮雕")]
            Emboss
        }

        private static readonly Matrix4x4 Convolute_RightTilt = new Matrix4x4(
            new Vector4(0, -1, 0, 0),
            new Vector4(-1, 2, 2, 0),
            new Vector4(0, -1, 0, 0),
            new Vector4(0, 0, 0, 0)
        );

        private static readonly Matrix4x4 Convolute_LeftTilt = new Matrix4x4(
            new Vector4(0, -1, 0, 0),
            new Vector4(3, 2, -2, 0),
            new Vector4(0, -1, 0, 0),
            new Vector4(0, 0, 0, 0)
        );

        private static readonly Matrix4x4 Convolute_Sauna_3x3 = new Matrix4x4(
            new Vector4(1.0f / 9, 1.0f / 9, 1.0f / 9, 0),
            new Vector4(1.0f / 9, 1.0f / 9, 1.0f / 9, 0),
            new Vector4(1.0f / 9, 1.0f / 9, 1.0f / 9, 0),
            new Vector4(0, 0, 0, 0)
        );

        private static readonly Matrix4x4 Convolute_Emboss = new Matrix4x4(
            new Vector4(1, 1, 1, 0),
            new Vector4(1, 1, -1, 0),
            new Vector4(-1, -1, -1, 0),
            new Vector4(0, 0, 0, 0)
        );

        public static Matrix4x4 GetConvolutionKernel(KernelType kernelType)
        {
            switch (kernelType)
            {
                case KernelType.RightTilt:
                    return Convolute_RightTilt;
                case KernelType.LeftTilt:
                    return Convolute_LeftTilt;
                case KernelType.Sauna:
                    return Convolute_Sauna_3x3;
                case KernelType.Emboss:
                    return Convolute_Emboss;
                default:
                    Debug.LogWarning("Unknown kernel type, returning identity matrix.");
                    return Matrix4x4.identity;
            }
        }
    }
    class VaporwaverPass : ScriptableRenderPass
    {

        public RTHandle temRT0;
        public RTHandle temRT1;
        public Material convoluteMat;
        public Material blitMat;
        public Material blitMatInvert;
        public Material snowMat;
        public Material invertLight;
        public Material interlaced;
        public Material transposeX;
        public Material yuvHandle;
        public Material rgb2yuv;
        public Material yuv2rgb;
        public Material graphNoise;
        public Material EvLED;
        public Material FishEye;
        public float2 texSize;
        public VaporwaverPass(Setting setting)
        {
            this.setting = setting;
            convoluteMat = CoreUtils.CreateEngineMaterial(Shader.Find("Vaporwave/Convolute"));
            blitMat = CoreUtils.CreateEngineMaterial(Shader.Find("Vaporwave/BlitGraph"));
            blitMatInvert = CoreUtils.CreateEngineMaterial(Shader.Find("Vaporwave/BlitGraphInvert"));
            snowMat = CoreUtils.CreateEngineMaterial(Shader.Find("Vaporwave/Snow"));
            invertLight = CoreUtils.CreateEngineMaterial(Shader.Find("Vaporwave/InvertLight"));
            interlaced = CoreUtils.CreateEngineMaterial(Shader.Find("Vaporwave/Interlaced"));
            transposeX = CoreUtils.CreateEngineMaterial(Shader.Find("Vaporwave/TransposeX"));
            yuvHandle = CoreUtils.CreateEngineMaterial(Shader.Find("Vaporwave/YUVHandle"));
            rgb2yuv = CoreUtils.CreateEngineMaterial(Shader.Find("Vaporwave/RGB2YUV"));
            yuv2rgb = CoreUtils.CreateEngineMaterial(Shader.Find("Vaporwave/YUV2RGB"));
            graphNoise = CoreUtils.CreateEngineMaterial(Shader.Find("Vaporwave/GraphNoise"));
            EvLED = CoreUtils.CreateEngineMaterial(Shader.Find("Vaporwave/EvLED"));
            FishEye = CoreUtils.CreateEngineMaterial(Shader.Find("Vaporwave/FishEye"));
        }
        public Setting setting;
        public bool invert;
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {


            var desc = renderingData.cameraData.cameraTargetDescriptor;
            RenderTextureDescriptor temRTDescriptor;
            texSize = new float2(desc.width, desc.height);
            temRTDescriptor = new RenderTextureDescriptor(Mathf.CeilToInt(desc.width), Mathf.CeilToInt(desc.height), RenderTextureFormat.ARGBFloat, 0, 0);
            temRTDescriptor.msaaSamples = 1;
            temRTDescriptor.useMipMap = false;
            temRTDescriptor.colorFormat = renderingData.cameraData.cameraTargetDescriptor.colorFormat;
            temRTDescriptor.sRGB = false;
            RenderingUtils.ReAllocateIfNeeded(ref temRT0, temRTDescriptor);
            RenderingUtils.ReAllocateIfNeeded(ref temRT1, temRTDescriptor);
            ConfigureTarget(temRT0);
            temRT0.rt.wrapMode = TextureWrapMode.Repeat;
            ConfigureClear(ClearFlag.None, Color.black);
            temRT1.rt.wrapMode = TextureWrapMode.Repeat;
            ConfigureTarget(temRT1);
            ConfigureClear(ClearFlag.None, Color.black);
        }
        public void Cleanup()
        {
            temRT0?.Release();
            temRT0 = null;
            temRT1?.Release();
            temRT1 = null;

        }
        public RTHandle GetSourceRT()
        {
            if (pingpong)
            {
                return temRT1;
            }
            else
            {
                return temRT0;
            }
        }
        public RTHandle GetTargetRT()
        {
            if (pingpong)
            {
                return temRT0;
            }
            else
            {
                return temRT1;
            }

        }
        
        public void SwapRT()
        {
            pingpong = !pingpong;
        }
        public bool pingpong;
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            RTHandle cameraTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;
            if (cameraTarget.rt != null)
            {

                CommandBuffer cmd = CommandBufferPool.Get("Varporwave Pass");
                pingpong = false;


                cmd.Blit(cameraTarget, temRT0, blitMat);
                //---------------------------------------
                if (setting.emphasizeLines)
                {
                    Convolute(cmd);
                }
                if (setting.snowEffect)
                {
                    SnowEffect(cmd);
                }
                else
                {
                    if (setting.yuvHandleXEnable)
                    {
                        YUVHandleEffect(cmd);
                    }
                    if (setting.invertLight)
                    {
                        InvertLightEffect(cmd);
                    }
                    if (setting.FishEyeEnable)
                    {
                        FishEyeEffect(cmd);
                    }
                    if (setting.interlacedEnable)
                    {
                        InterlacedEffect(cmd);
                    }
                    if (setting.transposeXEnable)
                    {
                        TransposeXEffect(cmd);
                    }
                }
                if (setting.qualityEnable)
                {
                    QualitySetting(cmd);
                }

                if (setting.LEDResolutionEnable)
                {
                    LEDEffect(cmd);
                }

                //---------------------------------------
                if (pingpong)
                {
                    cmd.Blit(temRT1, cameraTarget, blitMat);
                }
                else
                {
                    cmd.Blit(temRT0, cameraTarget, blitMat);
                }
                context.ExecuteCommandBuffer(cmd);


                CommandBufferPool.Release(cmd);
            }
        }
        public void QualitySetting(CommandBuffer cmd)
        {
            cmd.SetGlobalFloat("_DrakNoise", setting.darkNoise / 255f);
            cmd.SetGlobalFloat("_LightNoise", setting.lightNoise / 255f);
            cmd.Blit(GetSourceRT(), GetTargetRT(), graphNoise);
            SwapRT();
        }
        public void YUVHandleEffect(CommandBuffer cmd)
        {
            cmd.Blit(GetSourceRT(), GetTargetRT(), rgb2yuv);
            SwapRT();
            cmd.SetGlobalFloat("_ShiftX", setting.shiftX / texSize.x);
            cmd.SetGlobalFloat("_ShiftY", setting.shiftY / texSize.y);
            cmd.SetGlobalFloat("_ShiftU", setting.shiftU);
            cmd.SetGlobalFloat("_ShiftV", setting.shiftV);
            cmd.SetGlobalFloat("_Level", setting.level);
            cmd.SetGlobalFloat("_Contrast", setting.contrast);
            cmd.SetGlobalFloat("_Light", setting.light);
            cmd.SetGlobalFloat("_DarkFade", setting.darkFade);
            cmd.SetGlobalFloat("_BrightFade", setting.brightFade);
            cmd.SetGlobalFloat("_VividU", setting.vividU);
            cmd.SetGlobalFloat("_VividV", setting.vividV);

            cmd.Blit(GetSourceRT(), GetTargetRT(), yuvHandle);
            SwapRT();
            cmd.Blit(GetSourceRT(), GetTargetRT(), yuv2rgb);
            SwapRT();
        }
        public void TransposeXEffect(CommandBuffer cmd)
        {
            cmd.SetGlobalFloat("_TransposeX", setting.transposeX);
            cmd.SetGlobalFloat("_TransposePow", setting.transposePow);
            cmd.SetGlobalFloat("_TransposeNoise", setting.transposeNoise);
            cmd.SetGlobalVector("_TexSize", new Vector4(texSize.x, texSize.y, 0, 0));
            cmd.Blit(GetSourceRT(), GetTargetRT(), transposeX);
            SwapRT();
        }
        public void InterlacedEffect(CommandBuffer cmd)
        {
            cmd.SetGlobalFloat("_Interlaced", setting.interlaced);
            cmd.SetGlobalFloat("_InterlacedLine", setting.interlacedLine);
            cmd.SetGlobalFloat("_InterlacedLight", setting.interlacedLight);
            cmd.SetGlobalVector("_TexSize", new Vector4(texSize.x, texSize.y, 0, 0));
            cmd.Blit(GetSourceRT(), GetTargetRT(), interlaced);
            SwapRT();
        }
        public void InvertLightEffect(CommandBuffer cmd)
        {
            cmd.Blit(GetSourceRT(), GetTargetRT(), invertLight);
            SwapRT();
        }
        public void SnowEffect(CommandBuffer cmd)
        {
            cmd.SetGlobalFloat("_VaporwaveTime", Time.deltaTime);
            cmd.Blit(GetSourceRT(), GetTargetRT(), snowMat);
            SwapRT();
        }

        public void LEDEffect(CommandBuffer cmd)
        {
            cmd.SetGlobalFloat("_LEDResolutionLevel",setting.LEDResolutionLevel);
            cmd.Blit(GetSourceRT(), GetTargetRT(), EvLED);
            SwapRT();
        }
        public void FishEyeEffect(CommandBuffer cmd)
        {
            cmd.SetGlobalVector("_FishEyeIntensity", new Vector4(setting.FishEyeIntensity_X, setting.FishEyeIntensity_Y, 0, 0));
            cmd.SetGlobalFloat("_FishEyePow", setting.FishEyePow);
            cmd.Blit(GetSourceRT(), GetTargetRT(), FishEye);
            SwapRT();
        }
        public void Convolute(CommandBuffer cmd)
        {
            cmd.SetGlobalMatrix("_ConvoluteCore", ConvolutionKernels.GetConvolutionKernel(setting.convoluteType));
            cmd.SetGlobalVector("_TexDeltaSize", new Vector4(1 / texSize.x, 1 / texSize.y, 0, 0));
            cmd.Blit(GetSourceRT(), GetTargetRT(), convoluteMat);
            SwapRT();
        }
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
        }
    }

    VaporwaverPass m_ScriptablePass;
    public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
    public Setting m_Setting;
    [Serializable]
    public class Setting
    {
        public bool yuvHandleXEnable = true;
        public float shiftX = 6f;
        public float shiftY = 4f;
        public float shiftU = 0f;
        public float shiftV = 0f;
        public float level = 4f;
        public float contrast = 1f;
        public float light = 1f;
        public float darkFade = 0f;
        public float brightFade = 0f;
        public float vividU = 1.18f;
        public float vividV = 0.93f;
        public bool interlacedEnable = true;
        public int interlaced = 1;
        public int interlacedLine = 4;
        public float interlacedLight = 0.2f;
        public bool transposeXEnable = false;
        public float transposeX = 0.04f;
        public float transposePow = 6.7f;
        public float transposeNoise = 0.616f;
        public bool qualityEnable = true;
        public float darkNoise = 5;
        public float lightNoise = 0;

        public bool LEDResolutionEnable = false;
        public int LEDResolutionLevel = 1;

        public bool FishEyeEnable = false;
        public float FishEyeIntensity_X = 1f;
        public float FishEyeIntensity_Y = 1f;
        public float FishEyePow = 1;

        public bool emphasizeLines = true;

        public ConvolutionKernels.KernelType convoluteType = ConvolutionKernels.KernelType.RightTilt;
        public bool snowEffect = false;
        public bool invertLight = false;


    }
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(Setting))]
    public class SettingDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {

            EditorGUI.BeginProperty(position, label, property);
            EditorGUILayout.LabelField("基 础 设 置", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            SerializedProperty yuvHandleXEnable = property.FindPropertyRelative("yuvHandleXEnable");
            EditorGUILayout.PropertyField(yuvHandleXEnable, new GUIContent("启 用 色 彩 处 理"));
            SerializedProperty light = property.FindPropertyRelative("light");
            light.floatValue = EditorGUILayout.Slider(new GUIContent("画 面 亮 度"), light.floatValue, 0f, 4f);
            SerializedProperty contrast = property.FindPropertyRelative("contrast");
            contrast.floatValue = EditorGUILayout.Slider(new GUIContent("画 面 反 差"), contrast.floatValue, 0f, 4f);
            SerializedProperty darkFade = property.FindPropertyRelative("darkFade");
            darkFade.floatValue = EditorGUILayout.Slider(new GUIContent("暗 部 褪 色"), darkFade.floatValue, 0f, 128f);
            SerializedProperty brightFade = property.FindPropertyRelative("brightFade");
            brightFade.floatValue = EditorGUILayout.Slider(new GUIContent("亮 部 褪 色"), brightFade.floatValue, 0f, 128f);
            EditorGUILayout.EndVertical();

            EditorGUILayout.LabelField("颜 色 设 置", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            SerializedProperty vividU = property.FindPropertyRelative("vividU");
            vividU.floatValue = EditorGUILayout.Slider(new GUIContent("紫 鲜 艳 度"), vividU.floatValue, 0.1f, 4f);
            SerializedProperty vividV = property.FindPropertyRelative("vividV");
            vividV.floatValue = EditorGUILayout.Slider(new GUIContent("青 鲜 艳 度"), vividV.floatValue, 0.1f, 4f);
            SerializedProperty shiftU = property.FindPropertyRelative("shiftU");
            shiftU.floatValue = EditorGUILayout.Slider(new GUIContent("紫 色 偏 移"), shiftU.floatValue, -200f, 200f);
            SerializedProperty shiftV = property.FindPropertyRelative("shiftV");
            shiftV.floatValue = EditorGUILayout.Slider(new GUIContent("青 色 偏 移"), shiftV.floatValue, -200f, 200f);
            SerializedProperty level = property.FindPropertyRelative("level");
            level.floatValue = EditorGUILayout.Slider(new GUIContent("色 彩 断 层"), level.floatValue, 1f, 255f);
            EditorGUILayout.EndVertical();



            EditorGUILayout.LabelField("效 果 设 置", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            SerializedProperty shiftX = property.FindPropertyRelative("shiftX");
            shiftX.floatValue = EditorGUILayout.Slider(new GUIContent("横 向 偏 移"), shiftX.floatValue, 0f, 1000f);
            SerializedProperty shiftY = property.FindPropertyRelative("shiftY");
            shiftY.floatValue = EditorGUILayout.Slider(new GUIContent("纵 向 偏 移"), shiftY.floatValue, 0f, 1000f);
            EditorGUILayout.LabelField("隔 行 扫 描", EditorStyles.boldLabel);
            SerializedProperty interlacedEnable = property.FindPropertyRelative("interlacedEnable");
            EditorGUILayout.PropertyField(interlacedEnable, new GUIContent("启 用 隔 行 扫 描"));
            SerializedProperty interlaced = property.FindPropertyRelative("interlaced");
            interlaced.intValue = EditorGUILayout.IntSlider(new GUIContent("隔 行 扫 描"), interlaced.intValue, 0, 8);
            SerializedProperty interlacedLine = property.FindPropertyRelative("interlacedLine");
            interlacedLine.intValue = EditorGUILayout.IntSlider(new GUIContent("隔 行 扫 描 周 期"), interlacedLine.intValue, 2, 8);
            SerializedProperty interlacedLight = property.FindPropertyRelative("interlacedLight");
            interlacedLight.floatValue = EditorGUILayout.Slider(new GUIContent("隔 行 扫 描 亮 度 差"), interlacedLight.floatValue, 0f, 1f);
            EditorGUILayout.LabelField("漂 移", EditorStyles.boldLabel);
            SerializedProperty transposeXEnable = property.FindPropertyRelative("transposeXEnable");
            EditorGUILayout.PropertyField(transposeXEnable, new GUIContent("启 用 漂 移"));
            SerializedProperty transposeX = property.FindPropertyRelative("transposeX");
            transposeX.floatValue = EditorGUILayout.Slider(new GUIContent("横 向 漂 移"), transposeX.floatValue, 0f, 10f);
            SerializedProperty transposePow = property.FindPropertyRelative("transposePow");
            transposePow.floatValue = EditorGUILayout.Slider(new GUIContent("横 向 漂 移 抛 物 线"), transposePow.floatValue, 1f, 8f);
            SerializedProperty transposeNoise = property.FindPropertyRelative("transposeNoise");
            transposeNoise.floatValue = EditorGUILayout.Slider(new GUIContent("横 向 漂 移 杂 质"), transposeNoise.floatValue, 0f, 2f);
            EditorGUILayout.EndVertical();

            EditorGUILayout.LabelField("质 量 设 置", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            SerializedProperty qualityEnable = property.FindPropertyRelative("qualityEnable");
            EditorGUILayout.PropertyField(qualityEnable,new GUIContent("启 用 质 量 设 置"));
            SerializedProperty lightNoise = property.FindPropertyRelative("lightNoise");
            lightNoise.floatValue = EditorGUILayout.Slider(new GUIContent("信 号 噪 声"), lightNoise.floatValue, 0f, 500f);
            SerializedProperty darkNoise = property.FindPropertyRelative("darkNoise");
            darkNoise.floatValue = EditorGUILayout.Slider(new GUIContent("胶 片 颗 粒"), darkNoise.floatValue, 0f, 500f);
            EditorGUILayout.EndVertical();

            EditorGUILayout.LabelField("LED 设 置", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            SerializedProperty LEDResolutionEnable = property.FindPropertyRelative("LEDResolutionEnable");
            EditorGUILayout.PropertyField(LEDResolutionEnable, new GUIContent("LED 启 用"));
            SerializedProperty LEDResolutionLevel = property.FindPropertyRelative("LEDResolutionLevel");
            LEDResolutionLevel.intValue = EditorGUILayout.IntSlider(new GUIContent("LED 分 辨 率 缩 减"), LEDResolutionLevel.intValue, 0,10 );
            EditorGUILayout.EndVertical();

            EditorGUILayout.LabelField("鱼 眼 设 置", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            SerializedProperty FishEyeEnable = property.FindPropertyRelative("FishEyeEnable");
            EditorGUILayout.PropertyField(FishEyeEnable, new GUIContent("启 用 鱼 眼"));
            SerializedProperty FishEyeIntensity_X = property.FindPropertyRelative("FishEyeIntensity_X");
            FishEyeIntensity_X.floatValue = EditorGUILayout.Slider(new GUIContent("鱼 眼 横 向 强 度"), FishEyeIntensity_X.floatValue, 0, 1);
            SerializedProperty FishEyeIntensity_Y = property.FindPropertyRelative("FishEyeIntensity_Y");
            FishEyeIntensity_Y.floatValue = EditorGUILayout.Slider(new GUIContent("鱼 眼 纵 向 强 度"), FishEyeIntensity_Y.floatValue, 0, 1);

            SerializedProperty FishEyePow = property.FindPropertyRelative("FishEyePow");
            FishEyePow.floatValue = EditorGUILayout.Slider(new GUIContent("鱼 眼 曲 率"), FishEyePow.floatValue, 0, 10);

            EditorGUILayout.EndVertical();

            
            EditorGUILayout.LabelField("其 他 设 置", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            SerializedProperty emphasizeLines = property.FindPropertyRelative("emphasizeLines");
            EditorGUILayout.PropertyField(emphasizeLines, new GUIContent("强 调 线 条"));
            SerializedProperty convoluteType = property.FindPropertyRelative("convoluteType");
            EditorGUILayout.PropertyField(convoluteType, new GUIContent("线 条 方 案"));
            SerializedProperty snowEffect = property.FindPropertyRelative("snowEffect");
            EditorGUILayout.PropertyField(snowEffect, new GUIContent("切 断 信 号"));
            SerializedProperty invertLight = property.FindPropertyRelative("invertLight");
            EditorGUILayout.PropertyField(invertLight, new GUIContent("颠 倒 黑 白"));
            EditorGUILayout.EndVertical();


            EditorGUI.EndProperty();
        }

    }
#endif
    /// <inheritdoc/>
    public override void Create()
    {
        m_ScriptablePass = new VaporwaverPass(m_Setting);

        m_ScriptablePass.invert = renderPassEvent == RenderPassEvent.AfterRendering;
        m_ScriptablePass.renderPassEvent = renderPassEvent;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_ScriptablePass);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            m_ScriptablePass?.Cleanup();
        }
    }
}


