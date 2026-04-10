using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites; // Required for modern slicing
using System.Collections.Generic;

public class SpriteSheetSlicer : EditorWindow
{
    private int columns = 28;
    private int rows = 15;

    [MenuItem("Tools/Custom Sprite Slicer")]
    public static void ShowWindow() => GetWindow<SpriteSheetSlicer>("Sprite Slicer");

    private void OnGUI()
    {
        GUILayout.Label("Slice Settings", EditorStyles.boldLabel);
        columns = EditorGUILayout.IntField("Columns", columns);
        rows = EditorGUILayout.IntField("Rows", rows);

        if (GUILayout.Button("Slice Selected Texture")) SliceTexture();
    }

    private void SliceTexture()
    {
        Texture2D tex = Selection.activeObject as Texture2D;
        if (!tex) { Debug.LogError("Select a Texture first!"); return; }

        string path = AssetDatabase.GetAssetPath(tex);
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var dataProvider = factory.GetSpriteEditorDataProviderFromObject(AssetImporter.GetAtPath(path));
        dataProvider.InitSpriteEditorDataProvider();

        // Set Import Settings
        var texImporter = (TextureImporter)dataProvider.targetObject;
        texImporter.spriteImportMode = SpriteImportMode.Multiple;

        // Calculate Rects
        float sliceW = tex.width / (float)columns;
        float sliceH = tex.height / (float)rows;
        List<SpriteRect> newSprites = new List<SpriteRect>();

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                newSprites.Add(new SpriteRect()
                {
                    name = $"{tex.name}_{r}_{c}",
                    rect = new Rect(c * sliceW, (rows - 1 - r) * sliceH, sliceW, sliceH),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f)
                });
            }
        }

        // Apply metadata
        var spriteRects = dataProvider.GetSpriteRects(); // This is just to satisfy the API
        dataProvider.SetSpriteRects(newSprites.ToArray());
        dataProvider.Apply();

        // Reimport
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        Debug.Log($"Sliced {tex.name} successfully!");
    }
}