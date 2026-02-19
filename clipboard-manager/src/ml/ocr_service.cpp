#include "ocr_service.h"
#include <opencv2/opencv.hpp>
#include <iostream>

OCRService::OCRService(const std::string& tessdata_path) {
    api_ = new tesseract::TessBaseAPI();
    
    if (api_->Init(tessdata_path.c_str(), "eng+spa")) {
        std::cerr << "Could not initialize tesseract." << std::endl;
        delete api_;
        api_ = nullptr;
    }
}

OCRService::~OCRService() {
    if (api_) {
        api_->End();
        delete api_;
    }
}

std::string OCRService::extract_text(const std::vector<uint8_t>& image_data) {
    if (!api_) {
        return "";
    }
    
    // Decode image using OpenCV
    cv::Mat img = cv::imdecode(image_data, cv::IMREAD_COLOR);
    if (img.empty()) {
        return "";
    }
    
    // Convert to grayscale
    cv::Mat gray;
    cv::cvtColor(img, gray, cv::COLOR_BGR2GRAY);
    
    // Set image for Tesseract
    api_->SetImage(gray.data, gray.cols, gray.rows, 1, gray.step);
    
    // Extract text
    char* text = api_->GetUTF8Text();
    std::string result(text);
    delete[] text;
    
    return result;
}
