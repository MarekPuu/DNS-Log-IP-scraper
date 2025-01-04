namespace DNS_Log_IP_scraper.Model
{
    public class FileStatus
    {
        public string FileName { get; }
        public string Status { get; set; }
        public double Rate { get; set; }
        public int UniqueIps { get; set; }
        public DateTime LastUpdate { get; set; }
        public bool NeedsUpdate { get; set; }

        public FileStatus(string fileName)
        {
            FileName = fileName;
            Status = "Pending";
            NeedsUpdate = true;
        }
    }
}
