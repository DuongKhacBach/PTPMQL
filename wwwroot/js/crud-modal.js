/* ==========================================================================
   CRUD MODAL + AJAX TABLE ENGINE (Bootstrap 5 + Fetch API + jQuery Unobtrusive)
   ========================================================================== */
(function () {
    'use strict';

    const AJAX_HEADERS = { 'X-Requested-With': 'XMLHttpRequest' };

    function escapeHtml(str) {
        if (str === null || str === undefined) return '';
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    // ======================================================================
    // 1. TOAST NOTIFICATION
    // ======================================================================
    function showToast(message, type = 'success') {
        const container = document.getElementById('appToastContainer');
        if (!container) return;

        const isError = type === 'danger' || type === 'error';
        const isWarning = type === 'warning';
        const bgClass = isError ? 'text-bg-danger' : isWarning ? 'text-bg-warning' : 'text-bg-success';
        const iconClass = isError
            ? 'bi-exclamation-triangle-fill'
            : isWarning
                ? 'bi-exclamation-circle-fill'
                : 'bi-check-circle-fill';

        const toastEl = document.createElement('div');
        toastEl.className = `toast align-items-center ${bgClass} border-0 shadow`;
        toastEl.setAttribute('role', 'alert');
        toastEl.setAttribute('aria-live', 'assertive');
        toastEl.setAttribute('aria-atomic', 'true');
        toastEl.innerHTML = `
            <div class="d-flex">
                <div class="toast-body d-flex align-items-center gap-2">
                    <i class="bi ${iconClass} fs-5"></i>
                    <span>${escapeHtml(message)}</span>
                </div>
                <button type="button" class="btn-close ${isWarning ? '' : 'btn-close-white'} me-2 m-auto" data-bs-dismiss="toast" aria-label="Đóng"></button>
            </div>
        `;
        container.appendChild(toastEl);

        const bsToast = new bootstrap.Toast(toastEl, { delay: 4500 });
        toastEl.addEventListener('hidden.bs.toast', () => toastEl.remove());
        bsToast.show();
    }

    window.AppToast = { show: showToast };

    // ======================================================================
    // 2. AJAX TABLE REFRESH (Giữ nguyên trang, tìm kiếm, bộ lọc & sắp xếp)
    // ======================================================================
    async function loadTableUrl(url, pushHistory = true) {
        const tableContainer = document.getElementById('dataTableContainer');
        if (!tableContainer) return;

        const targetUrl = url || window.location.href;
        tableContainer.style.opacity = '0.55';
        tableContainer.style.pointerEvents = 'none';

        try {
            const response = await fetch(targetUrl, {
                method: 'GET',
                headers: AJAX_HEADERS,
                credentials: 'same-origin'
            });

            if (response.redirected && response.url.includes('/Identity/Account/Login')) {
                window.location.href = response.url;
                return;
            }

            if (response.status === 403) {
                showToast('Bạn không có quyền xem dữ liệu này.', 'danger');
                return;
            }

            if (!response.ok) {
                showToast('Không thể tải lại bảng dữ liệu. Vui lòng thử lại.', 'danger');
                return;
            }

            const html = await response.text();
            tableContainer.innerHTML = html;

            if (pushHistory) {
                window.history.replaceState(null, '', targetUrl);
            }

            syncHeaderMetadata(tableContainer, targetUrl);
        } catch (err) {
            console.error('loadTableUrl error:', err);
            showToast('Lỗi kết nối khi tải bảng dữ liệu.', 'danger');
        } finally {
            tableContainer.style.opacity = '';
            tableContainer.style.pointerEvents = '';
        }
    }

    function syncHeaderMetadata(tableContainer, currentUrl) {
        // Cập nhật tổng số bản ghi trên tiêu đề Card nếu có
        const metaEl = tableContainer.querySelector('[data-total-items]');
        if (metaEl) {
            const total = metaEl.getAttribute('data-total-items');
            const filterText = metaEl.getAttribute('data-current-filter') || '';
            const countTarget = document.querySelector('[data-total-count-target]');
            if (countTarget && total !== null) {
                countTarget.textContent = total;
            }
            const filterBadge = document.querySelector('[data-filter-keyword-target]');
            if (filterBadge) {
                if (filterText) {
                    filterBadge.innerHTML = `với từ khóa <mark class="px-1 rounded">"${escapeHtml(filterText)}"</mark>`;
                    filterBadge.classList.remove('d-none');
                } else {
                    filterBadge.innerHTML = '';
                    filterBadge.classList.add('d-none');
                }
            }
        }

        // Đồng bộ nút Export Excel theo bộ lọc hiện tại
        const exportBtn = document.getElementById('btnExportExcel');
        if (exportBtn) {
            const baseExportUrl = exportBtn.getAttribute('data-export-base-url');
            if (baseExportUrl) {
                const parsed = new URL(currentUrl, window.location.origin);
                const params = new URLSearchParams();
                ['searchString', 'facultyId', 'sortOrder'].forEach((k) => {
                    const v = parsed.searchParams.get(k);
                    if (v) params.set(k, v);
                });
                const qs = params.toString();
                exportBtn.setAttribute('href', qs ? `${baseExportUrl}?${qs}` : baseExportUrl);
            }
        }

        // Đồng bộ hidden sortOrder trong filter form nếu có
        const filterForm = document.querySelector('form[data-ajax-filter="true"]');
        if (filterForm) {
            const parsed = new URL(currentUrl, window.location.origin);
            const sortInput = filterForm.querySelector('input[name="sortOrder"]');
            if (sortInput) {
                sortInput.value = parsed.searchParams.get('sortOrder') || '';
            }
            const clearBtn = filterForm.querySelector('[data-ajax-clear-filter="true"]');
            if (clearBtn) {
                const hasSearch = !!(parsed.searchParams.get('searchString') || parsed.searchParams.get('facultyId') || parsed.searchParams.get('roleFilter'));
                clearBtn.classList.toggle('d-none', !hasSearch);
            }
        }
    }

    function buildUrlFromFilterForm(form) {
        const action = form.getAttribute('action') || window.location.pathname;
        const url = new URL(action, window.location.origin);
        const formData = new FormData(form);

        for (const [key, value] of formData.entries()) {
            const strVal = String(value).trim();
            if (strVal !== '') {
                url.searchParams.set(key, strVal);
            }
        }
        // Khi tìm kiếm hoặc thay đổi bộ lọc / pageSize thì quay về trang 1
        url.searchParams.set('pageNumber', '1');
        return url.pathname + url.search;
    }

    // ======================================================================
    // 3. OPEN & SUBMIT BOOTSTRAP 5 MODAL VIA AJAX
    // ======================================================================
    function getModalInstance() {
        const modalEl = document.getElementById('crudModal');
        if (!modalEl) return null;
        return bootstrap.Modal.getOrCreateInstance(modalEl);
    }

    async function openCrudModal(url, sizeClass = '') {
        const modalEl = document.getElementById('crudModal');
        const dialogEl = document.getElementById('crudModalDialog');
        const contentEl = document.getElementById('crudModalContent');
        if (!modalEl || !dialogEl || !contentEl) return;

        dialogEl.className = 'modal-dialog modal-dialog-centered' + (sizeClass ? ` ${sizeClass}` : '');
        contentEl.innerHTML = `
            <div class="modal-body text-center py-5">
                <div class="spinner-border text-primary mb-3" role="status">
                    <span class="visually-hidden">Đang tải...</span>
                </div>
                <div class="text-muted small">Đang tải nội dung...</div>
            </div>
        `;

        const modal = getModalInstance();
        modal.show();

        try {
            const response = await fetch(url, {
                method: 'GET',
                headers: AJAX_HEADERS,
                credentials: 'same-origin'
            });

            if (response.redirected && response.url.includes('/Identity/Account/Login')) {
                modal.hide();
                window.location.href = response.url;
                return;
            }

            if (response.status === 403) {
                contentEl.innerHTML = buildModalErrorHtml('Từ chối truy cập', 'Tài khoản của bạn không có quyền thực hiện chức năng này.');
                return;
            }

            if (response.status === 404) {
                contentEl.innerHTML = buildModalErrorHtml('Không tìm thấy dữ liệu', 'Bản ghi bạn yêu cầu không tồn tại hoặc đã bị xóa.');
                return;
            }

            if (!response.ok) {
                contentEl.innerHTML = buildModalErrorHtml('Đã xảy ra lỗi', 'Không thể tải biểu mẫu. Vui lòng thử lại sau.');
                return;
            }

            const html = await response.text();
            contentEl.innerHTML = html;
            initModalContent(contentEl);
        } catch (err) {
            console.error('openCrudModal error:', err);
            contentEl.innerHTML = buildModalErrorHtml('Lỗi kết nối', 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra lại.');
        }
    }

    function buildModalErrorHtml(title, message) {
        return `
            <div class="modal-header bg-danger text-white">
                <h5 class="modal-title fw-bold"><i class="bi bi-exclamation-triangle-fill me-2"></i>${escapeHtml(title)}</h5>
                <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal" aria-label="Đóng"></button>
            </div>
            <div class="modal-body p-4 text-center">
                <p class="mb-0 text-muted">${escapeHtml(message)}</p>
            </div>
            <div class="modal-footer bg-light">
                <button type="button" class="btn btn-secondary btn-sm px-3" data-bs-dismiss="modal">Đóng</button>
            </div>
        `;
    }

    async function handleModalFormSubmit(form) {
        if (window.jQuery && $(form).data('validator')) {
            if (!$(form).valid()) {
                return;
            }
        }

        const submitBtn = form.querySelector('button[type="submit"]');
        const originalBtnHtml = submitBtn ? submitBtn.innerHTML : '';
        if (submitBtn) {
            submitBtn.disabled = true;
            submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span>Đang xử lý...';
        }

        const contentEl = document.getElementById('crudModalContent');

        try {
            const response = await fetch(form.getAttribute('action') || window.location.href, {
                method: (form.getAttribute('method') || 'POST').toUpperCase(),
                body: new FormData(form),
                headers: AJAX_HEADERS,
                credentials: 'same-origin'
            });

            if (response.redirected && response.url.includes('/Identity/Account/Login')) {
                window.location.href = response.url;
                return;
            }

            if (response.status === 403) {
                showToast('Bạn không có quyền thực hiện thao tác này.', 'danger');
                if (submitBtn) {
                    submitBtn.disabled = false;
                    submitBtn.innerHTML = originalBtnHtml;
                }
                return;
            }

            if (response.status === 400) {
                showToast('Phiên làm việc hoặc mã xác thực form đã hết hạn. Vui lòng tải lại trang.', 'warning');
                if (submitBtn) {
                    submitBtn.disabled = false;
                    submitBtn.innerHTML = originalBtnHtml;
                }
                return;
            }

            if (response.status >= 500) {
                showToast('Máy chủ gặp lỗi khi xử lý yêu cầu. Vui lòng thử lại sau.', 'danger');
                if (submitBtn) {
                    submitBtn.disabled = false;
                    submitBtn.innerHTML = originalBtnHtml;
                }
                return;
            }

            const contentType = response.headers.get('content-type') || '';
            if (contentType.includes('application/json')) {
                const data = await response.json();
                if (data.success) {
                    const modal = getModalInstance();
                    if (modal) modal.hide();
                    showToast(data.message || 'Thao tác thành công!', 'success');
                    await loadTableUrl(window.location.href, false);
                } else {
                    const errorMsg = data.message || 'Thao tác không thành công.';
                    showToast(errorMsg, 'danger');
                    const errorBox = contentEl ? contentEl.querySelector('[data-modal-error]') : null;
                    if (errorBox) {
                        errorBox.innerHTML = `<i class="bi bi-exclamation-triangle-fill me-1"></i>${escapeHtml(errorMsg)}`;
                        errorBox.classList.remove('d-none');
                    }
                    if (submitBtn) {
                        submitBtn.disabled = false;
                        submitBtn.innerHTML = originalBtnHtml;
                    }
                }
            } else {
                // Server trả về PartialView có lỗi ModelState -> hiển thị ngay trong Modal
                const html = await response.text();
                if (contentEl) {
                    contentEl.innerHTML = html;
                    initModalContent(contentEl);
                }
            }
        } catch (err) {
            console.error('handleModalFormSubmit error:', err);
            showToast('Lỗi kết nối khi gửi dữ liệu.', 'danger');
            if (submitBtn) {
                submitBtn.disabled = false;
                submitBtn.innerHTML = originalBtnHtml;
            }
        }
    }

    // ======================================================================
    // 4. INITIALIZERS CHO NỘI DUNG ĐỘNG TRONG MODAL
    // ======================================================================
    function initModalContent(container) {
        reparseValidation(container);
        initPhoneNumericLock(container);
        initStudentAutoIdForm(container);
        initPermissionMatrix(container);
        initImportExcelWorkflow(container);

        // Tự động focus vào ô nhập liệu đầu tiên khi mở Modal (trừ trang document gốc)
        if (container !== document) {
            const firstInput = container.querySelector('input:not([type="hidden"]):not([type="checkbox"]):not([type="radio"]):not([readonly]):not([disabled]), select:not([disabled])');
            if (firstInput) {
                setTimeout(() => firstInput.focus(), 120);
            }
        }
    }

    function reparseValidation(container) {
        if (window.jQuery && $.validator && $.validator.unobtrusive) {
            $(container).find('form').each(function () {
                $(this).removeData('validator');
                $(this).removeData('unobtrusiveValidation');
                $.validator.unobtrusive.parse(this);
            });
        }
    }

    function initPhoneNumericLock(container) {
        container.querySelectorAll('input[data-digits-only="true"]').forEach((input) => {
            input.addEventListener('input', function () {
                this.value = this.value.replace(/\D/g, '');
            });
        });
    }

    // Khởi tạo logic Sinh mã SV tự động + Nhập nhanh mã khoa + Nhận diện Khoa từ Mã SV
    function initStudentAutoIdForm(container) {
        const facultySelect = container.querySelector('#facultySelect');
        const studentIdInput = container.querySelector('#studentIdInput');
        if (!facultySelect || !studentIdInput) return;

        const facultyQuickInput = container.querySelector('#facultyQuickInput');
        const btnAutoGenerate = container.querySelector('#btnAutoGenerate');
        const nextIdUrl = studentIdInput.getAttribute('data-next-id-url') || '/Student/GetNextStudentId';

        const codeToFacultyMap = {};
        const idToCodeMap = {};

        facultySelect.querySelectorAll('option').forEach((opt) => {
            const fId = opt.value;
            const fCode = opt.getAttribute('data-code');
            if (fId && fCode) {
                const padded = String(fCode).padStart(2, '0');
                codeToFacultyMap[padded] = fId;
                idToCodeMap[fId] = padded;
            }
        });

        // Đồng bộ ô nhập nhanh nếu đã có khoa được chọn sẵn (VD: sau lỗi validation)
        if (facultyQuickInput && facultySelect.value && idToCodeMap[facultySelect.value]) {
            facultyQuickInput.value = idToCodeMap[facultySelect.value];
        }

        let isAutoFilling = false;

        async function triggerAutoGenerate(force = false) {
            const facultyId = facultySelect.value;
            if (!facultyId) {
                if (force) {
                    showToast('Vui lòng chọn Khoa trước để sinh mã tự động!', 'warning');
                }
                return;
            }

            isAutoFilling = true;
            studentIdInput.value = 'Đang tạo mã...';

            try {
                const res = await fetch(`${nextIdUrl}?facultyId=${encodeURIComponent(facultyId)}`, {
                    headers: AJAX_HEADERS,
                    credentials: 'same-origin'
                });
                const data = await res.json();
                if (data && data.studentId) {
                    studentIdInput.value = data.studentId;
                } else {
                    studentIdInput.value = '';
                    if (data && data.message) {
                        showToast(data.message, 'warning');
                    }
                }
            } catch {
                studentIdInput.value = '';
            } finally {
                isAutoFilling = false;
            }
        }

        // 1. Tự nhập mã sinh viên -> tự nhận diện Khoa
        studentIdInput.addEventListener('input', function () {
            if (isAutoFilling) return;
            const val = this.value.trim();
            if (val.length < 4) return;

            let matchedFacultyId = null;
            if (val.length >= 4 && val.charAt(2) === '0') {
                const codeAt2 = val.substring(2, 4);
                if (codeToFacultyMap[codeAt2]) {
                    matchedFacultyId = codeToFacultyMap[codeAt2];
                }
            }
            if (!matchedFacultyId && val.length >= 6) {
                const codeAt4 = val.substring(4, 6);
                if (codeToFacultyMap[codeAt4]) {
                    matchedFacultyId = codeToFacultyMap[codeAt4];
                }
            }

            if (matchedFacultyId && facultySelect.value !== matchedFacultyId) {
                facultySelect.value = matchedFacultyId;
                if (facultyQuickInput) {
                    facultyQuickInput.value = idToCodeMap[matchedFacultyId] || matchedFacultyId;
                }
            }
        });

        // 2. Nhập nhanh mã Khoa (05, 5, CNTT, MO...)
        if (facultyQuickInput) {
            facultyQuickInput.addEventListener('input', function () {
                const keyword = this.value.trim().toUpperCase();
                if (!keyword) return;

                let matchedId = null;
                if (/^\d{1,2}$/.test(keyword)) {
                    const pad = keyword.padStart(2, '0');
                    if (codeToFacultyMap[pad]) {
                        matchedId = codeToFacultyMap[pad];
                    }
                }
                if (!matchedId) {
                    for (const opt of facultySelect.options) {
                        if (opt.value.toUpperCase() === keyword) {
                            matchedId = opt.value;
                            break;
                        }
                    }
                }

                if (matchedId) {
                    facultySelect.value = matchedId;
                    if (!studentIdInput.value.trim()) {
                        triggerAutoGenerate(false);
                    }
                }
            });
        }

        // 3. Chọn Khoa từ Dropdown
        facultySelect.addEventListener('change', function () {
            const selectedId = this.value;
            if (facultyQuickInput) {
                facultyQuickInput.value = selectedId && idToCodeMap[selectedId] ? idToCodeMap[selectedId] : '';
            }
            if (!studentIdInput.value.trim()) {
                triggerAutoGenerate(false);
            }
        });

        // 4. Bấm nút "Sinh mã"
        if (btnAutoGenerate) {
            btnAutoGenerate.addEventListener('click', function () {
                triggerAutoGenerate(true);
            });
        }
    }

    // Khởi tạo giao diện gom nhóm phân quyền Role Claims (Chọn tất cả / Bỏ chọn tất cả)
    function initPermissionMatrix(container) {
        const matrix = container.querySelector('[data-permission-matrix="true"]');
        if (!matrix) return;

        function updateCounts() {
            const allBoxes = matrix.querySelectorAll('.permission-checkbox');
            const checkedAll = matrix.querySelectorAll('.permission-checkbox:checked');
            const totalBadge = matrix.querySelector('[data-total-selected-badge]');
            if (totalBadge) {
                totalBadge.textContent = `${checkedAll.length} / ${allBoxes.length} quyền đã chọn`;
            }

            matrix.querySelectorAll('[data-permission-group]').forEach((groupCard) => {
                const boxes = groupCard.querySelectorAll('.permission-checkbox');
                const checked = groupCard.querySelectorAll('.permission-checkbox:checked');
                const badge = groupCard.querySelector('[data-group-count-badge]');
                if (badge) {
                    badge.textContent = `${checked.length}/${boxes.length}`;
                }
            });
        }

        matrix.querySelectorAll('[data-select-group]').forEach((btn) => {
            btn.addEventListener('click', function () {
                const groupName = this.getAttribute('data-select-group');
                const shouldCheck = this.getAttribute('data-check-state') === 'true';
                const groupCard = matrix.querySelector(`[data-permission-group="${CSS.escape(groupName)}"]`);
                if (groupCard) {
                    groupCard.querySelectorAll('.permission-checkbox').forEach((cb) => {
                        cb.checked = shouldCheck;
                    });
                    updateCounts();
                }
            });
        });

        matrix.querySelectorAll('[data-select-all-permissions]').forEach((btn) => {
            btn.addEventListener('click', function () {
                const shouldCheck = this.getAttribute('data-select-all-permissions') === 'true';
                matrix.querySelectorAll('.permission-checkbox').forEach((cb) => {
                    cb.checked = shouldCheck;
                });
                updateCounts();
            });
        });

        matrix.querySelectorAll('.permission-checkbox').forEach((cb) => {
            cb.addEventListener('change', updateCounts);
        });

        updateCounts();
    }

    // Khởi tạo quy trình 2 bước Import Excel (Bước 1: Xem trước thống kê -> Bước 2: Xác nhận Append/Overwrite)
    function initImportExcelWorkflow(container) {
        const form = container.querySelector('#importExcelForm');
        if (!form) return;

        const fileInput = form.querySelector('#excelFileInput');
        const btnPreview = form.querySelector('#btnPreviewImport');
        const previewSection = form.querySelector('#importPreviewSection');
        const step2Section = form.querySelector('#importStep2Section');
        const btnConfirm = form.querySelector('#btnConfirmImport');
        const errorBox = form.querySelector('[data-modal-error]');
        const modeRadios = form.querySelectorAll('input[name="importMode"]');
        const previewUrl = form.getAttribute('data-preview-url');

        function resetPreview() {
            if (previewSection) previewSection.classList.add('d-none');
            if (step2Section) step2Section.classList.add('d-none');
            if (btnConfirm) btnConfirm.disabled = true;
            modeRadios.forEach((r) => { r.checked = false; });
            if (errorBox) {
                errorBox.classList.add('d-none');
                errorBox.textContent = '';
            }
        }

        if (fileInput) {
            fileInput.addEventListener('change', resetPreview);
        }

        modeRadios.forEach((radio) => {
            radio.addEventListener('change', function () {
                if (btnConfirm) {
                    btnConfirm.disabled = !form.querySelector('input[name="importMode"]:checked');
                }
            });
        });

        if (btnPreview) {
            btnPreview.addEventListener('click', async function () {
                if (!fileInput || !fileInput.files || fileInput.files.length === 0) {
                    showToast('Vui lòng chọn tệp Excel (.xlsx) trước khi kiểm tra.', 'warning');
                    return;
                }

                if (errorBox) {
                    errorBox.classList.add('d-none');
                    errorBox.textContent = '';
                }

                const originalHtml = btnPreview.innerHTML;
                btnPreview.disabled = true;
                btnPreview.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>Đang phân tích file...';

                try {
                    const formData = new FormData();
                    const tokenInput = form.querySelector('input[name="__RequestVerificationToken"]');
                    if (tokenInput) {
                        formData.append('__RequestVerificationToken', tokenInput.value);
                    }
                    formData.append('excelFile', fileInput.files[0]);

                    const res = await fetch(previewUrl, {
                        method: 'POST',
                        body: formData,
                        headers: AJAX_HEADERS,
                        credentials: 'same-origin'
                    });

                    if (res.status === 403) {
                        showToast('Bạn không có quyền nhập dữ liệu Excel.', 'danger');
                        return;
                    }

                    const data = await res.json();
                    if (!data.success) {
                        if (errorBox) {
                            errorBox.innerHTML = `<i class="bi bi-exclamation-triangle-fill me-1"></i>${escapeHtml(data.errorMessage || 'Không thể phân tích file Excel.')}`;
                            errorBox.classList.remove('d-none');
                        }
                        if (previewSection) previewSection.classList.add('d-none');
                        if (step2Section) step2Section.classList.add('d-none');
                        return;
                    }

                    // Đổ số liệu thống kê Bước 1
                    const setText = (sel, val) => {
                        const el = form.querySelector(sel);
                        if (el) el.textContent = val;
                    };
                    setText('[data-stat="total"]', data.totalDataRows);
                    setText('[data-stat="valid"]', data.validRows);
                    setText('[data-stat="new"]', data.newCount);
                    setText('[data-stat="duplicate"]', data.duplicateCount);
                    setText('[data-stat="error"]', data.errorCount);
                    setText('[data-label-new-count]', data.newCount);
                    setText('[data-label-dup-count]', data.duplicateCount);
                    setText('[data-label-dup-count-2]', data.duplicateCount);
                    setText('[data-label-new-count-2]', data.newCount);

                    // Danh sách mã trùng minh họa (tối đa 10 mã)
                    const dupBox = form.querySelector('#importDuplicateListBox');
                    const dupList = form.querySelector('#importDuplicateList');
                    if (dupBox && dupList) {
                        if (data.duplicateCount > 0 && Array.isArray(data.sampleDuplicateIds)) {
                            const itemsHtml = data.sampleDuplicateIds
                                .map((id) => `<span class="badge bg-warning-subtle text-warning-emphasis border border-warning-subtle font-monospace">${escapeHtml(id)}</span>`)
                                .join(' ');
                            const moreHtml = data.duplicateCount > data.sampleDuplicateIds.length
                                ? `<span class="badge bg-light text-secondary border">... và ${data.duplicateCount - data.sampleDuplicateIds.length} mã trùng khác</span>`
                                : '';
                            dupList.innerHTML = itemsHtml + ' ' + moreHtml;
                            dupBox.classList.remove('d-none');
                        } else {
                            dupBox.classList.add('d-none');
                            dupList.innerHTML = '';
                        }
                    }

                    // Danh sách dòng lỗi (tối đa 10 lỗi)
                    const errListBox = form.querySelector('#importErrorListBox');
                    const errList = form.querySelector('#importErrorList');
                    if (errListBox && errList) {
                        if (data.errorCount > 0 && Array.isArray(data.sampleErrors)) {
                            const itemsHtml = data.sampleErrors
                                .map((msg) => `<li>${escapeHtml(msg)}</li>`)
                                .join('');
                            const moreHtml = data.errorCount > data.sampleErrors.length
                                ? `<li class="fst-italic text-muted">... và ${data.errorCount - data.sampleErrors.length} lỗi khác.</li>`
                                : '';
                            errList.innerHTML = itemsHtml + moreHtml;
                            errListBox.classList.remove('d-none');
                        } else {
                            errListBox.classList.add('d-none');
                            errList.innerHTML = '';
                        }
                    }

                    if (previewSection) previewSection.classList.remove('d-none');

                    if (data.validRows > 0) {
                        if (step2Section) step2Section.classList.remove('d-none');
                        modeRadios.forEach((r) => { r.checked = false; });
                        if (btnConfirm) btnConfirm.disabled = true;
                    } else {
                        if (step2Section) step2Section.classList.add('d-none');
                        if (btnConfirm) btnConfirm.disabled = true;
                    }
                } catch (err) {
                    console.error('PreviewImportExcel error:', err);
                    showToast('Lỗi kết nối khi phân tích tệp Excel.', 'danger');
                } finally {
                    btnPreview.disabled = false;
                    btnPreview.innerHTML = originalHtml;
                }
            });
        }
    }

    // ======================================================================
    // 5. GLOBAL EVENT DELEGATION
    // ======================================================================
    document.addEventListener('click', function (e) {
        // Mở CRUD Modal
        const modalTrigger = e.target.closest('[data-modal-url]');
        if (modalTrigger) {
            e.preventDefault();
            const url = modalTrigger.getAttribute('data-modal-url');
            const size = modalTrigger.getAttribute('data-modal-size') || '';
            if (url) {
                openCrudModal(url, size);
            }
            return;
        }

        // Chuyển trang / sắp xếp / xóa bộ lọc qua AJAX
        const ajaxLink = e.target.closest('a[data-ajax-link="true"]');
        if (ajaxLink) {
            if (ajaxLink.closest('.disabled')) {
                e.preventDefault();
                return;
            }
            e.preventDefault();
            const href = ajaxLink.getAttribute('href');
            if (href) {
                // Nếu là nút xóa bộ lọc thì reset các ô nhập trong filter form
                if (ajaxLink.hasAttribute('data-ajax-clear-filter')) {
                    const filterForm = document.querySelector('form[data-ajax-filter="true"]');
                    if (filterForm) {
                        const searchInput = filterForm.querySelector('input[name="searchString"]');
                        if (searchInput) searchInput.value = '';
                        const facSelect = filterForm.querySelector('select[name="facultyId"]');
                        if (facSelect) facSelect.value = '';
                        const roleSelect = filterForm.querySelector('select[name="roleFilter"]');
                        if (roleSelect) roleSelect.value = '';
                        const sortInput = filterForm.querySelector('input[name="sortOrder"]');
                        if (sortInput) sortInput.value = '';
                    }
                }
                loadTableUrl(href, true);
            }
        }
    });

    document.addEventListener('submit', function (e) {
        const form = e.target;

        // Form submit bên trong CRUD Modal
        if (form.matches('form[data-ajax-form="true"]')) {
            e.preventDefault();
            handleModalFormSubmit(form);
            return;
        }

        // Form tìm kiếm / lọc bảng dữ liệu qua AJAX
        if (form.matches('form[data-ajax-filter="true"]')) {
            e.preventDefault();
            const url = buildUrlFromFilterForm(form);
            loadTableUrl(url, true);
        }
    });

    document.addEventListener('change', function (e) {
        const target = e.target;
        if (target.matches('select[data-ajax-autosubmit="true"]')) {
            const form = target.closest('form[data-ajax-filter="true"]');
            if (form) {
                const url = buildUrlFromFilterForm(form);
                loadTableUrl(url, true);
            }
        }
    });

    // Hỗ trợ cũng khởi tạo Permission Matrix / Student Form khi mở trang độc lập (không qua Modal)
    document.addEventListener('DOMContentLoaded', function () {
        initModalContent(document);
    });
})();
