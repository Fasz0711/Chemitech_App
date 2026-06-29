using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Conecta el componente HeaderAvatar al objeto "AvatarIcon" del header de la
/// escena activa. Con sesión muestra el avatar de la cuenta; en invitado deja el
/// ícono genérico. Reutilizable para cualquier pantalla con un "AvatarIcon".
/// </summary>
public static class HeaderAvatarTool
{
    [MenuItem("ChemiTech/Fix/Header Avatar (escena actual)")]
    static void FixHeaderAvatar()
    {
        var canvas = GameObject.Find("Canvas");
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Error", "No se encontró 'Canvas' en la escena activa.", "OK");
            return;
        }
        var avatarT = FindChildRecursive(canvas.transform, "AvatarIcon");
        if (avatarT == null)
        {
            EditorUtility.DisplayDialog("Error", "No se encontró 'AvatarIcon' en el header.", "OK");
            return;
        }

        Setup(avatarT.gameObject);

        EditorUtility.SetDirty(avatarT.gameObject);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[HeaderAvatarTool] ✓ HeaderAvatar conectado en " + avatarT.name);
        EditorUtility.DisplayDialog("¡Listo!",
            "Header avatar conectado:\n• Con sesión → avatar de la cuenta.\n• Invitado → ícono genérico.\n\nGuarda con Ctrl+S.", "OK");
    }

    // Reutilizable también desde otros builders (pantallas nuevas).
    public static void Setup(GameObject avatarGo)
    {
        var circle = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/AtomCircle.png");
        var icons  = LoadAvatarIcons();

        var bg = avatarGo.GetComponent<Image>();
        if (bg == null) bg = avatarGo.AddComponent<Image>();

        // inner = hijo "Icon" (o crear uno)
        var innerT = avatarGo.transform.Find("Icon");
        Image inner;
        if (innerT != null)
        {
            inner = innerT.GetComponent<Image>();
            if (inner == null) inner = innerT.gameObject.AddComponent<Image>();
        }
        else
        {
            var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(avatarGo.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = ((RectTransform)avatarGo.transform).sizeDelta - new Vector2(18f, 18f);
            inner = go.GetComponent<Image>();
            inner.preserveAspect = true;
        }

        var ha = avatarGo.GetComponent<HeaderAvatar>();
        if (ha == null) ha = avatarGo.AddComponent<HeaderAvatar>();

        var so = new SerializedObject(ha);
        so.FindProperty("bg").objectReferenceValue           = bg;
        so.FindProperty("inner").objectReferenceValue        = inner;
        so.FindProperty("circleSprite").objectReferenceValue = circle;
        var ip = so.FindProperty("icons");
        ip.arraySize = icons.Length;
        for (int i = 0; i < icons.Length; i++)
            ip.GetArrayElementAtIndex(i).objectReferenceValue = icons[i];
        so.ApplyModifiedProperties();
    }

    static Sprite[] LoadAvatarIcons()
    {
        var names = AvatarCatalog.IconNames;
        var arr = new Sprite[names.Length];
        for (int i = 0; i < names.Length; i++)
            arr[i] = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprites/Icons/{names[i]}.png");
        return arr;
    }

    static Transform FindChildRecursive(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform c in root)
        {
            var r = FindChildRecursive(c, name);
            if (r) return r;
        }
        return null;
    }
}
