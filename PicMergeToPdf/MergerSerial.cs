using iText.IO.Image;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using System.IO.MemoryMappedFiles;
using static PicMerge.IMerger;

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
	internal class MergerSerial(Action finish1img, Parameters param) : IMerger {

		/// <summary>
		/// 完成一张图片（其实是一个文件，不论是否是图片）的回调。
		/// </summary>
		private readonly Action FinishOneImg = finish1img;
		/// <summary>
		/// 合并之参数。使用第一张图片的尺寸时需要修改，所以不能只读。
		/// </summary>
		private Parameters m_param = param;

		/// <summary>
		/// 用于接受压缩结果的内存映射文件。首次使用时创建。
		/// </summary>
		private MemoryMappedFile? m_mapfile = null;
		/// <summary>
		/// 压缩器。首次使用时创建。
		/// </summary>
		private PicCompress.Compressor? m_compressor = null;

		~MergerSerial() {
			Dispose(false);
		}

		/// <summary>
		/// 合并文件。此方法文件级串行，即依次读取并处理图片。
		/// </summary>
		/// <param name="outputfilepath">输出文件路径</param>
		/// <param name="files">输入文件的列表</param>
		/// <param name="title">内定标题</param>
		/// <returns>无法合入的文件的列表</returns>
		public virtual List<FailedFile> Process(string outputfilepath, List<string> files, string? title = null) {
			/// 无法合入的文件的列表。
			List<FailedFile> failed = [];
			try {
				Func<string, ImageData?> load = m_param.compress ? LoadImage_Compress : LoadImage_Direct;
				using PdfTarget pdfTarget = new(outputfilepath, title);

				/// 先扫到可以处理的文件。
				int i = 0;
				ImageData? imageData = null;
				for (; i < files.Count; ++i) {
					string file = files[i];
					imageData = load(file);
					if (imageData != null) {
						break;
					}
					failed.Add(new FailedFile(0x3001, file, "Unsupported type."));
					FinishOneImg();
				}
				if (imageData == null) {
					return failed;
				}
				/// 再打开文件开写。这样的话，如果没有可合入的文件，就不会创建出pdf。
				if (!pdfTarget.AddImage(imageData, ref m_param)) {
					failed.Add(new FailedFile(0x4001, files[i], "Unable to add into pdf [iText internal problem]."));
				}
				FinishOneImg();
				for (++i; i < files.Count; ++i) {
					string file = files[i];
					imageData = load(file);
					if (imageData == null) {
						failed.Add(new FailedFile(0x3001, file, "Unsupported type."));
						FinishOneImg();
						continue;
					}
					if (!pdfTarget.AddImage(imageData, ref m_param)) {
						failed.Add(new FailedFile(0x4001, files[i], "Unable to add into pdf [iText internal problem]."));
					}
					FinishOneImg();
				}
			}
			catch (Exception ex) {
				failed = [new FailedFile(0x4002, ex.Message, ex.Source ?? "")];
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
				int len = m_compressor.Compress(file, m_param.compressType, m_param.compressQuality);
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
				int len = m_compressor.Compress(file, m_param.compressType, m_param.compressQuality);
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

