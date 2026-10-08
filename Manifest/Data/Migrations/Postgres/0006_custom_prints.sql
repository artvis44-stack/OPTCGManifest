-- Prints someone added by hand because no card source lists them. Each also has a
-- catalog row, put back whenever the catalogue is reseeded; a deleted one is kept,
-- marked, so its id is never handed out again. Mirrors Database.Schema.

CREATE TABLE custom_prints (
    card_id    TEXT COLLATE "C" PRIMARY KEY,
    base_id    TEXT COLLATE "C" NOT NULL,
    name       TEXT NOT NULL,
    variant    TEXT NOT NULL,
    set_label  TEXT,
    rarity     TEXT,
    created_by BIGINT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at TIMESTAMPTZ
);
CREATE INDEX idx_custom_prints_base ON custom_prints (base_id);
