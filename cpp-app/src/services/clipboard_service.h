#pragma once

#include "../database/clipboard_db.h"
#include <functional>
#include <memory>
#include <mutex>
#include <string>

class EmbeddingService;
class LanguageDetector;
class OCRService;

struct ClipboardEvent {
    std::string content_type;
    std::string text_content;
    std::vector<uint8_t> image_data;
    int64_t timestamp;
};

class ClipboardService {
public:
    ClipboardService(std::shared_ptr<ClipboardDB> db);
    ~ClipboardService();
    
    void process_event(const ClipboardEvent& event);
    std::optional<ClipboardItem> get_item(int64_t id);
    std::vector<ClipboardItem> get_recent_items(int limit = 20);
    void delete_item(int64_t id);
    void clear_all();
    void copy_to_clipboard(const ClipboardItem& item);

    void set_items_updated_callback(std::function<void()> callback);
    
    std::shared_ptr<ClipboardDB> get_db() { return db_; }
    
private:
    std::shared_ptr<ClipboardDB> db_;
    std::string models_path_;

    std::once_flag embedding_init_once_;
    std::once_flag language_init_once_;
    std::once_flag ocr_init_once_;

    std::unique_ptr<EmbeddingService> embedding_service_;
    std::unique_ptr<LanguageDetector> language_detector_;
    std::unique_ptr<OCRService> ocr_service_;

    std::function<void()> items_updated_callback_;
    
    ClipboardType classify_content(const std::string& text);
    void process_image(ClipboardItem& item);
    void process_text(ClipboardItem& item);

    EmbeddingService* get_embedding_service();
    LanguageDetector* get_language_detector();
    OCRService* get_ocr_service();
};
