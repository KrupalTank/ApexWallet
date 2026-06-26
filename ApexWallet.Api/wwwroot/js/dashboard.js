const API_BASE_URL = "https://localhost:7284/api/v1";
const token = sessionStorage.getItem("authToken");

// Global Configuration Header Packet
const secureHeaders = {
    "Authorization": `Bearer ${token}`,
    "Content-Type": "application/json"
};

// --- STARTUP LOGIC LOADING ---
document.addEventListener("DOMContentLoaded", () => {
    loadUserProfile();
    loadWalletMetrics();
    loadAnalytics();
    loadTransactionHistory();
});

function logout() {
    sessionStorage.clear();
    window.location.href = "index.html";
}

// 1. Fetch Profile Info and bind into input fields
async function loadUserProfile() {
    try {
        const res = await fetch(`${API_BASE_URL}/user/profile`, { headers: secureHeaders });
        if (res.ok) {
            const data = await res.json();
            document.getElementById("navUser").innerText = `Welcome, ${data.fullName || data.fullname}`;

            // 📌 Set input field values for editing
            document.getElementById("txtProfFullName").value = data.fullName || data.fullname;
            document.getElementById("txtProfEmail").value = data.email;
            document.getElementById("txtProfIdentity").value = data.decryptedIdentityNumber || data.decryptedidentitynumber || "Verified Secured Encryption";
        }
    } catch (err) { console.error("Profile load failed.", err); }
}

// 2. Fetch Balance Stats
async function loadWalletMetrics() {
    try {
        const res = await fetch(`${API_BASE_URL}/wallet/balance`, { headers: secureHeaders });
        if (res.ok) {
            const data = await res.json();
            document.getElementById("lblBalance").innerText = data.balance.toFixed(2);
            document.getElementById("lblUpdated").innerText = new Date(data.lastUpdated).toLocaleTimeString();
        }
    } catch (err) { console.error(err); }
}

// 3. Fetch Analytics Monthly Aggregations
async function loadAnalytics() {
    try {
        const res = await fetch(`${API_BASE_URL}/wallet/analytics`, { headers: secureHeaders });
        if (res.ok) {
            const data = await res.json();
            document.getElementById("anlDeposit").innerText = `+${data.totalDeposited.toFixed(2)}`;
            document.getElementById("anlTransfer").innerText = `-${data.totalTransferredOut.toFixed(2)}`;
            document.getElementById("anlReceived").innerText = `+${data.totalReceivedIn.toFixed(2)}`;
        }
    } catch (err) { console.error(err); }
}

// 4. Fetch Ledger Table Logs
// 4. Fetch Ledger Table Logs with optional date query generation
async function loadTransactionHistory(startDate = "", endDate = "") {
    try {
        let url = `${API_BASE_URL}/wallet/transactions`;

        // Build query strings dynamically if dates are present
        if (startDate && endDate) {
            url += `?startDate=${startDate}&endDate=${endDate}`;
            document.getElementById("lblDateRangeInfo").innerText = `(${startDate} to ${endDate})`;
        } else {
            document.getElementById("lblDateRangeInfo").innerText = `(Showing Past 7 Days)`;
        }

        const res = await fetch(url, { headers: secureHeaders });
        if (res.ok) {
            const list = await res.json();
            const tbody = document.getElementById("tableBodyTransactions");
            tbody.innerHTML = "";

            if (list.length === 0) {
                tbody.innerHTML = `<tr><td colspan="5" class="text-center text-muted py-4">No transactions found inside this window.</td></tr>`;
                return;
            }

            list.forEach(t => {
                // 📌 Step 1: Assign strict contrast colors based on the transaction role
                const isSender = t.role === "Sender" || t.role === "sender";
                const badgeColor = isSender ? "bg-danger text-white" : "bg-success text-white";

                // Crimson red for transfers out, Emerald green for deposits/transfers in
                const amountColor = isSender ? "text-danger" : "text-success";
                const amountPrefix = isSender ? "-" : "+";

                // 📌 Step 2: Render the row with explicit text-color boundaries
                const row = `<tr>
                <td><span class="badge bg-secondary text-white">${t.transactionType || t.transactiontype}</span></td>
                <td><span class="badge ${badgeColor}">${t.role}</span></td>
                <td class="fw-bold ${amountColor}">${amountPrefix}${t.amount.toFixed(2)}</td>
                <td class="small text-muted" style="color: #495057 !important;">${new Date(t.timestamp).toLocaleString()}</td>
                <td><span class="text-success fw-medium">● ${t.status}</span></td>
                </tr>`;
                tbody.innerHTML += row;
            });
        }
    } catch (err) { console.error(err); }
}

// ==========================================
// 5. Execute Deposit Workflow (With Loading UI Protection)
// ==========================================
document.getElementById("frmDeposit").addEventListener("submit", async (e) => {
    e.preventDefault();

    const form = e.target;
    const input = document.getElementById("txtDepositAmount");
    const button = form.querySelector("button[type='submit']");
    const amt = parseFloat(input.value);

    // 📌 Step 1: Freeze UI instantly to prevent multi-clicks
    input.disabled = true;
    button.disabled = true;
    const originalBtnText = button.innerHTML;
    button.innerHTML = `<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Funding...`;

    try {
        const res = await fetch(`${API_BASE_URL}/wallet/deposit`, {
            method: "POST",
            headers: secureHeaders,
            body: JSON.stringify({ amount: amt })
        });

        if (res.ok) {
            // 📌 Step 2: Clear data fields only on explicit success
            form.reset();
            alert(`🎉 Success! Your deposit of ${amt.toFixed(2)} INR has been credited. Check your email for confirmation.`);

            // Refresh dashboard layers
            loadWalletMetrics();
            loadAnalytics();
            loadTransactionHistory();
        } else {
            alert("Deposit configuration processing failed.");
        }
    } catch (err) {
        console.error(err);
        alert("Network error processing deposit.");
    } finally {
        // 📌 Step 3: Unfreeze UI elements for next operation
        input.disabled = false;
        button.disabled = false;
        button.innerHTML = originalBtnText;
    }
});


// 6. Dynamic Fuzzy Auto-Complete Lookup Event
let searchTimeout;
document.getElementById("txtSearchRecipient").addEventListener("input", (e) => {
    clearTimeout(searchTimeout);
    const query = e.target.value.trim();
    const resultsDiv = document.getElementById("searchResults");

    if (query.length < 3) {
        resultsDiv.classList.add("d-none");
        return;
    }

    // Debounce wait timeout processing logic to preserve server pipeline loads
    searchTimeout = setTimeout(async () => {
        try {
            // Change this line inside the search timeout block:
            const res = await fetch(`${API_BASE_URL}/user/lookup/search?query=${query}`, { headers: secureHeaders });            if (res.ok) {
                const users = await res.json();
                resultsDiv.innerHTML = "";
                if (users.length === 0) { resultsDiv.classList.add("d-none"); return; }

                users.forEach(u => {
                    // Safely parse out the fields
                    const resolvedName = u.fullName || u.fullname || "Unknown User";
                    const resolvedEmail = u.email || "";
                    const resolvedUserId = u.userId || u.userid; // 📌 Map back to the User ID

                    const item = document.createElement("div");
                    item.className = "list-group-item";
                    item.innerText = `${resolvedName} (${resolvedEmail})`;

                    item.onclick = () => {
                        document.getElementById("txtSearchRecipient").value = resolvedName;
                        document.getElementById("txtTargetWalletId").value = resolvedUserId; // 📌 FIXED: Passes the user ID to match your Postman body!
                        document.getElementById("searchResults").classList.add("d-none");
                    };
                    resultsDiv.appendChild(item);
                });
                resultsDiv.classList.remove("d-none");
            }
        } catch (err) { console.error(err); }
    }, 400); // 400ms delay window
});

// ==========================================
// 7. Execute Transfer Workflow (With Loading UI Protection)
// ==========================================
document.getElementById("frmTransfer").addEventListener("submit", async (e) => {
    e.preventDefault();

    const form = e.target;
    const searchInput = document.getElementById("txtSearchRecipient");
    const amountInput = document.getElementById("txtTransferAmount");
    const button = form.querySelector("button[type='submit']");

    const targetId = parseInt(document.getElementById("txtTargetWalletId").value);
    const amt = parseFloat(amountInput.value);

    // 📌 Step 1: Freeze UI instantly to lock transaction parameters
    searchInput.disabled = true;
    amountInput.disabled = true;
    button.disabled = true;
    const originalBtnText = button.innerHTML;
    button.innerHTML = `<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Processing Secure Ledger...`;

    try {
        const res = await fetch(`${API_BASE_URL}/wallet/transfer`, {
            method: "POST",
            headers: secureHeaders,
            body: JSON.stringify({ receiverUserId: targetId, amount: amt })
        });

        if (res.ok) {
            // 📌 Step 2: Clear input values cleanly on success
            form.reset();
            document.getElementById("txtTargetWalletId").value = ""; // Clear hidden tracking field

            alert(`💸 Transfer Successful! Sent ${amt.toFixed(2)} INR cleanly. E-receipt notifications dispatched.`);

            // Re-render metrics instantly
            loadWalletMetrics();
            loadAnalytics();
            loadTransactionHistory();
        } else {
            const errorData = await res.json();
            alert(errorData[0]?.errorMessage || errorData.message || "Transfer processing rejected.");
        }
    } catch (err) {
        console.error(err);
        alert("Network connection error handling transaction.");
    } finally {
        // 📌 Step 3: Release form elements
        searchInput.disabled = false;
        amountInput.disabled = false;
        button.disabled = false;
        button.innerHTML = originalBtnText;
    }
});

// Triggered when user clicks the "Apply Filters" button
function applyDateFilters() {
    const start = document.getElementById("txtStartDate").value;
    const end = document.getElementById("txtEndDate").value;

    if (!start || !end) {
        alert("Please select both a Start Date and an End Date.");
        return;
    }

    if (new Date(start) > new Date(end)) {
        alert("Start Date cannot be later than End Date.");
        return;
    }

    // Pass chosen parameters directly down into the fetch method
    loadTransactionHistory(start, end);
}

// ==========================================
// 8. Execute Password Modification Workflow
// ==========================================
document.getElementById("frmChangePassword").addEventListener("submit", async (e) => {
    e.preventDefault();

    const form = e.target;
    const oldInput = document.getElementById("txtOldPassword");
    const newInput = document.getElementById("txtNewPassword");
    const button = form.querySelector("button[type='submit']");

    const oldPwd = oldInput.value;
    const newPwd = newInput.value;

    // Freeze interactive fields
    oldInput.disabled = true;
    newInput.disabled = true;
    button.disabled = true;
    const originalBtnText = button.innerHTML;
    button.innerHTML = `Updating Keys...`;

    try {
        const res = await fetch(`${API_BASE_URL}/auth/changepassword`, {
            method: "POST",
            headers: secureHeaders,
            body: JSON.stringify({ oldPassword: oldPwd, newPassword: newPwd })
        });

        if (res.ok) {
            form.reset();
            alert("🔒 Security Alert: Password updated successfully! A notification dispatch has been sent to your email.");
        } else {
            const errorData = await res.json();
            alert(errorData.message || "Failed to update password. Verify current credentials.");
        }
    } catch (err) {
        console.error(err);
        alert("Network error processing security changes.");
    } finally {
        // Unfreeze fields
        oldInput.disabled = false;
        newInput.disabled = false;
        button.disabled = false;
        button.innerHTML = originalBtnText;
    }
});

// ==========================================
// 9. Execute Profile Update Workflow
// ==========================================
document.getElementById("frmUpdateProfile").addEventListener("submit", async (e) => {
    e.preventDefault();

    const form = e.target;
    const nameInput = document.getElementById("txtProfFullName");
    const emailInput = document.getElementById("txtProfEmail");
    const button = form.querySelector("button[type='submit']");
    const alertBox = document.getElementById("profileAlert");

    alertBox.classList.add("d-none");

    // Freeze inputs instantly to prevent concurrent edits
    nameInput.disabled = true;
    emailInput.disabled = true;
    button.disabled = true;
    const originalBtnText = button.innerHTML;
    button.innerHTML = `Saving Changes...`;

    const payload = {
        fullName: nameInput.value,
        email: emailInput.value
    };

    try {
        const res = await fetch(`${API_BASE_URL}/user/update`, {
            method: "PUT",
            headers: secureHeaders,
            body: JSON.stringify(payload)
        });

        if (res.ok) {
            alert("✨ Profile updated successfully! If you modified your email, please check both your old and new inboxes for confirmation alerts.");
            loadUserProfile(); // Re-sync fields and update top greeting bar text
        } else {
            const errorData = await res.json();
            // Handle FluentValidation error lists or generic messages cleanly
            alertBox.innerText = errorData[0]?.errorMessage || errorData.message || "Failed to update profile updates.";
            alertBox.classList.remove("d-none");
        }
    } catch (err) {
        console.error(err);
        alert("Network error processing update data parameters.");
    } finally {
        // Release fields
        nameInput.disabled = false;
        emailInput.disabled = false;
        button.disabled = false;
        button.innerHTML = originalBtnText;
    }
});

// ==========================================
// 10. Emergency Lock Action Script
// ==========================================
async function triggerEmergencyLock() {
    const confirmation = confirm("⚠️ CRITICAL ALERT:\nAre you absolutely sure you want to lock your account? This will instantly freeze all actions until an administrator manually reactivates you.");
    if (!confirmation) return;

    try {
        const res = await fetch(`${API_BASE_URL}/auth/lock-account`, {
            method: "POST",
            headers: secureHeaders
        });

        if (res.ok) {
            alert("🔒 Your account has been frozen successfully. System security logs updated. Redirecting to gate...");
            sessionStorage.clear();
            window.location.href = "index.html"; // Send them back to login gate
        } else {
            alert("Failed to execute emergency protocol lock.");
        }
    } catch (err) { console.error(err); }
}