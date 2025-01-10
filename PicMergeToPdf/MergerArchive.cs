using iText.IO.Image;
using SharpCompress;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using static PicMerge.IMerger;

namespace PicMerge {
	/// <summary>
	/// 合成器实现：转换压缩文件。
	/// 目前，该类构造一个只能运行一次。
	/// </summary>
	/// <param name="_keepStruct">保持压缩包内结构</param>
	/// <param name="param">参数</param>
	/// <err frag="0x8003" ack="0009"></err>
	internal partial class MergerArchive(Action finish1img, bool _keepStruct, Parameters param) : Merger(param), IMerger {

		private readonly bool m_keepStruct = _keepStruct;
		/// <summary>
		/// 完成一张图片（其实是一个文件，不论是否是图片）的回调。
		/// </summary>
		private readonly Action FinishOneImg = finish1img;

		/// <summary>
		/// 用于接受压缩结果。
		/// </summary>
		private readonly CompressTarget m_compressTarget = new();

		private Dictionary<string, Tuple<PdfTarget, List<string>>> m_pdfs = [];

		private List<FileResult> m_result = [];

		private string m_outputDir = "";

		private string m_archivePath = "";

		private long m_totalSize = 0;
		private long m_mergedSize = 0;

		private long m_prevVirtualFinishedImgCnt = 0;

		~MergerArchive() {
			Dispose(false);
		}

		/// <summary>
		/// 合并文件。内部串行并行由具体对象决定。
		/// </summary>
		/// <param name="outputfilepath">输出文件路径</param>
		/// <param name="files">输入文件的列表</param>
		/// <param name="title">内定标题</param>
		/// <returns>无法合入的文件的列表</returns>
		public virtual List<FileResult> Process(string outputfilepath, List<string> files, string? title = null) {
			if (files.Count != 1) {
				m_result.Add(new FileResult(0x80030001, "", "Only support for one archive one time."));
				return m_result;
			}
			m_archivePath = files[0];
			m_outputDir = outputfilepath;
			try {
				if (!ArchiveFactory.IsArchive(m_archivePath, out ArchiveType? archiveType)) {
					m_result.Add(new FileResult(0x80030002, m_archivePath, $"Unsupported type: {archiveType}."));
					return m_result;
				}

				using IArchive archive = ArchiveFactory.Open(m_archivePath);
				m_totalSize = archive.TotalUncompressSize;
				if (m_totalSize <= 0) {
					m_result.Add(new FileResult(0x80030008, m_archivePath, "Size of archive is zero."));
					return m_result;
				}

				Task? prevTask = null;
				long prevSize = 0;

				using IReader reader = archive.ExtractAllEntries();
				while (reader.MoveToNextEntry()) {
					IEntry entry = reader.Entry;
					if (entry.IsDirectory)
						continue;

					if (string.IsNullOrEmpty(entry.Key)) {
						m_result.Add(new FileResult(0x80030003, m_archivePath, "A file whose name unknown."));
						continue;
					}
					string imgKey = entry.Key;

					MemoryMappedFile imgMapFile = MemoryMappedFile.CreateNew(null, MapFileSize, MemoryMappedFileAccess.ReadWrite);
					using (MemoryMappedViewStream imgView = imgMapFile.CreateViewStream()) {
						using EntryStream entryStream = reader.OpenEntryStream();
						imgView.Seek(0, SeekOrigin.Begin);
						entryStream.TransferTo(imgView);
						entryStream.Close();
					}
					prevTask?.Wait();
					m_mergedSize += prevSize;
					UpdateSizeBar();
					prevTask = CompressAndAddAsync(outputfilepath, imgKey, imgMapFile);
					prevSize = entry.Size;
				}
				prevTask?.Wait();
				m_mergedSize += prevSize;
				UpdateSizeBar();

				foreach (var pair in m_pdfs) {
					pair.Value.Item1.Dispose();
				}
				m_pdfs.Clear();
			}
			catch (Exception ex) {
				m_result.Add(new FileResult(0x80030007, m_archivePath, (ex.Source ?? "") + ", " + ex.Message));
			}

			return m_result;
		}

		private async Task CompressAndAddAsync(string? title, string imgKey, MemoryMappedFile imgMapFile) {
			ImageData? imageData = await Task.Run(() => { return ReadImage(imgMapFile, m_compressTarget); });
			if (imageData == null) {
				m_result.Add(new FileResult(0x80030004, imgKey, StrUnsupported));
				return;
			}

			string imgDirChain = Path.GetDirectoryName(imgKey) ?? "";

			if (!m_pdfs.ContainsKey(imgDirChain)) {
				string pdfDir = Path.Combine(m_outputDir, imgDirChain);
				string pdfName;
				if (string.IsNullOrEmpty(imgDirChain)) {
					pdfName = "FilesAtRoot";
				}
				else {
					pdfName = Path.GetFileName(imgDirChain);
					pdfDir = Path.GetDirectoryName(pdfDir) ?? pdfDir;
				}
				if (!m_keepStruct)
					pdfDir = m_outputDir;
				string pdfPath = EnumFileName(pdfDir, pdfName, ".pdf");
				EnsureFileCanExsist(pdfPath);
				m_pdfs.Add(imgDirChain, Tuple.Create<PdfTarget, List<string>>(new PdfTarget(pdfPath, Path.Combine(title ?? Path.GetFileNameWithoutExtension(m_archivePath), imgDirChain)), []));
			}
			if (!m_pdfs.TryGetValue(imgDirChain, out var tuple) || tuple == null) {
				m_result.Add(new FileResult(0x80030005, imgKey, "Unable to open target PDF file."));
				return;
			}
			var pdfTarget = tuple.Item1;
			var imageNames = tuple.Item2;
			string curImgName = Path.GetFileName(imgKey);

			int index = imageNames.Count;
			for (; index > 0; index--) {
				var imageName = imageNames[index - 1];
				if (StrCmpLogicalW(imageName, curImgName) <= 0) {
					break;
				}
			}
			tuple.Item2.Insert(index, curImgName);

			if (!pdfTarget.AddImage(imageData, ref m_param, index)) {
				m_result.Add(new FileResult(0x80030006, imgKey, StrFailedToAdd));
				return;
			}

			m_result.Add(new FileResult(0x1, imgKey));

			imgMapFile.Dispose();
		}

		private void UpdateSizeBar() {
			long virtualImgCnt = m_mergedSize * 100 / m_totalSize;
			if (virtualImgCnt > 100)
				virtualImgCnt = 100;
			while (virtualImgCnt > m_prevVirtualFinishedImgCnt) {
				FinishOneImg();
				m_prevVirtualFinishedImgCnt++;
			}
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
				m_compressTarget.Dispose();
			}
			m_disposed = true;
		}

		[LibraryImport("Shlwapi.dll", EntryPoint = "StrCmpLogicalW", StringMarshalling = StringMarshalling.Utf16)]
		[return: MarshalAs(UnmanagedType.I4)]
		private static partial int StrCmpLogicalW(string psz1, string psz2);
	}
}
