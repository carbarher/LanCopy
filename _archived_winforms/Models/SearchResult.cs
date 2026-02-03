namespace SlskDown
{
    public class SearchResult
    {
        public string Username { get; set; } = "";
        public string Filename { get; set; } = "";
        public long Size { get; set; }
        public long SizeBytes
        {
            get => Size;
            set => Size = value;
        }
        public string Query { get; set; } = "";
        public string Extension { get; set; } = "";
        public int? Bitrate { get; set; }
        public int? BitRate { get; set; }  // Alias para compatibilidad
        public int? Length { get; set; }
        public string FolderName { get; set; } = "";
        public string Directory { get; set; } = "";
        public string Country { get; set; } = "";
        public int QualityScore { get; set; }
        public int? QueueLength { get; set; }
        public int? FreeUploadSlots { get; set; }
        public string Source { get; set; } = "Soulseek"; // "Soulseek", "eMule", etc.
    }
}

