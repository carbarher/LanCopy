using System;
using System.Collections.Generic;

namespace SlskDown.Core
{
    /// <summary>
    /// Argumentos del evento de resultados parciales de búsqueda
    /// Permite mostrar resultados a medida que llegan de cada red
    /// </summary>
    public class PartialSearchResultsEventArgs : EventArgs
    {
        public string SearchId { get; set; }
        public string NetworkName { get; set; }
        public List<SearchResult> Results { get; set; }
        public bool IsComplete { get; set; }
        public int TotalResultsFromNetwork { get; set; }
    }
}
