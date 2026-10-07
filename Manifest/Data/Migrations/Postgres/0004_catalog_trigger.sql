-- The Trigger box, the attribute icon and the block icon, which the first scrapers
-- did not read. Mirrors Database.Schema. The rows stay empty until the catalogue is
-- reseeded; Database.Initialise does that by itself for a catalogue that has none.

ALTER TABLE catalog ADD COLUMN attributes   TEXT;
ALTER TABLE catalog ADD COLUMN trigger_text TEXT;
ALTER TABLE catalog ADD COLUMN block_icon   INTEGER;
