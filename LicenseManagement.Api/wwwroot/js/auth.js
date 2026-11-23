// ========================================
// auth.js
// หมายเหตุ: ไฟล์นี้ถูกโหลดใน <head> ก่อน config.js จึงห้ามใช้ API_URL ตอนโหลดไฟล์
// (ใช้ได้เฉพาะภายในฟังก์ชันที่ถูกเรียกทีหลัง)
// ========================================

// ออกจากระบบ: ให้ Server ลบ Session ก่อน แล้วค่อยล้างข้อมูลในเครื่อง
// ถ้าแค่ลบ localStorage Session ใน DB จะยังใช้งานได้จนหมดอายุ
async function logout() {

    const token = localStorage.getItem("sessionToken");

    try {

        if (token && typeof API_URL !== "undefined") {

            await fetch(`${API_URL}/account/logout`, {
                method: "POST",
                headers: { "X-Session-Token": token }
            });
        }
    }
    catch (error) {

        console.error("Logout Error:", error);
    }
    finally {

        localStorage.clear();
        location.href = "/login.html";
    }
}

// ด่านแรกฝั่งหน้าเว็บ (UX เท่านั้น) แค่เช็กว่ามี token ไหม
// ความปลอดภัยจริงอยู่ที่ Server ที่ตรวจ Session ทุก API
function checkLogin() {

    if (!localStorage.getItem("sessionToken")) {

        location.replace("/login.html");
    }
}

// ถามสิทธิ์จาก Server (ไม่อ่าน userType จาก localStorage เพราะผู้ใช้แก้เองได้)
// คืนข้อมูลผู้ใช้ หรือ null ถ้า Session ใช้ไม่ได้
async function fetchSessionUser() {

    const token = localStorage.getItem("sessionToken");

    if (!token) {

        localStorage.clear();
        location.replace("/login.html");
        return null;
    }

    try {

        const response = await fetch(`${API_URL}/account/session`, {
            headers: { "X-Session-Token": token }
        });

        if (response.status === 401 || response.status === 403) {

            localStorage.clear();
            location.replace("/login.html");
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

// ใช้: if (!(await checkStaff())) return;
async function checkStaff() {

    const user = await fetchSessionUser();

    if (!user) return false;

    if (user.usertype !== "Staff") {

        alert("ไม่มีสิทธิ์เข้าใช้งาน");
        location.replace("applications.html");
        return false;
    }

    return true;
}

// ใช้: if (!(await checkTrader())) return;
async function checkTrader() {

    const user = await fetchSessionUser();

    if (!user) return false;

    if (user.usertype !== "Trader") {

        alert("ไม่มีสิทธิ์เข้าใช้งาน");
        location.replace("applications.html");
        return false;
    }

    return true;
}