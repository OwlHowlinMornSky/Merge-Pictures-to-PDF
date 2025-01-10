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
	/// <err frag="0x8003" ack="0003"></err>
	internal partial class MergerArchive(bool _keepStruct, Parameters param) : Merger(param), IMerger {

		private readonly bool m_keepStruct = _keepStruct;

		/// <summary>
		/// 用于接受压缩结果。
		/// </summary>
		private readonly CompressTarget m_compressTarget = new();

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
			List<FileResult> result = [];

			if (files.Count != 1) {
				result.Add(new FileResult(0x80030001, "", "Only support for one archive one time."));
				return result;
			}
			string archivePath = files[0];
			try {
				if (!ArchiveFactory.IsArchive(archivePath, out ArchiveType? archiveType)) {
					result.Add(new FileResult(0x80030002, "", $"Unsupported type: {archiveType}."));
					return result;
				}

				using MemoryMappedFile imgMapFile = MemoryMappedFile.CreateNew(null, MapFileSize, MemoryMappedFileAccess.ReadWrite);
				using MemoryMappedViewStream imgView = imgMapFile.CreateViewStream();

				Dictionary<string, Tuple<PdfTarget, List<string>>> pdfs = [];

				using IArchive archive = ArchiveFactory.Open(archivePath);
				using IReader reader = archive.ExtractAllEntries();
				while (reader.MoveToNextEntry()) {
					IEntry entry = reader.Entry;
					if (entry.IsDirectory)
						continue;

					if (string.IsNullOrEmpty(entry.Key)) {
						result.Add(new FileResult(0x80030003, "", "A file whose name unknown."));
						continue;
					}
					string imgKey = entry.Key;

					using (EntryStream entryStream = reader.OpenEntryStream()) {
						imgView.Seek(0, SeekOrigin.Begin);
						entryStream.TransferTo(imgView);
						entryStream.Close();
					}

					ImageData? imageData = ReadImage(imgMapFile, m_compressTarget);
					if (imageData == null) {
						result.Add(new FileResult(0x80030004, imgKey, StrUnsupported));
						continue;
					}

					string imgDirChain = Path.GetDirectoryName(imgKey) ?? "";

					if (!pdfs.ContainsKey(imgDirChain)) {
						string pdfDir = Path.Combine(outputfilepath, imgDirChain);
						string pdfName;
						if (string.IsNullOrEmpty(imgDirChain)) {
							pdfName = "FilesAtRoot";
						}
						else {
							pdfName = Path.GetFileName(imgDirChain);
							pdfDir = Path.GetDirectoryName(pdfDir) ?? pdfDir;
						}
						if (!m_keepStruct)
							pdfDir = outputfilepath;
						string pdfPath = EnumFileName(pdfDir, pdfName, ".pdf");
						EnsureFileCanExsist(pdfPath);
						pdfs.Add(imgDirChain, Tuple.Create<PdfTarget, List<string>>(new PdfTarget(pdfPath, Path.Combine(title ?? Path.GetFileNameWithoutExtension(archivePath), imgDirChain)), []));
					}
					if (!pdfs.TryGetValue(imgDirChain, out var tuple) || tuple == null) {
						result.Add(new FileResult(0x80030005, imgKey, "Unable to open target PDF file."));
						continue;
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
						result.Add(new FileResult(0x80030006, imgKey, StrFailedToAdd));
						continue;
					}

					result.Add(new FileResult(0x1, imgKey));
				}

				foreach (var pair in pdfs) {
					pair.Value.Item1.Dispose();
				}
				pdfs.Clear();
			}
			catch (Exception ex) {
				result.Add(new FileResult(0x80030007, archivePath, (ex.Source ?? "") + ", " + ex.Message));
			}

			return result;
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
