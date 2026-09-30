BEGIN;

CREATE TABLE pdm_object (
    id                 BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    object_type        TEXT NOT NULL CHECK (object_type IN ('Assembly', 'Part', 'StandardPart')),
    designation        TEXT CHECK (designation IS NULL OR designation ~ '^[А-ЯЁ]{4}\.[0-9]{6}\.[0-9]{3}$'),
    name               TEXT NOT NULL,
    current_version_id BIGINT
);

CREATE UNIQUE INDEX uq_pdm_object_designation
    ON pdm_object (designation)
    WHERE designation IS NOT NULL;

CREATE UNIQUE INDEX uq_pdm_object_stdpart_name
    ON pdm_object (name)
    WHERE object_type = 'StandardPart';

CREATE TABLE object_version (
    id         BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    object_id  BIGINT NOT NULL REFERENCES pdm_object (id) ON DELETE CASCADE,
    version_no INT NOT NULL CHECK (version_no >= 1), state      TEXT NOT NULL CHECK (state IN ('InWork', 'Approved', 'Annulled')),
    material   TEXT,
    mass_kg    NUMERIC(12, 4) CHECK (mass_kg IS NULL OR mass_kg > 0),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(), 
    
    CONSTRAINT uq_object_version_no UNIQUE (object_id, version_no)
);

ALTER TABLE pdm_object
    ADD CONSTRAINT fk_pdm_object_current_version
        FOREIGN KEY (current_version_id) REFERENCES object_version (id);

CREATE TABLE bom_link (
    id                BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    parent_version_id BIGINT NOT NULL REFERENCES object_version (id) ON DELETE CASCADE,
    child_object_id   BIGINT NOT NULL REFERENCES pdm_object (id) ON DELETE RESTRICT,
    quantity          INT NOT NULL CHECK (quantity > 0),
    
    CONSTRAINT uq_bom_link_parent_child UNIQUE (parent_version_id, child_object_id)
);

CREATE TABLE import_log (
    id         BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    started_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    file_name  TEXT NOT NULL,
    severity   TEXT NOT NULL CHECK (severity IN ('Info', 'Warning', 'Error')),
    reason     TEXT
);

CREATE INDEX ix_object_version_object ON object_version (object_id);
CREATE INDEX ix_bom_link_parent       ON bom_link (parent_version_id);
CREATE INDEX ix_bom_link_child        ON bom_link (child_object_id);

COMMIT;