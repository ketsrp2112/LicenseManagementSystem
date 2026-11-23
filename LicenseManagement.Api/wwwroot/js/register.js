const $ = id => document.getElementById(id);

const RULES = {
    password: /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$/,
    name: /^[A-Za-zก-๙\s.'-]+$/,
    digits13: /^\d{13}$/,
    tel: /^0\d{8,11}$/,                 // เบอร์ไทย 9-11 หลัก ขึ้นต้นด้วย 0
    email: /^[^\s@]+@[^\s@]+\.[^\s@]+$/
};

$("year").textContent = new Date().getFullYear();

// ========================================
// HELPERS
// ========================================
function showMessage(text, type = "error") {
    const el = $("registerMessage");
    el.textContent = text;
    el.classList.toggle("success", type === "success");
    el.classList.add("show");
}

function clearErrors() {
    $("registerMessage").classList.remove("show");
    document.querySelectorAll(".form-control").forEach(i => i.removeAttribute("aria-invalid"));
}

// API ตอบได้ทั้ง JSON ({ message }) และข้อความธรรมดา
async function readBody(response) {
    const text = await response.text();
    try { return JSON.parse(text); } catch { return text; }
}

function errorText(result, fallback) {
    if (typeof result === "string" && result.trim()) return result;
    return (result && (result.message || result.Message || result.title)) || fallback;
}

function getData() {
    return {
        username: $("username").value.trim(),
        password: $("password").value,
        fullname: $("fullname").value.trim(),
        citizenid: $("citizenId").value.trim(),
        identify: $("identify").value.trim(),
        companyname: $("companyName").value.trim(),
        tel: $("tel").value.trim(),
        email: $("email").value.trim()
        // ไม่ส่ง usertype: server กำหนดเป็น Trader เอง
    };
}

// คืน [idของช่อง, ข้อความ] ถ้าไม่ผ่าน หรือ null ถ้าผ่าน
function validate(d, confirm) {
    if (!d.fullname) return ["fullname", "Please enter your full name."];
    if (!RULES.name.test(d.fullname)) return ["fullname", "Full name can contain letters, spaces, and . ' - only."];
    if (!d.username) return ["username", "Please enter a username."];
    if (!d.password) return ["password", "Please enter a password."];
    if (!RULES.password.test(d.password)) return ["password", "Password must be at least 8 characters with uppercase, lowercase and a number."];
    if (d.password !== confirm) return ["confirmPassword", "Passwords do not match."];
    if (!RULES.digits13.test(d.citizenid)) return ["citizenId", "Citizen ID must be exactly 13 digits."];
    if (!RULES.digits13.test(d.identify)) return ["identify", "Identify must be exactly 13 digits."];
    if (!d.companyname) return ["companyName", "Please enter a company name."];
    if (!RULES.tel.test(d.tel)) return ["tel", "Telephone must be 9-11 digits and start with 0."];
    if (!RULES.email.test(d.email)) return ["email", "Please enter a valid email address."];
    return null;
}

// ========================================
// SUBMIT
// ========================================
async function register() {
    clearErrors();

    const data = getData();
    const problem = validate(data, $("confirmPassword").value);
    if (problem) {
        const [field, text] = problem;
        $(field).setAttribute("aria-invalid", "true");
        showMessage(text);
        $(field).focus();
        return;
    }

    const button = $("registerButton");
    try {
        button.disabled = true;
        button.textContent = "Creating account...";

        const response = await fetch(`${API_URL}/account/register`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(data)
        });
        const result = await readBody(response);

        if (response.ok) {
            showMessage("Account created. Taking you to sign in...", "success");
            setTimeout(() => window.location.replace("login.html"), 1500);
            return;
        }
        showMessage(errorText(result, "Unable to create the account."));
    } catch (error) {
        console.error("Register Error:", error);
        showMessage("Unable to connect to the server. Please try again.");
    } finally {
        button.disabled = false;
        button.textContent = "Create account";
    }
}

// ========================================
// EVENTS
// ========================================
$("registerForm").addEventListener("submit", e => { e.preventDefault(); register(); });
$("registerForm").addEventListener("reset", clearErrors);

document.querySelectorAll(".toggle-password").forEach(btn =>
    btn.addEventListener("click", () => {
        const input = $(btn.dataset.target);
        const show = input.type === "password";
        input.type = show ? "text" : "password";
        btn.textContent = show ? "Hide" : "Show";
    }));

const digitsOnly = (id, max) => $(id).addEventListener("input", function () {
    this.value = this.value.replace(/\D/g, "").slice(0, max);
});
digitsOnly("citizenId", 13);
digitsOnly("identify", 13);
digitsOnly("tel", 11);

$("fullname").addEventListener("input", function () {
    this.value = this.value.replace(/[^A-Za-zก-๙\s.'-]/g, "");
});