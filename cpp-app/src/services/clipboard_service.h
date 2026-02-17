#pragma once

#include "../database/clipboard_db.h"
#include "../ml/embedding_service.h"
#include "../ml/language_detector.h"
#include "../ml/ocr_service.h"
#include <functional>
#include <memory>

struct ClipboardEvent {
    std::string content_type;
    std::string text_content;
    std::vector<uint8_t> image_data;
    int64_t timestamp;
};

class ClipboardService {
public:
    ClipboardService(std::shared_ptr<ClipboardDB> db);
    
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
    std::unique_ptr<EmbeddingService> embedding_service_;
    std::unique_ptr<LanguageDetector> language_detector_;
    std::unique_ptr<OCRService> ocr_service_;

    std::function<void()> items_updated_callback_;
    
    ClipboardType classify_content(const std::string& text);
    void process_image(ClipboardItem& item);
    void process_text(ClipboardItem& item);
};
