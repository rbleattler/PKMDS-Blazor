namespace Pkmds.Rcl.Components.MainTabPages;

public partial class RecordsTab
{
    [Parameter]
    [EditorRequired]
    public SAV3? SaveFile { get; set; }

    private int CurrentRecordIndex { get; set; }

    private uint? CurrentRecordValue { get; set; }

    private IList<ComboItem> RecordComboItems { get; set; } = [];

    private uint HallOfFameHours { get; set; }

    private byte HallOfFameMinutes { get; set; }

    private byte HallOfFameSeconds { get; set; }

    private bool HallOfFameIndexSelected => (SaveFile, CurrentRecordIndex) switch
    {
        (SAV3RS, (int)RecID3RuSa.FIRST_HOF_PLAY_TIME) => true,
        (SAV3E, (int)RecID3Emerald.FIRST_HOF_PLAY_TIME) => true,
        (SAV3FRLG, (int)RecID3FRLG.FIRST_HOF_PLAY_TIME) => true,
        _ => false
    };

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        LoadRecords();
    }

    private void LoadRecords()
    {
        if (SaveFile is null)
        {
            return;
        }

        RecordComboItems = Record3.GetItems(SaveFile);
        GetRecord();
    }

    private void GetRecord()
    {
        if (SaveFile is null)
        {
            return;
        }

        CurrentRecordValue = SaveFile.GetRecord(CurrentRecordIndex);

        if (HallOfFameIndexSelected)
        {
            SetFameTime(CurrentRecordValue ?? 0U);
        }
    }

    private void SetCurrentRecordValue(uint? newValue)
    {
        if (SaveFile is null)
        {
            return;
        }

        CurrentRecordValue = newValue;
        SaveFile.SetRecord(CurrentRecordIndex, newValue ?? 0U);

        if (HallOfFameIndexSelected)
        {
            SetFameTime(newValue ?? 0U);
        }
    }

    private void ChangeFame()
    {
        if (!HallOfFameIndexSelected || SaveFile is null)
        {
            return;
        }

        SaveFile.SetRecord(1, (uint)(CurrentRecordValue = GetFameTime()));
    }

    private uint GetFameTime()
    {
        if (!HallOfFameIndexSelected || SaveFile is null)
        {
            return 0U;
        }

        var hrs = Math.Min(9999U, HallOfFameHours);
        var min = Math.Min((byte)59, HallOfFameMinutes);
        var sec = Math.Min((byte)59, HallOfFameSeconds);

        return hrs << 16 | (uint)min << 8 | sec;
    }

    private void SetFameTime(uint time)
    {
        if (!HallOfFameIndexSelected || SaveFile is null)
        {
            return;
        }

        HallOfFameHours = Math.Min(9999U, time >> 16);
        HallOfFameMinutes = Math.Min((byte)59, (byte)(time >> 8));
        HallOfFameSeconds = Math.Min((byte)59, (byte)time);
    }
}
