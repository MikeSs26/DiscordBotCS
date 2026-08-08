# Comandos de moderación y activación del canal de logs

Fecha: 2026-08-07

## Problema

`GuildConfig.LogChannelId` y el comando `/config canal-log` existen desde el primer commit,
pero nada escribe en ese canal: la feature quedó a medio cablear. Al mismo tiempo el bot no
tiene ninguna herramienta de moderación.

Ambas cosas se resuelven juntas: los comandos de moderación son precisamente lo que da
sentido a un canal de registro.

## Alcance

Tres comandos, todos de nivel superior (no agrupados) porque en una situación urgente se
escriben más rápido, y todos restringidos a servidores:

| Comando | Permiso requerido | Acción |
|---|---|---|
| `/limpiar <cantidad> [usuario]` | `ManageMessages` | Borra de 1 a 100 mensajes, opcionalmente solo los de un usuario |
| `/timeout <usuario> <minutos> [razón]` | `ModerateMembers` | Aísla a un miembro (máximo 28 días, límite de Discord) |
| `/expulsar <usuario> [razón]` | `KickMembers` | Expulsa a un miembro |

Cada comando declara además `[RequireBotPermission]`, para que el fallo por permisos del
propio bot llegue como mensaje claro en vez de como excepción de la API.

### Fuera de alcance

Sistema de avisos con persistencia, números de caso, historial de sanciones y apelaciones.
`/banear` se omite por ser casi idéntico a `/expulsar`; añadirlo después es trivial.

## Registro en el canal de logs

### Decisión: servicio inyectado

Se evaluaron tres opciones:

- **Escribir desde cada comando.** Duplica el formato del embed y la comprobación de canal
  configurado en cada sitio, y volvería a duplicarse al registrar eventos del gateway.
- **Un `ModerationLogService` inyectado por DI.** Elegida.
- **Eventos internos con un handler suscrito.** Sobreingeniería para tres comandos.

El servicio es una unidad pequeña con un solo propósito: los comandos le dicen *qué pasó* y
él decide *dónde y cómo se ve*. Cuando más adelante se registren eventos del gateway
(mensajes borrados, miembros que se van), ya está listo sin tocar los comandos.

```csharp
Task LogAsync(IGuild guild, ModerationAction action, IUser moderator,
              string description, string? reason = null);
```

`ModerationAction` es un enum que determina título, emoji y color del embed. El comando
compone la descripción legible ("Se eliminaron 15 mensajes en #general").

### Comportamiento

- Si el servidor no tiene `LogChannelId` configurado, el servicio no hace nada. La feature
  sigue siendo opt-in por servidor, igual que las bienvenidas.
- Si el canal fue borrado o el bot no tiene permiso para escribir en él, se registra un
  `LogWarning` y nada más.

**Un fallo al registrar nunca puede hacer fallar el comando.** Cuando el log se escribe, la
expulsión ya ocurrió; que el canal haya desaparecido no puede convertirse en un error de cara
al moderador. El `try/catch` vive dentro del servicio.

## Comprobaciones de jerarquía

Es la parte con casos límite reales. Antes de aplicar un timeout o una expulsión hay que
verificar que el objetivo no es quien ejecuta el comando, no es el propio bot, no es el dueño
del servidor, y que su rol más alto está por debajo tanto del moderador como del bot.

Esta lógica es pura, sin dependencias de red, así que vive en su propio tipo en lugar de
enterrada en los comandos:

```csharp
public readonly record struct ModerationSubject(ulong Id, int Hierarchy);

public enum ModerationCheck
{
    Allowed, TargetIsSelf, TargetIsBot, TargetIsOwner,
    TargetOutranksModerator, TargetOutranksBot
}

public static ModerationCheck Check(ModerationSubject moderator, ModerationSubject target,
                                    ModerationSubject bot, ulong guildOwnerId);
```

El orden de las comprobaciones importa, porque determina qué mensaje recibe el moderador
cuando se cumple más de una condición. El dueño del servidor omite la comparación de rangos
contra sí mismo, pero sigue sujeto a que el bot pueda actuar sobre el objetivo.

`SocketGuildUser.Hierarchy` ya devuelve la posición del rol más alto, e `int.MaxValue` para
el dueño, así que el mapeo desde Discord.Net es directo.

## Tests

El repositorio no tenía infraestructura de tests. Se añade un proyecto xUnit mínimo en
`tests/DiscordBotCS.Tests` que cubre `ModerationGuard`: caso permitido, objetivo es uno
mismo, objetivo es el bot, objetivo es el dueño, rangos iguales o superiores al moderador,
rango superior al bot, y el caso del dueño actuando sobre alguien de rango mayor que el suyo.

Los comandos en sí no se testean: requerirían simular buena parte de la API de Discord y el
valor no compensa. Se verifican manualmente contra el servidor de pruebas.

Como el proyecto principal está en la raíz del repositorio, su glob de compilación incluiría
los ficheros del proyecto de tests. `DiscordBotCS.csproj` los excluye explícitamente.

## Cambios en ficheros existentes

- `Program.cs`: registrar `ModerationLogService` en el contenedor.
- `Modules/Shared/BotEmbed.cs`: añadir el color `Warning` (ámbar) para los timeouts.
- `DiscordBotCS.csproj`: excluir `tests/**` de la compilación.
