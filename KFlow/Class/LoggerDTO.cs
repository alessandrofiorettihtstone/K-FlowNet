using Microsoft.Extensions.Configuration;
using KFlow.Models;

namespace KFlow.Class
{
    public class LoggerDTO
    {
        private readonly dbContext _db;
        private readonly IConfiguration _configuration;

        public LoggerDTO(dbContext db, IConfiguration configuration)
        {
            _db = db;
            _configuration = configuration;
        }


        public void Log(LoggerDTO.logDTO logDTO)
        {
            try
            {
                TblLog t = new TblLog
                {
                    ODP = string.Empty,
                    IDMix = string.Empty,
                    IDRows = string.Empty,
                    IDUser = Truncate(logDTO.IDUser, 3),
                    Text = Truncate($"{logDTO.typeMessage} [{logDTO.TypeFunction}] {logDTO.TypeText}", 4000),
                    PostingDate = DateTime.Now
                };

                _db.TblLogs.Add(t);
                _db.SaveChanges();
            }
            catch (Exception ex)
            {
                TryWriteFileLog(logDTO, ex);
            }
        }

        private void TryWriteFileLog(logDTO logDTO, Exception? exception = null)
        {
            try
            {
                var configuredPath = _configuration["KFlow:LogDirectory"];
                var logDirectory = string.IsNullOrWhiteSpace(configuredPath)
                    ? Path.Combine(AppContext.BaseDirectory, "Logs")
                    : configuredPath;

                if (!Path.IsPathRooted(logDirectory))
                {
                    logDirectory = Path.Combine(AppContext.BaseDirectory, logDirectory);
                }

                Directory.CreateDirectory(logDirectory);
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{logDTO.typeMessage}]-[{logDTO.TypeFunction}] {logDTO.TypeText}";
                if (exception != null)
                {
                    line = $"{line} | logger fallback: {exception.Message}";
                }

                File.AppendAllLines(Path.Combine(logDirectory, $"{DateTime.Today:yyyyMMdd}.txt"), new[] { line });
            }
            catch
            {
            }
        }

        private static string Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Length <= maxLength ? value : value[..maxLength];
        }

        public class logDTO
        {

            public DateTime Timestamp { get; set; }
            public string typeMessage { get; set; } = string.Empty;

            public string TypeText { get; set; } = string.Empty;

            public string TypeFunction { get; set; } = string.Empty;

            public string IDUser { get; set; } = string.Empty;
        }
    }
}
