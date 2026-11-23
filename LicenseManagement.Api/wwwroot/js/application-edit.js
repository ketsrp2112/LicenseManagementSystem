const params =
    new URLSearchParams(window.location.search);
const trid = params.get("trid");
let isLocked = false;     // Type 3, 4 = แก้ได้เฉพาะไฟล์แนบ
let selectedFiles = [];   // ไฟล์ที่เลือกไว้ทั้งหมด (สะสมได้)
let isDraft = false;

const EDITABLE_STATUS = [1, 4, 5];   // Draft / Revision Required / Applicant Edited

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

// เปิดไฟล์แนบผ่าน API ที่ตรวจสิทธิ์ (ไม่เปิดตรงจาก /uploads)
async function openFile(fileId) {
    const win = window.open("", "_blank");   // เปิดหน้าต่างทันที กัน popup blocker

    try {
        const response = await authFetch(
            `${API_URL}/applications/file-download/${encodeURIComponent(fileId)}`);

        if (!response.ok) throw new Error("Unable to open the file.");

        const blob = await response.blob();
        const url = URL.createObjectURL(blob);

        if (win) win.location.href = url;
        else window.open(url, "_blank");

        setTimeout(() => URL.revokeObjectURL(url), 60000);
    }
    catch (error) {
        if (win) win.close();
        alert(error.message || "Unable to open the file.");
    }
}

// API ตอบได้ทั้ง JSON และข้อความธรรมดา
async function readBody(response) {
    const text = await response.text();
    try { return JSON.parse(text); } catch { return text; }
}

function errorText(result, fallback) {
    if (typeof result === "string" && result.trim()) return result;
    return (result && (result.message || result.Message || result.title)) || fallback;
}

// ========================================
// INITIALIZE
// ========================================
window.onload = async function () {

    if (!trid) {
        alert("TRID not found");
        window.location.replace("applications.html");
        return;
    }

    const session = await requireSession();
    if (!session) return;

    // หน้านี้สำหรับ Trader เจ้าของคำขอเท่านั้น (ตรวจจาก Server)
    if (session.usertype !== "Trader") {
        window.location.replace(
            `application-detail.html?trid=${encodeURIComponent(trid)}`);
        return;
    }

    document.getElementById("tel")
        .addEventListener("input", function () {

            this.value =
                this.value.replace(/\D/g, "")
                    .slice(0, 11);
        });

    await load();
};

function setValue(id, value) {
    const el = document.getElementById(id);
    if (el) el.value = value ?? "";
}
function setText(id, value) {
    const el = document.getElementById(id);
    if (el) el.textContent = value ?? "";
}

function escapeHtml(value) {
    return String(value ?? "")
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#039;");
}
async function load() {

    try {

        const response = await authFetch(
            `${API_URL}/applications/detail/${encodeURIComponent(trid)}`
        );

        if (!response.ok) {
            throw new Error("Cannot load application data");
        }

        const data = await response.json();

        const app = data.application;

        // แก้ไขได้เฉพาะสถานะที่กำหนด (API ตรวจซ้ำอีกชั้น)
        if (!EDITABLE_STATUS.includes(Number(app.statusid))) {
            alert("This application can't be edited.");
            window.location.replace(
                `application-detail.html?trid=${encodeURIComponent(trid)}`);
            return;
        }

        // Application Information
        setText("identify", app.identify);
        setText("companyname", app.companyname);
        setText("citizenname", app.citizenname);
        setText("citizenid", app.citizenid);
        setValue("email", app.email);
        setValue("tel", app.tel);
        // Type 3 (Renew), 4 (Cancel) แก้ได้เฉพาะไฟล์แนบ
        const appType = Number(app.type);
        isLocked = (appType === 3 || appType === 4);
        applyLockMode();

        // Draft (statusid = 1) = ยังไม่เคยส่งคำขอ จึงไม่มี Officer Request
        isDraft = Number(app.statusid) === 1;

        document.getElementById("officerRequestBox").style.display =
            isDraft ? "none" : "block";

        document.getElementById("editSubtitle").textContent = isDraft
            ? "Update your application before submitting."
            : "Please update the information requested by the officer.";

        if (!isDraft) {
            document.getElementById("officerRemarkDisplay").innerText =
                app.remark ?? "";
        }

        // Product Information
        const productBody =
            document.getElementById("productBody");

        productBody.innerHTML = "";

        const products = data.products || [];

        products.forEach(x => {

            addProductRow(
                x.productname,
                x.modelname,
                x.description
            );

        });

        document.getElementById("existingFilesTitle").textContent = isDraft
            ? "Existing Files"
            : "Existing Files (view only)";

        displayFiles(data.files || []);

    }
    catch (error) {

        console.error("Load Error:", error);

        alert("Cannot load application data");

    }
}

function applyLockMode() {
    document.getElementById("email").readOnly = isLocked;
    document.getElementById("tel").readOnly = isLocked;

    document.getElementById("btnAddProduct").style.display =
        isLocked ? "none" : "inline-block";

    document.getElementById("productActionHeader").style.display =
        isLocked ? "none" : "table-cell";

    document.getElementById("lockNotice").style.display =
        isLocked ? "block" : "none";
}
function displayFiles(files) {

    const body = document.getElementById("fileBody");
    body.innerHTML = "";

    if (!files || files.length === 0) {
        body.innerHTML = `
            <tr><td colspan="3">No files uploaded</td></tr>`;
        return;
    }

    files.forEach((f, index) => {

        const deleteBtn = isDraft && f.id != null
            ? `<button type="button" class="btn-danger"
                       style="background:#dc2626; color:#fff; border:1px solid #dc2626;
                              padding:4px 10px; border-radius:6px; margin-left:8px;"
                       onclick="deleteExistingFile(${Number(f.id)}, this)">
                   Delete
               </button>`
            : "";

        // ดูไฟล์ผ่าน API ที่ตรวจ Session (ไม่ใช้ลิงก์ /uploads ตรง ๆ)
        body.insertAdjacentHTML("beforeend", `
            <tr>
                <td>${index + 1}</td>
                <td>📎 ${escapeHtml(f.fileName || "")}</td>
                <td>
                    <a href="#" data-file="${Number(f.id)}">View</a>
                    ${deleteBtn}
                </td>
            </tr>`);
    });
}

async function deleteExistingFile(fileId, btn) {

    if (!confirm("Delete this file? This cannot be undone.")) return;

    btn.disabled = true;

    try {
        const response = await authFetch(
            `${API_URL}/applications/file/${encodeURIComponent(trid)}/${fileId}`,
            { method: "DELETE" });

        if (!response.ok) {
            throw new Error(errorText(await readBody(response), "Delete failed"));
        }

        // ลบแถวออกจากตารางทันที ไม่ต้องโหลดหน้าใหม่
        btn.closest("tr").remove();

        // จัดเลขลำดับใหม่ / แสดงข้อความถ้าไม่เหลือไฟล์
        const rows = document.querySelectorAll("#fileBody tr");

        if (rows.length === 0) {
            document.getElementById("fileBody").innerHTML =
                `<tr><td colspan="3">No files uploaded</td></tr>`;
        } else {
            rows.forEach((r, i) => r.cells[0].textContent = i + 1);
        }
    }
    catch (error) {
        console.error(error);
        alert("Delete Error: " + error.message);
        btn.disabled = false;
    }
}

const ALLOWED_EXT = [".pdf", ".jpg", ".jpeg", ".png"];
const MAX_FILE_MB = 10;


function validateFiles() {

    for (const file of selectedFiles) {

        const ext = file.name.substring(file.name.lastIndexOf(".")).toLowerCase();

        if (!ALLOWED_EXT.includes(ext)) {
            alert(`${file.name} is not allowed (PDF, JPG, PNG only).`);
            return false;
        }

        if (file.size > MAX_FILE_MB * 1024 * 1024) {
            alert(`${file.name} is larger than ${MAX_FILE_MB} MB.`);
            return false;
        }
    }

    return true;
}

async function uploadFiles(trid) {

    if (selectedFiles.length === 0) return;

    const formData = new FormData();

    for (const file of selectedFiles) {
        formData.append("files", file);
    }

    // ไม่ตั้ง Content-Type เอง ให้ browser ใส่ boundary ให้
    const response = await authFetch(
        `${API_URL}/applications/upload/${encodeURIComponent(trid)}`,
        { method: "POST", body: formData });

    if (!response.ok) {
        throw new Error(errorText(await readBody(response), "Upload failed"));
    }
}

function renderNewFileList() {

    const list = document.getElementById("newFileList");

    if (selectedFiles.length === 0) {
        list.innerHTML = "";
        return;
    }

    list.innerHTML = selectedFiles.map((f, i) => `
        <li style="display:flex; align-items:center; gap:10px;
                   padding:6px 10px; margin-bottom:6px;
                   background:#f8fafc; border-radius:6px;">
            <span style="flex:1">
                📎 ${escapeHtml(f.name)}
                <small style="color:#64748b">
                    (${(f.size / 1024 / 1024).toFixed(2)} MB)
                </small>
            </span>
           <button type="button" class="btn-danger"
        style="background:#dc2626; color:#fff; border:1px solid #dc2626;
               padding:6px 14px; border-radius:6px; cursor:pointer; font-weight:500;"
        onclick="removeNewFile(${i})">
    Remove
</button>
        </li>
    `).join("");
}

function removeNewFile(index) {
    selectedFiles.splice(index, 1);
    renderNewFileList();
}

document.addEventListener("DOMContentLoaded", () => {

    const input = document.getElementById("files");

    document.getElementById("btnPickFiles")
        .addEventListener("click", () => input.click());

    // เปิดไฟล์เดิมผ่าน API (event delegation)
    document.getElementById("fileBody")
        .addEventListener("click", e => {
            const a = e.target.closest("[data-file]");
            if (!a) return;
            e.preventDefault();
            openFile(a.dataset.file);
        });

    input.addEventListener("change", function () {

        for (const f of this.files) {

            // กันเลือกไฟล์ซ้ำ (ชื่อ + ขนาด + วันที่แก้ไขเหมือนกัน)
            const dup = selectedFiles.some(x =>
                x.name === f.name &&
                x.size === f.size &&
                x.lastModified === f.lastModified);

            if (!dup) selectedFiles.push(f);
        }

        // รีเซ็ต input เพื่อให้เลือกไฟล์ชื่อเดิมซ้ำได้ และ change event ทำงานทุกครั้ง
        this.value = "";

        renderNewFileList();
    });
});

function addProductRow(
    productname = "",
    modelname = "",
    description = "",
    readOnly = isLocked
) {
    const ro = readOnly ? "readonly" : "";

    const row = `
        <tr>
            <td>
                <input type="text" class="productname"
                       value="${escapeHtml(productname)}" ${ro}>
            </td>
            <td>
                <input type="text" class="modelname"
                       value="${escapeHtml(modelname)}" ${ro}>
            </td>
            <td>
                <textarea class="description" rows="1" ${ro}>${escapeHtml(description)}</textarea>
            </td>
            ${readOnly ? "" : `
            <td>
                <button type="button" class="btn-danger"
                        onclick="this.closest('tr').remove()">
                    Remove
                </button>
            </td>`}
        </tr>`;

    const body = document.getElementById("productBody");
    body.insertAdjacentHTML("beforeend", row);

    // ปรับความสูงของแถวที่เพิ่งเพิ่ม ให้พอดีกับข้อความเดิม
    autoResize(body.lastElementChild.querySelector("textarea.description"));
}
function autoResize(el) {
    if (!el) return;
    el.style.height = "auto";
    el.style.height = el.scrollHeight + "px";
}

// ปรับความสูงทุกครั้งที่พิมพ์ (ผูกครั้งเดียวที่ตัว tbody)
document.addEventListener("input", e => {
    if (e.target.matches("textarea.description")) {
        autoResize(e.target);
    }
});

async function saveEdit() {

    if (!validateEdit()) return;
    if (!validateFiles()) return;

    const products = [];

    if (!isLocked) {
        document.querySelectorAll("#productBody tr").forEach(row => {
            products.push({
                productname: row.querySelector(".productname").value.trim(),
                modelname: row.querySelector(".modelname").value.trim(),
                description: row.querySelector(".description").value.trim()
            });
        });
    }

    // ส่งเฉพาะข้อมูลที่แก้ไขได้ ตัวตนเจ้าของคำขอ API ตรวจจาก Session
    const request = {
        email: document.getElementById("email").value.trim(),
        tel: document.getElementById("tel").value.trim(),
        products: products
    };

    try {

        // 1) บันทึกข้อมูล (ฝั่ง backend บวก VersionNo)
        const response = await authFetch(
            `${API_URL}/applications/traderedit/${encodeURIComponent(trid)}`,
            {
                method: "PUT",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(request)
            });

        if (!response.ok) {
            throw new Error(errorText(await readBody(response), "Save failed"));
        }

        // 2) อัปโหลดไฟล์ใหม่ (เก็บเป็น _V{version ใหม่})
        await uploadFiles(trid);

        alert("Save Success");
        location.href = "applications.html";
    }
    catch (error) {
        console.error(error);
        alert("Save Error: " + error.message);
    }
}

function validateEdit() {

    // Type 3, 4: แก้ได้เฉพาะไฟล์ ต้องแนบอย่างน้อย 1 ไฟล์
    if (isLocked) {
        if (selectedFiles.length === 0) {
            alert("Please attach at least one new file.");
            return false;
        }
        return true;
    }

    const email = document.getElementById("email").value.trim();
    const tel = document.getElementById("tel").value.trim();

    if (!email) { alert("Please enter Email."); return false; }

    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
        alert("Invalid Email format.");
        return false;
    }

    if (!tel) { alert("Please enter Telephone Number."); return false; }

    // ให้ตรงกับ API: ขึ้นต้นด้วย 0 และยาว 9-12 หลัก
    if (!/^0\d{8,11}$/.test(tel)) {
        alert("Telephone must be 9-11 digits and start with 0.");
        return false;
    }

    const rows = document.querySelectorAll("#productBody tr");

    if (rows.length === 0) {
        alert("Please add at least one product.");
        return false;
    }

    for (let i = 0; i < rows.length; i++) {
        const get = s => rows[i].querySelector(s)?.value.trim();

        if (!get(".productname")) { alert(`Row ${i + 1}: Product Name is required.`); return false; }
        if (!get(".modelname")) { alert(`Row ${i + 1}: Model Name is required.`); return false; }
        if (!get(".description")) { alert(`Row ${i + 1}: Description is required.`); return false; }
    }

    return true;
}

function rowValue(row, selector) {

    const element =
        row.querySelector(selector);

    return element
        ? element.value.trim()
        : "";
}
