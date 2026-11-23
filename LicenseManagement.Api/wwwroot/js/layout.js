document.addEventListener("DOMContentLoaded", initialize);

async function initialize() {
    const host = document.getElementById("navbar");
    if (!host) return;

    try {
        const response = await fetch("/partials/navbar.html");
        if (!response.ok) throw new Error(response.status);
        host.innerHTML = await response.text();
        renderMenu();
    } catch (error) {
        console.error("Load navbar error:", error);
    }
}

function renderMenu() {
    const menu = document.getElementById("menuSection");
    if (!menu) return;

    const username = localStorage.getItem("username");
    const identify = localStorage.getItem("identify");
    const fullName = localStorage.getItem("fullName") || username || "";
    const userType = localStorage.getItem("userType");
    const page = location.pathname.split("/").pop();

    menu.replaceChildren();

    const addLink = (text, href, cls) => {
        const a = document.createElement("a");
        a.textContent = text;
        a.href = href;
        a.className = "nav-btn " + cls;
        menu.appendChild(a);
    };

    // ยังไม่ล็อกอิน
    if (!username && !identify) {
        addLink("Login", "/login.html", "nav-btn-primary");
        addLink("Register", "/register.html", "nav-btn-secondary");
        return;
    }

    // ล็อกอินแล้ว: Signed in as officer/trader <ชื่อ>
    const who = document.createElement("span");
    who.className = "nav-user";
    who.append(userType === "Staff" ? "Signed in as officer " : "Signed in as trader ");
    const strong = document.createElement("strong");
    strong.textContent = fullName;          // textContent กัน XSS
    who.appendChild(strong);
    menu.appendChild(who);

    if (page !== "applications.html")
        addLink("Applications Main", "/applications.html", "nav-btn-primary"); 

    const out = document.createElement("button");
    out.type = "button";
    out.textContent = "Log out";
    out.className = "nav-btn nav-btn-secondary";
    out.addEventListener("click", () => {
        if (typeof logout === "function") logout();
        else { localStorage.clear(); location.href = "/login.html"; }
    });
    menu.appendChild(out);
}