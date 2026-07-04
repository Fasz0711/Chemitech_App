using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using U = ForgotPasswordUI;

/// <summary>
/// Olvidé mi contraseña — paso 1/3: "Cambiar Contraseña" (pedir el correo).
/// Menú: ChemiTech → Build ForgotPassword Email Scene
/// </summary>
public static class ForgotPasswordEmailBuilder
{
    const string ScenePath = "Assets/Scenes/ForgotPasswordEmailScene.unity";
    const float PANEL_W = 760f, PANEL_H = 460f;

    [MenuItem("ChemiTech/Build ForgotPassword Email Scene")]
    public static void Build()
    {
        if (!U.ConfirmBuild("ForgotPasswordEmailScene", ScenePath)) return;

        var canvas = U.NewSceneCanvas();
        var panel  = U.MakePanel(canvas.transform, PANEL_W, PANEL_H).transform;

        U.MakeDots(panel, 190f, 3, 0);

        U.MakeText(panel, "Title", "Cambiar Contraseña", 50f, Color.white,
            new Vector2(0f, 128f), new Vector2(PANEL_W - 60f, 70f),
            TextAlignmentOptions.Center, FontStyles.Bold, wrap: true);

        U.MakeText(panel, "Subtitle", "Introduce el correo electrónico asociado a tu cuenta.",
            24f, new Color(1f, 1f, 1f, 0.7f), new Vector2(0f, 76f), new Vector2(PANEL_W - 90f, 36f),
            TextAlignmentOptions.Center, wrap: true);

        U.MakeText(panel, "LabelEmail", "Correo Electrónico", 22f, U.Hex("4DD9E8"),
            new Vector2(0f, 22f), new Vector2(PANEL_W - 100f, 30f), TextAlignmentOptions.Left);

        var input = U.MakeInput(panel, "InputEmail", "alex@chemitech.com", new Vector2(0f, -34f),
            U.IconEmail, TMP_InputField.ContentType.EmailAddress);

        var feedback = U.MakeText(panel, "Feedback", "", 20f, new Color(0.90f, 0.30f, 0.25f, 1f),
            new Vector2(0f, -92f), new Vector2(PANEL_W - 100f, 28f), TextAlignmentOptions.Center);

        const float BW = 220f, BH = 66f, GAP = 20f;
        var btnAtras = U.MakeButton(panel, "BtnAtras", "Atrás",
            new Vector2(-(BW / 2f + GAP / 2f), -158f), new Vector2(BW, BH), U.Hex("3A3B6B"), out _);
        var btnSig = U.MakeButton(panel, "BtnSiguiente", "Siguiente",
            new Vector2(BW / 2f + GAP / 2f, -158f), new Vector2(BW, BH), U.Hex("7B8096"), out _);

        // ── Manager ────────────────────────────────────────────────────────────
        var mgrGo = U.MakeEmpty(canvas.transform, "ForgotPasswordEmailManager");
        var mgr   = mgrGo.AddComponent<ForgotPasswordEmailManager>();
        var so    = new SerializedObject(mgr);
        so.FindProperty("inputEmail").objectReferenceValue   = input;
        so.FindProperty("txtFeedback").objectReferenceValue  = feedback;
        so.FindProperty("btnAtras").objectReferenceValue     = btnAtras;
        so.FindProperty("btnSiguiente").objectReferenceValue = btnSig;
        so.ApplyModifiedProperties();

        U.Save(ScenePath);
        U.Done("ForgotPasswordEmailScene");
    }
}
