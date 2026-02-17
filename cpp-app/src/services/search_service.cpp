#include "search_service.h"
#include <algorithm>
#include <unordered_set>
#include <unordered_map>
#include <iostream>
#include <cctype>

namespace {
std::string trim_copy(const std::string& input) {
    auto start = input.find_first_not_of(" \t\n\r");
    if (start == std::string::npos) return "";
    auto end = input.find_last_not_of(" \t\n\r");
    return input.substr(start, end - start + 1);
}

std::string to_lower_copy(std::string value) {
    std::transform(value.begin(), value.end(), value.begin(), [](unsigned char c) {
        return static_cast<char>(std::tolower(c));
    });
    return value;
}

bool is_code_intent(const std::string& q) {
    static const std::vector<std::string> code_terms = {
        "code", "codigo", "código", "snippet", "programming", "programacion", "programación",
        "c#", "csharp", "c sharp", "cs", "dotnet", ".net", "java", "python", "javascript",
        "typescript", "cpp", "c++", "rust", "go", "kotlin", "swift"
    };

    for (const auto& t : code_terms) {
        if (q.find(t) != std::string::npos) return true;
    }
    return false;
}

std::vector<std::string> build_query_expansions(const std::string& raw_query) {
    std::string q = to_lower_copy(trim_copy(raw_query));
    if (q.empty()) return {};

    static const std::unordered_map<std::string, std::string> typo_fix = {
        {"chsarp", "csharp"},
        {"cahrp", "csharp"},
        {"javascritp", "javascript"},
        {"pyhton", "python"}
    };

    auto typo_it = typo_fix.find(q);
    if (typo_it != typo_fix.end()) {
        q = typo_it->second;
    }

    std::vector<std::string> expanded;
    expanded.push_back(q);

    auto add_unique = [&expanded](const std::string& term) {
        if (term.empty()) return;
        if (std::find(expanded.begin(), expanded.end(), term) == expanded.end()) {
            expanded.push_back(term);
        }
    };

    if (q == "csharp" || q == "c#" || q == "c sharp" || q == "cs") {
        add_unique("c#");
        add_unique("csharp");
        add_unique("c sharp");
        add_unique("cs");
        add_unique("dotnet");
        add_unique(".net");
        add_unique("code");
        add_unique("codigo");
    }

    if (q == "codigo" || q == "código" || q == "code") {
        add_unique("code");
        add_unique("codigo");
        add_unique("código");
        add_unique("programming");
        add_unique("programacion");
        add_unique("programación");
        add_unique("snippet");
    }

    if (is_code_intent(q)) {
        add_unique("code");
        add_unique("codigo");
        add_unique("programming");
    }

    return expanded;
}
}

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
    if (query.empty()) {
        return db_->get_recent(limit);
    }

    auto expanded_queries = build_query_expansions(query);
    if (expanded_queries.empty()) {
        return db_->get_recent(limit);
    }

    std::vector<ClipboardItem> exact_accum;
    std::vector<ClipboardItem> fts_accum;
    std::vector<ClipboardItem> semantic_accum;

    for (const auto& q : expanded_queries) {
        auto exact_results = exact_search(q, limit * 2);
        exact_accum = merge_results(exact_accum, exact_results, limit * 4);

        auto fts_results = fts_search(q, limit * 2);
        fts_accum = merge_results(fts_accum, fts_results, limit * 4);
    }

    for (const auto& q : expanded_queries) {
        auto semantic_results = semantic_search(q, limit * 2);
        semantic_accum = merge_results(semantic_accum, semantic_results, limit * 4);
    }

    // Hybrid search with strict priority: EXACT > FTS > SEMANTIC
    auto exact_plus_fts = merge_results(exact_accum, fts_accum, limit * 3);
    return merge_results(exact_plus_fts, semantic_accum, limit);
}

std::vector<ClipboardItem> SearchService::exact_search(const std::string& query, int limit) {
    auto results = db_->search_exact(query, limit);

    const std::string query_lower = to_lower_copy(query);

    auto score_item = [&](const ClipboardItem& item) {
        std::string text;
        if (!item.content.empty() && item.type != ClipboardType::Image) {
            text.assign(item.content.begin(), item.content.end());
        }

        std::string ocr = item.ocr_text;
        std::string lang = item.code_language;

        text = to_lower_copy(text);
        ocr = to_lower_copy(ocr);
        lang = to_lower_copy(lang);

        if (text == query_lower || ocr == query_lower || lang == query_lower) return 0;
        if (text.rfind(query_lower, 0) == 0 || ocr.rfind(query_lower, 0) == 0) return 1;
        return 2;
    };

    std::stable_sort(results.begin(), results.end(), [&](const ClipboardItem& a, const ClipboardItem& b) {
        int sa = score_item(a);
        int sb = score_item(b);
        if (sa != sb) return sa < sb;
        return a.timestamp > b.timestamp;
    });

    return results;
}

std::vector<ClipboardItem> SearchService::fts_search(const std::string& query, int limit) {
    // FTS5 query works better with quoted phrases for user input containing spaces/symbols.
    std::string safe_query = query;
    if (safe_query.find(' ') != std::string::npos) {
        safe_query = '"' + safe_query + '"';
    }

    auto results = db_->search_fts(safe_query, limit);
    if (results.empty() && safe_query != query) {
        // Fallback to raw query if quoted phrase had no matches.
        return db_->search_fts(query, limit);
    }
    return results;
}

std::vector<ClipboardItem> SearchService::semantic_search(const std::string& query, int limit) {
    if (!embedding_service_ || query.size() < 3) {
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
