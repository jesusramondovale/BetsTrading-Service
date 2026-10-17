# Flutter web client en `/betrader`

La app Betrader (Flutter) se publica como estáticos en:

- Repo: `BetsTrading.API/wwwroot/betrader/`
- Producción: `/opt/betstrading/wwwroot/betrader/`
- URL: `https://api.betstrading.online/betrader/`

## Regenerar desde el cliente

Desde `C:\dev\BetsTrading`:

```powershell
.\scripts\publish_web.ps1
```

El script ejecuta `flutter build web --release --base-href /betrader/` y copia `build/web` al destino.

## Servidor

`Program.cs` sirve `wwwroot/betrader` bajo `/betrader` con fallback SPA. `/app` redirige a `/betrader`.

## Google OAuth (web)

En el cliente OAuth web, orígenes JS: `https://api.betstrading.online` y (dev) `http://localhost:5289`.  
Redirects: incluir `https://api.betstrading.online/betrader/`.
