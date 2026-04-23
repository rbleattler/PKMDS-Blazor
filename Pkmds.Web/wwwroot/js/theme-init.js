(function () {
    let stored = null;
    try {
        stored = localStorage.getItem('pkmds_theme');
    } catch (_) {
    }
    const prefersDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
    const theme = stored || (prefersDark ? 'dark' : 'light');
    document.documentElement.setAttribute('data-theme', theme);
})();

window.setAppTheme = function (theme) {
    document.documentElement.setAttribute('data-theme', theme);
};
