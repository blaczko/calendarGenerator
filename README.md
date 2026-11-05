# Katolikus Naptárkészítő

Windows asztali alkalmazás személyre szabott, nyomtatható 12 oldalas katolikus falinaptár készítéséhez.

## Technológia

- .NET 8 / WPF / MVVM (`CommunityToolkit.Mvvm`)
- QuestPDF (PDF export, 300 DPI)
- WPF BitmapDecoder (JPG/PNG/WebP, ha a rendszer WebP kodekje elérhető)
- Helyi JSON projektmentés

## Futtatás

```powershell
dotnet restore
dotnet build
dotnet run --project src/CalendarGenerator.App
```

## Fő funkciók

1. Évválasztás, hétfővel kezdődő magyar naptár, szökőév kezelés
2. Magyar katolikus névnapok (rögzített napok + mozgó ünnepek)
3. Max. 6 fénykép / hónap, vegyes tájolású kollázs
4. Egyedi események kézzel rajzolt piros körrel és kézírás-szerű betűvel
5. 12 absztrakt háttérsablon
6. Előnézet + 12 oldalas A4 PDF export
7. Projekt mentés / betöltés / automatikus mentés

Projektek helye: `%USERPROFILE%\Documents\CatholicCalendarCreator\Projects`

---

## Kapcsolódó modul: Google Apps Script rendelés

A webes megrendelő / admin felület a `google-apps-script/` mappában van (Drive + Sheets).  
Részletek: [google-apps-script/README.md](google-apps-script/README.md)
