# Guía de Despliegue - BetsTrading API

## Despliegue en EC2 AWS

### Requisitos Previos
- .NET 8.0 Runtime instalado en EC2
- PostgreSQL instalado y configurado
- Variables de entorno configuradas

### Paso 1: Configurar la Contraseña de PostgreSQL

**Variable de Entorno (Recomendado para Producción)**

```bash
# En tu EC2
export POSTGRES_ADMIN_PASSWORD="tu-password-postgres"
```

La aplicación leerá automáticamente esta variable y la usará para construir la cadena de conexión. La cadena base está en `appsettings.json` pero la contraseña se sobrescribe desde la variable de entorno.

### Paso 2: Publicar el Proyecto

**Desde tu máquina local:**

```bash
dotnet publish BetsTrading.API/BetsTrading.API.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained false \
  -o ./publish
```

**O compilar directamente en EC2:**

```bash
# En tu EC2
cd /ruta/a/tu/proyecto
dotnet publish BetsTrading.API/BetsTrading.API.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained false \
  -o /opt/betstrading
```

### Paso 3: Configurar Variables de Entorno en EC2

**Opción A: Usando archivo `.env` (Recomendado para producción)**

Crea un archivo `/etc/systemd/system/bets-trading.service`:

```ini
[Unit]
Description=BetsTrading Service
After=network.target

[Service]
WorkingDirectory=/opt/betstrading
ExecStart=/usr/bin/dotnet /opt/betstrading/BetsTrading.API.dll
EnvironmentFile=/opt/betstrading/.env
Restart=always
RestartSec=5
StandardOutput=journal
StandardError=journal
Environment=ASPNETCORE_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```

**Importante:** Crea el archivo `/opt/betstrading/.env` con todas las variables necesarias:

```bash
# /opt/betstrading/.env
ASPNETCORE_URLS=http://localhost:5000
POSTGRES_ADMIN_PASSWORD=tu_password_postgres
JWT_LOCAL_KEY=TU_JWT_KEY_AQUI
GOOGLE_CLIENT_ID=tu_google_client_id.apps.googleusercontent.com
STRIPE_SECRET_KEY=sk_live_...
STRIPE_WEBHOOK_SECRET=whsec_...
SMTP__Host=smtp.gmail.com
SMTP__Username=tu-email@gmail.com
SMTP__FromAddress=tu-email@gmail.com
SMTP__Password=tu_password_smtp
TWELVE_DATA_KEY0=tu_api_key_0
TWELVE_DATA_KEY1=tu_api_key_1
```

**Firebase (notificaciones "otro dispositivo"):** Si existe `betrader-v1-firebase.json` en la raíz del repo, se **incluye al publicar** (está en `.gitignore`; el csproj lo copia solo cuando existe). Si no está, súbelo manualmente a `/opt/betstrading/` y dale permisos restrictivos. Ver `doc/FIREBASE_README.md`.

**Seguridad:** Asegúrate de que el archivo `.env` tenga permisos restrictivos:
```bash
sudo chmod 600 /opt/betstrading/.env
sudo chown root:root /opt/betstrading/.env
```

**Opción B: Variables inline en el servicio (Alternativa)**

Si prefieres no usar `.env`, puedes definir las variables directamente en el servicio:

```ini
[Unit]
Description=BetsTrading API Service
After=network.target postgresql.service

[Service]
# Type=notify: la API usa UseSystemd() y notifica a systemd cuando está lista
# (Opcional: si no usas Type=notify, la app funcionará igual)
Type=notify
User=www-data
WorkingDirectory=/opt/betstrading
ExecStart=/usr/bin/dotnet /opt/betstrading/BetsTrading.API.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=betstrading-api

# Variables de entorno
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5000
Environment=POSTGRES_ADMIN_PASSWORD=tu_password_postgres
Environment=JWT_LOCAL_KEY=TU_JWT_KEY_AQUI
Environment=GOOGLE_CLIENT_ID=tu_google_client_id.apps.googleusercontent.com
Environment=STRIPE_SECRET_KEY=sk_live_...
Environment=STRIPE_WEBHOOK_SECRET=whsec_...
Environment=SMTP__Host=smtp.gmail.com
Environment=SMTP__Username=tu-email@gmail.com
Environment=SMTP__FromAddress=tu-email@gmail.com
Environment=SMTP__Password=tu_password_smtp
Environment=TWELVE_DATA_KEY0=tu_api_key_0
Environment=TWELVE_DATA_KEY1=tu_api_key_1

[Install]
WantedBy=multi-user.target
```

### Paso 4: Iniciar el Servicio

```bash
# Recargar systemd
sudo systemctl daemon-reload

# Habilitar el servicio (ajusta el nombre según tu archivo: bets-trading.service o betstrading-api.service)
sudo systemctl enable bets-trading.service

# Iniciar el servicio
sudo systemctl start bets-trading.service

# Verificar estado
sudo systemctl status bets-trading.service

# Ver logs
sudo journalctl -u bets-trading.service -f
```

### Paso 5: Configurar Nginx con HTTPS 443 (estándar)

Todo el tráfico debe ir por **HTTPS 443**. Cloudflare se conecta al origen por 443; Nginx termina SSL y hace proxy a la app.

**Flujo de tráfico:**
```
Cliente → Cloudflare (HTTPS 443) → Nginx (HTTPS 443) → App (localhost:5000 HTTP)
```

#### 5.1 Instalar Nginx

**Amazon Linux 2:**
```bash
sudo yum install -y nginx
sudo systemctl enable nginx
```

**Ubuntu / Debian:**
```bash
sudo apt update && sudo apt install -y nginx
sudo systemctl enable nginx
```

#### 5.2 Certificado de origen Cloudflare (SSL en 443)

1. Entra en **Cloudflare Dashboard** → tu dominio → **SSL/TLS** → **Origin Server**.
2. **Create Certificate** → validez 15 años → **Create**.
3. Guarda el **Origin Certificate** (PEM) y la **Private Key** (PEM) en archivos seguros.

En el servidor, crea el directorio y los archivos:

```bash
sudo mkdir -p /etc/nginx/ssl
sudo nano /etc/nginx/ssl/cloudflare-origin.pem    # Pegar el certificado
sudo nano /etc/nginx/ssl/cloudflare-origin.key    # Pegar la clave privada
sudo chmod 600 /etc/nginx/ssl/cloudflare-origin.key
sudo chown root:root /etc/nginx/ssl/cloudflare-origin.*
```

#### 5.3 Configuración Nginx (443 + redirect 80→443)

**Ubuntu/Debian** (sites-available):
```bash
sudo nano /etc/nginx/sites-available/api.betstrading.online
```

**Amazon Linux** (conf.d):
```bash
sudo nano /etc/nginx/conf.d/api.betstrading.online.conf
```

Pega esta configuración (ajusta rutas si usas `conf.d`):

```nginx
# Redirigir HTTP → HTTPS
server {
    listen 80;
    server_name api.betstrading.online;
    return 301 https://$host$request_uri;
}

# HTTPS 443 – estándar
server {
    listen 443 ssl;
    server_name api.betstrading.online;

    ssl_certificate     /etc/nginx/ssl/cloudflare-origin.pem;
    ssl_certificate_key /etc/nginx/ssl/cloudflare-origin.key;
    ssl_protocols       TLSv1.2 TLSv1.3;
    ssl_ciphers         ECDHE-ECDSA-AES128-GCM-SHA256:ECDHE-RSA-AES128-GCM-SHA256:ECDHE-ECDSA-AES256-GCM-SHA384:ECDHE-RSA-AES256-GCM-SHA384;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-Host $host;
        proxy_set_header CF-Connecting-IP $http_cf_connecting_ip;
    }
}
```

**Solo en Ubuntu/Debian**, habilita el site y quita el default si molesta:
```bash
sudo ln -sf /etc/nginx/sites-available/api.betstrading.online /etc/nginx/sites-enabled/
# sudo rm -f /etc/nginx/sites-enabled/default   # opcional
```

#### 5.4 Comprobar y arrancar Nginx

```bash
sudo nginx -t
sudo systemctl start nginx
sudo systemctl reload nginx
```

#### 5.5 Cloudflare SSL/TLS

En **Cloudflare** → **SSL/TLS** → **Overview**:

- Modo: **Full (strict)**.  
  Así Cloudflare se conecta a tu origen por **HTTPS 443** y valida el certificado de origen.

#### 5.6 Security Group (EC2)

Asegúrate de que el **Security Group** de la instancia permita:

| Tipo   | Puerto | Origen     |
|--------|--------|------------|
| HTTPS  | 443    | 0.0.0.0/0 (o [IPs Cloudflare](https://www.cloudflare.com/ips/)) |
| HTTP   | 80     | 0.0.0.0/0 (redirect) |

El **5000** no se expone; solo Nginx en 80/443.

#### Resumen HTTPS 443

1. **Nginx** escucha en **80** (redirect → 443) y **443** (HTTPS).
2. Certificado **Cloudflare Origin** en `/etc/nginx/ssl/`.
3. **Cloudflare** SSL/TLS → **Full (strict)**.
4. **Security Group** → 443 y 80 abiertos.
5. Verificación: `curl https://api.betstrading.online/health` desde fuera.

## Verificación

### Health check local

- **En el servidor:** la app escucha en `localhost:5000`. Nginx recibe HTTPS en 443 y hace proxy HTTP a la app.
- **Desde fuera:** usar siempre **HTTPS** vía `https://api.betstrading.online/health` (puerto 443).

Este comando debe responder `Healthy`:

```bash
curl http://localhost:5000/health
```

Si usas `bets-trading.service` (p. ej. con RestartServer.sh), verifica con ese nombre:

```bash
sudo systemctl status bets-trading.service
curl http://localhost:5000/health
```

**Nota sobre ForwardedHeaders:**
La app está configurada con `UseForwardedHeaders()` para reconocer correctamente:
- El protocolo real (HTTPS) aunque reciba HTTP desde Nginx
- La IP real del cliente (desde Cloudflare `CF-Connecting-IP` o `X-Forwarded-For`)
- El host original (`X-Forwarded-Host`)

Esto es necesario porque Cloudflare y Nginx actúan como proxies en cadena.

### Verificar Conexión a Base de Datos

```bash
# Probar conexión desde EC2
psql -h localhost -U postgres -d betstrading
```

### Verificar que la API está funcionando

```bash
curl http://localhost:5000/health
```

### "Healthy" en localhost pero no desde fuera

**Por qué ocurre:** La app escucha solo en `localhost:5000`. El puerto 5000 **no** está expuesto a internet; el tráfico externo debe entrar por **Nginx** (80/443).

| Origen            | URL correcta                                          |
|-------------------|--------------------------------------------------------|
| En el servidor    | `curl http://localhost:5000/health`                   |
| Desde fuera (IP)  | `curl https://3.88.206.171/health` (puerto 443)       |
| Desde fuera (DNS) | `curl https://api.betstrading.online/health` (443)    |

**No uses** el puerto 5000 desde fuera: es solo interno. Todo debe ir por **443** (HTTPS).

**Checklist en el servidor (ejecuta y corrige lo que falle):**

```bash
# 1. Nginx corriendo
sudo systemctl status nginx
# Si no: sudo systemctl start nginx && sudo systemctl enable nginx

# 2. Nginx escucha en 80 y 443
sudo ss -tlnp | grep -E ':80|:443'

# 3. Config para api.betstrading.online existe (443 + SSL)
sudo cat /etc/nginx/sites-enabled/api.betstrading.online
# O: sudo cat /etc/nginx/conf.d/api.betstrading.online.conf
# Debe tener listen 443 ssl, proxy_pass http://localhost:5000, server_name api.betstrading.online

# 4. Health vía Nginx (desde el servidor)
curl -k https://localhost/health
curl -k -H "Host: api.betstrading.online" https://localhost/health
```

Si (3) o (4) fallan, configura Nginx como en el **Paso 5** de esta guía. Si (4) responde `Healthy` pero desde fuera no, revisa:

- **AWS Security Group**: Inbound debe permitir **HTTP (80)** y **HTTPS (443)** desde `0.0.0.0/0` (o desde los [IPs de Cloudflare](https://www.cloudflare.com/ips/) si solo usas Cloudflare).
- **Firewall (ufw/firewalld)**: Debe permitir 80 y 443. No hace falta abrir el 5000.

## Troubleshooting

### Error: "password authentication failed"
- Verifica que la contraseña en la cadena de conexión sea correcta
- Verifica que el usuario `postgres` existe en PostgreSQL
- Verifica los permisos del usuario en PostgreSQL

### Error: "Connection refused"
- Verifica que PostgreSQL está corriendo: `sudo systemctl status postgresql`
- Verifica que PostgreSQL acepta conexiones en `localhost:5432`
- Revisa `/etc/postgresql/*/main/pg_hba.conf` para configurar autenticación

### SMTP: "5.7.0 Authentication Required" o "client was not authenticated"
- **Gmail**: No uses la contraseña de la cuenta. Debes usar una **Contraseña de aplicación**:
  1. Cuenta Google → [Seguridad](https://myaccount.google.com/security) → Verificación en 2 pasos (debe estar activa).
  2. [Contraseñas de aplicaciones](https://myaccount.google.com/apppasswords) → Crear → nombre "Betrader" → copiar la contraseña de 16 caracteres.
  3. Usa esa contraseña en `SMTP__Password` (no la de la cuenta).
- `SMTP__Username` y `SMTP__FromAddress` deben ser el **email completo** (ej. `helpme.betstrading@gmail.com`).
- Comprueba que `SMTP__Host=smtp.gmail.com`, puerto 587, SSL activo.

### Ver logs de la aplicación

```bash
# Logs del servicio systemd
sudo journalctl -u betstrading-api -n 100 -f

# Logs de archivo (si están configurados)
tail -f /opt/betstrading/Logs/BetsTrading_API_*.log
```

## Notas Importantes

1. **Seguridad**: Nunca commitees contraseñas reales al repositorio
2. **Variables de Entorno**: Usa variables de entorno para información sensible
3. **Firewall**: En el Security Group de EC2 abre **80** y **443** (no el 5000; la app solo escucha en localhost)
4. **SSL/TLS**: Configura HTTPS usando Let's Encrypt o AWS Certificate Manager
5. **Backups**: Configura backups automáticos de la base de datos PostgreSQL
