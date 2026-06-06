using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.IO;

/// <summary>
/// Reconstruye los input fields y corrige el layout de LoginScene.
/// Menú: ChemiTech/Fix/Login Layout
/// </summary>
public static class LoginLayoutFix
{
    const float INPUT_W = 784f;
    const float INPUT_H = 96f;

    [MenuItem("ChemiTech/Fix/Login Layout")]
    static void Fix()
    {
        var inner = GameObject.Find("PanelInner");
        if (inner == null)
        {
            EditorUtility.DisplayDialog("Error",
                "No se encontró 'PanelInner'. Abre LoginScene primero.", "OK");
            return;
        }

        var fnt    = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Fredoka-Medium SDF.asset");
        var uiSpr  = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var sprEmail = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Login/icon-email.png");
        var sprLock  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Login/icon-lock.png");
        var sprEye   = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Login/icon-eye.png");
        var sprCyan  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/cyan-button.png");

        // ── Borrar elementos viejos ───────────────────────────────────────────
        DestroyChild(inner, "LabelEmail");
        DestroyChild(inner, "EmailField");
        DestroyChild(inner, "LabelPassword");
        DestroyChild(inner, "PasswordField");
        DestroyChild(inner, "BtnOlvidaste");
        DestroyChild(inner, "BtnIniciarSesion");
        DestroyChild(inner, "BtnRegistrate");

        // Corregir título y beaker
        SetRT(inner, "TitleText",  new Vector2(44f,   248f), new Vector2(360f, 90f));
        SetRT(inner, "BeakerIcon", new Vector2(-186f, 248f), new Vector2(72f,  72f));
        SetRT(inner, "BtnCerrar", new Vector2(388f, 289f), new Vector2(56f, 56f));
        AlignLeft(inner, "TitleText");

        // ── Email label ───────────────────────────────────────────────────────
        // X=0 + left alignment => el texto arranca en -392, alineado con el borde del input
        MakeLabel(inner.transform, fnt, "LabelEmail",
            "Correo electrónico", new Vector2(0f, 139f));

        // ── Email field ───────────────────────────────────────────────────────
        var emailField = MakeInputField(inner.transform, fnt, uiSpr, sprEmail,
            "EmailField", "correo@ejemplo.com",
            new Vector2(0f, 66f),
            TMP_InputField.ContentType.EmailAddress);

        // ── Password label ────────────────────────────────────────────────────
        MakeLabel(inner.transform, fnt, "LabelPassword",
            "Contraseña", new Vector2(0f, -20f));

        // ── Password field ────────────────────────────────────────────────────
        var passField = MakeInputField(inner.transform, fnt, uiSpr, sprLock,
            "PasswordField", "••••••••",
            new Vector2(0f, -93f),
            TMP_InputField.ContentType.Password);

        // Toggle ojo dentro del password field
        var toggleGo = MakeEmpty(passField.gameObject.transform, "BtnToggle");
        SetRTDirect(toggleGo, new Vector2(360f, 0f), new Vector2(38f, 38f));
        var toggleImg = toggleGo.AddComponent<Image>();
        toggleImg.color = new Color(1f, 1f, 1f, 0.65f);
        if (sprEye != null) toggleImg.sprite = sprEye;
        var btnToggle = toggleGo.AddComponent<Button>();
        btnToggle.targetGraphic = toggleImg;

        // ── ¿Olvidaste tu contraseña? ─────────────────────────────────────────
        var forgotGo = MakeEmpty(inner.transform, "BtnOlvidaste");
        SetRTDirect(forgotGo, new Vector2(214f, -174f), new Vector2(270f, 36f));
        var forgotBg = forgotGo.AddComponent<Image>();
        forgotBg.color = Color.clear;
        var forgotTxt = forgotGo.AddComponent<TextMeshProUGUI>();
        forgotTxt.text         = "¿Olvidaste tu contraseña?";
        forgotTxt.font         = fnt;
        forgotTxt.fontSize     = 21f;
        forgotTxt.color        = Hex("B7EFFF");
        forgotTxt.alignment    = TextAlignmentOptions.Right;
        forgotTxt.overflowMode = TextOverflowModes.Overflow;
        var btnForgot = forgotGo.AddComponent<Button>();
        btnForgot.targetGraphic = forgotBg;

        // ── Botón Iniciar Sesión ──────────────────────────────────────────────
        var loginGo = MakeEmpty(inner.transform, "BtnIniciarSesion");
        SetRTDirect(loginGo, new Vector2(0f, -240f), new Vector2(INPUT_W, 68f));
        var loginImg = loginGo.AddComponent<Image>();
        loginImg.sprite = sprCyan;
        loginImg.type   = sprCyan ? Image.Type.Sliced : Image.Type.Simple;
        loginImg.color  = Color.white;
        var btnLogin = loginGo.AddComponent<Button>();
        btnLogin.targetGraphic = loginImg;
        loginGo.AddComponent<ButtonPressEffect>();
        var loginLbl = MakeEmpty(loginGo.transform, "Label");
        SetRTDirect(loginLbl, Vector2.zero, new Vector2(INPUT_W, 68f));
        var loginTxt = loginLbl.AddComponent<TextMeshProUGUI>();
        loginTxt.text         = "Iniciar Sesión";
        loginTxt.font         = fnt;
        loginTxt.fontSize     = 44f;
        loginTxt.color        = Hex("0A2F44");
        loginTxt.fontStyle    = FontStyles.Bold;
        loginTxt.alignment    = TextAlignmentOptions.Center;
        loginTxt.overflowMode = TextOverflowModes.Overflow;

        // ── ¿No tienes cuenta? Regístrate ────────────────────────────────────
        var regGo = MakeEmpty(inner.transform, "BtnRegistrate");
        SetRTDirect(regGo, new Vector2(0f, -315f), new Vector2(420f, 40f));
        var regBg = regGo.AddComponent<Image>();
        regBg.color = Color.clear;
        var regTxt = regGo.AddComponent<TextMeshProUGUI>();
        regTxt.text         = "¿No tienes cuenta?  <color=#FFD23F><b>Regístrate</b></color>";
        regTxt.font         = fnt;
        regTxt.fontSize     = 22f;
        regTxt.color        = new Color(1f, 1f, 1f, 0.8f);
        regTxt.alignment    = TextAlignmentOptions.Center;
        regTxt.richText     = true;
        regTxt.overflowMode = TextOverflowModes.Overflow;
        var btnReg = regGo.AddComponent<Button>();
        btnReg.targetGraphic = regBg;

        // ── Reconectar LoginManager ───────────────────────────────────────────
        var mgr = Object.FindFirstObjectByType<LoginManager>();
        if (mgr != null)
        {
            var mSO = new SerializedObject(mgr);
            mSO.FindProperty("inputEmail").objectReferenceValue       = emailField;
            mSO.FindProperty("inputPassword").objectReferenceValue    = passField;
            mSO.FindProperty("btnIniciarSesion").objectReferenceValue = btnLogin;
            mSO.FindProperty("btnOlvidaste").objectReferenceValue     = btnForgot;
            mSO.FindProperty("btnRegistrate").objectReferenceValue    = btnReg;
            mSO.FindProperty("btnTogglePassword").objectReferenceValue = btnToggle;
            mSO.FindProperty("sprOjoOculto").objectReferenceValue     = sprEye;
            mSO.ApplyModifiedProperties();
        }

        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("¡Listo!", "Layout corregido.\nGuarda con Ctrl+S y prueba con Play.", "OK");
        Debug.Log("[LoginLayoutFix] ✓ Inputs y layout corregidos.");
    }

    // ── Crea un TMP_InputField que funciona correctamente ─────────────────────
    static TMP_InputField MakeInputField(Transform parent, TMP_FontAsset fnt, Sprite uiSpr,
        Sprite iconSpr, string name, string placeholderText,
        Vector2 pos, TMP_InputField.ContentType contentType)
    {
        // Raíz del field
        var go = MakeEmpty(parent, name);
        SetRTDirect(go, pos, new Vector2(INPUT_W, INPUT_H));

        // Fondo
        var bg = go.AddComponent<Image>();
        bg.color  = Hex("0D1238");
        bg.sprite = uiSpr;
        bg.type   = Image.Type.Sliced;

        // Ícono izquierdo (fuera del viewport, decorativo)
        if (iconSpr != null)
        {
            var iconGo = MakeEmpty(go.transform, "Icon");
            SetRTDirect(iconGo, new Vector2(-INPUT_W / 2f + 44f, 0f), new Vector2(34f, 34f));
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.sprite = iconSpr;
            iconImg.color  = new Color(1f, 1f, 1f, 0.8f);
            iconImg.preserveAspect = true;
        }

        // Text Area (viewport con máscara)
        var area = new GameObject("Text Area", typeof(RectTransform));
        area.transform.SetParent(go.transform, false);
        var areaRT = area.GetComponent<RectTransform>();
        areaRT.anchorMin       = Vector2.zero;
        areaRT.anchorMax       = Vector2.one;
        areaRT.pivot           = new Vector2(0.5f, 0.5f);
        areaRT.offsetMin       = new Vector2(iconSpr != null ? 78f : 16f, 6f);
        areaRT.offsetMax       = new Vector2(-16f, -6f);
        area.AddComponent<RectMask2D>();

        // Placeholder
        var phGo = new GameObject("Placeholder", typeof(RectTransform));
        phGo.transform.SetParent(area.transform, false);
        StretchFull(phGo);
        var ph = phGo.AddComponent<TextMeshProUGUI>();
        ph.text              = placeholderText;
        ph.font              = fnt;
        ph.fontSize          = 30f;
        ph.color             = new Color(1f, 1f, 1f, 0.3f);
        ph.alignment         = TextAlignmentOptions.Left | TextAlignmentOptions.Midline;
        ph.enableWordWrapping = false;
        ph.richText          = false;

        // Text
        var txtGo = new GameObject("Text", typeof(RectTransform));
        txtGo.transform.SetParent(area.transform, false);
        StretchFull(txtGo);
        var txt = txtGo.AddComponent<TextMeshProUGUI>();
        txt.text              = "";
        txt.font              = fnt;
        txt.fontSize          = 30f;
        txt.color             = Color.white;
        txt.alignment         = TextAlignmentOptions.Left | TextAlignmentOptions.Midline;
        txt.enableWordWrapping = false;
        txt.richText          = false;

        // TMP_InputField — conectar via SerializedObject para que Unity serialice bien
        var field = go.AddComponent<TMP_InputField>();
        var so = new SerializedObject(field);
        so.FindProperty("m_TextViewport").objectReferenceValue  = areaRT;
        so.FindProperty("m_TextComponent").objectReferenceValue = txt;
        so.FindProperty("m_Placeholder").objectReferenceValue   = ph;
        so.FindProperty("m_TargetGraphic").objectReferenceValue = bg;
        so.FindProperty("m_ContentType").enumValueIndex = (int)contentType;
        so.FindProperty("m_LineType").enumValueIndex    = 0; // SingleLine
        so.ApplyModifiedProperties();

        // Colores del field
        var colors = field.colors;
        colors.normalColor      = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.9f);
        colors.selectedColor    = new Color(0.9f, 0.9f, 1f, 1f);
        colors.pressedColor     = new Color(0.8f, 0.8f, 0.8f, 1f);
        field.colors = colors;
        field.interactable = true;

        EditorUtility.SetDirty(go);
        return field;
    }

    // ── Label izquierdo ───────────────────────────────────────────────────────
    static void MakeLabel(Transform parent, TMP_FontAsset fnt, string name, string text, Vector2 pos)
    {
        var go = MakeEmpty(parent, name);
        SetRTDirect(go, pos, new Vector2(INPUT_W, 34f));
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text         = text;
        tmp.font         = fnt;
        tmp.fontSize     = 24f;
        tmp.color        = Hex("A2A2A2");
        tmp.alignment    = TextAlignmentOptions.Left | TextAlignmentOptions.Midline;
        tmp.overflowMode = TextOverflowModes.Overflow;
    }

    // ── Utils ─────────────────────────────────────────────────────────────────
    static void DestroyChild(GameObject parent, string name)
    {
        var t = parent.transform.Find(name);
        if (t != null) Object.DestroyImmediate(t.gameObject);
    }

    static void SetRT(GameObject parent, string childName, Vector2 pos, Vector2 size)
    {
        var t = parent.transform.Find(childName);
        if (t == null) return;
        var rt = t.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        EditorUtility.SetDirty(rt);
    }

    static void SetRTDirect(GameObject go, Vector2 pos, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void StretchFull(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void AlignLeft(GameObject parent, string childName)
    {
        var t = parent.transform.Find(childName);
        if (t == null) return;
        var tmp = t.GetComponent<TextMeshProUGUI>();
        if (tmp) { tmp.alignment = TextAlignmentOptions.Left | TextAlignmentOptions.Midline; EditorUtility.SetDirty(tmp); }
    }

    static GameObject MakeEmpty(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        return go;
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        return c;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ChemiTech/Fix/Rounded Panels
    // Genera un sprite de rectángulo redondeado y lo aplica a LoginPanel
    // y PanelInner para que los bordes se vean suaves.
    // ═══════════════════════════════════════════════════════════════════════
    [MenuItem("ChemiTech/Fix/Rounded Panels")]
    static void RoundedPanels()
    {
        const string SPRITES_DIR = "Assets/Sprites/Login";
        const string SPR_PATH    = SPRITES_DIR + "/rounded-panel.png";
        const int    TEX_SIZE    = 128;
        const int    RADIUS      = 24;   // px de esquina en la textura (sliced → se mantiene fijo en pantalla)

        if (!Directory.Exists(SPRITES_DIR))
            Directory.CreateDirectory(SPRITES_DIR);

        var spr = GenRoundedSprite(SPR_PATH, TEX_SIZE, RADIUS);
        if (spr == null)
        {
            EditorUtility.DisplayDialog("Error", "No se pudo generar el sprite.", "OK");
            return;
        }

        // LoginPanel: borde blanco exterior
        ApplyRoundedToPanel("LoginPanel", spr, Color.white);

        // PanelInner: fondo oscuro interior
        ApplyRoundedToPanel("PanelInner", spr, Hex("252858"));

        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("¡Listo!",
            "Bordes redondeados aplicados.\nGuarda con Ctrl+S.", "OK");
    }

    static void ApplyRoundedToPanel(string goName, Sprite spr, Color color)
    {
        var go = GameObject.Find(goName);
        if (go == null) { Debug.LogWarning($"[RoundedPanels] No encontró: {goName}"); return; }
        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        img.sprite = spr;
        img.type   = Image.Type.Sliced;
        img.color  = color;
        EditorUtility.SetDirty(img);
    }

    // Genera un PNG cuadrado blanco con esquinas redondeadas, lo configura
    // como Sprite con border = radius en los 4 lados (9-slicing).
    static Sprite GenRoundedSprite(string path, int size, int radius)
    {
        var tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = InsideRR(x + 0.5f, y + 0.5f, size, size, radius)
                    ? Color.white : Color.clear;

        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path);

        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType      = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.mipmapEnabled    = false;
        imp.filterMode       = FilterMode.Bilinear;
        imp.wrapMode         = TextureWrapMode.Clamp;

        var s = new TextureImporterSettings();
        imp.ReadTextureSettings(s);
        s.spriteBorder = new Vector4(radius, radius, radius, radius);
        imp.SetTextureSettings(s);
        imp.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // Comprueba si el punto (px, py) cae dentro del rectángulo redondeado.
    static bool InsideRR(float px, float py, int w, int h, int r)
    {
        if (px < r && py < r)       return Dist(px, py, r,   r  ) <= r;
        if (px > w-r && py < r)     return Dist(px, py, w-r, r  ) <= r;
        if (px < r && py > h-r)     return Dist(px, py, r,   h-r) <= r;
        if (px > w-r && py > h-r)   return Dist(px, py, w-r, h-r) <= r;
        return true;
    }

    static float Dist(float ax, float ay, float bx, float by)
        => Mathf.Sqrt((ax-bx)*(ax-bx) + (ay-by)*(ay-by));
}
