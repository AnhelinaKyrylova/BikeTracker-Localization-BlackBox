# BikeTracker – Škálovateľná softvérová architektúra pre navigáciu k Bluetooth objektom

## Štruktúra riešenia

**BikeTracker**
- **BikeTracker.Core** – jadro black boxu: lokalizačné filtre (Extended Kalman Filter,
  Particle Filter), rozhrania a stratégia výberu filtra podľa schopností zariadenia
- **BikeTracker.Core.Tests** – testy
- **BikeTracker.Analysis** – nástroj na spracovanie reálnych RSSI meraní z Excel,
  automatickú kalibráciu path-loss modelu a generovanie Excel reportu z porovnanim tabulek.
  Výstup:
   - `filter_comparison_output.csv` – porovnanie odhadov EKF/PF pri každej vzorke
   - `BikeTracker_Merania_Analyza.xlsx` – formátovaný report so 3 hárkami
     (Namerane_data, Vysledky_filtrov, Suhrn) a automaticky vypočítanou kalibráciou
     path-loss modelu (metóda najmenších štvorcov)

## Cez Powershell start

```
cd BikeTracker.Core

dotnet build

cd ..\BikeTracker.Core.Tests

dotnet test BikeTracker.Core.Tests\BikeTracker.Core.Tests.csproj -v n

cd BikeTracker.Analysis

dotnet run measurements.csv

cd BikeTracker

dotnet add package Plugin.BLE
```



## Poznámka ku kalibrácii

Parametre `TxPower` a `PathLossExponent` v `RssiPathLossModel` sa pri behu
`BikeTracker.Analysis` prepočítavajú automaticky z aktuálneho obsahu
`measurements.csv` (`RssiCalibration.cs`).
