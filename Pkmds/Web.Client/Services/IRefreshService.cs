﻿namespace Pkmds.Web.Client.Services;

public interface IRefreshService
{
    event Action? OnAppStateChanged;

    event Action? OnBoxStateChanged;

    event Action? OnPartyStateChanged;

    void Refresh();

    void RefreshBoxState();

    void RefreshPartyState();
}
