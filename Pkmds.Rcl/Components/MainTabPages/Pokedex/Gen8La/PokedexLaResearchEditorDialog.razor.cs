using static PKHeX.Core.PokedexResearchTaskType8a;

namespace Pkmds.Rcl.Components.MainTabPages.Pokedex.Gen8La;

public partial class PokedexLaResearchEditorDialog : BasePkmdsComponent
{
    [CascadingParameter]
    private IMudDialogInstance? MudDialog { get; set; }

    [Parameter]
    [EditorRequired]
    public ushort SpeciesId { get; set; }

    private string speciesName = string.Empty;

    // (label, task type, task index)
    private List<(string Label, PokedexResearchTaskType8a Task, int Idx)> catchTasks = [];
    private List<(string Label, PokedexResearchTaskType8a Task, int Idx)> battleTasks = [];
    private List<(string Label, PokedexResearchTaskType8a Task, int Idx)> interactTasks = [];
    private List<(string Label, PokedexResearchTaskType8a Task, int Idx)> observeTasks = [];

    private string[] taskDescriptions = [];
    private string[] timeTaskDescriptions = [];

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (AppState.SaveFile is not SAV8LA)
        {
            return;
        }

        var names = GameInfo.Strings.Species;
        speciesName = SpeciesId < names.Count ? names[SpeciesId] : SpeciesId.ToString();

        taskDescriptions = Util.GetStringList("tasks8a", "en");
        timeTaskDescriptions = Util.GetStringList("time_tasks8a", "en");

        BuildTaskLists();
    }

    private void BuildTaskLists()
    {
        catchTasks = [];
        battleTasks = [];
        interactTasks = [];
        observeTasks = [];

        // Get species-specific tasks to resolve move/type parameters
        var hisuiIdx = PokedexSave8a.GetDexIndex(PokedexType8a.Hisui, SpeciesId);
        var speciesTasks = hisuiIdx > 0
            ? PokedexConstants8a.ResearchTasks[hisuiIdx - 1]
            : ReadOnlySpan<PokedexResearchTask8a>.Empty;

        // Build parameter lookup from species tasks
        var moveParams = new Dictionary<int, int>(); // idx → move id
        var typeParams = new Dictionary<int, int>(); // idx → type id
        var catchAtTimeParam = 0;

        foreach (var t in speciesTasks)
        {
            switch (t.Task)
            {
                case UseMove:
                    moveParams.TryAdd(t.Index, t.Move);
                    break;
                case DefeatWithMoveType:
                    typeParams.TryAdd(t.Index, (int)t.Type);
                    break;
                case CatchAtTime:
                    catchAtTimeParam = (int)t.TimeOfDay;
                    break;
            }
        }

        // ── Catch ──
        catchTasks.Add((GetLabel(Catch, -1, -1), Catch, -1));
        catchTasks.Add((GetLabel(CatchAlpha, -1, -1), CatchAlpha, -1));
        catchTasks.Add((GetLabel(CatchLarge, -1, -1), CatchLarge, -1));
        catchTasks.Add((GetLabel(CatchSmall, -1, -1), CatchSmall, -1));
        catchTasks.Add((GetLabel(CatchHeavy, -1, -1), CatchHeavy, -1));
        catchTasks.Add((GetLabel(CatchLight, -1, -1), CatchLight, -1));
        catchTasks.Add((GetLabel(CatchAtTime, -1, catchAtTimeParam), CatchAtTime, -1));
        catchTasks.Add((GetLabel(CatchSleeping, -1, -1), CatchSleeping, -1));
        catchTasks.Add((GetLabel(CatchInAir, -1, -1), CatchInAir, -1));
        catchTasks.Add((GetLabel(CatchNotSpotted, -1, -1), CatchNotSpotted, -1));

        // ── Battle ──
        for (var i = 0; i < 4; i++)
        {
            var param = moveParams.TryGetValue(i, out var mv) ? mv : -1;
            battleTasks.Add((GetLabel(UseMove, i, param), UseMove, i));
        }

        for (var i = 0; i < 3; i++)
        {
            var param = typeParams.TryGetValue(i, out var tp) ? tp : -1;
            battleTasks.Add((GetLabel(DefeatWithMoveType, i, param), DefeatWithMoveType, i));
        }

        battleTasks.Add((GetLabel(Defeat, -1, -1), Defeat, -1));
        battleTasks.Add((GetLabel(UseStrongStyleMove, -1, -1), UseStrongStyleMove, -1));
        battleTasks.Add((GetLabel(UseAgileStyleMove, -1, -1), UseAgileStyleMove, -1));

        // ── Interact ──
        interactTasks.Add((GetLabel(Evolve, -1, -1), Evolve, -1));
        interactTasks.Add((GetLabel(GiveFood, -1, -1), GiveFood, -1));
        interactTasks.Add((GetLabel(StunWithItems, -1, -1), StunWithItems, -1));
        interactTasks.Add((GetLabel(ScareWithScatterBang, -1, -1), ScareWithScatterBang, -1));
        interactTasks.Add((GetLabel(LureWithPokeshiDoll, -1, -1), LureWithPokeshiDoll, -1));

        // ── Observe ──
        observeTasks.Add((GetLabel(LeapFromTrees, -1, -1), LeapFromTrees, -1));
        observeTasks.Add((GetLabel(LeapFromLeaves, -1, -1), LeapFromLeaves, -1));
        observeTasks.Add((GetLabel(LeapFromSnow, -1, -1), LeapFromSnow, -1));
        observeTasks.Add((GetLabel(LeapFromOre, -1, -1), LeapFromOre, -1));
        observeTasks.Add((GetLabel(LeapFromTussocks, -1, -1), LeapFromTussocks, -1));
    }

    private string GetLabel(PokedexResearchTaskType8a task, int idx, int param) =>
        PokedexResearchTask8aExtensions.GetGenericTaskLabelString(task, idx, param, taskDescriptions, timeTaskDescriptions);

    private void OnValueChanged(PokedexSave8a dex, PokedexResearchTaskType8a task, int idx, int value)
    {
        dex.SetResearchTaskProgressByForce(SpeciesId, task, value, idx);
        StateHasChanged();
    }

    private void Close(PokedexSave8a dex)
    {
        dex.UpdateSpecificReportPoke(SpeciesId);
        MudDialog?.Close();
    }
}
