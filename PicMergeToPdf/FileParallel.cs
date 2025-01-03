﻿using iText.IO.Image;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using SixLabors.ImageSharp.Formats.Gif;
using System.IO.MemoryMappedFiles;
using SixLabors.ImageSharp;

namespace PicMerge {
	/// <summary>
	/// 合成器实现：文件级并行。
	/// 目前，该类构造一个只能运行一次。
	/// </summary>
	/// <param name="finish1img">完成一个文件的回调</param>
	/// <param name="pageSizeType">页面大小类型</param>
	/// <param name="pagesizex">页面大小宽</param>
	/// <param name="pagesizey">页面大小高</param>
	/// <param name="compress">是否压缩所有图片</param>
	public class FileParallel(Action finish1img, int pageSizeType = 2, int pagesizex = 0, int pagesizey = 0, bool compress = true) : IMerger {

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
		/// 从Process输入的输入文件列表。
		/// </summary>
		private List<string> m_files = [];

		/// <summary>
		/// 已开始运行过的任务 的 计数。
		/// 用于 每个任务 在最开始 获取 自己该处理哪个文件（m_files[id]）。
		/// </summary>
		private int m_startedCnt = 0;
		/// <summary>
		/// 已开始运行过的任务计数 的 锁。
		/// </summary>
		private readonly object m_startedCntLock = new();
		/// <summary>
		/// 已将结果压入队列的任务 的 计数。
		/// 用于 每个任务 确定 是否 轮到自己 把结果入队 了，保证 入队顺序 就是 输入的文件顺序。
		/// </summary>
		private int m_loadedCnt = 0;
		/// <summary>
		/// 已将结果压入队列的任务计数 的 锁。
		/// </summary>
		private readonly object m_loadedCntLock = new();

		/// <summary>
		/// 加载结果的队列。
		/// </summary>
		private readonly Queue<ImageData?> m_loadedImg = [];
		/// <summary>
		/// 加载单个文件的任务 的 列表。
		/// </summary>
		private readonly List<Task> m_loadings = [];
		/// <summary>
		/// 多任务协作时，任务中sleep的 默认 毫秒数。
		/// </summary>
		private const int m_sleepMs = 20;

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

		~FileParallel() {
			Dispose(false);
		}

		/// <summary>
		/// 合并文件。此方法文件级并行，即并发加载处理，再依次加入结果。
		/// </summary>
		/// <param name="outputfilepath">输出文件路径</param>
		/// <param name="files">输入文件的列表</param>
		/// <param name="title">内定标题</param>
		/// <returns>无法合入的文件的列表</returns>
		public List<string> Process(string outputfilepath, List<string> files, string title = "") {
			m_files = files;
			/// 无法合入的文件的列表。
			List<string> failed = [];
			try {
				/// 按电脑核心数启动load。
				for (int i = 0, n = Environment.ProcessorCount; i < n; i++)
					StartNewLoad();

				List<ImageData?> imageDatas = [];
				int imgCnt = 0;
				bool havePdfOutput = false;
				while (true) {
					bool isEmpty = true;
					lock (m_loadedImg) {
						if (m_loadedImg.Count != 0) {
							isEmpty = false;
							while (m_loadedImg.Count > 0) {
								imageDatas.Add(m_loadedImg.Dequeue());
							}
						}
					}
					if (isEmpty) {
						Thread.Sleep(m_sleepMs);
						continue;
					}
					// Add Image.
					foreach (var imageData in imageDatas) {
						if (imageData == null) {
							failed.Add(files[imgCnt]);
							FinishOneImg();
							imgCnt++;
							continue;
						}
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
							m_pdfDocument.GetDocumentInfo().SetKeywords(title);
						}
						havePdfOutput = true;
						AddImage(imageData, m_pdfDocument);
						FinishOneImg();
						imgCnt++;
					}
					imageDatas.Clear();
					if (imgCnt >= files.Count)
						break;
				}

				Task.WaitAll([.. m_loadings]);
				m_loadings.Clear();

				if (!havePdfOutput) // 一个都没法合成的话返回空。
					failed.Clear();

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
		/// 开启一个新的加载图片任务。
		/// </summary>
		private void StartNewLoad() {
			lock (m_loadings) {
				m_loadings.Add(Task.Run(LoadOneProc));
			}
		}

		/// <summary>
		/// 加载一张图片的任务过程。
		/// </summary>
		private void LoadOneProc() {
			/// 取序号。
			int id;
			lock (m_startedCntLock) {
				if (m_startedCnt >= m_files.Count)
					return;
				id = m_startedCnt++;
			}
			string file = m_files[id];

			/// 加载并处理。
			ImageData? imageData =  m_compress ? LoadImage_Compress(file) : LoadImage_Direct(file);

			/// Add loaded data into queue.
			while (true) {
				lock (m_loadedCntLock) {
					/// My turn to enqueue.
					if (id == m_loadedCnt) {
						while (true) {
							lock (m_loadedImg) {
								/// Ensure queue is not too large.
								if (m_loadedImg.Count < Environment.ProcessorCount * 2) {
									m_loadedImg.Enqueue(imageData);
									break;
								}
							}
							Thread.Sleep(m_sleepMs);
						}
						m_loadedCnt++;
						break;
					}
				}
				Thread.Sleep(m_sleepMs);
			}

			/// Next Task.
			StartNewLoad();
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
				using MemoryMappedFile mapfile = MemoryMappedFile.CreateNew(null, MapFileSize);
				using PicCompress.Compressor compressor = new(mapfile.SafeMemoryMappedFileHandle.DangerousGetHandle(), MapFileSize);
				int len = compressor.Compress(file);
				using var mapstream = mapfile.CreateViewStream();
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
				using MemoryMappedFile mapfile = MemoryMappedFile.CreateNew(null, MapFileSize);
				using PicCompress.Compressor compressor = new(mapfile.SafeMemoryMappedFileHandle.DangerousGetHandle(), MapFileSize);
				int len = compressor.Compress(file);
				using var mapstream = mapfile.CreateViewStream();
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
			}
			m_disposed = true;
		}
	}
}
