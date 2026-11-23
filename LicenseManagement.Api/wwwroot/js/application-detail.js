
const params = new URLSearchParams(window.location.search);

const trid = params.get("trid");

// Role มาจาก Server (/account/session) เท่านั้น ไม่อ่านจาก localStorage
let currentRole = null;
let applicationData = null;


// ========================================
// AUTHENTICATION (ตรวจกับ Server ทุกครั้ง ไม่เชื่อค่าใน localStorage)
// ========================================
function handleUnauthorized() {
    localStorage.clear();
    window.location.replace("/login.html");
}

// fetch ที่แนบ X-Session-Token ให้เสมอ ถ้า Session หมดอายุ (401) จะกลับหน้า login
async function authFetch(url, options = {}) {
    const headers = { ...(options.headers || {}) };
    const token = localStorage.getItem("sessionToken");
    if (token) headers["X-Session-Token"] = token;

    const response = await fetch(url, { ...options, headers });

    if (response.status === 401) {
        handleUnauthorized();
        return new Promise(() => { });     // หยุดการทำงานต่อ ระหว่างที่กำลังย้ายหน้า
    }
    return response;
}

// คืนข้อมูลผู้ใช้จาก Server หรือ null ถ้าไม่ผ่าน
async function requireSession() {
    if (!localStorage.getItem("sessionToken")) {
        handleUnauthorized();
        return null;
    }

    try {
        const response = await authFetch(`${API_URL}/account/session`);

        if (response.status === 403) {
            handleUnauthorized();
            return null;
        }
        if (!response.ok) throw new Error("Session check failed");

        return await response.json();
    }
    catch (error) {
        console.error("Session Validation Error:", error);
        alert("Unable to verify your session. Please try again.");
        return null;
    }
}

// เปิดไฟล์ (ไฟล์แนบ / Certificate) ผ่าน API ที่ตรวจสิทธิ์
async function openProtectedFile(url, failMessage) {
    const win = window.open("", "_blank");   // เปิดหน้าต่างทันที กัน popup blocker

    try {
        const response = await authFetch(url);

        if (!response.ok) throw new Error(failMessage);

        const blob = await response.blob();
        const blobUrl = URL.createObjectURL(blob);

        if (win) win.location.href = blobUrl;
        else window.open(blobUrl, "_blank");

        setTimeout(() => URL.revokeObjectURL(blobUrl), 60000);
    }
    catch (error) {
        if (win) win.close();
        alert(error.message || failMessage);
    }
}

function openFile(fileId) {
    return openProtectedFile(
        `${API_URL}/applications/file-download/${encodeURIComponent(fileId)}`,
        "Unable to open the file.");
}


// ========================================
// INITIALIZE
// ========================================
let officerStatusSelect;

// ใช้ได้เมื่อหน้านี้โหลด library มาเท่านั้น
if (window.Choices) {
    new Choices('#officerStatus', {
        searchEnabled: false,
        itemSelectText: '',
        shouldSort: false
    });
}

window.onload = async function () {

    if (!trid) {
        alert("TRID not found");
        return;
    }

    const session = await requireSession();
    if (!session) return;

    currentRole = session.usertype;

    if (currentRole !== "Staff" && currentRole !== "Trader") {
        handleUnauthorized();
        return;
    }

    await load();

    if (window.TomSelect) {
        officerStatusSelect = new TomSelect("#officerStatus", {
            create: false,
            sortField: {
                field: "text",
                direction: "asc"
            }
        });
    }

};

// ========================================
// LOAD DETAIL
// ========================================

async function load() {

    try {

        const response = await authFetch(
            `${API_URL}/applications/Detail/${encodeURIComponent(trid)}`
        );

        if (!response.ok) {
            throw new Error(errorText(await readResponse(response), "Cannot load application."));
        }

        const data = await response.json();
        applicationData = data.application;

        displayApplication(data);

    }
    catch (error) {

        alert("Load Error: " + error.message);

        console.error(error);

    }
}


// ========================================
// DISPLAY APPLICATION
// ========================================
function formatDate(date) {
    if (!date) return "";

    const d = new Date(date);

    if (isNaN(d.getTime())) return "";

    const year = d.getFullYear();
    const month = String(d.getMonth() + 1).padStart(2, "0");
    const day = String(d.getDate()).padStart(2, "0");

    return `${year}-${month}-${day}`;
}

function setText(id, value, isDate = false) {
    const el = document.getElementById(id);

    if (el) {
        el.textContent = isDate
            ? formatDate(value)
            : (value ?? "");
    }
}
function displayApplication(data) {

    const app = data.application;

    setText("trid", app.trid);
    setText("statusname", app.statusname);
    setText("identify", app.identify);
    setText("companyname", app.companyname);
    setText("citizenname", app.citizenname);
    setText("citizenid", app.citizenid);
    setText("licensenumber", app.licensenumber);
    setText("typenm", app.typenm);
    setText("Dateappv", app.dateappv, true);
    setText("Dateexp", app.dateexp, true);
    setText("referencelicensenumber", app.referenceLicenseNumber ?? app.ReferenceLicenseNumber);
    setText("email", app.email);
    setText("tel", app.tel);

    displayProducts(data.products || []);

    displayHistory(data.history || []);

    displayFiles(data.files || []);

    loadVersionHistory();

    // แสดงปุ่มใบอนุญาตเฉพาะที่อนุมัติแล้วและมีเลขใบอนุญาต
    const canViewCertificate =
        Number(app.statusid) === 8 && !!app.licensenumber;

    document.getElementById("btnCertificate")
        .classList.toggle("hidden", !canViewCertificate);

    if (String(app.type) !== "1") {
        document.getElementById("referenceLicenseRow")
            .classList.remove("hidden");
    }

    if (currentRole === "Staff") {

        displayStaff();

    }
    else if (currentRole === "Trader") {

        displayTrader(data);

    }
    else {

        alert("Invalid User Type");

    }
}


function displayFiles(files) {

    const body = document.getElementById("fileBody");

    body.innerHTML = "";

    if (!files || files.length === 0) {

        body.innerHTML = `
            <tr>
                <td colspan="3">No files uploaded</td>
            </tr>
        `;

        return;
    }

    files.forEach((f, index) => {

        // ดูไฟล์ผ่าน API ที่ตรวจ Session (ไม่ใช้ลิงก์ /uploads ตรง ๆ)
        body.insertAdjacentHTML("beforeend", `
            <tr>
                <td>${index + 1}</td>

                <td>
                    📎 ${escapeHtml(f.fileName || "")}
                </td>

                <td>
                    <a href="#" data-file="${Number(f.id)}">
                        View
                    </a>
                </td>
            </tr>
        `);
    });
}

// เปิดไฟล์เมื่อคลิกลิงก์ View (event delegation)
document.addEventListener("click", e => {

    const a = e.target.closest("#fileBody [data-file]");

    if (!a) return;

    e.preventDefault();
    openFile(a.dataset.file);
});


async function loadVersionHistory() {

    try {

        const response = await authFetch(
            `${API_URL}/applications/VersionHistory/${encodeURIComponent(trid)}`);

        if (!response.ok) return;

        const versions = await response.json();

        let html = "";

        versions.forEach(v => {

            html += `
            <tr>

                <td>
                    V${escapeHtml(v.versionNo)}
                </td>

                <td>
                    ${escapeHtml(new Date(v.createDate).toLocaleString())}
                </td>

                <td>
                    <button type="button" onclick="viewVersion(${Number(v.versionNo)})">View</button>
                </td>

            </tr>
        `;
        });

        document.getElementById(
            "versionBody"
        ).innerHTML = html;

    }
    catch (error) {

        console.error("Version History Error:", error);

    }
}

async function viewVersion(version) {
    location.href =
        `application-version.html?trid=${encodeURIComponent(trid)}&version=${encodeURIComponent(version)}`;
}


// ========================================
// STAFF VIEW
// ========================================

function displayStaff() {

    document.getElementById("staffArea")
        .classList.remove("hidden");

    document.getElementById("traderArea")
        .classList.add("hidden");

    lockApplication(true);

    const statusSelect =
        document.getElementById("officerStatus");

    const remarkBox =
        document.getElementById("officerRemark");

    const saveButton =
        document.querySelector(
            'button[onclick="saveStatus()"]'
        );

    const currentStatus =
        Number(applicationData.statusid);

    const latestRemark =
        applicationData.remark || "";

    statusSelect.disabled = false;

    // ===== จบงานแล้ว =====
    if ([6, 7, 8].includes(currentStatus)) {

        let statusText = "";

        if (currentStatus === 6)
            statusText = "Trader Cancel";

        else if (currentStatus === 7)
            statusText = "Rejected";

        else if (currentStatus === 8)
            statusText = "Approved";

        statusSelect.innerHTML = `
            <option value="${currentStatus}">
                ${statusText}
            </option>
        `;

        statusSelect.disabled = true;

        remarkBox.value = latestRemark;
        remarkBox.readOnly = true;

        saveButton.style.display = "none";
    }

    // ===== รอ Trader แก้ =====
    else if (currentStatus === 4) {

        statusSelect.innerHTML = `
            <option value="4">
                Edit Required
            </option>
        `;

        statusSelect.disabled = true;

        remarkBox.value = latestRemark;
        remarkBox.readOnly = true;

        saveButton.style.display = "none";
    }

    // ===== Trader ส่งกลับมาแล้ว =====
    else if (currentStatus === 5) {

        statusSelect.innerHTML = `
            <option value="3">Officer Review</option>
            <option value="4">Edit</option>
            <option value="7">Officer Rejected</option>
            <option value="8">Approved</option>
        `;

        statusSelect.value = "3";

        remarkBox.value = "";
        remarkBox.readOnly = false;

        saveButton.style.display = "inline-block";
    }

    // ===== Draft / Submitted / Review =====
    else {

        statusSelect.innerHTML = `
            <option value="3">Officer Review</option>
            <option value="4">Edit</option>
            <option value="7">Officer Rejected</option>
            <option value="8">Approved</option>
        `;

        statusSelect.value =
            [3, 4, 7, 8].includes(currentStatus)
                ? currentStatus
                : 3;

        remarkBox.value = "";
        remarkBox.readOnly = false;

        saveButton.style.display = "inline-block";
    }

    statusChanged();
}
// ========================================
// TRADER VIEW
// ========================================

function displayTrader(data) {

    document.getElementById("staffArea")
        .classList.add("hidden");

    document.getElementById("traderArea")
        .classList.remove("hidden");

    const status = Number(applicationData.statusid);

    lockApplication(true);


    // แสดง Submit เฉพาะ Draft
    if (status === 1) {

        document.getElementById("submitArea")
            .classList.remove("hidden");
    }
    else {

        document.getElementById("submitArea")
            .classList.add("hidden");
    }

    // Cancel ได้ทุกสถานะ ยกเว้น 6,7,8,9
    const cancelArea = document.getElementById("cancelArea");

    if ([6, 7, 8, 9].includes(status)) {
        cancelArea.classList.add("hidden");
    } else {
        cancelArea.classList.remove("hidden");
    }
}

// ========================================
// LOCK / UNLOCK
// ========================================

function lockApplication(lock) {

    const type = document.getElementById("type");
    const email = document.getElementById("email");
    const tel = document.getElementById("tel");
    const remark = document.getElementById("remark");

    if (type) type.disabled = lock;
    if (email) email.disabled = lock;
    if (tel) tel.disabled = lock;
    if (remark) remark.disabled = lock;

    const addProductButton =
        document.getElementById("addProductButton");

    if (addProductButton) {
        addProductButton.style.display =
            lock ? "none" : "inline-block";
    }

    const actionHeader =
        document.getElementById("actionHeader");

    if (actionHeader) {
        actionHeader.style.display =
            lock ? "none" : "table-cell";
    }

    document.querySelectorAll(".productInput")
        .forEach(input => input.disabled = lock);

    document.querySelectorAll(".productAction")
        .forEach(td => {
            td.style.display = lock ? "none" : "table-cell";
        });
}

// ========================================
// DISPLAY PRODUCTS
// ========================================

function displayProducts(products) {

    const body = document.getElementById("productBody");

    body.innerHTML = "";

    products.forEach(p => {

        const row = document.createElement("tr");

        row.innerHTML = `
            <td>${escapeHtml(p.rid ?? p.RID ?? "")}</td>
            <td>${escapeHtml(p.newcode ?? p.NEWCODE ?? "")}</td>
            <td>${escapeHtml(p.productname ?? p.PRODUCTNAME ?? "")}</td>
            <td>${escapeHtml(p.modelname ?? p.MODELNAME ?? "")}</td>
            <td>${escapeHtml(p.description ?? p.DESCRIPTION ?? "")}</td>
        `;

        body.appendChild(row);

    });

    lockApplication(true);

}


// ========================================
// OFFICER STATUS CHANGE
// ========================================

function statusChanged() {

    const status = Number(
        document.getElementById("officerStatus").value
    );

    const remark = document.getElementById("officerRemark");

    if (status === 4) {

        remark.placeholder =
            "Please specify what Trader needs to edit";

    }
    else if (status === 7) {

        remark.placeholder =
            "Please enter rejection reason";

    }
    else {

        remark.placeholder =
            "Optional remark";

    }

}


// ========================================
// SAVE OFFICER STATUS (Staff เท่านั้น ตรวจที่ API จาก Session)
// ========================================

async function saveStatus() {

    const status = Number(
        document.getElementById("officerStatus").value
    );

    const remark =
        document.getElementById("officerRemark").value.trim();

    if ([4, 7].includes(status) && !remark) {

        alert("Please enter remark");

        return;

    }

    if (!confirm("Confirm update status?")) return;

    try {

        const response = await authFetch(
            `${API_URL}/applications/UpdateStatus/${encodeURIComponent(trid)}`,
            {
                method: "PUT",
                headers: {
                    "Content-Type": "application/json"
                },
                body: JSON.stringify({
                    statusid: status,
                    remark: remark
                })
            }
        );

        const result = await readResponse(response);

        if (!response.ok) {
            throw new Error(errorText(result, "Update failed"));
        }

        alert("Update Status Success");

        await load();

    }
    catch (error) {

        alert("Update Error: " + error.message);

    }

}


// ========================================
// TRADER SUBMITTED
// ========================================

async function submitApplication() {

    const status = Number(applicationData.statusid);

    if (![1, 4, 5].includes(status)) {

        alert("Cannot submit this application");

        return;

    }

    if (!confirm("Confirm Submitted?")) return;

    try {

        // ไม่ส่ง body: API ระบุตัวตนจาก Session
        const response = await authFetch(
            `${API_URL}/applications/Submitted/${encodeURIComponent(trid)}`,
            { method: "POST" }
        );

        const result = await readResponse(response);

        if (!response.ok) {
            throw new Error(errorText(result, "Submit failed"));
        }

        alert("Submitted Success");

        await load();

    }
    catch (error) {

        alert("Submit Error: " + error.message);

    }

}


// ========================================
// TRADER CANCEL
// ========================================

function showCancel() {

    document.getElementById("cancelForm")
        .classList.remove("hidden");

}


async function confirmCancel() {

    const remark = document.getElementById("cancelRemark")
        .value.trim();

    if (!remark) {

        alert("Please enter cancel remark");

        return;

    }

    if (!confirm("Confirm Trader Cancel?")) return;

    try {

        const response = await authFetch(
            `${API_URL}/applications/TraderCancel/${encodeURIComponent(trid)}`,
            {
                method: "POST",
                headers: {
                    "Content-Type": "application/json"
                },
                body: JSON.stringify({
                    remark: remark
                })
            }
        );

        const result = await readResponse(response);

        if (!response.ok) {
            throw new Error(errorText(result, "Cancel failed"));
        }

        alert("Cancel Success");

        await load();

    }
    catch (error) {

        alert("Cancel Error: " + error.message);

    }

}


// ========================================
// STATUS HISTORY
// ========================================

function getLastEditRemark(history) {

    const edit = history.find(x => x.statusid === 4);

    return edit?.remark || "No remark";

}


function displayHistory(history) {

    const body = document.getElementById("historyBody");

    body.innerHTML = "";

    history.forEach(h => {

        const row = document.createElement("tr");

        const values = [
            h.statusname,
            h.nameupdate,
            h.remark,
            h.dateupdate
                ? new Date(h.dateupdate).toLocaleString()
                : ""
        ];

        values.forEach(value => {

            const td = document.createElement("td");

            td.textContent = value || "";

            row.appendChild(td);

        });

        body.appendChild(row);

    });

}

// ========================================
// RESPONSE HELPER
// ========================================

async function readResponse(response) {

    const text = await response.text();

    try {
        return JSON.parse(text);
    }
    catch {
        return text;
    }

}

function errorText(result, fallback) {

    if (typeof result === "string" && result.trim())
        return result;

    return (result && (result.message || result.Message || result.title))
        || fallback;

}


// ========================================
// HTML SAFE
// ========================================

function escapeHtml(value) {

    return String(value ?? "")
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#039;");

}


// ========================================
// BACK
// ========================================

function backPage() {

    location.href = "applications.html";

}

// ========================================
//  Certificate (ดึงผ่าน API ด้วย Session แล้วเปิดเป็น PDF)
// ========================================
function viewCertificate(trid) {
    return openProtectedFile(
        `${API_URL}/applications/certificate/${encodeURIComponent(trid)}`,
        "Unable to load certificate.");
}
