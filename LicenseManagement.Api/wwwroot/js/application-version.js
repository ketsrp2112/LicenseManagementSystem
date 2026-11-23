const params = new URLSearchParams(window.location.search);

const trid = params.get("trid");
const version = params.get("version");

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

// เปิดไฟล์ของเวอร์ชันนี้ผ่าน API ที่ตรวจ Session และสิทธิ์ (ไม่เปิดตรงจาก /uploads)
async function openVersionFile(fileName) {
    const win = window.open("", "_blank");   // เปิดหน้าต่างทันที กัน popup blocker

    try {
        const response = await authFetch(
            `${API_URL}/applications/version-file-download/` +
            `${encodeURIComponent(trid)}/${encodeURIComponent(version)}/${encodeURIComponent(fileName)}`);

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

    if (!trid || !version) {
        alert("TRID or Version not found");
        return;
    }

    // Staff และ Trader ดูได้ แต่สิทธิ์ในคำขอแต่ละรายการ API ตรวจให้อีกชั้น
    const session = await requireSession();
    if (!session) return;

    if (session.usertype !== "Staff" && session.usertype !== "Trader") {
        handleUnauthorized();
        return;
    }

    setText("trid", trid);
    setText("versionBadge", `Version V${version}`);

    await Promise.all([
        loadVersion(),
        loadVersionList()
    ]);
};

// ========================================
// LOAD VERSION DATA
// ========================================

async function loadVersion() {

    try {

        const response = await authFetch(
            `${API_URL}/applications/versiondetail/${encodeURIComponent(trid)}/${encodeURIComponent(version)}`
        );

        if (!response.ok) {
            throw new Error(errorText(await readBody(response), "Cannot load version."));
        }

        const data = await response.json();

        // รองรับทั้ง { application, products, files } และ object แบบแบน
        const app = data.application || data.app || data;

        const files = data.files || data.Files || app.files || [];

        displayVersion(app);
        displayProducts(data.products || []);
        displayFiles(files);

        // เทียบกับเวอร์ชันปัจจุบัน เพื่อไฮไลต์ field ที่ต่างกัน
        await highlightChanges(app);

    }
    catch (error) {

        console.error(error);
        alert("Load Error: " + error.message);

    }
}

// ========================================
// DISPLAY
// ========================================

function pick(obj, ...keys) {

    for (const k of keys) {
        if (obj[k] !== undefined && obj[k] !== null) return obj[k];
    }

    return "";
}

function displayVersion(app) {

    setText("identify", pick(app, "identify", "IDENTIFY", "Identify"));
    setText("companyname", pick(app, "companyname", "companyName", "COMPANYNAME"));
    setText("citizenname", pick(app, "citizenname", "citizenName", "CITIZENNAME"));
    setText("citizenid", pick(app, "citizenid", "citizenId", "CITIZENID"));
    setText("licensenumber", pick(app, "licensenumber", "licenseNumber", "LICENSENUMBER"));
    setText("typenm", pick(app, "typenm", "typeNm", "TYPENM", "type"));
    setText("tel", pick(app, "tel", "TEL", "Tel"));
    setText("email", pick(app, "email", "EMAIL", "Email"));
    setText("remark", pick(app, "remark", "REMARK", "Remark"));

    const created = pick(app, "createDate", "createdate", "CREATEDATE");

    setText("createdate",
        created ? new Date(created).toLocaleString() : "");
}

function displayProducts(products) {

    const body = document.getElementById("productBody");

    body.innerHTML = "";

    if (!products || products.length === 0) {

        body.innerHTML =
            `<tr><td colspan="4">No products</td></tr>`;

        return;
    }

    products.forEach((p, index) => {

        const row = document.createElement("tr");

        const values = [
            index + 1,
            pick(p, "productname", "productName", "PRODUCTNAME"),
            pick(p, "modelname", "modelName", "MODELNAME"),
            pick(p, "description", "DESCRIPTION", "Description")
        ];

        values.forEach(v => {

            const td = document.createElement("td");
            td.textContent = v;
            row.appendChild(td);

        });

        body.appendChild(row);
    });
}

function displayFiles(files) {

    const body = document.getElementById("fileBody");

    body.innerHTML = "";

    if (!files || files.length === 0) {

        body.innerHTML =
            `<tr><td colspan="3">No files in this version</td></tr>`;

        return;
    }

    files.forEach((f, index) => {

        // API ระบุไฟล์ของเวอร์ชันด้วย trid + version + fileName
        const displayName = String(pick(f, "fileName", "FileName"));

        const row = document.createElement("tr");

        const tdNo = document.createElement("td");
        tdNo.textContent = index + 1;

        const tdName = document.createElement("td");
        tdName.textContent = "📎 " + displayName;

        const tdAction = document.createElement("td");
        const link = document.createElement("a");
        link.href = "#";
        link.textContent = "View";
        link.addEventListener("click", e => {
            e.preventDefault();
            openVersionFile(displayName);
        });
        tdAction.appendChild(link);

        row.append(tdNo, tdName, tdAction);

        body.appendChild(row);
    });
}

// ========================================
// VERSION LIST (dropdown) + NAVIGATION
// ========================================

async function loadVersionList() {

    try {

        const response = await authFetch(
            `${API_URL}/applications/VersionHistory/${encodeURIComponent(trid)}`
        );

        if (!response.ok) return;

        const versions = await response.json();

        const select = document.getElementById("versionSelect");

        select.innerHTML = "";

        versions.forEach(v => {

            const option = document.createElement("option");

            option.value = v.versionNo;

            option.textContent =
                `V${v.versionNo} - ${new Date(v.createDate).toLocaleString()}`;

            if (String(v.versionNo) === String(version)) {
                option.selected = true;
            }

            select.appendChild(option);
        });

    }
    catch (error) {

        console.error(error);

    }
}

function goVersion(v) {

    location.href =
        `application-version.html?trid=${encodeURIComponent(trid)}&version=${encodeURIComponent(v)}`;
}

function backToDetail() {

    location.href =
        `application-detail.html?trid=${encodeURIComponent(trid)}`;
}

// ========================================
// HIGHLIGHT CHANGES (เทียบกับเวอร์ชันปัจจุบัน)
// ========================================

async function highlightChanges(oldApp) {

    try {

        const response = await authFetch(
            `${API_URL}/applications/Detail/${encodeURIComponent(trid)}`
        );

        if (!response.ok) return;

        const current = (await response.json()).application;

        const fields = {
            tel: ["tel", "TEL"],
            email: ["email", "EMAIL"],
            remark: ["remark", "REMARK"],
            typenm: ["typenm", "TYPENM"]
        };

        Object.entries(fields).forEach(([id, keys]) => {

            const oldValue = String(pick(oldApp, ...keys) ?? "");
            const newValue = String(pick(current, ...keys) ?? "");

            if (oldValue !== newValue) {
                document.getElementById(id)
                    .closest(".info-item")
                    .classList.add("changed");
            }
        });

    }
    catch (error) {

        console.error(error);

    }
}

// ========================================
// HELPER
// ========================================

function setText(id, value) {

    const el = document.getElementById(id);

    if (el) el.textContent = value ?? "";
}