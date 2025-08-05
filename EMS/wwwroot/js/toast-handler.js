// toast-handler.js
function showToast(message, type = "error") {
    const toast = document.createElement("div");
    toast.className = "toast-message " + type;
    toast.innerText = message;
    document.body.appendChild(toast);
    setTimeout(() => toast.remove(), 3000);
}

async function fetchWithToast(url, options) {
    try {
        const response = await fetch(url, options);
        if (!response.ok) {
            const error = await response.json();
            showToast(error.message || "An error occurred");
        } else {
            const data = await response.json();
            showToast(data.message || "Success!", "success");
        }
    } catch (err) {
        showToast("Something went wrong!", "error");
    }
}
