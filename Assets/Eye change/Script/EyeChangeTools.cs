#if UNITY_EDITOR
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using VRC.SDKBase;
#if VRC_SDK_VRCSDK3
using UnityEditor;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
#endif
public static class EyeChangeTools
{
#if VRC_SDK_VRCSDK3
    public static void PreviewMerge(Texture2D bottomLayerTexture, Texture2D topLayerTexture, ref Texture2D previewTexture)
    {
        // 确保纹理可读写
        if (bottomLayerTexture != null)
            SetTextureReadable(bottomLayerTexture);
        SetTextureReadable(topLayerTexture);

        int width, height;

        if (bottomLayerTexture == null)
        {
            width = topLayerTexture.width;
            height = topLayerTexture.height;
            previewTexture = new Texture2D(width, height, TextureFormat.ARGB32, false);
            previewTexture.SetPixels(topLayerTexture.GetPixels());
        }
        else
        {
            width = Mathf.Max(bottomLayerTexture.width, topLayerTexture.width);
            height = Mathf.Max(bottomLayerTexture.height, topLayerTexture.height);
            previewTexture = new Texture2D(width, height, TextureFormat.ARGB32, false);
            // 合并像素
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color bottomColor = GetPixelSafe(bottomLayerTexture, x, y);
                    Color topColor = GetPixelSafe(topLayerTexture, x, y);
                    Color finalColor = Color.Lerp(bottomColor, topColor, topColor.a);
                    previewTexture.SetPixel(x, y, finalColor);
                }
            }
        }

        previewTexture.Apply();
    }

    public static List<Material> LoadGeneratedMaterials(string parameterName)
    {
        List<Material> materials = new List<Material>();
        // 获取保存材质路径
        string materialsPath = TextureMergeTool.GetMaterialsSavePath(parameterName);
        if (!Directory.Exists(materialsPath))
        {
            EditorUtility.DisplayDialog("Error", $"材质文件夹不存在，请先生成材质。路径: {materialsPath}", "确定");
            return materials;
        }
        // 加载文件夹下的所有材质
        string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { materialsPath });
        foreach (string guid in materialGuids)
        {
            string materialPath = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (mat != null)
            {
                materials.Add(mat);
            }
        }

        EditorUtility.DisplayDialog("Success", $"已加载 {materials.Count} 个材质", "确定");
        return materials;
    }

    public static List<SkinnedMeshRenderer> FindBodyMeshRenderers(GameObject avatar)
    {
        var renderers = avatar.GetComponentsInChildren<SkinnedMeshRenderer>();
        List<SkinnedMeshRenderer> bodyRenderers = new List<SkinnedMeshRenderer>();

        foreach (var renderer in renderers)
        {
            if (renderer.name.ToLower().Contains("body"))
            {
                bodyRenderers.Add(renderer);
            }
        }
        return bodyRenderers;
    }

    public static void CreateMaterialSwitchSystem(GameObject avatar, List<SkinnedMeshRenderer> bodyMeshRenderers, List<Material> materials, string parameterName)
    {
        // 获取avatar描述符
        var descriptor = avatar.GetComponent<VRCAvatarDescriptor>();
        if (descriptor == null)
        {
            EditorUtility.DisplayDialog("Error", "模型没有VRCAvatarDescriptor组件", "确定");
            return;
        }

        if (bodyMeshRenderers == null || bodyMeshRenderers.Count == 0)
        {
            EditorUtility.DisplayDialog("Error", "请指定至少一个Body网格渲染器", "确定");
            return;
        }

        string scriptPath = GetScriptPath();
        string parentPath = Directory.GetParent(scriptPath).FullName;
        string savePath = Path.Combine(parentPath, "Saves", parameterName, "EyeChangeSystem");
        savePath = savePath.Replace('\\', '/');
        string relativeSavePath = GetRelativeAssetPath(savePath);
        if (!Directory.Exists(savePath)) Directory.CreateDirectory(savePath);

        var animationClip = CreateAnimationClip(relativeSavePath, avatar, bodyMeshRenderers, materials);
        ConfigureFXLayer(descriptor, relativeSavePath, animationClip, parameterName);
        ConfigureParameters(descriptor, relativeSavePath, parameterName);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Success", "眼睛切换配置已创建完成! (菜单配置需要手动完成)", "确定");
    }

    private static AnimationClip CreateAnimationClip(string savePath, GameObject avatar, List<SkinnedMeshRenderer> bodyMeshRenderers, List<Material> materials)
    {
        string animPath = savePath + "/Animations";
        if (!Directory.Exists(animPath))
        {
            Directory.CreateDirectory(animPath);
        }
        AnimationClip clip = new AnimationClip();
        clip.name = "EyeChange_Material_Blend";
        float timeInterval = materials.Count > 1 ? 1.0f / (materials.Count - 1) : 0;
        foreach (var bodyMeshRenderer in bodyMeshRenderers)
        {
            string path = GetGameObjectPath(bodyMeshRenderer.gameObject, avatar.transform);
            EditorCurveBinding binding = new EditorCurveBinding
            {
                path = path,
                type = typeof(SkinnedMeshRenderer),
                propertyName = "m_Materials.Array.data[0]"
            };

            List<ObjectReferenceKeyframe> keyframes = new List<ObjectReferenceKeyframe>();

            for (int i = 0; i < materials.Count; i++)
            {
                float time = i * timeInterval;
                keyframes.Add(new ObjectReferenceKeyframe
                {
                    time = time,
                    value = materials[i]
                });
            }

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes.ToArray());
        }

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        // 保存
        string animFilePath = animPath + "/EyeChange_Material_Blend.anim";
        AssetDatabase.CreateAsset(clip, animFilePath);
        return clip;
    }

    private static void ConfigureFXLayer(VRCAvatarDescriptor descriptor, string savePath, AnimationClip clip, string parameterName)
    {
        AnimatorController fxController = null;

        for (int i = 0; i < descriptor.baseAnimationLayers.Length; i++)
        {
            if (descriptor.baseAnimationLayers[i].type == VRCAvatarDescriptor.AnimLayerType.FX)
            {
                fxController = descriptor.baseAnimationLayers[i].animatorController as AnimatorController;
                break;
            }
        }

        if (fxController == null)
        {
            fxController = new AnimatorController();
            fxController.name = "FX_EyeChange";
            for (int i = 0; i < descriptor.baseAnimationLayers.Length; i++)
            {
                if (descriptor.baseAnimationLayers[i].type == VRCAvatarDescriptor.AnimLayerType.FX)
                {
                    descriptor.baseAnimationLayers[i].animatorController = fxController;
                    break;
                }
            }

            string controllerPath = savePath + "/" + fxController.name + ".controller";
            AssetDatabase.CreateAsset(fxController, controllerPath);
        }
        // 添加参数
        bool hasParameter = false;
        foreach (var param in fxController.parameters)
        {
            if (param.name == parameterName)
            {
                hasParameter = true;
                break;
            }
        }
        if (!hasParameter)
        {
            fxController.AddParameter(parameterName, AnimatorControllerParameterType.Float);
        }
        // 创建图层
        AnimatorControllerLayer layer = new AnimatorControllerLayer
        {
            name = "EyeChange",
            defaultWeight = 1f
        };

        AnimatorStateMachine stateMachine = new AnimatorStateMachine
        {
            name = "EyeChange",
            hideFlags = HideFlags.HideInHierarchy
        };

        AssetDatabase.AddObjectToAsset(stateMachine, AssetDatabase.GetAssetPath(fxController));
        AnimatorState state = stateMachine.AddState("EyeChangeState");
        state.motion = clip;
        stateMachine.defaultState = state;
        // 启用Motion Time并关联参数
        state.timeParameterActive = true;
        state.timeParameter = parameterName;

        layer.stateMachine = stateMachine;
        fxController.AddLayer(layer);

        EditorUtility.SetDirty(fxController);
    }

    private static void ConfigureParameters(VRCAvatarDescriptor descriptor, string savePath, string parameterName)
    {
        VRCExpressionParameters expressionParams = descriptor.expressionParameters;

        if (expressionParams == null)
        {
            expressionParams = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            expressionParams.name = "ExpressionParameters_EyeChange";
            string paramsPath = savePath + "/" + expressionParams.name + ".asset";
            AssetDatabase.CreateAsset(expressionParams, paramsPath);
            descriptor.expressionParameters = expressionParams;
        }

        // 检查是否已存在参数
        bool hasParameter = false;
        foreach (var param in expressionParams.parameters)
        {
            if (param.name == parameterName)
            {
                hasParameter = true;
                break;
            }
        }

        if (!hasParameter)
        {
            // 添加新参数
            var parameters = new List<VRCExpressionParameters.Parameter>(expressionParams.parameters);
            parameters.Add(new VRCExpressionParameters.Parameter
            {
                name = parameterName,
                valueType = VRCExpressionParameters.ValueType.Float,
                defaultValue = 0f,
                saved = true
            });

            expressionParams.parameters = parameters.ToArray();
            EditorUtility.SetDirty(expressionParams);
        }
    }

    private static string GetGameObjectPath(GameObject obj, Transform root)
    {
        string path = obj.name;
        Transform parent = obj.transform.parent;

        while (parent != null && parent != root)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }
#endif

    private static Color GetPixelSafe(Texture2D tex, int x, int y)
    {
        if (tex == null || x < 0 || x >= tex.width || y < 0 || y >= tex.height)
            return Color.clear;

        return tex.GetPixel(x, y);
    }

    private static void SetTextureReadable(Texture2D texture)
    {
        if (texture == null) return;

        string path = AssetDatabase.GetAssetPath(texture);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

        if (importer != null && !importer.isReadable)
        {
            importer.isReadable = true;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
    }

    private static string GetScriptPath()
    {
        // 获取当前脚本的路径
        string[] guids = AssetDatabase.FindAssets("EyeChangeTools");
        if (guids.Length > 0)
        {
            string scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            return Path.GetDirectoryName(scriptPath);
        }
        return "Assets";
    }

    private static string GetRelativeAssetPath(string absolutePath)
    {
        string assetsPath = Application.dataPath;
        if (absolutePath.StartsWith(assetsPath))
        {
            return "Assets" + absolutePath.Substring(assetsPath.Length).Replace('\\', '/');
        }
        int assetsIndex = absolutePath.IndexOf("/Assets/");
        if (assetsIndex >= 0)
        {
            return absolutePath.Substring(assetsIndex + 1).Replace('\\', '/');
        }
        return absolutePath.Replace('\\', '/');
    }
}
#endif