---
name: run-and-test-bot
description: Use when running the bot locally, verifying a change works against Discord, registering slash commands, or troubleshooting startup (token, secrets, TestGuildId).
---

# Ejecutar y probar el bot

## Arranque
```powershell
dotnet build                        # verificación rápida sin conectar
dotnet test tests\DiscordBotCS.Tests  # lógica pura (ModerationGuard)
dotnet run                          # arranca el bot (aplica migraciones y conecta)
```
El bot queda corriendo en primer plano; logs por consola vía Serilog. Detener con Ctrl+C (apagado limpio en `DiscordBotService.StopAsync`).

Desde Claude Code: lanzar `dotnet run` con `run_in_background: true`, observar los logs y detener el proceso al terminar la verificación.

## Configuración (User Secrets — nunca en appsettings.json)
```powershell
dotnet user-secrets set "Discord:Token" "<token>"
dotnet user-secrets set "Discord:TestGuildId" "<id del servidor de pruebas>"
dotnet user-secrets list
```
- Sin token: el bot loguea `LogCritical` y sale sin conectar — ese es el síntoma de secrets sin configurar.
- **`TestGuildId` definido** → los comandos se registran solo en ese servidor y aparecen al instante. **Sin definir** → registro global (hasta ~1h de propagación). Para desarrollo, tenerlo siempre definido.

## Verificación de un cambio
1. `dotnet build` sin errores.
2. `dotnet run` y esperar en logs: `Connected as <bot>` y `Slash commands registered to test guild ...`.
3. Probar el comando/evento real en el servidor de pruebas.
4. Un comando nuevo que no aparece en Discord: confirmar que la clase hereda de `InteractionModuleBase<SocketInteractionContext>` y revisar si el log dice "registered to test guild" o "registered globally".

## Errores comunes
- Eventos de miembros (bienvenidas) no llegan → falta activar el intent privilegiado **Server Members** en el Discord Developer Portal (el código ya pide `GuildMembers`).
- `bot.db` bloqueada o corrupta en desarrollo: parar el bot y borrar `bot.db` — las migraciones la recrean al arrancar (solo datos locales de prueba).
- Los tests cubren solo lógica pura (`tests/DiscordBotCS.Tests`). Los comandos y handlers no se testean: requerirían simular la API de Discord. Para ellos la verificación es build + prueba manual contra el servidor de pruebas.
- Al añadir lógica con casos límite (jerarquías, límites de la API, parseo), extráela a un tipo puro sin dependencias de Discord.Net y testéala ahí, como se hizo con `ModerationGuard`.
