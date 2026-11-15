# Dysten Software Management

Aplikacja Blazor Server do zarządzania urządzeniami i oprogramowaniem, oparta na ASP.NET Core Identity, EF Core i SQLite. Kod i materiały oznaczone marką firmy wymagają zgody właściciela przed publicznym udostępnieniem. Repozytorium nie zawiera przykładowej bazy ani konta z domyślnym hasłem.

## Uruchomienie od zera

**Uwaga:** .NET 9 zakończył wsparcie 10 listopada 2026. Poniższa instrukcja odtwarza obecną aplikację; przed hostowaniem potrzebna jest migracja do wspieranej wersji ([polityka Microsoft](https://dotnet.microsoft.com/en-us/platform/support/policy)).

Wymagania: SDK .NET obsługujące `net9.0` (weryfikacja: SDK 10.0.401), runtime ASP.NET Core 9, Git oraz narzędzie `dotnet-ef` w wersji 9.0.5. Instalacja narzędzia, jeśli go nie ma:

```powershell
dotnet tool install --global dotnet-ef --version 9.0.5
dotnet restore
dotnet build
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet ef database update
dotnet run --launch-profile https
```

Profil HTTPS używa adresu `https://localhost:7229/Account/Login`. Jeśli potrzeba, zaufaj lokalnemu certyfikatowi poleceniem `dotnet dev-certs https --trust`.

Domyślna niesekretna konfiguracja to `ConnectionStrings:DefaultConnection=Data Source=appDataBase.db`. Polecenia wykonuj w katalogu projektu. Migracje tworzą pusty schemat; aplikacja nie wykonuje ich automatycznie. Nie używaj `EnsureDeleted`, nie kasuj istniejącej bazy. Przed migracją rzeczywistej instalacji wykonaj spójny backup SQLite. Użytkownik procesu musi mieć prawo zapisu do katalogu bazy i `Backups`.

**Ograniczenie istniejącej implementacji:** ręczny import, eksport i backup używają nazwy `appDataBase.db` względem katalogu roboczego. Nadpisanie connection stringa zmienia połączenie EF Core, ale nie te operacje. Do normalnego uruchomienia zachowaj domyślną nazwę i katalog roboczy równy katalogowi projektu; nie używaj funkcji administracji bazą przy innym położeniu bazy. Ujednolicenie tych ścieżek pozostaje osobnym zadaniem.

## Opcjonalne pierwsze konto administratora

Domyślnie `RootUser:Enabled=false`. Start nie wymaga loginu ani hasła administratora. Role `Admin`, `Developer`, `User` są inicjalizowane niezależnie od tej flagi. Bez żadnego konta można wyświetlić stronę logowania, ale do pracy w aplikacji potrzebne jest konto utworzone przez administratora lub bootstrap.

Projekt ma `UserSecretsId`; `WebApplication.CreateBuilder` automatycznie czyta User Secrets w środowisku Development. Ustaw własną nazwę konta i nowe, unikalne hasło poza repozytorium:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet user-secrets set "RootUser:UserName" "<WYBRANY_LOGIN>"
dotnet user-secrets set "RootUser:Password" "<NOWE_UNIKALNE_HASLO>"
dotnet user-secrets set "RootUser:Enabled" "true"
dotnet run --launch-profile https
```

Wartości w nawiasach są placeholderami, a nie działającymi danymi konta. Nie wpisuj rzeczywistych sekretów w skrypty wersjonowane w Git. Hasło przekazane jako argument może trafić do historii powłoki; można zamiast tego użyć lokalnego edytora Manage User Secrets. User Secrets przechowuje dane poza repozytorium, ale **nie szyfruje** ich: chroń konto systemowe i uprawnienia pliku.

Po utworzeniu konta wyłącz inicjalizację i usuń przechowywane hasło bootstrapu:

```powershell
dotnet user-secrets set "RootUser:Enabled" "false"
dotnet user-secrets remove "RootUser:Password"
```

Konto otrzymuje role `Admin` i `Developer`. Niepełna konfiguracja przy włączonej inicjalizacji kończy start czytelnym błędem. Nieprawidłowe hasło kończy start kodami błędów Identity bez jego wartości. Tworzenie konta i przypisanie ról odbywają się w transakcji. Istniejący administrator nie jest zmieniany; inne konto o wybranej nazwie nie jest automatycznie awansowane i wymaga ręcznej weryfikacji.

Polityka nowego/zmienianego hasła: minimum 12 znaków, minimum 4 różne znaki, mała i wielka litera, cyfra i znak spoza liter/cyfr. Standardowy hasher Identity pozostaje bez zmian. Nowa polityka nie weryfikuje ponownie haseł podczas logowania, nie blokuje istniejących kont ani nie wymusza resetu. **Ujawnione wcześniej hasło trzeba jednak zmienić ręcznie w działającej instalacji**; zmiana konfiguracji bootstrapu nie rotuje hasła istniejącego konta.

## Serwer

Używaj zmiennych środowiskowych procesu albo secret managera platformy. Nazwy kluczy:

| Zmienna | Znaczenie |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__DefaultConnection` | `Data Source=appDataBase.db` przy obecnym ograniczeniu ścieżek |
| `RootUser__Enabled` | `true` tylko na czas świadomej inicjalizacji; później `false` |
| `RootUser__UserName` | Wybrana nazwa nowego administratora |
| `RootUser__Password` | Nowe hasło z magazynu sekretów |

User Secrets służy lokalnemu developmentowi. Zmienne środowiskowe nadpisują pliki JSON i User Secrets. Sam plik `.env` nie jest automatycznie czytany przez ASP.NET Core; `.gitignore` jedynie chroni go przed przypadkowym dodaniem. W produkcji skonfiguruj HTTPS, ogranicz dostęp do konfiguracji, danych i logów, ustaw odpowiednie `AllowedHosts`. Endpoint migracji dostępny jest wyłącznie w Development.

Źródło: [konfiguracja sekretów ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/app-secrets).

## Weryfikacja

Repozytorium wcześniej nie miało projektu testowego. Dodano test integracyjny bez zewnętrznego frameworka (Python 3 i `dotnet-ef` 9.0.5):

```powershell
dotnet build
python tests/security_startup.py
python tests/scan_secrets.py
python tests/scan_secrets.py --history
dotnet list package --vulnerable --include-transitive
```

Test startuje prawdziwe procesy aplikacji w tymczasowym katalogu i migruje wyłącznie osobną bazę. Sprawdza start bez bootstrapu, stronę logowania, brakujące dane, słabe hasło, role, logowanie Identity, niezmienność hasha i security stamp po restarcie, brak awansu istniejącego konta oraz brak wygenerowanych haseł w przechwyconych logach. Nie kopiuje firmowej bazy ani źródłowej konfiguracji do środowiska testowego.

Skaner jest heurystyczny: wypisuje lokalizacje, nigdy wartości dopasowań. Wynik zerowy nie dowodzi braku wszystkich sekretów ani danych firmowych. Opcja `--baseline <SCIEZKA_CHRONIONEGO_ORYGINALU>` pozwala po oczyszczeniu kopii sprawdzić także dokładne hasła odczytane z oryginalnej historii. Tryb historii skanuje bloby, komunikaty commitów i tagi osiągalne przez lokalne referencje i reflogi, także pliki usunięte z bieżącego drzewa; przed oczyszczeniem historii spodziewane jest wykrycie starego hasła.

Plan oczyszczenia historii, wyniki audytu i pozostałe ograniczenia: [SECURITY-PREPUBLICATION.md](SECURITY-PREPUBLICATION.md).
