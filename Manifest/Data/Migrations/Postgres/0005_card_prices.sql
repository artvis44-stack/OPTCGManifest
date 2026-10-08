-- Each printing's price from each source - Cardmarket and TCGplayer through
-- Limitless and tcgcsv.com, and optcgapi.com - in its own currency and in GBP.
-- prices keeps the one a card is shown at. Mirrors Database.Schema.

CREATE TABLE card_prices (
    card_id    TEXT COLLATE "C" NOT NULL,
    source     TEXT NOT NULL,
    currency   TEXT NOT NULL,
    amount     DOUBLE PRECISION NOT NULL,
    gbp        DOUBLE PRECISION NOT NULL,
    url        TEXT,
    fetched_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (card_id, source)
);
