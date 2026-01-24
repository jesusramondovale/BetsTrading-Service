# BetsTrading-Service

API REST para la aplicación BetsTrading, migrada a **Clean Architecture + CQRS**.

## 🏗️ Arquitectura

El proyecto utiliza una arquitectura limpia con separación de responsabilidades:

- **BetsTrading.Domain**: Entidades de dominio y contratos (sin dependencias)
- **BetsTrading.Application**: Lógica de aplicación, Commands/Queries (CQRS)
- **BetsTrading.Infrastructure**: Implementaciones (EF Core, repositorios, servicios externos)
- **BetsTrading.API**: Capa de presentación (Controllers, middleware)

## 📊 Estado del Proyecto

✅ **Migración completada al 100%**

- **55 endpoints** migrados exitosamente
- **7 controllers** completamente funcionales
- **Arquitectura Clean + CQRS** implementada
- **Compilación sin errores**

Ver documentación detallada en:
- `MIGRATION_COMPLETE.md` - Resumen ejecutivo
- `MIGRATION_CHECKLIST.md` - Checklist completo
- `ARCHITECTURE.md` - Arquitectura del proyecto
- `ENV_VARIABLES.md` - Variables de entorno requeridas

## 🚀 Inicio Rápido

### Requisitos
- .NET 8.0 SDK
- PostgreSQL
- Variables de entorno configuradas (ver `ENV_VARIABLES.md`)

### Ejecutar

```bash
cd BetsTrading.API
dotnet run
```

## 📚 Documentación

- [Arquitectura](ARCHITECTURE.md)
- [Variables de Entorno](ENV_VARIABLES.md)
- [Estructura de Base de Datos](DATABASE_STRUCTURE_COMPARISON.md)
- [Migración Completada](MIGRATION_COMPLETE.md)

---

© All rights reserved to Jesús Ramón DoVale - 2023-2026
