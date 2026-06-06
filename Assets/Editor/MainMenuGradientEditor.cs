using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Ventana para editar el gradiente radial del fondo del Menú Principal.
/// Menú: ChemiTech → Editar Gradiente de Fondo
/// </summary>
public class MainMenuGradientEditor : EditorWindow
{
    const string TEX_PATH = "Assets/Sprites/MainMenuBG.png";
    const int W = 512, H = 288;

    // ── Parámetros editables ──────────────────────────────────────────────────
    Gradient gradient = new Gradient();

    [Range(0f, 1f)] float centerX = 0.5f;
    [Range(0f, 1f)] float centerY = 0.6f; // 0=abajo, 1=arriba (Unity flip)
    [Range(0.3f, 2f)] float radiusX = 0.71f;
    [Range(0.3f, 2f)] float radiusY = 0.85f;

    Texture2D preview;
    SerializedObject so;

    // ── Abrir ventana ─────────────────────────────────────────────────────────
    [MenuItem("ChemiTech/Editar Gradiente de Fondo")]
    static void Open()
    {
        var win = GetWindow<MainMenuGradientEditor>("Gradiente de Fondo");
        win.minSize = new Vector2(380f, 420f);
        win.Init();
        win.Show();
    }

    void Init()
    {
        // Gradiente por defecto = el del Figma
        var colorKeys = new GradientColorKey[]
        {
            new GradientColorKey(new Color(1.000f, 0.933f, 0.722f), 0.000f),
            new GradientColorKey(new Color(0.894f, 0.706f, 0.537f), 0.110f),
            new GradientColorKey(new Color(0.788f, 0.478f, 0.353f), 0.220f),
            new GradientColorKey(new Color(0.541f, 0.329f, 0.427f), 0.385f),
            new GradientColorKey(new Color(0.416f, 0.255f, 0.467f), 0.468f),
            new GradientColorKey(new Color(0.290f, 0.180f, 0.502f), 0.550f),
            new GradientColorKey(new Color(0.165f, 0.125f, 0.376f), 0.700f),
            new GradientColorKey(new Color(0.020f, 0.031f, 0.161f), 1.000f),
        };
        var alphaKeys = new GradientAlphaKey[]
        {
            new GradientAlphaKey(1f, 0f),
            new GradientAlphaKey(1f, 1f),
        };
        gradient = new Gradient();
        gradient.SetKeys(colorKeys, alphaKeys);

        GeneratePreview();
    }

    // ── GUI ───────────────────────────────────────────────────────────────────
    void OnGUI()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Gradiente Radial de Fondo", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("El gradiente va del centro hacia los bordes.", MessageType.None);
        EditorGUILayout.Space(4);

        EditorGUI.BeginChangeCheck();

        gradient = EditorGUILayout.GradientField("Gradiente", gradient);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Centro del gradiente", EditorStyles.boldLabel);
        centerX = EditorGUILayout.Slider("Centro X", centerX, 0f, 1f);
        centerY = EditorGUILayout.Slider("Centro Y", centerY, 0f, 1f);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Radio (cuánto se expande)", EditorStyles.boldLabel);
        radiusX = EditorGUILayout.Slider("Radio X", radiusX, 0.3f, 2f);
        radiusY = EditorGUILayout.Slider("Radio Y", radiusY, 0.3f, 2f);

        if (EditorGUI.EndChangeCheck())
            GeneratePreview();

        // Preview
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Vista previa", EditorStyles.boldLabel);
        if (preview != null)
        {
            var rect = GUILayoutUtility.GetRect(position.width - 20f, 120f);
            EditorGUI.DrawPreviewTexture(rect, preview, null, ScaleMode.ScaleToFit);
        }

        EditorGUILayout.Space(8);
        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("✓  Aplicar al fondo del Menú", GUILayout.Height(36)))
            ApplyToScene();
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(4);
        if (GUILayout.Button("↺  Restaurar colores del Figma"))
            Init();
    }

    // ── Generar textura de preview ────────────────────────────────────────────
    void GeneratePreview()
    {
        if (preview == null)
            preview = new Texture2D(W / 2, H / 2, TextureFormat.RGB24, false);

        int pw = preview.width, ph = preview.height;
        for (int y = 0; y < ph; y++)
        {
            for (int x = 0; x < pw; x++)
            {
                float u = (float)x / (pw - 1);
                float v = (float)y / (ph - 1);
                float dist = EllipticDist(u, v);
                preview.SetPixel(x, ph - 1 - y, gradient.Evaluate(dist));
            }
        }
        preview.Apply();
        Repaint();
    }

    // ── Aplicar a la escena ───────────────────────────────────────────────────
    void ApplyToScene()
    {
        // Generar textura completa
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float u = (float)x / (W - 1);
                float v = (float)y / (H - 1);
                float dist = EllipticDist(u, v);
                tex.SetPixel(x, H - 1 - y, gradient.Evaluate(dist));
            }
        }
        tex.Apply();

        // Guardar PNG
        File.WriteAllBytes(TEX_PATH, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(TEX_PATH);

        // Configurar importador como Sprite
        var importer = (TextureImporter)AssetImporter.GetAtPath(TEX_PATH);
        importer.textureType      = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled    = false;
        importer.filterMode       = FilterMode.Bilinear;
        importer.wrapMode         = TextureWrapMode.Clamp;
        importer.SaveAndReimport();

        // Buscar el Image "Background" en la escena y actualizar su sprite
        var bgImg = GameObject.Find("Background")?.GetComponent<UnityEngine.UI.Image>();
        if (bgImg != null)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TEX_PATH);
            bgImg.sprite = sprite;
            bgImg.color  = Color.white;
            EditorUtility.SetDirty(bgImg);
            UnityEditor.SceneManagement.EditorSceneManager
                .MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[GradientEditor] ✓ Fondo actualizado. Guarda la escena con Ctrl+S.");
            EditorUtility.DisplayDialog("¡Listo!", "Fondo actualizado.\nGuarda con Ctrl+S.", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Aviso",
                "No se encontró el objeto 'Background' en la escena.\n" +
                "Corre primero 'Build Main Menu Scene'.", "OK");
        }
    }

    // Distancia elíptica normalizada desde el centro (0=centro, 1=borde)
    float EllipticDist(float u, float v)
    {
        float dx = (u - centerX) / radiusX;
        float dy = (v - centerY) / radiusY;
        return Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
    }

    void OnDisable()
    {
        if (preview != null)
            DestroyImmediate(preview);
    }
}
