(() => {
    "use strict";

    try {
        const storedTheme = localStorage.getItem("moviesSeriesTheme");
        const theme =
            storedTheme === "dark" || storedTheme === "light"
                ? storedTheme
                : window.matchMedia?.("(prefers-color-scheme: light)").matches
                    ? "light"
                    : "dark";

        document.documentElement.dataset.theme = theme;
        document.documentElement.style.colorScheme = theme;
    } catch {
        document.documentElement.dataset.theme = "dark";
        document.documentElement.style.colorScheme = "dark";
    }
})();
