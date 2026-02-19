#pragma once

#include <sqlite3.h>
#include <string>
#include <vector>
#include <optional>
#include <memory>

enum class ClipboardType {
    Text,
    Image,
    Code,
    URL
};

struct ClipboardItem {
    int64_t id = 0;
    ClipboardType type = ClipboardType::Text;
    std::vector<uint8_t> content;
    std::string mime_type;
    std::string source_app;
    int64_t timestamp = 0;
    std::string ocr_text;
    std::string code_language;
    std::vector<float> embedding;
    bool is_password = false;
    bool is_encrypted = false;
    std::string metadata;
    std::vector<uint8_t> thumbnail;
    
    bool is_image() const { return type == ClipboardType::Image; }
    bool is_code() const { return type == ClipboardType::Code; }
    bool is_url() const { return type == ClipboardType::URL; }
    std::string get_text() const;
    std::string text_content;
    std::string content_type;
};

class ClipboardDB {
public:
    explicit ClipboardDB(const std::string& db_path);
    ~ClipboardDB();
    
    bool initialize();
    
    // CRUD operations
    int64_t insert(const ClipboardItem& item);
    std::optional<ClipboardItem> get(int64_t id);
    std::vector<ClipboardItem> get_recent(int limit = 20);
    bool update(const ClipboardItem& item);
    bool delete_item(int64_t id);
    bool delete_all();
    
    // Search
    std::vector<ClipboardItem> search_exact(const std::string& query, int limit = 20);
    std::vector<ClipboardItem> search_fts(const std::string& query, int limit = 20);
    std::vector<ClipboardItem> search_by_embedding(const std::vector<float>& embedding, int limit = 20);
    
    // Duplicate detection
    bool content_exists(const std::vector<uint8_t>& content);
    
private:
    std::string db_path_;
    sqlite3* db_ = nullptr;
    
    bool create_tables();
    bool create_indexes();
};
