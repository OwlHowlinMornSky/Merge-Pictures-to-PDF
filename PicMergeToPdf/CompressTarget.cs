using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.MemoryMappedFiles;

namespace PicMerge {
	internal class CompressTarget : IDisposable {

		private MemoryMappedFile? m_mapfile;
		private PicCompress.Compressor? m_compressor;

		internal PicCompress.Compressor Compressor {
			get {
				if (m_compressor == null) {
					m_mapfile ??= MemoryMappedFile.CreateNew(null, IMerger.MapFileSize, MemoryMappedFileAccess.ReadWrite);
					m_compressor = new(m_mapfile.SafeMemoryMappedFileHandle.DangerousGetHandle(), IMerger.MapFileSize);
				}
				return m_compressor;
			}
		}

		internal Stream ViewStream {
			get {
				m_mapfile ??= MemoryMappedFile.CreateNew(null, IMerger.MapFileSize, MemoryMappedFileAccess.ReadWrite);
				return m_mapfile.CreateViewStream();
			}
		}

		~CompressTarget() {
			Dispose(false);
		}

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private bool m_disposed = false;
		protected virtual void Dispose(bool disposing) {
			if (m_disposed)
				return;
			if (disposing) {
				m_compressor?.Dispose();
				m_mapfile?.Dispose();
			}
			m_disposed = true;
		}
	}
}
