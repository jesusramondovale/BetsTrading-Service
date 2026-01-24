# Troubleshooting Error 521 - Cloudflare "Web Server is Down"

## ¿Qué significa el error 521?

El error 521 de Cloudflare significa que **Cloudflare no puede conectarse a tu servidor de origen (EC2)**. Esto indica que:

1. La aplicación no está corriendo
2. La aplicación está crasheando al inicio
3. El puerto no está abierto o no está escuchando
4. El firewall está bloqueando las conexiones

## Pasos de Diagnóstico

### 1. Verificar que la aplicación está corriendo

```bash
sudo systemctl status betstrading-api
```

**Si está corriendo:** Verás `Active: active (running)`
**Si NO está corriendo:** Verás `Active: inactive (dead)` o `Active: failed`

### 2. Si NO está corriendo, iniciarla

```bash
sudo systemctl start betstrading-api
sudo systemctl status betstrading-api
```

### 3. Verificar los logs del servicio

```bash
# Ver los últimos 100 logs
sudo journalctl -u betstrading-api -n 100

# Ver logs en tiempo real
sudo journalctl -u betstrading-api -f
```

**Busca errores como:**
- `Fatal` - Errores críticos que impiden el inicio
- `Error` - Errores que causan fallos
- `Exception` - Excepciones no manejadas

### 4. Verificar que el puerto está escuchando

```bash
# Verificar puerto 5000 (o el que uses)
sudo netstat -tlnp | grep :5000

# O usar ss
sudo ss -tlnp | grep :5000
```

**Deberías ver algo como:**
```
tcp  0  0  127.0.0.1:5000  0.0.0.0:*  LISTEN  12345/dotnet
```

### 5. Verificar logs de archivo

```bash
# Ver los últimos logs del archivo
tail -f /opt/betstrading/Logs/BetsTrading_API_*.log

# O ver los últimos 50 líneas
tail -n 50 /opt/betstrading/Logs/BetsTrading_API_*.log
```

### 6. Probar la conexión localmente

```bash
# Desde el servidor EC2, probar si responde
curl http://localhost:5000/health

# O si usas otro puerto (ej: 44)
curl http://localhost:44/health
```

**Si funciona:** Verás una respuesta JSON
**Si NO funciona:** Verás "Connection refused" o timeout

### 7. Verificar variables de entorno

```bash
# Ver las variables de entorno del servicio
sudo systemctl show betstrading-api | grep Environment

# Verificar que las variables críticas están configuradas
sudo cat /etc/systemd/system/betstrading-api.service | grep Environment
```

**Variables críticas:**
- `POSTGRES_ADMIN_PASSWORD` - Debe estar configurada
- `JWT_LOCAL_KEY` - Debe estar configurada
- `GOOGLE_CLIENT_ID` - Debe estar configurada (o en appsettings.json)
- `ASPNETCORE_URLS` - Debe coincidir con el puerto que usa Nginx

### 8. Verificar configuración de Nginx

```bash
# Ver la configuración de Nginx
sudo cat /etc/nginx/sites-available/api.betstrading.online

# Verificar que el puerto en proxy_pass coincide con ASPNETCORE_URLS
# Ejemplo: proxy_pass http://localhost:5000;
```

### 9. Verificar firewall

```bash
# Verificar que el puerto está abierto (si usas ufw)
sudo ufw status

# Verificar reglas de iptables
sudo iptables -L -n | grep 5000
```

### 10. Verificar que PostgreSQL está corriendo

```bash
sudo systemctl status postgresql

# Probar conexión
psql -h localhost -U postgres -d bets_db
```

## Errores Comunes y Soluciones

### Error: "password authentication failed"
**Causa:** La variable `POSTGRES_ADMIN_PASSWORD` no está configurada o es incorrecta
**Solución:** 
```bash
# Verificar variable
echo $POSTGRES_ADMIN_PASSWORD

# Configurar en el servicio systemd
sudo nano /etc/systemd/system/betstrading-api.service
# Agregar: Environment=POSTGRES_ADMIN_PASSWORD=tu_password
sudo systemctl daemon-reload
sudo systemctl restart betstrading-api
```

### Error: "JWT Local Custom Key is empty!"
**Causa:** La variable `JWT_LOCAL_KEY` no está configurada
**Solución:**
```bash
# Configurar en el servicio systemd
sudo nano /etc/systemd/system/betstrading-api.service
# Agregar: Environment=JWT_LOCAL_KEY=tu_jwt_key
sudo systemctl daemon-reload
sudo systemctl restart betstrading-api
```

### Error: "Google JWT Client Id is empty!"
**Causa:** La variable `GOOGLE_CLIENT_ID` no está configurada
**Solución:**
```bash
# Configurar en el servicio systemd
sudo nano /etc/systemd/system/betstrading-api.service
# Agregar: Environment=GOOGLE_CLIENT_ID=tu_google_client_id
sudo systemctl daemon-reload
sudo systemctl restart betstrading-api
```

### Error: "Connection refused" en curl
**Causa:** La aplicación no está escuchando en el puerto
**Solución:**
1. Verificar que la aplicación está corriendo: `sudo systemctl status betstrading-api`
2. Verificar el puerto en `ASPNETCORE_URLS`: `sudo cat /etc/systemd/system/betstrading-api.service | grep ASPNETCORE_URLS`
3. Verificar que el puerto no está en uso por otro proceso: `sudo lsof -i :5000`

### Error: La aplicación se reinicia constantemente
**Causa:** Un error fatal está causando que la aplicación crashee
**Solución:**
1. Ver los logs: `sudo journalctl -u betstrading-api -n 100`
2. Buscar el error fatal que causa el crash
3. Corregir el problema (variable de entorno faltante, error de configuración, etc.)

## Cambios Realizados para Cloudflare

### 1. Rate Limiting configurado para Cloudflare
- `RealIpHeader` cambiado a `"CF-Connecting-IP"` en `appsettings.json`
- Esto permite que el rate limiting use la IP real del cliente desde Cloudflare

### 2. ForwardedHeaders configurado
- Agregado `UseForwardedHeaders()` al inicio del pipeline
- Configurado para leer headers de Cloudflare (`X-Forwarded-For`, `X-Forwarded-Proto`, etc.)

### 3. CORS permisivo
- Cambiado a `AllowAnyOrigin()` para coincidir con el comportamiento del proyecto legacy
- Necesario porque las peticiones vienen a través de Cloudflare proxy

### 4. HTTPS Redirection deshabilitado
- `UseHttpsRedirection()` está comentado porque Cloudflare ya maneja HTTPS
- Esto evita redirecciones innecesarias

## Verificación Final

Después de aplicar los cambios, verifica:

1. ✅ La aplicación está corriendo: `sudo systemctl status betstrading-api`
2. ✅ El puerto está escuchando: `sudo netstat -tlnp | grep :5000`
3. ✅ Responde localmente: `curl http://localhost:5000/health`
4. ✅ No hay errores en los logs: `sudo journalctl -u betstrading-api -n 50`
5. ✅ Nginx está configurado correctamente: `sudo nginx -t`

Si todo está correcto pero Cloudflare sigue dando error 521, verifica:
- La configuración de SSL/TLS en Cloudflare (debe ser "Flexible" o "Full")
- Que las IPs de Cloudflare no estén bloqueadas en el firewall
- Que el Security Group de EC2 permita conexiones desde Cloudflare
