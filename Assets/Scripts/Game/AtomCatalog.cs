using System.Collections.Generic;
using UnityEngine;

public enum AtomFilter { Todos, Metales, Gases, NoMetales, Metaloides }

/// <summary>Datos de un elemento disponible en el selector de átomos.</summary>
[System.Serializable]
public class AtomInfo
{
    public string symbol;
    public string nameEs;
    public int    number;
    public bool   isMetal;
    public bool   isNonmetal;
    public bool   isGas;
    public bool   isMetalloid;
    public bool   diatomic;
    public Color  color;

    public string Formula   => diatomic ? symbol + "₂" : symbol; // X₂
    public string PopupText => $"{nameEs} - {Formula}";

    public bool Matches(AtomFilter f)
    {
        switch (f)
        {
            case AtomFilter.Metales:    return isMetal;
            case AtomFilter.Gases:      return isGas;
            case AtomFilter.NoMetales:  return isNonmetal;
            case AtomFilter.Metaloides: return isMetalloid;
            default:                    return true; // Todos
        }
    }

    public bool MatchesSearch(string q)
    {
        if (string.IsNullOrWhiteSpace(q)) return true;
        q = q.Trim().ToLowerInvariant();
        return symbol.ToLowerInvariant().StartsWith(q)
            || nameEs.ToLowerInvariant().Contains(q)
            || number.ToString().StartsWith(q);
    }
}

/// <summary>Catálogo estático de los 24 átomos disponibles.</summary>
public static class AtomCatalog
{
    public static readonly List<AtomInfo> All = new List<AtomInfo>
    {
        //  sym   nombre        Z   metal  noMet  gas    metaloide diat   color
        A("C",  "Carbono",      6,  false, true,  false, false, false, "4A4E5A"),
        A("H",  "Hidrógeno",    1,  false, true,  true,  false, true,  "C9CDD6"),
        A("O",  "Oxígeno",      8,  false, true,  true,  false, true,  "E0484B"),
        A("N",  "Nitrógeno",    7,  false, true,  true,  false, true,  "4A7BE8"),
        A("S",  "Azufre",       16, false, true,  false, false, false, "D4A017"),
        A("Cl", "Cloro",        17, false, true,  true,  false, true,  "6FBF4B"),
        A("F",  "Flúor",        9,  false, true,  true,  false, true,  "7ED957"),
        A("P",  "Fósforo",      15, false, true,  false, false, false, "E8893A"),
        A("Na", "Sodio",        11, true,  false, false, false, false, "F2C84B"),
        A("Br", "Bromo",        35, false, true,  false, false, true,  "A0341E"),
        A("I",  "Yodo",         53, false, true,  false, false, true,  "7E4FB0"),
        A("Si", "Silicio",      14, false, false, false, true,  false, "B0A07A"),
        A("K",  "Potasio",      19, true,  false, false, false, false, "9B6BD4"),
        A("Ca", "Calcio",       20, true,  false, false, false, false, "B8C99A"),
        A("Zn", "Zinc",         30, true,  false, false, false, false, "7C92A8"),
        A("Al", "Aluminio",     13, true,  false, false, false, false, "AEB6BF"),
        A("Li", "Litio",        3,  true,  false, false, false, false, "F0964A"),
        A("Sn", "Estaño",       50, true,  false, false, false, false, "9AA0AB"),
        A("Fe", "Hierro",       26, true,  false, false, false, false, "C8763C"),
        A("Co", "Cobalto",      27, true,  false, false, false, false, "3E63C0"),
        A("Cu", "Cobre",        29, true,  false, false, false, false, "C7783F"),
        A("B",  "Boro",         5,  false, false, false, true,  false, "C2A878"),
        A("Cr", "Cromo",        24, true,  false, false, false, false, "6E8AA0"),
        A("Mn", "Manganeso",    25, true,  false, false, false, false, "9E7BA8"),
    };

    static AtomInfo A(string sym, string name, int z, bool metal, bool nonmetal,
        bool gas, bool metalloid, bool diatomic, string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        return new AtomInfo
        {
            symbol = sym, nameEs = name, number = z,
            isMetal = metal, isNonmetal = nonmetal, isGas = gas,
            isMetalloid = metalloid, diatomic = diatomic, color = c,
        };
    }
}
