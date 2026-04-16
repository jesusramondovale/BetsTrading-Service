-- Ejecutar manualmente en PostgreSQL si no usas migraciones EF en tu entorno.
-- Esquema: BetsTrading | Tabla: Users

ALTER TABLE "BetsTrading"."Users"
    ADD COLUMN IF NOT EXISTS no_ads boolean NOT NULL DEFAULT false;
