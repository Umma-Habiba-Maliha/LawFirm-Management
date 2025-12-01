"use strict";

const hubUrl = "/hubs/admin";

const connection = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl)
    .build();

// 1. General Notification
connection.on("ReceiveNotification", function (payload) {
    showToast(payload.Title, payload.Message, "bg-info");
});

// 2. New User Registration Alert
connection.on("NewPendingUser", function (fullName) {
    const msg = `<strong>${fullName}</strong> has registered.<br/><a href="/Account/AdminPendingList" class="text-white fw-bold" style="text-decoration:underline">Review Now</a>`;
    showToast("New Request", msg, "bg-warning");
});

function showToast(title, body, bgClass) {
    const el = document.createElement("div");
    el.className = `alert ${bgClass} text-white shadow`;
    el.style.position = "fixed";
    el.style.right = "20px";
    el.style.top = "80px";
    el.style.zIndex = "9999";
    el.style.minWidth = "300px";
    el.innerHTML = `
        <div class="d-flex justify-content-between">
            <strong>${title}</strong>
            <button type="button" class="btn-close btn-close-white" onclick="this.parentElement.parentElement.remove()"></button>
        </div>
        <div class="mt-2">${body}</div>`;
    document.body.appendChild(el);
    setTimeout(() => el.remove(), 8000);
}

connection.start()
    .then(() => console.log("Connected to Admin Hub"))
    .catch(err => console.error(err));