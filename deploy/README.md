# Despliegue en el droplet

Configuración única del servidor y luego despliegues con un solo comando.

## Estado actual

El droplet **24.144.98.61** ya tiene la configuración inicial hecha (usuario, directorios,
fichero de entorno y unidad de systemd habilitada). La sección "Configuración inicial" de
más abajo queda como referencia por si hay que rehacerlo o montar otro servidor.

Para desplegar el día a día basta con `.\deploy\deploy.ps1`.

| Dato | Valor |
|---|---|
| SO | Ubuntu 24.04 LTS, x86_64 |
| Recursos | 1 CPU, 1 GB RAM (+1 GB swap), 24 GB disco |
| Acceso | `root` por clave ed25519; contraseña deshabilitada |
| Otros servicios | nginx activo en el puerto 80 |
| Firewall | `ufw` activo: entrada solo por 22, 80 y 443 |

El droplet solo trae el runtime de **.NET 8**, y este proyecto apunta a **net10.0**. Por eso
se publica en modo *self-contained*: no depende de nada instalado en el servidor. No intentes
cambiarlo a *framework-dependent* sin instalar antes el runtime de .NET 10.

## Diseño

Tres rutas separadas, y la separación es intencionada:

| Ruta | Contenido | Se borra en cada despliegue |
|---|---|---|
| `/opt/discordbot` | Binarios publicados | **Sí** |
| `/var/lib/discordbot` | `bot.db` (la base de datos) | No |
| `/etc/discordbot` | Token y configuración | No |

**La base de datos vive fuera del directorio de la aplicación a propósito.** El despliegue vacía `/opt/discordbot`; si `bot.db` estuviera ahí, perderías la configuración de todos los servidores en cada actualización. La cadena de conexión por defecto (`Data Source=bot.db`) es una ruta *relativa*, así que en producción se sobrescribe con una absoluta mediante variable de entorno.

No hace falta instalar .NET en el droplet: se publica en modo *self-contained*, con el runtime incluido en el binario.

## Configuración inicial (una sola vez)

Conéctate al droplet y ejecuta:

```bash
# 1. Usuario de servicio sin login ni home.
sudo useradd --system --no-create-home --shell /usr/sbin/nologin discordbot

# 2. Directorios.
sudo mkdir -p /opt/discordbot /var/lib/discordbot /etc/discordbot
sudo chown discordbot:discordbot /opt/discordbot /var/lib/discordbot

# 3. Fichero de entorno con el token.
sudo tee /etc/discordbot/discordbot.env > /dev/null <<'EOF'
Discord__Token=PEGA_AQUI_TU_TOKEN
ConnectionStrings__Default=Data Source=/var/lib/discordbot/bot.db
DOTNET_ENVIRONMENT=Production
EOF

# 4. Solo root puede leerlo: contiene el token.
sudo chmod 600 /etc/discordbot/discordbot.env
sudo chown root:root /etc/discordbot/discordbot.env
```

El doble guion bajo (`Discord__Token`) es la forma en que .NET traduce la anidación de secciones de `appsettings.json` a variables de entorno. No hay que tocar código: el proveedor de variables de entorno ya está activo y tiene prioridad sobre el fichero.

**No definas `Discord__TestGuildId` en producción.** Sin esa variable, los comandos se registran globalmente, que es lo que quieres para un bot en uso real.

Copia el fichero de servicio desde tu máquina:

```powershell
scp .\deploy\discordbot.service usuario@IP:/tmp/
```

Y actívalo en el droplet:

```bash
sudo mv /tmp/discordbot.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable discordbot    # arranque automático tras reiniciar
```

## Desplegar

Desde la raíz del proyecto, en tu máquina:

```powershell
.\deploy\deploy.ps1 -ServerHost <IP> -SshUser <usuario>
```

El script publica, comprime, sube un único tarball, reemplaza los binarios, reinicia el servicio y comprueba que arrancó. Si falla, muestra las últimas 40 líneas del log y termina con error.

Si el droplet fuera ARM en lugar de x86, añade `-Runtime linux-arm64`.

## Operación

```bash
sudo systemctl status discordbot        # estado
sudo journalctl -u discordbot -f        # logs en vivo
sudo journalctl -u discordbot -n 100    # últimas 100 líneas
sudo systemctl restart discordbot       # reiniciar
```

Serilog escribe a stdout y systemd lo recoge en journald, así que no hay ficheros de log que rotar.

### Firewall

`ufw` está activo con entrada denegada por defecto y solo tres puertos abiertos: 22 (SSH),
80 y 443 (nginx). El bot **no necesita ningún puerto entrante** — solo abre una conexión
saliente hacia el gateway de Discord, y el tráfico de salida está permitido.

Si algún día tienes que tocar las reglas, hazlo siempre con red de seguridad para no quedarte
fuera del servidor. Programa el desactivado automático **antes** de aplicar el cambio:

```bash
systemd-run --on-active=300 --unit=ufw-rollback /usr/sbin/ufw --force disable
# ...aplica los cambios y comprueba que puedes abrir una conexion SSH NUEVA...
systemctl stop ufw-rollback.timer   # cancela el rollback solo si funciona
```

Si el cambio te deja fuera, no hagas nada: a los 5 minutos el firewall se apaga solo y
recuperas el acceso.

### Copia de seguridad de la base de datos

```bash
sudo -u discordbot sqlite3 /var/lib/discordbot/bot.db "VACUUM INTO '/tmp/bot-backup.db'"
```

`VACUUM INTO` genera una copia consistente con el bot en marcha (necesario si algún día activas WAL). Sin `sqlite3` instalado, basta con parar el servicio y copiar el fichero.

## Problemas frecuentes

**`Permission denied (publickey)` al desplegar.** La clave `id_ed25519` tiene passphrase y
necesita estar cargada en el agente de Windows:

```powershell
Get-Service ssh-agent          # debe estar Running
ssh-add -l                     # debe listar la huella SHA256:RENkaXcg...
```

Si el servicio está parado, actívalo desde una PowerShell **como administrador** con
`Set-Service ssh-agent -StartupType Automatic; Start-Service ssh-agent; ssh-add`.

Ojo: el `ssh` de Git Bash usa un agente distinto al de Windows y **no ve esta clave**.
Los despliegues van por PowerShell precisamente por eso.

**El servicio arranca y se para en bucle.** Mira `journalctl -u discordbot -n 50`. Lo más común es el token: si falta o es inválido, el bot registra `No Discord token configured` y termina. Revisa el `.env` y que no tenga comillas alrededor del valor.

**El bot conecta pero los comandos no aparecen.** El registro global tarda hasta una hora en propagarse la primera vez. Confirma en los logs que dice `Slash commands registered globally`.

**Las bienvenidas no funcionan.** Falta activar el intent privilegiado *Server Members* en el Discord Developer Portal. Es configuración del portal, no del servidor.

**La configuración se perdió tras desplegar.** Señal de que `ConnectionStrings__Default` no está definido y la base de datos se creó dentro de `/opt/discordbot`, que se vacía en cada despliegue. Revisa el fichero de entorno.
