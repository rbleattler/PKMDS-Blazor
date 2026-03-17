namespace Pkmds.Rcl.Components.MainTabPages.Pokedex.Gen4;

public partial class PokedexGen4SpeciesPanel
{
    [Parameter]
    [EditorRequired]
    public ushort SpeciesId { get; set; }

    private void OnCaughtChanged(Zukan4 dex, bool value)
    {
        dex.SetCaught(SpeciesId, value);
        if (value && !dex.GetSeen(SpeciesId))
        {
            dex.SetSeen(SpeciesId);
            var pi = (AppState.SaveFile as SAV4)!.Personal[SpeciesId];
            var gender = (byte)(pi.RandomGender() & 1);
            dex.SetSeenGenderNewFlag(SpeciesId, gender);
        }
        StateHasChanged();
    }

    private void OnSeenChanged(Zukan4 dex, bool value)
    {
        if (!value)
        {
            dex.ClearSeen(SpeciesId);
        }
        else
        {
            dex.SetSeen(SpeciesId);
            var pi = (AppState.SaveFile as SAV4)!.Personal[SpeciesId];
            var gender = (byte)(pi.RandomGender() & 1);
            dex.SetSeenGenderNewFlag(SpeciesId, gender);
        }
        StateHasChanged();
    }

    private void OnFirstGenderChanged(Zukan4 dex, int newFirst)
    {
        var single = dex.GetSeenSingleGender(SpeciesId);
        dex.SetSeenGenderFirst(SpeciesId, newFirst);
        if (single)
            dex.SetSeenGenderSecond(SpeciesId, newFirst);
        StateHasChanged();
    }

    private void OnBothGendersChanged(Zukan4 dex, int firstGender, bool bothSeen)
    {
        if (bothSeen)
            dex.SetSeenGenderSecond(SpeciesId, firstGender ^ 1);
        else
            dex.SetSeenGenderSecond(SpeciesId, firstGender);
        StateHasChanged();
    }

    private void OnFormChanged(Zukan4 dex, byte formIdx, bool value)
    {
        if (SpeciesId == (ushort)Species.Unown)
        {
            if (value)
                dex.AddUnownForm(formIdx);
            else
            {
                // Remove by rebuilding forms list without this entry
                var current = dex.GetForms(SpeciesId).ToList();
                current.Remove(formIdx);
                dex.SetForms(SpeciesId, [.. current]);
            }
        }
        else
        {
            var current = dex.GetForms(SpeciesId).ToArray();
            if (value)
            {
                // Add if not present
                if (!current.Contains(formIdx))
                {
                    var list = current.Where(b => b != Zukan4.FORM_NONE).ToList();
                    list.Add(formIdx);
                    dex.SetForms(SpeciesId, [.. list]);
                }
            }
            else
            {
                var list = current.Where(b => b != Zukan4.FORM_NONE && b != formIdx).ToList();
                dex.SetForms(SpeciesId, [.. list]);
            }
        }
        StateHasChanged();
    }

    private void OnSpindaPidChanged(Zukan4 dex, string hex)
    {
        if (uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var pid))
            dex.SpindaPID = pid;
    }
}
