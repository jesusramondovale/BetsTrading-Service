# Firebase – Credenciales y notificaciones

## Por qué no existe el archivo en el servidor (EC2)

El archivo `betrader-v1-firebase.json` **no se incluye en el repositorio** (está en `.gitignore`), pero **sí se copia al compilar/publicar cuando existe** en la raíz del repo:

1. **`.gitignore`**: está listado para no subir la clave privada a Git.
2. **`BetsTrading.API.csproj`**: está como `Content` con `Condition="Exists(...)"`. Si el archivo existe en la raíz (`BetsTrading-Service/betrader-v1-firebase.json`), se copia a la salida y al publish; si no existe (p. ej. CI, clone fresco), el build sigue y Firebase queda deshabilitado.
3. Para **EC2 / producción**: coloca el JSON en la raíz antes de `dotnet publish`, o súbelo manualmente a la carpeta de despliegue.

En **legacy**, el mismo archivo vivía en la **raíz del proyecto** (`BetsTrading-Service/betrader-v1-firebase.json`). El servicio `Services/NotificactionService.cs` lo usaba; al migrar a `FirebaseNotificationService` se mantiene el mismo nombre y uso. En producción legacy seguramente se desplegaba a mano o por otro medio fuera del repo.

La API busca el archivo en **el directorio de ejecución** (`AppDomain.CurrentDomain.BaseDirectory`). Si publicas en `/opt/betstrading`, debe estar en **`/opt/betstrading/betrader-v1-firebase.json`**.

---

## Cómo tener Firebase en desarrollo y en EC2

### Desarrollo local

- Coloca `betrader-v1-firebase.json` en la **raíz del repo** (`BetsTrading-Service/`).
- El csproj lo copia a `bin/Debug` y al publish si existe. La app lo busca en el directorio de ejecución.

### Producción (EC2, `/opt/betstrading`)

**Opción A – Incluido en publish:** Coloca `betrader-v1-firebase.json` en la raíz del repo antes de `dotnet publish ... -o /opt/betstrading`. Se copiará al publish y la app lo encontrará ahí.

**Opción B – Manual en servidor:**
1. Descarga el JSON desde [Firebase Console → Service Accounts](https://console.firebase.google.com/u/0/project/betrader-v1/settings/serviceaccounts/adminsdk) (Generate New Private Key).
2. Súbelo a la carpeta de la app: `scp betrader-v1-firebase.json usuario@tu-ec2:/opt/betstrading/`
3. Ajusta permisos: `chmod 600`, `chown www-data:www-data` si aplica.
4. Reinicia la API.

**Ruta alternativa**: `Firebase:CredentialsPath` o `Firebase__CredentialsPath` con ruta absoluta (p. ej. `/opt/betstrading/betrader-v1-firebase.json`).

---

## Actualizar la clave (cuando caduque)

1. Ve a [Firebase Console - Service Accounts](https://console.firebase.google.com/u/0/project/betrader-v1/settings/serviceaccounts/adminsdk).
2. **Project Settings** → **Service Accounts** → **Generate New Private Key**; descarga el JSON.
3. Sustituye el archivo:
   - **Local**: `BetsTrading-Service/betrader-v1-firebase.json`.
   - **EC2**: `/opt/betstrading/betrader-v1-firebase.json` (o la ruta que uses en `Firebase:CredentialsPath`).
4. Reinicia la aplicación y prueba las notificaciones.

**Importante:** No subas el JSON a Git ni lo incluyas en el artefacto de publish. Mantenlo solo en máquinas y rutas controladas.
