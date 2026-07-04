using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using U = ForgotPasswordUI;

/// <summary>
/// Olvidé mi contraseña — pantalla final: "¡Contraseña cambiada exitosamente!".
/// Menú: ChemiTech → Build ForgotPassword Success Scene
/// </summary>
public static class ForgotPasswordSuccessBuilder
{
    const string ScenePath = "Assets/Scenes/ForgotPasswordSuccessScene.unity";
    const float PANEL_W = 680f, PANEL_H = 470f;

    [MenuItem("ChemiTech/Build ForgotPassword Success Scene")]
    public static void Build()
    {
        if (!U.ConfirmBuild("ForgotPasswordSuccessScene", ScenePath)) return;

        var canvas = U.NewSceneCanvas();
        var panel  = U.MakePanel(canvas.transform, PANEL_W, PANEL_H).transform;

        U.MakeText(panel, "Title", "¡La contraseña se ha cambiado exitosamente!", 38f, Color.white,
            new Vector2(0f, 150f), new Vector2(PANEL_W - 80f, 130f),
            TextAlignmentOptions.Center, FontStyles.Bold, wrap: true);

        // Check verde
        var checkGo = U.MakeEmpty(panel, "CheckIcon");
        U.SetRT(checkGo, new Vector2(0f, 4f), new Vector2(130f, 130f));
        var checkImg = checkGo.AddComponent<Image>();
        checkImg.sprite = U.CheckGreen;
        checkImg.preserveAspect = true;
        checkImg.color = U.CheckGreen != null ? Color.white : U.Hex("6ABF43");

        var btnLogin = U.MakeButton(panel, "BtnLogin", "Ir a iniciar sesión",
            new Vector2(0f, -150f), new Vector2(320f, 68f), U.Hex("30D3E6"), out _, 30f);

        // ── Manager ────────────────────────────────────────────────────────────
        var mgrGo = U.MakeEmpty(canvas.transform, "ForgotPasswordSuccessManager");
        var mgr   = mgrGo.AddComponent<ForgotPasswordSuccessManager>();
        var so    = new SerializedObject(mgr);
        so.FindProperty("btnLogin").objectReferenceValue = btnLogin;
        so.ApplyModifiedProperties();

        U.Save(ScenePath);
        U.Done("ForgotPasswordSuccessScene");
    }
}
