/*!
 * PBS ERP Request Helper
 * Purpose:
 * - One signature function for all API/MVC calls
 * - Keeps standard response format:
 *   { Success, Message, Data, Errors }
 * - Handles JWT, anti-forgery, JSON, form, FormData, GET/POST/PUT/DELETE
 */

(function (window, $) {
    "use strict";

    if (!$) {
        throw new Error("PBS ERP Request Helper requires jQuery.");
    }

    window.PbsErp = window.PbsErp || {};

    const config = window.PbsErpConfig || {};

    const loginUrl = config.loginUrl || "/Account/Login";
    const accessDeniedUrl = config.accessDeniedUrl || "/Account/AccessDenied";
    const refreshUrl = config.refreshUrl || apiUrl("/api/auth/refresh");

    // =====================================================
    // TOKEN HELPERS
    // =====================================================

    function getToken() {
        return localStorage.getItem("pbs_erp_token")
            || sessionStorage.getItem("pbs_erp_token");
    }

    function apiUrl(path) {
        const baseUrl = window.PBS_ERP_API_BASE_URL || "";

        if (!path.startsWith("/")) {
            path = "/" + path;
        }

        return baseUrl + path;
    }

    function setToken(token, user, expiresAtUtc, rememberMe) {
        clearToken();

        /*
            Recommended:
            localStorage allows new tabs to use the same JWT.
            If you want sessionStorage only, change this logic.
        */
        localStorage.setItem("pbs_erp_token", token || "");
        localStorage.setItem("pbs_erp_user", JSON.stringify(user || null));
        localStorage.setItem("pbs_erp_token_expires", expiresAtUtc || "");
        localStorage.setItem("pbs_erp_remember", rememberMe ? "true" : "false");
    }

    function clearToken() {
        localStorage.removeItem("pbs_erp_token");
        localStorage.removeItem("pbs_erp_user");
        localStorage.removeItem("pbs_erp_token_expires");
        localStorage.removeItem("pbs_erp_remember");

        sessionStorage.removeItem("pbs_erp_token");
        sessionStorage.removeItem("pbs_erp_user");
        sessionStorage.removeItem("pbs_erp_token_expires");
        sessionStorage.removeItem("pbs_erp_remember");
    }

    function getUser() {
        const userJson =
            localStorage.getItem("pbs_erp_user") ||
            sessionStorage.getItem("pbs_erp_user");

        if (!userJson) return null;

        try {
            return JSON.parse(userJson);
        } catch {
            return null;
        }
    }

    function redirectToLogin() {
        clearToken();
        window.location.href = loginUrl;
    }

    function redirectToAccessDenied() {
        window.location.href = accessDeniedUrl;
    }

    // =====================================================
    // ANTI-FORGERY HELPERS
    // =====================================================

    function getAntiForgeryToken(formSelector) {
        let token = "";

        if (formSelector) {
            token = $(formSelector)
                .find('input[name="__RequestVerificationToken"]')
                .val() || "";
        }

        if (!token) {
            token = $('input[name="__RequestVerificationToken"]').first().val() || "";
        }

        return token;
    }

    // =====================================================
    // COMMON HELPERS
    // =====================================================

    function buildQuery(params) {
        if (!params) return "";

        const query = Object.keys(params)
            .filter(function (key) {
                return params[key] !== undefined &&
                    params[key] !== null &&
                    params[key] !== "";
            })
            .map(function (key) {
                return encodeURIComponent(key) + "=" + encodeURIComponent(params[key]);
            })
            .join("&");

        return query ? "?" + query : "";
    }

    function isStandardResponse(response) {
        return response &&
            typeof response === "object" &&
            (
                Object.prototype.hasOwnProperty.call(response, "Success") ||
                Object.prototype.hasOwnProperty.call(response, "Message") ||
                Object.prototype.hasOwnProperty.call(response, "Data") ||
                Object.prototype.hasOwnProperty.call(response, "Errors")
            );
    }

    function normalizeResponse(response, action) {
        /*
            Always return your standard format:
            { Success, Message, Data, Errors }
        */

        if (isStandardResponse(response)) {
            return {
                Success: response.Success === true,
                Message: response.Message || "",
                Data: response.Data ?? null,
                Errors: response.Errors ?? null
            };
        }

        if (action && action.responseType === "html") {
            return {
                Success: true,
                Message: "",
                Data: response,
                Errors: null
            };
        }

        return {
            Success: true,
            Message: "",
            Data: response,
            Errors: null
        };
    }

    function normalizeError(xhr) {
        let message = "Request failed.";
        let data = null;
        let errors = null;

        if (xhr.responseJSON) {
            message =
                xhr.responseJSON.Message ||
                xhr.responseJSON.message ||
                xhr.responseJSON.title ||
                message;

            data =
                xhr.responseJSON.Data ||
                xhr.responseJSON.data ||
                null;

            errors =
                xhr.responseJSON.Errors ||
                xhr.responseJSON.errors ||
                null;
        }
        else if (xhr.responseText) {
            message = xhr.responseText;
        }

        return {
            Success: false,
            Message: message,
            Data: data,
            Errors: errors
        };
    }

    function collectFormFields(formSelector, options) {
        options = options || {};

        const data = {};
        const includeEmpty = options.includeEmpty === true;
        const excludeNames = options.excludeNames || ["__RequestVerificationToken"];

        $(formSelector).find("input, select, textarea").each(function () {
            const el = this;
            const $el = $(el);

            if (!el.name) return;
            if (excludeNames.includes(el.name)) return;
            if ($el.prop("disabled")) return;

            if (el.type === "checkbox") {
                if (el.checked) {
                    if (data[el.name]) {
                        data[el.name] += "," + el.value;
                    } else {
                        data[el.name] = el.value;
                    }
                }
                return;
            }

            if (el.type === "radio") {
                if (el.checked) {
                    data[el.name] = el.value;
                }
                return;
            }

            const value = $el.val();

            if (includeEmpty || (value !== null && value !== undefined && value !== "")) {
                data[el.name] = value;
            }
        });

        return data;
    }

    function collectFormData(formSelector) {
        const form = $(formSelector)[0];

        if (!form) {
            throw new Error("Form not found: " + formSelector);
        }

        return new FormData(form);
    }

    // =====================================================
    // TOKEN REFRESH
    // =====================================================

    async function refreshAccessToken() {
        try {
            const response = await fetch(refreshUrl, {
                method: "POST",
                credentials: "same-origin",
                headers: {
                    "Content-Type": "application/json",
                    "Accept": "application/json"
                },
                body: JSON.stringify({})
            });

            if (!response.ok) {
                return null;
            }

            const data = await response.json();

            if (!data || !data.token) {
                return null;
            }

            setToken(
                data.token,
                data.user || null,
                data.accessTokenExpiresAtUtc || "",
                localStorage.getItem("pbs_erp_remember") === "true"
            );

            return data.token;
        }
        catch {
            return null;
        }
    }

    // =====================================================
    // ACTION REGISTRY
    // Define your endpoint format once here.
    // =====================================================

    const actions = {

        // =================================================
        // CRUD API
        // =================================================

        "crud.save": {
            method: "POST",
            url: apiUrl("/api/crud/save"),
            auth: "jwt",
            requestType: "json",
            responseType: "json",
            query: function (p) {
                return {
                    table: p.table
                };
            },
            body: function (p) {
                return {
                    ID: p.id || p.ID || null,
                    fields: p.fields || {}
                };
            }
        },
        "crud.upload": {
            method: "POST",
            url: apiUrl("/api/crud/upload"),
            auth: "jwt",
            requestType: "formData",
            responseType: "json",
            query: function (p) {
                return {
                    table: p.table
                };
            },
            body: function (p) {
                return p.formData;
            }
        },
        "crud.update": {
            method: "PUT",
            url: apiUrl("/api/crud/update"),
            auth: "jwt",
            requestType: "json",
            responseType: "json",
            query: function (p) {
                return {
                    table: p.table,
                    uid: p.uid
                };
            },
            body: function (p) {
                return {
                    fields: p.fields || {}
                };
            }
        },

        "crud.delete": {
            method: "DELETE",
            url: apiUrl("/api/crud/delete"),
            auth: "jwt",
            requestType: "none",
            responseType: "json",
            query: function (p) {
                return {
                    table: p.table,
                    id: p.uid
                };
            }
        },

        "crud.bulkSave": {
            method: "POST",
            url: apiUrl("/api/crud/bulksave"),
            auth: "jwt",
            requestType: "json",
            responseType: "json",
            query: function (p) {
                return {
                    table: p.table
                };
            },
            body: function (p) {
                return {
                    fields: p.rows || p.fields || []
                };
            }
        },

        "crud.list": {
            method: "GET",
            url: apiUrl("/api/crud/list"),
            auth: "jwt",
            requestType: "none",
            responseType: "json",
            query: function (p) {
                return {
                    table: p.table,
                    filter: p.filter || ""
                };
            }
        },

        // /api/crud/get is same as /api/crud/record
        "crud.record": {
            method: "GET",
            url: apiUrl("/api/crud/record"),
            auth: "jwt",
            requestType: "none",
            responseType: "json",
            query: function (p) {
                return {
                    table: p.table,
                    id: p.uid || p.id
                };
            }
        },

        // =================================================
        // METADATA API
        // =================================================

        "metadata.get": {
            method: "GET",
            url: apiUrl("/api/metadata/get"),
            auth: "jwt",
            requestType: "none",
            responseType: "json",
            query: function (p) {
                return {
                    table: p.table
                };
            }
        },
        "metadata.tables": {
            method: "GET",
            url: apiUrl("/api/metadata/tables"),
            auth: "jwt",
            requestType: "none",
            responseType: "json",
            query: function (p) {
                const q = {};

                if (p && Array.isArray(p.uids) && p.uids.length > 0) {
                    q.uids = p.uids;
                }

                if (p && p.database) {
                    q.database = p.database;
                }

                return q;
            }
        },

        "metadata.columns": {
            method: "GET",
            url: apiUrl("/api/metadata/columns"),
            auth: "jwt",
            requestType: "none",
            responseType: "json",
            query: function (p) {
                return {
                    table: p.table
                };
            }
        },
        // =================================================
        // LOOKUP API
        // =================================================

        "lookup.get": {
            method: "GET",
            url: apiUrl("/api/lookup/get"),
            auth: "jwt",
            requestType: "none",
            responseType: "json",
            query: function (p) {
                return {
                    table: p.table,
                    valueCol: p.valueCol,
                    textCol: p.textCol,
                    where: p.where || "",
                    order: p.order || ""
                };
            }
        },

        // =================================================
        // MVC ACTIONS
        // Example: [HttpPost("File")] [ValidateAntiForgeryToken]
        // =================================================

        "mvc.file": {
            method: "POST",
            url: "/Crud/File",
            auth: "cookie",
            antiForgery: true,
            requestType: "form",
            responseType: "html",
            body: function (p) {
                return p.data || {};
            }
        },

        // Generic MVC form post.
        // Use when any MVC action returns View/PartialView HTML.
        "mvc.postForm": {
            method: "POST",
            url: function (p) {
                return p.url;
            },
            auth: "cookie",
            antiForgery: true,
            requestType: "form",
            responseType: "html",
            body: function (p) {
                return p.data || {};
            }
        },

        // Generic API JSON call.
        // Use only for unusual API actions not already listed.
        "api.json": {
            method: function (p) {
                return p.method || "POST";
            },
            url: function (p) {
                return p.url;
            },
            auth: "jwt",
            requestType: "json",
            responseType: "json",
            query: function (p) {
                return p.query || null;
            },
            body: function (p) {
                return p.body || {};
            }
        },

        "crud.clean": {
            method: "POST",
            url: apiUrl("/api/crud/clean"),
            auth: "jwt",
            requestType: "json",
            responseType: "json",
            body: function (p) {
                return {
                    id: p.id || "",
                    fields: p.fields || {}
                };
            }
        },
        "auth.updateProfile": {
            method: "PUT",
            url: apiUrl("/api/auth/profile"),
            auth: "jwt",
            requestType: "json",
            responseType: "json",
            body: function (p) {
                return {
                    name: p.name,
                    cnic: p.cnic,
                    gender: p.gender,
                    mobile: p.mobile
                };
            }
        },
    };

    // =====================================================
    // REGISTER / EXTEND ACTIONS
    // =====================================================

    function registerAction(name, definition) {
        if (!name) {
            throw new Error("Action name is required.");
        }

        if (!definition) {
            throw new Error("Action definition is required.");
        }

        actions[name] = definition;
    }

    // =====================================================
    // MAIN CALL FUNCTION
    // Signature:
    // PbsErp.call("crud.save", params, handlers)
    // =====================================================

    function call(actionName, params, handlers) {
        params = params || {};
        handlers = handlers || {};

        const action = actions[actionName];

        if (!action) {
            throw new Error("Unknown PBS ERP action: " + actionName);
        }

        let method = typeof action.method === "function"
            ? action.method(params)
            : action.method;

        method = (method || "GET").toUpperCase();

        let url = typeof action.url === "function"
            ? action.url(params)
            : action.url;

        if (!url) {
            throw new Error("URL is missing for action: " + actionName);
        }

        const query = action.query ? action.query(params) : null;
        url += buildQuery(query);

        const headers = {};

        if (action.auth === "jwt") {
            const token = getToken();

            if (token) {
                headers["Authorization"] = "Bearer " + token;
            }
        }

        if (action.antiForgery === true) {
            const antiForgeryToken = getAntiForgeryToken(params.formSelector);

            if (antiForgeryToken) {
                headers["RequestVerificationToken"] = antiForgeryToken;
            }
        }

        const ajaxOptions = {
            url: url,
            method: method,
            headers: headers,
            dataType: action.responseType || "json",

            xhr: function () {
                const xhr = new window.XMLHttpRequest();

                if (typeof handlers.progress === "function") {
                    xhr.upload.addEventListener("progress", function (evt) {
                        if (evt.lengthComputable) {
                            const percent = Math.round((evt.loaded / evt.total) * 100);
                            handlers.progress(percent, evt);
                        }
                    }, false);
                }

                return xhr;
            },

            success: function (response) {
                const standardResponse = normalizeResponse(response, action);

                if (typeof handlers.success === "function") {
                    handlers.success(standardResponse);
                }
            },

            error: async function (xhr) {
                /*
                    If JWT expired, try refresh once, then retry same request.
                */
                if (
                    xhr.status === 401 &&
                    action.auth === "jwt" &&
                    params.retryOn401 !== false &&
                    params.__retried !== true
                ) {
                    const newToken = await refreshAccessToken();

                    if (newToken) {
                        params.__retried = true;
                        call(actionName, params, handlers);
                        return;
                    }
                }

                if (xhr.status === 401 && handlers.redirectOn401 !== false) {
                    redirectToLogin();
                    return;
                }

                if (xhr.status === 403 && handlers.redirectOn403 !== false) {
                    redirectToAccessDenied();
                    return;
                }

                const standardError = normalizeError(xhr);

                if (typeof handlers.error === "function") {
                    handlers.error(standardError);
                }
            }
        };

        if (action.requestType === "json") {
            ajaxOptions.contentType = "application/json; charset=utf-8";
            ajaxOptions.data = JSON.stringify(action.body ? action.body(params) : {});
        }
        else if (action.requestType === "form") {
            ajaxOptions.data = action.body ? action.body(params) : {};
        }
        else if (action.requestType === "formData") {
            ajaxOptions.data = action.body ? action.body(params) : params.formData;
            ajaxOptions.processData = false;
            ajaxOptions.contentType = false;
        }
        else if (action.requestType === "none") {
            // No request body.
        }
        else {
            throw new Error("Invalid requestType for action " + actionName + ": " + action.requestType);
        }

        return $.ajax(ajaxOptions);
    }

    // =====================================================
    // OPTIONAL: SETUP GLOBAL AJAX JWT FOR EXISTING OLD CODE
    // This helps old $.ajax calls, but new code should use PbsErp.call().
    // =====================================================

    function setupJQueryAjax() {
        $.ajaxSetup({
            beforeSend: function (xhr, settings) {
                const token = getToken();

                if (!token || !settings || !settings.url) return;

                const url = settings.url.toString().toLowerCase();

                if (url.includes("/api/")) {
                    xhr.setRequestHeader("Authorization", "Bearer " + token);
                }
            }
        });
    }

    // =====================================================
    // EXPORTS
    // =====================================================

    window.PbsErp.getToken = getToken;
    window.PbsErp.setToken = setToken;
    window.PbsErp.clearToken = clearToken;
    window.PbsErp.getUser = getUser;

    window.PbsErp.getAntiForgeryToken = getAntiForgeryToken;
    window.PbsErp.collectFormFields = collectFormFields;
    window.PbsErp.collectFormData = collectFormData;

    window.PbsErp.refreshAccessToken = refreshAccessToken;

    window.PbsErp.call = call;
    window.PbsErp.registerAction = registerAction;
    window.PbsErp.actions = actions;

    window.PbsErp.setupJQueryAjax = setupJQueryAjax;

    setupJQueryAjax();

})(window, window.jQuery);

function normalizeErrorList(errors) {
    if (!errors) {
        return null;
    }

    if (Array.isArray(errors)) {
        return errors;
    }

    if (typeof errors === "string") {
        return [errors];
    }

    if (typeof errors === "object") {
        return Object.keys(errors).map(function (key) {
            const value = errors[key];

            if (Array.isArray(value)) {
                return key + ": " + value.join(", ");
            }

            return key + ": " + value;
        });
    }

    return [String(errors)];
}