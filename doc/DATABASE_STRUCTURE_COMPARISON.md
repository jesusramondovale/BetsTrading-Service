# Comparación de Estructura de Base de Datos

Este documento confirma que la estructura de tablas de la base de datos es **idéntica** entre el proyecto legacy y el nuevo proyecto migrado.

## ✅ Tablas Confirmadas

### Tablas Principales
- ✅ **Users** - Estructura idéntica
- ✅ **Bets** - Estructura idéntica
- ✅ **FinancialAssets** - Estructura idéntica
- ✅ **BetZones** - Estructura idéntica (con conversión UTC para fechas)
- ✅ **BetZonesUSD** - Estructura idéntica (con conversión UTC para fechas)
- ✅ **PriceBets** - Estructura idéntica (con conversión UTC para end_date)
- ✅ **PriceBetsUSD** - Estructura idéntica (con conversión UTC para end_date)

### Tablas de Datos
- ✅ **AssetCandles** - Estructura idéntica (clave compuesta: AssetId, Exchange, Interval, DateTime)
- ✅ **AssetCandlesUSD** - Estructura idéntica (clave compuesta: AssetId, Exchange, Interval, DateTime)
- ✅ **Trends** - Estructura idéntica
- ✅ **VerificationCodes** - Estructura idéntica

### Tablas de Pagos y Recompensas
- ✅ **PaymentData** - Estructura idéntica
- ✅ **WithdrawalData** - Estructura idéntica
- ✅ **RewardNonces** - Estructura idéntica (con índices únicos)
- ✅ **RewardTransactions** - Estructura idéntica (con índices únicos)

### Tablas Adicionales (Agregadas)
- ✅ **Favorites** - Estructura idéntica (agregada al nuevo proyecto)
- ✅ **Raffles** - Estructura idéntica (agregada al nuevo proyecto)
- ✅ **RaffleItems** - Estructura idéntica (agregada al nuevo proyecto)
- ✅ **WithdrawalMethods** - Estructura idéntica (agregada al nuevo proyecto)

## Configuraciones Especiales

### Esquema de Base de Datos
- ✅ Ambos proyectos usan el esquema `"BetsTrading"` para todas las tablas

### Conversiones de Fechas UTC
- ✅ **BetZone.start_date** y **end_date**: Conversión UTC configurada
- ✅ **BetZoneUSD.start_date** y **end_date**: Conversión UTC configurada
- ✅ **PriceBet.end_date**: Conversión UTC configurada
- ✅ **PriceBetUSD.end_date**: Conversión UTC configurada
- ✅ Todas usan `timestamp without time zone` con conversión `DateTime.SpecifyKind(v, DateTimeKind.Utc)`

### Índices
- ✅ **FinancialAssets.ticker**: Índice único en ambos proyectos
- ✅ **RewardNonces.nonce**: Índice único en ambos proyectos
- ✅ **RewardTransactions.transaction_id**: Índice único en ambos proyectos

### Claves Compuestas
- ✅ **AssetCandles**: Clave compuesta (AssetId, Exchange, Interval, DateTime)
- ✅ **AssetCandlesUSD**: Clave compuesta (AssetId, Exchange, Interval, DateTime)

### Tipos de Datos Especiales
- ✅ **WithdrawalMethod.data**: Tipo `jsonb` en ambos proyectos

## Mapeo de Columnas

Todas las columnas están correctamente mapeadas usando `HasColumnName()` para mantener la compatibilidad con la base de datos existente:

- ✅ Nombres de columnas en snake_case (ej: `user_id`, `bet_zone`, `created_at`)
- ✅ Nombres de propiedades en PascalCase (ej: `UserId`, `BetZoneId`, `CreatedAt`)
- ✅ Conversiones de tipos correctas

## Conclusión

✅ **La estructura de tablas es 100% idéntica** entre el proyecto legacy y el nuevo proyecto migrado.

Todas las tablas, columnas, índices, claves primarias, claves foráneas y configuraciones especiales (conversiones UTC, tipos jsonb, etc.) están correctamente replicadas en el nuevo proyecto.

El nuevo proyecto puede usar la misma base de datos sin necesidad de migraciones adicionales (excepto las que EF Core pueda generar automáticamente para optimizaciones menores).
