using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace SlskDown.Core
{
    /// <summary>
    /// Servicio de ranking inteligente usando ML.NET
    /// Aprende de las preferencias del usuario para rankear resultados
    /// </summary>
    public class SmartRankingService
    {
        private readonly MLContext _mlContext;
        private ITransformer? _model;
        private readonly string _modelPath;
        private readonly List<SearchFeatures> _trainingData = new();

        public SmartRankingService(string modelPath)
        {
            _mlContext = new MLContext(seed: 0);
            _modelPath = modelPath;
            
            // Cargar modelo si existe
            if (File.Exists(modelPath))
            {
                LoadModel();
            }
        }

        /// <summary>
        /// Entrena el modelo con historial de descargas
        /// </summary>
        public void Train(List<(SearchResultItem item, bool downloaded)> history)
        {
            if (history.Count < 10)
            {
                System.Diagnostics.Debug.WriteLine("Necesitas al menos 10 ejemplos para entrenar");
                return;
            }

            // Convertir a features
            _trainingData.Clear();
            foreach (var (item, downloaded) in history)
            {
                _trainingData.Add(new SearchFeatures
                {
                    Size = item.Size,
                    Quality = item.Quality,
                    Speed = item.Speed,
                    QueueLength = item.QueueLength,
                    HasFreeSlot = item.HasFreeSlot ? 1f : 0f,
                    Label = downloaded
                });
            }

            var data = _mlContext.Data.LoadFromEnumerable(_trainingData);

            // Pipeline de entrenamiento
            var pipeline = _mlContext.Transforms.Concatenate("Features",
                    nameof(SearchFeatures.Size),
                    nameof(SearchFeatures.Quality),
                    nameof(SearchFeatures.Speed),
                    nameof(SearchFeatures.QueueLength),
                    nameof(SearchFeatures.HasFreeSlot))
                .Append(_mlContext.Transforms.NormalizeMinMax("Features"))
                .Append(_mlContext.BinaryClassification.Trainers.FastTree(
                    numberOfLeaves: 20,
                    numberOfTrees: 100,
                    minimumExampleCountPerLeaf: 2));

            // Entrenar
            _model = pipeline.Fit(data);

            // Guardar modelo
            SaveModel();

            System.Diagnostics.Debug.WriteLine($"Modelo entrenado con {history.Count} ejemplos");
        }

        /// <summary>
        /// Predice relevancia de un resultado (0-1)
        /// </summary>
        public float PredictRelevance(SearchResultItem item)
        {
            if (_model == null)
                return 0.5f; // Sin modelo, relevancia neutral

            var engine = _mlContext.Model.CreatePredictionEngine<SearchFeatures, Prediction>(_model);
            
            var features = new SearchFeatures
            {
                Size = item.Size,
                Quality = item.Quality,
                Speed = item.Speed,
                QueueLength = item.QueueLength,
                HasFreeSlot = item.HasFreeSlot ? 1f : 0f
            };

            var prediction = engine.Predict(features);
            return prediction.Score;
        }

        /// <summary>
        /// Rankea lista de resultados según modelo
        /// </summary>
        public List<SearchResultItem> RankResults(List<SearchResultItem> results)
        {
            if (_model == null || results.Count == 0)
                return results;

            // Predecir relevancia para cada resultado
            var scored = results.Select(r => new
            {
                Result = r,
                Score = PredictRelevance(r)
            }).ToList();

            // Ordenar por score descendente
            return scored.OrderByDescending(s => s.Score)
                .Select(s => s.Result)
                .ToList();
        }

        /// <summary>
        /// Registra feedback del usuario (descargó o no)
        /// </summary>
        public void RecordFeedback(SearchResultItem item, bool downloaded)
        {
            _trainingData.Add(new SearchFeatures
            {
                Size = item.Size,
                Quality = item.Quality,
                Speed = item.Speed,
                QueueLength = item.QueueLength,
                HasFreeSlot = item.HasFreeSlot ? 1f : 0f,
                Label = downloaded
            });

            // Re-entrenar cada 50 ejemplos
            if (_trainingData.Count % 50 == 0)
            {
                var history = _trainingData.Select(f => (
                    new SearchResultItem
                    {
                        Size = (long)f.Size,
                        Quality = (int)f.Quality,
                        Speed = (int)f.Speed,
                        QueueLength = (int)f.QueueLength,
                        HasFreeSlot = f.HasFreeSlot > 0
                    },
                    f.Label
                )).ToList();

                Train(history);
            }
        }

        /// <summary>
        /// Evalúa el modelo con datos de test
        /// </summary>
        public ModelMetrics? Evaluate(List<(SearchResultItem item, bool downloaded)> testData)
        {
            if (_model == null || testData.Count == 0)
                return null;

            var features = testData.Select(t => new SearchFeatures
            {
                Size = t.item.Size,
                Quality = t.item.Quality,
                Speed = t.item.Speed,
                QueueLength = t.item.QueueLength,
                HasFreeSlot = t.item.HasFreeSlot ? 1f : 0f,
                Label = t.downloaded
            }).ToList();

            var testDataView = _mlContext.Data.LoadFromEnumerable(features);
            var predictions = _model.Transform(testDataView);
            var metrics = _mlContext.BinaryClassification.Evaluate(predictions);

            return new ModelMetrics
            {
                Accuracy = metrics.Accuracy,
                AUC = metrics.AreaUnderRocCurve,
                F1Score = metrics.F1Score,
                Precision = metrics.PositivePrecision,
                Recall = metrics.PositiveRecall
            };
        }

        private void SaveModel()
        {
            if (_model == null)
                return;

            try
            {
                _mlContext.Model.Save(_model, null, _modelPath);
                System.Diagnostics.Debug.WriteLine($"Modelo guardado en {_modelPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error guardando modelo: {ex.Message}");
            }
        }

        private void LoadModel()
        {
            try
            {
                _model = _mlContext.Model.Load(_modelPath, out _);
                System.Diagnostics.Debug.WriteLine($"Modelo cargado desde {_modelPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando modelo: {ex.Message}");
                _model = null;
            }
        }
    }

    /// <summary>
    /// Features para entrenamiento
    /// </summary>
    public class SearchFeatures
    {
        [LoadColumn(0)]
        public float Size { get; set; }

        [LoadColumn(1)]
        public float Quality { get; set; }

        [LoadColumn(2)]
        public float Speed { get; set; }

        [LoadColumn(3)]
        public float QueueLength { get; set; }

        [LoadColumn(4)]
        public float HasFreeSlot { get; set; }

        [LoadColumn(5), ColumnName("Label")]
        public bool Label { get; set; }
    }

    /// <summary>
    /// Predicción del modelo
    /// </summary>
    public class Prediction
    {
        [ColumnName("Score")]
        public float Score { get; set; }

        [ColumnName("PredictedLabel")]
        public bool PredictedLabel { get; set; }
    }

    /// <summary>
    /// Métricas del modelo
    /// </summary>
    public class ModelMetrics
    {
        public double Accuracy { get; set; }
        public double AUC { get; set; }
        public double F1Score { get; set; }
        public double Precision { get; set; }
        public double Recall { get; set; }

        public override string ToString()
        {
            return $"Accuracy: {Accuracy:P2}, AUC: {AUC:F3}, F1: {F1Score:F3}";
        }
    }

    /// <summary>
    /// Servicio de ranking híbrido (reglas + ML)
    /// </summary>
    public class HybridRankingService
    {
        private readonly SmartRankingService _mlRanking;

        public HybridRankingService(string modelPath)
        {
            _mlRanking = new SmartRankingService(modelPath);
        }

        /// <summary>
        /// Rankea con combinación de reglas y ML
        /// </summary>
        public List<SearchResultItem> RankResults(List<SearchResultItem> results)
        {
            // Score base con reglas
            var scored = results.Select(r => new
            {
                Result = r,
                RuleScore = CalculateRuleScore(r),
                MLScore = _mlRanking.PredictRelevance(r)
            }).ToList();

            // Combinar scores (70% reglas, 30% ML)
            var ranked = scored.Select(s => new
            {
                s.Result,
                FinalScore = (s.RuleScore * 0.7) + (s.MLScore * 0.3)
            }).OrderByDescending(s => s.FinalScore)
            .Select(s => s.Result)
            .ToList();

            return ranked;
        }

        private double CalculateRuleScore(SearchResultItem item)
        {
            double score = 0;

            // Calidad (0-40 puntos)
            score += (item.Quality / 100.0) * 40;

            // Velocidad (0-30 puntos)
            if (item.Speed > 0)
            {
                var speedScore = Math.Min(item.Speed / 10000.0, 1.0) * 30;
                score += speedScore;
            }

            // Free slot (20 puntos)
            if (item.HasFreeSlot)
                score += 20;

            // Cola corta (0-10 puntos)
            var queueScore = Math.Max(0, 10 - (item.QueueLength / 10.0));
            score += queueScore;

            return score / 100.0; // Normalizar a 0-1
        }

        public void Train(List<(SearchResultItem item, bool downloaded)> history)
        {
            _mlRanking.Train(history);
        }

        public void RecordFeedback(SearchResultItem item, bool downloaded)
        {
            _mlRanking.RecordFeedback(item, downloaded);
        }
    }
}
