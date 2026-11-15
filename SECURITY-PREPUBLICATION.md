# Audyt przed publikacją — 15 listopada 2026

## Aktualizacja po zatwierdzonym oczyszczeniu historii

Po zgodzie użytkownika usunięto historyczne `appsettings.json` i `DystenSoftwateManagment.csproj.user` narzędziem git-filter-repo 2.47.0 w osobnym mirrorze. Bezpieczna konfiguracja oraz poprawki zostały przywrócone w nowym commicie. Sprawdzono również lokalną referencję drzewa Codex. Oczyszczone metadane Git zastosowano w katalogu projektu, zachowując robocze zmiany widoków urządzeń i lokalne dane.

Aktualna kopia odzyskiwania (zawiera stare sekrety): `C:\Users\Mikolaj\AppData\Local\Dysten-security-backups\history-clean-20261115-151122`. Zawiera bundle, pełne stare metadane Git oraz archiwum bieżących plików. Nie publikować jej.

Skan wszystkich referencji i reflogów oraz dokładne sprawdzenie wszystkich lokalnych obiektów względem historycznego hasła nie wykazały starego sekretu w oczyszczonej historii. Kompilacja i sześć grup testów integracyjnych na osobnej bazie przeszły. Po osobnej zgodzie wykonano force-with-lease gałęzi main na GitHubie. Świeży mirror z GitHuba zawiera wyłącznie oczyszczoną gałąź main; skan wszystkich referencji, reflogów i zapisanych obiektów nie wykrywa historycznego hasła. Brak innych gałęzi, tagów, pull requestów, forków, wydań i artefaktów workflow. Repozytorium pozostaje prywatne. **Istotne ograniczenie: GitHub nadal udostępnia stare commity i historyczny plik po dokładnych SHA przez uwierzytelnione API. Nie można jeszcze potwierdzić całkowitego usunięcia danych z serwera.** Przygotowano lokalny szkic zgłoszenia do GitHub Support (bez wartości hasła), lecz go nie wysłano. Potrzebna jest ocena Support dotycząca usunięcia cache i nieosiągalnych obiektów; Support może odmówić, jeśli ryzyko można usunąć rotacją hasła. Stare kopie odzyskiwania zawierają sekret poza repozytorium. Rotacja hasła i wyczyszczenie pozostałych klonów nadal są konieczne; potwierdzone usunięcie kopii serwerowych pozostaje warunkiem przed upublicznieniem.

Poniższe sekcje opisują audyt i pierwotny plan **sprzed oczyszczenia**; zapis o obecności sekretu odnosi się do starej historii i kopii odzyskiwania.

## Wykonane działania i granice

Usunięto login i hasło bootstrapu z bieżącego `appsettings.json`, wprowadzono wyłączony domyślnie bootstrap, User Secrets, bezpieczniejsze zasady Identity i sprawdzanie wyników inicjalizacji. `appsettings.Development.json` ma wyłącznie poziomy logowania i nie wymagał zmiany. Dodano właściwy connection string SQLite do konfiguracji aplikacji; usunięto nieskuteczną sekcję z `launchSettings.json`. Przeniesiono endpoint migracji do Development. `.gitignore` obejmuje konfigurację lokalną, sekrety, SQLite i pliki pomocnicze, backupy oraz artefakty narzędzi. Lokalny plik `.csproj.user` przestał być śledzony; plik na dysku pozostał.

Zachowano wcześniejsze zmiany użytkownika w `DevicePage.razor` i `DeviceSoftwaresPage.razor`. Nie wykonano commita, push, przepisywania historii ani zmiany widoczności repozytorium. Nie zmieniono rzeczywistej bazy danych, kont ani haseł.

## Odkrycia dotyczące danych i historii

Hasło jest w `appsettings.json:4` w commitach `3164dabf51ce` (początkowy) i `2f70ee9cdeb5` (aktualny HEAD). W obu drzewach jest ten sam blob konfiguracji `0ad80b307f68`. Przeskanowano również wszystkie osiągalne lokalne obiekty i reflogi, w tym usunięte pliki. Poza gałęzią `main` i `origin/main` stara historia jest osiągalna przez lokalną referencję `refs/codex/turn-diffs/captures/.../base`. Nie wolno uznać wyczyszczenia samej gałęzi głównej za wyczyszczenie całego lokalnego repozytorium.

`git ls-remote origin` podczas audytu wykazał tylko HEAD i `refs/heads/main`, wskazujące `2f70ee9cdeb5e53702fc6478c731d08cdf4cc783`. Nie daje to wglądu w cache GitHuba, ukryte referencje ani cudze klony.

Nie znaleziono śledzonych baz, backupów, archiwów danych ani kluczy prywatnych. Lokalnie istnieją `appDataBase.db` oraz pliki WAL/SHM — pozostawiono je bez odczytu danych aplikacyjnych i bez modyfikacji. W projekcie są logotypy i grafiki firmy; automatyczny skan nie ustala prawa do ich publikacji ani do publikacji kodu.

## Kopia bezpieczeństwa

Przed zmianami utworzono poza repozytorium:

`C:\Users\Mikolaj\AppData\Local\Dysten-security-backups\20261115-144227\`

- `before-security.bundle`: wszystkie ówczesne lokalne referencje i pełna osiągalna historia; `git bundle verify` zakończony pomyślnie.
- `working-files.zip`: pliki śledzone wraz z bieżącymi zmianami użytkownika, z zachowaniem ich treści.

**Ta kopia zawiera stare sekrety.** Jest lokalną kopią odzyskiwania, nie materiałem do publikacji. Ogranicz dostęp do tego katalogu. Nie jest kopią działającej bazy SQLite — baza pozostaje w dotychczasowej lokalizacji.

## Proponowane oczyszczenie historii — wymaga osobnej zgody

Proponuję zachować historię pozostałych plików, usunąć z całej historii `appsettings.json` i lokalny plik IDE, a następnie przywrócić oczyszczony `appsettings.json` w nowym commicie. Przy dwóch pierwotnych commitach jest to prostsze i bardziej odporne na warianty zapisu hasła niż podmiana jednego ciągu. Zmienią się identyfikatory commitów; starych gałęzi nie należy potem scalać do oczyszczonej historii.

Poniższe polecenia są **planem, nie zostały wykonane**. Przed ich użyciem trzeba zatwierdzić plan, przejrzeć i zacommitować bezpieczne zmiany bieżącego projektu (osobno rozstrzygnąć zmiany użytkownika), ponownie sprawdzić wszystkie referencje i utworzyć aktualny bundle oraz archiwum plików roboczych. Nie pracować destrukcyjnie w bieżącym checkoutcie.

Wymagania: Python 3, Git, `git-filter-repo` >= 2.47, dostępne polecenie `git filter-repo` w PATH:

```powershell
python -m pip install --user "git-filter-repo>=2.47"
git filter-repo --version
```

Po zgodzie i po bezpiecznym commicie, z katalogu oryginalnego projektu:

```powershell
$sourceRepo = (Get-Location).Path
$cleanRepo = Join-Path $env:LOCALAPPDATA ("Dysten-clean-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
# Fresh mirror includes ALL local refs, including Codex refs; avoid hardlinks.
git clone --mirror --no-local "$sourceRepo" "$cleanRepo.git"
# DESTRUCTIVE ONLY IN THE NEW COPY; all refs are processed by default.
git -C "$cleanRepo.git" filter-repo --sensitive-data-removal --invert-paths --path appsettings.json --path DystenSoftwateManagment.csproj.user
git clone "$cleanRepo.git" "$cleanRepo"
Copy-Item -LiteralPath (Join-Path $sourceRepo 'appsettings.json') -Destination (Join-Path $cleanRepo 'appsettings.json')
git -C "$cleanRepo" add -- appsettings.json
git -C "$cleanRepo" commit -m "Restore non-secret application configuration"
```

W razie odmowy narzędzia dotyczącej świeżego klona zatrzymać się i zbadać przyczynę; nie dopisywać automatycznie `--force`. `filter-repo` może usunąć `origin` jako zabezpieczenie. W żadnym z powyższych poleceń nie ma publikowania ani force push.

Po przepisaniu trzeba sprawdzić **zarówno mirror, jak i końcowy klon**. Skaner obsługuje katalog Git jako ROOT, więc można go użyć w mirrorze przez import:

```powershell
$env:DYSTEN_SCAN_REPO = "$cleanRepo.git"
$env:DYSTEN_BASELINE_REPO = "$sourceRepo"
python -c "import os,sys,runpy,pathlib; m=runpy.run_path('tests/scan_secrets.py'); m['main'].__globals__['ROOT']=pathlib.Path(os.environ['DYSTEN_SCAN_REPO']); sys.argv=['scan','--history','--baseline',os.environ['DYSTEN_BASELINE_REPO']]; sys.exit(m['main']())"
git -C "$cleanRepo.git" for-each-ref --format='%(refname) %(objectname)'
git -C "$cleanRepo.git" fsck --full
Push-Location "$cleanRepo"
python tests/scan_secrets.py --baseline "$sourceRepo"
python tests/scan_secrets.py --history --baseline "$sourceRepo"
git for-each-ref --format='%(refname) %(objectname)'
git fsck --full
dotnet build
python tests/security_startup.py
Pop-Location
Remove-Item Env:DYSTEN_SCAN_REPO
Remove-Item Env:DYSTEN_BASELINE_REPO
```

Opcja `--baseline` odczytuje stare hasła z chronionego oryginalnego repozytorium, aby sprawdzić ich dokładne wartości także po wyczyszczeniu historii kopii. Skan obejmuje bloby, komunikaty commitów i tagi osiągalne przez referencje i reflogi; nie wypisuje wartości. Nie obejmuje nieosiągalnych obiektów ani cache serwera. Sprawdzić listę ścieżek wszystkich drzew pod kątem baz, archiwów i wcześniejszych nazw poufnych plików. Jeśli potrzebne jest usuwanie starych lokalnych referencji/reflogów/obiektów, wykonać je dopiero po weryfikacji nowej kopii i osobnej zgodzie, z zachowaniem chronionej kopii odzyskiwania.

Następnie przedstawić nowe SHA, listę zmienionych referencji i wynik skanu do zatwierdzenia. Dopiero oddzielna zgoda może pozwolić na aktualizację odpowiednich gałęzi/tagów na GitHubie; nie używać bezrefleksyjnie `push --mirror` (wysłałby m.in. lokalne referencje narzędzi). Po ewentualnej aktualizacji ponownie sklonować repozytorium z GitHuba i przeskanować wszystkie pobrane referencje; stare klony trzeba wymienić. Cache i referencje pull requestów mogą wymagać procedury GitHub Support. Stare kopie i backupy nadal zawierają ujawnione hasło, dlatego jego rotacja jest konieczna niezależnie od oczyszczenia historii.

Dokumentacja narzędzia: [git-filter-repo](https://github.com/newren/git-filter-repo/blob/main/Documentation/git-filter-repo.txt).

## Wyniki weryfikacji

- `dotnet build`: powodzenie, 0 błędów, 117 ostrzeżeń; przed zmianą również 117 ostrzeżeń.
- `dotnet test --no-restore`: polecenie zakończone poprawnie, ale brak projektu testowego .NET — nie jest to wynik wykonania testów jednostkowych.
- `python tests/security_startup.py`: sześć grup testów integracyjnych przeszło, w tym logowanie nowego konta i logowanie pierwotnym hasłem po restarcie ze zmienioną konfiguracją.
- Bieżący skan plików: bez wskazań. Skan historii nadal wykrywa hasło w historycznym blobie konfiguracji (dokładna wartość i heurystyka wskazują ten sam sekret).
- `git diff --check` dla zmienionych przeze mnie plików i indeksu: bez błędów. Pełne sprawdzenie wykazuje wcześniejsze końcowe spacje w `DeviceSoftwaresPage.razor:143-145`; nie zmieniano pliku użytkownika.
- Niezależny przegląd statyczny zmian nie zgłosił problemów krytycznych ani istotnych; nie zastępuje wykonanych testów ani pełnego audytu.

## Pozostałe problemy przed wystawieniem aplikacji w sieci

- Projekt używa `net9.0`, którego wsparcie zakończyło się 10 listopada 2026. Przed hostowaniem wymagana migracja do wspieranej wersji: [polityka wsparcia .NET](https://dotnet.microsoft.com/en-us/platform/support/policy). W tej zmianie nie zmieniano platformy docelowej.
- Trzy endpointy `/download/...` w `Program.cs` nie mają `RequireAuthorization`; anonimowy klient może żądać plików po identyfikatorze. Autoryzacja widoków Blazor nie chroni tych endpointów. Wymaga to poprawki i testów dostępu przed hostowaniem z rzeczywistymi dokumentami.
- Import bazy w `DataBaseManager` interpoluje ścieżkę do SQL, usuwa i kopiuje tabele bez transakcji. Nie korzystać z niezaufanych plików; poprawa tego biznesowego przepływu pozostaje poza zmianą bootstrapu.
- Connection string EF Core i ścieżki importu/eksportu/backupów są niezależne — ograniczenie opisane w README.
- Logowanie nie włącza `lockoutOnFailure`; przed publicznym hostowaniem potrzebna jest ocena ochrony przed zgadywaniem haseł.
- `dotnet list package --vulnerable --include-transitive` zgłosił: `Microsoft.Build 17.10.4` (High), `SQLitePCLRaw.lib.e_sqlite3 2.1.10` (High), `NuGet.Packaging 6.11.0` i `NuGet.Protocol 6.11.0` (Low). Wymagana osobna, sprawdzona aktualizacja zależności; sam wynik skanera nie dowodzi wykorzystania podatności przez HTTP.
- Porady: [Microsoft.Build](https://github.com/advisories/GHSA-w3q9-fxm7-j8fq), [SQLite](https://github.com/advisories/GHSA-2m69-gcr7-jv3q), [NuGet](https://github.com/advisories/GHSA-g4vj-cjjj-v7hg).

Nie przeprowadzono pełnego audytu aplikacji, testów penetracyjnych ani przeglądu wszystkich ekranów biznesowych. Wynik skanu sekretów nie jest zgodą na publikację. Przed publikacją wymagane są rotacja ujawnionego hasła (i wszelkich miejsc jego ponownego użycia), oczyszczenie historii i potwierdzona zgoda właściciela kodu oraz materiałów firmy.
