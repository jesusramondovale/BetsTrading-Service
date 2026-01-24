# Variables de Entorno

Este documento lista todas las variables de entorno necesarias para ejecutar la aplicación BetsTrading API.

## Variables Requeridas

### Base de Datos PostgreSQL
- `POSTGRES_ADMIN_PASSWORD`: Contraseña del usuario postgres de PostgreSQL (requerida)

### Autenticación JWT
- `JWT_LOCAL_KEY`: Clave secreta para firmar tokens JWT locales (requerida)

### Google OAuth
- `GOOGLE_CLIENT_ID`: Client ID de Google OAuth para autenticación con Google (requerida)
- Si no se configura la variable de entorno, se intentará leer desde `appsettings.json` bajo la sección `Google:ClientId`

### Stripe
- `STRIPE_SECRET_KEY`: Clave secreta de Stripe para procesar pagos (requerida para módulo de pagos)
- `STRIPE_WEBHOOK_SECRET`: Secreto del webhook de Stripe para validar eventos (requerida para webhooks)

### SMTP (Email)
- `SMTP__Host`: Servidor SMTP (p. ej. `smtp.gmail.com`). Puede ir en appsettings o en `.env`.
- `SMTP__Username`: Usuario SMTP (normalmente el email). Puede ir en appsettings o en `.env`.
- `SMTP__FromAddress`: Email remitente. **Requerido**; si falta, el envío falla con "FromAddress cannot be empty". Puede ir en appsettings o en `.env`.
- `SMTP__Password`: Contraseña SMTP. Puede ir en appsettings o en `.env` (recomendado en producción).  
  **Gmail:** usa una [Contraseña de aplicación](https://myaccount.google.com/apppasswords), no la contraseña de la cuenta.

### TwelveData API
- `TWELVE_DATA_KEY0`: Primera clave API de TwelveData
- `TWELVE_DATA_KEY1`: Segunda clave API de TwelveData
- `TWELVE_DATA_KEY2`: Tercera clave API de TwelveData
- ... (hasta `TWELVE_DATA_KEY10`)

**Nota**: El servicio UpdaterService rota entre estas claves para evitar límites de rate limiting. Se recomienda configurar al menos 2-3 claves.

### Firebase (notificaciones push, p. ej. "otro dispositivo")
- **Archivo `betrader-v1-firebase.json`**: credenciales de Service Account de Firebase. No hay variable de entorno estándar; la ruta se configura en `appsettings.json` como `Firebase:CredentialsPath` (por defecto `betrader-v1-firebase.json` en el directorio de ejecución).
- Opcional: `Firebase__CredentialsPath` para sobrescribir la ruta (p. ej. `/opt/betstrading/betrader-v1-firebase.json`).
- El archivo está en **`.gitignore`**. Si existe en la raíz del repo, **se copia al compilar/publicar** (csproj con `Condition="Exists(...)"`). Si no, colócalo manualmente en la carpeta de despliegue. Ver `doc/FIREBASE_README.md`.

## Variables Opcionales

### Base de Datos
- La cadena de conexión base se configura en `appsettings.json` bajo `ConnectionStrings:DefaultConnection`
- **La contraseña se lee automáticamente** de la variable de entorno `POSTGRES_ADMIN_PASSWORD`
- Si no se configura la variable de entorno, se usará la contraseña del `appsettings.json` (no recomendado para producción)

## Configuración en Desarrollo

Para desarrollo local, puedes crear un archivo `.env` (no incluido en git) o configurar las variables en tu IDE.

### Ejemplo de configuración en PowerShell (Windows):
```powershell
$env:POSTGRES_ADMIN_PASSWORD = "tu-password-postgres"
$env:JWT_LOCAL_KEY = "tu-clave-secreta-aqui"
$env:GOOGLE_CLIENT_ID = "tu-google-client-id.apps.googleusercontent.com"
$env:STRIPE_SECRET_KEY = "sk_test_..."
$env:STRIPE_WEBHOOK_SECRET = "whsec_..."
$env:SMTP__Host = "smtp.gmail.com"
$env:SMTP__Username = "tu-email@gmail.com"
$env:SMTP__FromAddress = "tu-email@gmail.com"
$env:SMTP__Password = "tu-password-smtp"
$env:TWELVE_DATA_KEY0 = "tu-api-key-0"
$env:TWELVE_DATA_KEY1 = "tu-api-key-1"
```

### Ejemplo de configuración en Bash (Linux/Mac):
```bash
export POSTGRES_ADMIN_PASSWORD="tu-password-postgres"
export JWT_LOCAL_KEY="tu-clave-secreta-aqui"
export GOOGLE_CLIENT_ID="tu-google-client-id.apps.googleusercontent.com"
export STRIPE_SECRET_KEY="sk_test_..."
export STRIPE_WEBHOOK_SECRET="whsec_..."
export SMTP__Host="smtp.gmail.com"
export SMTP__Username="tu-email@gmail.com"
export SMTP__FromAddress="tu-email@gmail.com"
export SMTP__Password="tu-password-smtp"
export TWELVE_DATA_KEY0="tu-api-key-0"
export TWELVE_DATA_KEY1="tu-api-key-1"
```

## Configuración en Producción

En producción, configura estas variables de entorno según tu plataforma de hosting:

- **Azure**: App Settings / Configuration
- **AWS**: Environment Variables en Elastic Beanstalk o ECS Task Definition
- **Docker**: Variables de entorno en `docker-compose.yml` o `Dockerfile`
- **Linux Service**: Archivo de servicio systemd con `Environment=` o archivo `.env`

## Seguridad

⚠️ **IMPORTANTE**: Nunca commitees valores reales de estas variables al repositorio. Usa siempre variables de entorno o secretos gestionados por tu plataforma de hosting.
