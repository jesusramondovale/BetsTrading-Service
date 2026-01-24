# Diagnóstico: Nginx y Conectividad Cloudflare

## Problema Actual
- ✅ La aplicación funciona en `localhost:5000` (health check responde "Healthy")
- ❌ No se puede acceder desde fuera del servidor (IP pública 3.88.206.171)
- ❌ Cloudflare devuelve error 521 ("Web Server is Down")

## Verificación Paso a Paso

### 1. Verificar que Nginx está corriendo

```bash
# Verificar estado de Nginx
sudo systemctl status nginx

# Si no está corriendo, iniciarlo:
sudo systemctl start nginx
sudo systemctl enable nginx
```

### 2. Verificar configuración de Nginx para api.betstrading.online

```bash
# Verificar sintaxis de configuración
sudo nginx -t

# Ver configuración actual
sudo cat /etc/nginx/sites-available/api.betstrading.online
# O si está en sites-enabled:
sudo cat /etc/nginx/sites-enabled/api.betstrading.online

# Ver todos los archivos de configuración
ls -la /etc/nginx/sites-available/
ls -la /etc/nginx/sites-enabled/
```

**Configuración esperada de Nginx:**

```nginx
server {
    listen 80;
    server_name api.betstrading.online;

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
        proxy_set_header CF-Connecting-IP $http_cf_connecting_ip;  # Cloudflare header
    }
}
```

### 3. Probar health check a través de Nginx (desde el servidor)

```bash
# Probar directamente
curl http://localhost/health

# Probar con el header Host
curl -H "Host: api.betstrading.online" http://localhost/health

# Probar con IP local
curl http://127.0.0.1/health
```

### 4. Verificar que Nginx está escuchando en los puertos correctos

```bash
# Verificar puertos en uso
sudo netstat -tlnp | grep nginx
# O con ss:
sudo ss -tlnp | grep nginx

# Deberías ver algo como:
# :80 (HTTP)
# :443 (HTTPS, si está configurado)
```

### 5. Verificar logs de Nginx

```bash
# Ver logs de acceso en tiempo real
sudo tail -f /var/log/nginx/access.log

# Ver logs de errores en tiempo real
sudo tail -f /var/log/nginx/error.log

# Ver últimos errores
sudo tail -n 50 /var/log/nginx/error.log
```

### 6. Verificar firewall local (si está activo)

```bash
# Si usas firewalld (CentOS/RHEL):
sudo firewall-cmd --list-all
sudo firewall-cmd --list-ports
# Asegúrate de que los puertos 80 y 443 estén abiertos:
sudo firewall-cmd --permanent --add-service=http
sudo firewall-cmd --permanent --add-service=https
sudo firewall-cmd --reload

# Si usas ufw (Ubuntu/Debian):
sudo ufw status
# Si está activo, asegúrate de que permita HTTP/HTTPS:
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
```

### 7. Verificar Security Group de AWS EC2

En la consola de AWS:

1. Ve a **EC2** → **Instances** → Selecciona tu instancia
2. Ve a la pestaña **Security** → Click en el **Security Group**
3. Verifica las **Inbound Rules**:
   - Debe permitir **HTTP (80)** desde `0.0.0.0/0` o desde rangos de IP de Cloudflare
   - Debe permitir **HTTPS (443)** desde `0.0.0.0/0` o desde rangos de IP de Cloudflare

**Rangos de IP de Cloudflare (si quieres restringir):**
- https://www.cloudflare.com/ips/

### 8. Probar conectividad desde el servidor

```bash
# Probar que la aplicación responde
curl http://localhost:5000/health

# Probar que Nginx puede hacer proxy
curl http://localhost/health
curl -H "Host: api.betstrading.online" http://localhost/health
```

### 9. Verificar DNS y Cloudflare

En el panel de Cloudflare:

1. Verifica que `api.betstrading.online` apunta a la IP correcta (3.88.206.171)
2. Verifica que el **Proxy Status** está en "Proxied" (nube naranja)
3. Verifica que **SSL/TLS** está configurado (recomendado: "Full" o "Full (strict)")

### 10. Probar desde fuera (si tienes acceso a otra máquina)

```bash
# Desde tu máquina local (debería fallar si Security Group bloquea)
curl http://3.88.206.171/health
curl -H "Host: api.betstrading.online" http://3.88.206.171/health

# A través de Cloudflare (debería funcionar si todo está bien)
curl https://api.betstrading.online/health
```

## Soluciones Comunes

### Problema: Nginx no está corriendo
```bash
sudo systemctl start nginx
sudo systemctl enable nginx
```

### Problema: Nginx no tiene configuración para api.betstrading.online
```bash
# Crear archivo de configuración
sudo nano /etc/nginx/sites-available/api.betstrading.online

# Pegar la configuración de arriba y guardar

# Crear enlace simbólico
sudo ln -s /etc/nginx/sites-available/api.betstrading.online /etc/nginx/sites-enabled/

# Verificar sintaxis
sudo nginx -t

# Recargar Nginx
sudo systemctl reload nginx
```

### Problema: Security Group bloquea conexiones
1. Ve a AWS Console → EC2 → Security Groups
2. Edita las reglas de entrada (Inbound Rules)
3. Agrega:
   - **Type**: HTTP, **Port**: 80, **Source**: 0.0.0.0/0
   - **Type**: HTTPS, **Port**: 443, **Source**: 0.0.0.0/0

### Problema: Firewall local bloquea
```bash
# firewalld
sudo firewall-cmd --permanent --add-service=http
sudo firewall-cmd --permanent --add-service=https
sudo firewall-cmd --reload

# ufw
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
```

## Checklist de Verificación

- [ ] Nginx está corriendo (`systemctl status nginx`)
- [ ] Nginx tiene configuración para `api.betstrading.online`
- [ ] `curl http://localhost/health` funciona a través de Nginx
- [ ] Nginx está escuchando en puerto 80 (`netstat -tlnp | grep :80`)
- [ ] Security Group permite HTTP (80) y HTTPS (443)
- [ ] Firewall local permite HTTP/HTTPS
- [ ] DNS en Cloudflare apunta a la IP correcta
- [ ] Cloudflare Proxy está activo (nube naranja)

## Comandos de Diagnóstico Rápido

Ejecuta estos comandos y comparte los resultados:

```bash
# 1. Estado de Nginx
sudo systemctl status nginx

# 2. Configuración de Nginx
sudo cat /etc/nginx/sites-enabled/api.betstrading.online 2>/dev/null || echo "No config found"

# 3. Probar health check a través de Nginx
curl http://localhost/health

# 4. Puertos en uso
sudo netstat -tlnp | grep -E ':(80|443|5000)'

# 5. Últimos errores de Nginx
sudo tail -n 20 /var/log/nginx/error.log
```
