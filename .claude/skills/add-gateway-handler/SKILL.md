---
name: add-gateway-handler
description: Use when the bot must react to Discord gateway events (UserJoined, MessageDeleted, UserLeft, message logs, etc.) instead of a slash command.
---

# Añadir un handler de eventos del gateway

## Principio
Los eventos del gateway se manejan con implementaciones de `IGatewayEventHandler` (`Services/IGatewayEventHandler.cs`). `DiscordBotService` llama a `InitializeAsync()` de cada handler registrado **antes** de conectar, y ahí es donde se suscriben los eventos. Modelo de referencia: `Services/WelcomeHandler.cs`.

## Receta
1. Crear `Services/<Nombre>Handler.cs`, clase `sealed` que implementa `IGatewayEventHandler`.
2. En `InitializeAsync()`, suscribirse: `_client.EventoX += OnEventoXAsync;` y devolver `Task.CompletedTask`.
3. **Registrarlo en `Program.cs`** (esto sí es manual):
   ```csharp
   .AddSingleton<IGatewayEventHandler, MiHandler>()
   ```

## Convenciones obligatorias
- Configuración por servidor sale de `GuildConfig` (p. ej. `LogChannelId`, `WelcomeChannelId`); si el campo es `null`, el handler no hace nada — la feature es opt-in por servidor.
- BD: `await using var db = await _dbFactory.CreateDbContextAsync();` por operación.
- Envolver el envío de mensajes en try/catch con `_logger.LogWarning` — un canal borrado o sin permisos no debe tumbar el handler.
- Ignorar bots cuando aplique: `if (user.IsBot) return;`.
- Embeds con `BotEmbed.Create(...)`, texto visible en español.

## Intents
Los intents actuales (`Program.cs`): `AllUnprivileged | MessageContent | GuildMembers`, con `AlwaysDownloadUsers = true`. Si el evento nuevo necesita otro intent privilegiado (p. ej. `GuildPresences`), hay que añadirlo al `DiscordSocketConfig` **y** activarlo en el Discord Developer Portal, o el evento nunca llegará.

## Errores comunes
- Suscribir eventos fuera de `InitializeAsync` o crear el handler sin registrarlo en DI → nunca se ejecuta.
- Trabajo pesado directamente en el handler del evento bloquea el gateway; para tareas largas, despachar a un `Task.Run` o cola.
- `GetTextChannel` puede devolver null (canal borrado) — comprobar siempre, como hace `WelcomeHandler`.
