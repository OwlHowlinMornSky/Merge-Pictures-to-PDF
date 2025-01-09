using iText.IO.Image;
using SharpCompress;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using System.IO.MemoryMappedFiles;
using static PicMerge.IMerger;

namespace PicMerge {
	internal class ArchiveConverter(bool _keepStruct, Parameters param) : Merger(param), IMerger {

		private readonly bool m_keepStruct = _keepStruct;

		/// <summary>
		/// 用于接受压缩结果。
		/// </summary>
		private CompressTarget m_compressTarget = new();

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
					imageData = ReadImage(imgfile, m_compressTarget);
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
