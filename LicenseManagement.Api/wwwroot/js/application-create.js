// =====================================
// ค่าคงที่ ตัวแปร และ helper
// =====================================
const MAX_FILES = 10;
const MAX_FILE_SIZE = 10 * 1024 * 1024;           // 10 MB (ให้ตรงกับ API)
const ALLOWED_EXT = [".pdf", ".jpg", ".jpeg", ".png"];

const TYPE_HELP = {
    1: "Register new products and apply for a first license.",
    2: "Change the products on an approved license. A new license number is issued and the old one is replaced.",
    3: "Renew a license that expires within 3 months. Products are copied from the original license.",
    4: "Cancel an approved license. Products are copied from the original license."
};

const NO_LICENSE_TEXT = {
    2: "No license can be amended right now. A license that already has a pending amendment, renewal or cancel request can't be selected.",
    3: "No license is due for renewal. Only licenses that expire within 3 months can be renewed.",
    4: "No license can be cancelled right now. A license that already has a pending amendment, renewal or cancel request can't be selected."
};

let selectedLicenseData = null;
let isProductReadOnly = false;
let typeRequestId = 0;
let pendingFiles = [];        // { file, name, ext }  name = ชื่อที่ผู้ใช้ตั้ง (ไม่รวมนามสกุล)
let busy = false;

const $ = id => document.getElementById(id);

const show = id => {
    const el = $(id);
    el.style.display = "";
    el.classList.remove("hidden");
};
const hide = id => $(id).classList.add("hidden");

// =====================================
// AUTHENTICATION (ตรวจกับ Server ทุกครั้ง ไม่เชื่อค่าใน localStorage)
// =====================================
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
        showMessage("Unable to verify your session. Please try again.");
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
        showMessage(error.message || "Unable to open the file.");
    }
}

function showMessage(text, type = "error", link = null) {
    const box = $("pageMessage");
    box.className = "page-message " + type;
    box.textContent = text;
    if (link) {
        box.append(" ");
        const a = document.createElement("a");
        a.href = link.href;
        a.textContent = link.text;
        box.appendChild(a);
    }
    box.scrollIntoView({ behavior: "smooth", block: "center" });
}

function clearMessage() {
    $("pageMessage").className = "page-message hidden";
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

function setButtons(enabled) {
    $("btnCreate").disabled = !enabled;
    $("btnSubmit").disabled = !enabled;
}

// =====================================
// เริ่มต้นหน้า
// =====================================
async function loadProfile(session) {
    // แสดงผลอย่างเดียว ข้อมูลผู้สมัครจริงที่บันทึกใช้จาก Session ฝั่ง API
    let profile = null;

    try {
        const response = await authFetch(
            `${API_URL}/account/GetUserId?id=${encodeURIComponent(session.ida)}`);
        if (response.ok) profile = await response.json();
    }
    catch (error) {
        console.error("Load Profile Error:", error);
    }

    const p = profile || session;

    $("identify").value = p.identify ?? "";
    $("companyname").value = p.companyname ?? "";
    $("citizenid").value = p.citizenid ?? "";
    $("citizenname").value = p.fullname ?? "";
    $("tel").value = p.tel ?? "";
    $("email").value = p.email ?? "";
}

window.onload = async function () {

    const session = await requireSession();
    if (!session) return;

    // หน้านี้สำหรับ Trader เท่านั้น (ตรวจจาก Server)
    if (session.usertype !== "Trader") {
        window.location.replace("applications.html");
        return;
    }

    await loadProfile(session);

    $("files").addEventListener("change", e => {
        addFiles([...e.target.files]);     // คัดลอกก่อน เพราะ FileList จะว่างเมื่อรีเซ็ต input
        e.target.value = "";               // เลือกไฟล์เดิมซ้ำได้
    });

    renderPendingFiles();
    handleTypeChange();
};

// =====================================
// ซ่อน/แสดงส่วนต่างๆ
// =====================================
function hideAllSections() {
    hide("originalLicenseArea");
    hide("originalFilesArea");
    hide("productInformationArea");
    hide("newFileArea");
    updateSupportingCard();
}

function updateSupportingCard() {
    const empty =
        $("originalFilesArea").classList.contains("hidden") &&
        $("newFileArea").classList.contains("hidden");
    $("supportingDocumentsArea").classList.toggle("hidden", empty);
}

function resetOriginalLicenseView() {
    selectedLicenseData = null;
    hideAllSections();

    ["originalLicenseNumber", "originalLicenseType",
        "originalApprovalDate", "originalExpirationDate"]
        .forEach(id => $(id).value = "");

    $("originalFileList").innerHTML = "";
    $("selectedLicenseInfo").textContent = "";
}

// =====================================
// 1. เมื่อเปลี่ยน Type
// =====================================
async function handleTypeChange() {
    const type = Number($("type").value);
    const row = $("referenceLicenseRow");
    const select = $("referenceLicenseNumber");

    typeRequestId++;
    resetOriginalLicenseView();
    clearMessage();

    $("typeHelp").textContent = TYPE_HELP[type] || "";
    $("licenseMessage").textContent = "";
    $("licenseLoading").style.display = "none";
    select.innerHTML = '<option value="">-- Select License Number --</option>';
    select.disabled = true;
    setButtons(false);
    row.style.display = "none";
    setProductMode([], true);

    if (!type) return;

    if (type === 1) {
        setProductMode([], false);
        show("productInformationArea");
        show("newFileArea");
        updateSupportingCard();
        updateCreateButton();
        return;
    }

    row.style.display = "table-row";
    await loadEligibleLicenses(type);
}

// =====================================
// 2. โหลด License ที่เลือกได้
// (API ใช้เจ้าของจาก Session เอง ไม่ต้องส่ง identify / citizenid)
// =====================================
async function loadEligibleLicenses(type) {
    const myRequest = typeRequestId;
    const select = $("referenceLicenseNumber");
    const message = $("licenseMessage");
    const loading = $("licenseLoading");

    setButtons(false);
    select.disabled = true;
    loading.style.display = "block";

    try {
        const response = await authFetch(
            `${API_URL}/applications/EligibleLicenses?type=${type}`
        );
        const result = await readBody(response);

        if (myRequest !== typeRequestId) return;

        if (!response.ok) {
            throw new Error(errorText(result, "Unable to load licenses."));
        }

        const licenses = Array.isArray(result) ? result : (result.licenses || []);
        const validLicenses = licenses.filter(item => {
            const no = item.licensenumber || item.licenseNumber;
            return no && String(no).trim() !== "";
        });

        if (validLicenses.length === 0) {
            message.textContent = NO_LICENSE_TEXT[type];
            select.innerHTML = '<option value="">No eligible license found</option>';
            return;
        }

        validLicenses.forEach(item => {
            const no = item.licensenumber || item.licenseNumber;
            const exp = item.dateexp || item.dateExp;

            const option = document.createElement("option");
            option.value = String(no).trim();
            option.textContent = no + (exp ? " (Expiry: " + formatLicenseDate(exp) + ")" : "");
            select.appendChild(option);
        });

        select.disabled = false;
        message.textContent = "Please select a license.";
    }
    catch (error) {
        if (myRequest !== typeRequestId) return;
        console.error("Load Eligible Licenses Error:", error);
        message.textContent = error.message;
    }
    finally {
        if (myRequest === typeRequestId) loading.style.display = "none";
    }
}

// =====================================
// 3. เลือก Reference License
// =====================================
async function onReferenceLicenseChange() {
    const select = $("referenceLicenseNumber");
    const type = Number($("type").value);
    const myRequest = typeRequestId;

    resetOriginalLicenseView();
    setProductMode([], true);
    setButtons(false);
    clearMessage();

    if (!select.value) return;

    $("selectedLicenseInfo").textContent = "Selected License Number: " + select.value;

    try {
        await loadOriginalLicense(select.value);

        if (myRequest !== typeRequestId) return;

        // Type 2 แก้ไข Product ได้ / Type 3,4 อ่านอย่างเดียว
        const readOnly = (type === 3 || type === 4);
        setProductMode(selectedLicenseData.products || [], readOnly);

        show("originalLicenseArea");
        show("productInformationArea");
        show("originalFilesArea");
        show("newFileArea");
        updateSupportingCard();
        updateCreateButton();
    }
    catch (error) {
        console.error("Reference License Error:", error);
        resetOriginalLicenseView();
        setButtons(false);
        showMessage(error.message);
    }
}

async function loadOriginalLicense(licenseNumber) {
    const response = await authFetch(
        `${API_URL}/applications/LicenseDetail/${encodeURIComponent(licenseNumber)}`
    );
    if (!response.ok) throw new Error("Cannot load original license.");

    const result = await response.json();
    if (!result.licenseNumber) throw new Error("Invalid license information.");

    selectedLicenseData = result;

    setText("originalLicenseNumber", result.licenseNumber);
    setText("originalLicenseType", result.licenseType);
    setText("originalApprovalDate", formatLicenseDate(result.dateappv));
    setText("originalExpirationDate", formatLicenseDate(result.dateexp));

    displayOriginalFiles(result.files || []);
}

function setText(id, value) {
    const element = $(id);
    if (element) element.value = value ?? "";
}

function updateCreateButton() {
    const type = Number($("type").value);

    if (!type) { setButtons(false); return; }
    if (type === 1) { setButtons(true); return; }

    const select = $("referenceLicenseNumber");
    setButtons(!!(
        selectedLicenseData &&
        select.value.trim() !== "" &&
        selectedLicenseData.licenseNumber === select.value
    ));
}

function formatLicenseDate(date) {
    if (!date) return "";
    return new Date(date).toLocaleDateString("en-GB");
}

// =====================================
// 4. Product table
// =====================================
function setProductMode(products, readOnly) {
    const body = $("productBody");

    body.innerHTML = "";
    isProductReadOnly = readOnly;

    $("btnAddProduct").style.display = readOnly ? "none" : "inline-block";
    $("productActionHeader").style.display = readOnly ? "none" : "table-cell";

    if (!products || products.length === 0) {
        if (!readOnly) {
            addProductRow(null, false);
        } else {
            body.innerHTML = `<tr><td colspan="3" style="text-align:center;">No product information found.</td></tr>`;
        }
        return;
    }

    products.forEach(p => addProductRow(p, readOnly));
}

function addProductRow(product = null, readOnly = isProductReadOnly) {
    if (readOnly && !product) return;

    const tr = document.createElement("tr");
    tr.className = "product-row";

    const productName = document.createElement("input");
    productName.type = "text";
    productName.className = "product-name";
    productName.placeholder = "Product Name";
    productName.setAttribute("aria-label", "Product Name");
    productName.value = product?.productname || "";
    productName.readOnly = readOnly;

    const modelName = document.createElement("input");
    modelName.type = "text";
    modelName.className = "model-name";
    modelName.placeholder = "Model Name";
    modelName.setAttribute("aria-label", "Model Name");
    modelName.value = product?.modelname || "";
    modelName.readOnly = readOnly;

    const description = document.createElement("textarea");
    description.className = "description";
    description.placeholder = "Description";
    description.setAttribute("aria-label", "Description");
    description.value = product?.description || "";
    description.readOnly = readOnly;

    const td1 = document.createElement("td");
    const td2 = document.createElement("td");
    const td3 = document.createElement("td");
    td1.appendChild(productName);
    td2.appendChild(modelName);
    td3.appendChild(description);
    tr.append(td1, td2, td3);

    if (!readOnly) {
        const td4 = document.createElement("td");
        td4.style.textAlign = "center";

        const del = document.createElement("button");
        del.type = "button";
        del.className = "btn-danger";
        del.textContent = "Delete";
        del.onclick = () => tr.remove();

        td4.appendChild(del);
        tr.appendChild(td4);
    }

    $("productBody").appendChild(tr);
}

// =====================================
// 5. ไฟล์แนบ (หลายไฟล์ ตั้งชื่อเองได้)
// =====================================
function splitName(fileName) {
    const i = fileName.lastIndexOf(".");
    return i > 0 ? [fileName.slice(0, i), fileName.slice(i).toLowerCase()] : [fileName, ""];
}

// ตัดอักขระที่ใช้ในชื่อไฟล์ไม่ได้ และจำกัดความยาว
function cleanName(s) {
    return s.replace(/[\\/:*?"<>|]/g, "").replace(/\s+/g, " ").trim().replace(/^\.+/, "").slice(0, 100);
}

const finalName = item => cleanName(item.name) + item.ext;

function formatSize(bytes) {
    return bytes < 1024 * 1024
        ? Math.max(1, Math.round(bytes / 1024)) + " KB"
        : (bytes / 1024 / 1024).toFixed(1) + " MB";
}

function addFiles(fileList) {
    const problems = [];

    for (const file of fileList) {
        const [stem, ext] = splitName(file.name);

        if (pendingFiles.length >= MAX_FILES) {
            problems.push(`You can attach up to ${MAX_FILES} files.`);
            break;
        }
        if (!ALLOWED_EXT.includes(ext)) {
            problems.push(`${file.name}: only PDF, JPG, JPEG and PNG files are allowed.`);
            continue;
        }
        if (file.size === 0) {
            problems.push(`${file.name}: the file is empty.`);
            continue;
        }
        if (file.size > MAX_FILE_SIZE) {
            problems.push(`${file.name}: the file is larger than 10 MB.`);
            continue;
        }
        pendingFiles.push({ file, name: cleanName(stem), ext });
    }

    renderPendingFiles();
    if (problems.length) showMessage(problems.join("\n"));
}

function renderPendingFiles() {
    const list = $("pendingFileList");
    list.innerHTML = "";

    pendingFiles.forEach((item, i) => {
        const row = document.createElement("div");
        row.className = "file-row";

        const input = document.createElement("input");
        input.type = "text";
        input.value = item.name;
        input.maxLength = 100;
        input.setAttribute("aria-label", `Name for file ${i + 1}`);
        input.addEventListener("input", () => { item.name = input.value; });

        const ext = document.createElement("span");
        ext.className = "file-ext";
        ext.textContent = item.ext;

        const size = document.createElement("span");
        size.className = "file-size";
        size.textContent = formatSize(item.file.size);

        const del = document.createElement("button");
        del.type = "button";
        del.className = "btn-danger";
        del.textContent = "Remove";
        del.onclick = () => {
            pendingFiles.splice(pendingFiles.indexOf(item), 1);
            renderPendingFiles();
        };

        row.append(input, ext, size, del);
        list.appendChild(row);
    });

    $("fileCount").textContent = pendingFiles.length
        ? `${pendingFiles.length} file(s) attached`
        : "No files attached.";
}

function validateFileNames() {
    const seen = new Set();

    for (const item of pendingFiles) {
        if (!cleanName(item.name)) return "Please enter a name for every attached file.";

        const key = finalName(item).toLowerCase();
        if (seen.has(key)) {
            return `Two attached files are named "${finalName(item)}". Give each file a different name.`;
        }
        seen.add(key);
    }
    return null;
}

function displayOriginalFiles(files) {
    const list = $("originalFileList");
    list.innerHTML = "";

    if (!files || files.length === 0) {
        list.textContent = "No original documents found.";
        return;
    }

    files.forEach(file => {
        const div = document.createElement("div");
        div.style.padding = "10px";
        div.style.borderBottom = "1px solid #e5e7eb";

        const link = document.createElement("a");
        link.textContent = file.fileName;
        link.href = "#";

        // เปิดผ่าน API ที่ตรวจ Session และสิทธิ์
        link.addEventListener("click", e => {
            e.preventDefault();
            openFile(file.id);
        });

        div.appendChild(link);
        list.appendChild(div);
    });
}

async function uploadFiles(trid) {
    if (pendingFiles.length === 0) return;

    const formData = new FormData();
    pendingFiles.forEach(item => {
        formData.append("files", item.file);
        formData.append("names", finalName(item));     // ชื่อเรียงตามลำดับเดียวกับไฟล์
    });

    // ไม่ตั้ง Content-Type เอง ให้ browser ใส่ boundary ให้
    const response = await authFetch(
        `${API_URL}/applications/upload/${encodeURIComponent(trid)}`,
        { method: "POST", body: formData }
    );

    if (!response.ok) {
        throw new Error(errorText(await readBody(response), "Upload failed"));
    }
}

// =====================================
// 6. Validate
// =====================================
// คืนข้อความผิดพลาด หรือ null ถ้าผ่าน
function validateApplication() {
    if (!isProductReadOnly) {
        const rows = document.querySelectorAll("#productBody tr.product-row");

        if (rows.length === 0) return "Please add at least one product.";

        for (let i = 0; i < rows.length; i++) {
            if (!rows[i].querySelector(".product-name").value.trim()) return `Row ${i + 1}: Product Name is required.`;
            if (!rows[i].querySelector(".model-name").value.trim()) return `Row ${i + 1}: Model Name is required.`;
            if (!rows[i].querySelector(".description").value.trim()) return `Row ${i + 1}: Description is required.`;
        }
    }
    return validateFileNames();
}

// =====================================
// 7. Create Application
//    submitNow = false -> บันทึกเป็น Draft
//    submitNow = true  -> สร้าง + อัปโหลดไฟล์ + ส่งคำขอ
// =====================================
async function submitApplication(trid) {
    // ไม่ส่ง body: API ระบุตัวตนจาก Session
    const response = await authFetch(
        `${API_URL}/applications/Submitted/${encodeURIComponent(trid)}`,
        { method: "POST" }
    );
    if (!response.ok) {
        throw new Error(errorText(await readBody(response), "Submit failed"));
    }
}

async function Create(submitNow) {
    if (busy) return;
    clearMessage();

    const type = Number($("type").value);
    const referenceLicenseNumber = $("referenceLicenseNumber").value.trim();

    if (!type) { showMessage("Please select an application type."); return; }

    if (type !== 1 &&
        (!selectedLicenseData || selectedLicenseData.licenseNumber !== referenceLicenseNumber)) {
        showMessage("Please select and load a valid license.");
        return;
    }

    const problem = validateApplication();
    if (problem) { showMessage(problem); return; }

    if (submitNow && !confirm(
        "Submit this application to the officer now?\n" +
        "You can't edit it unless the officer asks for changes.")) return;

    // Renew / Cancel ไม่ส่ง Products (backend ดึงจาก License เดิมเอง)
    const products = [];
    if (!isProductReadOnly) {
        document.querySelectorAll("#productBody tr.product-row").forEach(row => {
            products.push({
                productname: row.querySelector(".product-name").value,
                modelname: row.querySelector(".model-name").value,
                description: row.querySelector(".description").value
            });
        });
    }

    // ส่งเฉพาะข้อมูลคำขอ ข้อมูลผู้สมัครและสถานะ API กำหนดเองจาก Session
    const request = {
        application: {
            type: type,
            referenceLicenseNumber: type === 1 ? null : referenceLicenseNumber
        },
        products: products
    };

    busy = true;
    setButtons(false);

    // ---------- ขั้นที่ 1: สร้าง Application ----------
    let trid;
    try {
        const response = await authFetch(`${API_URL}/applications`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(request)
        });
        const result = await readBody(response);

        if (!response.ok) {
            showMessage(errorText(result, "Unable to create the application."));
            busy = false;
            updateCreateButton();
            return;
        }
        trid = result.trid ?? result.Trid;
    }
    catch (error) {
        console.error("Create Application Error:", error);
        showMessage("Unable to connect to the server. Please try again.");
        busy = false;
        updateCreateButton();
        return;
    }

    // มาถึงตรงนี้ Application ถูกสร้างแล้ว ปุ่มจะไม่เปิดอีก (กันสร้างซ้ำ)
    const detail = {
        href: `application-detail.html?trid=${encodeURIComponent(trid)}`,
        text: "Open application"
    };

    // ---------- ขั้นที่ 2: อัปโหลดไฟล์ ----------
    try {
        await uploadFiles(trid);
    }
    catch (error) {
        console.error("Upload Files Error:", error);
        showMessage(
            `Application ${trid} was saved as a draft, but the files could not be uploaded (${error.message}). ` +
            `Open it to attach the files again.`, "error", detail);
        return;
    }

    // ---------- ขั้นที่ 3: ส่งคำขอ (เฉพาะ Submit) ----------
    if (submitNow) {
        try {
            await submitApplication(trid);
        }
        catch (error) {
            console.error("Submit Error:", error);
            showMessage(
                `Application ${trid} was saved as a draft, but could not be submitted (${error.message}). ` +
                `Open it to submit.`, "error", detail);
            return;
        }
    }

    showMessage(
        submitNow ? `Application ${trid} submitted.` : `Application ${trid} saved as a draft.`,
        "success");
    setTimeout(() => { location.href = detail.href; }, 1200);
}