#MARBELY
#!/bin/bash

# Generar timestamp con fecha y hora
TIMESTAMP=$(date +"%Y-%m-%d_%H%M")
OUTFILE="informe_monitoreo_${TIMESTAMP}.txt"

# Recoger información
HOSTNAME=$(hostname)
UPTIME=$(uptime -p)
OS=$(grep PRETTY_NAME /etc/os-release | cut -d= -f2 | tr -d '"')

#CPU info
CPU_LOAD=$(uptime | awk -F'load average:' '{print $2}' | cut -d, -f1 | xargs)
CPU_TOTAL=$(nproc)
CPU_STATUS="Dentro del umbral (< 80 %)"
if (( $(echo "$CPU_LOAD > 0.80" | bc -l) )); then
    CPU_STATUS="Carga elevada"
fi
read USER_CPU SYSTEM_CPU NICE_CPU IDLE_CPU WAIT_CPU HW_CPU SW_CPU STEAL_CPU < <(
  top -b -n1 | grep "Cpu(s)" | sed 's/,/./g' | awk '{
    match($0, /([0-9.]+) us/, a);
    match($0, /([0-9.]+) sy/, b);
    match($0, /([0-9.]+) ni/, c);
    match($0, /([0-9.]+) id/, d);
    match($0, /([0-9.]+) wa/, e);
    match($0, /([0-9.]+) hi/, f);
    match($0, /([0-9.]+) si/, g);
    match($0, /([0-9.]+) st/, h);
    print a[1], b[1], c[1], d[1], e[1], f[1], g[1], h[1]
  }')


# Memoria
MEM_TOTAL=$(free -h | awk '/Mem:/ {print $2}')
MEM_USED=$(free -h | awk '/Mem:/ {print $3}')
SWAP_USED=$(free -h | awk '/Swap:/ {print $3}')
MEM_STATUS="Sin cuellos de botella"
[[ "$SWAP_USED" != "0B" ]] && MEM_STATUS="Uso de swap detectado"

# Disco
DISK_USAGE=$(df -h / | awk 'NR==2 {print $3}')
DISK_TOTAL=$(df -h / | awk 'NR==2 {print $2}')
DISK_PERCENT=$(df -h / | awk 'NR==2 {print $5}' | tr -d '%')
DISK_STATUS="Vigilar uso de disco"
[[ $DISK_PERCENT -lt 80 ]] && DISK_STATUS="Sin alerta de espacio"

# Conexiones de red
CONNECTIONS=$(ss -tun | grep ESTAB | wc -l)

# Errores críticos del sistema (excluyendo sshd, pam, swap)
ERROR_LOG=$(journalctl -p 3 --since today --no-pager | grep -vE 'sshd|pam|swap')
LOG_ERRORS=$(echo "$ERROR_LOG" | grep -c .)

if [ "$LOG_ERRORS" -gt 0 ]; then
  TOP_ERRORS=$(echo "$ERROR_LOG" | awk -F':' '{print $4}' | sort | uniq -c | sort -nr | head -3)
  LOG_STATUS="$LOG_ERRORS errores críticos detectados"
else
  TOP_ERRORS="(ninguno)"
  LOG_STATUS="Sin errores críticos"
fi


# Seguridad SSH
SSH_FAILS=$(journalctl -u sshd --since "24 hours ago" | grep 'Failed password' | wc -l)
SSH_ERRORS=$(journalctl -u sshd --since today --no-pager | grep -i "error" | wc -l)



# Actualizaciones
UPDATES_TOTAL=$(dnf check-update | grep -E '^[a-zA-Z0-9]' | wc -l)
SEC_UPDATES=$(dnf updateinfo summary | grep 'Aviso(s) de seguridad' | awk '{print $1}')
SEC_UPDATES=${SEC_UPDATES:-0}
UPDATE_STATUS="Sin actualizaciones pendientes"
[[ $UPDATES_TOTAL -gt 0 ]] && UPDATE_STATUS="$UPDATES_TOTAL actualizaciones disponibles, incluyendo $SEC_UPDATES de seguridad"

# Puertos específicos
#PORTS=$(ss -tulpn | awk 'NR>1 && $5 ~ /:[0-9]+$/ {split($5, a, ":"); print a[2]}' | sort -un | tr '\n' ' ')
PORTS=$(ss -lntu | awk 'NR>1 {split($5, a, ":"); print a[length(a)]}' | sort -un | tr '\n' ' ')


# Crear informe
cat <<EOF > "$OUTFILE"
===============================
   INFORME DEL SISTEMA
===============================

🔹 Resumen del sistema:
Uptime:        $UPTIME
Hostname:      $HOSTNAME
OS:            $OS

🔹 Rendimiento CPU:
Carga actual:  $CPU_LOAD de $CPU_TOTAL núcleos de CPU
Estado:        $CPU_STATUS
CPU en uso de usuario:                  $USER_CPU% 
CPU en uso de sistema (kernel):         $SYSTEM_CPU% 
CPU en procesos nice:                   $NICE_CPU% 
CPU inactiva:                           $IDLE_CPU% 
CPU esperando por I/O:                  $WAIT_CPU% 
CPU en interrupciones de hardware:      $HW_CPU% 
CPU en interrupciones de software:      $SW_CPU% 
CPU robada por otros entornos (VMs):    $STEAL_CPU%  

🔹 Memoria:
Uso:           $MEM_USED / $MEM_TOTAL
Swap:          $SWAP_USED
Estado:        $MEM_STATUS

🔹 Disco:
Uso:           $DISK_USAGE / $DISK_TOTAL
Estado:        $DISK_STATUS

🔹 Red:
Conexiones establecidas: $CONNECTIONS

🔹 Registros del sistema:
Errores críticos: $LOG_ERRORS
Estado:           $LOG_STATUS
Errores más frecuentes:
$TOP_ERRORS

🔹 Seguridad SSH:
Errores totales detectados: $SSH_ERRORS
🔹Registros(Logs):
Intentos fallidos de login (últimas 24h): $SSH_FAILS

🔹 Actualizaciones del sistema:
$UPDATE_STATUS

🔹 Servicios:
Puertos escuchando: $PORTS

EOF

echo "✅ Informe generado: $OUTFILE"
