#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
#if VRC_SDK_VRCSDK3
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
#endif

public class EyeChangeToolsWindow : EditorWindow
{
    private int selectedToolIndex = 0;
    private string[] toolNames;
    // 语言选择
    private enum Language { Chinese, English, Japanese }
    private Language currentLanguage = Language.Chinese;
    private Dictionary<string, string[]> localizedStrings = new Dictionary<string, string[]>
    {
        {"PreviewTool", new string[] {"预览合成图片", "Preview Image", "画像合成プレビュー"}},
        {"MaterialSwitchTool", new string[] {"眼睛切换工具", "Eye Change Tool", "目切り替えツール"}},
        {"HeterochromiaTool", new string[] {"异瞳制作工具", "Heterochromia Tool", "オッドアイ制作ツール"}},
        {"BottomImage", new string[] {"底部图片", "Bottom Image", "ベース画像"}},
        {"TopImage", new string[] {"顶层图片", "Top Image", "トップ画像"}},
        {"PreviewButton", new string[] {"合并预览", "Preview Merge", "合成プレビュー"}},
        {"PreviewResult", new string[] {"预览结果:", "Preview Result:", "プレビュー結果:"}},
        {"ImageMergeTool", new string[] {"合并工具", "Merge Tool", "合成ツール"}},
        {"TopImageList", new string[] {"顶层图片列表:", "Top Image List:", "トップ画像リスト:"}},
        {"GenerateButton", new string[] {"生成", "Generate", "生成"}},
        {"MaterialSwitchConfig", new string[] {"模型应用", "Model Application", "モデル適用"}},
        {"Avatar", new string[] {"模型", "Avatar", "アバター"}},
        {"BodyMeshRenderer", new string[] {"Body网格渲染器", "Body Mesh Renderer", "ボディメッシュレンダラー"}},
        {"BodyMeshRenderers", new string[] {"Body网格渲染器列表", "Body Mesh Renderers", "ボディメッシュレンダラーリスト"}},
        {"LoadMaterials", new string[] {"加载生成的眼睛材质", "Load Generated Eye Materials", "生成された目のマテリアルを読み込み"}},
        {"LoadedMaterials", new string[] {"已加载眼睛材质数量:", "Loaded Eye Materials Count:", "読み込まれた目のマテリアル数:"}},
        {"ParameterName", new string[] {"参数名称", "Parameter Name", "パラメーター名"}},
        {"GenerateSystem", new string[] {"生成眼睛切换参数", "Generate Eye Change Parameters", "目切り替えパラメーターを生成"}},
        {"VRChatRequired", new string[] {"此功能需要VRChat SDK3支持", "This feature requires VRChat SDK3", "この機能にはVRChat SDK3が必要です"}},
        {"InstallVRChat", new string[] {"请先安装VRChat SDK3以使用材质切换功能", "Please install VRChat SDK3 to use material switching", "マテリアル切り替え機能を使用するにはVRChat SDK3をインストールしてください"}},
        {"Error", new string[] {"错误", "Error", "エラー"}},
        {"SelectBothImages", new string[] {"请先选择底部和顶部图片", "Please select both bottom and top images", "ベース画像とトップ画像を選択してください"}},
        {"SelectBottomImage", new string[] {"请先选择底部图片并添加至少一个顶层图片", "Please select a bottom image and add at least one top image", "ベース画像を選択し、少なくとも1つのトップ画像を追加してください"}},
        {"SelectBodyMesh", new string[] {"请先指定至少一个Body网格渲染器", "Please specify at least one Body Mesh Renderer", "少なくとも1つのボディメッシュレンダラーを指定してください"}},
        {"AddMaterial", new string[] {"请添加至少一个材质", "Please add at least one material", "少なくとも1つのマテリアルを追加してください"}},
        {"Success", new string[] {"成功", "Success", "成功"}},
        {"MaterialsLoaded", new string[] {"已加载 {0} 个材质", "Loaded {0} materials", "{0}個のマテリアルを読み込みました"}},
        {"SystemCreated", new string[] {"眼睛参数已创建完成! (菜单配置需要手动完成)", "Eye parameters created! (Menu configuration needs to be done manually)", "目のパラメーターを作成しました！(メニュー設定は手動で行う必要があります)"}},
        {"Language", new string[] {"语言", "Language", "言語"}},
        {"By", new string[] {"By: solaier", "By: solaier", "By: solaier"}},
        {"LeftImage", new string[] {"左眼", "Left Eye", "左目"}},
        {"RightImage", new string[] {"右眼", "Right Eye", "右目"}},
        {"SavePath", new string[] {"保存路径", "Save Path", "保存パス"}},
        {"Browse", new string[] {"浏览", "Browse", "参照"}},
        {"CropAndMergeButton", new string[] {"裁剪并拼接图片", "Crop and Merge Images", "画像を切り取って結合"}},
        {"SaveImageButton", new string[] {"保存图片", "Save Image", "画像を保存"}},
        {"ProcessResult", new string[] {"处理结果:", "Process Result:", "処理結果:"}}
    };

    private Texture2D bottomLayerTexture;
    private Texture2D topLayerTexture;
    private Texture2D previewTexture;

    [SerializeField]
    private List<Texture2D> topLayerTextures = new List<Texture2D>();

    [SerializeField]
    private List<SkinnedMeshRenderer> bodyMeshRenderers = new List<SkinnedMeshRenderer>();

    private SerializedObject serializedObject;
    private SerializedProperty topLayersProperty;
    private SerializedProperty bodyMeshRenderersProperty;

    // 异瞳制作工具字段
    private Texture2D heterochromiaLeftImage;
    private Texture2D heterochromiaRightImage;
    private Texture2D heterochromiaResultImage;
    private string heterochromiaSavePath = "Assets/";

#if VRC_SDK_VRCSDK3
    private GameObject avatar;
    private List<Material> materials = new List<Material>();
#endif

    private string parameterName = "Eyechange";
    private GUIStyle bylineStyle;

    [MenuItem("Eye Change/眼睛切换工具")]
    public static void ShowWindow()
    {
        GetWindow<EyeChangeToolsWindow>("眼睛切换工具");
    }

    private void OnEnable()
    {
        serializedObject = new SerializedObject(this);
        topLayersProperty = serializedObject.FindProperty("topLayerTextures");
        bodyMeshRenderersProperty = serializedObject.FindProperty("bodyMeshRenderers");
        UpdateToolNames();
        bylineStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
        bylineStyle.fontSize = 12;
        bylineStyle.alignment = TextAnchor.MiddleCenter;
    }

    private void UpdateToolNames()
    {
        toolNames = new string[] {
            GetLocalizedString("PreviewTool"),
            GetLocalizedString("MaterialSwitchTool"),
            GetLocalizedString("HeterochromiaTool")
        };
    }

    private string GetLocalizedString(string key)
    {
        if (localizedStrings.ContainsKey(key))
        {
            return localizedStrings[key][(int)currentLanguage];
        }
        return key;
    }

    void OnGUI()
    {
        // 语言下拉菜单
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        EditorGUI.BeginChangeCheck();
        currentLanguage = (Language)EditorGUILayout.EnumPopup(GetLocalizedString("Language"), currentLanguage, GUILayout.Width(200));
        if (EditorGUI.EndChangeCheck())
        {
            UpdateToolNames();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        selectedToolIndex = GUILayout.Toolbar(selectedToolIndex, toolNames);

        switch (selectedToolIndex)
        {
            case 0:
                DrawTexturePreviewTool();
                break;
            case 1:
                DrawMaterialSwitchTool();
                break;
            case 2:
                DrawHeterochromiaTool();
                break;
        }
        // By：solaier
        EditorGUILayout.Space();
        EditorGUILayout.Space();
        GUILayout.Label(GetLocalizedString("By"), bylineStyle);
    }

    private void DrawTexturePreviewTool()
    {
        GUILayout.Label(GetLocalizedString("PreviewTool"), EditorStyles.boldLabel);
        // 底部
        EditorGUILayout.Space();
        bottomLayerTexture = (Texture2D)EditorGUILayout.ObjectField(GetLocalizedString("BottomImage"), bottomLayerTexture, typeof(Texture2D), false);
        // 顶部
        EditorGUILayout.Space();
        topLayerTexture = (Texture2D)EditorGUILayout.ObjectField(GetLocalizedString("TopImage"), topLayerTexture, typeof(Texture2D), false);
        // 预览
        EditorGUILayout.Space();
        if (GUILayout.Button(GetLocalizedString("PreviewButton"), GUILayout.Height(30)))
        {
            if (topLayerTexture != null)
            {
                EyeChangeTools.PreviewMerge(bottomLayerTexture, topLayerTexture, ref previewTexture);
                Repaint();
            }
            else
            {
                EditorUtility.DisplayDialog(GetLocalizedString("Error"), GetLocalizedString("SelectBothImages"), "OK");
            }
        }
        // 预览区域
        if (previewTexture != null)
        {
            EditorGUILayout.Space();
            GUILayout.Label(GetLocalizedString("PreviewResult"), EditorStyles.boldLabel);

            Rect previewRect = GUILayoutUtility.GetAspectRect((float)previewTexture.width / previewTexture.height);
            EditorGUI.DrawPreviewTexture(previewRect, previewTexture);
        }
    }

    private void DrawMaterialSwitchTool()
    {
#if !VRC_SDK_VRCSDK3
        GUILayout.Label(GetLocalizedString("VRChatRequired"), EditorStyles.boldLabel);
        GUILayout.Label(GetLocalizedString("InstallVRChat"), EditorStyles.helpBox);
        return;
#else
        GUILayout.Label(GetLocalizedString("MaterialSwitchConfig"), EditorStyles.boldLabel);
        EditorGUILayout.Space();
        avatar = (GameObject)EditorGUILayout.ObjectField(GetLocalizedString("Avatar"), avatar, typeof(GameObject), true);

        if (avatar != null)
        {
            EditorGUILayout.Space();
            GUILayout.Label(GetLocalizedString("BodyMeshRenderers"), EditorStyles.boldLabel);

            serializedObject.Update();
            EditorGUILayout.PropertyField(bodyMeshRenderersProperty, true);
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.Space();
            parameterName = EditorGUILayout.TextField(GetLocalizedString("ParameterName"), parameterName);
            // 加载生成的材质
            EditorGUILayout.Space();
            if (GUILayout.Button(GetLocalizedString("LoadMaterials")))
            {
                materials = EyeChangeTools.LoadGeneratedMaterials(parameterName);
            }

            GUILayout.Label(string.Format(GetLocalizedString("LoadedMaterials"), materials.Count));

            EditorGUILayout.Space();
            if (GUILayout.Button(GetLocalizedString("GenerateSystem"), GUILayout.Height(30)))
            {
                if (bodyMeshRenderers == null || bodyMeshRenderers.Count == 0)
                {
                    EditorUtility.DisplayDialog(GetLocalizedString("Error"), GetLocalizedString("SelectBodyMesh"), "OK");
                    return;
                }

                if (materials.Count == 0)
                {
                    EditorUtility.DisplayDialog(GetLocalizedString("Error"), GetLocalizedString("AddMaterial"), "OK");
                    return;
                }

                EyeChangeTools.CreateMaterialSwitchSystem(avatar, bodyMeshRenderers, materials, parameterName);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.Space();
        GUILayout.Label(GetLocalizedString("ImageMergeTool"), EditorStyles.boldLabel);

        EditorGUILayout.Space();
        bottomLayerTexture = (Texture2D)EditorGUILayout.ObjectField(GetLocalizedString("BottomImage"), bottomLayerTexture, typeof(Texture2D), false);
        EditorGUILayout.Space();
        GUILayout.Label(GetLocalizedString("TopImageList"), EditorStyles.boldLabel);
        serializedObject.Update();
        EditorGUILayout.PropertyField(topLayersProperty, true);
        serializedObject.ApplyModifiedProperties();
        EditorGUILayout.Space();
        if (GUILayout.Button(GetLocalizedString("GenerateButton"), GUILayout.Height(30)))
        {
            if (topLayerTextures.Count > 0)
            {
                // 先检查liltoon着色器是否存在
                if (!TextureMergeTool.CheckLiltoonShader())
                {
                    EditorUtility.DisplayDialog(GetLocalizedString("Error"), "未找到liltoon着色器", "OK");
                    return;
                }

                TextureMergeTool.MergeAllLayersAndCreateMaterials(bottomLayerTexture, topLayerTextures, parameterName);
            }
            else
            {
                EditorUtility.DisplayDialog(GetLocalizedString("Error"), GetLocalizedString("SelectBottomImage"), "OK");
            }
        }
#endif
    }

    private void DrawHeterochromiaTool()
    {
        GUILayout.Label(GetLocalizedString("HeterochromiaTool"), EditorStyles.boldLabel);
        EditorGUILayout.Space();
        heterochromiaLeftImage = (Texture2D)EditorGUILayout.ObjectField(GetLocalizedString("LeftImage"), heterochromiaLeftImage, typeof(Texture2D), false);
        heterochromiaRightImage = (Texture2D)EditorGUILayout.ObjectField(GetLocalizedString("RightImage"), heterochromiaRightImage, typeof(Texture2D), false);
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        heterochromiaSavePath = EditorGUILayout.TextField(GetLocalizedString("SavePath"), heterochromiaSavePath);
        if (GUILayout.Button(GetLocalizedString("Browse"), GUILayout.Width(60)))
        {
            string selectedPath = EditorUtility.SaveFolderPanel(GetLocalizedString("SavePath"), heterochromiaSavePath, "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                heterochromiaSavePath = "Assets" + selectedPath.Replace(Application.dataPath, "");
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
        if (GUILayout.Button(GetLocalizedString("CropAndMergeButton"), GUILayout.Height(30)))
        {
            if (heterochromiaLeftImage == null || heterochromiaRightImage == null)
            {
                EditorUtility.DisplayDialog(GetLocalizedString("Error"), GetLocalizedString("SelectBothImages"), "OK");
                return;
            }

            ProcessHeterochromiaImages();
        }
        if (heterochromiaResultImage != null)
        {
            EditorGUILayout.Space();
            if (GUILayout.Button(GetLocalizedString("SaveImageButton"), GUILayout.Height(30)))
            {
                SaveHeterochromiaImage();
            }
        }

        EditorGUILayout.Space();

        if (heterochromiaResultImage != null)
        {
            GUILayout.Label(GetLocalizedString("ProcessResult"), EditorStyles.boldLabel);
            Rect previewRect = GUILayoutUtility.GetAspectRect((float)heterochromiaResultImage.width / heterochromiaResultImage.height);
            EditorGUI.DrawPreviewTexture(previewRect, heterochromiaResultImage);
        }
    }

    private void ProcessHeterochromiaImages()
    {
        // 确保图片可读写
        SetTextureReadable(heterochromiaLeftImage);
        SetTextureReadable(heterochromiaRightImage);
        int leftHalfWidth = heterochromiaLeftImage.width / 2;
        int rightHalfWidth = heterochromiaRightImage.width / 2;
        int resultHeight = Mathf.Max(heterochromiaLeftImage.height, heterochromiaRightImage.height);
        heterochromiaResultImage = new Texture2D(leftHalfWidth + rightHalfWidth, resultHeight, TextureFormat.ARGB32, false);
        for (int y = 0; y < resultHeight; y++)
        {
            for (int x = 0; x < leftHalfWidth; x++)
            {
                if (y < heterochromiaLeftImage.height)
                {
                    Color pixel = heterochromiaLeftImage.GetPixel(x, y);
                    heterochromiaResultImage.SetPixel(x, y, pixel);
                }
                else
                {
                    heterochromiaResultImage.SetPixel(x, y, Color.clear);
                }
            }
        }

        for (int y = 0; y < resultHeight; y++)
        {
            for (int x = 0; x < rightHalfWidth; x++)
            {
                int sourceX = heterochromiaRightImage.width - rightHalfWidth + x;

                if (y < heterochromiaRightImage.height)
                {
                    Color pixel = heterochromiaRightImage.GetPixel(sourceX, y);
                    heterochromiaResultImage.SetPixel(leftHalfWidth + x, y, pixel);
                }
                else
                {
                    heterochromiaResultImage.SetPixel(leftHalfWidth + x, y, Color.clear);
                }
            }
        }

        heterochromiaResultImage.Apply();
        Repaint();
    }

    private void SaveHeterochromiaImage()
    {
        if (heterochromiaResultImage == null)
        {
            EditorUtility.DisplayDialog(GetLocalizedString("Error"), "没有可保存的图片", "OK");
            return;
        }
        if (!Directory.Exists(heterochromiaSavePath))
        {
            Directory.CreateDirectory(heterochromiaSavePath);
        }
        string filePath = Path.Combine(heterochromiaSavePath, "MergedImage.png");
        byte[] bytes = heterochromiaResultImage.EncodeToPNG();
        File.WriteAllBytes(filePath, bytes);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(GetLocalizedString("Success"), $"图片已保存至: {filePath}", "OK");
    }

    private void SetTextureReadable(Texture2D texture)
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
}
#endif