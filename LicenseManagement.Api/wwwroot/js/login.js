const PASSWORD_RULE = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$/;

const $ = id => document.getElementById(id);

$("year").textContent = new Date().getFullYear();


// ========================================
// HELPERS
// ========================================

function showMessage(el, text, type = "error") {
    el.textContent = text;
    el.classList.toggle("success", type === "success");
    el.classList.add("show");
}

function clearMessages() {
    ["loginMessage", "changeMessage"]
        .forEach(id => $(id).classList.remove("show"));
}

async function readBody(response) {
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

    return result?.message
        || result?.title
        || fallback;
}

function resetPasswordFields() {

    document
        .querySelectorAll(".toggle-password")
        .forEach(btn => {

            $(btn.dataset.target).type = "password";
            btn.textContent = "Show";
        });

    [
        "password",
        "oldPassword",
        "newPassword",
        "confirmPassword"
    ]
        .forEach(id => $(id).value = "");
}

function showView(name) {

    clearMessages();
    resetPasswordFields();

    $("loginView")
        .classList
        .toggle("hidden", name !== "login");

    $("changeView")
        .classList
        .toggle("hidden", name !== "change");

    $(name === "login"
        ? "username"
        : "cpUsername")
        .focus();
}

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


// ========================================
// EVENTS
// ========================================

document
    .querySelectorAll(".toggle-password")
    .forEach(btn => {

        btn.addEventListener("click", () => {

            const input =
                $(btn.dataset.target);

            const show =
                input.type === "password";

            input.type =
                show
                    ? "text"
                    : "password";

            btn.textContent =
                show
                    ? "Hide"
                    : "Show";
        });
    });

$("showChange")
    .addEventListener("click", () => {

        const typed =
            $("username")
                .value
                .trim();

        showView("change");

        $("cpUsername").value = typed;

        if (typed)
            $("oldPassword").focus();
    });

$("showLogin")
    .addEventListener(
        "click",
        () => showView("login")
    );

document
    .querySelectorAll(".demo-btn")
    .forEach(btn =>
        btn.addEventListener(
            "click",
            () => {

                $("username").value =
                    btn.dataset.user;

                $("password").value =
                    btn.dataset.pass;

                login();
            }));

$("loginForm")
    .addEventListener(
        "submit",
        e => {

            e.preventDefault();
            login();
        });

$("changeForm")
    .addEventListener(
        "submit",
        e => {

            e.preventDefault();
            changePassword();
        });


// ========================================
// LOGIN
// ========================================

async function login() {

    const username =
        $("username")
            .value
            .trim();

    const password =
        $("password")
            .value;

    const message =
        $("loginMessage");

    const button =
        $("loginButton");

    clearMessages();

    if (!username || !password) {

        showMessage(
            message,
            "Please enter username and password."
        );

        return;
    }

    try {

        button.disabled = true;
        button.textContent = "Signing in...";

        const response =
            await fetch(
                `${API_URL}/account/login`,
                {
                    method: "POST",

                    headers: {
                        "Content-Type": "application/json"
                    },

                    body: JSON.stringify({
                        username,
                        password
                    })
                });

        const result =
            await readBody(response);

        if (response.ok) {

            localStorage.clear();

            localStorage.setItem(
                "sessionToken",
                result.sessionToken
            );

            localStorage.setItem(
                "userId",
                result.ida
            );

            localStorage.setItem(
                "username",
                result.username
            );

            localStorage.setItem(
                "fullName",
                result.fullname
            );

            localStorage.setItem(
                "companyName",
                result.companyname ?? ""
            );

            localStorage.setItem(
                "email",
                result.email ?? ""
            );

            localStorage.setItem(
                "tel",
                result.tel ?? ""
            );

            localStorage.setItem(
                "userType",
                result.usertype ?? ""
            );

            window.location.href =
                "/applications.html";
        }
        else {

            showMessage(
                message,
                errorText(
                    result,
                    "Invalid username or password."
                )
            );
        }

    }
    catch (error) {

        console.error(
            "Login Error:",
            error
        );

        showMessage(
            message,
            "Unable to connect to the server. Please try again."
        );
    }
    finally {

        button.disabled = false;
        button.textContent = "Sign In";
    }
}


// ========================================
// CHANGE PASSWORD
// ========================================

async function changePassword() {

    const username =
        $("cpUsername")
            .value
            .trim();

    const oldPassword =
        $("oldPassword")
            .value;

    const newPassword =
        $("newPassword")
            .value;

    const confirmPassword =
        $("confirmPassword")
            .value;

    const message =
        $("changeMessage");

    const button =
        $("changeButton");

    clearMessages();

    if (
        !username ||
        !oldPassword ||
        !newPassword ||
        !confirmPassword
    ) {

        showMessage(
            message,
            "Please fill in all fields."
        );

        return;
    }

    if (!PASSWORD_RULE.test(newPassword)) {

        showMessage(
            message,
            "New password must be at least 8 characters with uppercase, lowercase and a number."
        );

        return;
    }

    if (newPassword !== confirmPassword) {

        showMessage(
            message,
            "New password and confirmation do not match."
        );

        return;
    }

    if (newPassword === oldPassword) {

        showMessage(
            message,
            "New password must be different from the current password."
        );

        return;
    }

    try {

        button.disabled = true;
        button.textContent = "Saving...";

        const response =
            await fetch(
                `${API_URL}/account/ChangePasswordByUsername`,
                {
                    method: "PUT",
                    headers: {
                        "Content-Type": "application/json"
                    },
                    body: JSON.stringify({
                        username,
                        oldPassword,
                        newPassword
                    })
                });

        const result =
            await readBody(response);

        if (response.ok) {

            showView("login");

            $("username").value =
                username;

            showMessage(
                $("loginMessage"),
                "Password changed. Sign in with your new password.",
                "success"
            );

            $("password").focus();
        }
        else {

            showMessage(
                message,
                errorText(
                    result,
                    "Unable to change password."
                )
            );
        }

    }
    catch (error) {

        console.error(
            "Change Password Error:",
            error
        );

        showMessage(
            message,
            "Unable to connect to the server. Please try again."
        );
    }
    finally {

        button.disabled = false;
        button.textContent =
            "Change password";
    }
}


// ========================================
// logout
// ========================================

async function logout() {
    try {
        await fetch(`${API_URL}/account/logout`, {
            method: "POST",
            headers: authHeaders()
        });
    } catch (error) {
        console.error("Logout Error:", error);
    } finally {
        localStorage.clear();
        window.location.href = "/login.html";
    }
}