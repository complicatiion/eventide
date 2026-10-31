namespace Eventide.Core.Localization;

/// <summary>All UI strings: key → (English, German).</summary>
internal static class Strings
{
    public static readonly Dictionary<string, (string En, string De)> All = new()
    {
        // ---------------------------------------------------------------- Tray menu
        ["TrayOpenSettings"] = ("Settings…", "Einstellungen…"),
        ["TrayEnable"] = ("Enable filter", "Filter aktivieren"),
        ["TrayDisable"] = ("Disable filter", "Filter deaktivieren"),
        ["TrayMode"] = ("Mode", "Modus"),
        ["ModeAuto"] = ("Automatic (sunset)", "Automatisch (Sonnenuntergang)"),
        ["ModeScheduleFmt"] = ("Schedule ({0} – {1})", "Zeitplan ({0} – {1})"),
        ["ModeAlways"] = ("Always on", "Immer an"),
        ["TrayIntensity"] = ("Intensity (colour temperature)", "Intensität (Farbtemperatur)"),
        ["IntSoft"] = ("Soft (≈ 5000 K)", "Sanft (ca. 5000 K)"),
        ["IntMedium"] = ("Medium (≈ 4200 K)", "Medium (ca. 4200 K)"),
        ["IntWarm"] = ("Warm (≈ 3800 K)", "Warm (ca. 3800 K)"),
        ["IntStrong"] = ("Strong (≈ 3200 K) – halogen look", "Stark (ca. 3200 K) – Halogen-Look"),
        ["IntExtreme"] = ("Extreme (≈ 2700 K) – candle light", "Extrem (ca. 2700 K) – Kerzenlicht"),
        ["IntCustomFmt"] = ("Custom ({0} K)", "Benutzerdefiniert ({0} K)"),
        ["TrayPause"] = ("Pause", "Pausieren"),
        ["PauseMorning"] = ("Until tomorrow", "Bis morgen"),
        ["DefaultSuffix"] = (" · default", " · Standard"),
        ["DurMinFmt"] = ("{0} min", "{0} Min."),
        ["DurHourFmt"] = ("{0} h", "{0} Std."),
        ["DurHourMinFmt"] = ("{0} h {1} min", "{0} Std. {1} Min."),
        ["ResumeFmt"] = ("Resume (paused until {0})", "Fortsetzen (pausiert bis {0})"),
        ["TrayAutostart"] = ("Start with Windows", "Mit Windows starten"),
        ["TrayExit"] = ("Exit", "Beenden"),

        // ---------------------------------------------------------------- Status texts (tray + window)
        ["StOff"] = ("Off", "Aus"),
        ["StPausedFmt"] = ("Paused until {0}", "Pausiert bis {0}"),
        ["StDay"] = ("Day – no filter", "Tag – kein Filter"),
        ["StDusk"] = ("Getting warmer", "Wird wärmer"),
        ["StNight"] = ("Night filter", "Nachtfilter"),
        ["StDawn"] = ("Back to daylight", "Zurück zum Tageslicht"),
        ["StAlways"] = ("Always on", "Immer an"),
        ["StNextFmt"] = ("until {0}", "bis {0}"),
        ["StRemote"] = ("Remote session – inactive", "Remotesitzung – inaktiv"),
        ["StUnsupported"] = ("Display rejects colour changes", "Bildschirm lehnt Farbänderung ab"),
        ["StPreview"] = ("Preview", "Vorschau"),
        ["FirstRunTitle"] = ("Eventide is running", "Eventide läuft"),
        ["FirstRunText"] = (
            "Left click on the tray icon: filter on/off. Right click: menu and settings. Starts with Windows (can be switched off in the menu).",
            "Linksklick auf das Tray-Symbol: Filter ein/aus. Rechtsklick: Menü und Einstellungen. Startet mit Windows (im Menü abschaltbar)."),
        ["TooltipFmt"] = ("Eventide – {0}", "Eventide – {0}"),

        // ---------------------------------------------------------------- Window: navigation and pages
        ["NavStatus"] = ("Status", "Status"),
        ["NavSchedule"] = ("Schedule", "Zeitplan"),
        ["NavFilter"] = ("Filter", "Filter"),
        ["NavGeneral"] = ("General", "Allgemein"),
        ["NavAbout"] = ("About", "Info"),
        ["StatusSub"] = ("What Eventide is doing right now", "Was Eventide gerade macht"),
        ["ScheduleSub"] = ("When the filter is active", "Wann der Filter aktiv ist"),
        ["FilterSub"] = ("How warm the screen gets at night", "Wie warm der Bildschirm nachts wird"),
        ["GeneralSub"] = ("Start, language and colour range", "Start, Sprache und Farbbereich"),
        ["AboutSub"] = ("Version, license and project", "Version, Lizenz und Projekt"),

        // ---------------------------------------------------------------- Status page
        ["BtnTurnOn"] = ("Turn on", "Einschalten"),
        ["BtnTurnOff"] = ("Turn off", "Ausschalten"),
        ["BtnResume"] = ("Resume", "Fortsetzen"),
        ["TrayNotRunning"] = ("Warning: the Eventide tray is not running – the filter is inactive.", "Warnung: Der Eventide-Tray läuft nicht – der Filter ist inaktiv."),
        ["NoticeInfoPrefix"] = ("Note: ", "Hinweis: "),
        ["NoticeWarnPrefix"] = ("Warning: ", "Warnung: "),
        ["SecPause"] = ("Pause", "Pausieren"),
        ["PausedUntilLabel"] = ("Paused until", "Pausiert bis"),
        ["PauseCustom"] = ("Custom", "Eigene Dauer"),
        ["UnitMinutes"] = ("Minutes", "Minuten"),
        ["UnitHours"] = ("Hours", "Stunden"),
        ["BtnPause"] = ("Pause", "Pausieren"),
        ["PauseHint"] = (
            "The selected duration is also the default in the tray menu.",
            "Die gewählte Dauer ist auch der Standard im Tray-Menü."),
        ["PauseOnlyWhenOn"] = ("Available while the filter is on.", "Verfügbar, solange der Filter an ist."),
        ["BtnStartTray"] = ("Start tray", "Tray starten"),
        ["CardToday"] = ("Today", "Heute"),
        ["Sunset"] = ("Sunset", "Sonnenuntergang"),
        ["Sunrise"] = ("Sunrise", "Sonnenaufgang"),
        ["CardScreen"] = ("Screen", "Bildschirm"),
        ["CurrentKelvin"] = ("Current", "Aktuell"),
        ["NightValue"] = ("Night value", "Nachtwert"),
        ["Displays"] = ("Displays", "Bildschirme"),
        ["NextChange"] = ("Next change", "Nächster Wechsel"),
        ["NoticeLimitedFmt"] = (
            "Windows limits the colour range: only {0} K can be reached. Enable the extended gamma range under General.",
            "Windows begrenzt den Farbbereich: Es sind nur {0} K erreichbar. Aktiviere den erweiterten Gamma-Bereich unter Allgemein."),
        ["NoticeHdr"] = (
            "HDR is on for at least one display. Windows ignores gamma ramps in HDR mode – the filter has no effect there.",
            "HDR ist für mindestens einen Bildschirm an. Windows ignoriert Gamma-Rampen im HDR-Modus – dort wirkt der Filter nicht."),
        ["NoticeNightLight"] = (
            "Windows Night light is on as well. Turn one of them off to avoid a double tint.",
            "Der Windows-Nachtmodus ist ebenfalls an. Schalte einen der beiden aus, damit der Bildschirm nicht doppelt getönt wird."),
        ["NoticeRemote"] = (
            "Remote session (RDP / Citrix): colour ramps are not available here, Eventide stays inactive.",
            "Remotesitzung (RDP / Citrix): Farbrampen sind hier nicht verfügbar, Eventide bleibt inaktiv."),
        ["NoticeUnsupported"] = (
            "No display accepted a colour change (driver, virtual or remote display).",
            "Kein Bildschirm hat eine Farbänderung angenommen (Treiber, virtueller oder Remote-Bildschirm)."),
        ["NoticeApproxLocation"] = (
            "No location set – sun times are estimated from your time zone.\nSet your location under Schedule.",
            "Kein Standort festgelegt – die Sonnenzeiten werden aus der Zeitzone geschätzt.\nLege den Standort unter Zeitplan fest."),

        // ---------------------------------------------------------------- Schedule page
        ["SecMode"] = ("Mode", "Modus"),
        ["ModeAutoTitle"] = ("Automatic", "Automatisch"),
        ["ModeAutoDesc"] = ("From sunset to sunrise at your location", "Von Sonnenuntergang bis Sonnenaufgang an deinem Standort"),
        ["ModeSchedTitle"] = ("Schedule", "Zeitplan"),
        ["ModeSchedDesc"] = ("Fixed time window every day", "Festes Zeitfenster an jedem Tag"),
        ["ModeAlwaysTitle"] = ("Always on", "Immer an"),
        ["ModeAlwaysDesc"] = ("Permanent filter, e.g. for dark rooms", "Dauerhafter Filter, z. B. für dunkle Arbeitsräume"),
        ["SecLocation"] = ("Location", "Standort"),
        ["Latitude"] = ("Latitude", "Breitengrad"),
        ["Longitude"] = ("Longitude", "Längengrad"),
        ["LocationHint"] = (
            "Decimal degrees, e.g. 53.08 / 8.80. Two decimals (≈ 1 km) are enough.",
            "Dezimalgrad, z. B. 53,08 / 8,80. Zwei Nachkommastellen (ca. 1 km) reichen."),
        ["BtnWindowsLocation"] = ("Use Windows location", "Windows-Standort verwenden"),
        ["LocationPrivacy"] = (
            "Requested once, rounded to about 1 km and stored only on this PC. Eventide never sends it anywhere.",
            "Wird einmalig abgefragt, auf ca. 1 km gerundet und nur auf diesem PC gespeichert. Eventide sendet ihn nirgendwohin."),
        ["LocDenied"] = (
            "Location access is off or denied (Windows Settings › Privacy & security › Location).",
            "Standortzugriff ist aus oder verweigert (Windows-Einstellungen › Datenschutz und Sicherheit › Position)."),
        ["LocFailed"] = ("The location could not be determined.", "Der Standort konnte nicht ermittelt werden."),
        ["LocBusy"] = ("Requesting location…", "Standort wird abgefragt…"),
        ["LocOkFmt"] = ("Location set: {0}, {1}", "Standort übernommen: {0}, {1}"),
        ["LocInvalid"] = (
            "Enter valid coordinates (latitude −90 … 90, longitude −180 … 180).",
            "Gültige Koordinaten eingeben (Breitengrad −90 … 90, Längengrad −180 … 180)."),
        ["SunPreviewFmt"] = ("Today: sunset {0} · sunrise tomorrow {1}", "Heute: Sonnenuntergang {0} · Sonnenaufgang morgen {1}"),
        ["SunPolarNight"] = ("Polar night – the filter stays on all day.", "Polarnacht – der Filter bleibt den ganzen Tag an."),
        ["SunMidnight"] = ("Midnight sun – no night today.", "Mitternachtssonne – heute keine Nacht."),
        ["SecTimes"] = ("Time window", "Zeitfenster"),
        ["From"] = ("From", "Von"),
        ["To"] = ("To", "Bis"),
        ["SecTransition"] = ("Transition", "Übergang"),
        ["MinutesFmt"] = ("{0} min", "{0} Min."),
        ["TransitionHint"] = (
            "Soft fade after the start and before the end of the night window.",
            "Sanfter Übergang nach Beginn und vor Ende des Nachtfensters."),

        // ---------------------------------------------------------------- Filter page
        ["SecIntensity"] = ("Night colour temperature", "Nacht-Farbtemperatur"),
        ["IntSoftTitle"] = ("Soft", "Sanft"),
        ["IntSoftDesc"] = ("≈ 5000 K", "ca. 5000 K"),
        ["IntMediumTitle"] = ("Medium", "Medium"),
        ["IntMediumDesc"] = ("≈ 4200 K", "ca. 4200 K"),
        ["IntWarmTitle"] = ("Warm", "Warm"),
        ["IntWarmDesc"] = ("≈ 3800 K · warm white", "ca. 3800 K · Warmweiß"),
        ["IntStrongTitle"] = ("Strong", "Stark"),
        ["IntStrongDesc"] = ("≈ 3200 K · halogen", "ca. 3200 K · Halogen"),
        ["IntExtremeTitle"] = ("Extreme", "Extrem"),
        ["IntExtremeDesc"] = ("≈ 2700 K · candle light", "ca. 2700 K · Kerzenlicht"),
        ["IntCustomTitle"] = ("Custom", "Benutzerdefiniert"),
        ["IntCustomDesc"] = ("Own value with the slider", "Eigener Wert per Regler"),
        ["SecCustom"] = ("Custom value", "Eigener Wert"),
        ["PreviewHint"] = (
            "While this page is open, the screen shows the selected night value.",
            "Solange diese Seite offen ist, zeigt der Bildschirm den gewählten Nachtwert."),
        ["LimitedHintFmt"] = (
            "Values below {0} K need the extended gamma range (General).",
            "Werte unter {0} K brauchen den erweiterten Gamma-Bereich (Allgemein)."),

        // ---------------------------------------------------------------- General page
        ["SecStartup"] = ("Start", "Start"),
        ["AutostartTitle"] = ("Start with Windows", "Mit Windows starten"),
        ["AutostartDesc"] = (
            "Starts the tray at sign-in (this user, no admin rights needed).",
            "Startet den Tray bei der Anmeldung (dieser Benutzer, ohne Adminrechte)."),
        ["SecLanguage"] = ("Language", "Sprache"),
        ["LangAuto"] = ("Automatic (Windows)", "Automatisch (Windows)"),
        ["LangEn"] = ("English", "English"),
        ["LangDe"] = ("German", "German"),
        ["SecGamma"] = ("Extended gamma range", "Erweiterter Gamma-Bereich"),
        ["GammaDesc"] = (
            "By default Windows only accepts mild colour changes. Strong and Extreme need the system-wide value GdiIcmGammaRange = 256 (HKLM, admin rights) – f.lux uses the same value. It takes effect after signing in again or a restart.",
            "Standardmäßig akzeptiert Windows nur leichte Farbänderungen. Stark und Extrem brauchen den systemweiten Wert GdiIcmGammaRange = 256 (HKLM, Adminrechte) – f.lux nutzt denselben Wert. Er wirkt nach einer Neuanmeldung oder einem Neustart."),
        ["GammaOn"] = ("Enabled", "Aktiviert"),
        ["GammaOff"] = ("Not enabled", "Nicht aktiviert"),
        ["BtnGammaEnable"] = ("Enable", "Aktivieren"),
        ["BtnGammaDisable"] = ("Disable", "Deaktivieren"),
        ["GammaRestartHint"] = (
            "Changed – sign in again or restart Windows to apply it.",
            "Geändert – zum Übernehmen neu anmelden oder Windows neu starten."),
        ["GammaCancelled"] = ("Cancelled – nothing was changed.", "Abgebrochen – nichts geändert."),
        ["GammaFailed"] = ("The value could not be changed.", "Der Wert konnte nicht geändert werden."),
        ["GammaNotOwned"] = (
            "Set by another program (e.g. f.lux) – Eventide leaves it unchanged.",
            "Von einem anderen Programm gesetzt (z. B. f.lux) – Eventide lässt ihn unverändert."),
        ["GammaAdminHint"] = ("Needs administrator rights (UAC prompt).", "Benötigt Administratorrechte (UAC-Abfrage)."),
        ["SecData"] = ("Data", "Daten"),
        ["SettingsFile"] = ("Settings file", "Einstellungsdatei"),
        ["BtnOpenFolder"] = ("Open folder", "Ordner öffnen"),
        ["PortableMode"] = ("Portable mode", "Portabler Modus"),

        // ---------------------------------------------------------------- About page
        ["VersionFmt"] = ("Version {0}", "Version {0}"),
        ["LicenseLine"] = ("MIT License · © 2026 Sven Klocke (sksdesign | complicatiion)", "MIT-Lizenz · © 2026 Sven Klocke (sksdesign | complicatiion)"),
        ["BtnLicenses"] = ("Licenses", "Lizenzen"),
        ["BtnWebsite"] = ("sksdesign.de", "sksdesign.de"),
        ["BtnGitHub"] = ("GitHub repository", "GitHub-Repository"),
        ["ThirdParty"] = ("Third-party components", "Komponenten von Drittanbietern"),
        ["ThirdPartyList"] = (
            ".NET (MIT) · Avalonia UI (MIT) · CommunityToolkit.Mvvm (MIT) · Inter (SIL OFL 1.1) · Material Design Icons (Apache-2.0)",
            ".NET (MIT) · Avalonia UI (MIT) · CommunityToolkit.Mvvm (MIT) · Inter (SIL OFL 1.1) · Material Design Icons (Apache-2.0)"),

        // ---------------------------------------------------------------- Footer
        ["Unsaved"] = ("Unsaved changes", "Ungespeicherte Änderungen"),
        ["Saved"] = ("Saved", "Gespeichert"),
        ["BtnDiscard"] = ("Discard", "Verwerfen"),
        ["BtnSave"] = ("Save", "Speichern"),
    };
}
