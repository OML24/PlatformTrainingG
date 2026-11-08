(function () {
    const html = document.documentElement
    const storageKey = "__THEME_CONFIG__"
    const params = new URLSearchParams(window.location.search)

    /* ---------- DEFAULT CONFIG ---------- */
    const defaultConfig = {
        dir: "ltr",
        skin: "default",
        theme: "light",
        width: "fluid",
        position: "fixed",
        "sidenav-size": "on-hover-active",
        "sidenav-user": false,
        "topbar-color": "light",
        "sidenav-color": "dark",
    }

    /* ---------- SKIN PRESETS ---------- */
    const skinPresets = {
        minimal: { theme: "light", "sidenav-color": "gray", "topbar-color": "gray", "sidenav-user": false },
        material: { theme: "light", "topbar-color": "light", "sidenav-color": "gray", "sidenav-user": false },
        saas: { theme: "light", "topbar-color": "light", "sidenav-color": "dark", "sidenav-user": false },
        modern: { theme: "light", "topbar-color": "light", "sidenav-color": "dark", "sidenav-user": false },
        flat: { theme: "light", "sidenav-color": "light", "topbar-color": "light", "sidenav-user": false },
        galaxy: { theme: "dark" },
        luxe: { theme: "light", "topbar-color": "dark", "sidenav-color": "light", "sidenav-user": true },
        retro: { theme: "light", "sidenav-color": "dark", "topbar-color": "light", "sidenav-user": false },
        neon: { theme: "light", "sidenav-color": "light", "topbar-color": "light", "sidenav-user": false },
        pixel: { theme: "light", "sidenav-color": "dark", "topbar-color": "light", "sidenav-user": false },
        vivid: { theme: "light", "sidenav-color": "dark", "topbar-color": "light", "sidenav-user": false },
        soft: { theme: "light", "sidenav-color": "gradient", "topbar-color": "light", "sidenav-user": false },
        mono: { theme: "light", "sidenav-color": "light", "topbar-color": "dark", "sidenav-user": false },
        zen: { theme: "light", "sidenav-color": "dark", "topbar-color": "gray", "sidenav-user": false },
        silver: { theme: "light", "sidenav-color": "light", "topbar-color": "dark", "sidenav-user": false },
        prism: { theme: "light", "sidenav-color": "light", "topbar-color": "light", "sidenav-user": false },
        nova: { theme: "light", "sidenav-color": "dark", "topbar-color": "light", "sidenav-user": false },
        elegant: { theme: "light", "sidenav-color": "dark", "topbar-color": "light", "sidenav-user": false },
        matrix: { theme: "light", "sidenav-color": "dark", "topbar-color": "light", "sidenav-user": false },
        neo: { theme: "light", "sidenav-color": "gray", "topbar-color": "light", "sidenav-user": false },
        xenon: { theme: "light", "sidenav-color": "gradient", "topbar-color": "light", "sidenav-user": false },
        aurora: { theme: "light", "sidenav-color": "gradient", "topbar-color": "gray", "sidenav-user": false },
        crystal: { theme: "light", "sidenav-color": "dark", "topbar-color": "light", "sidenav-user": false },
        orbit: { theme: "light", "sidenav-color": "gradient", "topbar-color": "light", "sidenav-user": false },
    }

    /* ---------- LOAD SAVED CONFIG ---------- */
    let config = {
        ...defaultConfig,
        ...(JSON.parse(sessionStorage.getItem(storageKey)) || {})
    }

    /* ---------- SKIN FROM URL (?minimal) ---------- */
    for (const key of params.keys()) {
        if (skinPresets[key]) {
            config.skin = key
            Object.assign(config, skinPresets[key])
            break
        }
    }

    /* ---------- DARK OVERRIDE (?dark) ---------- */
    if (params.has("dark")) {
        config.theme = "dark"
    }

    /* ---------- RTL OVERRIDE (?rtl) ---------- */
    if (params.has("rtl")) {
        config.dir = "rtl"
    }

    /* ---------- RESPONSIVE SIDENAV ---------- */
    if (window.innerWidth <= 1140) {
        config["sidenav-size"] = "offcanvas"
    }

    /* ---------- APPLY TO HTML ---------- */
    html.setAttribute("dir", config.dir)
    html.setAttribute("data-skin", config.skin)
    html.setAttribute("data-bs-theme", config.theme)
    html.setAttribute("data-layout-position", config.position)
    html.setAttribute("data-layout-width", config.width)
    html.setAttribute("data-sidenav-size", config["sidenav-size"])
    html.setAttribute("data-topbar-color", config["topbar-color"])
    html.setAttribute("data-menu-color", config["sidenav-color"])

    if (config["sidenav-user"] === true) {
        html.setAttribute("data-sidenav-user", "true")
    } else {
        html.removeAttribute("data-sidenav-user")
    }

    /* ---------- SAVE FINAL CONFIG ---------- */
    sessionStorage.setItem(storageKey, JSON.stringify(config))

    /* ---------- EXPOSE FOR DEBUG ---------- */
    window.config = config
})();
