#pragma once

#include <string>
#include <vector>
#include <onnxruntime_cxx_api.h>
#include <memory>
#include <unordered_map>

class EmbeddingService {
public:
    explicit EmbeddingService(const std::string& model_path);
    ~EmbeddingService() = default;
    
    std::vector<float> generate_embedding(const std::string& text);
    bool is_available() const { return session_ != nullptr; }
    
private:
    struct UnigramEntry {
        int64_t id;
        float score;
    };

    std::unique_ptr<Ort::Env> env_;
    std::unique_ptr<Ort::Session> session_;
    std::unique_ptr<Ort::SessionOptions> session_options_;

    std::unordered_map<std::string, UnigramEntry> unigram_vocab_;
    size_t max_piece_bytes_ = 0;
    int64_t unk_id_ = 3;
    int64_t bos_id_ = 0;
    int64_t eos_id_ = 2;
    int64_t pad_id_ = 1;
    int max_length_ = 128;
    
    void load_tokenizer(const std::string& model_path);
    std::vector<int64_t> tokenize(const std::string& text);
    std::vector<std::string> whitespace_split(const std::string& text) const;
    std::vector<int64_t> unigram_encode_piece(const std::string& piece) const;
    size_t next_utf8_char_len(const std::string& text, size_t offset) const;
    std::vector<float> mean_pooling(const std::vector<float>& token_embeddings, size_t seq_len, size_t hidden_size);
};
