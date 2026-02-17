-- Tabla principal de items del clipboard
CREATE TABLE IF NOT EXISTS clipboard_items (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    content BLOB NOT NULL,
    content_type TEXT NOT NULL,
    ocr_text TEXT,
    embedding BLOB,
    source_app TEXT,
    timestamp INTEGER NOT NULL,
    is_password BOOLEAN NOT NULL DEFAULT 0,
    is_encrypted BOOLEAN NOT NULL DEFAULT 0,
    metadata TEXT,
    thumbnail BLOB,
    code_language TEXT
);

-- Índices para performance
CREATE INDEX IF NOT EXISTS idx_timestamp ON clipboard_items(timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_type ON clipboard_items(content_type);
CREATE INDEX IF NOT EXISTS idx_password ON clipboard_items(is_password);
CREATE INDEX IF NOT EXISTS idx_source_app ON clipboard_items(source_app);

-- FTS5 para búsqueda full-text
CREATE VIRTUAL TABLE IF NOT EXISTS clipboard_fts USING fts5(
    content,
    ocr_text,
    code_language,
    source_app,
    tokenize='porter unicode61'
);

-- NO usar content= para evitar triggers automáticos
-- Actualizaremos FTS manualmente desde el código

-- Tabla de configuración
CREATE TABLE IF NOT EXISTS config (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

-- Optimizaciones de SQLite
PRAGMA journal_mode = WAL;
PRAGMA synchronous = NORMAL;
PRAGMA cache_size = -64000;  -- 64MB cache
PRAGMA temp_store = MEMORY;
PRAGMA foreign_keys = ON;
