"use strict";

(() => {
    const THEME_KEY = "moviesSeriesTheme";

    const root = document.documentElement;
    const body = document.body;

    const themeToggle = document.getElementById("themeToggle");
    const themeLabel = document.getElementById("themeLabel");
    const searchInput = document.getElementById("searchInput");
    const clearSearch = document.getElementById("clearSearch");
    const universeFilter = document.getElementById("universeFilter");
    const sortSelect = document.getElementById("sortSelect");
    const filterButtons = Array.from(document.querySelectorAll("[data-filter-type]"));
    const contentList = document.getElementById("contentList");
    const contentRows = Array.from(document.querySelectorAll("[data-content-row]"));
    const resultIndicator = document.getElementById("resultIndicator");
    const emptyState = document.getElementById("emptyState");
    const emptyTitle = document.getElementById("emptyTitle");
    const emptyCopy = document.getElementById("emptyCopy");
    const toast = document.getElementById("toast");
    const toastMessage = document.getElementById("toastMessage");

    const antiForgeryToken = document.querySelector(
        "#antiForgeryForm input[name='__RequestVerificationToken']"
    )?.value;

    const collator = new Intl.Collator("en", { sensitivity: "base" });

    let selectedType = "all";
    let preferredGeneralSort = "az";
    let toastTimerId;

    function normalizeText(value) {
        return String(value ?? "")
            .toLowerCase()
            .normalize("NFD")
            .replace(/[\u0300-\u036f]/g, "")
            .trim();
    }

    function formatTitleCount(count) {
        return count === 1 ? "1 title shown" : `${count} titles shown`;
    }

    function setTheme(theme) {
        const normalizedTheme = theme === "light" ? "light" : "dark";
        const isDark = normalizedTheme === "dark";

        root.dataset.theme = normalizedTheme;
        root.style.colorScheme = normalizedTheme;

        themeToggle?.setAttribute(
            "aria-label",
            isDark ? "Switch to light mode" : "Switch to dark mode"
        );

        if (themeLabel) {
            themeLabel.textContent = isDark ? "Light mode" : "Dark mode";
        }

        try {
            localStorage.setItem(THEME_KEY, normalizedTheme);
        } catch {
        }
    }

    function initializeTheme() {
        const current = root.dataset.theme === "light" ? "light" : "dark";
        setTheme(current);
    }

    function updateEmptyState(visibleCount) {
        if (!emptyState || !contentList) {
            return;
        }

        const hasAnyContent = contentRows.length > 0;
        const shouldShow = visibleCount === 0;

        emptyState.hidden = !shouldShow;
        contentList.hidden = shouldShow;

        if (!shouldShow) {
            return;
        }

        if (!hasAnyContent) {
            emptyTitle.textContent = "No titles yet";
            emptyCopy.textContent = "Add your first movie or series from the Management page.";
            return;
        }

        emptyTitle.textContent = "No matching titles found";
        emptyCopy.textContent = "Try adjusting your search query or active filters.";
    }

    function isSpecificUniverseSelected() {
        const selectedUniverse = universeFilter?.value ?? "all";
        return selectedUniverse !== "all" && selectedUniverse !== "standalone";
    }

    function syncSortOptions(autoSelectUniverseOrder = false) {
        if (!sortSelect) {
            return;
        }

        const isSpecificUniverse = isSpecificUniverseSelected();
        const previousValue = sortSelect.value;

        const options = isSpecificUniverse
            ? [
                { value: "universe-order", label: "Universe Order" },
                { value: "az", label: "A to Z" },
                { value: "newest", label: "Newest First" }
            ]
            : [
                { value: "az", label: "A to Z" },
                { value: "newest", label: "Newest First" }
            ];

        sortSelect.innerHTML = "";

        options.forEach(({ value, label }) => {
            const option = document.createElement("option");
            option.value = value;
            option.textContent = label;
            sortSelect.appendChild(option);
        });

        if (isSpecificUniverse) {
            if (autoSelectUniverseOrder) {
                sortSelect.value = "universe-order";
            } else if (options.some(option => option.value === previousValue)) {
                sortSelect.value = previousValue;
            } else {
                sortSelect.value = "universe-order";
            }
        } else {
            sortSelect.value = preferredGeneralSort === "newest" ? "newest" : "az";
        }
    }

    function sortContentRows() {
        if (!contentList) {
            return;
        }

        const sortMode = sortSelect?.value ?? "az";

        contentRows.sort((left, right) => {
            const leftId = Number(left.dataset.id ?? 0);
            const rightId = Number(right.dataset.id ?? 0);

            if (sortMode === "universe-order") {
                const leftOrderRaw = left.dataset.universeOrder;
                const rightOrderRaw = right.dataset.universeOrder;

                const leftOrder =
                    leftOrderRaw !== undefined && leftOrderRaw !== ""
                        ? Number(leftOrderRaw)
                        : Number.MAX_SAFE_INTEGER;

                const rightOrder =
                    rightOrderRaw !== undefined && rightOrderRaw !== ""
                        ? Number(rightOrderRaw)
                        : Number.MAX_SAFE_INTEGER;

                if (leftOrder !== rightOrder) {
                    return leftOrder - rightOrder;
                }

                return leftId - rightId;
            }

            if (sortMode === "newest") {
                const leftCreatedAt = Number(left.dataset.createdAt ?? 0);
                const rightCreatedAt = Number(right.dataset.createdAt ?? 0);

                if (leftCreatedAt !== rightCreatedAt) {
                    return rightCreatedAt - leftCreatedAt;
                }

                return rightId - leftId;
            }

            const titleComparison = collator.compare(
                left.dataset.title ?? "",
                right.dataset.title ?? ""
            );

            return titleComparison !== 0 ? titleComparison : leftId - rightId;
        });

        const fragment = document.createDocumentFragment();
        contentRows.forEach(row => fragment.appendChild(row));
        contentList.appendChild(fragment);
    }

    function applyFilters() {
        sortContentRows();

        const query = normalizeText(searchInput?.value);
        const selectedUniverse = universeFilter?.value ?? "all";
        let visibleCount = 0;

        contentRows.forEach(row => {
            const title = normalizeText(row.dataset.title);
            const type = row.dataset.type;
            const universe = row.dataset.universe;

            const matchesSearch = query.length === 0 || title.startsWith(query);
            const matchesType = selectedType === "all" || type === selectedType;
            const matchesUniverse = selectedUniverse === "all" || universe === selectedUniverse;

            const isVisible = matchesSearch && matchesType && matchesUniverse;
            row.hidden = !isVisible;

            if (isVisible) {
                visibleCount += 1;
            }
        });

        if (clearSearch) {
            clearSearch.hidden = (searchInput?.value.length ?? 0) === 0;
        }

        if (resultIndicator) {
            resultIndicator.textContent = formatTitleCount(visibleCount);
        }

        updateEmptyState(visibleCount);
    }

    function selectTypeFilter(button) {
        selectedType = button.dataset.filterType ?? "all";

        filterButtons.forEach(item => {
            const isActive = item === button;
            item.classList.toggle("active", isActive);
            item.setAttribute("aria-pressed", String(isActive));
        });

        applyFilters();
    }

    function showToast(message, isError = false) {
        if (!toast || !toastMessage) {
            return;
        }

        window.clearTimeout(toastTimerId);

        toastMessage.textContent = message;
        toast.dataset.tone = isError ? "error" : "success";
        toast.hidden = false;

        requestAnimationFrame(() => {
            toast.classList.add("visible");
        });

        toastTimerId = window.setTimeout(() => {
            toast.classList.remove("visible");

            window.setTimeout(() => {
                toast.hidden = true;
            }, 220);
        }, 2800);
    }

    async function postForm(url, values) {
        if (!url) {
            throw new Error("Request endpoint not found.");
        }

        if (!antiForgeryToken) {
            throw new Error("Security token missing. Refresh the page and try again.");
        }

        const formData = new FormData();
        formData.append("__RequestVerificationToken", antiForgeryToken);

        Object.entries(values).forEach(([key, value]) => {
            formData.append(key, String(value));
        });

        const response = await fetch(url, {
            method: "POST",
            body: formData,
            headers: {
                "X-Requested-With": "XMLHttpRequest"
            }
        });

        const contentType = response.headers.get("content-type") ?? "";
        const result = contentType.includes("application/json")
            ? await response.json()
            : null;

        if (!response.ok || result?.success === false) {
            throw new Error(result?.message ?? "Request failed.");
        }

        return result ?? { success: true };
    }

    function updateWatchedButton(button, isWatched) {
        button.dataset.watched = String(isWatched);
        button.setAttribute("aria-pressed", String(isWatched));
        button.classList.toggle("is-watched", isWatched);

        const label = button.querySelector("[data-watch-label]");
        if (label) {
            label.textContent = isWatched ? "Watched" : "Unwatched";
        }
    }

    async function toggleMovie(button) {
        const contentItemId = Number(button.dataset.movieToggle);
        const nextState = button.dataset.watched !== "true";

        if (!Number.isInteger(contentItemId) || contentItemId <= 0) {
            showToast("Unable to read movie identifier.", true);
            return;
        }

        button.disabled = true;

        try {
            const result = await postForm(body.dataset.setMovieWatchedUrl, {
                contentItemId,
                isWatched: nextState
            });

            updateWatchedButton(button, Boolean(result.isWatched));
            showToast(result.message ?? "Movie status updated.");
        } catch (error) {
            showToast(error.message ?? "Could not update movie status.", true);
        } finally {
            button.disabled = false;
        }
    }

    async function toggleSeason(button) {
        const seasonId = Number(button.dataset.seasonToggle);
        const nextState = button.dataset.watched !== "true";

        if (!Number.isInteger(seasonId) || seasonId <= 0) {
            showToast("Unable to read season identifier.", true);
            return;
        }

        button.disabled = true;

        try {
            const result = await postForm(body.dataset.setSeasonWatchedUrl, {
                seasonId,
                isWatched: nextState
            });

            const isWatched = Boolean(result.isWatched);
            button.dataset.watched = String(isWatched);
            button.setAttribute("aria-pressed", String(isWatched));
            button.classList.toggle("is-watched", isWatched);

            showToast(result.message ?? "Season status updated.");
        } catch (error) {
            showToast(error.message ?? "Could not update season status.", true);
        } finally {
            button.disabled = false;
        }
    }

    themeToggle?.addEventListener("click", () => {
        setTheme(root.dataset.theme === "dark" ? "light" : "dark");
    });

    searchInput?.addEventListener("input", applyFilters);

    universeFilter?.addEventListener("change", () => {
        syncSortOptions(true);
        applyFilters();
    });

    sortSelect?.addEventListener("change", () => {
        if (
            !isSpecificUniverseSelected() &&
            (sortSelect.value === "az" || sortSelect.value === "newest")
        ) {
            preferredGeneralSort = sortSelect.value;
        }

        applyFilters();
    });

    clearSearch?.addEventListener("click", () => {
        searchInput.value = "";
        searchInput.focus();
        applyFilters();
    });

    filterButtons.forEach(button => {
        button.addEventListener("click", () => selectTypeFilter(button));
    });

    contentList?.addEventListener("click", event => {
        const movieButton = event.target.closest("[data-movie-toggle]");
        if (movieButton) {
            void toggleMovie(movieButton);
            return;
        }

        const seasonButton = event.target.closest("[data-season-toggle]");
        if (seasonButton) {
            void toggleSeason(seasonButton);
        }
    });

    initializeTheme();
    syncSortOptions(isSpecificUniverseSelected());
    applyFilters();
})();
