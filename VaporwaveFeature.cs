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
 * ��Ŀ��Դ: ��Դ��Ŀ [Vaporwave](https://github.com/itorr/vaporwave?tab=readme-ov-file)
 * 
 * Project Source: Open-source project [Vaporwave](https://github.com/itorr/vaporwave?tab=readme-ov-file)
 *******************************************************/
namespace Vaporwave
{
    public class VaporwaveFeature : ScriptableRendererFeature
    {
        public static class ConvolutionKernels
        {
            public enum KernelType
            {
                [InspectorName("����")]
                RightTilt,

                [InspectorName("����")]
                LeftTilt,

                [InspectorName("ɣ��")]
                Sauna,

                [InspectorName("����")]
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
            public Material WaterMark;
            public float2 texSize;
            public VaporwaverPass(Setting setting, ShaderSetting shaderSetting)
            {
                this.setting = setting;
                convoluteMat = CoreUtils.CreateEngineMaterial(shaderSetting.ConvoluteShader);
                blitMat = CoreUtils.CreateEngineMaterial(shaderSetting.BlitGraphShader);
                blitMatInvert = CoreUtils.CreateEngineMaterial(shaderSetting.BlitGraphInvertShader);
                snowMat = CoreUtils.CreateEngineMaterial(shaderSetting.SnowShader);
                invertLight = CoreUtils.CreateEngineMaterial(shaderSetting.InvertLightShader);
                interlaced = CoreUtils.CreateEngineMaterial(shaderSetting.InterlacedShader);
                transposeX = CoreUtils.CreateEngineMaterial(shaderSetting.TransposeXShader);
                yuvHandle = CoreUtils.CreateEngineMaterial(shaderSetting.YUVHandleShader);
                rgb2yuv = CoreUtils.CreateEngineMaterial(shaderSetting.RGB2YUVShader);
                yuv2rgb = CoreUtils.CreateEngineMaterial(shaderSetting.YUV2RGBShader);
                graphNoise = CoreUtils.CreateEngineMaterial(shaderSetting.GraphNoiseShader);
                EvLED = CoreUtils.CreateEngineMaterial(shaderSetting.EvLEDShader);
                FishEye = CoreUtils.CreateEngineMaterial(shaderSetting.FishEyeShader);
                WaterMark = CoreUtils.CreateEngineMaterial(shaderSetting.WaterMarkShader);

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
                    if (setting.WaterMarkEnable && setting.MarkTexture != null)
                    {
                        WaterMarkEffect(cmd, cameraTarget);
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
            #region Vector����
            public static (Vector2 a, Vector2 b) Vec4ToVec2(Vector4 vector4)
            {
                return (new Vector2(vector4.x, vector4.y), new Vector2(vector4.z, vector4.w));
            }
            public static Vector4 Vec2ToVec4(Vector2 a, Vector2 b)
            {
                return new Vector4(a.x, a.y, b.x, b.y);
            }
            public static Vector4 Multiply(Vector4 a, Vector4 b)
            {
                return new Vector4(
                    a.x * b.x,
                    a.y * b.y,
                    a.z * b.z,
                    a.w * b.w
                );
            }
            public static Vector4 Divide(Vector4 a, Vector4 b)
            {
                return new Vector4(
                    b.x != 0 ? a.x / b.x : 0,
                    b.y != 0 ? a.y / b.y : 0,
                    b.z != 0 ? a.z / b.z : 0,
                    b.w != 0 ? a.w / b.w : 0
                );
            }
            public static Vector2 Multiply(Vector2 a, Vector2 b)
            {
                return new Vector2(
                    a.x * b.x,
                    a.y * b.y
                );
            }
            public static Vector2 Divide(Vector2 a, Vector2 b)
            {
                return new Vector2(
                    b.x != 0 ? a.x / b.x : 0,
                    b.y != 0 ? a.y / b.y : 0
                );
            }
            #endregion
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
                cmd.SetGlobalFloat("_LEDResolutionLevel", setting.LEDResolutionLevel);
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

            public void WaterMarkEffect(CommandBuffer cmd, RTHandle rt)
            {
                Vector2 size = Divide(new Vector2(setting.MarkTexture.width, setting.MarkTexture.height), new Vector2(rt.rt.width, rt.rt.height));
                var rect = Vec4ToVec2(setting.MarkTextureRect);
                size = Multiply(rect.b, size);
                cmd.SetGlobalVector("_MarkTextureRect", Vec2ToVec4(rect.a - size / 2f, size));
                cmd.SetGlobalFloat("_MarkTextureAlpha", setting.MarkTextureAlpha);
                cmd.SetGlobalTexture("_MarkTexture", setting.MarkTexture);

                cmd.Blit(GetSourceRT(), GetTargetRT(), WaterMark);
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
        [Serializable]
        public class ShaderSetting
        {
            public Shader ConvoluteShader = Shader.Find("Vaporwave/Convolute");
            public Shader BlitGraphShader = Shader.Find("Vaporwave/BlitGraph");
            public Shader BlitGraphInvertShader = Shader.Find("Vaporwave/BlitGraphInvert");
            public Shader SnowShader = Shader.Find("Vaporwave/Snow");
            public Shader InvertLightShader = Shader.Find("Vaporwave/InvertLight");
            public Shader InterlacedShader = Shader.Find("Vaporwave/Interlaced");
            public Shader TransposeXShader = Shader.Find("Vaporwave/TransposeX");
            public Shader YUVHandleShader = Shader.Find("Vaporwave/YUVHandle");
            public Shader RGB2YUVShader = Shader.Find("Vaporwave/RGB2YUV");
            public Shader YUV2RGBShader = Shader.Find("Vaporwave/YUV2RGB");
            public Shader GraphNoiseShader = Shader.Find("Vaporwave/GraphNoise");
            public Shader EvLEDShader = Shader.Find("Vaporwave/EvLED");
            public Shader FishEyeShader = Shader.Find("Vaporwave/FishEye");
            public Shader WaterMarkShader = Shader.Find("Vaporwave/WaterMark");
        }
        public ShaderSetting shaderSetting;
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

            public bool FishEyeEnable = true;
            public float FishEyeIntensity_X = 0.1f;
            public float FishEyeIntensity_Y = 0.1f;
            public float FishEyePow = 0.5f;

            public bool WaterMarkEnable = false;
            public Vector4 MarkTextureRect = new Vector4(0.5f, 0.21f, 1f, 1f);
            public float MarkTextureAlpha = 1;

            public Texture2D MarkTexture;
            public GameObject MarkTextPrefab;
            public string MarkTextFiled;

            public bool emphasizeLines = true;

            public ConvolutionKernels.KernelType convoluteType = ConvolutionKernels.KernelType.RightTilt;
            public bool snowEffect = false;
            public bool invertLight = false;


        }

#if UNITY_EDITOR
        [CustomPropertyDrawer(typeof(Setting))]
        public class SettingDrawer : PropertyDrawer
        {
            public static (Vector2 a, Vector2 b) Vec4ToVec2(Vector4 vector4)
            {
                return (new Vector2(vector4.x, vector4.y), new Vector2(vector4.z, vector4.w));
            }
            public static Vector4 Vec2ToVec4(Vector2 a, Vector2 b)
            {
                return new Vector4(a.x, a.y, b.x, b.y);
            }
            public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            {
                #region ��������
                EditorGUI.BeginProperty(position, label, property);
                EditorGUILayout.LabelField("�� �� �� ��", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical("box");
                SerializedProperty yuvHandleXEnable = property.FindPropertyRelative("yuvHandleXEnable");
                EditorGUILayout.PropertyField(yuvHandleXEnable, new GUIContent("�� �� ɫ �� �� ��"));
                SerializedProperty light = property.FindPropertyRelative("light");
                light.floatValue = EditorGUILayout.Slider(new GUIContent("�� �� �� ��"), light.floatValue, 0f, 4f);
                SerializedProperty contrast = property.FindPropertyRelative("contrast");
                contrast.floatValue = EditorGUILayout.Slider(new GUIContent("�� �� �� ��"), contrast.floatValue, 0f, 4f);
                SerializedProperty darkFade = property.FindPropertyRelative("darkFade");
                darkFade.floatValue = EditorGUILayout.Slider(new GUIContent("�� �� �� ɫ"), darkFade.floatValue, 0f, 128f);
                SerializedProperty brightFade = property.FindPropertyRelative("brightFade");
                brightFade.floatValue = EditorGUILayout.Slider(new GUIContent("�� �� �� ɫ"), brightFade.floatValue, 0f, 128f);
                EditorGUILayout.EndVertical();
                #endregion

                #region ��ɫ����
                EditorGUILayout.LabelField("�� ɫ �� ��", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical("box");
                SerializedProperty vividU = property.FindPropertyRelative("vividU");
                vividU.floatValue = EditorGUILayout.Slider(new GUIContent("�� �� �� ��"), vividU.floatValue, 0.1f, 4f);
                SerializedProperty vividV = property.FindPropertyRelative("vividV");
                vividV.floatValue = EditorGUILayout.Slider(new GUIContent("�� �� �� ��"), vividV.floatValue, 0.1f, 4f);
                SerializedProperty shiftU = property.FindPropertyRelative("shiftU");
                shiftU.floatValue = EditorGUILayout.Slider(new GUIContent("�� ɫ ƫ ��"), shiftU.floatValue, -200f, 200f);
                SerializedProperty shiftV = property.FindPropertyRelative("shiftV");
                shiftV.floatValue = EditorGUILayout.Slider(new GUIContent("�� ɫ ƫ ��"), shiftV.floatValue, -200f, 200f);
                SerializedProperty level = property.FindPropertyRelative("level");
                level.floatValue = EditorGUILayout.Slider(new GUIContent("ɫ �� �� ��"), level.floatValue, 1f, 255f);
                EditorGUILayout.EndVertical();
                #endregion

                #region Ч������
                EditorGUILayout.LabelField("Ч �� �� ��", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical("box");
                SerializedProperty shiftX = property.FindPropertyRelative("shiftX");
                shiftX.floatValue = EditorGUILayout.Slider(new GUIContent("�� �� ƫ ��"), shiftX.floatValue, 0f, 1000f);
                SerializedProperty shiftY = property.FindPropertyRelative("shiftY");
                shiftY.floatValue = EditorGUILayout.Slider(new GUIContent("�� �� ƫ ��"), shiftY.floatValue, 0f, 1000f);
                EditorGUILayout.LabelField("�� �� ɨ ��", EditorStyles.boldLabel);
                SerializedProperty interlacedEnable = property.FindPropertyRelative("interlacedEnable");
                EditorGUILayout.PropertyField(interlacedEnable, new GUIContent("�� �� �� �� ɨ ��"));
                SerializedProperty interlaced = property.FindPropertyRelative("interlaced");
                interlaced.intValue = EditorGUILayout.IntSlider(new GUIContent("�� �� ɨ ��"), interlaced.intValue, 0, 8);
                SerializedProperty interlacedLine = property.FindPropertyRelative("interlacedLine");
                interlacedLine.intValue = EditorGUILayout.IntSlider(new GUIContent("�� �� ɨ �� �� ��"), interlacedLine.intValue, 2, 8);
                SerializedProperty interlacedLight = property.FindPropertyRelative("interlacedLight");
                interlacedLight.floatValue = EditorGUILayout.Slider(new GUIContent("�� �� ɨ �� �� �� ��"), interlacedLight.floatValue, 0f, 1f);
                EditorGUILayout.LabelField("Ư ��", EditorStyles.boldLabel);
                SerializedProperty transposeXEnable = property.FindPropertyRelative("transposeXEnable");
                EditorGUILayout.PropertyField(transposeXEnable, new GUIContent("�� �� Ư ��"));
                SerializedProperty transposeX = property.FindPropertyRelative("transposeX");
                transposeX.floatValue = EditorGUILayout.Slider(new GUIContent("�� �� Ư ��"), transposeX.floatValue, 0f, 10f);
                SerializedProperty transposePow = property.FindPropertyRelative("transposePow");
                transposePow.floatValue = EditorGUILayout.Slider(new GUIContent("�� �� Ư �� �� �� ��"), transposePow.floatValue, 1f, 8f);
                SerializedProperty transposeNoise = property.FindPropertyRelative("transposeNoise");
                transposeNoise.floatValue = EditorGUILayout.Slider(new GUIContent("�� �� Ư �� �� ��"), transposeNoise.floatValue, 0f, 2f);
                EditorGUILayout.EndVertical();
                #endregion

                #region ��������
                EditorGUILayout.LabelField("�� �� �� ��", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical("box");
                SerializedProperty qualityEnable = property.FindPropertyRelative("qualityEnable");
                EditorGUILayout.PropertyField(qualityEnable, new GUIContent("�� �� �� �� �� ��"));
                SerializedProperty lightNoise = property.FindPropertyRelative("lightNoise");
                lightNoise.floatValue = EditorGUILayout.Slider(new GUIContent("�� �� �� ��"), lightNoise.floatValue, 0f, 500f);
                SerializedProperty darkNoise = property.FindPropertyRelative("darkNoise");
                darkNoise.floatValue = EditorGUILayout.Slider(new GUIContent("�� Ƭ �� ��"), darkNoise.floatValue, 0f, 500f);
                EditorGUILayout.EndVertical();
                #endregion

                #region LED����
                EditorGUILayout.LabelField("LED �� ��", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical("box");
                SerializedProperty LEDResolutionEnable = property.FindPropertyRelative("LEDResolutionEnable");
                EditorGUILayout.PropertyField(LEDResolutionEnable, new GUIContent("LED �� ��"));
                SerializedProperty LEDResolutionLevel = property.FindPropertyRelative("LEDResolutionLevel");
                LEDResolutionLevel.intValue = EditorGUILayout.IntSlider(new GUIContent("LED �� �� �� �� ��"), LEDResolutionLevel.intValue, 0, 10);
                EditorGUILayout.EndVertical();
                #endregion

                #region ��������
                EditorGUILayout.LabelField("�� �� �� ��", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical("box");
                SerializedProperty FishEyeEnable = property.FindPropertyRelative("FishEyeEnable");
                EditorGUILayout.PropertyField(FishEyeEnable, new GUIContent("�� �� �� ��"));
                SerializedProperty FishEyeIntensity_X = property.FindPropertyRelative("FishEyeIntensity_X");
                FishEyeIntensity_X.floatValue = EditorGUILayout.Slider(new GUIContent("�� �� �� �� ǿ ��"), FishEyeIntensity_X.floatValue, 0, 1);
                SerializedProperty FishEyeIntensity_Y = property.FindPropertyRelative("FishEyeIntensity_Y");
                FishEyeIntensity_Y.floatValue = EditorGUILayout.Slider(new GUIContent("�� �� �� �� ǿ ��"), FishEyeIntensity_Y.floatValue, 0, 1);

                SerializedProperty FishEyePow = property.FindPropertyRelative("FishEyePow");
                FishEyePow.floatValue = EditorGUILayout.Slider(new GUIContent("�� �� �� ��"), FishEyePow.floatValue, 0, 10);

                EditorGUILayout.EndVertical();
                #endregion

                #region ˮӡ����
                EditorGUILayout.LabelField("ˮ ӡ �� ��", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical("box");
                SerializedProperty WaterMarkEnable = property.FindPropertyRelative("WaterMarkEnable");
                EditorGUILayout.PropertyField(WaterMarkEnable, new GUIContent("�� �� ˮ ӡ"));
                SerializedProperty MarkTextureRect = property.FindPropertyRelative("MarkTextureRect");
                var rect = Vec4ToVec2(MarkTextureRect.vector4Value);
                rect.a = EditorGUILayout.Vector2Field(new GUIContent("ˮ ӡ ƫ ��"), rect.a);
                rect.b = EditorGUILayout.Vector2Field(new GUIContent("ˮ ӡ �� ��"), rect.b);
                MarkTextureRect.vector4Value = Vec2ToVec4(rect.a, rect.b);
                SerializedProperty MarkTextureAlpha = property.FindPropertyRelative("MarkTextureAlpha");
                MarkTextureAlpha.floatValue = EditorGUILayout.Slider(new GUIContent("ˮ ӡ ͸ �� ��"), MarkTextureAlpha.floatValue, 0, 1);

                SerializedProperty MarkTexture = property.FindPropertyRelative("MarkTexture");
                MarkTexture.objectReferenceValue = EditorGUILayout.ObjectField(new GUIContent("ˮ ӡ �� ͼ"), MarkTexture.objectReferenceValue, typeof(Texture2D), allowSceneObjects: false);
                /*   SerializedProperty MarkTextPrefab = property.FindPropertyRelative("MarkTextPrefab");
                   MarkTextPrefab.objectReferenceValue = EditorGUILayout.ObjectField(new GUIContent("ˮӡ��ͼ"), MarkTextPrefab.objectReferenceValue, typeof(GameObject), allowSceneObjects: false);
                   SerializedProperty MarkTextFiled = property.FindPropertyRelative("MarkTextFiled");
                   MarkTextFiled.stringValue = EditorGUILayout.TextField(new GUIContent("ˮӡ��ͼ"), MarkTextFiled.stringValue);
       */
                ;

                EditorGUILayout.EndVertical();
                #endregion

                #region ��������
                EditorGUILayout.LabelField("�� �� �� ��", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical("box");
                SerializedProperty emphasizeLines = property.FindPropertyRelative("emphasizeLines");
                EditorGUILayout.PropertyField(emphasizeLines, new GUIContent("ǿ �� �� ��"));
                SerializedProperty convoluteType = property.FindPropertyRelative("convoluteType");
                EditorGUILayout.PropertyField(convoluteType, new GUIContent("�� �� �� ��"));
                SerializedProperty snowEffect = property.FindPropertyRelative("snowEffect");
                EditorGUILayout.PropertyField(snowEffect, new GUIContent("�� �� �� ��"));
                SerializedProperty invertLight = property.FindPropertyRelative("invertLight");
                EditorGUILayout.PropertyField(invertLight, new GUIContent("�� �� �� ��"));
                EditorGUILayout.EndVertical();
                #endregion

                EditorGUI.EndProperty();
            }

        }
#endif
        /// <inheritdoc/>
        public override void Create()
        {
            m_ScriptablePass = new VaporwaverPass(m_Setting, shaderSetting);

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


}