# Arquitectura Clean + CQRS

## Estructura del Proyecto

```
BetsTrading-Service/
│
├── 📦 BetsTrading.Domain/              # Capa de Dominio (sin dependencias)
│   ├── Entities/                        # Entidades de dominio con lógica de negocio
│   │   ├── Bet.cs
│   │   ├── User.cs
│   │   ├── BetZone.cs
│   │   └── FinancialAsset.cs
│   ├── Interfaces/                      # Contratos (puertos)
│   │   ├── IRepository.cs
│   │   ├── IBetRepository.cs
│   │   ├── IUserRepository.cs
│   │   ├── IBetZoneRepository.cs
│   │   ├── IFinancialAssetRepository.cs
│   │   └── IUnitOfWork.cs
│   └── Exceptions/                      # Excepciones de dominio
│       └── InsufficientPointsException.cs
│
├── 📦 BetsTrading.Application/          # Capa de Aplicación
│   ├── Commands/                        # Escritura (CQRS)
│   │   └── Bets/
│   │       ├── CreateBetCommand.cs
│   │       ├── CreateBetCommandHandler.cs
│   │       └── CreateBetCommandValidator.cs
│   ├── Queries/                         # Lectura (CQRS)
│   │   └── Bets/
│   │       ├── GetUserBetsQuery.cs
│   │       └── GetUserBetsQueryHandler.cs
│   ├── DTOs/                            # Data Transfer Objects
│   │   └── BetDto.cs
│   ├── Services/                        # Servicios de aplicación
│   │   └── BetCalculationService.cs
│   └── Mappings/                        # AutoMapper profiles
│       └── MappingProfile.cs
│
├── 📦 BetsTrading.Infrastructure/       # Capa de Infraestructura
│   └── Persistence/
│       ├── AppDbContext.cs              # DbContext con mapeos
│       ├── Repositories/                # Implementaciones de repositorios
│       │   ├── Repository.cs
│       │   ├── BetRepository.cs
│       │   ├── UserRepository.cs
│       │   ├── BetZoneRepository.cs
│       │   └── FinancialAssetRepository.cs
│       └── UnitOfWork.cs                # Patrón Unit of Work
│
└── 📦 BetsTrading.API/                  # Capa de Presentación
    ├── Controllers/
    │   └── BetController.cs             # Solo delega a MediatR
    ├── Middleware/
    │   └── ExceptionHandlingMiddleware.cs
    └── Program.cs                        # Configuración y DI
```

## Flujo de una Operación

### Crear Apuesta (Command)

```
1. Cliente → POST /api/Bet/NewBet
2. BetController → CreateBetCommand
3. MediatR → CreateBetCommandHandler
4. Handler:
   - Valida usuario (IUserRepository)
   - Valida bet zone (IBetZoneRepository)
   - Crea entidad Bet (lógica de dominio)
   - Deducte puntos (método de dominio)
   - Guarda cambios (UnitOfWork)
5. Retorna CreateBetResult
```

### Obtener Apuestas (Query)

```
1. Cliente → POST /api/Bet/UserBets
2. BetController → GetUserBetsQuery
3. MediatR → GetUserBetsQueryHandler
4. Handler:
   - Obtiene bets (IBetRepository)
   - Obtiene assets (IFinancialAssetRepository)
   - Obtiene bet zones (IBetZoneRepository)
   - Calcula necessary gain (BetCalculationService)
   - Mapea a DTOs
5. Retorna IEnumerable<BetDto>
```

## Principios Aplicados

### Clean Architecture
- ✅ Dependencias hacia adentro
- ✅ Domain sin dependencias externas
- ✅ Application depende solo de Domain
- ✅ Infrastructure implementa interfaces de Domain

### CQRS
- ✅ Separación de Commands (escritura) y Queries (lectura)
- ✅ Handlers especializados por operación
- ✅ Optimización independiente de lecturas y escrituras

### DDD (Domain Driven Design)
- ✅ Entidades con lógica de negocio
- ✅ Métodos de dominio (DeductPoints, MarkAsWon, etc.)
- ✅ Excepciones de dominio
- ✅ Value Objects (preparado para agregar)

## Tecnologías

- **MediatR** 14.0.0 - Desacoplamiento y CQRS
- **FluentValidation** 12.1.1 - Validación de comandos
- **AutoMapper** 12.0.1 - Mapeo de entidades a DTOs
- **Entity Framework Core** 8.0.4 - ORM
- **PostgreSQL** 8.0.4 - Base de datos

## Estado de Migración

### ✅ Completado (100%)
- [x] Estructura de proyectos (Domain, Application, Infrastructure, API)
- [x] Domain entities (Bet, User, BetZone, FinancialAsset, PriceBet, Favorite, Raffle, RaffleItem, WithdrawalMethod, etc.)
- [x] Repositorios y UnitOfWork (todos los módulos)
- [x] Commands y Queries (todos los módulos migrados)
- [x] Middleware de excepciones
- [x] Configuración de MediatR, AutoMapper, FluentValidation
- [x] **Módulo Auth** - ✅ COMPLETO (12 endpoints)
- [x] **Módulo Payments** - ✅ COMPLETO (3 endpoints)
- [x] **Módulo FinancialAssets** - ✅ COMPLETO (5 endpoints)
- [x] **Módulo Info** - ✅ COMPLETO (18 endpoints)
- [x] **Módulo Bets** - ✅ COMPLETO (13 endpoints)
- [x] **Módulo Rewards** - ✅ COMPLETO (2 endpoints)
- [x] **Módulo Didit** - ✅ COMPLETO (2 endpoints)
- [x] Autenticación JWT (Local + Google)
- [x] Seguridad (Rate Limiting, HSTS, HTTPS, CORS)
- [x] Logging (Serilog)
- [x] Hosted Services (UpdaterService, OddsAdjusterService)
- [x] Servicios externos (Email, Firebase, Stripe, Didit)

### 📋 Pendiente (Opcional - Mejoras Futuras)
- [ ] Agregar tests unitarios
- [ ] Agregar health checks para servicios externos
- [ ] Implementar caché
- [ ] Optimizaciones de performance
- [ ] Swagger con ejemplos
- [ ] Documentación completa de API

## Cómo Usar

### Ejecutar la nueva API

```bash
cd BetsTrading.API
dotnet run
```

### Ejecutar el proyecto legacy (temporal)

```bash
dotnet run --project BetsTrading-Service.csproj
```

Ambos pueden coexistir durante la migración.
