using System.Diagnostics;

using Pkmds.Rcl.Models;

namespace Pkmds.Rcl.Services;

/// <summary>
/// Auto-Legality engine ported from the PKHeX ALM plugin, adapted for Blazor WASM.
/// In WASM, responsiveness comes from cooperative yielding (Task.Yield) inside the
/// legalization loop rather than wrapping the work in Task.Run.
/// </summary>
public sealed class LegalizationService : ILegalizationService
{
    /// <summary>Maximum wall-clock time (seconds) for a single legalization attempt.</summary>
    private const int TimeoutSeconds = 15;

    public async Task<LegalizationOutcome> LegalizeAsync(
        PKM pk,
        SaveFile sav,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        // Build a ShowdownSet from the existing PKM so the core loop can treat both
        // "legalize existing" and "generate from set" uniformly.
        var set = new ShowdownSet(ShowdownParsing.GetShowdownText(pk));
        return await CoreLegalizeAsync(set, pk, sav, progress, ct);
    }

    public async Task<LegalizationOutcome> GenerateFromSetAsync(
        ShowdownSet set,
        SaveFile sav,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        return await CoreLegalizeAsync(set, template: null, sav, progress, ct);
    }

    public LegalizationOutcome GenerateFromSetSync(ShowdownSet set, SaveFile sav)
    {
        // Synchronous path: run the core loop without yielding. Acceptable because
        // most generation completes in <100ms and the caller (ConvertShowdownSetToPkm)
        // is already synchronous.
        return CoreLegalizeSync(set, template: null, sav);
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Core legalization loop
    // ──────────────────────────────────────────────────────────────────────

    private async Task<LegalizationOutcome> CoreLegalizeAsync(
        ShowdownSet set,
        PKM? template,
        SaveFile sav,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        // In Blazor WASM, responsiveness comes from cooperative yielding inside the
        // legalization loop rather than wrapping the work in Task.Run.
        return await RunLegalizationLoop(set, template, sav, progress, ct, async: true);
    }

    private LegalizationOutcome CoreLegalizeSync(
        ShowdownSet set,
        PKM? template,
        SaveFile sav)
    {
        return RunLegalizationLoop(set, template, sav, progress: null, ct: default, async: false)
            .GetAwaiter().GetResult();
    }

    private async Task<LegalizationOutcome> RunLegalizationLoop(
        ShowdownSet set,
        PKM? existingPkm,
        SaveFile sav,
        IProgress<string>? progress,
        CancellationToken ct,
        bool @async)
    {
        if (set.Species == 0)
        {
            return new LegalizationOutcome(
                sav.BlankPKM,
                LegalizationStatus.Failed,
                "No species specified.");
        }

        var blank = sav.BlankPKM;
        var destType = blank.GetType();

        // Build the probe template for encounter searching.
        var probe = blank.Clone();
        probe.Species = set.Species;
        probe.Form = set.Form;
        probe.CurrentLevel = set.Level;

        // Toxtricity form is nature-locked.
        if (probe.Species == (int)Species.Toxtricity && set.Nature != Nature.Random)
        {
            probe.Form = (byte)ToxtricityUtil.GetAmpLowKeyResult(set.Nature);
        }

        EncounterMovesetGenerator.OptimizeCriteria(probe, sav);

        // Build game version list, newest-first.
        var versions = GameUtil.GetVersionsWithinRange(probe)
            .OrderByDescending(v => (int)v)
            .ToArray();

        // Build encounter criteria from the set.
        var allowed = EncounterMutationUtil.GetSuggested(probe.Context, (byte)set.Level);
        var criteria = EncounterCriteria.GetCriteria(set, probe.PersonalInfo, allowed);

        // When legalizing an existing Pokémon, don't constrain the encounter search by
        // moves — the PKM may have illegal moves (e.g. Charmander with Volt Tackle) that
        // would cause the generator to yield zero encounters. We fix moves post-generation.
        // For Showdown imports, use the requested moves so the generator finds encounters
        // that can learn them.
        var searchMoves = existingPkm is not null
            ? ReadOnlyMemory<ushort>.Empty
            : set.Moves.AsMemory();
        var encounters = GetAllEncounters(probe, searchMoves, versions);

        var timer = Stopwatch.StartNew();
        PKM? bestAttempt = null;
        var attemptCount = 0;

        foreach (var enc in encounters)
        {
            if (ct.IsCancellationRequested)
            {
                return new LegalizationOutcome(
                    bestAttempt ?? blank,
                    LegalizationStatus.Failed,
                    "Cancelled.");
            }

            if (timer.Elapsed.TotalSeconds >= TimeoutSeconds)
            {
                return new LegalizationOutcome(
                    bestAttempt ?? blank,
                    LegalizationStatus.Timeout,
                    $"Timed out after {TimeoutSeconds}s ({attemptCount} encounters tried).");
            }

            attemptCount++;

            // Pre-filter: skip encounters that are obviously incompatible.
            if (!IsEncounterValid(set, enc))
            {
                continue;
            }

            progress?.Report($"Trying encounter {attemptCount}: {enc.GetType().Name}...");

            // Yield to the browser event loop every encounter attempt (each is expensive).
            if (@async)
            {
                await Task.Yield();
            }

            try
            {
                // Generate PKM from encounter.
                var adjustedCriteria = AdjustCriteriaForEncounter(criteria, enc, set);
                var raw = enc.ConvertToPKM(sav, adjustedCriteria);

                // Restore OT if blank.
                if (raw.OriginalTrainerName.Length == 0)
                {
                    raw.Language = sav.Language;
                    sav.ApplyTo(raw);
                }

                // Handle egg encounters.
                if (raw.IsEgg)
                {
                    HatchEgg(raw, sav);
                }

                // Apply RNG-correlated PID/IV for encounter types that require it.
                PreSetPidIv(raw, enc, set, adjustedCriteria);

                // Convert to target format.
                var pk = EntityConverter.ConvertToType(raw, destType, out _);
                if (pk is null)
                {
                    continue;
                }

                // Apply the user's requested details on top of the legal base.
                pk.ApplySetDetails(set);

                // Post-application fixes.
                ApplyPostGenerationFixes(pk, sav, enc, set);

                // Validate the result.
                var la = new LegalityAnalysis(pk);
                if (la.Valid && pk.Species == set.Species)
                {
                    pk.RefreshChecksum();
                    return new LegalizationOutcome(pk, LegalizationStatus.Success);
                }

                bestAttempt = pk;
            }
            catch
            {
                // Encounter conversion can throw for incompatible formats; skip silently.
                continue;
            }
        }

        return new LegalizationOutcome(
            bestAttempt ?? blank,
            LegalizationStatus.Failed,
            $"No legal result found after {attemptCount} encounters.");
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Encounter enumeration (including alternate forms)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Yields encounters for the requested species+form, then tries alternate forms.
    /// Mirrors ALM's GetAllEncounters pattern.
    /// </summary>
    private static IEnumerable<IEncounterable> GetAllEncounters(
        PKM probe,
        ReadOnlyMemory<ushort> moves,
        GameVersion[] versions)
    {
        // Primary: requested form.
        foreach (var enc in EncounterMovesetGenerator.GenerateEncounters(probe, moves, versions))
        {
            yield return enc;
        }

        // Secondary: try other forms that might be convertible (e.g. pre-evolution forms).
        var origForm = probe.Form;
        var pi = probe.PersonalInfo;
        var fc = pi.FormCount;
        if (fc == 0)
        {
            yield break;
        }

        for (byte f = 0; f < fc; f++)
        {
            if (f == origForm)
            {
                continue;
            }

            if (FormInfo.IsBattleOnlyForm(probe.Species, f, probe.Format))
            {
                continue;
            }

            probe.Form = f;
            probe.SetGender(probe.GetSaneGender());

            foreach (var enc in EncounterMovesetGenerator.GenerateEncounters(probe, moves, versions))
            {
                yield return enc;
            }
        }

        // Restore original form.
        probe.Form = origForm;
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Encounter pre-filter
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Quick pre-filter to skip encounters that cannot possibly match the request.
    /// Ported from ALM's IsEncounterValid.
    /// </summary>
    private static bool IsEncounterValid(ShowdownSet set, IEncounterable enc)
    {
        // Level check: encounter minimum must not exceed requested level (Gen 3+).
        if (enc.Generation > 2 && enc.LevelMin > set.Level)
        {
            return false;
        }

        // Shiny compatibility.
        if (set.Shiny && enc.Shiny == Shiny.Never)
        {
            return false;
        }

        if (!set.Shiny && enc.Shiny.IsShiny())
        {
            return false;
        }

        // Gender lock.
        if (set.Gender != -1 && enc is IFixedGender { IsFixedGender: true } fg && fg.Gender != set.Gender)
        {
            return false;
        }

        return true;
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Criteria adjustment per encounter
    // ──────────────────────────────────────────────────────────────────────

    private static EncounterCriteria AdjustCriteriaForEncounter(
        EncounterCriteria criteria,
        IEncounterable enc,
        ShowdownSet set)
    {
        // Fixed-nature encounters: let the encounter decide the nature.
        if (enc is IFixedNature { IsFixedNature: true })
        {
            criteria = criteria with { Nature = Nature.Random };
        }

        return criteria;
    }

    // ──────────────────────────────────────────────────────────────────────
    //  PID/IV pre-setting for RNG-correlated encounters
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Applies RNG-correlated PID/IV for encounter types that require it.
    /// Ported from ALM's PreSetPIDIV. This is what fixes the Gen 3-5 PID correlation issue.
    /// </summary>
    private static void PreSetPidIv(
        PKM pk,
        IEncounterable enc,
        ShowdownSet set,
        EncounterCriteria criteria)
    {
        // Gen 9 Tera Raids: use PKHeX.Core's built-in TryApply.
        if (enc is ITeraRaid9)
        {
            ApplyTeraRaidPidIv(pk, enc, set, criteria);
            return;
        }

        // Gen 8 SwSh overworld correlation.
        if (enc is IOverworldCorrelation8 ow && pk is PK8 pk8)
        {
            if (ow.GetRequirement(pk8) == OverworldCorrelation8Requirement.MustHave)
            {
                var flawless = enc is IFlawlessIVCount f ? f.FlawlessIVCount : 0;
                var shiny = set.Shiny ? Shiny.Always : Shiny.Never;
                FindWildPidIv8(pk8, shiny, flawless);
            }

            return;
        }

        // Gen 8b BDSP static correlation.
        if (enc is IStaticCorrelation8b sc && pk is PB8 pb8)
        {
            if (sc.GetRequirement(pb8) == StaticCorrelation8bRequirement.MustHave)
            {
                var flawless = enc is IFlawlessIVCount f ? f.FlawlessIVCount : 0;
                var shiny = set.Shiny ? Shiny.Always : Shiny.Never;
                Roaming8bRNG.ApplyDetails(pb8, EncounterCriteria.Unrestricted, shiny, flawless);
            }

            return;
        }

        // Gen 3-5: PID↔IV↔Nature correlation via LCRNG.
        if (enc.Generation is 3 or 4 or 5 && enc is not MysteryGift && enc is not IEncounterEgg)
        {
            ApplyClassicPidIv(pk, enc, set);
        }
    }

    /// <summary>
    /// Gen 9 Tera Raid PID/IV generation via XOROSHIRO.
    /// </summary>
    private static void ApplyTeraRaidPidIv(
        PKM pk,
        IEncounterable enc,
        ShowdownSet set,
        EncounterCriteria criteria)
    {
        if (pk is not PK9 pk9)
        {
            return;
        }

        // Try up to 15,000 seeds to find one that satisfies the criteria.
        for (var i = 0; i < 15_000; i++)
        {
            var seed = (ulong)Random.Shared.NextInt64();
            switch (enc)
            {
                case EncounterTera9 e:
                    if (e.TryApply32(pk9, seed, GetTeraParam(e, pk9), criteria))
                    {
                        ApplyTeraType(pk9, set);
                        return;
                    }

                    break;
                case EncounterDist9 e:
                    if (e.TryApply32(pk9, seed, GetDistParam(e, pk9), criteria))
                    {
                        ApplyTeraType(pk9, set);
                        return;
                    }

                    break;
                case EncounterMight9 e:
                    if (e.TryApply32(pk9, seed, GetMightParam(e, pk9), criteria))
                    {
                        ApplyTeraType(pk9, set);
                        return;
                    }

                    break;
            }
        }
    }

    private static void ApplyTeraType(PK9 pk9, ShowdownSet set)
    {
        if (set.TeraType != MoveType.Any && set.TeraType != pk9.TeraType)
        {
            pk9.SetTeraType(set.TeraType);
        }
    }

    private static GenerateParam9 GetTeraParam(EncounterTera9 e, PK9 pk)
    {
        var pi = PersonalTable.SV.GetFormEntry(e.Species, e.Form);
        return new GenerateParam9(pk.Species, pi.Gender, e.FlawlessIVCount, 1, 0, 0, 0, 0, e.Ability, e.Shiny);
    }

    private static GenerateParam9 GetDistParam(EncounterDist9 e, PK9 pk)
    {
        var pi = PersonalTable.SV.GetFormEntry(e.Species, e.Form);
        return new GenerateParam9(pk.Species, pi.Gender, e.FlawlessIVCount, 1, 0, 0, e.ScaleType, e.Scale, e.Ability, e.Shiny, IVs: e.IVs);
    }

    private static GenerateParam9 GetMightParam(EncounterMight9 e, PK9 pk)
    {
        var pi = PersonalTable.SV.GetFormEntry(e.Species, e.Form);
        return new GenerateParam9(pk.Species, pi.Gender, e.FlawlessIVCount, 1, 0, 0, e.ScaleType, e.Scale, e.Ability, e.Shiny, e.Nature, e.IVs);
    }

    /// <summary>
    /// Gen 8 SwSh wild encounter PID/IV via XOROSHIRO128+.
    /// Ported from ALM's FindWildPIDIV8.
    /// The entire EC→PID→IV→Height→Weight sequence must derive from the same original seed
    /// to satisfy the overworld correlation legality checks.
    /// </summary>
    private static void FindWildPidIv8(PK8 pk, Shiny shiny, int flawless)
    {
        static bool MatchesShinyConstraint(Shiny s, uint xor) => s switch
        {
            Shiny.Never => xor >= 16,
            Shiny.AlwaysSquare => xor == 0,
            Shiny.AlwaysStar => xor is > 0 and < 16,
            Shiny.Always => xor < 16,
            _ => true,
        };

        // Search for a seed whose EC→PID satisfies the shiny constraint.
        uint matchedSeed;
        uint matchedEc;
        uint matchedPid;
        while (true)
        {
            var seed = (uint)Random.Shared.Next();
            var rng = new Xoroshiro128Plus(seed);

            var ec = (uint)rng.NextInt();
            var pid = (uint)rng.NextInt();
            var xor = (uint)(((pid >> 16) ^ (pid & 0xFFFF) ^ pk.TID16 ^ pk.SID16) & 0xFFFF);
            if (!MatchesShinyConstraint(shiny, xor))
            {
                continue;
            }

            matchedSeed = seed;
            matchedEc = ec;
            matchedPid = pid;
            break;
        }

        pk.EncryptionConstant = matchedEc;
        pk.PID = matchedPid;

        // Re-initialize RNG from the matched original seed to continue the IV sequence.
        // Replay from EC generation since two calls were consumed for EC and PID.
        var rng2 = new Xoroshiro128Plus(matchedSeed);
        _ = rng2.NextInt(); // EC
        _ = rng2.NextInt(); // PID

        var ivs = new[] { -1, -1, -1, -1, -1, -1 };
        const int unset = -1;
        const int max = 31;
        for (var i = 0; i < flawless; i++)
        {
            int index;
            do
            {
                index = (int)rng2.NextInt(6);
            } while (ivs[index] != unset);

            ivs[index] = max;
        }

        for (var i = 0; i < 6; i++)
        {
            if (ivs[i] == unset)
            {
                ivs[i] = (int)rng2.NextInt(32);
            }
        }

        pk.IV_HP = ivs[0];
        pk.IV_ATK = ivs[1];
        pk.IV_DEF = ivs[2];
        pk.IV_SPA = ivs[3];
        pk.IV_SPD = ivs[4];
        pk.IV_SPE = ivs[5];

        var height = (int)rng2.NextInt(0x81) + (int)rng2.NextInt(0x80);
        var weight = (int)rng2.NextInt(0x81) + (int)rng2.NextInt(0x80);
        pk.HeightScalar = (byte)height;
        pk.WeightScalar = (byte)weight;
    }

    /// <summary>
    /// Gen 3-5 PID↔IV correlation via classic LCRNG (Method 1).
    /// Finds a seed whose PID satisfies nature, gender, ability, and shiny constraints,
    /// then derives IVs from the same RNG sequence.
    /// Uses the same approach as PidEcDialog.GenerateWithMethod.
    /// </summary>
    private static void ApplyClassicPidIv(PKM pk, IEncounterable enc, ShowdownSet set)
    {
        // Gen 5 PID is not RNG-correlated.
        if (enc.Generation == 5)
        {
            return;
        }

        // Skip encounter types with fixed PIDs.
        if (enc is EncounterTrade3 or EncounterTrade4PID or EncounterTrade4RanchGift or EncounterSlot3XD)
        {
            return;
        }

        var desiredNature = (byte)pk.Nature;
        var gr = pk.PersonalInfo.Gender;
        var isDualGender = pk.PersonalInfo.IsDualGender;

        const int maxAttempts = 1_000_000;
        for (var count = 0; count < maxAttempts; count++)
        {
            var seed = (uint)Random.Shared.Next();
            var pid = ClassicEraRNG.GetSequentialPID(ref seed);

            // Nature must match (PID % 25).
            if (pid % 25 != desiredNature)
            {
                continue;
            }

            // Gender must match for dual-gender species.
            if (isDualGender && EntityGender.GetFromPIDAndRatio(pid, gr) != pk.Gender)
            {
                continue;
            }

            // Shiny match.
            pk.PID = pid;
            if (set.Shiny && !pk.IsShiny)
            {
                continue;
            }

            if (!set.Shiny && pk.IsShiny)
            {
                continue;
            }

            // Unown form must match.
            if (pk.Species == (int)Species.Unown && enc.Generation == 3
                && pk.Form != EntityPID.GetUnownForm3(pid))
            {
                continue;
            }

            // Derive IVs from the same LCRNG sequence (Method 1).
            var ivs = ClassicEraRNG.GetSequentialIVs(ref seed);
            pk.SetIVs(ivs);
            return;
        }

        // Exhausted attempts — PID stays as-is (best effort).
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Post-generation fixes
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Applies fixes after <see cref="CommonEdits.ApplySetDetails"/> to ensure legality.
    /// Mirrors AppService.ApplyPostImportFixes + ALM's ApplySetDetails final tweaks.
    /// </summary>
    private static void ApplyPostGenerationFixes(
        PKM pk,
        SaveFile sav,
        IEncounterable enc,
        ShowdownSet set)
    {
        // Clamp level to encounter minimum.
        if (pk.CurrentLevel < enc.LevelMin)
        {
            pk.CurrentLevel = enc.LevelMin;
        }

        // Clamp MetLevel to CurrentLevel.
        if (pk.MetLevel > pk.CurrentLevel)
        {
            pk.MetLevel = pk.CurrentLevel;
        }

        // Clamp ObedienceLevel (Gen 9).
        if (pk is IObedienceLevel obLevel && obLevel.ObedienceLevel > pk.CurrentLevel)
        {
            obLevel.ObedienceLevel = pk.CurrentLevel;
        }

        // Restore fixed-nature encounters overwritten by ApplySetDetails.
        if (enc is IFixedNature { IsFixedNature: true } fn)
        {
            pk.Nature = fn.Nature;
            pk.StatNature = fn.Nature;
        }

        // Toxtricity form ↔ nature lock.
        if (pk.Species == (int)Species.Toxtricity)
        {
            pk.Form = (byte)ToxtricityUtil.GetAmpLowKeyResult(pk.Nature);
        }

        // Validate moves: if any are illegal for this encounter, replace with suggested moves.
        // This handles cases like a Charmander with Volt Tackle — ApplySetDetails re-applied
        // the illegal move from the Showdown set, so we need to fix it here.
        {
            var moveLa = new LegalityAnalysis(pk);
            if (!MoveResult.AllValid(moveLa.Info.Moves))
            {
                Span<ushort> suggestedMoves = stackalloc ushort[4];
                moveLa.GetSuggestedCurrentMoves(suggestedMoves);
                pk.SetMoves(suggestedMoves);
                pk.FixMoves();
            }
        }

        // Suggest relearn moves.
        if (!pk.FatefulEncounter)
        {
            Span<ushort> suggestedRelearn = stackalloc ushort[4];
            var la = new LegalityAnalysis(pk);
            la.GetSuggestedRelearnMovesFromEncounter(suggestedRelearn, enc);
            pk.SetRelearnMoves(suggestedRelearn);
        }

        // Recalculate PP from actual moves + PP Ups to avoid "PP above allowed" issues.
        pk.HealPP();

        // Fill language.
        if (pk.Language <= 0)
        {
            pk.Language = sav.Language > 0 ? sav.Language : (int)LanguageID.English;
        }

        // Clear invalid held items.
        if (pk.HeldItem != 0 && !sav.HeldItems.Contains((ushort)pk.HeldItem))
        {
            pk.HeldItem = 0;
        }

        // Default nickname.
        if (string.IsNullOrEmpty(pk.Nickname))
        {
            pk.SetDefaultNickname();
        }

        // Average height/weight if unset.
        if (pk is IScaledSize ss && ss.HeightScalar == 0 && ss.WeightScalar == 0)
        {
            ss.HeightScalar = 0x80;
            ss.WeightScalar = 0x80;
        }

        // FormArgument for evolution-requiring species.
        if (pk is IFormArgument fa && enc.Species != pk.Species)
        {
            var minArg = GetFormArgumentMinEvolution(pk.Species, enc.Species);
            if (fa.FormArgument < minArg)
            {
                fa.FormArgument = minArg;
            }
        }

        // HOME tracker.
        if (pk is IHomeTrack { HasTracker: false } ht)
        {
            ht.Tracker = 1;
            if (pk is IScaledSize3 ss3 && pk is IScaledSize ss2)
            {
                ss3.Scale = ss2.HeightScalar;
            }
        }

        // Set all obtainable ribbons.
        var laForRibbons = new LegalityAnalysis(pk);
        RibbonApplicator.SetAllValidRibbons(laForRibbons);

        // Suggest legal ball.
        BallApplicator.ApplyBallLegalByColor(pk);

        // Hyper Training for Lv100 with imperfect IVs.
        if (pk.CurrentLevel == 100 && pk is IHyperTrain ht2)
        {
            ApplyHyperTraining(pk, ht2, set);
        }

        pk.RefreshChecksum();
    }

    /// <summary>
    /// Applies Hyper Training to IVs that don't match the requested values.
    /// </summary>
    private static void ApplyHyperTraining(PKM pk, IHyperTrain ht, ShowdownSet set)
    {
        if (pk.IV_HP != set.IVs[0] && set.IVs[0] == 31)
        {
            ht.HT_HP = true;
        }

        if (pk.IV_ATK != set.IVs[1] && set.IVs[1] == 31)
        {
            ht.HT_ATK = true;
        }

        if (pk.IV_DEF != set.IVs[2] && set.IVs[2] == 31)
        {
            ht.HT_DEF = true;
        }

        if (pk.IV_SPA != set.IVs[3] && set.IVs[3] == 31)
        {
            ht.HT_SPA = true;
        }

        if (pk.IV_SPD != set.IVs[4] && set.IVs[4] == 31)
        {
            ht.HT_SPD = true;
        }

        if (pk.IV_SPE != set.IVs[5] && set.IVs[5] == 31)
        {
            ht.HT_SPE = true;
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Egg handling
    // ──────────────────────────────────────────────────────────────────────

    private static void HatchEgg(PKM pk, SaveFile sav)
    {
        pk.IsEgg = false;
        pk.EggMetDate = pk.MetDate;
        pk.EggLocation = pk.MetLocation;
        pk.MetDate = DateOnly.FromDateTime(DateTime.Now);
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Utility
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Forces a PID to be shiny with the given XOR target.
    /// </summary>
    internal static uint GetShinyPid(int tid, int sid, uint pid, int type) =>
        (uint)(((tid ^ sid ^ (pid & 0xFFFF) ^ type) << 16) | (pid & 0xFFFF));

    /// <summary>
    /// Mirrors IFormArgument.GetFormArgumentMinEvolution.
    /// Inlined because that method uses C#14 extension syntax unavailable in the consumed NuGet package.
    /// </summary>
    private static uint GetFormArgumentMinEvolution(ushort currentSpecies, ushort originalSpecies) =>
        originalSpecies switch
        {
            (int)Species.Yamask when currentSpecies == (int)Species.Runerigus => 49u,
            (int)Species.Qwilfish when currentSpecies == (int)Species.Overqwil => 20u,
            (int)Species.Stantler when currentSpecies == (int)Species.Wyrdeer => 20u,
            (int)Species.Basculin when currentSpecies == (int)Species.Basculegion => 294u,
            (int)Species.Mankey or (int)Species.Primeape when currentSpecies == (int)Species.Annihilape => 20u,
            (int)Species.Pawniard or (int)Species.Bisharp when currentSpecies == (int)Species.Kingambit => 3u,
            (int)Species.Farfetchd when currentSpecies == (int)Species.Sirfetchd => 3u,
            (int)Species.Gimmighoul when currentSpecies == (int)Species.Gholdengo => 999u,
            _ => 0u,
        };
}
