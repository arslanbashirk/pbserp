(function (window, $) {
    "use strict";

    window.PbsErp = window.PbsErp || {};

    const config = window.PbsErpConfig || {};

    const loginUrl = config.loginUrl || "/Account/Login";
    const accessDeniedUrl = config.accessDeniedUrl || "/Account/AccessDenied";

    function getToken() {
        return sessionStorage.getItem("pbs_erp_token")
            || localStorage.getItem("pbs_erp_token");
    }

    function getUser() {
        const userJson = sessionStorage.getItem("pbs_erp_user")
            || localStorage.getItem("pbs_erp_user");

        if (!userJson) {
            return null;
        }

        try {
            return JSON.parse(userJson);
        } catch {
            return null;
        }
    }

    function clearLogin() {
        localStorage.removeItem("pbs_erp_token");
        localStorage.removeItem("pbs_erp_user");
        localStorage.removeItem("pbs_erp_token_expires");

        sessionStorage.removeItem("pbs_erp_token");
        sessionStorage.removeItem("pbs_erp_user");
        sessionStorage.removeItem("pbs_erp_token_expires");
    }

    function isApiUrl(url) {
        if (!url) {
            return false;
        }

        const value = url.toString().toLowerCase();

        return value.startsWith("/api/")
            || value.startsWith("api/")
            || value.includes("/api/");
    }

    function redirectToLogin() {
        clearLogin();
        window.location.href = loginUrl;
    }

    function redirectToAccessDenied() {
        window.location.href = accessDeniedUrl;
    }

    async function apiFetch(url, options = {}) {
        const token = getToken();

        const headers = {
            "Accept": "application/json",
            ...(options.headers || {})
        };

        if (token && isApiUrl(url)) {
            headers["Authorization"] = "Bearer " + token;
        }

        const response = await fetch(url, {
            ...options,
            credentials: "same-origin",
            headers: headers
        });

        if (response.status === 401) {
            redirectToLogin();
            throw new Error("Unauthorized");
        }

        if (response.status === 403) {
            redirectToAccessDenied();
            throw new Error("Forbidden");
        }

        return response;
    }

    function setupJQueryAjax() {
        if (!$ || !$.ajaxSetup) {
            return;
        }

        $.ajaxSetup({
            beforeSend: function (xhr, settings) {
                const token = getToken();

                if (token && settings && isApiUrl(settings.url)) {
                    xhr.setRequestHeader("Authorization", "Bearer " + token);
                }
            },
            statusCode: {
                401: function () {
                    redirectToLogin();
                },
                403: function () {
                    redirectToAccessDenied();
                }
            }
        });
    }

    window.PbsErp.getToken = getToken;
    window.PbsErp.getUser = getUser;
    window.PbsErp.clearLogin = clearLogin;
    window.PbsErp.apiFetch = apiFetch;
    window.PbsErp.setupJQueryAjax = setupJQueryAjax;

    setupJQueryAjax();

})(window, window.jQuery);