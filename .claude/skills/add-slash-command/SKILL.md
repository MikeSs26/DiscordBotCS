---
name: add-slash-command
description: Use when adding a new slash command, command group, or command module to this bot (Discord.Net InteractionModuleBase).
---

# Añadir un comando slash

## Principio
Los módulos se descubren por reflexión (`InteractionHandler.InitializeAsync` escanea el assembly). Crear la clase en `Modules/` basta — **no hay que registrar nada en `Program.cs`** salvo que el módulo necesite un servicio nuevo en DI.

## Receta
1. Crear `Modules/<Nombre>Module.cs`, clase `sealed` que hereda de `InteractionModuleBase<SocketInteractionContext>`.
2. Comandos sueltos: `[SlashCommand("nombre", "Descripción.")]`. Grupo: `[Group("nombre", "Descripción.")]` en la clase (ver `UserModule`, `ConfigModule`).
3. Dependencias por constructor: `DiscordSocketClient`, `IDbContextFactory<BotDbContext>`, etc. — DI las resuelve.
4. Restricciones vía atributos, no comprobaciones manuales:
   - Solo en servidor → `[RequireContext(ContextType.Guild)]`
   - Requiere permisos → `[DefaultMemberPermissions(GuildPermission.X)]`

## Convenciones obligatorias
- **Todo el texto visible por el usuario en español** (nombres de comandos, descripciones, `[Summary]`, respuestas). Código y comentarios en inglés.
- **Embeds siempre con `BotEmbed.Create(...)`** (`Modules/Shared/BotEmbed.cs`) — nunca `new EmbedBuilder()` directo. Colores: `Brand`, `Success`, `Danger`.
- Respuestas de configuración/administración → `ephemeral: true`.
- Fechas en Discord con timestamps: `$"<t:{fecha.ToUnixTimeSeconds()}:R>"`.
- Parámetros opcionales de usuario: `IUser? user = null` + `user ??= Context.User;` (ver `UserModule`).
- Acceso a BD: `await using var db = await _dbFactory.CreateDbContextAsync();` — contexto corto por operación, nunca cachear el DbContext.

## Errores comunes
- Responder dos veces: una interacción admite un solo `RespondAsync`; para trabajo largo usar `DeferAsync()` + `FollowupAsync`/`ModifyOriginalResponseAsync` (ver `/ping` en `GeneralModule`).
- Los errores de precondición/argumentos ya los maneja `InteractionHandler.OnInteractionExecutedAsync` con mensajes en español — no envolver comandos en try/catch genéricos.
- Comandos globales tardan ~1h en propagarse; para probar, usar `TestGuildId` (ver skill run-and-test-bot).
