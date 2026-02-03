using System;
using System.Collections.Generic;

namespace SlskDown.Core
{
    public static class SearchFilterRules
    {
        public static bool IsValidResult(string filename) => true;
        
        public static List<string> ApplyFilters(List<string> results) => results ?? new List<string>();
        
        public static void AddRule(string rule) { }
        
        public static void ClearRules() { }
    }
}
