"use strict";
const hubUrl = "/hubs/admin";

const connection = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl)
    .build();

connection.on("ReceiveNotification", function (payload) {
    // payload.Title, payload.Message
    console.log("Notification", payload);
    const el = document.createElement("div");
    el.className = "alert alert-info";
    el.style.position = "fixed";
    el.style.right = "20px";
    el.style.top = "20px";
    el.style.zIndex = "9999";
    el.innerHTML = `<strong>${payload.Title}</strong><div>${payload.Message}</div>`;
    document.body.appendChild(el);
    setTimeout(() => el.remove(), 8000);
});

connection.start().then(() => console.log("Connected to admin hub"))
    .catch(err => console.error(err));
