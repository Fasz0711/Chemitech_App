using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using U = ForgotPasswordUI;

/// <summary>
/// Olvidé mi contraseña — paso 2/3: "Revisa tu correo" (código de 6 casillas).
/// Menú: ChemiTech → Build ForgotPassword Code Scene
/// </summary>
public static class ForgotPasswordCodeBuilder
{
    const string ScenePath = "Assets/Scenes/ForgotPasswordCodeScene.unity";
    const float PANEL_W = 780f, PANEL_H = 520f;

    [MenuItem("ChemiTech/Build ForgotPassword Code Scene")]
    public static void Build()
    {
        if (!U.ConfirmBuild("ForgotPasswordCodeScene", ScenePath)) return;

        var canvas = U.NewSceneCanvas();
        var panel  = U.MakePanel(canvas.transform, PANEL_W, PANEL_H).transform;

        U.MakeDots(panel, 232f, 3, 1);

        // Ícono de correo
        var iconGo = U.MakeEmpty(panel, "MailIcon");
        U.SetRT(iconGo, new Vector2(0f, 194f), new Vector2(48f, 48f));
        var iconImg = iconGo.AddComponent<Image>();
        iconImg.sprite = U.IconEmail; iconImg.color = U.Hex("4DD9E8"); iconImg.preserveAspect = true;

        U.MakeText(panel, "Title", "Revisa tu correo", 44f, Color.white,
            new Vector2(0f, 150f), new Vector2(PANEL_W - 60f, 60f),
            TextAlignmentOptions.Center, FontStyles.Bold);

        U.MakeText(panel, "Subtitle", "Te enviamos un código de 6 caracteres a", 22f,
            new Color(1f, 1f, 1f, 0.7f), new Vector2(0f, 106f), new Vector2(PANEL_W - 90f, 30f),
            TextAlignmentOptions.Center);

        var txtEmail = U.MakeText(panel, "EmailLabel", "correo@ejemplo.com", 24f, U.Hex("4DD9E8"),
            new Vector2(0f, 76f), new Vector2(PANEL_W - 90f, 30f), TextAlignmentOptions.Center, FontStyles.Bold);

        // ── 6 casillas ─────────────────────────────────────────────────────────
        const float BOX = 64f, BGAP = 16f;
        float startX = -((6 - 1) * (BOX + BGAP)) / 2f;
        var boxes = new TMP_InputField[6];
        for (int i = 0; i < 6; i++)
            boxes[i] = U.MakeCodeBox(panel, $"CodeBox{i}", new Vector2(startX + i * (BOX + BGAP), 2f), BOX);

        // ── Reenviar ─────────────────────────────────────────────────────────────
        U.MakeText(panel, "ResendQuestion", "¿No recibiste el correo o el código es inválido?", 19f,
            new Color(1f, 1f, 1f, 0.6f), new Vector2(0f, -70f), new Vector2(PANEL_W - 80f, 26f),
            TextAlignmentOptions.Center);

        var btnReenviar = U.MakeButton(panel, "BtnReenviar", "Reenviar", new Vector2(0f, -104f),
            new Vector2(260f, 40f), new Color(0f, 0f, 0f, 0f), out var txtReenviar, 22f);

        var feedback = U.MakeText(panel, "Feedback", "", 20f, new Color(0.90f, 0.30f, 0.25f, 1f),
            new Vector2(0f, -140f), new Vector2(PANEL_W - 100f, 28f), TextAlignmentOptions.Center);

        const float BW = 220f, BH = 66f, GAP = 20f;
        var btnAtras = U.MakeButton(panel, "BtnAtras", "Atrás",
            new Vector2(-(BW / 2f + GAP / 2f), -196f), new Vector2(BW, BH), U.Hex("3A3B6B"), out _);
        var btnVerif = U.MakeButton(panel, "BtnVerificar", "Verificar",
            new Vector2(BW / 2f + GAP / 2f, -196f), new Vector2(BW, BH), U.Hex("7B8096"), out _);

        // ── Manager ────────────────────────────────────────────────────────────
        var mgrGo = U.MakeEmpty(canvas.transform, "ForgotPasswordCodeManager");
        var mgr   = mgrGo.AddComponent<ForgotPasswordCodeManager>();
        var so    = new SerializedObject(mgr);

        var boxesProp = so.FindProperty("boxes");
        boxesProp.arraySize = 6;
        for (int i = 0; i < 6; i++)
            boxesProp.GetArrayElementAtIndex(i).objectReferenceValue = boxes[i];

        so.FindProperty("txtEmail").objectReferenceValue     = txtEmail;
        so.FindProperty("txtFeedback").objectReferenceValue  = feedback;
        so.FindProperty("txtReenviar").objectReferenceValue  = txtReenviar;
        so.FindProperty("btnAtras").objectReferenceValue     = btnAtras;
        so.FindProperty("btnVerificar").objectReferenceValue = btnVerif;
        so.FindProperty("btnReenviar").objectReferenceValue  = btnReenviar;
        so.ApplyModifiedProperties();

        U.Save(ScenePath);
        U.Done("ForgotPasswordCodeScene");
    }
}
