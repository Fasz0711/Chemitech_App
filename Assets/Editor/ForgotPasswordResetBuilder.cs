using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using U = ForgotPasswordUI;

/// <summary>
/// Olvidé mi contraseña — paso 3/3: "Elige Nueva Contraseña".
/// Mismas validaciones/estilo que RegisterPasswordScene + campo de confirmación.
/// Menú: ChemiTech → Build ForgotPassword Reset Scene
/// </summary>
public static class ForgotPasswordResetBuilder
{
    const string ScenePath = "Assets/Scenes/ForgotPasswordResetScene.unity";
    const float PANEL_W = 780f, PANEL_H = 740f, INPUT_W = 580f;

    [MenuItem("ChemiTech/Build ForgotPassword Reset Scene")]
    public static void Build()
    {
        if (!U.ConfirmBuild("ForgotPasswordResetScene", ScenePath)) return;

        var canvas = U.NewSceneCanvas();
        var panel  = U.MakePanel(canvas.transform, PANEL_W, PANEL_H).transform;

        U.MakeDots(panel, 336f, 3, 2);

        U.MakeText(panel, "Title", "Elige Nueva Contraseña", 44f, Color.white,
            new Vector2(0f, 282f), new Vector2(PANEL_W - 60f, 60f),
            TextAlignmentOptions.Center, FontStyles.Bold);

        U.MakeText(panel, "Subtitle", "Ingresa tu nueva contraseña", 22f, new Color(1f, 1f, 1f, 0.7f),
            new Vector2(0f, 242f), new Vector2(PANEL_W - 90f, 30f), TextAlignmentOptions.Center);

        // Contraseña
        U.MakeText(panel, "LabelPassword", "Contraseña", 21f, U.Hex("4DD9E8"),
            new Vector2(0f, 200f), new Vector2(INPUT_W, 28f), TextAlignmentOptions.Left);
        var inputPwd = U.MakeInput(panel, "InputPassword", "••••••••", new Vector2(0f, 148f),
            U.IconLock, TMP_InputField.ContentType.Password, rightPad: 52f);
        var togglePwd = U.AddEyeToggle(inputPwd, INPUT_W);

        // Confirmar
        U.MakeText(panel, "LabelConfirm", "Vuelve a ingresar la contraseña", 21f, U.Hex("4DD9E8"),
            new Vector2(0f, 92f), new Vector2(INPUT_W, 28f), TextAlignmentOptions.Left);
        var inputConfirm = U.MakeInput(panel, "InputConfirm", "••••••••", new Vector2(0f, 40f),
            U.IconLock, TMP_InputField.ContentType.Password, rightPad: 52f);
        var toggleConfirm = U.AddEyeToggle(inputConfirm, INPUT_W);

        var matchLabel = U.MakeText(panel, "MatchLabel", "", 20f, new Color(0.18f, 0.80f, 0.44f, 1f),
            new Vector2(0f, -18f), new Vector2(INPUT_W, 26f), TextAlignmentOptions.Left);

        // Barra de fortaleza
        var strengthFill = MakeStrengthBar(panel, new Vector2(0f, -48f), INPUT_W);
        var strengthLabel = U.MakeText(panel, "StrengthLabel", "", 20f, U.Hex("E53535"),
            new Vector2(-INPUT_W / 2f + 2f, -68f), new Vector2(200f, 24f), TextAlignmentOptions.Left);

        // Requisitos (2×2)
        const float REQ_W = 282f, REQ_H = 38f, REQ_GX = 16f, REQ_GY = 10f;
        float c1 = -(REQ_W / 2f + REQ_GX / 2f), c2 = REQ_W / 2f + REQ_GX / 2f;
        float r1 = -106f, r2 = r1 - REQ_H - REQ_GY;
        var (bgLen, txtLen) = MakeReqItem(panel, "ReqLength", "8+ caracteres", new Vector2(c1, r1), new Vector2(REQ_W, REQ_H));
        var (bgUpp, txtUpp) = MakeReqItem(panel, "ReqUpper",  "Una mayúscula", new Vector2(c2, r1), new Vector2(REQ_W, REQ_H));
        var (bgNum, txtNum) = MakeReqItem(panel, "ReqNumber", "Un número",     new Vector2(c1, r2), new Vector2(REQ_W, REQ_H));
        var (bgSym, txtSym) = MakeReqItem(panel, "ReqSymbol", "Un símbolo",    new Vector2(c2, r2), new Vector2(REQ_W, REQ_H));

        var feedback = U.MakeText(panel, "Feedback", "", 20f, new Color(0.90f, 0.30f, 0.25f, 1f),
            new Vector2(0f, -196f), new Vector2(PANEL_W - 100f, 28f), TextAlignmentOptions.Center);

        const float BW = 250f, BH = 66f, GAP = 20f;
        var btnAtras = U.MakeButton(panel, "BtnAtras", "Atrás",
            new Vector2(-(BW / 2f + GAP / 2f), -252f), new Vector2(BW, BH), U.Hex("3A3B6B"), out _);
        var btnCambiar = U.MakeButton(panel, "BtnCambiar", "Cambiar Contraseña",
            new Vector2(BW / 2f + GAP / 2f, -252f), new Vector2(BW, BH), U.Hex("7B8096"), out _, 24f);

        // ── Manager ────────────────────────────────────────────────────────────
        var mgrGo = U.MakeEmpty(canvas.transform, "ForgotPasswordResetManager");
        var mgr   = mgrGo.AddComponent<ForgotPasswordResetManager>();
        var so    = new SerializedObject(mgr);
        so.FindProperty("inputPassword").objectReferenceValue     = inputPwd;
        so.FindProperty("inputConfirm").objectReferenceValue      = inputConfirm;
        so.FindProperty("btnTogglePassword").objectReferenceValue = togglePwd;
        so.FindProperty("btnToggleConfirm").objectReferenceValue  = toggleConfirm;
        so.FindProperty("strengthBarFill").objectReferenceValue   = strengthFill;
        so.FindProperty("strengthLabel").objectReferenceValue     = strengthLabel;
        so.FindProperty("reqLengthBg").objectReferenceValue       = bgLen;
        so.FindProperty("reqUpperBg").objectReferenceValue        = bgUpp;
        so.FindProperty("reqNumberBg").objectReferenceValue       = bgNum;
        so.FindProperty("reqSymbolBg").objectReferenceValue       = bgSym;
        so.FindProperty("reqLengthTxt").objectReferenceValue      = txtLen;
        so.FindProperty("reqUpperTxt").objectReferenceValue       = txtUpp;
        so.FindProperty("reqNumberTxt").objectReferenceValue      = txtNum;
        so.FindProperty("reqSymbolTxt").objectReferenceValue      = txtSym;
        so.FindProperty("matchLabel").objectReferenceValue        = matchLabel;
        so.FindProperty("txtFeedback").objectReferenceValue       = feedback;
        so.FindProperty("btnAtras").objectReferenceValue          = btnAtras;
        so.FindProperty("btnCambiar").objectReferenceValue        = btnCambiar;
        so.ApplyModifiedProperties();

        U.Save(ScenePath);
        U.Done("ForgotPasswordResetScene");
    }

    // ── Helpers locales ──────────────────────────────────────────────────────
    static Image MakeStrengthBar(Transform parent, Vector2 pos, float w)
    {
        var bgGo = U.MakeEmpty(parent, "StrengthBarBg");
        U.SetRT(bgGo, pos, new Vector2(w, 8f));
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color(1f, 1f, 1f, 0.15f);

        var fillGo = U.MakeEmpty(bgGo.transform, "Fill");
        var fillRT = fillGo.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = fillRT.offsetMax = Vector2.zero;
        var fillImg = fillGo.AddComponent<Image>();
        fillImg.color      = U.Hex("E53535");
        fillImg.type       = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImg.fillAmount = 0f;
        return fillImg;
    }

    static (Image bg, TextMeshProUGUI txt) MakeReqItem(Transform parent, string name, string label, Vector2 pos, Vector2 size)
    {
        var go = U.MakeEmpty(parent, name);
        U.SetRT(go, pos, size);
        var bg = go.AddComponent<Image>();
        bg.sprite = U.Rounded; bg.type = Image.Type.Sliced;
        bg.color  = new Color(1f, 1f, 1f, 0.10f);
        var txt = U.MakeLabel(go.transform, "○  " + label, 20f, FontStyles.Normal, new Color(1f, 1f, 1f, 0.85f));
        return (bg, txt);
    }
}
