namespace Pkmds.Rcl.Components.Dialogs;

public partial class AppSettingsDialog
{
    internal static readonly IReadOnlyList<(LanguageID Id, string Name)> SupportedLanguages =
    [
        (LanguageID.Japanese, "Japanese (日本語)"),
        (LanguageID.English, "English"),
        (LanguageID.French, "French (Français)"),
        (LanguageID.Italian, "Italian (Italiano)"),
        (LanguageID.German, "German (Deutsch)"),
        (LanguageID.Spanish, "Spanish (Español)"),
        (LanguageID.Korean, "Korean (한국어)"),
        (LanguageID.ChineseS, "Chinese Simplified (简体中文)"),
        (LanguageID.ChineseT, "Chinese Traditional (繁體中文)"),
        (LanguageID.SpanishL, "Spanish LATAM (Español LATAM)")
    ];

    private LanguageID defaultLanguageId = LanguageID.English;
    private string defaultOtName = string.Empty;
    private uint defaultSecretId;
    private uint defaultTrainerId;
    private bool isAutoBackupEnabled = true;
    private bool isHaXEnabled;
    private bool isVerboseLogging;
    private int maxBackupCount = 10;
    private bool showFishyIndicator = true;
    private bool showIllegalIndicator = true;
    private bool showLegalIndicator = true;
    private SpriteStyle spriteStyle;

    // Working copy — only committed on Save
    private ThemeMode themeMode;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public AppSettings InitialSettings { get; set; } = new();

    protected override void OnInitialized()
    {
        themeMode = InitialSettings.ThemeMode switch
        {
            "light" => ThemeMode.Light,
            "dark" => ThemeMode.Dark,
            _ => ThemeMode.System
        };
        isHaXEnabled = InitialSettings.IsHaXEnabled;
        isVerboseLogging = InitialSettings.IsVerboseLoggingEnabled;
        spriteStyle = InitialSettings.SpriteStyle;
        defaultOtName = InitialSettings.DefaultOtName;
        defaultTrainerId = InitialSettings.DefaultTrainerId;
        defaultSecretId = InitialSettings.DefaultSecretId;
        defaultLanguageId = InitialSettings.DefaultLanguageId;
        isAutoBackupEnabled = InitialSettings.IsAutoBackupEnabled;
        maxBackupCount = InitialSettings.MaxBackupCount;
        showLegalIndicator = InitialSettings.ShowLegalIndicator;
        showFishyIndicator = InitialSettings.ShowFishyIndicator;
        showIllegalIndicator = InitialSettings.ShowIllegalIndicator;
    }

    private async Task OnHaXEnabledChanged(bool newValue)
    {
        if (newValue && !InitialSettings.IsHaXEnabled)
        {
            var ack = await JSRuntime.InvokeAsync<string?>("localStorage.getItem", "pkmds_hax_warning_ack");
            if (ack != "true")
            {
                await DialogService.ShowMessageBoxAsync(
                    "PKHaX Mode",
                    "Illegal mode activated. Editing restrictions are now lifted. " +
                    "Pokémon created or modified in this mode may be illegal and untradable. " +
                    "Please behave.",
                    "I understand");
                await JSRuntime.InvokeVoidAsync("localStorage.setItem", "pkmds_hax_warning_ack", "true");
            }
        }

        isHaXEnabled = newValue;
    }

    private async Task OnResetAll()
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            "Reset All Settings",
            "This will reset all settings to their default values. Continue?",
            "Reset",
            cancelText: "Cancel");

        if (confirmed != true)
        {
            return;
        }

        var defaults = new AppSettings();
        themeMode = ThemeMode.System;
        isHaXEnabled = defaults.IsHaXEnabled;
        isVerboseLogging = defaults.IsVerboseLoggingEnabled;
        spriteStyle = defaults.SpriteStyle;
        defaultOtName = defaults.DefaultOtName;
        defaultTrainerId = defaults.DefaultTrainerId;
        defaultSecretId = defaults.DefaultSecretId;
        defaultLanguageId = defaults.DefaultLanguageId;
        isAutoBackupEnabled = defaults.IsAutoBackupEnabled;
        maxBackupCount = defaults.MaxBackupCount;
        showLegalIndicator = defaults.ShowLegalIndicator;
        showFishyIndicator = defaults.ShowFishyIndicator;
        showIllegalIndicator = defaults.ShowIllegalIndicator;
        StateHasChanged();
    }

    private async Task OnClearAppCache()
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            "Clear App Cache",
            "This unregisters the service worker, deletes all browser caches for this site, " +
            "and reloads the page. Your save files and backups are preserved. Continue?",
            "Clear & Reload",
            cancelText: "Cancel");

        if (confirmed != true)
        {
            return;
        }

        await JSRuntime.InvokeVoidAsync("clearAppCacheAndReload");
    }

    private void Cancel() => MudDialog.Close(DialogResult.Cancel());

    private void Save()
    {
        var themeStr = themeMode switch
        {
            ThemeMode.Light => "light",
            ThemeMode.Dark => "dark",
            _ => "system"
        };

        var updated = new AppSettings
        {
            ThemeMode = themeStr,
            IsHaXEnabled = isHaXEnabled,
            IsVerboseLoggingEnabled = isVerboseLogging,
            SpriteStyle = spriteStyle,
            DefaultOtName = defaultOtName,
            DefaultTrainerId = defaultTrainerId,
            DefaultSecretId = defaultSecretId,
            DefaultLanguageId = defaultLanguageId,
            IsAutoBackupEnabled = isAutoBackupEnabled,
            MaxBackupCount = maxBackupCount,
            ShowLegalIndicator = showLegalIndicator,
            ShowFishyIndicator = showFishyIndicator,
            ShowIllegalIndicator = showIllegalIndicator
        };

        MudDialog.Close(DialogResult.Ok(updated));
    }

    private enum ThemeMode
    {
        Light,
        System,
        Dark
    }
}
