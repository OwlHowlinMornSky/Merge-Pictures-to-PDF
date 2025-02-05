using iText.IO.Image;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;

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
						_pdfDocument.GetDocumentInfo().SetTitle(title);
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

		/// <summary>
		/// 向 PDF 添加一页图片。
		/// </summary>
		/// <param name="imageData">图片数据</param>
		/// <param name="pdfDocument">PDF文件数据</param>
		/// <returns>是否成功</returns>
		internal bool AddImage(in ImageData imageData, in PageParam param, int index = -1) {
			index++;
			bool fixedWidth = (param.fixedType & 1) != 0 && param.width >= 10;
			bool fixedHeight = (param.fixedType & 2) != 0 && param.height >= 10;
			try {
				PageSize pageSize;
				PageSize imageSize;
				float width = imageData.GetWidth();
				float height = imageData.GetHeight();

				if (fixedWidth && fixedHeight) { // 固定大小
					pageSize = new(param.width, param.height);
					float r = float.Min(
						1.0f * param.width / width,
						1.0f * param.height / height
					);
					imageSize = new(width * r, height * r);
					imageSize.SetX((pageSize.GetWidth() - imageSize.GetWidth()) / 2.0f);
					imageSize.SetY((pageSize.GetHeight() - imageSize.GetHeight()) / 2.0f);
				}
				else if (fixedWidth) { // 固定宽度
					imageSize = new(param.width, param.width / width * height);
					pageSize = imageSize;
				}
				else if (fixedHeight) { // 固定高度
					imageSize = new(param.height / height * width, param.height);
					pageSize = imageSize;
				}
				else { // 与图片大小一致
					imageSize = new(width, height);
					pageSize = imageSize;
				}

				PdfPage page = (index < 1 || index > Document.GetNumberOfPages()) ? Document.AddNewPage(pageSize) : Document.AddNewPage(index, pageSize);
				PdfCanvas canvas = new(page);
				canvas.AddImageFittedIntoRectangle(imageData, imageSize, false);
			}
			catch (Exception) {
				return false;
			}
			return true;
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
