#!/bin/bash

# Script para limpiar, extraer, iniciar servicio y mostrar logs
# Preserva: ./CA, ./Logs, RestartServer.sh y *.tar
clear
echo "Iniciando proceso de reinicio..."

# Obtener la ruta del directorio actual
CURRENT_DIR=$(pwd)
echo "Directorio actual: $CURRENT_DIR"

# Desactivar auto-restart primero para evitar bucles
echo "Desactivando auto-restart del servicio..."
systemctl set-property bets-trading.service Restart=no 2>/dev/null || true
sleep 1

# Detener el servicio con timeout para evitar que se quede bloqueado
echo "Deteniendo servicio bets-trading.service..."
timeout 10 systemctl stop bets-trading.service 2>/dev/null || true
sleep 2

# Matar el proceso padre (PID más bajo) con "BetsTrading"
echo "Buscando proceso padre con 'BetsTrading'..."
PID=$(ps aux | grep "BetsTrading" | grep -v grep | awk '{print $2}' | sort -n | head -1)

if [ -n "$PID" ]; then
    echo "Matando proceso padre con PID: $PID"
    kill $PID 2>/dev/null
    sleep 2
    if ps -p $PID > /dev/null 2>&1; then
        echo "Proceso $PID aún activo, forzando kill -9..."
        kill -9 $PID 2>/dev/null
        sleep 1
    fi
    echo "Proceso $PID terminado"
else
    echo "No se encontró proceso 'BetsTrading'"
fi

# Encontrar todos los archivos y directorios excepto los que debemos preservar
echo "Eliminando archivos y directorios (preservando ./CA, ./Logs, RestartServer.sh y *.tar)..."

# Buscar y eliminar archivos/directorios, excluyendo los especificados
find . -maxdepth 1 \
  ! -name '.' \
  ! -name 'CA' \
  ! -name 'Logs' \
  ! -name 'RestartServer.sh' \
  ! -name '*.tar' \
  ! -name '.env' \
  -exec rm -rf {} +

# Encontrar el archivo .tar más reciente
LATEST_TAR=$(ls -t *.tar 2>/dev/null | head -1)

# Verificar que existe al menos un archivo tar
if [ -z "$LATEST_TAR" ]; then
    echo "Error: No se encuentra ningún archivo .tar en el directorio actual"
    exit 1
fi

echo "Archivo tar más reciente encontrado: $LATEST_TAR"

# Extraer el archivo tar más reciente
echo "Extrayendo $LATEST_TAR..."
tar -xf "$LATEST_TAR"

# Esperar un momento para asegurar que la extracción terminó completamente
sleep 2

# Desactivar auto-restart temporalmente para evitar bucles
echo "Desactivando auto-restart del servicio temporalmente..."
systemctl set-property bets-trading.service Restart=no 2>/dev/null || true
sleep 1

# Resetear el estado del servicio completamente
echo "Reseteando estado del servicio..."
systemctl reset-failed bets-trading.service 2>/dev/null
sleep 1

# Verificar que el ejecutable existe (asumiendo que el nombre del ejecutable es BetsTrading-API)
EXECUTABLE="./BetsTrading-API"
if [ -f "$EXECUTABLE" ]; then
    echo "Ejecutable encontrado: $EXECUTABLE"
    chmod +x "$EXECUTABLE" 2>/dev/null || true
else
    # Buscar el ejecutable en el directorio
    EXECUTABLE=$(find . -maxdepth 1 -name "BetsTrading.API" -type f 2>/dev/null | head -1)
    if [ -n "$EXECUTABLE" ]; then
        echo "Ejecutable encontrado: $EXECUTABLE"
        chmod +x "$EXECUTABLE" 2>/dev/null || true
    else
        echo "Advertencia: No se encontró el ejecutable BetsTrading en el directorio actual"
    fi
fi

# Verificar que el archivo de servicio systemd existe
SERVICE_FILE="/etc/systemd/system/bets-trading.service"
if [ ! -f "$SERVICE_FILE" ]; then
    echo "Error: El archivo de servicio $SERVICE_FILE no existe"
    echo "El servicio no puede iniciarse sin este archivo"
    exit 1
fi


# Verificar que el DLL existe
DLL_FILE="$CURRENT_DIR/BetsTrading.API.dll"
if [ ! -f "$DLL_FILE" ]; then
    echo "Error: No se encuentra BetsTrading.API.dll en $CURRENT_DIR"
    exit 1
fi


sleep 1

# Restaurar auto-restart
echo "Restaurando auto-restart del servicio..."
systemctl set-property bets-trading.service Restart=always 2>/dev/null || true
sleep 1

# Reiniciar el servicio SIEMPRE (lo iniciará si está apagado, lo reiniciará si está encendido)
echo "Iniciando servicio bets-trading.service..."
systemctl start bets-trading.service
RETVAL=$?

# Esperar un momento para que el servicio se inicie completamente
sleep 5

# Si falló al iniciar, intentar de nuevo después de limpiar más
if [ $RETVAL -ne 0 ]; then
    echo "Primer intento falló, limpiando y reintentando..."
    systemctl stop bets-trading.service 2>/dev/null
    systemctl reset-failed bets-trading.service 2>/dev/null
    sleep 2
    systemctl daemon-reload
    sleep 1
    systemctl start bets-trading.service
    sleep 5
fi

# Verificar el estado del servicio y los PIDs
echo ""
echo "=========================================="
echo "Verificando estado final del servicio..."
echo "=========================================="

SERVICE_ACTIVE=false
PIDS_EXIST=false

# Verificar si el servicio está activo
if systemctl is-active --quiet bets-trading.service; then
    SERVICE_ACTIVE=true
    echo "✓ Servicio está activo"
else
    echo "✗ Servicio NO está activo"
fi

# Verificar si existen PIDs de procesos "BetsTrading"
PIDS=$(ps aux | grep "BetsTrading" | awk '{print $2}')
if [ -n "$PIDS" ]; then
    PIDS_EXIST=true
    echo "✓ Procesos encontrados con PIDs: $PIDS"
else
    echo "✗ No se encontraron procesos 'BetsTrading'"
fi

# Mostrar resultado final
echo ""
if [ "$SERVICE_ACTIVE" = true ] && [ "$PIDS_EXIST" = true ]; then
    echo "=========================================="
    echo "✅ OK - Servicio levantado correctamente"
    echo "=========================================="
else
    echo "=========================================="
    echo "❌ ERROR - El servicio no está funcionando correctamente"
    echo "=========================================="
    echo "Estado del servicio:"
    echo "=========================================="
    systemctl status bets-trading.service --no-pager -l
    echo ""
    echo "=========================================="
    echo "Logs detallados del servicio (últimas 10 líneas):"
    echo "=========================================="
    journalctl -u bets-trading.service -n 10 --no-pager
    echo ""
    echo "=========================================="
    echo "Información del sistema:"
    echo "=========================================="
    echo "Directorio actual: $(pwd)"
    echo "Ejecutable encontrado: ${EXECUTABLE:-'NO ENCONTRADO'}"
    if [ -f "$SERVICE_FILE" ]; then
        echo "Archivo de servicio: $SERVICE_FILE (existe)"
        echo "Contenido del archivo de servicio:"
        cat "$SERVICE_FILE"
    else
        echo "Archivo de servicio: $SERVICE_FILE (NO EXISTE)"
    fi
    echo ""
    echo "Verifica:"
    echo "1. El archivo /etc/systemd/system/bets-trading.service existe y es válido"
    echo "2. La ruta del ejecutable en el archivo de servicio es correcta: debe apuntar a $(pwd)/BetsTrading.API.dll"
    echo "3. Los permisos del ejecutable y directorios son correctos"
    echo "4. No hay problemas de recursos (memoria, disco, etc.)"
    echo "5. Si el archivo de servicio referencia archivos EnvironmentFile que no existen"
    echo "=========================================="
    sleep 3
fi

