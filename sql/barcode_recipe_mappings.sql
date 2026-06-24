CREATE TABLE IF NOT EXISTS barcode_recipe_mappings (
    object_id   TEXT PRIMARY KEY,
    recipe_name TEXT NOT NULL,
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE barcode_recipe_mappings IS 'Maps scanned barcode object IDs to RPA recipe names';
COMMENT ON COLUMN barcode_recipe_mappings.object_id IS 'Scanned barcode value (unique identifier)';
COMMENT ON COLUMN barcode_recipe_mappings.recipe_name IS 'Resolved recipe name used for script lookup';
COMMENT ON COLUMN barcode_recipe_mappings.updated_at IS 'Last update timestamp';