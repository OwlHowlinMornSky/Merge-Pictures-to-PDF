using iText.IO.Image;
using SharpCompress;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using System.IO.MemoryMappedFiles;
using static PicMerge.IMerger;

namespace PicMerge {
	/// <summary>
	/// 合成器实现：转换压缩文件。
	/// 目前，该类构造一个只能运行一次。
	/// </summary>
	/// <param name="_keepStruct">保持压缩包内结构</param>
	/// <param name="param">参数</param>
	/// <err frag="0x8003" ack="0003"></err>
	internal class MergerArchive(bool _keepStruct, Parameters param) : Merger(param), IMerger {

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
			string archivefile = files[0];
			try {
				if (!ArchiveFactory.IsArchive(archivefile, out ArchiveType? archiveType)) {
					result.Add(new FileResult(0x80030002, "", $"Unsupported type: {archiveType}."));
					return result;
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
						result.Add(new FileResult(0x80030003, "", "A file whose name unknown."));
						continue;
					}
					string file = entry.Key;

					using (EntryStream entryStream = reader.OpenEntryStream()) {
						imgstream.Seek(0, SeekOrigin.Begin);
						entryStream.TransferTo(imgstream);
						entryStream.Close();
					}

					ImageData? imageData = ReadImage(imgfile, m_compressTarget);
					if (imageData == null) {
						result.Add(new FileResult(0x80030004, file, StrUnsupported));
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
						result.Add(new FileResult(0x80030005, file, "Unable to open target PDF file."));
						continue;
					}

					if (!pdfTarget.AddImage(imageData, ref m_param)) {
						result.Add(new FileResult(0x80030006, file, StrFailedToAdd));
						continue;
					}

					result.Add(new FileResult(0x1, file));
				}

				foreach (var pair in pdfs) {
					pair.Value.Dispose();
				}
				pdfs.Clear();
			}
			catch (Exception ex) {
				result.Add(new FileResult(0x80030007, archivefile, (ex.Source ?? "") + ", " + ex.Message));
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
	}
}
