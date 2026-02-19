using ClipboardManager.Core.Models;

namespace ClipboardManager.Core.Interfaces;

public interface IClipboardRepository
{
    Task<long> AddAsync(ClipboardItem item);
    Task<ClipboardItem?> GetByIdAsync(long id);
    Task<List<ClipboardItem>> GetRecentAsync(int limit = 100);
    Task<bool> UpdateAsync(ClipboardItem item);
    Task<bool> DeleteAsync(long id);
    Task<int> DeleteAllAsync();
    Task<int> DeleteOlderThanAsync(DateTime cutoffDate);
    Task<bool> UpdateOcrTextAsync(long id, string ocrText);
    Task<int> GetCountAsync();
    Task<bool> ExistsByHashAsync(string contentHash);
}
