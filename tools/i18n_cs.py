# (plik, stary tekst, nowy tekst) + nowe klucze (klucz -> (pl, en))
K = {}
def k(key, pl, en): K[key] = (pl, en); return key
E = []
def r(f, old, new): E.append((f, old, new))

VM = 'src/ArcSperanzaIntel.App/ViewModels/'
# ---- Common
r(VM+'Common.cs', '''        QuestStatus.Done => "UKOŃCZONY",
        QuestStatus.Available => "DOSTĘPNY",
        _ => "ZABLOKOWANY",''', f'''        QuestStatus.Done => Loc.T("{k("StatusDoneUpper","UKOŃCZONY","COMPLETED")}"),
        QuestStatus.Available => Loc.T("{k("StatusAvailableUpper","DOSTĘPNY","AVAILABLE")}"),
        _ => Loc.T("{k("StatusLockedUpper","ZABLOKOWANY","LOCKED")}"),''')
r(VM+'Common.cs', '''IsDone ? "Cofnij" : "Zrobione";''', f'''IsDone ? Loc.T("{k("Undo","Cofnij","Undo")}") : Loc.T("{k("Done","Zrobione","Done")}");''')
# ---- Settings
S = VM+'SettingsViewModel.cs'
r(S, '"Skrót globalny potrzebuje Ctrl / Alt / Shift (albo klawisza F1–F24), żeby nie blokować pisania."', f'Loc.T("{k("GlobalNeedsModifier","Skrót globalny potrzebuje Ctrl / Alt / Shift (albo klawisza F1–F24), żeby nie blokować pisania.","A global shortcut needs Ctrl / Alt / Shift (or an F1–F24 key) so it doesn't block typing.")}")')
r(S, '"Przywrócono domyślne skróty"', f'Loc.T("{k("KeybindsRestored","Przywrócono domyślne skróty","Default shortcuts restored")}")')
r(S, '"Dane odświeżone. Nowe questy i itemy pojawią się po ponownym uruchomieniu."', f'Loc.T("{k("DataRefreshed","Dane odświeżone. Nowe questy i itemy pojawią się po ponownym uruchomieniu.","Data refreshed. New quests and items will appear after a restart.")}")')
r(S, '''            "Reset aplikacji",
            "Usunie postęp questów, subtaski, pinezki, ulubione, szukane itemy, ustawienia i cache. Aplikacja uruchomi się od nowa. Tego nie da się cofnąć.",
            "Resetuj wszystko", danger: true);''', f'''            Loc.T("ResetApp"),
            Loc.T("{k("ResetAppConfirm","Usunie postęp questów, subtaski, pinezki, ulubione, szukane itemy, ustawienia i cache. Aplikacja uruchomi się od nowa. Tego nie da się cofnąć.","This deletes quest progress, subtasks, pins, favourites, wanted items, settings and cache. The app will restart. This cannot be undone.")}"),
            Loc.T("{k("ResetEverything","Resetuj wszystko","Reset everything")}"), danger: true);''')
r(S, '''_main.ConfirmAsync("Reset questów", "Odznaczy wszystkie questy i subtaski. Reszta zostaje.", "Resetuj questy", danger: true);''',
     f'''_main.ConfirmAsync(Loc.T("{k("ResetQuestsTitle","Reset questów","Reset quests")}"), Loc.T("{k("ResetQuestsConfirm","Odznaczy wszystkie questy i subtaski. Reszta zostaje.","Unchecks all quests and subtasks. Everything else stays.")}"), Loc.T("ResetQuests"), danger: true);''')
r(S, '"Postęp questów wyczyszczony"', f'Loc.T("{k("QuestsCleared","Postęp questów wyczyszczony","Quest progress cleared")}")')
r(S, '''IsCapturing ? "Naciśnij skrót…  (Esc = anuluj)" : GestureText;''', f'''IsCapturing ? Loc.T("{k("PressShortcut","Naciśnij skrót…  (Esc = anuluj)","Press a shortcut…  (Esc = cancel)")}") : GestureText;''')
# ---- Main
M = VM+'MainViewModel.cs'
r(M, '_loadingText = "Uruchamianie…";', f'_loadingText = Loc.T("{k("Starting","Uruchamianie…","Starting…")}");')
r(M, 'LoadingText = "Pobieranie questów, itemów i handlarzy z MetaForge…";', f'LoadingText = Loc.T("{k("LoadingCore","Pobieranie questów, itemów i handlarzy z MetaForge…","Downloading quests, items and traders from MetaForge…")}");')
r(M, 'LoadingText = "Nie udało się pobrać danych z MetaForge. Sprawdź internet i uruchom aplikację ponownie.";', f'LoadingText = Loc.T("{k("LoadFailed","Nie udało się pobrać danych z MetaForge. Sprawdź internet i uruchom aplikację ponownie.","Could not download data from MetaForge. Check your connection and restart the app.")}");')
r(M, 'LoadingText = "Błąd uruchamiania: " + ex.Message;', f'LoadingText = Loc.T("{k("StartError","Błąd uruchamiania: ","Startup error: ")}") + ex.Message;')
r(M, '''$"  (+{change.AlsoChanged.Count} wcześniejszych)"''', f'''Loc.F("{k("PlusEarlier","  (+{{0}} wcześniejszych)","  (+{{0}} earlier)")}", change.AlsoChanged.Count)''')
r(M, '''Toast("↺ Odznaczono: " + q.Name + (change.AlsoChanged.Count > 0 ? $"  (+{change.AlsoChanged.Count} zależnych)" : ""));''',
     f'''Toast(Loc.T("{k("Unchecked","↺ Odznaczono: ","↺ Unchecked: ")}") + q.Name + (change.AlsoChanged.Count > 0 ? Loc.F("{k("PlusDependent","  (+{{0}} zależnych)","  (+{{0}} dependent)")}", change.AlsoChanged.Count) : ""));''')
r(M, 'string initial = "", string confirmText = "Zapisz")', 'string initial = "", string? confirmText = null)')
# ---- Maps
P = VM+'MapsViewModel.cs'
r(P, '''        [MarkerCategories.Quests] = "Questy",
        [MarkerCategories.Locations] = "Lokacje",
        [MarkerCategories.Arc] = "ARC",
        [MarkerCategories.Containers] = "Kontenery",
        [MarkerCategories.Events] = "Wydarzenia",
        [MarkerCategories.Nature] = "Natura",
        ["custom"] = "Moje",''', f'''        [MarkerCategories.Quests] = "{k("CatQuests","Questy","Quests")}",
        [MarkerCategories.Locations] = "{k("CatLocations","Lokacje","Locations")}",
        [MarkerCategories.Arc] = "{k("CatArc","ARC","ARC")}",
        [MarkerCategories.Containers] = "{k("CatContainers","Kontenery","Containers")}",
        [MarkerCategories.Events] = "{k("CatEvents","Wydarzenia","Events")}",
        [MarkerCategories.Nature] = "{k("CatNature","Natura","Nature")}",
        ["custom"] = "{k("CatMine","Moje","Mine")}",''')
r(P, '''FilterGroupViewModel(MarkerCategories.Quests, CategoryTitles[MarkerCategories.Quests],''', '''FilterGroupViewModel(MarkerCategories.Quests, Loc.T(CategoryTitles[MarkerCategories.Quests]),''')
r(P, '''CategoryTitles.GetValueOrDefault(cat) ?? TextFormat.Pretty(cat),''', '''CategoryTitles.GetValueOrDefault(cat) is { } titleKey ? Loc.T(titleKey) : TextFormat.Pretty(cat),''')
r(P, '''FilterGroupViewModel("custom", CategoryTitles["custom"],''', '''FilterGroupViewModel("custom", Loc.T(CategoryTitles["custom"]),''')
r(P, '''(QuestAvailableKey, "Dostępne"), (QuestLockedKey, "Zablokowane"), (QuestDoneKey, "Ukończone"),''',
     f'''(QuestAvailableKey, Loc.T("{k("FilterAvailable","Dostępne","Available")}")), (QuestLockedKey, Loc.T("{k("FilterLocked","Zablokowane","Locked")}")), (QuestDoneKey, Loc.T("{k("FilterDone","Ukończone","Completed")}")),''')
r(P, '''new FilterItemViewModel(PinsKey, "Moje pinezki",''', f'''new FilterItemViewModel(PinsKey, Loc.T("{k("MyPins","Moje pinezki","My pins")}"),''')
r(P, '''_main.PromptAsync("Nowa pinezka", "Notatka do tego miejsca (np. „dobry spot na baterie”).", "", "Dodaj");''',
     f'''_main.PromptAsync(Loc.T("{k("NewPin","Nowa pinezka","New pin")}"), Loc.T("{k("NewPinHelp","Notatka do tego miejsca (np. „dobry spot na baterie”).","A note for this spot (e.g. “good battery spot”).")}"), "", Loc.T("{k("Add","Dodaj","Add")}"));''')
r(P, '''_main.Toast("Dodano pinezkę", ToastKind.Ok);''', f'''_main.Toast(Loc.T("{k("PinAdded","Dodano pinezkę","Pin added")}"), ToastKind.Ok);''')
r(P, '''_main.PromptAsync("Edytuj pinezkę", "", pin.Note);''', f'''_main.PromptAsync(Loc.T("{k("EditPin","Edytuj pinezkę","Edit pin")}"), "", pin.Note);''')
r(P, '''_main.Toast("MetaForge nie ma jeszcze markerów tego questu na tej mapie — sprawdź opis celów.");''', f'''_main.Toast(Loc.T("{k("NoQuestMarkers","MetaForge nie ma jeszcze markerów tego questu na tej mapie — sprawdź opis celów.","MetaForge has no markers for this quest on this map yet — check the objectives.")}"));''')
r(P, '''(marker.Name ?? "Miejsce questu")''', f'''(marker.Name ?? Loc.T("{k("QuestSpot","Miejsce questu","Quest location")}"))''')
r(P, '''IsFavorite ? "★ Ulubione" : "☆ Ulubione";''', f'''IsFavorite ? Loc.T("{k("FavOn","★ Ulubione","★ Favourite")}") : Loc.T("{k("FavOff","☆ Ulubione","☆ Favourite")}");''')
r(P, '''status == QuestStatus.Done ? "Odznacz quest" : "Ukończ cały quest";''', f'''status == QuestStatus.Done ? Loc.T("{k("UncheckQuest","Odznacz quest","Uncheck quest")}") : Loc.T("{k("CompleteWholeQuest","Ukończ cały quest","Complete whole quest")}");''')
r(P, '''? "(bez notatki)" : pin.Note;''', f'''? Loc.T("{k("NoNote","(bez notatki)","(no note)")}") : pin.Note;''')
r(P, '''    public string Name { get; } = floor.Name;''', '''    public string Name { get; } = Loc.T(floor.Name);''')
# ---- Quests
Q = VM+'QuestsViewModel.cs'
r(Q, '''CountsText = $"dostępne {available} · zablokowane {locked}";''', f'''CountsText = Loc.F("{k("QuestCounts","dostępne {{0}} · zablokowane {{1}}","available {{0}} · locked {{1}}")}", available, locked);''')
r(Q, '''IsDone ? "↺ Odznacz quest" : "✔ Ukończ quest";''', f'''IsDone ? Loc.T("{k("UncheckQuestBtn","↺ Odznacz quest","↺ Uncheck quest")}") : Loc.T("{k("CompleteQuestBtn","✔ Ukończ quest","✔ Complete quest")}");''')
r(Q, '''_main.Toast("Ten quest nie ma przypisanej mapy w danych MetaForge.");''', f'''_main.Toast(Loc.T("{k("QuestNoMap","Ten quest nie ma przypisanej mapy w danych MetaForge.","This quest has no map in MetaForge data.")}"));''')
r(Q, '''hasMarkers ? "markery na mapie" : "wspomniana w celach";''', f'''hasMarkers ? Loc.T("{k("HintMarkers","markery na mapie","markers on the map")}") : Loc.T("{k("HintMentioned","wspomniana w celach","mentioned in objectives")}");''')
# ---- Items
I = VM+'ItemsViewModel.cs'
r(I, '''        Rarities = items.Select(i => i.Rarity ?? "Brak").Distinct()
            .OrderBy(r => Palette.RarityRank(r == "Brak" ? null : r))
            .Select(r => new RarityFilterViewModel(r, Palette.Rarity(r == "Brak" ? null : r), this))''', '''        Rarities = items.Select(i => i.Rarity ?? NoRarity).Distinct()
            .OrderBy(r => Palette.RarityRank(r == NoRarity ? null : r))
            .Select(r => new RarityFilterViewModel(r, Palette.Rarity(r == NoRarity ? null : r), this))''')
r(I, '''activeRarities.Contains(c.Item.Rarity ?? "Brak")''', '''activeRarities.Contains(c.Item.Rarity ?? NoRarity)''')
r(I, '''CountText = $"{total} itemów";''', f'''CountText = Loc.F("{k("ItemCount","{{0}} itemów","{{0}} items")}", total);''')
r(I, '''{(t.Currency == TraderCurrency.Seeds ? "nasion" : "monet")}''', f'''{{(t.Currency == TraderCurrency.Seeds ? Loc.T("{k("SeedsWord","nasion","seeds")}") : Loc.T("{k("CoinsWord","monet","coins")}"))}}''')
r(I, '''IsFavorite ? "★ W ulubionych" : "☆ Dodaj do ulubionych";''', f'''IsFavorite ? Loc.T("{k("InFavorites","★ W ulubionych","★ In favourites")}") : Loc.T("{k("AddFavorite","☆ Dodaj do ulubionych","☆ Add to favourites")}");''')
r(I, '''IsMarked ? "✔ W szukanych (Marked By Me)" : "+ Dodaj do szukanych";''', f'''IsMarked ? Loc.T("{k("InWanted","✔ W szukanych (Marked By Me)","✔ Wanted (Marked By Me)")}") : Loc.T("{k("AddWanted","+ Dodaj do szukanych","+ Add to wanted")}");''')
r(I, '''_main.Toast($"Dodano do szukanych: {Item.Name}", ToastKind.Ok);''', f'''_main.Toast(Loc.T("{k("AddedToWanted","Dodano do szukanych: ","Added to wanted: ")}") + Item.Name, ToastKind.Ok);''')
r(I, '''public sealed partial class RarityFilterViewModel(string name, Color color, ItemsViewModel owner) : ObservableObject
{
    public string Name { get; } = name;''', '''public sealed partial class RarityFilterViewModel(string name, Color color, ItemsViewModel owner) : ObservableObject
{
    public string Name { get; } = name == ItemsViewModel.NoRarity ? Loc.T("None") : name;''')
# ---- Events
V = VM+'EventsViewModel.cs'
r(V, '''    private static readonly CultureInfo Pl = CultureInfo.GetCultureInfo("pl-PL");
''', '''    private static CultureInfo Pl => Loc.Culture;
''')
r(V, '''new EventMapFilterViewModel(AllMaps, "Wszystkie mapy", this)''', f'''new EventMapFilterViewModel(AllMaps, Loc.T("{k("AllMaps","Wszystkie mapy","All maps")}"), this)''')
r(V, '''            ? $"Harmonogram MetaForge (Europa), pobrany {t.ToLocalTime().ToString("dd.MM HH:mm", Pl)}. Godziny w Twoim czasie."
            : "Harmonogram MetaForge (Europa).";''', f'''            ? Loc.F("{k("EventsSource","Harmonogram MetaForge (Europa), pobrany {{0}}. Godziny w Twoim czasie.","MetaForge schedule (Europe), fetched {{0}}. Times are in your time zone.")}", t.ToLocalTime().ToString("dd.MM HH:mm", Pl))
            : Loc.T("{k("EventsSourceShort","Harmonogram MetaForge (Europa).","MetaForge schedule (Europe).")}");''')
r(V, '''local.Date == today ? "dziś" : local.Date == today.AddDays(1) ? "jutro" :''', f'''local.Date == today ? Loc.T("{k("Today","dziś","today")}") : local.Date == today.AddDays(1) ? Loc.T("{k("Tomorrow","jutro","tomorrow")}") :''')
r(V, '''            ? "kończy się za " + EventsViewModel.Span(e.End - now)
            : "za " + EventsViewModel.Span(e.Start - now);''', f'''            ? Loc.F("{k("EndsIn","kończy się za {{0}}","ends in {{0}}")}", EventsViewModel.Span(e.End - now))
            : Loc.F("{k("StartsIn","za {{0}}","in {{0}}")}", EventsViewModel.Span(e.Start - now));''')
r(V, '''    public string InText { get; } = "za " + EventsViewModel.Span(start - now);''', '''    public string InText { get; } = Loc.F("StartsIn", EventsViewModel.Span(start - now));''')
# ---- Shops
H = VM+'ShopsViewModel.cs'
r(H, '''        TotalText = $"{Traders.Sum(t => t.Offers.Count)} ofert u {Traders.Count} handlarzy"
                    + (Ermal is null ? "" : $" + {Ermal.Offers.Count} u Ermala");''', f'''        TotalText = Loc.F("{k("TradersTotal","{{0}} ofert u {{1}} handlarzy","{{0}} offers from {{1}} traders")}", Traders.Sum(t => t.Offers.Count), Traders.Count)
                    + (Ermal is null ? "" : Loc.F("{k("PlusErmal"," + {{0}} u Ermala"," + {{0}} from Ermal")}", Ermal.Offers.Count));''')
r(H, '''CountText = $"{Offers.Count} itemów";''', '''CountText = Loc.F("ItemCount", Offers.Count);''')
r(H, '''IsSeeds ? "płaci się nasionami (Assorted Seeds)" : "płaci się monetami";''', f'''IsSeeds ? Loc.T("{k("PaySeeds","płaci się nasionami (Assorted Seeds)","paid in seeds (Assorted Seeds)")}") : Loc.T("{k("PayCoins","płaci się monetami","paid in coins")}");''')
r(H, '''seeds ? "Nasiona (Assorted Seeds)" : "Monety";''', f'''seeds ? Loc.T("{k("SeedsTip","Nasiona (Assorted Seeds)","Seeds (Assorted Seeds)")}") : Loc.T("{k("CoinsTip","Monety","Coins")}");''')
r(H, '''        "Wędrowny handlarz, od 25 poziomu. Ofertę kupujesz monetami albo oddając przedmioty o łącznej wartości równej cenie.";''',
     f'''        Loc.T("{k("ErmalInfo","Wędrowny handlarz, od 25 poziomu. Ofertę kupujesz monetami albo oddając przedmioty o łącznej wartości równej cenie.","Travelling trader, from level 25. Pay with coins or hand in items whose total value matches the price.")}");''')
r(H, '''CountdownText = "TYGODNIOWA ROTACJA";''', f'''CountdownText = Loc.T("{k("WeeklyRotation","TYGODNIOWA ROTACJA","WEEKLY ROTATION")}");''')
r(H, '''            ? "ROTACJA SIĘ ZMIENIŁA — ODŚWIEŻ DANE"
            : $"NOWA ROTACJA ZA {(int)left.TotalDays}d {left.Hours}h {left.Minutes}min";''', f'''            ? Loc.T("{k("RotationChanged","ROTACJA SIĘ ZMIENIŁA — ODŚWIEŻ DANE","ROTATION CHANGED — REFRESH DATA")}")
            : Loc.F("{k("NextRotation","NOWA ROTACJA ZA {{0}}d {{1}}h {{2}}min","NEW ROTATION IN {{0}}d {{1}}h {{2}}min")}", (int)left.TotalDays, left.Hours, left.Minutes);''')
r(H, '''        AcceptedHeader = $"PRZYJMUJE ({Accepted.Count})";
        _itemId''', f'''        AcceptedHeader = Loc.F("{k("Accepts","PRZYJMUJE ({{0}})","ACCEPTS ({{0}})")}", Accepted.Count);
        _itemId''')
r(H, '''        AcceptedHeader = $"PRZYJMUJE ({Accepted.Count})";
    }''', '''        AcceptedHeader = Loc.F("Accepts", Accepted.Count);
    }''')
r(H, '''item.IsCategory ? "Dowolny przedmiot z tej kategorii" :''', f'''item.IsCategory ? Loc.T("{k("AnyOfCategory","Dowolny przedmiot z tej kategorii","Any item of this category")}") :''')
# ---- Services / Controls / Core
r('src/ArcSperanzaIntel.App/Services/Keybinds.cs', '''        KeybindAction.ToggleWindow => "Pokaż / schowaj okno (działa też w grze)",
        KeybindAction.TabMaps => "Zakładka Mapy",
        KeybindAction.TabQuests => "Zakładka Questy",
        KeybindAction.TabItems => "Zakładka Itemy",
        KeybindAction.TabShops => "Zakładka Handlarze",
        KeybindAction.TabEvents => "Zakładka Wydarzenia",
        KeybindAction.TabSettings => "Ustawienia",''', f'''        KeybindAction.ToggleWindow => Loc.T("{k("KbToggle","Pokaż / schowaj okno (działa też w grze)","Show / hide window (works in game too)")}"),
        KeybindAction.TabMaps => Loc.T("{k("KbMaps","Zakładka Mapy","Maps tab")}"),
        KeybindAction.TabQuests => Loc.T("{k("KbQuests","Zakładka Questy","Quests tab")}"),
        KeybindAction.TabItems => Loc.T("{k("KbItems","Zakładka Itemy","Items tab")}"),
        KeybindAction.TabShops => Loc.T("{k("KbTraders","Zakładka Handlarze","Traders tab")}"),
        KeybindAction.TabEvents => Loc.T("{k("KbEvents","Zakładka Wydarzenia","Events tab")}"),
        KeybindAction.TabSettings => Loc.T("Settings"),''')
r('src/ArcSperanzaIntel.App/Services/Keybinds.cs', '''if (g is null) return "brak";''', f'''if (g is null) return Loc.T("{k("NoShortcut","brak","none")}");''')
G = 'src/ArcSperanzaIntel.App/Services/GlobalHotkey.cs'
r(G, '''Task.FromResult<string?>("Skróty globalne działają tylko w Windows.");''', f'''Task.FromResult<string?>(Loc.T("{k("HotkeyWindowsOnly","Skróty globalne działają tylko w Windows.","Global shortcuts only work on Windows.")}"));''')
r(G, '''Task.FromResult<string?>($"Klawisz {gesture.Key} nie jest obsługiwany jako skrót globalny.");''', f'''Task.FromResult<string?>(Loc.F("{k("HotkeyUnsupported","Klawisz {{0}} nie jest obsługiwany jako skrót globalny.","Key {{0}} is not supported as a global shortcut.")}", gesture.Key));''')
r(G, '''tcs.TrySetResult("Ten skrót jest już zajęty przez inny program. Wybierz inny w Ustawieniach.");''', f'''tcs.TrySetResult(Loc.T("{k("HotkeyTaken","Ten skrót jest już zajęty przez inny program. Wybierz inny w Ustawieniach.","This shortcut is already used by another program. Pick another one in Settings.")}"));''')
r('src/ArcSperanzaIntel.App/Controls/MapCanvas.cs', '''? "Moja pinezka" : _hoverPin.Note;''', f'''? Loc.T("{k("MyPinLabel","Moja pinezka","My pin")}") : _hoverPin.Note;''')
D = 'src/ArcSperanzaIntel.Core/Api/GameData.cs'
r(D, '''        var quests = Guard(source.GetQuestsAsync(force, ct), "questy");
        var items = Guard(source.GetItemsAsync(force, ct), "itemy");
        var traders = Guard(source.GetTradersAsync(force, ct), "handlarzy");''', f'''        var quests = Guard(source.GetQuestsAsync(force, ct), Loc.T("{k("WhatQuests","questy","quests")}"));
        var items = Guard(source.GetItemsAsync(force, ct), Loc.T("{k("WhatItems","itemy","items")}"));
        var traders = Guard(source.GetTradersAsync(force, ct), Loc.T("{k("WhatTraders","handlarzy","traders")}"));''')
r(D, '''SourceInfo = saved is null ? "brak danych" : $"dane MetaForge z {saved.Value.ToLocalTime():dd.MM HH:mm}";''', f'''SourceInfo = saved is null ? Loc.T("{k("NoData","brak danych","no data")}") : Loc.F("{k("DataFrom","dane MetaForge z {{0:dd.MM HH:mm}}","MetaForge data from {{0:dd.MM HH:mm}}")}", saved.Value.ToLocalTime());''')
r(D, '''Guard(source.GetEventsAsync(force, ct), "wydarzenia")''', f'''Guard(source.GetEventsAsync(force, ct), Loc.T("{k("WhatEvents","wydarzenia","events")}"))''')
r(D, '''"markery " + MapCatalog.Get(mapId).ShortName''', f'''Loc.T("{k("WhatMarkers","markery ","markers ")}") + MapCatalog.Get(mapId).ShortName''')
r(D, '''$"Brak połączenia — {what} z zapisanych danych."''', f'''Loc.F("{k("OfflineData","Brak połączenia — {{0}} z zapisanych danych.","Offline — {{0}} from saved data.")}", what)''')
r(D, '''$"Nie udało się pobrać: {what} ({ex.Message})"''', f'''Loc.F("{k("FetchFailed","Nie udało się pobrać: {{0}} ({{1}})","Could not download: {{0}} ({{1}})")}", what, ex.Message)''')
C = 'src/ArcSperanzaIntel.Core/Maps/MapCatalog.cs'
r(C, '"Powierzchnia", 2, Static + "spaceport', f'"{k("FloorSurface","Powierzchnia","Surface")}", 2, Static + "spaceport')
r(C, '"Tunele", 1,', f'"{k("FloorTunnels","Tunele","Tunnels")}", 1,')
r(C, '"Powierzchnia", 2, Static + "blue-gate', '"FloorSurface", 2, Static + "blue-gate')
r(C, '"Podziemia", 1,', f'"{k("FloorUnderground","Podziemia","Underground")}", 1,')
r(C, '"Górne piętro", 2,', f'"{k("FloorTop","Górne piętro","Top floor")}", 2,')
r(C, '"Dolne piętro", 1,', f'"{k("FloorBottom","Dolne piętro","Bottom floor")}", 1,')
r('src/ArcSperanzaIntel.Core/Api/MetaForgeParser.cs', '''?? "Inne",''', '''?? "Other",''')
r('src/ArcSperanzaIntel.Core/Api/MetaForgeParser.cs', '''JsonValueKind.True => "Tak",''', '''JsonValueKind.True => "Yes",''')
r('src/ArcSperanzaIntel.Core/Models/Item.cs', '''= "Inne";''', '''= "Other";''')
