(function () {
    var badge = document.getElementById("notificationBadge");
    var list = document.getElementById("notificationList");
    if (!badge && !list) return;

    var API = "/api/v1/notifications";

    function setUnreadCount(count) {
        if (!badge) return;
        badge.classList.toggle("d-none", count <= 0);
    }

    function refreshUnreadCount() {
        fetch(API + "/unread-count")
            .then(function (res) { return res.ok ? res.json() : 0; })
            .then(setUnreadCount)
            .catch(function () {});
    }

    function removeEmptyState() {
        var empty = list.querySelector("[data-empty-state]");
        if (empty) empty.remove();
    }

    function markRead(item, notification) {
        if (notification.isRead) return;
        notification.isRead = true;
        item.classList.remove("border-start", "border-3", "border-success");
        fetch(API + "/" + notification.id + "/read", { method: "POST", keepalive: true })
            .then(refreshUnreadCount)
            .catch(function () {});
    }

    // Built with text nodes so notification text (which can include user-entered names) is never treated as HTML.
    function createItem(notification) {
        var item = document.createElement("a");
        item.href = notification.url || "#";
        item.className = "dropdown-item notification-item text-wrap";
        item.dataset.notificationId = notification.id;
        if (!notification.isRead) {
            item.classList.add("border-start", "border-3", "border-success");
        }

        var title = document.createElement("div");
        title.className = "fw-semibold";
        title.textContent = notification.title;

        var message = document.createElement("div");
        message.className = "small text-muted";
        message.textContent = notification.message;

        item.append(title, message);
        item.addEventListener("click", function () { markRead(item, notification); });
        return item;
    }

    function prependNotification(notification) {
        if (!list) return;
        removeEmptyState();
        list.prepend(createItem(notification));
    }

    function renderNotifications(notifications) {
        if (!list || !notifications || notifications.length === 0) return;
        removeEmptyState();
        notifications.forEach(function (notification) {
            list.append(createItem(notification));
        });
    }

    var connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/notifications")
        .withAutomaticReconnect()
        .build();

    connection.on("ReceiveNotification", function (notification) {
        prependNotification(notification);
        refreshUnreadCount();
    });

    connection.start().catch(function (err) { console.error("SignalR connection failed:", err); });

    // Show the notifications the user already has, not only the ones that arrive while the page is open.
    fetch(API)
        .then(function (res) { return res.ok ? res.json() : []; })
        .then(renderNotifications)
        .catch(function () {});

    refreshUnreadCount();
})();
