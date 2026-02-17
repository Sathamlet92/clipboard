#include "search_service.h"
#include <algorithm>
#include <unordered_set>
#include <iostream>

SearchService::SearchService(std::shared_ptr<ClipboardDB> db)
    : db_(db)
{
    std::string model_path = std::string(getenv("HOME")) + 
        "/.clipboard-manager/models/ml/embedding-model.onnx";
    
    try {
        embedding_service_ = std::make_shared<EmbeddingService>(model_path);
    } catch (const std::exception&) {
        // Embedding service optional
    }
}

std::vector<ClipboardItem> SearchService::search(const std::string& query, int limit) {
    // Hybrid search: FTS + Semantic
    auto fts_results = fts_search(query, limit * 2);
    auto semantic_results = semantic_search(query, limit * 2);
    
    return merge_results(fts_results, semantic_results, limit);
}

std::vector<ClipboardItem> SearchService::fts_search(const std::string& query, int limit) {
    return db_->search_fts(query, limit);
}

std::vector<ClipboardItem> SearchService::semantic_search(const std::string& query, int limit) {
    if (!embedding_service_) {
        return {};
    }
    auto query_embedding = embedding_service_->generate_embedding(query);
    return db_->search_by_embedding(query_embedding, limit);
}

std::vector<ClipboardItem> SearchService::merge_results(
    const std::vector<ClipboardItem>& fts_results,
    const std::vector<ClipboardItem>& semantic_results,
    int limit)
{
    std::vector<ClipboardItem> merged;
    std::unordered_set<int64_t> seen_ids;
    
    // Add FTS results first (higher priority)
    for (const auto& item : fts_results) {
        if (seen_ids.find(item.id) == seen_ids.end()) {
            merged.push_back(item);
            seen_ids.insert(item.id);
            if (merged.size() >= static_cast<size_t>(limit)) break;
        }
    }
    
    // Add semantic results
    for (const auto& item : semantic_results) {
        if (seen_ids.find(item.id) == seen_ids.end()) {
            merged.push_back(item);
            seen_ids.insert(item.id);
            if (merged.size() >= static_cast<size_t>(limit)) break;
        }
    }
    
    return merged;
}
