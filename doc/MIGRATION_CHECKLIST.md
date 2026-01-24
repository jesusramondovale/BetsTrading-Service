# Checklist de Migración

## ✅ Completado

### Estructura Base
- [x] Proyectos Domain, Application, Infrastructure, API
- [x] Entidades de dominio (Bet, User, BetZone, FinancialAsset)
- [x] Repositorios y UnitOfWork
- [x] Commands y Queries básicos (Bets)
- [x] Middleware de excepciones
- [x] Configuración MediatR, AutoMapper, FluentValidation

### Módulo Bets
- [x] CreateBetCommand + Handler
- [x] GetUserBetsQuery + Handler
- [x] GetUserBetQuery + Handler (apuesta específica por ID)
- [x] GetHistoricUserBetsQuery + Handler
- [x] GetBetZoneQuery + Handler
- [x] DeleteRecentBetCommand + Handler
- [x] DeleteHistoricBetsCommand + Handler (incluye PriceBets)
- [x] BetCalculationService (soporta EUR y USD)
- [x] PriceBet functionality - ✅ COMPLETO
  - [x] Entidades PriceBet y PriceBetUSD en Domain
  - [x] Repositorios IPriceBetRepository e IPriceBetUsdRepository
  - [x] CreatePriceBetCommand + Handler
  - [x] GetPriceBetsQuery + Handler
  - [x] GetHistoricPriceBetsQuery + Handler
  - [x] DeleteRecentPriceBetCommand + Handler
  - [x] PriceBetCostService (cálculo de costos)
  - [x] BetController migrado (todos los métodos completados)

---

## 🔄 Pendiente - Crítico

### 1. Autenticación y Autorización
- [x] JWT Bearer Authentication (Local)
- [x] Google JWT Authentication
- [x] Policy Scheme combinado (Google + Local)
- [x] Generación de tokens JWT (JwtTokenService)
- [x] Validación de tokens en middleware
- [x] Claims personalizados

### 2. Configuración de Seguridad
- [x] Rate Limiting (AspNetCoreRateLimit)
- [x] HSTS
- [x] HTTPS Redirection
- [x] Response Compression
- [x] CORS (si es necesario)

### 3. Logging
- [x] Serilog configurado
- [x] File logging con rolling interval
- [x] ICustomLogger interface migrada
- [x] Logging en handlers y middleware

### 4. Base de Datos
- [x] Migraciones de EF Core (configurado en Program.cs)
- [x] Aplicación automática de migraciones (opcional)
- [x] Configuración de Npgsql.EnableLegacyTimestampBehavior
- [x] VerificationCode entity agregada

---

## 📋 Pendiente - Módulos de Negocio

### 5. Módulo Auth
- [x] LoginCommand + Handler
- [x] RegisterCommand + Handler
- [x] GoogleSignInCommand + Handler
- [x] ChangePasswordCommand + Handler
- [x] SendCodeCommand + Handler
- [x] ResetPasswordCommand + Handler
- [x] NewPasswordCommand + Handler
- [x] GoogleLogInCommand + Handler
- [x] LogOutCommand + Handler
- [x] IsLoggedInQuery + Handler
- [x] RefreshFcmCommand + Handler
- [x] IVerificationCodeRepository + Implementación
- [x] AuthController migrado - ✅ COMPLETO (12/12 endpoints)

### 6. Módulo Payments
- [x] CreatePaymentIntentCommand + Handler
- [x] RetireBalanceCommand + Handler
- [x] Stripe Webhook Handler (en PaymentsController)
- [x] IPaymentDataRepository + Implementación
- [x] IWithdrawalDataRepository + Implementación
- [x] PaymentsController migrado

### 7. Módulo FinancialAssets
- [x] GetFinancialAssetsQuery + Handler
- [x] GetFinancialAssetsByGroupQuery + Handler
- [x] GetFinancialAssetsByCountryQuery + Handler
- [x] GetBetZonesQuery + Handler
- [x] FetchCandlesQuery + Handler
- [x] FinancialAssetsController migrado - ✅ COMPLETO (5/5 endpoints)

### 8. Módulo Info
- [x] GetUserInfoQuery + Handler
- [x] GetAppAds endpoint
- [x] GetFavoritesQuery + Handler
- [x] ToggleFavoriteCommand + Handler
- [x] GetTrendsQuery + Handler
- [x] GetTopUsersQuery + Handler
- [x] GetTopUsersByCountryQuery + Handler
- [x] GetPendingBalanceQuery + Handler
- [x] GetPaymentHistoryQuery + Handler
- [x] GetWithdrawalHistoryQuery + Handler
- [x] GetStoreOptionsQuery + Handler
- [x] GetRetireOptionsQuery + Handler
- [x] DeleteWithdrawalMethodCommand + Handler
- [x] AddBankWithdrawalMethodCommand + Handler
- [x] AddPaypalWithdrawalMethodCommand + Handler
- [x] AddCryptoWithdrawalMethodCommand + Handler
- [x] GetRaffleItemsQuery + Handler
- [x] CreateRaffleCommand + Handler
- [x] UploadProfilePicCommand + Handler
- [x] IFavoriteRepository + Implementación
- [x] IWithdrawalMethodRepository + Implementación
- [x] IRaffleRepository + IRaffleItemRepository + Implementaciones
- [x] InfoController migrado - ✅ COMPLETO (18/18 endpoints)

### 9. Módulo Rewards
- [x] RequestAdNonceCommand + Handler + Validator
- [x] VerifyAdRewardCommand + Handler + Validator
- [x] AdMobSsvVerifier (servicio de validación SSV)
- [x] IRewardNonceRepository + Implementación
- [x] IRewardTransactionRepository + Implementación
- [x] Entidades RewardNonce y RewardTransaction en Domain
- [x] RewardsController migrado

### 10. Módulo Didit (Verificación)
- [x] CreateDiditSessionCommand + Handler
- [x] ProcessDiditWebhookCommand + Handler
- [x] IDiditApiService + Implementación
- [x] ILocalizationService + Implementación
- [x] CountryCodeMapper (servicio de mapeo de países)
- [x] DiditController migrado

---

## 🔧 Pendiente - Servicios e Infraestructura

### 11. Servicios Externos
- [x] EmailService (SMTP)
- [x] FirebaseNotificationService
- [x] UpdaterService (Hosted Service) - ✅ COMPLETO: Todos los métodos implementados (CheckBetsAsync, UpdateTrendsAsync, RefreshTargetOddsAsync, UpdateCurrentMaxOddsAsync, UpdateAssetsAsync, CreateBetZonesAsync). Incluye análisis técnico completo (RSI, Bollinger Bands, soportes/resistencias, generación inteligente de zonas)
- [x] OddsAdjusterService (Hosted Service)

### 12. Configuración
- [x] appsettings.json completo
- [ ] Variables de entorno documentadas
- [x] Configuración de SMTP
- [x] Configuración de Firebase
- [x] Configuración de Stripe
- [x] Configuración de Didit

### 13. Health Checks
- [x] Health check endpoint
- [x] Database health check
- [ ] External services health checks (opcional)

---

## 📊 Pendiente - Mejoras y Optimizaciones

### 14. Performance
- [ ] Caché para queries frecuentes
- [ ] Optimización de queries (N+1)
- [ ] Paginación en queries grandes

### 15. Testing
- [ ] Tests unitarios para handlers
- [ ] Tests de integración
- [ ] Tests de repositorios

### 16. Documentación
- [ ] Swagger con ejemplos
- [ ] Documentación de API
- [ ] Guía de migración completa

---

## ✅ Resumen del Progreso

### Módulos Completados (100%)
- ✅ Estructura Base (Domain, Application, Infrastructure, API)
- ✅ Autenticación y Autorización (JWT Local + Google)
- ✅ Configuración de Seguridad (Rate Limiting, HSTS, CORS, HTTPS)
- ✅ Logging (Serilog con file logging)
- ✅ Base de Datos (EF Core, Migraciones, PostgreSQL)
- ✅ Módulo Auth (Login, Register, Google SignIn, ChangePassword, SendCode, ResetPassword, NewPassword, GoogleLogIn, LogOut, IsLoggedIn, RefreshFCM) - ✅ COMPLETO (12/12 endpoints)
- ✅ Módulo Payments (Stripe Integration, Webhooks, Withdrawals)
- ✅ Módulo FinancialAssets (Get, ByGroup, ByCountry, BetZones, FetchCandles) - ✅ COMPLETO (5/5 endpoints)
- ✅ Módulo Info (UserInfo, AppAds, Favorites, Trends, Rankings, Historiales, Métodos de Retiro, Rifas, UploadPic) - ✅ COMPLETO (18/18 endpoints)
- ✅ Módulo Rewards (AdMob SSV Verification)
- ✅ Módulo Didit (Verificación de identidad)
- ✅ UpdaterService (Análisis técnico completo, creación de zonas)
- ✅ OddsAdjusterService (Hosted Service)
- ✅ EmailService (SMTP)
- ✅ FirebaseNotificationService
- ✅ BetController - ✅ COMPLETO (13/13 métodos migrados: NewBet, UserBets, UserBet, HistoricUserBets, GetBetZone, GetBetZones, DeleteRecentBet, DeleteHistoricBet, PriceBets, HistoricPriceBets, NewPriceBet, DeleteRecentPriceBet)

### Pendientes Menores
- [x] PriceBet functionality - ✅ COMPLETO (entidades, repositorios, commands, queries, controller)
- [x] Variables de entorno documentadas - ✅ COMPLETO (ver ENV_VARIABLES.md)
- [x] Configuración de Stripe en Program.cs - ✅ COMPLETO
- [x] Copia de app-ads.txt al proyecto API - ✅ COMPLETO
- [x] Todos los endpoints migrados - ✅ COMPLETO (55/55 endpoints - 100%)
- [ ] External services health checks (opcional)
- [ ] Optimizaciones de performance
- [ ] Testing
- [ ] Documentación completa

---

## 🎯 Prioridad de Implementación

### Fase 1 (Crítico - Sin esto no funciona)
1. Autenticación JWT
2. Logging básico
3. Migraciones de BD

### Fase 2 (Funcionalidad Core)
4. Módulo Auth completo
5. Módulo Payments básico
6. Módulo FinancialAssets

### Fase 3 (Completar Funcionalidad)
7. Módulo Info
8. Módulo Rewards
9. Módulo Didit

### Fase 4 (Servicios y Optimización)
10. Hosted Services
11. Email y Notificaciones
12. Health Checks
13. Caché y optimizaciones

---

## 📝 Notas

- El proyecto legacy sigue funcionando en paralelo
- La migración es gradual, módulo por módulo
- Los endpoints pueden coexistir durante la transición
- Priorizar funcionalidad crítica primero
