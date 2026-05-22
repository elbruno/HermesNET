-- 001_InitialSchema.sql
-- Idempotent schema migration for Hermes session store.

CREATE TABLE IF NOT EXISTS Sessions (
    Id          TEXT    PRIMARY KEY,
    ProfileId   TEXT    NOT NULL,
    CreatedAt   TEXT    NOT NULL,
    UpdatedAt   TEXT    NOT NULL,
    LastMessage TEXT,
    MessageCount INTEGER DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_sessions_profile ON Sessions(ProfileId);
CREATE INDEX IF NOT EXISTS idx_sessions_created ON Sessions(CreatedAt);
