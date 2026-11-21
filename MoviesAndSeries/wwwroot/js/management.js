(() => {
    "use strict";

    const THEME_KEY = "moviesSeriesTheme";

    const body = document.body;

    const endpoints = {
        create: body.dataset.createUrl,
        update: body.dataset.updateUrl,
        delete: body.dataset.deleteUrl,
        getContent: body.dataset.getContentUrl,
        orderOptions: body.dataset.orderOptionsUrl
    };

    const contentList = document.getElementById("contentList");
    const contentRows = [...document.querySelectorAll(".content-row")];
    const emptyState = document.getElementById("emptyState");
    const emptyTitle = document.getElementById("emptyTitle");
    const emptyCopy = document.getElementById("emptyCopy");
    const resultLabel = document.getElementById("resultLabel");
    const searchInput = document.getElementById("searchInput");
    const clearSearch = document.getElementById("clearSearch");
    const universeFilter = document.getElementById("universeFilter");
    const sortSelect = document.getElementById("sortSelect");
    const filterButtons = [...document.querySelectorAll("[data-filter-type]")];
    const themeToggle = document.getElementById("themeToggle");

    const floatingActionMenu = document.getElementById("floatingActionMenu");
    const menuEditButton = document.getElementById("menuEditButton");
    const menuDeleteButton = document.getElementById("menuDeleteButton");

    const drawerBackdrop = document.getElementById("drawerBackdrop");
    const closeDrawerButton = document.getElementById("closeDrawerButton");
    const cancelFormButton = document.getElementById("cancelFormButton");
    const openCreateButton = document.getElementById("openCreateButton");
    const contentForm = document.getElementById("contentForm");
    const contentId = document.getElementById("contentId");
    const contentTypeInput = document.getElementById("contentTypeInput");
    const contentTitle = document.getElementById("contentTitle");
    const seasonField = document.getElementById("seasonField");
    const seasonCount = document.getElementById("seasonCount");
    const typeButtons = [...document.querySelectorAll("[data-content-type]")];
    const relationButtons = [...document.querySelectorAll("[data-relation]")];
    const universeRelationButton = document.querySelector('[data-relation="universe"]');
    const universeFields = document.getElementById("universeFields");
    const universeSelect = document.getElementById("universeSelect");
    const universePosition = document.getElementById("universePosition");
    const positionHelp = document.getElementById("positionHelp");
    const drawerKicker = document.getElementById("drawerKicker");
    const drawerTitle = document.getElementById("drawerTitle");
    const drawerCopy = document.getElementById("drawerCopy");
    const typeHelp = document.getElementById("typeHelp");
    const saveButton = document.getElementById("saveButton");
    const formError = document.getElementById("formError");

    const deleteModal = document.getElementById("deleteModal");
    const deleteDescription = document.getElementById("deleteDescription");
    const cancelDeleteButton = document.getElementById("cancelDeleteButton");
    const confirmDeleteButton = document.getElementById("confirmDeleteButton");
    const toast = document.getElementById("toast");

    const collator = new Intl.Collator("en", { sensitivity: "base" });

    let activeFilterType = "all";
    let preferredGeneralSort = "az";
    let selectedContentType = "movie";
    let selectedRelation = "standalone";
    let editingId = null;
    let originalSeasonCount = null;
    let deletingId = null;
    let deletingTitle = "";
    let activeMenuTrigger = null;
    let activeMenuItemId = null;
    let toastTimer = null;

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

    function isSpecificUniverseSelected() {
        const selectedUniverse = universeFilter.value;
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

        const query = normalizeText(searchInput.value);
        const selectedUniverse = universeFilter.value;
        let visibleCount = 0;

        contentRows.forEach(row => {
            const titleMatches = !query || normalizeText(row.dataset.title).startsWith(query);
            const typeMatches = activeFilterType === "all" || row.dataset.type === activeFilterType;
            const universeMatches =
                selectedUniverse === "all" || row.dataset.universeId === selectedUniverse;

            const shouldShow = titleMatches && typeMatches && universeMatches;
            row.hidden = !shouldShow;

            if (shouldShow) {
                visibleCount += 1;
            }
        });

        emptyState.classList.toggle("visible", visibleCount === 0);
        clearSearch.classList.toggle("visible", searchInput.value.length > 0);
        resultLabel.textContent = formatTitleCount(visibleCount);

        if (contentRows.length === 0) {
            emptyTitle.textContent = "No titles added yet";
            emptyCopy.textContent = "Use the New Title button to add your first movie or series.";
        } else {
            emptyTitle.textContent = "No matching titles found";
            emptyCopy.textContent = "Try adjusting your search query or active filters.";
        }

        closeActionMenu();
    }

    function setTheme(theme) {
        const safeTheme = theme === "light" ? "light" : "dark";
        document.documentElement.dataset.theme = safeTheme;
        document.documentElement.style.colorScheme = safeTheme;

        themeToggle.setAttribute(
            "aria-label",
            safeTheme === "dark" ? "Switch to light mode" : "Switch to dark mode"
        );

        try {
            localStorage.setItem(THEME_KEY, safeTheme);
        } catch {
        }
    }

    function initializeTheme() {
        const theme = document.documentElement.dataset.theme === "light" ? "light" : "dark";
        setTheme(theme);
    }

    function showToast(message, type = "success") {
        window.clearTimeout(toastTimer);

        toast.textContent = message;
        toast.dataset.type = type;
        toast.classList.add("visible");

        toastTimer = window.setTimeout(() => {
            toast.classList.remove("visible");
        }, 2800);
    }

    function closeActionMenu() {
        if (activeMenuTrigger) {
            activeMenuTrigger.classList.remove("active");
            activeMenuTrigger.setAttribute("aria-expanded", "false");
        }

        activeMenuTrigger = null;
        activeMenuItemId = null;

        floatingActionMenu.classList.remove("open");
        floatingActionMenu.hidden = true;
    }

    function openActionMenu(trigger) {
        const itemId = Number(trigger.dataset.menuTrigger);

        if (activeMenuItemId === itemId && !floatingActionMenu.hidden) {
            closeActionMenu();
            return;
        }

        closeActionMenu();

        activeMenuTrigger = trigger;
        activeMenuItemId = itemId;

        trigger.classList.add("active");
        trigger.setAttribute("aria-expanded", "true");

        floatingActionMenu.hidden = false;
        floatingActionMenu.classList.add("open");
        floatingActionMenu.style.visibility = "hidden";

        const triggerRect = trigger.getBoundingClientRect();
        const menuRect = floatingActionMenu.getBoundingClientRect();
        const pagePadding = 12;

        let top = triggerRect.bottom + 8;
        let left = triggerRect.right - menuRect.width;

        if (top + menuRect.height > window.innerHeight - pagePadding) {
            top = triggerRect.top - menuRect.height - 8;
        }

        left = Math.max(pagePadding, Math.min(left, window.innerWidth - menuRect.width - pagePadding));
        top = Math.max(pagePadding, top);

        floatingActionMenu.style.top = `${Math.round(top)}px`;
        floatingActionMenu.style.left = `${Math.round(left)}px`;
        floatingActionMenu.style.visibility = "visible";
    }

    function showFormError(message) {
        formError.textContent = message;
        formError.classList.add("visible");
    }

    function clearFormError() {
        formError.textContent = "";
        formError.classList.remove("visible");
    }

    function updateBodyLock() {
        const shouldLock =
            drawerBackdrop.classList.contains("open") ||
            deleteModal.classList.contains("open");

        body.classList.toggle("drawer-open", shouldLock);
    }

    function openDrawer() {
        drawerBackdrop.classList.add("open");
        drawerBackdrop.setAttribute("aria-hidden", "false");
        updateBodyLock();

        window.setTimeout(() => {
            contentTitle.focus();
        }, 240);
    }

    function closeDrawer() {
        drawerBackdrop.classList.remove("open");
        drawerBackdrop.setAttribute("aria-hidden", "true");

        editingId = null;
        originalSeasonCount = null;

        clearFormError();
        updateBodyLock();
    }

    function setContentType(type, lockButtons = false) {
        selectedContentType = type === "series" ? "series" : "movie";
        contentTypeInput.value = selectedContentType === "movie" ? "1" : "2";

        typeButtons.forEach(button => {
            const isActive = button.dataset.contentType === selectedContentType;
            button.classList.toggle("active", isActive);
            button.disabled = lockButtons;
            button.setAttribute("aria-pressed", String(isActive));
        });

        const isSeries = selectedContentType === "series";
        seasonField.classList.toggle("visible", isSeries);
        seasonCount.disabled = !isSeries;
    }

    async function loadUniverseOrderOptions(preferredOrder = null) {
        if (!universeSelect.value) {
            universePosition.innerHTML = "";
            universePosition.disabled = true;
            positionHelp.textContent = "Create a universe first.";
            return;
        }

        universePosition.disabled = true;
        universePosition.innerHTML = '<option value="">Loading positions...</option>';
        positionHelp.textContent = "Loading watch order...";

        const requestUrl = new URL(endpoints.orderOptions, window.location.origin);
        requestUrl.searchParams.set("universeId", universeSelect.value);

        if (editingId) {
            requestUrl.searchParams.set("contentItemId", String(editingId));
        }

        try {
            const response = await fetch(requestUrl, {
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });

            const data = await readJsonResponse(response);

            if (!response.ok || !data.success) {
                throw new Error(data.message || "Could not load universe order options.");
            }

            universePosition.innerHTML = data.positions
                .map(position =>
                    `<option value="${position.value}">${escapeHtml(position.label)}</option>`
                )
                .join("");

            const requestedValue = Number(preferredOrder);
            const fallbackValue = Number(data.currentOrder) || Number(data.maximumOrder);
            const selectedValue =
                Number.isInteger(requestedValue) &&
                requestedValue >= 1 &&
                requestedValue <= data.maximumOrder
                    ? requestedValue
                    : fallbackValue;

            universePosition.value = String(selectedValue);
            universePosition.disabled = false;

            const otherItemCount = data.universeItems.filter(
                item => Number(item.id) !== Number(editingId)
            ).length;

            positionHelp.textContent =
                otherItemCount === 0
                    ? "This will be the first title in this universe."
                    : otherItemCount === 1
                        ? "There is 1 other title in this universe."
                        : `There are ${otherItemCount} other titles in this universe.`;
        } catch (error) {
            universePosition.innerHTML = "";
            universePosition.disabled = true;
            positionHelp.textContent = "Could not load positions.";
            showFormError(error.message || "Could not load universe order options.");
        }
    }

    async function setRelation(relation, preferredOrder = null) {
        const hasUniverse = universeSelect.options.length > 0;

        selectedRelation =
            relation === "universe" && hasUniverse ? "universe" : "standalone";

        relationButtons.forEach(button => {
            const isActive = button.dataset.relation === selectedRelation;
            button.classList.toggle("active", isActive);
            button.setAttribute("aria-pressed", String(isActive));
        });

        const usesUniverse = selectedRelation === "universe";
        universeFields.classList.toggle("visible", usesUniverse);
        universeSelect.disabled = !usesUniverse;
        universePosition.disabled = !usesUniverse;

        if (usesUniverse) {
            await loadUniverseOrderOptions(preferredOrder);
        } else {
            universePosition.innerHTML = "";
            positionHelp.textContent = "";
        }
    }

    function openCreateDrawer() {
        editingId = null;
        originalSeasonCount = null;

        contentForm.reset();
        contentId.value = "";
        contentTitle.value = "";
        seasonCount.value = "1";

        drawerKicker.textContent = "New Entry";
        drawerTitle.textContent = "Add title";
        drawerCopy.textContent = "Create a new movie or TV series.";
        typeHelp.textContent = "Choose the type of title you want to add.";
        saveButton.textContent = "Save Title";

        clearFormError();
        setContentType("movie", false);
        void setRelation("standalone");
        openDrawer();
    }

    async function openEditDrawer(itemId) {
        closeActionMenu();
        clearFormError();

        const requestUrl = new URL(endpoints.getContent, window.location.origin);
        requestUrl.searchParams.set("id", String(itemId));

        try {
            const response = await fetch(requestUrl, {
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });

            const data = await readJsonResponse(response);

            if (!response.ok || !data.success) {
                throw new Error(data.message || "Could not load content details.");
            }

            const item = data.contentItem;
            editingId = Number(item.id);
            originalSeasonCount = Number(item.seasonCount) || 0;

            contentForm.reset();
            contentId.value = String(item.id);
            contentTitle.value = item.title;

            drawerKicker.textContent = "Edit Entry";
            drawerTitle.textContent = Number(item.type) === 1 ? "Edit movie" : "Edit series";
            drawerCopy.textContent = "Existing watch progress is preserved.";
            typeHelp.textContent = "Content type cannot be changed while editing.";
            saveButton.textContent = "Save Changes";

            const type = Number(item.type) === 1 ? "movie" : "series";
            setContentType(type, true);

            if (type === "series") {
                seasonCount.value = String(item.seasonCount);
            }

            if (item.universeId) {
                universeSelect.value = String(item.universeId);
                await setRelation("universe", item.universeOrder);
            } else {
                await setRelation("standalone");
            }

            openDrawer();
        } catch (error) {
            showToast(error.message || "Could not open title.", "error");
        }
    }

    function openDeleteModal(itemId, itemTitle) {
        closeActionMenu();

        deletingId = Number(itemId);
        deletingTitle = itemTitle;

        deleteDescription.textContent =
            `"${itemTitle}" will be permanently deleted. This action cannot be undone.`;

        deleteModal.classList.add("open");
        deleteModal.setAttribute("aria-hidden", "false");
        updateBodyLock();
    }

    function closeDeleteModal() {
        deletingId = null;
        deletingTitle = "";

        deleteModal.classList.remove("open");
        deleteModal.setAttribute("aria-hidden", "true");
        updateBodyLock();
    }

    async function submitContentForm(event) {
        event.preventDefault();
        clearFormError();

        const title = contentTitle.value.trim();

        if (!title) {
            showFormError("Please enter a title.");
            contentTitle.focus();
            return;
        }

        if (selectedContentType === "series") {
            const newSeasonCount = Number(seasonCount.value);

            if (
                editingId &&
                originalSeasonCount &&
                newSeasonCount < originalSeasonCount &&
                !window.confirm(
                    `Reducing seasons from ${originalSeasonCount} to ${newSeasonCount} will remove watch progress for the deleted seasons. Continue?`
                )
            ) {
                return;
            }
        }

        if (
            selectedRelation === "universe" &&
            (!universeSelect.value || !universePosition.value)
        ) {
            showFormError("Please select a universe and watch order.");
            return;
        }

        const formData = new FormData(contentForm);

        if (selectedContentType === "movie") {
            formData.delete("SeasonCount");
        }

        if (selectedRelation === "standalone") {
            formData.delete("UniverseId");
            formData.delete("UniverseOrder");
        }

        const requestUrl = editingId ? endpoints.update : endpoints.create;
        const originalButtonText = saveButton.textContent;

        saveButton.disabled = true;
        saveButton.textContent = "Saving...";

        try {
            const response = await fetch(requestUrl, {
                method: "POST",
                body: formData,
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });

            const data = await readJsonResponse(response);

            if (!response.ok || !data.success) {
                throw new Error(data.message || "Could not save title.");
            }

            closeDrawer();
            showToast(data.message || "Title saved.");

            window.setTimeout(() => window.location.reload(), 650);
        } catch (error) {
            showFormError(error.message || "Could not save title.");
            saveButton.disabled = false;
            saveButton.textContent = originalButtonText;
        }
    }

    async function deleteSelectedContent() {
        if (!deletingId) {
            return;
        }

        const token = contentForm.querySelector(
            'input[name="__RequestVerificationToken"]'
        )?.value;

        const formData = new FormData();
        formData.append("id", String(deletingId));

        if (token) {
            formData.append("__RequestVerificationToken", token);
        }

        const deletedTitle = deletingTitle;
        confirmDeleteButton.disabled = true;
        confirmDeleteButton.textContent = "Deleting...";

        try {
            const response = await fetch(endpoints.delete, {
                method: "POST",
                body: formData,
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });

            const data = await readJsonResponse(response);

            if (!response.ok || !data.success) {
                throw new Error(data.message || "Could not delete title.");
            }

            closeDeleteModal();
            showToast(`"${deletedTitle}" deleted.`);

            window.setTimeout(() => window.location.reload(), 650);
        } catch (error) {
            showToast(error.message || "Could not delete title.", "error");
            confirmDeleteButton.disabled = false;
            confirmDeleteButton.textContent = "Delete Permanently";
        }
    }

    async function readJsonResponse(response) {
        try {
            return await response.json();
        } catch {
            return {
                success: false,
                message: "Received an invalid response from the server."
            };
        }
    }

    function escapeHtml(value) {
        return String(value)
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#039;");
    }

    searchInput.addEventListener("input", applyFilters);

    clearSearch.addEventListener("click", () => {
        searchInput.value = "";
        searchInput.focus();
        applyFilters();
    });

    universeFilter.addEventListener("change", () => {
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

    filterButtons.forEach(button => {
        button.addEventListener("click", () => {
            activeFilterType = button.dataset.filterType;

            filterButtons.forEach(otherButton => {
                const isActive = otherButton === button;
                otherButton.classList.toggle("active", isActive);
                otherButton.setAttribute("aria-pressed", String(isActive));
            });

            applyFilters();
        });
    });

    typeButtons.forEach(button => {
        button.addEventListener("click", () => {
            if (!editingId) {
                setContentType(button.dataset.contentType, false);
            }
        });
    });

    relationButtons.forEach(button => {
        button.addEventListener("click", () => {
            clearFormError();
            void setRelation(button.dataset.relation);
        });
    });

    universeSelect.addEventListener("change", () => {
        clearFormError();
        void loadUniverseOrderOptions();
    });

    contentList.addEventListener("click", event => {
        const menuButton = event.target.closest("[data-menu-trigger]");
        if (menuButton) {
            event.stopPropagation();
            openActionMenu(menuButton);
        }
    });

    menuEditButton.addEventListener("click", () => {
        if (activeMenuItemId) {
            void openEditDrawer(activeMenuItemId);
        }
    });

    menuDeleteButton.addEventListener("click", () => {
        if (!activeMenuItemId || !activeMenuTrigger) {
            return;
        }

        openDeleteModal(
            activeMenuItemId,
            activeMenuTrigger.dataset.itemTitle || "Selected title"
        );
    });

    document.addEventListener("click", event => {
        if (
            !event.target.closest("#floatingActionMenu") &&
            !event.target.closest("[data-menu-trigger]")
        ) {
            closeActionMenu();
        }
    });

    window.addEventListener("resize", closeActionMenu);
    window.addEventListener("scroll", closeActionMenu, true);

    openCreateButton.addEventListener("click", openCreateDrawer);
    closeDrawerButton.addEventListener("click", closeDrawer);
    cancelFormButton.addEventListener("click", closeDrawer);
    contentForm.addEventListener("submit", submitContentForm);
    cancelDeleteButton.addEventListener("click", closeDeleteModal);
    confirmDeleteButton.addEventListener("click", deleteSelectedContent);

    drawerBackdrop.addEventListener("click", event => {
        if (event.target === drawerBackdrop) {
            closeDrawer();
        }
    });

    deleteModal.addEventListener("click", event => {
        if (event.target === deleteModal) {
            closeDeleteModal();
        }
    });

    document.addEventListener("keydown", event => {
        if (event.key !== "Escape") {
            return;
        }

        if (deleteModal.classList.contains("open")) {
            closeDeleteModal();
        } else if (drawerBackdrop.classList.contains("open")) {
            closeDrawer();
        } else {
            closeActionMenu();
        }
    });

    themeToggle.addEventListener("click", () => {
        const nextTheme =
            document.documentElement.dataset.theme === "dark" ? "light" : "dark";
        setTheme(nextTheme);
    });

    universeRelationButton.disabled = universeSelect.options.length === 0;

    initializeTheme();
    setContentType("movie", false);
    void setRelation("standalone");
    syncSortOptions(isSpecificUniverseSelected());
    applyFilters();
})();
