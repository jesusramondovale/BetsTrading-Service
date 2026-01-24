# Limpieza post-migración a Clean Architecture

Elementos **anteriores al commit 7efa12453b5be3e8a835dd5923b5112bd4f1cfd8** (monolito legacy) que ya **no hacen falta** tras la migración a Clean Architecture y **se pueden eliminar**.

La solución actual solo compila: **BetsTrading.API**, **BetsTrading.Application**, **BetsTrading.Domain**, **BetsTrading.Infrastructure** y **Load test**. El proyecto **BetsTrading-Service.csproj** (raíz) **no está en la solución** y todo su código ha sido reemplazado por la arquitectura limpia.

---

## 1. Proyecto legacy (raíz)

| Elemento | Motivo |
|----------|--------|
| `BetsTrading-Service.csproj` | Proyecto monolito legacy; no está en la solución. La API es el entry point. |
| `BetsTrading-Service.csproj.user` | Configuración de usuario del proyecto legacy. |

---

## 2. Punto de entrada y configuración de arranque

| Elemento | Motivo |
|----------|--------|
| `Program.cs` (raíz) | Sustituido por `BetsTrading.API/Program.cs`. |
| `Properties/` (raíz) | `launchSettings.json`, `PublishProfiles/` (Linux-x64, Win-Local). La API tiene los suyos en `BetsTrading.API/Properties/`. |

---

## 3. Controladores legacy

| Elemento | Motivo |
|----------|--------|
| `Controllers/AuthController.cs` | Sustituido por `BetsTrading.API/Controllers/AuthController.cs` (MediatR). |
| `Controllers/BetController.cs` | Sustituido por `BetsTrading.API/Controllers/BetController.cs`. |
| `Controllers/DiditController.cs` | Sustituido por `BetsTrading.API/Controllers/DiditController.cs`. |
| `Controllers/FinancialAssetsController.cs` | Sustituido por `BetsTrading.API/Controllers/FinancialAssetsController.cs`. |
| `Controllers/InfoController.cs` | Sustituido por `BetsTrading.API/Controllers/InfoController.cs`. |
| `Controllers/PaymentsController.cs` | Sustituido por `BetsTrading.API/Controllers/PaymentsController.cs`. |
| `Controllers/RewardsController.cs` | Sustituido por `BetsTrading.API/Controllers/RewardsController.cs`. |

Todos usan `BetsTrading_Service.Database`, `Models`, `Requests`, `Services`, `Locale`. Equivalentes en API + Application + Infrastructure.

---

## 4. Base de datos y persistencia

| Elemento | Motivo |
|----------|--------|
| `Database/AppDbContext.cs` | Sustituido por `BetsTrading.Infrastructure/Persistence/AppDbContext.cs`. |

---

## 5. Interfaces y modelos legacy

| Elemento | Motivo |
|----------|--------|
| `Interfaces/ICustomLogger.cs` | Sustituido por logging en `BetsTrading.Infrastructure/Logging` (CustomLogger, ApplicationLogger). |
| `Models/` (carpeta completa) | Entidades → `BetsTrading.Domain/Entities/`. DTOs y requests → Commands/Queries y `BetsTrading.Application/DTOs/`. `TwelveDataParser` solo lo usa el `UpdaterService` legacy; Infrastructure usa `TwelveDataModels`. |

Archivos en `Models/`: `AdmobSsvQuery.cs`, `AssetCandle.cs`, `Bet.cs`, `BetZone.cs`, `Favorite.cs`, `FinancialAsset.cs`, `PaymentData.cs`, `PriceBet.cs`, `Raffle.cs`, `RaffleItem.cs`, `RewardNonce.cs`, `RewardTransaction.cs`, `Trend.cs`, `TwelveDataParser.cs`, `User.cs`, `VerificationCode.cs`, `WithdrawalData.cs`, `WithdrawalMethod.cs`.

---

## 6. Requests (DTOs de API legacy)

| Elemento | Motivo |
|----------|--------|
| `Requests/` (carpeta completa) | Sustituidos por Commands, Queries y DTOs en Application. |

Archivos: `addWithdrawalMethodRequest.cs`, `ChangePasswordRequest.cs`, `googleSignRequest.cs`, `idRequest.cs`, `LoginRequest.cs`, `newBetRequest.cs`, `newFavoriteRequest.cs`, `SignUpRequest.cs`, `uploadPicRequest.cs`.

---

## 7. Servicios legacy

| Elemento | Motivo |
|----------|--------|
| `Services/EmailService.cs` | Sustituido por `BetsTrading.Infrastructure/Services/EmailService.cs`. |
| `Services/NotificactionService.cs` | Sustituido por `BetsTrading.Infrastructure/Services/FirebaseNotificationService.cs`. |
| `Services/OddsAdjusterService .cs` | Sustituido por `BetsTrading.Infrastructure/HostedServices/OddsAdjusterHostedService.cs`. |
| `Services/UpdaterService.cs` | Sustituido por `BetsTrading.Infrastructure/Services/UpdaterService.cs` y `UpdaterHostedService`. |

---

## 8. Locale

| Elemento | Motivo |
|----------|--------|
| `Locale/LocalizedTexts.cs` | Los textos están embebidos en `BetsTrading.Infrastructure/Services/LocalizationService.cs` (`LocalizedTextsDictionary`). |

---

## 9. Archivos temporales

| Elemento | Motivo |
|----------|--------|
| `temp_program_before.cs` | Backup temporal del `Program` legacy. |
| `temp_program_old_check.cs` | Backup temporal. |
| `temp_program_old.cs` | Backup temporal. |

---

## 10. Opcionales / según uso

| Elemento | Motivo |
|----------|--------|
| `appsettings.Development.json` (raíz) | La API usa `BetsTrading.API/appsettings.Development.json`. El de raíz solo sirve al monolito. Se puede borrar si no se usa para nada más. |
| `BetsTrading-Service.http` | Apunta al host legacy (puerto 5256, `/weatherforecast`). Se puede eliminar o adaptar para probar la API (ej. `BetsTrading.API` en 5289/7207 y Swagger). |

---

## Qué **no** eliminar

- **`app-ads.txt`**, **`exchange_options_eur.json`**, **`exchange_options_usd.json`**: la API los referencia como `Content` en `BetsTrading.API.csproj`.
- **`doc/`**: documentación (ARCHITECTURE, MIGRATION_*, DEPLOYMENT_GUIDE, etc.).
- **`FullDB.sql`**: backup/semilla de BD; útil para referencia o restauración.
- **`RestartServer.sh`**, **`add-app-worker.js`**, **`wrangler.toml`**: scripts y config de despliegue.
- **`BetsTrading-Service.sln`**, **`.editorconfig`**, **`.gitignore`**, **`.gitattributes`**, **`.config/`**: solución y configuración del repo.

---

## Resumen de eliminación

```text
Eliminar (carpetas o archivos):
  BetsTrading-Service.csproj
  BetsTrading-Service.csproj.user
  Program.cs
  Properties/
  Controllers/
  Database/
  Interfaces/
  Models/
  Requests/
  Services/
  Locale/
  temp_program_before.cs
  temp_program_old_check.cs
  temp_program_old.cs

Opcional:
  appsettings.Development.json (raíz)
  BetsTrading-Service.http (o actualizarlo para la API)
```

Tras borrar todo lo anterior, la solución sigue compilando y ejecutándose solo con **BetsTrading.API** y sus dependencias (Application, Infrastructure, Domain).
