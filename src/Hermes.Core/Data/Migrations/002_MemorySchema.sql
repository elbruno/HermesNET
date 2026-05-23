-- 002_MemorySchema.sql
-- Idempotent memory schema migration for Hermes.NET M2.
-- Adds Memory and UserProfiles tables for profile-scoped curated memory.
-- R2 risk: (ProfileId, Kind) unique index ensures one MEMORY.md and one USER.md per profile.

CREATE TABLE IF NOT EXISTS Memory (
    Id        TEXT    NOT NULL PRIMARY KEY,
    ProfileId TEXT    NOT NULL,
    Kind      TEXT    NOT NULL DEFAULT 'memory',
    Content   TEXT    NOT NULL DEFAULT '',
    Format    TEXT    NOT NULL DEFAULT 'markdown',
    Version   INTEGER NOT NULL DEFAULT 1,
    CreatedAt TEXT    NOT NULL,
    UpdatedAt TEXT    NOT NULL
);

-- Speeds up per-profile loads (the most common query pattern).
CREATE INDEX IF NOT EXISTS idx_memory_profile ON Memory(ProfileId);

-- Enforces exactly one MEMORY.md ('memory') and one USER.md ('user_profile') per profile.
CREATE UNIQUE INDEX IF NOT EXISTS idx_memory_profile_kind ON Memory(ProfileId, Kind);

CREATE TABLE IF NOT EXISTS UserProfiles (
    Id            TEXT    NOT NULL PRIMARY KEY,
    ProfileId     TEXT    NOT NULL UNIQUE,
    Data          TEXT    NOT NULL DEFAULT '',
    SchemaVersion INTEGER NOT NULL DEFAULT 1,
    CreatedAt     TEXT    NOT NULL,
    UpdatedAt     TEXT    NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_userprofiles_profile ON UserProfiles(ProfileId);
