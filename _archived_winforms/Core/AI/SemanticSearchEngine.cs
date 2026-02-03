using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML;
using Microsoft.ML.Transforms.Text;
using SlskDown.Models;

namespace SlskDown.Core.AI
{
    public class SemanticSearchEngine
    {
        private readonly MLContext _mlContext;
        private readonly List<SearchEmbedding> _authorEmbeddings = new();

        public SemanticSearchEngine()
        {
            _mlContext = new MLContext();
        }

        public void IndexAuthors(IEnumerable<string> authors)
        {
            var data = authors.Select(a => new TextData { Text = a }).ToList();
            var textDataView = _mlContext.Data.LoadFromEnumerable(data);

            var pipeline = _mlContext.Transforms.Text.FeaturizeText("Features", nameof(TextData.Text));
            var transformer = pipeline.Fit(textDataView);
            var transformedData = transformer.Transform(textDataView);

            var features = _mlContext.Data.CreateEnumerable<TransformedTextData>(transformedData, reuseRowObject: false).ToList();

            for (int i = 0; i < data.Count; i++)
            {
                _authorEmbeddings.Add(new SearchEmbedding 
                { 
                    Text = data[i].Text, 
                    Vector = features[i].Features 
                });
            }
        }

        public List<string> SearchSemantic(string query, int limit = 10)
        {
            // Nota: En una implementación real, convertiríamos la query a vector
            // y calcularíamos la similitud del coseno. Aquí usamos una aproximación
            // simplificada para demostrar el concepto.
            return _authorEmbeddings
                .OrderByDescending(e => CalculateSimilarity(query, e.Text))
                .Take(limit)
                .Select(e => e.Text)
                .ToList();
        }

        private float CalculateSimilarity(string source, string target)
        {
            // Similitud de Levenshtein o Jaccard simplificada para el ejemplo
            if (source.Contains(target, StringComparison.OrdinalIgnoreCase) || 
                target.Contains(source, StringComparison.OrdinalIgnoreCase)) return 1.0f;
            return 0.0f; 
        }

        private class TextData
        {
            public string Text { get; set; } = "";
        }

        private class TransformedTextData
        {
            public float[] Features { get; set; } = Array.Empty<float>();
        }

        private class SearchEmbedding
        {
            public string Text { get; set; } = "";
            public float[] Vector { get; set; } = Array.Empty<float>();
        }
    }
}
