using System;

/// <summary>Familia de sonido de un botón de interfaz.</summary>
public enum UiSfxRole
{
    /// <summary>Lo decide UiSfxClassifier a partir del nombre del objeto.</summary>
    Auto = 0,
    /// <summary>Navegación corriente: abrir una pantalla, un modal informativo.</summary>
    Neutral,
    /// <summary>Confirmar, avanzar, guardar, crear.</summary>
    Primary,
    /// <summary>Cancelar o descartar sin consecuencias.</summary>
    Cancel,
    /// <summary>Volver atrás, cerrar.</summary>
    Back,
    /// <summary>Acción destructiva o irreversible.</summary>
    Danger,
    /// <summary>Cambiar de pestaña o elegir una opción dentro de una lista.</summary>
    Toggle,
    /// <summary>No suena (el botón ya dispara su propio efecto).</summary>
    Silent,
}

/// <summary>
/// Decide el sonido de un botón por el NOMBRE de su GameObject.
///
/// Toda la UI la generan los builders con una convención estable (BtnGuardar,
/// BtnDeleteConfirm, Tab_Audio, Seg_Calidad…), así que clasificar por nombre
/// cubre las 19 escenas sin tocar ninguna ni cablear nada a mano. Para los casos
/// que se escapen, UiClickSfx permite fijar el rol desde el inspector.
///
/// El ORDEN de las reglas importa: se evalúan de la más específica a la más
/// general. 'BtnCerrarSesion' contiene "Cerrar", pero es destructivo, no un
/// "volver atrás"; y 'BtnLogoutCancel' contiene "Logout" sin ser destructivo.
/// Por eso las reglas peligrosas usan el sufijo completo ("LogoutConfirm") y se
/// consultan antes que las genéricas.
/// </summary>
public static class UiSfxClassifier
{
    // Botones que ya disparan su propio efecto en el código de juego: si además
    // les enganchamos el click genérico, se oiría doble.
    static readonly string[] SILENT_EXACT =
    {
        "BtnPlace",   // AtomPlacementController → PlayPlaceAtom
        "DeleteBar",  // AtomPlacementController → PlayDeleteAtom (así lo nombra ZonaJuegoBuilder)
    };

    // Excepciones al clasificador: nombres que una regla general leería mal.
    // 'BtnCambiarAvatar' ABRE la vista de avatares, no selecciona uno, así que no
    // debe sonar como las opciones 'Avatar0..9' de la cuadrícula.
    static readonly string[] NEUTRAL_EXACT =
    {
        "BtnCambiarAvatar",
    };

    static readonly string[] DANGER =
    {
        "Eliminar", "DeleteConfirm", "LogoutConfirm", "CerrarSesion", "ExitConfirm",
    };

    static readonly string[] CANCEL =
    {
        "Cancel", "Omitir", "Entendido",
    };

    static readonly string[] BACK =
    {
        "Back", "Atras", "Volver", "Cerrar", "Close", "Salir",
    };

    static readonly string[] PRIMARY =
    {
        "IniciarSesion", "Login", "Crear", "Regist", "Siguiente", "Submit",
        "Guardar", "Verificar", "Continuar", "Iniciar", "EmpezarJugar", "Jugar",
        "Confirm", "Reintentar", "Reenviar", "Explorar", "Reanudar",
    };

    // 'Icon_0..7' y 'Color_0..5' son las opciones de CrearUniverso/EditarUniverso;
    // 'Avatar0..9', las de la cuadrícula de perfil.
    static readonly string[] TOGGLE =
    {
        "Tab_", "Seg_", "Toggle", "Selector", "Avatar", "Icon_", "Color_",
    };

    public static UiSfxRole Classify(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return UiSfxRole.Neutral;

        foreach (var s in SILENT_EXACT)
            if (objectName.Equals(s, StringComparison.OrdinalIgnoreCase)) return UiSfxRole.Silent;

        foreach (var s in NEUTRAL_EXACT)
            if (objectName.Equals(s, StringComparison.OrdinalIgnoreCase)) return UiSfxRole.Neutral;

        if (MatchesAny(objectName, DANGER))  return UiSfxRole.Danger;
        if (MatchesAny(objectName, CANCEL))  return UiSfxRole.Cancel;
        if (MatchesAny(objectName, BACK))    return UiSfxRole.Back;
        if (MatchesAny(objectName, PRIMARY)) return UiSfxRole.Primary;
        if (MatchesAny(objectName, TOGGLE))  return UiSfxRole.Toggle;

        return UiSfxRole.Neutral;
    }

    static bool MatchesAny(string name, string[] keys)
    {
        foreach (var k in keys)
            if (name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }
}
