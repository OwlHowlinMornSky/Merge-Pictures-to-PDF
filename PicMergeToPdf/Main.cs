using iText.IO.Image;
using iText.Kernel.Pdf;
using iText.Kernel.Geom;
using iText.Kernel.Pdf.Canvas;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using System.IO.MemoryMappedFiles;

namespace PicMerge {
	/// <summary>
	/// 合成器实现：普通串行。
	/// 目前，该类构造一个只能运行一次。
	/// </summary>
	/// <param name="finish1img">完成一个文件的回调</param>
	/// <param name="pageSizeType">页面大小类型</param>
	/// <param name="pagesizex">页面大小宽</param>
	/// <param name="pagesizey">页面大小高</param>
	/// <param name="compress">是否压缩所有图片</param>
	public class Main(Action finish1img, int pageSizeType = 2, int pagesizex = 0, int pagesizey = 0, bool compress = true) : IMerger {

		/// <summary>
		/// 完成一张图片（其实是一个文件，不论是否是图片）的回调。
		/// </summary>
		private readonly Action FinishOneImg = finish1img;
		/// <summary>
		/// 是否压缩所有图片。
		/// </summary>
		private readonly bool m_compress = compress;
		/// <summary>
		/// 页面大小类型。
		/// </summary>
		private readonly int m_pageSizeType = pageSizeType;
		/// <summary>
		/// 页面大小宽。使用第一张图片的尺寸时需要修改，所以不能只读。
		/// </summary>
		private float m_pagesizex = pagesizex;
		/// <summary>
		/// 页面大小高。使用第一张图片的尺寸时需要修改，所以不能只读。
		/// </summary>
		private float m_pagesizey = pagesizey;

		/// <summary>
		/// 内存映射文件设定的最大大小。
		/// </summary>
		private const long MapFileSize = 0x04000000;
		/// <summary>
		/// 用于接受压缩结果的内存映射文件。首次使用时创建。
		/// </summary>
		private MemoryMappedFile? m_mapfile = null;
		/// <summary>
		/// 压缩器。首次使用时创建。
		/// </summary>
		private PicCompress.Compressor? m_compressor = null;

		/// <summary>
		/// 输出——文件流。首次使用时创建。
		/// </summary>
		private FileStream? m_outputFileStream = null;
		/// <summary>
		/// 输出——填写器。首次使用时创建。
		/// </summary>
		private PdfWriter? m_pdfWriter = null;
		/// <summary>
		/// 输出——文档。首次使用时创建。
		/// </summary>
		private PdfDocument? m_pdfDocument = null;

		~Main() {
			Dispose(false);
		}

		/// <summary>
		/// 合并文件。此方法文件级串行，即依次读取并处理图片。
		/// </summary>
		/// <param name="outputfilepath">输出文件路径</param>
		/// <param name="files">输入文件的列表</param>
		/// <param name="title">内定标题</param>
		/// <returns>无法合入的文件的列表</returns>
		public virtual List<string> Process(string outputfilepath, List<string> files, string? title = null) {
			/// 无法合入的文件的列表。
			List<string> failed = [];
			try {
				Func<string, ImageData?> load = m_compress ? LoadImage_Compress : LoadImage_Direct;

				/// 先扫到可以处理的文件。
				int i = 0;
				ImageData? imageData = null;
				for (; i < files.Count; ++i) {
					string file = files[i];
					imageData = load(file);
					if (imageData != null) {
						break;
					}
					failed.Add(file);
					FinishOneImg();
				}
				if (imageData == null) {
					return [];
				}
				/// 再打开文件开写。这样的话，如果没有可合入的文件，就不会创建出pdf。
				if (m_pdfDocument == null) {
					if (m_pdfWriter == null) {
						// 需要写时再打开文件开写。这样的话，如果没有可合入的文件，就不会创建出空文件。
						if (m_outputFileStream == null) {
							IMerger.EnsureFileCanExsist(outputfilepath);
							m_outputFileStream = new(outputfilepath, FileMode.OpenOrCreate, FileAccess.Write);
						}
						WriterProperties writerProperties = new();
						writerProperties.SetFullCompressionMode(true);
						writerProperties.SetCompressionLevel(CompressionConstants.DEFAULT_COMPRESSION);
						m_pdfWriter = new(m_outputFileStream, writerProperties);
					}
					m_pdfDocument = new(m_pdfWriter);
					if (title != null)
						m_pdfDocument.GetDocumentInfo().SetSubject(title);
				}
				if (false == AddImage(imageData, m_pdfDocument)) {
					failed.Add(files[i]);
				}
				FinishOneImg();
				for (++i; i < files.Count; ++i) {
					string file = files[i];
					imageData = load(file);
					if (imageData == null) {
						failed.Add(file);
						FinishOneImg();
						continue;
					}
					if (false == AddImage(imageData, m_pdfDocument)) {
						failed.Add(file);
					}
					FinishOneImg();
				}
				m_pdfDocument?.Close();
				m_pdfWriter?.Close();
				m_outputFileStream?.Close();
			}
			catch (Exception ex) {
				failed = ["An Exception Occurred:", ex.GetType().ToString(), ex.Message, ex.Source ?? "", ex.StackTrace ?? ""];
			}
			return failed;
		}

		/// <summary>
		/// 压缩全部图片时的加载逻辑。
		/// </summary>
		/// <param name="file">欲加载的文件</param>
		/// <returns>加载出的数据，或者 null 若无法加载</returns>
		private ImageData? LoadImage_Compress(string file) {
			ImageData? imageData = null;

			// 尝试利用 Caesium-Iodine 压缩（压缩为 80% 的 JPG）
			try {
				if (m_compressor == null) {
					m_mapfile ??= MemoryMappedFile.CreateNew(null, MapFileSize);
					m_compressor = new(m_mapfile.SafeMemoryMappedFileHandle.DangerousGetHandle(), MapFileSize);
				}
				int len = m_compressor.Compress(file);
#pragma warning disable CS8602 // 解引用可能出现空引用。
				using var mapstream = m_mapfile.CreateViewStream();
#pragma warning restore CS8602 // 解引用可能出现空引用。
				using BinaryReader br = new(mapstream);
				imageData = ImageDataFactory.Create(br.ReadBytes(len));
				br.Close();
				mapstream.Close();
			}
			catch (Exception) { }
			if (imageData != null)
				return imageData;

			// 若不支持则 尝试 直接加载
			try {
				imageData = ImageDataFactory.Create(file);
			}
			catch (Exception) { }
			if (imageData != null)
				return imageData;

			// 若不支持则 尝试利用 ImageSharp 压缩（压缩为 GIF）
			try {
				GifEncoder gifEncoder = new() {
					SkipMetadata = true
				};
				using Image image = Image.Load(file);
				using MemoryStream imgSt = new();
				image.SaveAsGif(imgSt, gifEncoder);
				imageData = ImageDataFactory.Create(imgSt.ToArray());
				imgSt.Close();
			}
			catch (Exception) { }

			return imageData;
		}

		/// <summary>
		/// 直接读取时（不全部压缩）的加载逻辑。
		/// </summary>
		/// <param name="file">欲加载的文件</param>
		/// <returns>加载出的数据，或者 null 若无法加载</returns>
		private ImageData? LoadImage_Direct(string file) {
			ImageData? imageData = null;

			// 尝试 直接加载
			try {
				imageData = ImageDataFactory.Create(file);
			}
			catch (Exception) { }
			if (imageData != null)
				return imageData;

			// 若不支持则 尝试利用 Caesium-Iodine 压缩（压缩为 80% 的 JPG）
			try {
				if (m_compressor == null) {
					m_mapfile ??= MemoryMappedFile.CreateNew(null, MapFileSize);
					m_compressor = new(m_mapfile.SafeMemoryMappedFileHandle.DangerousGetHandle(), MapFileSize);
				}
				int len = m_compressor.Compress(file);
#pragma warning disable CS8602 // 解引用可能出现空引用。
				using var mapstream = m_mapfile.CreateViewStream();
#pragma warning restore CS8602 // 解引用可能出现空引用。
				using BinaryReader br = new(mapstream);
				imageData = ImageDataFactory.Create(br.ReadBytes(len));
				br.Close();
				mapstream.Close();
			}
			catch (Exception) { }
			if (imageData != null)
				return imageData;

			// 若不支持则 尝试利用 ImageSharp 压缩（压缩为 GIF）
			try {
				GifEncoder gifEncoder = new() {
					SkipMetadata = true
				};
				using Image image = Image.Load(file);
				using MemoryStream imgSt = new();
				image.SaveAsGif(imgSt, gifEncoder);
				imageData = ImageDataFactory.Create(imgSt.ToArray());
				imgSt.Close();
			}
			catch (Exception) { }

			return imageData;
		}

		/// <summary>
		/// 向 PDF 添加一页图片。
		/// </summary>
		/// <param name="imageData">图片数据</param>
		/// <param name="pdfDocument">PDF文件数据</param>
		/// <returns>是否成功</returns>
		private bool AddImage(ImageData imageData, PdfDocument pdfDocument) {
			try {
				PageSize pageSize;
				PageSize imageSize;
				float width = imageData.GetWidth();
				float height = imageData.GetHeight();
				switch (m_pageSizeType) {
				default:
				case 1: // 与图片大小一致
					imageSize = new(width, height);
					pageSize = imageSize;
					break;
				case 2: // 固定宽度
					if (m_pagesizex == 0) {
						m_pagesizex = width;
					}
					imageSize = new(m_pagesizex, m_pagesizex / width * height);
					pageSize = imageSize;
					break;
				case 3: // 固定大小
					if (m_pagesizex == 0 || m_pagesizey == 0) {
						m_pagesizex = width;
						m_pagesizey = height;
					}
					pageSize = new(m_pagesizex, m_pagesizey);
					float r = float.Min(
					1.0f * m_pagesizex / width,
					1.0f * m_pagesizey / height
				);
					imageSize = new(width * r, height * r);
					imageSize.SetX((pageSize.GetWidth() - imageSize.GetWidth()) / 2.0f);
					imageSize.SetY((pageSize.GetHeight() - imageSize.GetHeight()) / 2.0f);
					break;
				}
				pageSize.SetWidth(pageSize.GetWidth());
				pageSize.SetHeight(pageSize.GetHeight());
				PdfPage page = pdfDocument.AddNewPage(pageSize);
				PdfCanvas canvas = new(page, true);
				canvas.AddImageFittedIntoRectangle(imageData, imageSize, true);
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
				m_pdfDocument?.Close();
				m_pdfWriter?.Dispose();
				m_outputFileStream?.Dispose();

				m_compressor?.Dispose();
				m_mapfile?.Dispose();
			}
			m_disposed = true;
		}
	}
}

