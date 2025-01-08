using iText.IO.Image;
using SharpCompress;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using System.IO.MemoryMappedFiles;
using static PicMerge.IMerger;

namespace PicMerge {
	internal class ArchiveConverter(bool _keepStruct, Parameters param) : IMerger {

		/// <summary>
		/// 合并之参数。使用第一张图片的尺寸时需要修改，所以不能只读。
		/// </summary>
		private Parameters m_param = param;

		private readonly bool m_keepStruct = _keepStruct;

		/// <summary>
		/// 用于接受压缩结果的内存映射文件。首次使用时创建。
		/// </summary>
		private MemoryMappedFile? m_mapfile = null;
		/// <summary>
		/// 压缩器。首次使用时创建。
		/// </summary>
		private PicCompress.Compressor? m_compressor = null;

		private readonly List<FailedFile> m_failed = [];

		~ArchiveConverter() {
			Dispose(false);
		}

		/// <summary>
		/// 合并文件。内部串行并行由具体对象决定。
		/// </summary>
		/// <param name="outputfilepath">输出文件路径</param>
		/// <param name="files">输入文件的列表</param>
		/// <param name="title">内定标题</param>
		/// <returns>无法合入的文件的列表</returns>
		public virtual List<FailedFile> Process(string outputfilepath, List<string> files, string? title = null) {
			if (files.Count != 1) {
				return m_failed;
			}
			string archivefile = files[0];

			if (!ArchiveFactory.IsArchive(archivefile, out ArchiveType? archiveType)) {
				//Console.WriteLine($"\"{path}\" is not an archive.");
				return m_failed;
			}
			using MemoryMappedFile imgfile = MemoryMappedFile.CreateNew(null, MapFileSize, MemoryMappedFileAccess.ReadWrite);
			using MemoryMappedViewStream imgstream = imgfile.CreateViewStream();

			Dictionary<string, PdfTarget> pdfs = [];

			using IArchive archive = ArchiveFactory.Open(archivefile);
			using IReader reader = archive.ExtractAllEntries();
			while (reader.MoveToNextEntry()) {
				IEntry entry = reader.Entry;

				if (entry.IsDirectory)
					continue;
				if (string.IsNullOrEmpty(entry.Key)) {
					m_failed.Add(new FailedFile(0x3001, "", $"A file with name unknown."));
					continue;
				}
				string file = entry.Key;

				imgstream.Seek(0, SeekOrigin.Begin);

				using EntryStream entryStream = reader.OpenEntryStream();
				entryStream.TransferTo(imgstream);

				ImageData? imageData = null;
				try {
					/// 加载并处理。
					imageData = m_param.compress ? LoadImage_Compress(imgfile) : LoadImage_Direct(imgfile);
				}
				catch (Exception ex) {
					m_failed.Add(new FailedFile(0x3002, file, $"Failed to load image because: {ex.Message}"));
				}
				if (imageData == null) {
					m_failed.Add(new FailedFile(0x3003, file, $"Failed to load image."));
					continue;
				}

				string structure = Path.GetDirectoryName(file) ?? "";

				if (!pdfs.ContainsKey(structure)) {
					string fullstruct = Path.Combine(outputfilepath, structure);
					string outdir = m_keepStruct ? fullstruct : outputfilepath;
					string filename = EnumFileName(outdir, Path.GetFileNameWithoutExtension(file), ".pdf");
					EnsureFileCanExsist(filename);
					pdfs.Add(structure, new PdfTarget(filename, fullstruct));
				}
				if (!pdfs.TryGetValue(structure, out PdfTarget? pdfTarget) || pdfTarget == null) {
					m_failed.Add(new FailedFile(0x3004, file, "Unable to open target PDF file."));
					continue;
				}

				if (!pdfTarget.AddImage(imageData, ref m_param)) {
					m_failed.Add(new FailedFile(0x3005, file, "Unable to add into pdf [iText internal problem]."));
				}

				entryStream.Close();
			}

			foreach (var pair in pdfs) {
				pair.Value.Dispose();
			}
			pdfs.Clear();

			return m_failed;
		}

		/// <summary>
		/// 压缩全部图片时的加载逻辑。
		/// </summary>
		/// <param name="filepath">欲加载的文件路径</param>
		/// <returns>加载出的数据，或者 null 若无法加载</returns>
		private ImageData? LoadImage_Compress(MemoryMappedFile inFile) {
			ImageData? imageData = null;

			using var instream = inFile.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);

			var type = FileType.CheckType(instream);

			switch (type) {
			case FileType.Type.JPEG: // CSI, Img#, Direct.
			case FileType.Type.PNG:  // CSI, Img#, Direct.
			case FileType.Type.TIFF: // CSI, Img#, Direct.
			case FileType.Type.WEBP: // CSI, Img#.
				/// 尝试利用 Caesium-Iodine 压缩（压缩为 80% 的 JPG）
				try {
					if (m_compressor == null) {
						m_mapfile ??= MemoryMappedFile.CreateNew(null, MapFileSize);
						m_compressor = new(m_mapfile.SafeMemoryMappedFileHandle.DangerousGetHandle(), MapFileSize);
					}
					int len = m_compressor.CompressFrom(
						inFile.SafeMemoryMappedFileHandle.DangerousGetHandle(), instream.Length,
						m_param.compressType, m_param.compressQuality
					);
#pragma warning disable CS8602 // 解引用可能出现空引用。
					using var mapstream = m_mapfile.CreateViewStream();
#pragma warning restore CS8602 // 解引用可能出现空引用。
					using BinaryReader br = new(mapstream);
					imageData = ImageDataFactory.Create(br.ReadBytes(len));
					br.Close();
					mapstream.Close();
				}
				catch (Exception) {
					imageData = null;
					goto case FileType.Type.GIF;
				}
				break;
			case FileType.Type.BMP:  // Img#, Direct.
			case FileType.Type.GIF:  // Img#, Direct.
				/// 尝试利用 ImageSharp 压缩（压缩为 80% 的 JPG）
				try {
					instream.Seek(0, SeekOrigin.Begin);
					JpegEncoder encoder = new() {
						SkipMetadata = true,
						ColorType = JpegEncodingColor.Rgb,
						Quality = 80,
						Interleaved = false
					};
					using Image image = Image.Load(instream);
					using MemoryStream imgSt = new();
					image.SaveAsJpeg(imgSt, encoder);
					imageData = ImageDataFactory.Create(imgSt.ToArray());
					imgSt.Close();
				}
				catch (Exception) {
					imageData = null;
				}
				if (type == FileType.Type.WEBP)
					break;
				/// 尝试 直接加载
				try {
					instream.Seek(0, SeekOrigin.Begin);
					using BinaryReader br = new(instream);
					imageData = ImageDataFactory.Create(br.ReadBytes((int)instream.Length));
				}
				catch (Exception) {
					imageData = null;
				}
				goto default;
			//case FileType.Type.ZIP:  // Archive.
			//case FileType.Type._7ZIP:// Archive.
			//case FileType.Type.RAR:  // Archive.
			//	throw new FileType.ArchiveException();
			default:
				throw new NotImplementedException("Unsupported type.");
			}
			return imageData;
		}

		/// <summary>
		/// 直接读取时（不全部压缩）的加载逻辑。
		/// </summary>
		/// <param name="file">欲加载的文件</param>
		/// <returns>加载出的数据，或者 null 若无法加载</returns>
		private ImageData? LoadImage_Direct(MemoryMappedFile inFile) {
			ImageData? imageData = null;

			using var instream = inFile.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);

			var type = FileType.CheckType(instream);

			switch (type) {
			case FileType.Type.JPEG: // CSI, Img#, Direct.
			case FileType.Type.PNG:  // CSI, Img#, Direct.
			case FileType.Type.TIFF: // CSI, Img#, Direct.
			case FileType.Type.BMP:  // Img#, Direct.
			case FileType.Type.GIF:  // Img#, Direct.
				/// 尝试 直接加载
				try {
					instream.Seek(0, SeekOrigin.Begin);
					using BinaryReader br = new(instream);
					imageData = ImageDataFactory.Create(br.ReadBytes((int)instream.Length));
				}
				catch (Exception) {
					imageData = null;
					goto case FileType.Type.WEBP;
				}
				break;
			case FileType.Type.WEBP: // CSI, Img#.
				/// 尝试利用 Caesium-Iodine 压缩（压缩为 80% 的 JPG）
				try {
					if (m_compressor == null) {
						m_mapfile ??= MemoryMappedFile.CreateNew(null, MapFileSize);
						m_compressor = new(m_mapfile.SafeMemoryMappedFileHandle.DangerousGetHandle(), MapFileSize);
					}
					int len = m_compressor.CompressFrom(
						inFile.SafeMemoryMappedFileHandle.DangerousGetHandle(), instream.Length,
						m_param.compressType, m_param.compressQuality
					);
#pragma warning disable CS8602 // 解引用可能出现空引用。
					using var mapstream = m_mapfile.CreateViewStream();
#pragma warning restore CS8602 // 解引用可能出现空引用。
					using BinaryReader br = new(mapstream);
					imageData = ImageDataFactory.Create(br.ReadBytes(len));
					br.Close();
					mapstream.Close();
				}
				catch (Exception) {
					imageData = null;
				}
				/// 尝试利用 ImageSharp 压缩（压缩为 80% 的 JPG）
				try {
					instream.Seek(0, SeekOrigin.Begin);
					JpegEncoder encoder = new() {
						SkipMetadata = true,
						ColorType = JpegEncodingColor.Rgb,
						Quality = 80,
						Interleaved = false
					};
					using Image image = Image.Load(instream);
					using MemoryStream imgSt = new();
					image.SaveAsJpeg(imgSt, encoder);
					imageData = ImageDataFactory.Create(imgSt.ToArray());
					imgSt.Close();
				}
				catch (Exception) {
					imageData = null;
				}
				goto default;
			//case FileType.Type.ZIP:  // Archive.
			//case FileType.Type._7ZIP:// Archive.
			//case FileType.Type.RAR:  // Archive.
			//	throw new FileType.ArchiveException();
			default:
				throw new NotImplementedException("Unsupported type.");
			}
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
