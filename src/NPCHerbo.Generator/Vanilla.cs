using Mutagen.Bethesda.Plugins;

namespace NPCHerbo.Generator;

/// <summary>FormKeys vanilla (Skyrim.esm) utilisés par le mod. Vérifiés via "dotnet run -- explore".</summary>
public static class Vanilla
{
    static FormKey K(uint id) => new(ModKey.FromFileName("Skyrim.esm"), id);

    public static readonly FormKey PlayerRef = K(0x000014);
    public static readonly FormKey Gold001 = K(0x00000F);
    public static readonly FormKey XMarker = K(0x00003B);
    public static readonly FormKey PlayerFaction = K(0x000DB1);
    public static readonly FormKey WoodElfRace = K(0x013749);
    public static readonly FormKey FemaleYoungEager = K(0x013ADC);
    public static readonly FormKey ClassCitizen = K(0x01326B);
    public static readonly FormKey ClassTrainerAlchemyExpert = K(0x0E3A6E); // formation en Alchimie jusqu'au niveau 75 (comme Arcadia)
    public static readonly FormKey[] RareIngredients = { K(0x059B86), K(0x0B701A), K(0x01BCBC) }; // Nirnroot, Nirnroot cramoisie, racine de jarrin
    public static readonly FormKey HunterOutfit01 = K(0x106988);
    public static readonly FormKey FaceTemplate = K(0x10ABA7);       // WoodElfFemalePreset10
    public static readonly FormKey SatchelContainer = K(0x0AF265);   // FFI02ReydaSatchel (modele Satchel.nif)
    public static readonly FormKey FollowPlayerPackage = K(0x10ACD1); // HowlSummonedWolfFollowPlayer (template Follow)
    public static readonly FormKey IdleSearchingChest = K(0x06FF14);
    // Répliques vanilla doublées en FemaleYoungEager (fichier présent dans Skyrim - Voices_fr0.bsa)
    public static readonly uint[] LinesFoundPlant =
    {
        0x0228BA, // « Regardez ça ! »
        0x0AB979, // « Oh, regardez toutes ces couleurs ! »
        0x017260, // « Oh, ça a l'air charmant. »
        0x013A74, // « Oh là ! »
    };
    public static readonly uint[] LinesHarvestDone =
    {
        0x036007, // « Parfois ça paye de regarder dans le moindre recoin. »
    };

    public static readonly string HoldingCellEditorId = "AAADeleteWhenDoneTestJeremy"; // cellule de dev inaccessible (06C3B6)
    public static readonly string TravelToAliasPackageEditorId = "VC01FalionAtSummoningCircle"; // template Travel, cible = alias
}
