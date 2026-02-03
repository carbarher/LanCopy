using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SlskDown.AI
{
    /// <summary>
    /// Gestor centralizado para todas las funcionalidades de IA con Ollama
    /// </summary>
    public class AIManager
    {
        private readonly OllamaClient ollamaClient;
        private readonly AIFileClassifier classifier;
        private readonly AIRecommender recommender;
        private readonly AIQualityDetector qualityDetector;
        private readonly SemanticSearch semanticSearch;
        private readonly ChatAssistant chatAssistant;
        private readonly BookSummarizer bookSummarizer;
        private readonly MetadataExtractor metadataExtractor;
        private readonly LanguageDetector languageDetector;
        private readonly TagSuggester tagSuggester;
        
        public bool IsEnabled { get; private set; }
        public bool IsAvailable { get; private set; }

        public AIManager(string ollamaUrl = "http://localhost:11434", string defaultModel = "llama3.2")
        {
            ollamaClient = new OllamaClient(ollamaUrl, defaultModel);
            
            // Inicializar componentes
            classifier = new AIFileClassifier(ollamaClient);
            recommender = new AIRecommender(ollamaClient);
            qualityDetector = new AIQualityDetector(ollamaClient);
            semanticSearch = new SemanticSearch(ollamaClient);
            chatAssistant = new ChatAssistant(ollamaClient);
            bookSummarizer = new BookSummarizer(ollamaClient);
            metadataExtractor = new MetadataExtractor(ollamaClient);
            languageDetector = new LanguageDetector(ollamaClient);
            tagSuggester = new TagSuggester(ollamaClient);
            
            IsEnabled = true;
        }

        /// <summary>
        /// Verifica disponibilidad de Ollama
        /// </summary>
        public async Task<bool> CheckAvailabilityAsync()
        {
            try
            {
                IsAvailable = await ollamaClient.IsAvailableAsync();
                return IsAvailable;
            }
            catch
            {
                IsAvailable = false;
                return false;
            }
        }

        /// <summary>
        /// Lista modelos disponibles en Ollama
        /// </summary>
        public async Task<List<string>> GetAvailableModelsAsync()
        {
            try
            {
                return await ollamaClient.ListModelsAsync();
            }
            catch
            {
                return new List<string>();
            }
        }

        // CLASIFICACIÓN
        public async Task<FileClassification> ClassifyFileAsync(string fileName, string author = null)
        {
            if (!IsEnabled || !IsAvailable) return null;
            return await classifier.ClassifyFileAsync(fileName, author);
        }

        public async Task<List<FileClassification>> ClassifyBatchAsync(List<(string fileName, string author)> files)
        {
            if (!IsEnabled || !IsAvailable) return new List<FileClassification>();
            return await classifier.ClassifyBatchAsync(files);
        }

        public async Task<List<DuplicateGroup>> DetectSemanticDuplicatesAsync(List<string> fileNames)
        {
            if (!IsEnabled || !IsAvailable) return new List<DuplicateGroup>();
            return await classifier.DetectSemanticDuplicatesAsync(fileNames);
        }

        // RECOMENDACIONES
        public async Task<List<Recommendation>> GetRecommendationsAsync(List<string> downloadHistory, int maxRecommendations = 10)
        {
            if (!IsEnabled || !IsAvailable) return new List<Recommendation>();
            return await recommender.GetRecommendationsAsync(downloadHistory, maxRecommendations);
        }

        public async Task<List<Recommendation>> GetSimilarFilesAsync(string fileName, string author = null, int maxResults = 5)
        {
            if (!IsEnabled || !IsAvailable) return new List<Recommendation>();
            return await recommender.GetSimilarFilesAsync(fileName, author, maxResults);
        }

        public async Task<List<string>> SuggestRelatedAuthorsAsync(List<string> currentAuthors, int maxSuggestions = 5)
        {
            if (!IsEnabled || !IsAvailable) return new List<string>();
            return await recommender.SuggestRelatedAuthorsAsync(currentAuthors, maxSuggestions);
        }

        public async Task<UserInterestProfile> AnalyzeUserInterestsAsync(List<string> downloadHistory)
        {
            if (!IsEnabled || !IsAvailable) return new UserInterestProfile();
            return await recommender.AnalyzeUserInterestsAsync(downloadHistory);
        }

        // DETECCIÓN DE CALIDAD
        public async Task<QualityAnalysis> AnalyzeFileQualityAsync(string fileName, string username = null, long fileSize = 0)
        {
            if (!IsEnabled || !IsAvailable) return null;
            return await qualityDetector.AnalyzeFileQualityAsync(fileName, username, fileSize);
        }

        public async Task<bool> IsSpamOrFakeAsync(string fileName)
        {
            if (!IsEnabled || !IsAvailable) return false;
            return await qualityDetector.IsSpamOrFakeAsync(fileName);
        }

        public async Task<MetadataValidation> ValidateMetadataAsync(string fileName, string author, long fileSize, string extension)
        {
            if (!IsEnabled || !IsAvailable) return new MetadataValidation { IsValid = true };
            return await qualityDetector.ValidateMetadataAsync(fileName, author, fileSize, extension);
        }

        public async Task<List<QualityAnalysis>> AnalyzeBatchQualityAsync(List<(string fileName, string username, long fileSize)> files)
        {
            if (!IsEnabled || !IsAvailable) return new List<QualityAnalysis>();
            return await qualityDetector.AnalyzeBatchAsync(files);
        }

        // BÚSQUEDA SEMÁNTICA
        public async Task<List<SemanticSearchResult>> SearchByConceptAsync(string concept, List<string> fileNames, int maxResults = 10)
        {
            if (!IsEnabled || !IsAvailable) return new List<SemanticSearchResult>();
            return await semanticSearch.SearchByConceptAsync(concept, fileNames, maxResults);
        }

        public async Task<List<string>> SearchByDescriptionAsync(string description, List<string> fileNames, int maxResults = 10)
        {
            if (!IsEnabled || !IsAvailable) return new List<string>();
            return await semanticSearch.SearchByDescriptionAsync(description, fileNames, maxResults);
        }

        public async Task<List<string>> ExpandQueryAsync(string query)
        {
            if (!IsEnabled || !IsAvailable) return new List<string> { query };
            return await semanticSearch.ExpandQueryAsync(query);
        }

        public async Task<List<SemanticSearchResult>> FindSimilarFilesAsync(string fileName, List<string> allFiles, int maxResults = 5)
        {
            if (!IsEnabled || !IsAvailable) return new List<SemanticSearchResult>();
            return await semanticSearch.FindSimilarFilesAsync(fileName, allFiles, maxResults);
        }

        public async Task<List<SemanticGroup>> GroupBySimilarityAsync(List<string> fileNames, double similarityThreshold = 0.7)
        {
            if (!IsEnabled || !IsAvailable) return new List<SemanticGroup>();
            return await semanticSearch.GroupBySimilarityAsync(fileNames, similarityThreshold);
        }

        // ASISTENTE DE CHAT
        public async Task<ChatResponse> ProcessChatMessageAsync(string userMessage)
        {
            if (!IsEnabled || !IsAvailable) return new ChatResponse { Message = "IA no disponible" };
            return await chatAssistant.ProcessMessageAsync(userMessage);
        }

        public async Task<string> GetContextualHelpAsync(string context)
        {
            if (!IsEnabled || !IsAvailable) return "Ayuda no disponible";
            return await chatAssistant.GetContextualHelpAsync(context);
        }

        public async Task<List<string>> SuggestActionsAsync(string currentState)
        {
            if (!IsEnabled || !IsAvailable) return new List<string>();
            return await chatAssistant.SuggestActionsAsync(currentState);
        }

        public async Task<string> ExplainErrorAsync(string errorMessage)
        {
            if (!IsEnabled || !IsAvailable) return errorMessage;
            return await chatAssistant.ExplainErrorAsync(errorMessage);
        }

        public async Task<string> GenerateActivitySummaryAsync(Dictionary<string, object> stats)
        {
            if (!IsEnabled || !IsAvailable) return "Resumen no disponible";
            return await chatAssistant.GenerateActivitySummaryAsync(stats);
        }

        public void ClearChatHistory()
        {
            chatAssistant.ClearHistory();
        }

        public List<ChatMessage> GetChatHistory()
        {
            return chatAssistant.GetHistory();
        }

        // GENERACIÓN DE RESÚMENES
        public async Task<BookSummary> GenerateBookSummaryAsync(string filePath)
        {
            if (!IsEnabled || !IsAvailable) return new BookSummary { Success = false, Error = "IA no disponible" };
            return await bookSummarizer.GenerateSummaryAsync(filePath);
        }

        public async Task<List<BookSummary>> GenerateBookSummariesAsync(List<string> filePaths, IProgress<int> progress = null)
        {
            if (!IsEnabled || !IsAvailable) return new List<BookSummary>();
            return await bookSummarizer.GenerateSummariesAsync(filePaths, progress);
        }

        // EXTRACCIÓN DE METADATOS
        public async Task<ExtractedMetadata> ExtractMetadataAsync(string filePath)
        {
            if (!IsEnabled || !IsAvailable) return new ExtractedMetadata { Success = false, Error = "IA no disponible" };
            return await metadataExtractor.ExtractFromFileAsync(filePath);
        }

        public async Task<List<ExtractedMetadata>> ExtractMetadataFromFilesAsync(List<string> filePaths, IProgress<int> progress = null)
        {
            if (!IsEnabled || !IsAvailable) return new List<ExtractedMetadata>();
            return await metadataExtractor.ExtractFromFilesAsync(filePaths, progress);
        }

        // DETECCIÓN DE IDIOMA
        public async Task<LanguageDetectionResult> DetectLanguageAsync(string filePath)
        {
            if (!IsEnabled || !IsAvailable) return new LanguageDetectionResult { Success = false, Error = "IA no disponible" };
            return await languageDetector.DetectLanguageAsync(filePath);
        }

        public async Task<List<LanguageDetectionResult>> DetectLanguagesAsync(List<string> filePaths, IProgress<int> progress = null)
        {
            if (!IsEnabled || !IsAvailable) return new List<LanguageDetectionResult>();
            return await languageDetector.DetectLanguagesAsync(filePaths, progress);
        }

        public async Task<bool> IsSpanishTextAsync(string text)
        {
            if (!IsEnabled || !IsAvailable) return false;
            return await languageDetector.IsSpanishAsync(text);
        }

        // SUGERENCIAS DE ETIQUETAS
        public async Task<TagSuggestion> SuggestTagsAsync(string filePath, string author = null, string title = null)
        {
            if (!IsEnabled || !IsAvailable) return new TagSuggestion { Success = false, Error = "IA no disponible" };
            return await tagSuggester.SuggestTagsAsync(filePath, author, title);
        }

        public async Task<TagSuggestion> SuggestTagsFromMetadataAsync(string author, string title, string genre = null)
        {
            if (!IsEnabled || !IsAvailable) return new TagSuggestion { Success = false, Error = "IA no disponible" };
            return await tagSuggester.SuggestTagsFromMetadataAsync(author, title, genre);
        }

        public async Task<List<TagSuggestion>> SuggestTagsForFilesAsync(List<string> filePaths, IProgress<int> progress = null)
        {
            if (!IsEnabled || !IsAvailable) return new List<TagSuggestion>();
            return await tagSuggester.SuggestTagsForFilesAsync(filePaths, progress);
        }

        // CONTROL
        public void Enable()
        {
            IsEnabled = true;
        }

        public void Disable()
        {
            IsEnabled = false;
        }
    }
}
