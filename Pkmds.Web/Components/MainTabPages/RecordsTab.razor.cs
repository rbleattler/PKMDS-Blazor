namespace Pkmds.Web.Components.MainTabPages;

public partial class RecordsTab
{
    [Parameter, EditorRequired]
    public SAV3? SaveFile { get; set; }

    private int CurrentRecordIndex { get; set; }

    private uint? CurrentRecordValue { get; set; }

    private Record3? Records { get; set; }

    private IList<ComboItem> RecordComboItems { get; set; } = [];

    private uint HallOfFameHours { get; set; }

    private byte HallOfFameMinutes { get; set; }

    private byte HallOfFameSeconds { get; set; }

    private bool HallOfFameIndexSelected => CurrentRecordIndex switch
    {
        (int)RecID3RuSa.FIRST_HOF_PLAY_TIME or (int)RecID3Emerald.FIRST_HOF_PLAY_TIME or (int)RecID3FRLG.FIRST_HOF_PLAY_TIME => true,
        _ => false,
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

        Records = new Record3(SaveFile);
        RecordComboItems = Record3.GetItems(SaveFile);
        GetRecord();
    }

    private void GetRecord()
    {
        if (SaveFile is null || Records is null)
        {
            return;
        }

        CurrentRecordValue = Records.GetRecord(CurrentRecordIndex);

        if (HallOfFameIndexSelected)
        {
            SetFameTime(CurrentRecordValue ?? 0U);
        }
    }

    private void SetCurrentRecordValue(uint? newValue)
    {
        if (SaveFile is null || Records is null)
        {
            return;
        }

        CurrentRecordValue = newValue;
        Records.SetRecord(CurrentRecordIndex, newValue ?? 0U);

        if (HallOfFameIndexSelected)
        {
            SetFameTime(newValue ?? 0U);
        }
    }

    void ChangeFame()
    {
        if (!HallOfFameIndexSelected || Records is null)
        {
            return;
        }

        Records.SetRecord(1, (uint)(CurrentRecordValue = GetFameTime()));
    }

    public uint GetFameTime()
    {
        if (!HallOfFameIndexSelected || Records is null)
        {
            return 0U;
        }

        var hrs = Math.Min(9999U, HallOfFameHours);
        var min = Math.Min((byte)59, HallOfFameMinutes);
        var sec = Math.Min((byte)59, HallOfFameSeconds);

        return (uint)((hrs << 16) | (min << 8) | sec);
    }

    public void SetFameTime(uint time)
    {
        if (!HallOfFameIndexSelected || Records is null)
        {
            return;
        }

        HallOfFameHours = Math.Min(9999U, time >> 16);
        HallOfFameMinutes = Math.Min((byte)59, (byte)(time >> 8));
        HallOfFameSeconds = Math.Min((byte)59, (byte)time);
    }
}