using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace ClipboardManager.ML.Services;

public class BpeTokenizer
{
    private readonly Dictionary<string, int> _vocab;
    private readonly List<(string, string)> _merges;
    private readonly Dictionary<string, int> _cache;
    
    // Tokens especiales de RoBERTa
    private const int BOS_TOKEN_ID = 0;  // <s>
    private const int EOS_TOKEN_ID = 2;  // </s>
    private const int UNK_TOKEN_ID = 3;  // <unk>
    private const int PAD_TOKEN_ID = 1;  // <pad>

    public BpeTokenizer(string vocabPath, string mergesPath)
    {
        _cache = new Dictionary<string, int>();
        
        // Cargar vocabulario desde vocab.json
        var vocabJson = File.ReadAllText(vocabPath);
        _vocab = JsonSerializer.Deserialize<Dictionary<string, int>>(vocabJson) 
                 ?? throw new Exception("Failed to load vocabulary");
        
        // Cargar merges desde merges.txt
        _merges = new List<(string, string)>();
        var mergeLines = File.ReadAllLines(mergesPath);
        
        // Saltar la primera línea si es un header
        var startIndex = mergeLines[0].StartsWith("#") ? 1 : 0;
        
        for (int i = startIndex; i < mergeLines.Length; i++)
        {
            var line = mergeLines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            
            var parts = line.Split(' ');
            if (parts.Length == 2)
            {
                _merges.Add((parts[0], parts[1]));
            }
        }
    }

    public List<int> Encode(string text, int maxLength = 512)
    {
        var tokens = new List<int> { BOS_TOKEN_ID };
        
        if (string.IsNullOrEmpty(text))
        {
            tokens.Add(EOS_TOKEN_ID);
            return tokens;
        }
        
        // Pre-tokenización: dividir en palabras
        var words = PreTokenize(text);
        
        foreach (var word in words)
        {
            if (tokens.Count >= maxLength - 1)
                break;
            
            // Aplicar BPE a cada palabra
            var wordTokens = BpeEncode(word);
            
            foreach (var tokenStr in wordTokens)
            {
                if (tokens.Count >= maxLength - 1)
                    break;
                
                if (_vocab.TryGetValue(tokenStr, out int tokenId))
                {
                    tokens.Add(tokenId);
                }
                else
                {
                    tokens.Add(UNK_TOKEN_ID);
                }
            }
        }
        
        tokens.Add(EOS_TOKEN_ID);
        return tokens;
    }

    private List<string> PreTokenize(string text)
    {
        // Pre-tokenización simple: dividir por espacios y caracteres especiales
        var words = new List<string>();
        var currentWord = new StringBuilder();
        
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (currentWord.Length > 0)
                {
                    words.Add(currentWord.ToString());
                    currentWord.Clear();
                }
            }
            else if (char.IsPunctuation(ch) || char.IsSymbol(ch))
            {
                if (currentWord.Length > 0)
                {
                    words.Add(currentWord.ToString());
                    currentWord.Clear();
                }
                words.Add(ch.ToString());
            }
            else
            {
                currentWord.Append(ch);
            }
        }
        
        if (currentWord.Length > 0)
        {
            words.Add(currentWord.ToString());
        }
        
        return words;
    }

    private List<string> BpeEncode(string word)
    {
        // Convertir palabra a caracteres individuales con espacio especial
        var chars = word.Select(c => c.ToString()).ToList();
        
        if (chars.Count == 0)
            return new List<string>();
        
        // Agregar símbolo de inicio de palabra (Ġ en RoBERTa)
        chars[0] = "Ġ" + chars[0];
        
        // Aplicar merges iterativamente
        while (chars.Count > 1)
        {
            var pairs = GetPairs(chars);
            if (pairs.Count == 0)
                break;
            
            // Encontrar el merge con menor índice (más prioritario)
            (string, string)? bestPair = null;
            int bestIndex = int.MaxValue;
            
            foreach (var pair in pairs)
            {
                var index = _merges.IndexOf(pair);
                if (index >= 0 && index < bestIndex)
                {
                    bestIndex = index;
                    bestPair = pair;
                }
            }
            
            if (bestPair == null)
                break;
            
            // Aplicar el merge
            chars = MergePair(chars, bestPair.Value);
        }
        
        return chars;
    }

    private List<(string, string)> GetPairs(List<string> chars)
    {
        var pairs = new List<(string, string)>();
        
        for (int i = 0; i < chars.Count - 1; i++)
        {
            pairs.Add((chars[i], chars[i + 1]));
        }
        
        return pairs.Distinct().ToList();
    }

    private List<string> MergePair(List<string> chars, (string, string) pair)
    {
        var result = new List<string>();
        int i = 0;
        
        while (i < chars.Count)
        {
            if (i < chars.Count - 1 && chars[i] == pair.Item1 && chars[i + 1] == pair.Item2)
            {
                result.Add(pair.Item1 + pair.Item2);
                i += 2;
            }
            else
            {
                result.Add(chars[i]);
                i++;
            }
        }
        
        return result;
    }
}
