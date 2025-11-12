using UnityEditor;
using UnityEngine;
using System.IO;

public class ImageCropAndMergeTool : EditorWindow
{
    private Texture2D leftImage;
    private Texture2D rightImage;
    private Texture2D resultImage;
    private string savePath = "Assets/";

    [MenuItem("Tools/图片裁剪拼接工具")]
    public static void ShowWindow()
    {
        GetWindow<ImageCropAndMergeTool>("图片裁剪拼接工具");
    }

    void OnGUI()
    {
        GUILayout.Label("图片裁剪拼接工具", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        // 左右图片选择
        leftImage = (Texture2D)EditorGUILayout.ObjectField("左图片", leftImage, typeof(Texture2D), false);
        rightImage = (Texture2D)EditorGUILayout.ObjectField("右图片", rightImage, typeof(Texture2D), false);
        EditorGUILayout.Space();
        // 保存路径
        EditorGUILayout.BeginHorizontal();
        savePath = EditorGUILayout.TextField("保存路径", savePath);
        if (GUILayout.Button("浏览", GUILayout.Width(60)))
        {
            string selectedPath = EditorUtility.SaveFolderPanel("选择保存目录", savePath, "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                savePath = "Assets" + selectedPath.Replace(Application.dataPath, "");
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        // 处理按钮
        if (GUILayout.Button("裁剪并拼接图片", GUILayout.Height(30)))
        {
            if (leftImage == null || rightImage == null)
            {
                EditorUtility.DisplayDialog("错误", "请先选择左右图片", "确定");
                return;
            }

            ProcessImages();
        }

        EditorGUILayout.Space();
        // 显示结果
        if (resultImage != null)
        {
            GUILayout.Label("处理结果:", EditorStyles.boldLabel);
            Rect previewRect = GUILayoutUtility.GetAspectRect((float)resultImage.width / resultImage.height);
            EditorGUI.DrawPreviewTexture(previewRect, resultImage);

            if (GUILayout.Button("保存图片"))
            {
                SaveImage();
            }
        }
    }

    private void ProcessImages()
    {
        SetTextureReadable(leftImage);
        SetTextureReadable(rightImage);
        int leftHalfWidth = leftImage.width / 2;
        int rightHalfWidth = rightImage.width / 2;
        // 使用两张图片中较大的高度作为结果图片的高度
        int resultHeight = Mathf.Max(leftImage.height, rightImage.height);
        resultImage = new Texture2D(leftHalfWidth + rightHalfWidth, resultHeight, TextureFormat.ARGB32, false);
        // 处理左半部分（从左图片取左半部分）
        for (int y = 0; y < resultHeight; y++)
        {
            for (int x = 0; x < leftHalfWidth; x++)
            {
                if (y < leftImage.height)
                {
                    Color pixel = leftImage.GetPixel(x, y);
                    resultImage.SetPixel(x, y, pixel);
                }
                else
                {
                    resultImage.SetPixel(x, y, Color.clear);
                }
            }
        }
        for (int y = 0; y < resultHeight; y++)
        {
            for (int x = 0; x < rightHalfWidth; x++)
            {
                int sourceX = rightImage.width - rightHalfWidth + x;

                if (y < rightImage.height)
                {
                    Color pixel = rightImage.GetPixel(sourceX, y);
                    resultImage.SetPixel(leftHalfWidth + x, y, pixel);
                }
                else
                {
                    resultImage.SetPixel(leftHalfWidth + x, y, Color.clear);
                }
            }
        }

        resultImage.Apply();
        Repaint();
    }

    private void SaveImage()
    {
        if (resultImage == null)
        {
            EditorUtility.DisplayDialog("错误", "没有可保存的图片", "确定");
            return;
        }

        // 确保目录存在
        if (!Directory.Exists(savePath))
        {
            Directory.CreateDirectory(savePath);
        }

        // 保存图片
        string filePath = Path.Combine(savePath, "MergedImage.png");
        byte[] bytes = resultImage.EncodeToPNG();
        File.WriteAllBytes(filePath, bytes);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("成功", $"图片已保存至: {filePath}", "确定");
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