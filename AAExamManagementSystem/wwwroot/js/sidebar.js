(function () {
    var STORAGE_KEY = 'ems-sidebar-collapsed';
    var mobileQuery = window.matchMedia('(max-width: 767.98px)');

    function applyCollapsed(sidebar, toggle, collapsed) {
        sidebar.classList.toggle('collapsed', collapsed);
        if (toggle) {
            toggle.setAttribute('aria-expanded', String(!collapsed));
        }
    }

    // On small screens the sidebar is a drawer that overlays the page; "collapsed" only applies to desktop.
    function setDrawerOpen(sidebar, toggle, open) {
        sidebar.classList.toggle('mobile-open', open);
        document.body.classList.toggle('sidebar-open', open);
        if (toggle) {
            toggle.setAttribute('aria-expanded', String(open));
        }
    }

    function highlightActiveLink(sidebar) {
        var currentPath = window.location.pathname.replace(/\/$/, '').toLowerCase();
        sidebar.querySelectorAll('.sidebar-link').forEach(function (link) {
            var linkPath = (link.getAttribute('href') || '').replace(/\/$/, '').toLowerCase();
            if (linkPath && (currentPath === linkPath || currentPath.indexOf(linkPath + '/') === 0)) {
                link.classList.add('active');
            }
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        var sidebar = document.getElementById('sidebar');
        var toggle = document.getElementById('sidebarToggle');
        if (!sidebar) return;

        var backdrop = document.createElement('div');
        backdrop.className = 'sidebar-backdrop';
        backdrop.setAttribute('aria-hidden', 'true');
        sidebar.insertAdjacentElement('afterend', backdrop);

        var storedCollapsed = false;
        try {
            storedCollapsed = localStorage.getItem(STORAGE_KEY) === 'true';
        } catch (e) { /* localStorage unavailable */ }

        function syncToViewport() {
            if (mobileQuery.matches) {
                applyCollapsed(sidebar, null, false);
                setDrawerOpen(sidebar, toggle, false);
            } else {
                setDrawerOpen(sidebar, null, false);
                applyCollapsed(sidebar, toggle, storedCollapsed);
            }
        }

        syncToViewport();
        highlightActiveLink(sidebar);

        if (mobileQuery.addEventListener) {
            mobileQuery.addEventListener('change', syncToViewport);
        } else {
            mobileQuery.addListener(syncToViewport);
        }

        if (toggle) {
            toggle.addEventListener('click', function () {
                if (mobileQuery.matches) {
                    setDrawerOpen(sidebar, toggle, !sidebar.classList.contains('mobile-open'));
                    return;
                }

                storedCollapsed = !sidebar.classList.contains('collapsed');
                applyCollapsed(sidebar, toggle, storedCollapsed);
                try {
                    localStorage.setItem(STORAGE_KEY, String(storedCollapsed));
                } catch (e) { /* localStorage unavailable */ }
            });
        }

        backdrop.addEventListener('click', function () {
            setDrawerOpen(sidebar, toggle, false);
        });

        sidebar.addEventListener('click', function (event) {
            if (mobileQuery.matches && event.target.closest('.sidebar-link, .sidebar-brand')) {
                setDrawerOpen(sidebar, toggle, false);
            }
        });

        document.addEventListener('keydown', function (event) {
            if (event.key === 'Escape' && mobileQuery.matches) {
                setDrawerOpen(sidebar, toggle, false);
            }
        });
    });
})();
