using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pinta el avatar guardado de la cuenta (AvatarStore + AvatarCatalog) en un
/// círculo de fondo + ícono interno. Componente reutilizable para headers.
/// Si faltan sprites, no hace nada (deja lo que haya).
/// </summary>
public class HeaderAvatar : MonoBehaviour
{
    [SerializeField] private Image    bg;            // círculo de fondo
    [SerializeField] private Image    inner;         // ícono interno
    [SerializeField] private Sprite   circleSprite;  // AtomCircle
    [SerializeField] private Sprite[] icons;         // orden = AvatarCatalog.IconNames
    [SerializeField] private int      defaultIndex = 3;

    private void Start() => Refresh();

    public void Refresh()
    {
        // Invitado (sin sesión): se deja el ícono genérico que ya trae la escena.
        if (!SessionData.IsLoggedIn) return;

        if (!bg || !inner || circleSprite == null || icons == null || icons.Length == 0) return;

        int idx = AvatarStore.Load(defaultIndex);

        bg.sprite         = circleSprite;
        bg.type           = Image.Type.Simple;
        bg.color          = AvatarCatalog.ColorAt(idx);
        bg.preserveAspect = true;

        inner.sprite = icons[AvatarCatalog.IconIndexAt(idx)];
        inner.color  = Color.white;
        inner.gameObject.SetActive(true);
    }
}
