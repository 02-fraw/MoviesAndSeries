(() => {
    "use strict";

    const THEME_KEY = "moviesSeriesTheme";
    const page = document.body;

    const endpoints = {
        getUniverse: page.dataset.getUniverseUrl,
        getDetails: page.dataset.getDetailsUrl,
        create: page.dataset.createUniverseUrl,
        update: page.dataset.updateUniverseUrl,
        delete: page.dataset.deleteUniverseUrl,
        move: page.dataset.moveContentUrl,
        reorder: page.dataset.reorderContentUrl,
        remove: page.dataset.removeContentUrl,
        deleteContent: page.dataset.deleteContentUrl
    };

    const collator = new Intl.Collator("en", { sensitivity: "base" });

    const listView = document.getElementById("universeListView");
    const detailView = document.getElementById("universeDetailView");
    const universeGrid = document.getElementById("universeGrid");
    const universeEmptyState = document.getElementById("universeEmptyState");
    const universeEmptyTitle = document.getElementById("universeEmptyTitle");
    const universeEmptyCopy = document.getElementById("universeEmptyCopy");
    const universeSearch = document.getElementById("universeSearch");
    const clearUniverseSearch = document.getElementById("clearUniverseSearch");
    const universeResultLabel = document.getElementById("universeResultLabel");
    const universeSortSelect = document.getElementById("universeSortSelect");

    const detailBackButton = document.getElementById("detailBackButton");
    const detailMark = document.getElementById("detailMark");
    const detailTitle = document.getElementById("detailTitle");
    const detailSubtitle = document.getElementById("detailSubtitle");
    const detailEditButton = document.getElementById("detailEditButton");
    const detailDeleteButton = document.getElementById("detailDeleteButton");
    const detailTotalCount = document.getElementById("detailTotalCount");
    const detailMovieCount = document.getElementById("detailMovieCount");
    const detailSeriesCount = document.getElementById("detailSeriesCount");
    const detailSearch = document.getElementById("detailSearch");
    const clearDetailSearch = document.getElementById("clearDetailSearch");
    const detailFilterButtons = [...document.querySelectorAll("[data-detail-filter]")];
    const detailResultLabel = document.getElementById("detailResultLabel");
    const orderingNote = document.getElementById("orderingNote");
    const orderList = document.getElementById("orderList");
    const detailEmptyState = document.getElementById("detailEmptyState");
    const detailEmptyTitle = document.getElementById("detailEmptyTitle");
    const detailEmptyCopy = document.getElementById("detailEmptyCopy");

    const contextMenu = document.getElementById("contextMenu");

    const drawer = document.getElementById("universeDrawer");
    const drawerBackdrop = document.getElementById("drawerBackdrop");
    const drawerTitle = document.getElementById("drawerTitle");
    const drawerEyebrow = document.getElementById("drawerEyebrow");
    const universeForm = document.getElementById("universeForm");
    const universeIdInput = document.getElementById("universeIdInput");
    const universeNameInput = document.getElementById("universeNameInput");
    const colorPickerInput = document.getElementById("colorPickerInput");
    const colorPickerShell = document.getElementById("colorPickerShell");
    const hexColorInput = document.getElementById("hexColorInput");
    const colorPresets = document.getElementById("colorPresets");
    const previewCard = document.getElementById("previewCard");
    const previewMark = document.getElementById("previewMark");
    const previewName = document.getElementById("previewName");
    const drawerSaveButton = document.getElementById("drawerSaveButton");

    const moveModal = document.getElementById("moveModal");
    const moveTitle = document.getElementById("moveTitle");
    const moveCopy = document.getElementById("moveCopy");
    const movePosition = document.getElementById("movePosition");
    const moveSubmitButton = document.getElementById("moveSubmitButton");

    const confirmModal = document.getElementById("confirmModal");
    const confirmTitle = document.getElementById("confirmTitle");
    const confirmCopy = document.getElementById("confirmCopy");
    const confirmButton = document.getElementById("confirmButton");

    const toast = document.getElementById("toast");
    const toastMessage = document.getElementById("toastMessage");

    const themeToggle = document.getElementById("themeToggle");
    const themeLabel = document.getElementById("themeLabel");

    const presetColors = [
        "#22C55E",
        "#06B6D4",
        "#3B82F6",
        "#8B5CF6",
        "#EC4899",
        "#EF4444",
        "#F59E0B",
        "#84CC16"
    ];

    let currentUniverse = null;
    let activeDetailFilter = "all";
    let contextTarget = null;
    let movingContent = null;
    let confirmOperation = null;
    let toastTimer = null;
    let draggedRow = null;
    let dragStartOrder = "";
    let dragFinished = false;

    const normalizeText = value =>
        String(value ?? "")
            .toLowerCase()
            .normalize("NFD")
            .replace(/[\u0300-\u036f]/g, "");

    const escapeHtml = value =>
        String(value ?? "")
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#039;");

    function formatCount(count, singular, plural) {
        return count === 1 ? `1 ${singular}` : `${count} ${plural}`;
    }

    function initials(name) {
        const words = String(name ?? "")
            .trim()
            .split(/\s+/)
            .filter(Boolean);

        return words
            .slice(0, 2)
            .map(word => word[0])
            .join("")
            .toUpperCase() || "UN";
    }

    function normalizeHex(value) {
        let hex = String(value ?? "").trim();

        if (!hex.startsWith("#")) {
            hex = `#${hex}`;
        }

        return /^#[0-9a-fA-F]{6}$/.test(hex) ? hex.toUpperCase() : null;
    }

    function token() {
        return universeForm.querySelector('input[name="__RequestVerificationToken"]').value;
    }

    async function readJson(response) {
        const contentType = response.headers.get("content-type") ?? "";

        if (contentType.includes("application/json")) {
            return response.json();
        }

        return {
            success: false,
            message: "Received an unexpected response from the server."
        };
    }

    async function postForm(url, fields) {
        const formData = new FormData();
        formData.append("__RequestVerificationToken", token());

        Object.entries(fields).forEach(([key, value]) => {
            if (Array.isArray(value)) {
                value.forEach(item => formData.append(key, item));
                return;
            }

            if (value !== undefined && value !== null) {
                formData.append(key, value);
            }
        });

        const response = await fetch(url, {
            method: "POST",
            body: formData,
            headers: { "X-Requested-With": "XMLHttpRequest" }
        });

        return readJson(response);
    }

    function showToast(message, tone = "success") {
        clearTimeout(toastTimer);

        toast.dataset.tone = tone;
        toastMessage.textContent = message;
        toast.hidden = false;

        requestAnimationFrame(() => {
            toast.classList.add("visible");
        });

        toastTimer = setTimeout(() => {
            toast.classList.remove("visible");

            setTimeout(() => {
                toast.hidden = true;
            }, 220);
        }, 3200);
    }

    function setBusy(button, isBusy, busyText) {
        if (!button) {
            return;
        }

        if (isBusy) {
            button.dataset.originalText = button.textContent;
            button.disabled = true;
            button.textContent = busyText;
            return;
        }

        button.disabled = false;
        button.textContent = button.dataset.originalText || button.textContent;
    }

    function syncBodyLock() {
        const hasOpenLayer =
            drawer.classList.contains("open") ||
            moveModal.classList.contains("open") ||
            confirmModal.classList.contains("open");

        document.body.classList.toggle("no-scroll", hasOpenLayer);
    }

    function applyTheme(theme) {
        const safeTheme = theme === "light" ? "light" : "dark";
        const isDark = safeTheme === "dark";

        document.documentElement.dataset.theme = safeTheme;
        document.documentElement.style.colorScheme = safeTheme;

        themeLabel.textContent = isDark ? "Light mode" : "Dark mode";
        themeToggle.setAttribute(
            "aria-label",
            isDark ? "Switch to light mode" : "Switch to dark mode"
        );
    }

    function initializeTheme() {
        const current =
            document.documentElement.dataset.theme === "light" ? "light" : "dark";
        applyTheme(current);
    }

    function universeCards() {
        return [...universeGrid.querySelectorAll("[data-universe-card]")];
    }

    function sortUniverseCards() {
        const sortMode = universeSortSelect?.value ?? "az";

        universeCards()
            .sort((left, right) => {
                if (sortMode === "newest") {
                    const leftCreatedAt = Number(left.dataset.createdAt ?? 0);
                    const rightCreatedAt = Number(right.dataset.createdAt ?? 0);

                    if (leftCreatedAt !== rightCreatedAt) {
                        return rightCreatedAt - leftCreatedAt;
                    }

                    return (
                        Number(right.dataset.universeId ?? 0) -
                        Number(left.dataset.universeId ?? 0)
                    );
                }

                return collator.compare(
                    left.dataset.universeName,
                    right.dataset.universeName
                );
            })
            .forEach(card => universeGrid.appendChild(card));
    }

    function updateOverviewCounts() {
        const cards = universeCards();

        const totals = cards.reduce(
            (result, card) => {
                result.content += Number(card.dataset.contentCount || 0);
                result.movies += Number(card.dataset.movieCount || 0);
                result.series += Number(card.dataset.seriesCount || 0);
                return result;
            },
            { content: 0, movies: 0, series: 0 }
        );

        document.getElementById("overviewUniverseCount").textContent = cards.length;
        document.getElementById("overviewContentCount").textContent = totals.content;
        document.getElementById("overviewMovieCount").textContent = totals.movies;
        document.getElementById("overviewSeriesCount").textContent = totals.series;
    }

    function filterUniverseCards() {
        const query = normalizeText(universeSearch.value.trim());
        let visible = 0;

        universeCards().forEach(card => {
            const matches =
                !query || normalizeText(card.dataset.universeName).startsWith(query);

            card.hidden = !matches;
            if (matches) {
                visible += 1;
            }
        });

        clearUniverseSearch.hidden = !universeSearch.value;
        clearUniverseSearch.classList.toggle("visible", Boolean(universeSearch.value));

        universeResultLabel.textContent = query
            ? `${formatCount(visible, "universe", "universes")} matched`
            : `${formatCount(universeCards().length, "universe", "universes")} shown`;

        const hasAnyUniverse = universeCards().length > 0;
        universeEmptyState.hidden = visible > 0;

        if (!visible) {
            universeEmptyTitle.textContent = hasAnyUniverse
                ? "No matching universes found"
                : "No universes yet";

            universeEmptyCopy.textContent = hasAnyUniverse
                ? "Try adjusting your search query."
                : "Create your first viewing universe to get started.";
        }
    }

    function openContextMenu(button, target) {
        contextTarget = target;

        contextMenu.innerHTML =
            target.kind === "universe"
                ? `
                    <button type="button" data-menu-action="open">Open universe</button>
                    <button type="button" data-menu-action="edit">Edit</button>
                    <span class="context-divider"></span>
                    <button type="button" class="danger" data-menu-action="delete-universe">Delete universe</button>
                `
                : `
                    <button type="button" data-menu-action="move-content">Move to position</button>
                    <button type="button" data-menu-action="remove-content">Remove from universe</button>
                    <span class="context-divider"></span>
                    <button type="button" class="danger" data-menu-action="delete-content">Delete permanently</button>
                `;

        contextMenu.hidden = false;
        contextMenu.classList.add("open");

        const rect = button.getBoundingClientRect();
        const menuWidth = 220;
        const menuHeight = contextMenu.offsetHeight;

        const left = Math.min(
            window.innerWidth - menuWidth - 12,
            Math.max(12, rect.right - menuWidth)
        );

        const top =
            rect.bottom + menuHeight + 8 > window.innerHeight
                ? Math.max(12, rect.top - menuHeight - 8)
                : rect.bottom + 8;

        contextMenu.style.left = `${left}px`;
        contextMenu.style.top = `${top}px`;

        button.setAttribute("aria-expanded", "true");
        contextMenu.dataset.triggerId = button.id || "";
    }

    function closeContextMenu() {
        const triggerId = contextMenu.dataset.triggerId;

        if (triggerId) {
            document.getElementById(triggerId)?.setAttribute("aria-expanded", "false");
        }

        contextMenu.classList.remove("open");
        contextMenu.hidden = true;
        contextMenu.innerHTML = "";
        contextTarget = null;
    }

    function updateUniverseCard(universe) {
        const card = universeGrid.querySelector(`[data-universe-id="${universe.id}"]`);
        if (!card) {
            return;
        }

        card.dataset.universeName = universe.name;
        card.dataset.contentCount = universe.contentCount;
        card.dataset.movieCount = universe.movieCount;
        card.dataset.seriesCount = universe.seriesCount;
        card.style.setProperty("--universe-color", universe.colorHex);

        card.querySelector("[data-card-mark]").textContent = initials(universe.name);
        card.querySelector("[data-card-name]").textContent = universe.name;
        card.querySelector("[data-card-total]").textContent = universe.contentCount;
        card.querySelector("[data-card-movies]").textContent = universe.movieCount;
        card.querySelector("[data-card-series]").textContent = universe.seriesCount;

        updateOverviewCounts();
    }

    function showUniverseList() {
        currentUniverse = null;

        detailView.hidden = true;
        listView.hidden = false;
        detailSearch.value = "";
        activeDetailFilter = "all";

        detailFilterButtons.forEach(button => {
            button.classList.toggle("active", button.dataset.detailFilter === "all");
        });

        closeContextMenu();
        history.replaceState(null, "", window.location.pathname);
        universeSearch.focus({ preventScroll: true });
    }

    async function loadUniverseDetail(id, updateHistory = true) {
        closeContextMenu();

        orderList.innerHTML = `
            <div class="detail-loading">
                <span class="spinner"></span>
                Loading universe...
            </div>
        `;

        detailEmptyState.hidden = true;
        listView.hidden = true;
        detailView.hidden = false;

        try {
            const response = await fetch(
                `${endpoints.getDetails}?id=${encodeURIComponent(id)}`,
                { headers: { "X-Requested-With": "XMLHttpRequest" } }
            );

            const result = await readJson(response);

            if (!response.ok || !result.success) {
                throw new Error(result.message || "Could not load universe details.");
            }

            currentUniverse = result.universe;

            if (!currentUniverse) {
                throw new Error("Universe details were missing in the response.");
            }

            activeDetailFilter = "all";
            detailSearch.value = "";
            clearDetailSearch.hidden = true;

            detailFilterButtons.forEach(button => {
                button.classList.toggle("active", button.dataset.detailFilter === "all");
            });

            renderDetail();
            updateUniverseCard(currentUniverse);

            if (updateHistory) {
                history.replaceState(null, "", `#universe-${currentUniverse.id}`);
            }
        } catch (error) {
            showToast(error.message, "error");
            showUniverseList();
        }
    }

    function renderDetail() {
        if (!currentUniverse) {
            return;
        }

        detailView.style.setProperty("--active-universe-color", currentUniverse.colorHex);
        detailMark.textContent = initials(currentUniverse.name);
        detailTitle.textContent = currentUniverse.name;
        detailSubtitle.textContent =
            `Custom watch order for ${formatCount(currentUniverse.contentCount, "title", "titles")}`;

        detailTotalCount.textContent = currentUniverse.contentCount;
        detailMovieCount.textContent = currentUniverse.movieCount;
        detailSeriesCount.textContent = currentUniverse.seriesCount;

        renderOrderList();
    }

    function renderOrderList() {
        if (!currentUniverse) {
            return;
        }

        const query = normalizeText(detailSearch.value.trim());
        const filtering = Boolean(query) || activeDetailFilter !== "all";

        const items = currentUniverse.contentItems.filter(item => {
            const typeMatches =
                activeDetailFilter === "all" ||
                (activeDetailFilter === "movie" && item.type === 1) ||
                (activeDetailFilter === "series" && item.type === 2);

            const searchMatches = !query || normalizeText(item.title).startsWith(query);
            return typeMatches && searchMatches;
        });

        clearDetailSearch.hidden = !detailSearch.value;
        clearDetailSearch.classList.toggle("visible", Boolean(detailSearch.value));

        detailResultLabel.textContent = filtering
            ? `${formatCount(items.length, "title", "titles")} matched`
            : `${formatCount(currentUniverse.contentCount, "title", "titles")} shown`;

        orderingNote.classList.toggle("filtered", filtering);

        orderingNote.innerHTML = filtering
            ? `
                <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
                    <path d="M7 7h10M7 12h7M7 17h4" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"></path>
                </svg>
                <div>
                    <strong>Reordering is temporarily paused.</strong>
                    <small>Clear search and filters to drag and drop items.</small>
                </div>
            `
            : `
                <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
                    <path d="M8 6h11M8 12h11M8 18h11M4.5 6h.01M4.5 12h.01M4.5 18h.01" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"></path>
                </svg>
                <div>
                    <strong>Custom watch order.</strong>
                    <small>Drag rows to reorder or pick an exact position from the row menu.</small>
                </div>
            `;

        orderList.innerHTML = items
            .map(item => {
                const isSeries = item.type === 2;
                const watchText = isSeries
                    ? `${item.watchedSeasonCount}/${item.seasonCount} seasons watched`
                    : item.isWatched
                        ? "Watched"
                        : "Unwatched";

                const statusClass = item.isWatched ? "is-complete" : "";

                return `
                    <article class="order-row ${statusClass}"
                             data-content-id="${item.id}"
                             draggable="${filtering ? "false" : "true"}">
                        <div class="order-number" aria-label="Position ${item.universeOrder}">
                            ${item.universeOrder}
                        </div>

                        <button class="drag-handle"
                                type="button"
                                title="Drag to reorder"
                                ${filtering ? "disabled" : ""}
                                aria-label="Drag ${escapeHtml(item.title)}">
                            <span></span><span></span><span></span>
                            <span></span><span></span><span></span>
                        </button>

                        <div class="order-main">
                            <h3 class="order-title">${escapeHtml(item.title)}</h3>
                            <div class="badges">
                                <span class="badge ${isSeries ? "type-series" : "type-film"}">
                                    <span class="badge-dot"></span>
                                    ${isSeries ? "Series" : "Movie"}
                                </span>
                                <span class="badge">${escapeHtml(watchText)}</span>
                            </div>
                        </div>

                        <span class="watch-state ${item.isWatched ? "is-watched" : ""}"
                              aria-label="${item.isWatched ? "Watched" : "Unwatched"}">
                            ${item.isWatched ? "✓" : ""}
                        </span>

                        <button class="content-menu-button"
                                id="contentMenuButton${item.id}"
                                type="button"
                                data-content-menu="${item.id}"
                                aria-label="Actions for ${escapeHtml(item.title)}"
                                aria-haspopup="menu"
                                aria-expanded="false">
                            •••
                        </button>
                    </article>
                `;
            })
            .join("");

        detailEmptyState.hidden = items.length > 0;

        if (!items.length) {
            detailEmptyTitle.textContent =
                currentUniverse.contentCount === 0
                    ? "No titles in this universe yet"
                    : "No matching titles found";

            detailEmptyCopy.textContent =
                currentUniverse.contentCount === 0
                    ? "Assign a movie or series to this universe from the Management page."
                    : "Try adjusting your search query or content filter.";
        }

        if (!filtering) {
            bindDragAndDrop();
        }
    }

    function findCurrentContent(id) {
        return currentUniverse?.contentItems.find(item => item.id === Number(id));
    }

    function renderColorPresets(selectedColor) {
        colorPresets.innerHTML = presetColors
            .map(color => `
                <button class="color-preset ${color === selectedColor ? "active" : ""}"
                        type="button"
                        data-preset-color="${color}"
                        style="--preset-color: ${color}"
                        aria-label="Select color ${color}">
                </button>
            `)
            .join("");
    }

    function applyDrawerColor(value) {
        const color = normalizeHex(value);

        if (!color) {
            hexColorInput.classList.add("is-invalid");
            return false;
        }

        hexColorInput.classList.remove("is-invalid");
        hexColorInput.value = color;
        colorPickerInput.value = color;

        colorPickerShell.style.setProperty("--selected-color", color);
        previewCard.style.setProperty("--preview-color", color);
        renderColorPresets(color);

        return true;
    }

    function refreshDrawerPreview() {
        const name = universeNameInput.value.trim() || "New universe";
        previewName.textContent = name;
        previewMark.textContent = initials(name);
    }

    async function openUniverseDrawer(id = null) {
        closeContextMenu();
        universeForm.reset();
        universeIdInput.value = id || "";

        let universe = { name: "", colorHex: "#8B5CF6" };

        if (id) {
            try {
                const response = await fetch(
                    `${endpoints.getUniverse}?id=${encodeURIComponent(id)}`,
                    { headers: { "X-Requested-With": "XMLHttpRequest" } }
                );

                const result = await readJson(response);

                if (!response.ok || !result.success) {
                    throw new Error(result.message || "Could not load universe.");
                }

                universe = result.universe;

                if (!universe) {
                    throw new Error("Universe details were missing in the response.");
                }
            } catch (error) {
                showToast(error.message, "error");
                return;
            }
        }

        drawerEyebrow.textContent = id ? "EDIT UNIVERSE" : "NEW UNIVERSE";
        drawerTitle.textContent = id ? "Update universe" : "Create a viewing universe";
        drawerSaveButton.textContent = id ? "Save Changes" : "Create Universe";

        universeNameInput.value = universe.name;
        applyDrawerColor(universe.colorHex);
        refreshDrawerPreview();

        drawer.hidden = false;
        drawerBackdrop.hidden = false;

        requestAnimationFrame(() => {
            drawer.classList.add("open");
            drawerBackdrop.classList.add("open");
            syncBodyLock();
            universeNameInput.focus();
        });
    }

    function closeUniverseDrawer() {
        drawer.classList.remove("open");
        drawerBackdrop.classList.remove("open");

        setTimeout(() => {
            drawer.hidden = true;
            drawerBackdrop.hidden = true;
            syncBodyLock();
        }, 240);
    }

    function openMoveModal(content) {
        movingContent = content;
        const count = currentUniverse.contentItems.length;

        moveTitle.textContent = `Move "${content.title}"`;
        moveCopy.textContent =
            `Currently at position #${content.universeOrder}. Other items will shift automatically.`;

        movePosition.innerHTML = Array.from({ length: count }, (_, index) => {
            const position = index + 1;
            return `<option value="${position}" ${position === content.universeOrder ? "selected" : ""}>Position #${position}</option>`;
        }).join("");

        moveModal.hidden = false;

        requestAnimationFrame(() => {
            moveModal.classList.add("open");
            syncBodyLock();
            movePosition.focus();
        });
    }

    function closeMoveModal() {
        moveModal.classList.remove("open");

        setTimeout(() => {
            moveModal.hidden = true;
            movingContent = null;
            syncBodyLock();
        }, 200);
    }

    function openConfirmation({ title, copy, buttonText, tone = "danger", operation }) {
        confirmTitle.textContent = title;
        confirmCopy.textContent = copy;
        confirmButton.textContent = buttonText;
        confirmButton.dataset.tone = tone;
        confirmOperation = operation;
        confirmModal.hidden = false;

        requestAnimationFrame(() => {
            confirmModal.classList.add("open");
            syncBodyLock();
            confirmButton.focus();
        });
    }

    function closeConfirmation() {
        confirmModal.classList.remove("open");

        setTimeout(() => {
            confirmModal.hidden = true;
            confirmOperation = null;
            syncBodyLock();
        }, 200);
    }

    function deleteUniverse(id, name) {
        openConfirmation({
            title: `Delete "${name}"?`,
            copy: "The universe will be removed, and its movies and series will remain in your library as standalone titles.",
            buttonText: "Delete Universe",
            operation: async () => {
                const result = await postForm(endpoints.delete, { id });

                if (!result.success) {
                    throw new Error(result.message || "Could not delete universe.");
                }

                window.location.reload();
            }
        });
    }

    async function refreshCurrentUniverse(message) {
        if (!currentUniverse) {
            return;
        }

        await loadUniverseDetail(currentUniverse.id, false);

        if (message) {
            showToast(message);
        }
    }

    function bindDragAndDrop() {
        orderList.querySelectorAll(".order-row").forEach(row => {
            row.addEventListener("dragstart", event => {
                if (activeDetailFilter !== "all" || detailSearch.value.trim()) {
                    event.preventDefault();
                    return;
                }

                draggedRow = row;
                dragStartOrder = currentDomOrder();
                dragFinished = false;

                row.classList.add("dragging");
                event.dataTransfer.effectAllowed = "move";
                event.dataTransfer.setData("text/plain", row.dataset.contentId);
            });

            row.addEventListener("dragover", event => {
                event.preventDefault();

                if (!draggedRow || draggedRow === row) {
                    return;
                }

                const rect = row.getBoundingClientRect();
                const insertAfter = event.clientY > rect.top + rect.height / 2;

                orderList.insertBefore(
                    draggedRow,
                    insertAfter ? row.nextSibling : row
                );

                renumberDomRows();
            });

            row.addEventListener("drop", event => {
                event.preventDefault();
                void finishDrag();
            });

            row.addEventListener("dragend", () => {
                void finishDrag();
            });
        });
    }

    function currentDomIds() {
        return [...orderList.querySelectorAll(".order-row")].map(row =>
            Number(row.dataset.contentId)
        );
    }

    function currentDomOrder() {
        return currentDomIds().join(",");
    }

    function renumberDomRows() {
        orderList.querySelectorAll(".order-row").forEach((row, index) => {
            row.querySelector(".order-number").textContent = index + 1;
        });
    }

    async function finishDrag() {
        if (!draggedRow || dragFinished) {
            return;
        }

        dragFinished = true;
        draggedRow.classList.remove("dragging");

        const newOrder = currentDomOrder();
        draggedRow = null;

        if (newOrder === dragStartOrder) {
            return;
        }

        orderList.classList.add("is-saving-order");

        try {
            const result = await postForm(endpoints.reorder, {
                UniverseId: currentUniverse.id,
                OrderedContentItemIds: currentDomIds()
            });

            if (!result.success) {
                throw new Error(result.message || "Could not save watch order.");
            }

            await refreshCurrentUniverse("Watch order updated.");
        } catch (error) {
            showToast(error.message, "error");
            await loadUniverseDetail(currentUniverse.id, false);
        } finally {
            orderList.classList.remove("is-saving-order");
        }
    }

    async function handleContextAction(action) {
        const target = contextTarget;
        closeContextMenu();

        if (!target) {
            return;
        }

        if (target.kind === "universe") {
            const card = universeGrid.querySelector(`[data-universe-id="${target.id}"]`);
            const name = card?.dataset.universeName || "Universe";

            if (action === "open") {
                await loadUniverseDetail(target.id);
            } else if (action === "edit") {
                await openUniverseDrawer(target.id);
            } else if (action === "delete-universe") {
                deleteUniverse(target.id, name);
            }

            return;
        }

        const content = findCurrentContent(target.id);
        if (!content) {
            return;
        }

        if (action === "move-content") {
            openMoveModal(content);
        } else if (action === "remove-content") {
            openConfirmation({
                title: `Remove "${content.title}" from universe?`,
                copy: "The title will not be deleted; it will stay in your collection as a standalone item.",
                buttonText: "Remove",
                tone: "neutral",
                operation: async () => {
                    const result = await postForm(endpoints.remove, { id: content.id });

                    if (!result.success) {
                        throw new Error(result.message || "Could not remove title from universe.");
                    }

                    closeConfirmation();
                    await refreshCurrentUniverse("Title moved to standalone.");
                }
            });
        } else if (action === "delete-content") {
            openConfirmation({
                title: `Permanently delete "${content.title}"?`,
                copy: "This removes the title and all its seasons from the database. This action cannot be undone.",
                buttonText: "Delete Permanently",
                operation: async () => {
                    const result = await postForm(endpoints.deleteContent, { id: content.id });

                    if (!result.success) {
                        throw new Error(result.message || "Could not delete title.");
                    }

                    closeConfirmation();
                    await refreshCurrentUniverse("Title permanently deleted.");
                }
            });
        }
    }

    universeSearch.addEventListener("input", filterUniverseCards);

    universeSortSelect?.addEventListener("change", () => {
        sortUniverseCards();
        filterUniverseCards();
    });

    clearUniverseSearch.addEventListener("click", () => {
        universeSearch.value = "";
        filterUniverseCards();
        universeSearch.focus();
    });

    universeGrid.addEventListener("click", async event => {
        const menuButton = event.target.closest("[data-universe-menu]");
        if (menuButton) {
            event.stopPropagation();
            openContextMenu(menuButton, {
                kind: "universe",
                id: Number(menuButton.dataset.universeMenu)
            });
            return;
        }

        const card = event.target.closest("[data-universe-card]");
        if (card) {
            await loadUniverseDetail(Number(card.dataset.universeId));
        }
    });

    universeGrid.addEventListener("keydown", async event => {
        const card = event.target.closest("[data-universe-card]");
        if (!card || event.target.closest("button")) {
            return;
        }

        if (event.key === "Enter" || event.key === " ") {
            event.preventDefault();
            await loadUniverseDetail(Number(card.dataset.universeId));
        }
    });

    orderList.addEventListener("click", event => {
        const menuButton = event.target.closest("[data-content-menu]");
        if (!menuButton) {
            return;
        }

        openContextMenu(menuButton, {
            kind: "content",
            id: Number(menuButton.dataset.contentMenu)
        });
    });

    contextMenu.addEventListener("click", async event => {
        const actionButton = event.target.closest("[data-menu-action]");
        if (actionButton) {
            await handleContextAction(actionButton.dataset.menuAction);
        }
    });

    document.addEventListener("pointerdown", event => {
        const clickedMenuButton = event.target.closest(
            "[data-universe-menu], [data-content-menu]"
        );

        if (!contextMenu.hidden && !contextMenu.contains(event.target) && !clickedMenuButton) {
            closeContextMenu();
        }
    });

    window.addEventListener("resize", closeContextMenu);
    window.addEventListener("scroll", closeContextMenu, true);

    detailBackButton.addEventListener("click", showUniverseList);

    detailEditButton.addEventListener("click", () => {
        if (currentUniverse) {
            void openUniverseDrawer(currentUniverse.id);
        }
    });

    detailDeleteButton.addEventListener("click", () => {
        if (currentUniverse) {
            deleteUniverse(currentUniverse.id, currentUniverse.name);
        }
    });

    detailSearch.addEventListener("input", renderOrderList);

    clearDetailSearch.addEventListener("click", () => {
        detailSearch.value = "";
        renderOrderList();
        detailSearch.focus();
    });

    detailFilterButtons.forEach(button => {
        button.addEventListener("click", () => {
            activeDetailFilter = button.dataset.detailFilter;

            detailFilterButtons.forEach(item => {
                item.classList.toggle("active", item === button);
            });

            renderOrderList();
        });
    });

    document.getElementById("createUniverseButton").addEventListener("click", () => {
        void openUniverseDrawer();
    });

    document.querySelectorAll("[data-close-drawer]").forEach(button => {
        button.addEventListener("click", closeUniverseDrawer);
    });

    drawerBackdrop.addEventListener("click", event => {
        if (event.target === drawerBackdrop) {
            closeUniverseDrawer();
        }
    });

    universeNameInput.addEventListener("input", refreshDrawerPreview);

    colorPickerInput.addEventListener("input", event => {
        applyDrawerColor(event.target.value);
    });

    hexColorInput.addEventListener("input", event => {
        if (event.target.value.length >= 6) {
            applyDrawerColor(event.target.value);
        }
    });

    hexColorInput.addEventListener("blur", event => {
        if (!applyDrawerColor(event.target.value)) {
            showToast("Color must follow the #RRGGBB format.", "error");
        }
    });

    colorPresets.addEventListener("click", event => {
        const button = event.target.closest("[data-preset-color]");
        if (button) {
            applyDrawerColor(button.dataset.presetColor);
        }
    });

    universeForm.addEventListener("submit", async event => {
        event.preventDefault();

        const name = universeNameInput.value.trim();
        const color = normalizeHex(hexColorInput.value);

        if (!name) {
            showToast("Please enter a universe name.", "error");
            universeNameInput.focus();
            return;
        }

        if (!color) {
            showToast("Please select a valid hex color.", "error");
            hexColorInput.focus();
            return;
        }

        const id = Number(universeIdInput.value || 0);
        setBusy(drawerSaveButton, true, id ? "Saving..." : "Creating...");

        try {
            const payload = id
                ? { Id: id, Name: name, ColorHex: color }
                : { Name: name, ColorHex: color };

            const result = await postForm(
                id ? endpoints.update : endpoints.create,
                payload
            );

            if (!result.success) {
                throw new Error(result.message || "Could not save universe.");
            }

            if (!id) {
                window.location.reload();
                return;
            }

            closeUniverseDrawer();

            const card = universeGrid.querySelector(`[data-universe-id="${id}"]`);
            if (card) {
                card.dataset.universeName = name;
                card.style.setProperty("--universe-color", color);
                card.querySelector("[data-card-mark]").textContent = initials(name);
                card.querySelector("[data-card-name]").textContent = name;
            }

            sortUniverseCards();

            if (currentUniverse?.id === id) {
                await loadUniverseDetail(id, false);
            }

            filterUniverseCards();
            showToast(result.message || "Universe updated.");
        } catch (error) {
            showToast(error.message, "error");
        } finally {
            setBusy(drawerSaveButton, false);
        }
    });

    document.querySelectorAll("[data-close-move]").forEach(button => {
        button.addEventListener("click", closeMoveModal);
    });

    moveSubmitButton.addEventListener("click", async () => {
        if (!movingContent || !currentUniverse) {
            return;
        }

        setBusy(moveSubmitButton, true, "Moving...");

        try {
            const result = await postForm(endpoints.move, {
                ContentItemId: movingContent.id,
                TargetOrder: Number(movePosition.value)
            });

            if (!result.success) {
                throw new Error(result.message || "Could not move title.");
            }

            closeMoveModal();
            await refreshCurrentUniverse(result.message || "Title moved.");
        } catch (error) {
            showToast(error.message, "error");
        } finally {
            setBusy(moveSubmitButton, false);
        }
    });

    document.querySelectorAll("[data-close-confirm]").forEach(button => {
        button.addEventListener("click", closeConfirmation);
    });

    confirmButton.addEventListener("click", async () => {
        if (!confirmOperation) {
            return;
        }

        const operation = confirmOperation;
        setBusy(confirmButton, true, "Processing...");

        try {
            await operation();
        } catch (error) {
            showToast(error.message, "error");
        } finally {
            setBusy(confirmButton, false);
        }
    });

    themeToggle.addEventListener("click", () => {
        const next = document.documentElement.dataset.theme === "dark" ? "light" : "dark";

        try {
            localStorage.setItem(THEME_KEY, next);
        } catch {
        }

        applyTheme(next);
    });

    document.addEventListener("keydown", event => {
        if (event.key !== "Escape") {
            return;
        }

        if (!contextMenu.hidden) {
            closeContextMenu();
        } else if (confirmModal.classList.contains("open")) {
            closeConfirmation();
        } else if (moveModal.classList.contains("open")) {
            closeMoveModal();
        } else if (drawer.classList.contains("open")) {
            closeUniverseDrawer();
        }
    });

    initializeTheme();
    sortUniverseCards();
    updateOverviewCounts();
    filterUniverseCards();

    const hashMatch = window.location.hash.match(/^#universe-(\d+)$/);
    if (hashMatch) {
        void loadUniverseDetail(Number(hashMatch[1]), false);
    }
})();
