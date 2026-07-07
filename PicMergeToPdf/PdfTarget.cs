using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System.IO;
using System.Reflection.Metadata;
using System.Xml.Linq;

namespace PicMerge;

internal class PdfTarget(string _outputPath, string? _title) : IDisposable {

	private readonly string outputfilepath = _outputPath;
	private readonly string? title = _title;

	/// <summary>
	/// 文件流。首次使用时创建。
	/// </summary>
	private FileStream? _outputFileStream = null;
	/// <summary>
	/// 文档。首次使用时创建。
	/// </summary>
	private PdfDocument? _pdfDocument = null;

	internal PdfDocument Document {
		get {
			if (_pdfDocument == null) {
				// 需要写时再打开文件开写。这样的话，如果没有可合入的文件，就不会创建出空文件。
				if (_outputFileStream == null) {
					IMerger.EnsureFileCanExsist(outputfilepath);
					_outputFileStream = new(outputfilepath, FileMode.OpenOrCreate, FileAccess.Write);
				}
				_pdfDocument = new(_outputFileStream);
				_pdfDocument.Options.CompressContentStreams = true;
				_pdfDocument.Options.FlateEncodeMode = PdfFlateEncodeMode.Default;
				_pdfDocument.Options.EnableCcittCompressionForBilevelImages = true;
				_pdfDocument.Options.UseFlateDecoderForJpegImages =
					PdfUseFlateDecoderForJpegImages.Automatic;
				if (title != null)
					_pdfDocument.Info.Title = title;
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
	/// <param name="stream_to_image">图片数据流</param>
	/// <param name="param">页面参数</param>
	/// <param name="index">图片插入在第几页，从1开始</param>
	/// <returns>是否成功</returns>
	internal bool AddImage(in Stream stream_to_image, in PageParam param, int img_w_override = -1, int img_h_override = -1) {
		bool fixedWidth = (param.fixedType & PageParam.FixedType.WidthFixed) != 0 && param.width >= 10;
		bool fixedHeight = (param.fixedType & PageParam.FixedType.HeightFixed) != 0 && param.height >= 10;
		try {
			using var image = XImage.FromStream(stream_to_image);

			// 添加新页面并设置尺寸（单位：点）
			var page = Document.AddPage();
			var page_w = page.Width;
			var page_h = page.Height;

			double img_w = img_w_override > 0 ? img_w_override : image.PixelWidth;
			double img_h = img_h_override > 0 ? img_h_override : image.PixelHeight;

			if (fixedWidth && fixedHeight) { // 固定大小
				page_w.Point = param.width;
				page_h.Point = param.height;
			}
			else if (fixedWidth) { // 固定宽度
				page_w.Point = param.width;
				page_h.Point = param.width / img_w * img_h;
			}
			else if (fixedHeight) { // 固定高度
				page_w.Point = param.height / img_h * img_w;
				page_h.Point = param.height;
			}
			else { // 与图片大小一致
				page_w.Point = img_w;
				page_h.Point = img_h;
			}

			float page_scale = 72.0f / param.dpi;
			page_w.Point *= page_scale;
			page_h.Point *= page_scale;

			// 绘制图片（铺满页面）
			using var gfx = XGraphics.FromPdfPage(page);
			gfx.DrawImage(image, 0, 0, page.Width.Point, page.Height.Point);
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
			_pdfDocument?.Dispose();
			_outputFileStream?.Close();
			_outputFileStream?.Dispose();
		}
		m_disposed = true;
	}
}
