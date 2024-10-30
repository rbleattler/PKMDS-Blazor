﻿namespace Pkmds.Web;

public static class SpriteHelper
{
    private const string SpritesRoot = "sprites/";

    public static string GetPokemonSpriteFilename(PKM? pokemon) =>
        new StringBuilder($"{SpritesRoot}a/a_")
        .Append(pokemon switch
        {
            null => "unknown",
            { Context: EntityContext.Gen7b } and ({ Species: (ushort)Species.Pikachu, Form: 8 } or { Species: (ushort)Species.Eevee, Form: 1 }) => $"{pokemon.Species}-{pokemon.Form}p",
            { Species: (ushort)Species.Manaphy, IsEgg: true } => "490-e",
            { IsEgg: true } => "egg",
            { Species: (ushort)Species.Frillish or (ushort)Species.Jellicent, Gender: (byte)Gender.Female } => $"{pokemon.Species}f",
            { Species: (ushort)Species.Alcremie } => $"{pokemon.Species}-{pokemon.Form}-{pokemon.GetFormArgument(0)}",
            { Form: var form, Species: var species } when form > 0 && FormInfo.HasTotemForm(species) && FormInfo.IsTotemForm(species, form) => $"{species}-{FormInfo.GetTotemBaseForm(species, form)}",
            { Form: > 0 } => pokemon.Species switch
            {
                (ushort)Species.Scatterbug or (ushort)Species.Spewpa => pokemon.Species.ToString(),
                (ushort)Species.Urshifu => pokemon.Species.ToString(),
                _ => $"{pokemon.Species}-{pokemon.Form}",
            },
            { Species: > (ushort)Species.None and < (ushort)Species.MAX_COUNT } =>
                pokemon.Species.ToString(),
            _ => "unknown",
        })
        .Append(".png")
        .ToString();

    public static string GetBallSpriteFilename(int ball) =>
        $"{SpritesRoot}b/_ball{ball}.png";

    public static string GetBigItemSpriteFilename(int item) =>
        $"{SpritesRoot}bi/bitem_{item}.png";

    public static string GetArtworkItemSpriteFilename(int item) =>
        $"{SpritesRoot}ai/aitem_{item}.png";

    public static string GetTypeGemSpriteFileName(byte type) =>
        $"{SpritesRoot}t/g/gem_{type:00}.png";

    public static string GetTypeSquareSpriteFileName(byte type) =>
        $"{SpritesRoot}t/s/type_icon_{type:00}.png";

    public static string GetTypeWideSpriteFileName(byte type) =>
        $"{SpritesRoot}t/w/type_wide_{type:00}.png";

    // TODO: Implement
    public static string GetMoveCategorySpriteFileName(int categoryId) =>
        string.Empty;

    public static string GetSpriteCssClass(PKM? pkm) =>
        $"d-flex align-items-center justify-center {(pkm is { Species: > 0 } ? "slot-fill" : string.Empty)}";
}
