using iText.Kernel.Pdf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PicMerge {
	internal class PdfTarget(string _outputPath, string? _title) : IDisposable {

		private readonly string outputfilepath = _outputPath;
		private readonly string? title = _title;

		/// <summary>
		/// 文件流。首次使用时创建。
		/// </summary>
		private FileStream? _outputFileStream = null;
		/// <summary>
		/// 填写器。首次使用时创建。
		/// </summary>
		private PdfWriter? _pdfWriter = null;
		/// <summary>
		/// 文档。首次使用时创建。
		/// </summary>
		private PdfDocument? _pdfDocument = null;

		internal PdfDocument Document {
			get {
				if (_pdfDocument == null) {
					if (_pdfWriter == null) {
						// 需要写时再打开文件开写。这样的话，如果没有可合入的文件，就不会创建出空文件。
						if (_outputFileStream == null) {
							IMerger.EnsureFileCanExsist(outputfilepath);
							_outputFileStream = new(outputfilepath, FileMode.OpenOrCreate, FileAccess.Write);
						}
						WriterProperties writerProperties = new();
						writerProperties.SetFullCompressionMode(true);
						writerProperties.SetCompressionLevel(CompressionConstants.DEFAULT_COMPRESSION);
						_pdfWriter = new(_outputFileStream, writerProperties);
					}
					_pdfDocument = new(_pdfWriter);
					if (title != null)
						_pdfDocument.GetDocumentInfo().SetSubject(title);
				}
				return _pdfDocument;
			}
		}

		~PdfTarget() {
			Dispose(false);
		}

		internal bool IsUsed() {
			return _pdfDocument != null;
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
				_pdfDocument?.Close();
				_pdfWriter?.Dispose();
				_outputFileStream?.Dispose();
			}
			m_disposed = true;
		}
	}
}
