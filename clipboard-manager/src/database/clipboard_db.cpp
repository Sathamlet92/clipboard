#include "clipboard_db.h"
#include <iostream>
#include <cstring>
#include <sstream>
#include <set>
#include <cmath>
#include <algorithm>

ClipboardDB::ClipboardDB(const std::string& db_path) : db_path_(db_path) {}

ClipboardDB::~ClipboardDB() {
    if (db_) {
        sqlite3_close(db_);
    }
}

// Forward declaration for schema migration helper
static bool migrate_schema(sqlite3* db);

bool ClipboardDB::initialize() {
    int rc = sqlite3_open(db_path_.c_str(), &db_);
    if (rc != SQLITE_OK) {
        std::cerr << "Failed to open database: " << sqlite3_errmsg(db_) << std::endl;
        return false;
    }
    // Apply PRAGMAs to match .NET settings
    const char* pragmas = R"(
        PRAGMA journal_mode = WAL;
        PRAGMA synchronous = NORMAL;
        PRAGMA cache_size = -64000;
        PRAGMA temp_store = MEMORY;
        PRAGMA foreign_keys = ON;
    )";
    char* err_msg = nullptr;
    int prc = sqlite3_exec(db_, pragmas, nullptr, nullptr, &err_msg);
    if (prc != SQLITE_OK) {
        std::cerr << "Failed to apply PRAGMAs: " << err_msg << std::endl;
        sqlite3_free(err_msg);
        // continue, but warn
    }

    if (!create_tables()) return false;

    // Migrate existing schema (add missing columns) before creating indexes
    if (!migrate_schema(db_)) {
        std::cerr << "Failed to migrate database schema" << std::endl;
        return false;
    }

    if (!create_indexes()) return false;

    return true;
}

bool ClipboardDB::create_tables() {
    // Schema MUST match .NET Schema.sql exactly - NO triggers (FTS updated manually in code)
    const char* sql = R"(
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

        CREATE VIRTUAL TABLE IF NOT EXISTS clipboard_fts USING fts5(
            content, ocr_text, code_language, source_app, tokenize='porter unicode61'
        );

        CREATE TABLE IF NOT EXISTS config (
            key TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );
    )";
    
    char* err_msg = nullptr;
    int rc = sqlite3_exec(db_, sql, nullptr, nullptr, &err_msg);
    if (rc != SQLITE_OK) {
        std::cerr << "SQL error: " << err_msg << std::endl;
        sqlite3_free(err_msg);
        return false;
    }
    
    return true;
}


// Ensure the clipboard_items table has the columns expected by the .NET schema.
static bool migrate_schema(sqlite3* db) {
    // Read existing columns
    const char* pragma = "PRAGMA table_info(clipboard_items);";
    sqlite3_stmt* stmt;
    std::set<std::string> cols;
    if (sqlite3_prepare_v2(db, pragma, -1, &stmt, nullptr) == SQLITE_OK) {
        while (sqlite3_step(stmt) == SQLITE_ROW) {
            const unsigned char* name = sqlite3_column_text(stmt, 1);
            if (name) cols.insert(std::string(reinterpret_cast<const char*>(name)));
        }
        sqlite3_finalize(stmt);
    }

    auto exec = [&](const std::string& sql)->bool{
        char* err = nullptr;
        int rc = sqlite3_exec(db, sql.c_str(), nullptr, nullptr, &err);
        if (rc != SQLITE_OK) {
            std::cerr << "Schema migration SQL error: " << (err ? err : "unknown") << "\nSQL: " << sql << std::endl;
            if (err) sqlite3_free(err);
            return false;
        }
        return true;
    };

    // Add columns if missing
    if (!cols.count("content_type")) {
        if (!exec("ALTER TABLE clipboard_items ADD COLUMN content_type TEXT")) return false;
        // If older schema has 'mime_type', copy values
        if (cols.count("mime_type")) {
            exec("UPDATE clipboard_items SET content_type = mime_type WHERE content_type IS NULL OR content_type = ''");
        }
    }

    if (!cols.count("is_password")) {
        if (!exec("ALTER TABLE clipboard_items ADD COLUMN is_password INTEGER DEFAULT 0")) return false;
    }

    if (!cols.count("is_encrypted")) {
        if (!exec("ALTER TABLE clipboard_items ADD COLUMN is_encrypted INTEGER DEFAULT 0")) return false;
    }

    if (!cols.count("metadata")) {
        if (!exec("ALTER TABLE clipboard_items ADD COLUMN metadata TEXT")) return false;
    }

    if (!cols.count("thumbnail")) {
        if (!exec("ALTER TABLE clipboard_items ADD COLUMN thumbnail BLOB")) return false;
    }

    if (!cols.count("code_language")) {
        if (!exec("ALTER TABLE clipboard_items ADD COLUMN code_language TEXT")) return false;
    }

    return true;
}

bool ClipboardDB::create_indexes() {
    const char* sql = R"(
        CREATE INDEX IF NOT EXISTS idx_timestamp ON clipboard_items(timestamp DESC);
        CREATE INDEX IF NOT EXISTS idx_content_type ON clipboard_items(content_type);
        CREATE INDEX IF NOT EXISTS idx_password ON clipboard_items(is_password);
        CREATE INDEX IF NOT EXISTS idx_source_app ON clipboard_items(source_app);
    )";
    
    char* err_msg = nullptr;
    int rc = sqlite3_exec(db_, sql, nullptr, nullptr, &err_msg);
    if (rc != SQLITE_OK) {
        std::cerr << "SQL error: " << err_msg << std::endl;
        sqlite3_free(err_msg);
        return false;
    }
    
    return true;
}


// Helper to update FTS manually (like .NET does)
static bool update_fts(sqlite3* db, int64_t id, const ClipboardItem& item) {
    // Use INSERT OR REPLACE for compatibility with .NET which may have already inserted
    const char* sql = R"(
        INSERT OR REPLACE INTO clipboard_fts(rowid, content, ocr_text, code_language, source_app)
        VALUES (?, ?, ?, ?, ?)
    )";
    sqlite3_stmt* stmt;
    if (sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr) != SQLITE_OK) {
        std::cerr << "❌ FTS update prepare failed: " << sqlite3_errmsg(db) << std::endl;
        return false;
    }
    sqlite3_bind_int64(stmt, 1, id);
    // For images, use empty string; for text, decode content
    std::string content_str = "";
    if (item.type != ClipboardType::Image && !item.content.empty()) {
        content_str = std::string(item.content.begin(), item.content.end());
    }
    sqlite3_bind_text(stmt, 2, content_str.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 3, item.ocr_text.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 4, item.code_language.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 5, item.source_app.c_str(), -1, SQLITE_TRANSIENT);
    int rc = sqlite3_step(stmt);
    sqlite3_finalize(stmt);
    if (rc != SQLITE_DONE) {
        std::cerr << "❌ FTS update failed: " << sqlite3_errmsg(db) << std::endl;
        return false;
    }
    return true;
}

int64_t ClipboardDB::insert(const ClipboardItem& item) {
    // Schema matches .NET exactly - no 'type' column
    const char* sql = R"(
        INSERT INTO clipboard_items (content, content_type, ocr_text, embedding, source_app, timestamp, is_password, is_encrypted, metadata, thumbnail, code_language)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    )";
    
    sqlite3_stmt* stmt;
    if (sqlite3_prepare_v2(db_, sql, -1, &stmt, nullptr) != SQLITE_OK) {
        std::cerr << "❌ DB Insert prepare failed: " << sqlite3_errmsg(db_) << std::endl;
        return -1;
    }
    
    sqlite3_bind_blob(stmt, 1, item.content.data(), item.content.size(), SQLITE_TRANSIENT);
    
    // Convertir tipo enum a string como .NET: "Text", "Code", "Image", "Url"
    std::string type_str;
    switch (item.type) {
        case ClipboardType::Text: type_str = "Text"; break;
        case ClipboardType::Code: type_str = "Code"; break;
        case ClipboardType::Image: type_str = "Image"; break;
        case ClipboardType::URL: type_str = "Url"; break;
        default: type_str = "Text"; break;
    }
    sqlite3_bind_text(stmt, 2, type_str.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 3, item.ocr_text.c_str(), -1, SQLITE_TRANSIENT);

    if (!item.embedding.empty()) {
        sqlite3_bind_blob(stmt, 4, item.embedding.data(), item.embedding.size() * sizeof(float), SQLITE_TRANSIENT);
    } else {
        sqlite3_bind_null(stmt, 4);
    }

    sqlite3_bind_text(stmt, 5, item.source_app.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_int64(stmt, 6, item.timestamp);
    sqlite3_bind_int(stmt, 7, item.is_password ? 1 : 0);
    sqlite3_bind_int(stmt, 8, item.is_encrypted ? 1 : 0);

    if (!item.metadata.empty()) {
        sqlite3_bind_text(stmt, 9, item.metadata.c_str(), -1, SQLITE_TRANSIENT);
    } else {
        sqlite3_bind_null(stmt, 9);
    }

    if (!item.thumbnail.empty()) {
        sqlite3_bind_blob(stmt, 10, item.thumbnail.data(), item.thumbnail.size(), SQLITE_TRANSIENT);
    } else {
        sqlite3_bind_null(stmt, 10);
    }

    sqlite3_bind_text(stmt, 11, item.code_language.c_str(), -1, SQLITE_TRANSIENT);
    
    int rc = sqlite3_step(stmt);
    
    if (rc != SQLITE_DONE) {
        std::cerr << "❌ DB Insert failed: " << sqlite3_errmsg(db_) << " (code: " << rc << ")" << std::endl;
        sqlite3_finalize(stmt);
        return -1;
    }
    
    int64_t id = sqlite3_last_insert_rowid(db_);
    sqlite3_finalize(stmt);
    
    // Update FTS manually (like .NET does)
    update_fts(db_, id, item);
    
    return id;
}

std::optional<ClipboardItem> ClipboardDB::get(int64_t id) {
    const char* sql = R"(
        SELECT id, content, content_type, ocr_text, embedding, source_app, timestamp, is_password, is_encrypted, metadata, thumbnail, code_language
        FROM clipboard_items WHERE id = ?
    )";

    sqlite3_stmt* stmt;
    if (sqlite3_prepare_v2(db_, sql, -1, &stmt, nullptr) != SQLITE_OK) {
        return std::nullopt;
    }

    sqlite3_bind_int64(stmt, 1, id);

    if (sqlite3_step(stmt) != SQLITE_ROW) {
        sqlite3_finalize(stmt);
        return std::nullopt;
    }

    ClipboardItem item;
    item.id = sqlite3_column_int64(stmt, 0);
    
    // Get content and content_type
    const void* blob = sqlite3_column_blob(stmt, 1);
    int blob_size = sqlite3_column_bytes(stmt, 1);
    if (blob && blob_size > 0) item.content.assign(static_cast<const uint8_t*>(blob), static_cast<const uint8_t*>(blob) + blob_size);

    const char* ctype = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 2));
    if (ctype) {
        item.content_type = ctype;
        // Parsear tipo como .NET: "Text", "Code", "Image", "Url"
        std::string ct = ctype;
        if (ct == "Code") {
            item.type = ClipboardType::Code;
        } else if (ct == "Image") {
            item.type = ClipboardType::Image;
        } else if (ct == "Url") {
            item.type = ClipboardType::URL;
        } else {
            item.type = ClipboardType::Text;
        }
    }

    if (sqlite3_column_type(stmt, 3) != SQLITE_NULL) {
        const char* ocr = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 3));
        if (ocr) item.ocr_text = ocr;
    }

    // embedding at index 4 - skip for now

    const char* app = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 5));
    if (app) item.source_app = app;

    item.timestamp = sqlite3_column_int64(stmt, 6);

    item.is_password = sqlite3_column_int(stmt, 7) == 1;
    item.is_encrypted = sqlite3_column_int(stmt, 8) == 1;

    if (sqlite3_column_type(stmt, 9) != SQLITE_NULL) {
        const char* meta = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 9));
        if (meta) item.metadata = meta;
    }

    if (sqlite3_column_type(stmt, 10) != SQLITE_NULL) {
        const void* thumb = sqlite3_column_blob(stmt, 10);
        int tsize = sqlite3_column_bytes(stmt, 10);
        if (thumb && tsize > 0) item.thumbnail.assign(static_cast<const uint8_t*>(thumb), static_cast<const uint8_t*>(thumb) + tsize);
    }

    if (sqlite3_column_type(stmt, 11) != SQLITE_NULL) {
        const char* lang = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 11));
        if (lang) item.code_language = lang;
    }

    // Si tiene code_language, debe marcarse como Code (comportamiento original)
    if (!item.code_language.empty()) {
        item.type = ClipboardType::Code;
    }

    sqlite3_finalize(stmt);
    return item;
}

// Helper function to convert ClipboardType to string
// Unused for now, but kept for future use
[[maybe_unused]] static std::string type_to_string(ClipboardType type) {
    switch (type) {
        case ClipboardType::Text: return "text";
        case ClipboardType::Image: return "image";
        case ClipboardType::Code: return "code";
        case ClipboardType::URL: return "url";
        default: return "text";
    }
}

std::vector<ClipboardItem> ClipboardDB::get_recent(int limit) {
    std::cout << "🔧 DB: Getting recent items..." << std::endl;
    std::vector<ClipboardItem> items;
    
    const char* sql = R"(
        SELECT id, content, content_type, ocr_text, embedding, source_app, timestamp, is_password, is_encrypted, metadata, thumbnail, code_language
        FROM clipboard_items ORDER BY timestamp DESC LIMIT ?
    )";
    
    sqlite3_stmt* stmt;
    if (sqlite3_prepare_v2(db_, sql, -1, &stmt, nullptr) != SQLITE_OK) {
        std::cerr << "❌ DB: Failed to prepare: " << sqlite3_errmsg(db_) << std::endl;
        return items;
    }
    
    sqlite3_bind_int(stmt, 1, limit);
    
    std::cout << "🔧 DB: Executing query..." << std::endl;
    while (sqlite3_step(stmt) == SQLITE_ROW) {
        std::cout << "🔧 DB: Reading row..." << std::endl;
        ClipboardItem item;
        item.id = sqlite3_column_int64(stmt, 0);

        const void* blob = sqlite3_column_blob(stmt, 1);
        int blob_size = sqlite3_column_bytes(stmt, 1);
        if (blob && blob_size > 0) item.content.assign(static_cast<const uint8_t*>(blob), static_cast<const uint8_t*>(blob) + blob_size);

        const char* ctype = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 2));
        if (ctype) {
            item.content_type = ctype;
            // Parsear tipo como .NET: "Text", "Code", "Image", "Url"
            std::string ct = ctype;
            if (ct == "Code") {
                item.type = ClipboardType::Code;
            } else if (ct == "Image") {
                item.type = ClipboardType::Image;
            } else if (ct == "Url") {
                item.type = ClipboardType::URL;
            } else {
                item.type = ClipboardType::Text;
            }
        }

        if (sqlite3_column_type(stmt, 3) != SQLITE_NULL) {
            const char* ocr = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 3));
            if (ocr) item.ocr_text = ocr;
        }

        const char* app = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 5));
        if (app) item.source_app = app;

        item.timestamp = sqlite3_column_int64(stmt, 6);

        if (sqlite3_column_type(stmt, 11) != SQLITE_NULL) {
            const char* lang = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 11));
            if (lang) item.code_language = lang;
        }
        
        // Si tiene code_language, debe marcarse como Code (comportamiento original)
        if (!item.code_language.empty()) {
            item.type = ClipboardType::Code;
        }
        
        items.push_back(std::move(item));
        std::cout << "✅ DB: Row added" << std::endl;
    }
    
    std::cout << "✅ DB: Loaded " << items.size() << " items" << std::endl;
    sqlite3_finalize(stmt);
    return items;
}

bool ClipboardDB::update(const ClipboardItem& item) {
    const char* sql = R"(
        UPDATE clipboard_items 
        SET content = ?, content_type = ?, ocr_text = ?, embedding = ?, source_app = ?, timestamp = ?, is_password = ?, is_encrypted = ?, metadata = ?, thumbnail = ?, code_language = ?
        WHERE id = ?
    )";
    
    sqlite3_stmt* stmt;
    if (sqlite3_prepare_v2(db_, sql, -1, &stmt, nullptr) != SQLITE_OK) {
        std::cerr << "❌ DB Update prepare failed: " << sqlite3_errmsg(db_) << std::endl;
        return false;
    }
    
    sqlite3_bind_blob(stmt, 1, item.content.data(), item.content.size(), SQLITE_TRANSIENT);
    
    // Convertir tipo enum a string como .NET
    std::string type_str;
    switch (item.type) {
        case ClipboardType::Text: type_str = "Text"; break;
        case ClipboardType::Code: type_str = "Code"; break;
        case ClipboardType::Image: type_str = "Image"; break;
        case ClipboardType::URL: type_str = "Url"; break;
        default: type_str = "Text"; break;
    }
    sqlite3_bind_text(stmt, 2, type_str.c_str(), -1, SQLITE_TRANSIENT);

    sqlite3_bind_text(stmt, 3, item.ocr_text.c_str(), -1, SQLITE_TRANSIENT);

    if (!item.embedding.empty()) {
        sqlite3_bind_blob(stmt, 4, item.embedding.data(), item.embedding.size() * sizeof(float), SQLITE_TRANSIENT);
    } else {
        sqlite3_bind_null(stmt, 4);
    }

    sqlite3_bind_text(stmt, 5, item.source_app.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_int64(stmt, 6, item.timestamp);

    sqlite3_bind_int(stmt, 7, item.is_password ? 1 : 0);
    sqlite3_bind_int(stmt, 8, item.is_encrypted ? 1 : 0);

    if (!item.metadata.empty()) {
        sqlite3_bind_text(stmt, 9, item.metadata.c_str(), -1, SQLITE_TRANSIENT);
    } else {
        sqlite3_bind_null(stmt, 9);
    }

    if (!item.thumbnail.empty()) {
        sqlite3_bind_blob(stmt, 10, item.thumbnail.data(), item.thumbnail.size(), SQLITE_TRANSIENT);
    } else {
        sqlite3_bind_null(stmt, 10);
    }

    sqlite3_bind_text(stmt, 11, item.code_language.c_str(), -1, SQLITE_TRANSIENT);

    sqlite3_bind_int64(stmt, 12, item.id);
    
    int rc = sqlite3_step(stmt);
    sqlite3_finalize(stmt);
    
    if (rc != SQLITE_DONE) {
        std::cerr << "❌ DB Update failed: " << sqlite3_errmsg(db_) << std::endl;
        return false;
    }

    // Keep FTS synchronized with updated OCR/language/text fields.
    update_fts(db_, item.id, item);
    
    return true;
}

bool ClipboardDB::delete_item(int64_t id) {
    const char* sql = "DELETE FROM clipboard_items WHERE id = ?";
    
    sqlite3_stmt* stmt;
    if (sqlite3_prepare_v2(db_, sql, -1, &stmt, nullptr) != SQLITE_OK) {
        std::cerr << "❌ Error preparando DELETE: " << sqlite3_errmsg(db_) << std::endl;
        return false;
    }
    
    sqlite3_bind_int64(stmt, 1, id);
    int rc = sqlite3_step(stmt);
    
    if (rc != SQLITE_DONE) {
        std::cerr << "❌ Error ejecutando DELETE: " << sqlite3_errmsg(db_) << std::endl;
        sqlite3_finalize(stmt);
        return false;
    }
    
    int changes = sqlite3_changes(db_);
    std::cout << "🔧 BD: " << changes << " filas borradas" << std::endl;
    
    sqlite3_finalize(stmt);
    return changes > 0;
}

bool ClipboardDB::delete_all() {
    const char* sql = "DELETE FROM clipboard_items";
    char* err_msg = nullptr;
    int rc = sqlite3_exec(db_, sql, nullptr, nullptr, &err_msg);
    
    if (rc != SQLITE_OK) {
        sqlite3_free(err_msg);
        return false;
    }
    
    return true;
}

std::vector<ClipboardItem> ClipboardDB::search_exact(const std::string& query, int limit) {
    std::vector<ClipboardItem> items;

    if (query.empty()) {
        return items;
    }

    const char* sql = R"(
        SELECT id, content, content_type, ocr_text, embedding, source_app, timestamp, is_password, is_encrypted, metadata, thumbnail, code_language
        FROM clipboard_items
        WHERE (
            (content_type != 'Image' AND CAST(content AS TEXT) LIKE '%' || ? || '%' COLLATE NOCASE)
            OR (ocr_text LIKE '%' || ? || '%' COLLATE NOCASE)
            OR (code_language LIKE '%' || ? || '%' COLLATE NOCASE)
            OR (source_app LIKE '%' || ? || '%' COLLATE NOCASE)
            OR (content_type LIKE '%' || ? || '%' COLLATE NOCASE)
        )
        ORDER BY timestamp DESC
        LIMIT ?
    )";

    sqlite3_stmt* stmt;
    if (sqlite3_prepare_v2(db_, sql, -1, &stmt, nullptr) != SQLITE_OK) {
        return items;
    }

    sqlite3_bind_text(stmt, 1, query.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 2, query.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 3, query.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 4, query.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 5, query.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_int(stmt, 6, limit);

    while (sqlite3_step(stmt) == SQLITE_ROW) {
        ClipboardItem item;
        item.id = sqlite3_column_int64(stmt, 0);

        const void* blob = sqlite3_column_blob(stmt, 1);
        int blob_size = sqlite3_column_bytes(stmt, 1);
        if (blob && blob_size > 0) item.content.assign(static_cast<const uint8_t*>(blob), static_cast<const uint8_t*>(blob) + blob_size);

        const char* ctype = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 2));
        if (ctype) {
            item.content_type = ctype;
            std::string ct = ctype;
            if (ct == "Code") {
                item.type = ClipboardType::Code;
            } else if (ct == "Image") {
                item.type = ClipboardType::Image;
            } else if (ct == "Url") {
                item.type = ClipboardType::URL;
            } else {
                item.type = ClipboardType::Text;
            }
        }

        if (sqlite3_column_type(stmt, 3) != SQLITE_NULL) {
            const char* ocr = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 3));
            if (ocr) item.ocr_text = ocr;
        }

        const char* app = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 5));
        if (app) item.source_app = app;

        item.timestamp = sqlite3_column_int64(stmt, 6);

        if (sqlite3_column_type(stmt, 11) != SQLITE_NULL) {
            const char* lang = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 11));
            if (lang) item.code_language = lang;
        }

        if (!item.code_language.empty()) {
            item.type = ClipboardType::Code;
        }

        items.push_back(std::move(item));
    }

    sqlite3_finalize(stmt);
    return items;
}

std::vector<ClipboardItem> ClipboardDB::search_fts(const std::string& query, int limit) {
    std::vector<ClipboardItem> items;
    
    const char* sql = R"(
        SELECT c.id, c.content, c.content_type, c.ocr_text, c.embedding, c.source_app, c.timestamp, c.is_password, c.is_encrypted, c.metadata, c.thumbnail, c.code_language
        FROM clipboard_items c
        INNER JOIN clipboard_fts f ON c.id = f.rowid
        WHERE f MATCH ?
        LIMIT ?
    )";
    
    sqlite3_stmt* stmt;
    if (sqlite3_prepare_v2(db_, sql, -1, &stmt, nullptr) != SQLITE_OK) {
        return items;
    }
    
    sqlite3_bind_text(stmt, 1, query.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_int(stmt, 2, limit);
    
    while (sqlite3_step(stmt) == SQLITE_ROW) {
        ClipboardItem item;
        item.id = sqlite3_column_int64(stmt, 0);
        
        const void* blob = sqlite3_column_blob(stmt, 1);
        int blob_size = sqlite3_column_bytes(stmt, 1);
        if (blob && blob_size > 0) item.content.assign(static_cast<const uint8_t*>(blob), static_cast<const uint8_t*>(blob) + blob_size);

        const char* ctype = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 2));
        if (ctype) {
            item.content_type = ctype;
            std::string ct = ctype;
            if (ct == "Code") {
                item.type = ClipboardType::Code;
            } else if (ct == "Image") {
                item.type = ClipboardType::Image;
            } else if (ct == "Url") {
                item.type = ClipboardType::URL;
            } else {
                item.type = ClipboardType::Text;
            }
        }

        if (sqlite3_column_type(stmt, 3) != SQLITE_NULL) {
            const char* ocr = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 3));
            if (ocr) item.ocr_text = ocr;
        }

        const char* app = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 5));
        if (app) item.source_app = app;

        item.timestamp = sqlite3_column_int64(stmt, 6);
        
        if (sqlite3_column_type(stmt, 11) != SQLITE_NULL) {
            const char* lang = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 11));
            if (lang) item.code_language = lang;
        }
        
        // Si tiene code_language, debe marcarse como Code (comportamiento original)
        if (!item.code_language.empty()) {
            item.type = ClipboardType::Code;
        }

        items.push_back(std::move(item));
    }
    
    sqlite3_finalize(stmt);
    return items;
}

std::string ClipboardItem::get_text() const {
    if (!text_content.empty()) return text_content;
    if (!ocr_text.empty()) return ocr_text;
    return "";
}

std::vector<ClipboardItem> ClipboardDB::search_by_embedding(const std::vector<float>& query_embedding, int limit) {
    std::vector<ClipboardItem> results;
    if (db_ == nullptr) return results;

    const char* sql = R"(
        SELECT id, embedding, content, content_type, ocr_text, source_app, timestamp, code_language
        FROM clipboard_items
        WHERE embedding IS NOT NULL
        ORDER BY timestamp DESC
        LIMIT 100
    )";

    sqlite3_stmt* stmt;
    if (sqlite3_prepare_v2(db_, sql, -1, &stmt, nullptr) != SQLITE_OK) {
        return results;
    }

    struct ScoredItem { double score; ClipboardItem item; };
    std::vector<ScoredItem> scored;

    while (sqlite3_step(stmt) == SQLITE_ROW) {
        ClipboardItem item;
        item.id = sqlite3_column_int64(stmt, 0);

        const void* emb_blob = sqlite3_column_blob(stmt, 1);
        int emb_bytes = sqlite3_column_bytes(stmt, 1);
        if (!emb_blob || emb_bytes <= 0) continue;

        int num_floats = emb_bytes / sizeof(float);
        const float* emb_data = static_cast<const float*>(emb_blob);
        std::vector<float> row_emb(num_floats);
        for (int i = 0; i < num_floats; ++i) row_emb[i] = emb_data[i];

        // Fill basic fields
        const void* content_blob = sqlite3_column_blob(stmt, 2);
        int content_size = sqlite3_column_bytes(stmt, 2);
        if (content_blob && content_size > 0)
            item.content.assign(static_cast<const uint8_t*>(content_blob), static_cast<const uint8_t*>(content_blob) + content_size);

        const char* ctype = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 3));
        if (ctype) {
            item.content_type = ctype;
            std::string ct = ctype;
            if (ct == "Code") {
                item.type = ClipboardType::Code;
            } else if (ct == "Image") {
                item.type = ClipboardType::Image;
            } else if (ct == "Url") {
                item.type = ClipboardType::URL;
            } else {
                item.type = ClipboardType::Text;
            }
        }

        if (sqlite3_column_type(stmt, 4) != SQLITE_NULL) {
            const char* ocr = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 4));
            if (ocr) item.ocr_text = ocr;
        }

        const char* app = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 5));
        if (app) item.source_app = app;

        item.timestamp = sqlite3_column_int64(stmt, 6);

        if (sqlite3_column_type(stmt, 7) != SQLITE_NULL) {
            const char* lang = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 7));
            if (lang) item.code_language = lang;
        }

        if (!item.code_language.empty()) {
            item.type = ClipboardType::Code;
        }

        // store embedding temporarily in item.embedding
        item.embedding = std::move(row_emb);

        scored.push_back({0.0, std::move(item)});
    }

    sqlite3_finalize(stmt);

    if (scored.empty() || query_embedding.empty()) return results;

    auto dot = [](const std::vector<float>& a, const std::vector<float>& b)->double{
        double s = 0.0;
        size_t n = std::min(a.size(), b.size());
        for (size_t i = 0; i < n; ++i) s += static_cast<double>(a[i]) * static_cast<double>(b[i]);
        return s;
    };

    auto norm = [](const std::vector<float>& a)->double{
        double s = 0.0;
        for (float v : a) s += static_cast<double>(v) * static_cast<double>(v);
        return std::sqrt(s);
    };

    double qnorm = norm(query_embedding);
    if (qnorm == 0.0) return results;

    for (auto &si : scored) {
        if (si.item.embedding.empty()) continue;
        if (si.item.embedding.size() != query_embedding.size()) continue;
        double dotp = dot(si.item.embedding, query_embedding);
        double denom = norm(si.item.embedding) * qnorm;
        if (denom == 0.0) si.score = 0.0;
        else si.score = dotp / denom;
    }

    // sort by score desc
    std::sort(scored.begin(), scored.end(), [](const ScoredItem& a, const ScoredItem& b){ return a.score > b.score; });

    int taken = 0;
    for (const auto &si : scored) {
        if (taken >= limit) break;
        results.push_back(si.item);
        ++taken;
    }

    return results;
}

bool ClipboardDB::content_exists(const std::vector<uint8_t>& content) {
    if (content.empty()) return false;
    
    // First check: exact match in content field
    const char* sql_content = "SELECT COUNT(*) FROM clipboard_items WHERE content = ?";
    sqlite3_stmt* stmt;
    
    if (sqlite3_prepare_v2(db_, sql_content, -1, &stmt, nullptr) != SQLITE_OK) {
        std::cerr << "❌ DB: Failed to prepare content_exists query: " << sqlite3_errmsg(db_) << std::endl;
        return false;
    }
    
    sqlite3_bind_blob(stmt, 1, content.data(), content.size(), SQLITE_STATIC);
    
    bool exists = false;
    if (sqlite3_step(stmt) == SQLITE_ROW) {
        int count = sqlite3_column_int(stmt, 0);
        exists = (count > 0);
    }
    sqlite3_finalize(stmt);
    
    if (exists) {
        std::cout << "🔍 Duplicate found: exact match in content" << std::endl;
        return true;
    }
    
    // Second check: if content is text, also check against ocr_text field in images
    // This prevents copying OCR text as a separate item when it's already part of an image
    std::string text_content(content.begin(), content.end());
    
    // Trim whitespace for comparison
    auto trim = [](const std::string& s) {
        auto start = s.find_first_not_of(" \t\n\r");
        auto end = s.find_last_not_of(" \t\n\r");
        if (start == std::string::npos) return std::string();
        return s.substr(start, end - start + 1);
    };
    
    std::string trimmed = trim(text_content);
    
    const char* sql_ocr = "SELECT ocr_text FROM clipboard_items WHERE content_type = 'Image' AND ocr_text IS NOT NULL AND ocr_text != ''";
    if (sqlite3_prepare_v2(db_, sql_ocr, -1, &stmt, nullptr) != SQLITE_OK) {
        std::cerr << "❌ DB: Failed to prepare OCR check query: " << sqlite3_errmsg(db_) << std::endl;
        return false;
    }
    
    while (sqlite3_step(stmt) == SQLITE_ROW) {
        const char* ocr = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 0));
        if (ocr) {
            std::string ocr_text(ocr);
            std::string trimmed_ocr = trim(ocr_text);
            if (trimmed == trimmed_ocr) {
                std::cout << "🔍 Duplicate found: matches OCR text of an existing image" << std::endl;
                exists = true;
                break;
            }
        }
    }
    sqlite3_finalize(stmt);
    
    return exists;
}
