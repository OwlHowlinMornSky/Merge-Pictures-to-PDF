
namespace PicMerge {
	internal static class Logger {

		private static string m_logFileName = "";
		private static bool m_used = false;

		public static string FilePath {
			get {
				return m_logFileName;
			}
		}

		public static bool Used {
			get {
				return m_used;
			}
		}

		internal static void Init() {
			string dir = Environment.CurrentDirectory;
			dir = Path.Combine(dir, "log");
			if (!Directory.Exists(dir)) {
				Directory.CreateDirectory(dir);
			}
			string time = DateTime.Now.ToString("yyyy-MM-ddTHH+mm+ss");
			int i = 0;
			string path = Path.Combine(dir, time + ".log");
			while (File.Exists(path)) {
				i++;
				path = Path.Combine(dir, time + $"({i}).log");
			}
			m_logFileName = path;
			m_used = false;
		}

		internal static void Reset() {
			m_file?.Dispose();
			m_file = null;
			m_logFileName = "";
			m_used = false;
		}

		internal static void Log(string message) {
			if (string.IsNullOrEmpty(message))
				return;
			if (m_file == null) {
				if (string.IsNullOrEmpty(m_logFileName)) {
					Init();
				}
				m_file = new(m_logFileName);
			}
			m_file.LogString(message);
			m_used = true;
		}

		private class LogFile : IDisposable {
			//private string m_path;
			private readonly FileStream m_file;
			private readonly StreamWriter m_writer;

			public LogFile(string path) {
				//m_path = path;
				m_file = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write);
				m_writer = new StreamWriter(m_file);
			}

			public void LogString(string str) {
				m_writer.WriteLine(str);
			}

			public void Dispose() {
				Dispose(true);
				GC.SuppressFinalize(this);
			}

			~LogFile() {
				Dispose(false);
			}

			private bool m_disposed = false;
			protected virtual void Dispose(bool disposing) {
				if (m_disposed)
					return;
				m_disposed = true;
				if (disposing) {
					m_writer.Close();
					m_writer.Dispose();
					m_file.Close();
					m_file.Dispose();
				}
			}
		}
		private static LogFile? m_file = null;

	}
}
