// ========================================
// AUTHENTICATION
// ========================================
const $ = id => document.getElementById(id);
let currentUser = null;
let currentRole = null;
let isStaff = false;
let isTrader = false;

let sessionToken = null;


function handleUnauthorized() {
    localStorage.clear();
    window.location.replace("/login.html");
}

// ตรวจสอบ Session กับ Backend ก่อนเปิดหน้า
async function validateSession() {

    sessionToken = localStorage.getItem("sessionToken");

    if (!sessionToken) {
        handleUnauthorized();
        return false;
    }

    try {
        const response = await fetch(
            `${API_URL}/account/session`,
            {
                method: "GET",
                headers: authHeaders()
            }
        );

        if (!response.ok) {
            handleUnauthorized();
            return false;
        }

        const result = await response.json();

        currentUser = result;
        currentRole = result.usertype;

        isStaff = currentRole === "Staff";
        isTrader = currentRole === "Trader";

        if (!isStaff && !isTrader) {
            handleUnauthorized();
            return false;
        }

        return true;

    } catch (error) {
        console.error("Session Validation Error:", error);

        // กรณี Server ติดต่อไม่ได้ ไม่ควรเปิดหน้าแบบไม่มีการตรวจสอบ
        $("tbodyApp").innerHTML =
            `<tr><td colspan="14" class="table-error">
                Unable to verify your session. Please try again.
            </td></tr>`;

        return false;
    }
}

// ========================================
// PAGINATION
// ========================================
let totalRecords = 0;
let totalPages = 1;
let page = 1;
let abortCtrl = null;
const DEFAULT_PAGE_SIZE = 20;

// ========================================
// SETTINGS
// ========================================
const EDITABLE_STATUS = [1, 4];
const PENDING_STATUS = [2, 3, 5];
const WAITING_TRADER = [4];
const REJECTED_STATUS = [6, 7, 9];

const SLA_DAYS = 7;
const WARN_DAYS = 4;

const STATUS_OPTIONS = [
    { id: 1, name: "Draft" },
    { id: 2, name: "Submitted" },
    { id: 3, name: "Officer Review" },
    { id: 4, name: "Revision Required" },
    { id: 5, name: "Applicant Edited" },
    { id: 6, name: "Applicant cancel" },
    { id: 7, name: "Officer Rejected" },
    { id: 8, name: "Approved" },
    { id: 9, name: "Cancel License" }
];

const TYPE_ID = {
    "New License": 1,
    "Amendment": 2,
    "Renew": 3,
    "Cancel": 4
};

const TYPE_OPTIONS = [
    { id: 1, name: "New License" },
    { id: 2, name: "Amendment" },
    { id: 3, name: "Renew" },
    { id: 4, name: "Cancel" }
];

const STATUS_COLOR = {
    1: "#9ca3af",
    2: "#0891b2",
    3: "#2563eb",
    4: "#d97706",
    5: "#7c3aed",
    6: "#dc2626",
    7: "#dc2626",
    8: "#16a34a",
    9: "#dc2626"
};

const COLUMNS = [
    { key: "no", label: "No.", sort: false },
    { key: "trid", label: "TRID" },
    { key: "typenm", label: "Type" },
    { key: "companyname", label: "Company Name" },
    { key: "statusname", label: "Status" },
    { key: "submitted", label: "Submitted", optional: true },
    { key: "aging", label: "Waiting", optional: true, staffOnly: true },
    { key: "officer", label: "Officer", optional: true, staffOnly: true },
    { key: "productcount", label: "Products", optional: true },
    { key: "licensenumber", label: "License Number" },
    { key: "dateappv", label: "Approval Date" },
    { key: "remark", label: "Remark" },
    { key: "action", label: "Action", sort: false }
];

const TYPE_BUTTONS = [
    { name: "New License", color: "#4a90d9" },
    { name: "Amendment", color: "#f5b400" },
    { name: "Renew", color: "#5aa832" },
    { name: "Cancel", color: "#ec7a2c" }
];

// ========================================
// GLOBAL VARIABLES
// ========================================
let allRows = [];
let view = [];
let activeCols = COLUMNS;

let activeCard = "";
let sort = null;

let searchTimer;
let requestId = 0;

let summaryData = {
    pending: 0,
    waitingTrader: 0,
    approved: 0,
    rejected: 0
};

let typeCounts = {};



// ========================================
// HELPERS
// ========================================

function authHeaders() {

    const token =
        localStorage.getItem(
            "sessionToken"
        );

    return {
        "Content-Type": "application/json",

        ...(token
            ? { "X-Session-Token": token }
            : {})
    };
}

function escapeHtml(v) {
    return String(v ?? "")
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#039;");
}

function toDate(v) {
    if (!v) return null;

    const d = new Date(v);

    return isNaN(d.getTime()) ? null : d;
}

function formatDate(v) {
    const d = toDate(v);

    if (!d) return "";

    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}

function getStatusClass(id) {
    switch (Number(id)) {
        case 1: return "status-draft";
        case 2: return "status-submitted";
        case 4: return "status-edit";
        case 5: return "status-edited";
        case 6:
        case 7:
        case 9:
            return "status-rejected";
        case 8:
            return "status-approved";
        default:
            return "status-review";
    }
}

function daysSince(d) {
    return Math.floor(
        (Date.now() - d.getTime()) / 86400000
    );
}

// อ่านค่าฟิลด์จากหลายชื่อที่เป็นไปได้
function pick(x, names) {
    for (const n of names) {
        const key = Object.keys(x).find(
            k => k.toLowerCase() === n.toLowerCase()
        );

        if (
            key !== undefined &&
            x[key] !== null &&
            x[key] !== undefined &&
            x[key] !== ""
        ) {
            return x[key];
        }
    }

    return null;
}

// ========================================
// NORMALIZE API DATA
// ========================================
function normalize(x) {
    const submitted = toDate(
        pick(x, ["datesubmission"])
    );

    const statusid = Number(x.statusid);

    const pending = PENDING_STATUS.includes(statusid);

    return {
        ...x,

        statusid,

        submitted,

        created: toDate(
            pick(x, ["datecreate", "createddate"])
        ),

        approved: toDate(x.dateappv),

        officer:
            x.officername ??
            x.assignedto ??
            x.officer ??
            "",

        productcount:
            x.productcount ??
            x.productcnt ??
            null,

        aging:
            pending && submitted
                ? daysSince(submitted)
                : null
    };
}

// ========================================
// MESSAGE ROW
// ========================================
function setMessageRow(cssClass, message) {
    $("tbodyApp").innerHTML =
        `<tr><td colspan="${activeCols.length}" class="${cssClass}">${escapeHtml(message)}</td></tr>`;

    $("resultCount").textContent = "";
    $("pageInfo").textContent = "";
    $("pageNo").textContent = "";

    $("btnPrev").disabled = true;
    $("btnNext").disabled = true;
}

// ========================================
// INITIALIZE
// ========================================
window.onload = async function () {

    // ตรวจสอบ Session ก่อนทุกอย่าง
    const authenticated = await validateSession();

    if (!authenticated) return;

    // แสดงหน้าตาม Role ที่ Backend ยืนยัน
    $("traderArea").classList.toggle(
        "hidden",
        isStaff
    );

    $("typeSidebar").classList.toggle(
        "hidden",
        !isStaff
    );

    $("summary").classList.toggle(
        "hidden",
        !isStaff
    );

    $("subtitle").textContent = isStaff
        ? "Review and manage license applications"
        : "License Application Management";

    activeCols = visibleColumns([]);
    renderHead();

    $("txtSearch").addEventListener("input", () => {
        clearTimeout(searchTimer);

        searchTimer = setTimeout(() => {
            page = 1;
            loadApplications();
        }, 400);
    });

    [
        "selStatus",
        "selType",
        "subFrom",
        "subTo",
        "appFrom",
        "appTo"
    ].forEach(id => {
        $(id).addEventListener("change", () => {
            activeCard = "";
            page = 1;
            loadApplications();
        });
    });

    $("selPageSize").addEventListener("change", () => {
        page = 1;
        loadApplications();
    });

    $("btnPrev").addEventListener("click", () => {
        if (page > 1) {
            page--;
            loadApplications();
        }
    });

    $("btnNext").addEventListener("click", () => {
        if (page < totalPages) {
            page++;
            loadApplications();
        }
    });

    $("btnClear").addEventListener(
        "click",
        clearFilters
    );

    $("btnExport").addEventListener(
        "click",
        exportCsv
    );

    fillDropdowns();

    $("tbodyApp").addEventListener("click", e => {
        const a = e.target.closest("[data-cert]");
        if (!a) return;
        e.preventDefault();
        viewCertificate(a.dataset.cert);   // viewCertificate encode เองครั้งเดียวอยู่แล้ว
    });

    // Staff เท่านั้นที่เรียก Summary
    if (isStaff) {
        await Promise.all([
            loadStats(),
            loadApplications()
        ]);
    } else {
        // Trader เรียกเฉพาะรายการ Applications
        await loadApplications();
    }
};

// ========================================
// loadStats
// ========================================

async function loadStats() {

    if (!isStaff) return;

    try {

        const res = await fetch(
            `${API_URL}/applications/GetApplicationStats`,
            {
                method: "GET",
                headers: authHeaders()
            }
        );

        if (res.status === 401 || res.status === 403) {
            handleUnauthorized();
            return;
        }

        if (!res.ok) {
            throw new Error("Unable to load statistics.");
        }

        const json = await res.json();

        summaryData = {
            pending: json.summary?.pending ?? 0,
            waitingTrader: json.summary?.waitingTrader ?? 0,
            approved: json.summary?.approved ?? 0,
            rejected: json.summary?.rejected ?? 0
        };

        typeCounts = {};

        (json.typeCounts || []).forEach(x => {
            if (x.type != null) {
                typeCounts[x.type] = x.count;
            }
        });

        renderSummary();
        renderTypeButtons();

    } catch (error) {

        console.error("Load Stats Error:", error);

    }
}

// ========================================
// LOAD APPLICATIONS FROM API
// ========================================
async function loadApplications() {
    abortCtrl?.abort();
    abortCtrl = new AbortController();
    const myRequest = ++requestId;

    setMessageRow(
        "table-empty",
        "Loading..."
    );

    try {

        const pageSize = Number(
            $("selPageSize").value || DEFAULT_PAGE_SIZE
        );

        const params = new URLSearchParams({
            page: String(page),
            pageSize: String(pageSize),
            keyword: $("txtSearch").value.trim()
        });

        // Status filter
        const status = $("selStatus").value;

        if (status) {
            params.set("statusId", status);
        }

        // Type filter
        const typeName = $("selType").value;

        if (typeName && TYPE_ID[typeName]) {
            params.set("type", TYPE_ID[typeName]);
        }

        // Submission date
        if ($("subFrom").value) {
            params.set(
                "submissionFrom",
                $("subFrom").value
            );
        }

        if ($("subTo").value) {
            params.set(
                "submissionTo",
                $("subTo").value
            );
        }

        // Approval date
        if ($("appFrom").value) {
            params.set(
                "approvalFrom",
                $("appFrom").value
            );
        }

        if ($("appTo").value) {
            params.set(
                "approvalTo",
                $("appTo").value
            );
        }

        // Sort
        if (sort) {
            params.set("sortBy", sort.key);
            params.set("sortDir", sort.dir);
        }

        // Summary card
        if (activeCard) {
            params.set("card", activeCard);
        }

        const response = await fetch(
            `${API_URL}/applications/GetApplications?${params}`,
            {
                method: "GET",
                headers: authHeaders(),
                signal: abortCtrl.signal
            }
        );

        if (response.status === 401 || response.status === 403) {
            handleUnauthorized();
            return;
        }

        if (!response.ok) {
            throw new Error(
                `API Error: ${response.status}`
            );
        }


        const json = await response.json();

        if (myRequest !== requestId) return;

        if (!Array.isArray(json.data)) {
            console.error(
                "Unexpected API response:",
                json
            );

            throw new Error(
                "Unexpected response format"
            );
        }

        // Pagination data
        totalRecords = json.totalRecords ?? 0;
        totalPages = json.totalPages ?? 0;

        // Current page data only
        allRows = json.data.map(normalize);
        view = [...allRows];

        // Render
        activeCols = visibleColumns(allRows);

        renderHead();
        updateTitle();
        renderTable();

    

    } catch (error) {

        if (error.name === "AbortError") return;

        if (myRequest !== requestId) return;

        console.error("Load Applications Error:", error);

        setMessageRow(
            "table-error",
            "Unable to load applications. Check your connection and try again."
        );
    }
}

// ========================================
// VISIBLE COLUMNS
// ========================================
function visibleColumns(rows) {

    return COLUMNS.filter(c => {

        if (c.staffOnly && !isStaff) {
            return false;
        }

        if (!c.optional) {
            return true;
        }

        if (c.key === "aging") {
            return isStaff;
        }

        if (c.key === "submitted") {
            return rows.some(r => r.submitted);
        }

        return rows.some(r =>
            r[c.key] !== null &&
            r[c.key] !== "" &&
            r[c.key] !== undefined
        );
    });
}

// ========================================
// DROPDOWNS
// ========================================
function fillDropdowns() {

    const keepStatus = $("selStatus").value;
    const keepType = $("selType").value;

    // Status options: fixed list, not from current page
    $("selStatus").innerHTML =
        `<option value="">All statuses</option>` +
        STATUS_OPTIONS.map(s => `
            <option value="${s.id}">
                ${escapeHtml(s.name)}
            </option>
        `).join("");

    // Type options: fixed list
    $("selType").innerHTML =
        `<option value="">All types</option>` +
        TYPE_OPTIONS.map(t => `
            <option value="${escapeHtml(t.name)}">
                ${escapeHtml(t.name)}
            </option>
        `).join("");

    $("selStatus").value = keepStatus;
    $("selType").value = keepType;

    const hasSubmit = allRows.some(r => r.submitted);

    document.querySelectorAll(".col-submit")
        .forEach(el => {
            el.classList.toggle(
                "hidden",
                !hasSubmit
            );
        });
}

// ========================================
// SUMMARY CARDS
// ========================================
function renderSummary() {

    if (!isStaff) return;

    const cards = [
        {
            id: "pending",
            color: "#2563eb",
            num: summaryData.pending,
            label: "Waiting for officer",
            note: ""
        },
        {
            id: "waiting",
            color: "#d97706",
            num: summaryData.waitingTrader,
            label: "Waiting for trader to revise",
            note: ""
        },
        {
            id: "approved",
            color: "#16a34a",
            num: summaryData.approved,
            label: "Approved this month",
            note: ""
        },
        {
            id: "rejected",
            color: "#dc2626",
            num: summaryData.rejected,
            label: "Rejected / cancelled",
            note: ""
        }
    ];

    $("summary").innerHTML = cards.map(c => `
        <button type="button"
            class="stat ${activeCard === c.id ? "active" : ""}"
            style="--c:${c.color}"
            data-card="${c.id}"
            aria-pressed="${activeCard === c.id}">

            <div class="stat-num">
                ${Number(c.num).toLocaleString()}
            </div>

            <div class="stat-label">
                ${c.label}
            </div>

            <div class="stat-note">
                ${escapeHtml(c.note)}
            </div>
        </button>
    `).join("");

    $("summary").querySelectorAll(".stat")
        .forEach(btn => {

            btn.addEventListener("click", () => {

                activeCard =
                    activeCard === btn.dataset.card
                        ? ""
                        : btn.dataset.card;

                $("selStatus").value = "";

                page = 1;

                loadApplications();
            });
        });
}

// ========================================
// CLEAR FILTERS
// ========================================
function clearFilters() {

    [
        "selStatus",
        "selType",
        "subFrom",
        "subTo",
        "appFrom",
        "appTo",
        "txtSearch"
    ].forEach(id => {
        $(id).value = "";
    });

    activeCard = "";
    sort = null;
    page = 1;

    loadApplications();
}

// ========================================
// TABLE HEADER + SERVER SORT
// ========================================
function renderHead() {

    $("theadApp").innerHTML =
        "<tr>" +
        activeCols.map(c => {

            if (c.sort === false) {
                return `<th>${c.label}</th>`;
            }

            const on = sort?.key === c.key;

            const arrow = on
                ? (sort.dir === "asc" ? "▲" : "▼")
                : "";

            const aria = on
                ? (sort.dir === "asc"
                    ? "ascending"
                    : "descending")
                : "none";

            return `
                <th class="sortable"
                    tabindex="0"
                    role="button"
                    aria-sort="${aria}"
                    data-key="${c.key}">

                    ${c.label}
                    <span class="arrow">${arrow}</span>
                </th>
            `;

        }).join("") +
        "</tr>";

    $("theadApp").querySelectorAll("th.sortable")
        .forEach(th => {

            const go = () => {

                const key = th.dataset.key;

                sort = (sort?.key === key)
                    ? (
                        sort.dir === "asc"
                            ? { key, dir: "desc" }
                            : null
                    )
                    : { key, dir: "asc" };

                page = 1;

                loadApplications();
            };

            th.addEventListener("click", go);

            th.addEventListener("keydown", e => {

                if (
                    e.key === "Enter" ||
                    e.key === " "
                ) {
                    e.preventDefault();
                    go();
                }
            });
        });
}

// ========================================
// AGING
// ========================================
function agingCell(r) {

    if (r.aging === null) {
        return `<span class="aging none">-</span>`;
    }

    const cls =
        r.aging > SLA_DAYS
            ? "late"
            : r.aging >= WARN_DAYS
                ? "warn"
                : "";

    const text =
        r.aging === 0
            ? "Today"
            : `${r.aging} day${r.aging > 1 ? "s" : ""}`;

    return `
        <span class="aging ${cls}">
            ${text}${r.aging > SLA_DAYS ? " (overdue)" : ""}
        </span>
    `;
}

// ========================================
// ACTION BUTTONS
// ========================================
function actionCell(r) {

    const trid = encodeURIComponent(r.trid ?? "");

    let html = "";

    if (isStaff && r.statusid === 2) {

        html += `
            <a class="action-btn work"
                href="application-detail.html?trid=${trid}">
                Review
            </a>
        `;

    } else if (
        isStaff &&
        (r.statusid === 3 || r.statusid === 5)
    ) {

        html += `
            <a class="action-btn work"
                href="application-detail.html?trid=${trid}">
                Continue
            </a>
        `;

    } else {

        html += `
            <a class="action-btn"
                href="application-detail.html?trid=${trid}">
                Detail
            </a>
        `;
    }

    // Trader แก้ไขได้เฉพาะสถานะที่กำหนด
    if (
        isTrader &&
        EDITABLE_STATUS.includes(r.statusid)
    ) {

        html += `
            <a class="action-btn edit"
                href="application-edit.html?trid=${trid}">
                Edit
            </a>
        `;
    }

    if (r.statusid === 8 && r.licensenumber) {
        html += `
        <a class="action-btn cert" href="#"
            data-cert="${escapeHtml(r.trid ?? "")}">
            Certificate
        </a>
    `;
    }

    return html;
}

// ========================================
// TABLE CELL
// ========================================
function cell(key, r, index) {

    switch (key) {

        case "no":
            return index + 1;

        case "trid":
            return escapeHtml(r.trid);

        case "typenm":
            return escapeHtml(r.typenm);

        case "companyname":
            return escapeHtml(r.companyname);

        case "statusname":
            return `
                <span class="status-badge ${getStatusClass(r.statusid)}">
                    ${escapeHtml(r.statusname)}
                </span>
            `;

        case "submitted":
            return formatDate(r.submitted);

        case "aging":
            return agingCell(r);

        case "officer":
            return escapeHtml(r.officer);

        case "productcount":
            return escapeHtml(r.productcount);

        case "licensenumber": {

            const ls =
                r.licenseStatus ??
                r.licensestatus;

            const tag =
                r.licensenumber &&
                    ls &&
                    ls !== "Active"
                    ? `<span class="license-tag">${escapeHtml(ls)}</span>`
                    : "";

            return escapeHtml(r.licensenumber) + tag;
        }

        case "dateappv":
            return formatDate(r.approved);

        case "remark":
            return escapeHtml(r.remark);

        case "action":
            return actionCell(r);

        default:
            return "";
    }
}

// ========================================
// RENDER TABLE
// ========================================
function renderTable() {

    if (view.length === 0) {

        setMessageRow(
            "table-empty",
            "No applications found."
        );

        $("resultCount").textContent =
            `${totalRecords.toLocaleString()} application(s)`;

        $("pageInfo").textContent = "";

        $("pageNo").textContent =
            `Page ${page} / ${totalPages}`;

        $("btnPrev").disabled = true;
        $("btnNext").disabled = true;

        return;
    }

    const pageSize = Number(
        $("selPageSize").value || DEFAULT_PAGE_SIZE
    );

    const start = (page - 1) * pageSize;
    const end = start + view.length;

    $("resultCount").textContent =
        `${totalRecords.toLocaleString()} application(s)`;

    $("pageInfo").textContent =
        `Showing ${start + 1}-${end} of ${totalRecords.toLocaleString()}`;

    $("pageNo").textContent =
        `Page ${page} / ${totalPages}`;

    $("btnPrev").disabled = page <= 1;
    $("btnNext").disabled = page >= totalPages;

    $("tbodyApp").innerHTML = view.map((r, i) => {

        const bar =
            STATUS_COLOR[r.statusid] || "#e5e7eb";

        return `<tr>` +
            activeCols.map((c, ci) => {

                return `
                    <td class="c-${c.key}"
                        ${ci === 0 ? `style="--bar:${bar}"` : ""}>
                        ${cell(c.key, r, start + i)}
                    </td>
                `;

            }).join("") +
            `</tr>`;

    }).join("");
}

// ========================================
// TYPE SIDEBAR
// ========================================
function renderTypeButtons() {

    if (!isStaff) return;

    const current = $("selType").value;

    const allTypeCount = Object.values(typeCounts)
        .reduce((sum, count) => sum + Number(count || 0), 0);

    const items = [
        {
            name: "",
            label: "All Types",
            color: "#475569",
            count: allTypeCount
        },

        ...TYPE_BUTTONS.map((t, index) => ({
            name: t.name,
            label: t.name,
            color: t.color,
            count: typeCounts[index + 1] ?? 0
        }))
    ];

    $("typeButtons").innerHTML = items.map(t => {

        const on = current === t.name;

        return `
            <button type="button"
                class="type-btn ${on ? "active" : ""}"
                style="--c:${t.color}"
                data-type="${escapeHtml(t.name)}"
                aria-pressed="${on}">

                <span>${escapeHtml(t.label)}</span>

                <span class="cnt">
                    ${Number(t.count).toLocaleString()}
                </span>
            </button>
        `;

    }).join("");

    $("typeButtons").querySelectorAll(".type-btn")
        .forEach(btn => {

            btn.addEventListener("click", () => {

                $("selType").value = btn.dataset.type;

                activeCard = "";
                page = 1;

                loadApplications();
            });
        });
}

// ========================================
// TITLE
// ========================================
function updateTitle() {

    const type = isStaff
        ? $("selType").value
        : "";

    $("pageTitle").textContent =
        type || "Applications";

    $("subtitle").textContent = isStaff
        ? (
            type
                ? `Review and manage ${type} applications`
                : "Review and manage license applications"
        )
        : "License Application Management";

    document.title =
        (type || "Applications") +
        " | License Management System";
}

// ========================================
// EXPORT CSV
// หมายเหตุ: ส่งออกเฉพาะข้อมูลหน้าปัจจุบัน
// ========================================
function exportCsv() {

    if (!view.length) return;

    const cols = activeCols.filter(c =>
        c.key !== "no" &&
        c.key !== "action"
    );

    const val = (r, k) => {

        if (k === "submitted") {
            return formatDate(r.submitted);
        }

        if (k === "dateappv") {
            return formatDate(r.approved);
        }

        if (k === "aging") {
            return r.aging ?? "";
        }

        return r[k] ?? "";
    };

    const q = v =>
        `"${String(v).replace(/"/g, '""')}"`;

    const lines = [
        cols.map(c => q(c.label)).join(",")
    ].concat(
        view.map(r =>
            cols.map(c => q(val(r, c.key))).join(",")
        )
    );

    const blob = new Blob(
        ["\uFEFF" + lines.join("\r\n")],
        {
            type: "text/csv;charset=utf-8;"
        }
    );

    const a = document.createElement("a");

    a.href = URL.createObjectURL(blob);

    a.download =
        `applications_${formatDate(new Date())}.csv`;

    a.click();

    URL.revokeObjectURL(a.href);
}

// ========================================
// CERTIFICATE
// ========================================
async function viewCertificate(trid) {

    try {

        const response = await fetch(
            `${API_URL}/applications/certificate/${encodeURIComponent(trid)}`,
            {
                method: "GET",
                headers: authHeaders()
            }
        );

        if (response.status === 401 || response.status === 403) {
            handleUnauthorized();
            return;
        }

        if (!response.ok) {
            alert("Unable to load certificate.");
            return;
        }

        const blob = await response.blob();

        const url = window.URL.createObjectURL(blob);

        window.open(url, "_blank");

        setTimeout(() => {
            window.URL.revokeObjectURL(url);
        }, 60000);

    } catch (error) {

        console.error("Certificate Error:", error);
        alert("Unable to connect to the server.");

    }
}
