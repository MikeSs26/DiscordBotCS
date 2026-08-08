---
name: db-schema-changes
description: Use when adding or changing persisted data - new entities, new columns in GuildConfig, EF Core migrations, or queries against bot.db (SQLite).
---

# Cambios de esquema y datos (EF Core + SQLite)

## Principio
La BD es SQLite (`bot.db`), gestionada por `BotDbContext` con **migraciones que se aplican solas al arrancar** (`db.Database.MigrateAsync()` en `Program.cs`). Nunca editar `bot.db` a mano ni usar `EnsureCreated`.

## Receta: añadir un campo o entidad
1. Editar/crear la entidad en `Data/Entities/` (clases `sealed`, XML doc por propiedad, snowflakes de Discord como `ulong`/`ulong?`).
2. Entidad nueva: añadir `DbSet<T>` en `Data/BotDbContext.cs` y configurar la clave en `OnModelCreating`. Si la clave es un snowflake de Discord, añadir `.ValueGeneratedNever()` (el id viene de Discord, no lo genera la BD) — ver la configuración de `GuildConfig`.
3. Generar la migración (dotnet-ef es tool local, ver `dotnet-tools.json`):
   ```powershell
   dotnet tool restore
   dotnet ef migrations add NombreDescriptivo
   ```
4. Revisar la migración generada en `Data/Migrations/` antes de dar por bueno el cambio.
5. No hace falta `database update`: la migración se aplica al arrancar el bot. Para forzarla ya: `dotnet ef database update`.

## Convenciones obligatorias
- Acceso siempre vía `IDbContextFactory<BotDbContext>`: `await using var db = await _dbFactory.CreateDbContextAsync();` — contexto corto por operación (el bot es multievento concurrente; un DbContext compartido no es thread-safe).
- Lookup por clave: `db.GuildConfigs.FindAsync(guildId)`.
- Upsert (patrón de `ConfigModule.SaveAsync`): comprobar existencia con `AnyAsync` y fijar `EntityState.Added`/`Modified`.
- Campos de configuración opcionales → nullable (`ulong?`), y los consumidores tratan `null` como "feature desactivada".
- `DesignTimeDbContextFactory` existe para que `dotnet ef` funcione sin arrancar el host — si cambia la connection string, actualizarla también ahí.

## Errores comunes
- Olvidar generar la migración tras tocar la entidad → excepción de esquema al arrancar.
- Renombrar columnas genera drop+add en SQLite (pierde datos); revisar la migración y usar `RenameColumn` si procede.
- `bot.db` es estado local de desarrollo: no committearlo.
