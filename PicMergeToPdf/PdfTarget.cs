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
		internal bool AddImage(ImageData imageData, ref IMerger.Parameters param, int index = -1) {
			index++;
			try {
				PageSize pageSize;
				PageSize imageSize;
				float width = imageData.GetWidth();
				float height = imageData.GetHeight();
				switch (param.pageSizeType) {
				default:
				case 1: // 与图片大小一致
					imageSize = new(width, height);
					pageSize = imageSize;
					break;
				case 2: // 固定宽度
					if (param.pagesizex == 0) {
						param.pagesizex = width;
					}
					imageSize = new(param.pagesizex, param.pagesizex / width * height);
					pageSize = imageSize;
					break;
				case 3: // 固定大小
					if (param.pagesizex == 0 || param.pagesizey == 0) {
						param.pagesizex = width;
						param.pagesizey = height;
					}
					pageSize = new(param.pagesizex, param.pagesizey);
					float r = float.Min(
					1.0f * param.pagesizex / width,
					1.0f * param.pagesizey / height
				);
					imageSize = new(width * r, height * r);
					imageSize.SetX((pageSize.GetWidth() - imageSize.GetWidth()) / 2.0f);
					imageSize.SetY((pageSize.GetHeight() - imageSize.GetHeight()) / 2.0f);
					break;
				}
				pageSize.SetWidth(pageSize.GetWidth());
				pageSize.SetHeight(pageSize.GetHeight());
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
