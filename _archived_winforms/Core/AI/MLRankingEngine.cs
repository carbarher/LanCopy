using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML;
using Microsoft.ML.Data;
using SlskDown.Models;

namespace SlskDown.Core.AI
{
    public class SearchResultMLData
    {
        [LoadColumn(0)] public float FileSize { get; set; }
        [LoadColumn(1)] public float Bitrate { get; set; }
        [LoadColumn(2)] public float UploadSpeed { get; set; }
        [LoadColumn(3)] public float QueueLength { get; set; }
        [LoadColumn(4)] public float IsSpanish { get; set; }
        [LoadColumn(5)] public float Label { get; set; }
    }

    public class SearchResultPrediction
    {
        [ColumnName("Score")] public float Score { get; set; }
        [ColumnName("Probability")] public float Probability { get; set; }
        [ColumnName("PredictedLabel")] public bool PredictedLabel { get; set; }
    }

    public class MLRankingEngine
    {
        private readonly MLContext _mlContext;
        private ITransformer? _model;
        private PredictionEngine<SearchResultMLData, SearchResultPrediction>? _predictionEngine;

        public MLRankingEngine()
        {
            _mlContext = new MLContext(seed: 42);
        }

        public void TrainDefaultModel()
        {
            const float OneMB = 1024 * 1024;
            var data = new List<SearchResultMLData>
            {
                new() { FileSize = 100 * OneMB, Bitrate = 320, UploadSpeed = 1000, QueueLength = 0, IsSpanish = 1, Label = 1 },
                new() { FileSize = 5 * OneMB, Bitrate = 128, UploadSpeed = 10, QueueLength = 50, IsSpanish = 0, Label = 0 },
                new() { FileSize = 50 * OneMB, Bitrate = 256, UploadSpeed = 500, QueueLength = 5, IsSpanish = 1, Label = 1 },
                new() { FileSize = 2 * OneMB, Bitrate = 64, UploadSpeed = 5, QueueLength = 100, IsSpanish = 0, Label = 0 }
            };

            var trainingData = _mlContext.Data.LoadFromEnumerable(data);

            var pipeline = _mlContext.Transforms.Concatenate("Features", 
                nameof(SearchResultMLData.FileSize), 
                nameof(SearchResultMLData.Bitrate), 
                nameof(SearchResultMLData.UploadSpeed), 
                nameof(SearchResultMLData.QueueLength), 
                nameof(SearchResultMLData.IsSpanish))
                .Append(_mlContext.BinaryClassification.Trainers.FastTree(labelColumnName: "Label"));

            _model = pipeline.Fit(trainingData);
            _predictionEngine = _mlContext.Model.CreatePredictionEngine<SearchResultMLData, SearchResultPrediction>(_model);
        }

        public float PredictScore(SlskDown.SearchResult item)
        {
            if (_predictionEngine == null) TrainDefaultModel();

            var input = new SearchResultMLData
            {
                FileSize = (float)item.Size,
                Bitrate = (float)(item.Bitrate ?? 0),
                UploadSpeed = 0,
                QueueLength = (float)(item.QueueLength ?? 0),
                IsSpanish = (item.Filename != null && item.Filename.IndexOf("Spanish", StringComparison.OrdinalIgnoreCase) >= 0) ? 1f : 0f
            };

            var prediction = _predictionEngine!.Predict(input);
            return prediction.Score;
        }

        public List<SlskDown.SearchResult> RankResults(IEnumerable<SlskDown.SearchResult> results)
        {
            return results.OrderByDescending(PredictScore).ToList();
        }
    }
}
