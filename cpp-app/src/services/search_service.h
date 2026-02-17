#pragma once

#include <string>
#include <vector>
#include <memory>
#include "../database/clipboard_db.h"
#include "../ml/embedding_service.h"

class SearchService {
public:
    explicit SearchService(std::shared_ptr<ClipboardDB> db);
    
    std::vector<ClipboardItem> search(const std::string& query, int limit = 20);
    
private:
    std::shared_ptr<ClipboardDB> db_;
    std::shared_ptr<EmbeddingService> embedding_service_;
    
    std::vector<ClipboardItem> exact_search(const std::string& query, int limit);
    std::vector<ClipboardItem> fts_search(const std::string& query, int limit);
    std::vector<ClipboardItem> semantic_search(const std::string& query, int limit);
    std::vector<ClipboardItem> merge_results(
        const std::vector<ClipboardItem>& fts_results,
        const std::vector<ClipboardItem>& semantic_results,
        int limit);
};
