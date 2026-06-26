// 📌 FIXED: Dynamic URL detection. Uses Render when live, localhost when offline!
const API_BASE_URL = window.location.hostname === "localhost" || window.location.hostname === "127.0.0.1"
    ? "https://localhost:7284/api/v1"                 // Local .NET 10 core development engine
    : "https://apexwallet.onrender.com/api/v1";       // Live production instance endpoint

function toggleAuth(showRegister) {
    if (showRegister) {
        document.getElementById('loginCard').classList.add('d-none');
        document.getElementById('registerCard').classList.remove('d-none');
    } else {
        document.getElementById('registerCard').classList.add('d-none');
        document.getElementById('loginCard').classList.remove('d-none');
    }
}

// --- REGISTER FORM SUBMISSION ---
document.getElementById('registerForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    const alertBox = document.getElementById('registerAlert');
    const successBox = document.getElementById('registerSuccess');
    alertBox.classList.add('d-none');
    successBox.classList.add('d-none');

    const payload = {
        fullName: document.getElementById('regName').value,
        email: document.getElementById('regEmail').value,
        identityNumber: document.getElementById('regIdentity').value,
        password: document.getElementById('regPassword').value
    };

    try {
        const response = await fetch(`${API_BASE_URL}/auth/register`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });

        const data = await response.json();

        if (response.ok) {
            successBox.innerText = "Wallet initialized! Check email. Redirecting to Login...";
            successBox.classList.remove('d-none');
            document.getElementById('registerForm').reset();
            setTimeout(() => { toggleAuth(false); }, 2500);
        } else {
            alertBox.innerText = data[0]?.errorMessage || data.message || "Registration failed.";
            alertBox.classList.remove('d-none');
        }
    } catch (err) {
        alertBox.innerText = "Cannot connect to server API.";
        alertBox.classList.remove('d-none');
    }
});

// --- LOGIN FORM SUBMISSION ---
document.getElementById('loginForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    const alertBox = document.getElementById('loginAlert');
    alertBox.classList.add('d-none');

    const payload = {
        email: document.getElementById('loginEmail').value,
        password: document.getElementById('loginPassword').value
    };

    try {
        const response = await fetch(`${API_BASE_URL}/auth/login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });

        const data = await response.json();

        if (response.ok) {
            sessionStorage.setItem("authToken", data.token);
            window.location.href = "dashboard.html";

        } else {
            alertBox.innerText = data.message || "Invalid email or password.";
            alertBox.classList.remove('d-none');
        }
    } catch (err) {
        alertBox.innerText = "Cannot connect to server API.";
        alertBox.classList.remove('d-none');
    }
});